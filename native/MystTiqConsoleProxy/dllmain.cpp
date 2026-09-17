// MystTiq Console Proxy -- v0.7.57.0 Native Console Capture (PalServer DLL Proxy Logger)
// Console-write hook implemented v0.7.72.0.
//
// Why this exists: PalServer's own console output isn't reachable through normal process
// redirection. Live testing (see docs/architecture/v0.7.57.0-native-console-capture.md) confirmed
// that stdio-pipe redirection captures nothing even when the game process is launched directly, and
// that "-ABSLOG" produces no file -- Unreal's own file-logging subsystem appears compiled out of
// this build (NO_LOGGING). The one proven, working technique other tools in this space use
// (PalServerLogger, and the load-order UE4SS/PalDefender already rely on) is DLL proxying: ship a
// DLL with the same name as one the game already imports, forward its real exports to the genuine
// system DLL so the game behaves identically, and use the loader's own DllMain entry point to run
// arbitrary code inside the game's process.
//
// Target DLL: DSOUND.dll. Confirmed via a live `dumpbin /imports` against the real
// PalServer-Win64-Shipping-Cmd.exe that it imports exactly 6 functions from DSOUND.dll, all by
// ordinal, none of which UE4SS or PalDefender's own d3d9.dll-based proxy already occupies -- chosen
// specifically to avoid colliding with either.
//
// v0.7.72.0: the originally-scoped hook target, Unreal's own FOutputDevice::LogfImpl, was
// deliberately never implemented -- it isn't exported, its offset isn't stable across builds, and
// with NO_LOGGING compiled in it would likely capture almost nothing anyway (confirmed: 0 of 12
// standard Unreal log category strings exist anywhere in the binary). A better target was found
// instead by re-checking the same `dumpbin /imports` data this proxy's own DSOUND target was picked
// from: PalServer-Win64-Shipping-Cmd.exe imports **WriteConsoleA and WriteConsoleW directly, by
// name, from KERNEL32.dll**, in its own import table. Unreal's Windows console output device
// (FWindowsConsoleOutputDevice) is documented to write the visible console window's text through
// exactly this API, independent of the file-logging subsystem -- and it's the same low-level path
// any other code in the process (PalDefender's own console printer included) ultimately goes
// through to put text on screen. Hooking these two entries in the main EXE's own Import Address
// Table -- not a shared kernel32 export patch, which would affect every DLL in the process,
// including UE4SS/PalDefender/Steam -- captures everything the visible console window shows, from
// any source, with a minimal, surgical blast radius: only this one importer is touched, and every
// call is still forwarded to the real function afterward so the game's own console behaves exactly
// as before. See docs/architecture/v0.7.72.0-console-write-hook.md for the full design and the live
// isolated-clone verification this was proven against before ever touching a real production server.

#include <windows.h>
#include <cstdio>
#include <cstring>
#include <ctime>
#include <string>
#include <share.h>

namespace {

HMODULE realDsound = nullptr;
FILE* logFile = nullptr;
FILE* captureLogFile = nullptr;
CRITICAL_SECTION captureLock{};
bool captureLockReady = false;

using WriteConsoleAFn = BOOL(WINAPI*)(HANDLE, const VOID*, DWORD, LPDWORD, LPVOID);
using WriteConsoleWFn = BOOL(WINAPI*)(HANDLE, const VOID*, DWORD, LPDWORD, LPVOID);

WriteConsoleAFn realWriteConsoleA = nullptr;
WriteConsoleWFn realWriteConsoleW = nullptr;

// Real DSOUND.dll export signatures, exactly as documented by Microsoft (dsound.h). PalServer only
// calls a subset of these (confirmed via dumpbin: DirectSoundCreate, DirectSoundCreate8,
// DirectSoundEnumerateW, DirectSoundCaptureCreate, DirectSoundCaptureEnumerateW,
// DirectSoundCaptureCreate8), but all 6 real signatures are declared here so the forwarding stubs
// below type-check against the genuine function pointers rather than raw void*.
using DirectSoundCreateFn = HRESULT(WINAPI*)(const GUID*, void**, IUnknown*);
using DirectSoundCreate8Fn = HRESULT(WINAPI*)(const GUID*, void**, IUnknown*);
using DirectSoundEnumerateWFn = HRESULT(WINAPI*)(void*, void*);
using DirectSoundCaptureCreateFn = HRESULT(WINAPI*)(const GUID*, void**, IUnknown*);
using DirectSoundCaptureCreate8Fn = HRESULT(WINAPI*)(const GUID*, void**, IUnknown*);
using DirectSoundCaptureEnumerateWFn = HRESULT(WINAPI*)(void*, void*);

void WriteLogLine(const char* message)
{
    if (!logFile) return;
    time_t now = time(nullptr);
    tm localNow{};
    localtime_s(&localNow, &now);
    char stamp[32];
    strftime(stamp, sizeof(stamp), "%Y-%m-%d %H:%M:%S", &localNow);
    fprintf(logFile, "[%s] %s\n", stamp, message);
    fflush(logFile);
}

// Appends raw captured console text (already UTF-8) to a log file distinct from this proxy's own
// diagnostic log, so a genuine capture failure never gets lost in -- or confused with -- injection
// lifecycle noise. Thread-safe: WriteConsoleA/W can legitimately be called from multiple engine
// threads concurrently.
void WriteCaptureBytes(const char* data, size_t length)
{
    if (!captureLogFile || !data || length == 0) return;
    if (captureLockReady) EnterCriticalSection(&captureLock);
    fwrite(data, 1, length, captureLogFile);
    fflush(captureLogFile);
    if (captureLockReady) LeaveCriticalSection(&captureLock);
}

BOOL WINAPI HookedWriteConsoleA(HANDLE consoleOutput, const VOID* buffer, DWORD numberOfCharsToWrite,
    LPDWORD numberOfCharsWritten, LPVOID reserved)
{
    if (buffer && numberOfCharsToWrite > 0)
    {
        WriteCaptureBytes(static_cast<const char*>(buffer), numberOfCharsToWrite);
    }
    if (realWriteConsoleA) return realWriteConsoleA(consoleOutput, buffer, numberOfCharsToWrite, numberOfCharsWritten, reserved);
    if (numberOfCharsWritten) *numberOfCharsWritten = 0;
    return FALSE;
}

BOOL WINAPI HookedWriteConsoleW(HANDLE consoleOutput, const VOID* buffer, DWORD numberOfCharsToWrite,
    LPDWORD numberOfCharsWritten, LPVOID reserved)
{
    if (buffer && numberOfCharsToWrite > 0)
    {
        int utf8Length = WideCharToMultiByte(CP_UTF8, 0, static_cast<LPCWCH>(buffer),
            static_cast<int>(numberOfCharsToWrite), nullptr, 0, nullptr, nullptr);
        if (utf8Length > 0)
        {
            std::string utf8(static_cast<size_t>(utf8Length), '\0');
            WideCharToMultiByte(CP_UTF8, 0, static_cast<LPCWCH>(buffer), static_cast<int>(numberOfCharsToWrite),
                utf8.data(), utf8Length, nullptr, nullptr);
            WriteCaptureBytes(utf8.data(), utf8.size());
        }
    }
    if (realWriteConsoleW) return realWriteConsoleW(consoleOutput, buffer, numberOfCharsToWrite, numberOfCharsWritten, reserved);
    if (numberOfCharsWritten) *numberOfCharsWritten = 0;
    return FALSE;
}

// Overwrites one named KERNEL32.dll import in `targetModule`'s own Import Address Table with
// `hookFn`. Deliberately does NOT read the "original" function pointer back out of the IAT slot --
// that slot might not be resolved yet depending on import-processing order. Instead the caller
// resolves the real function directly via GetProcAddress against the already-fully-loaded kernel32
// module (kernel32 is always the very first thing the loader sets up, before any other DLL's
// DllMain -- including this one -- ever runs, so this is always safe). Returns false, harmlessly,
// if the target module doesn't import that function at all -- never treated as fatal, since a
// future PalServer build changing how it writes console output should degrade to "no capture",
// not a crash.
bool PatchKernel32Import(HMODULE targetModule, const char* importName, void* hookFn)
{
    if (!targetModule || !importName || !hookFn) return false;
    auto base = reinterpret_cast<BYTE*>(targetModule);
    auto dosHeader = reinterpret_cast<PIMAGE_DOS_HEADER>(base);
    if (dosHeader->e_magic != IMAGE_DOS_SIGNATURE) return false;
    auto ntHeaders = reinterpret_cast<PIMAGE_NT_HEADERS>(base + dosHeader->e_lfanew);
    if (ntHeaders->Signature != IMAGE_NT_SIGNATURE) return false;

    const auto& importDataDir = ntHeaders->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_IMPORT];
    if (importDataDir.VirtualAddress == 0) return false;

    auto importDesc = reinterpret_cast<PIMAGE_IMPORT_DESCRIPTOR>(base + importDataDir.VirtualAddress);
    for (; importDesc->Name != 0; ++importDesc)
    {
        const char* moduleName = reinterpret_cast<const char*>(base + importDesc->Name);
        if (_stricmp(moduleName, "KERNEL32.dll") != 0) continue;
        if (importDesc->OriginalFirstThunk == 0) return false; // no name table to search by -- bail safely

        auto nameThunk = reinterpret_cast<PIMAGE_THUNK_DATA>(base + importDesc->OriginalFirstThunk);
        auto addressThunk = reinterpret_cast<PIMAGE_THUNK_DATA>(base + importDesc->FirstThunk);
        for (; nameThunk->u1.AddressOfData != 0; ++nameThunk, ++addressThunk)
        {
            if (IMAGE_SNAP_BY_ORDINAL(nameThunk->u1.Ordinal)) continue;
            auto importByName = reinterpret_cast<PIMAGE_IMPORT_BY_NAME>(base + nameThunk->u1.AddressOfData);
            if (strcmp(importByName->Name, importName) != 0) continue;

            DWORD oldProtect = 0;
            if (!VirtualProtect(&addressThunk->u1.Function, sizeof(ULONG_PTR), PAGE_READWRITE, &oldProtect)) return false;
            addressThunk->u1.Function = reinterpret_cast<ULONG_PTR>(hookFn);
            DWORD ignored;
            VirtualProtect(&addressThunk->u1.Function, sizeof(ULONG_PTR), oldProtect, &ignored);
            return true;
        }
        return false; // KERNEL32.dll is imported, but not this specific function
    }
    return false; // module doesn't import KERNEL32.dll at all (should never happen in practice)
}

void InstallConsoleCaptureHook()
{
    HMODULE kernel32 = GetModuleHandleW(L"kernel32.dll");
    HMODULE mainModule = GetModuleHandleW(nullptr);
    if (!kernel32 || !mainModule)
    {
        WriteLogLine("Console capture: could not resolve kernel32/main module handles -- hook not installed.");
        return;
    }

    // Resolve the REAL functions independently of the IAT (see PatchKernel32Import's comment) so
    // forwarding is correct no matter what order the loader processed this EXE's imports in.
    realWriteConsoleA = reinterpret_cast<WriteConsoleAFn>(GetProcAddress(kernel32, "WriteConsoleA"));
    realWriteConsoleW = reinterpret_cast<WriteConsoleWFn>(GetProcAddress(kernel32, "WriteConsoleW"));

    bool hookedA = realWriteConsoleA &&
        PatchKernel32Import(mainModule, "WriteConsoleA", reinterpret_cast<void*>(&HookedWriteConsoleA));
    bool hookedW = realWriteConsoleW &&
        PatchKernel32Import(mainModule, "WriteConsoleW", reinterpret_cast<void*>(&HookedWriteConsoleW));

    char msg[256];
    sprintf_s(msg, "Console capture: WriteConsoleA hook %s, WriteConsoleW hook %s.",
        hookedA ? "installed" : "NOT installed", hookedW ? "installed" : "NOT installed");
    WriteLogLine(msg);
}

bool LoadRealDsound()
{
    wchar_t systemDir[MAX_PATH];
    if (GetSystemDirectoryW(systemDir, MAX_PATH) == 0) return false;
    std::wstring realPath = std::wstring(systemDir) + L"\\dsound.dll";
    realDsound = LoadLibraryW(realPath.c_str());
    return realDsound != nullptr;
}

template <typename Fn>
Fn ResolveReal(const char* exportName)
{
    if (!realDsound) return nullptr;
    return reinterpret_cast<Fn>(GetProcAddress(realDsound, exportName));
}

} // namespace

// ---------------------------------------------------------------------------------------------
// Forwarding exports. Each one lazily resolves and tail-calls the real system DSOUND.dll function
// with an identical signature, so PalServer behaves exactly as it would with the genuine DLL --
// this proxy is invisible to the game except for the DllMain side effects below.
// ---------------------------------------------------------------------------------------------

extern "C" __declspec(dllexport) HRESULT WINAPI DirectSoundCreate(const GUID* guid, void** ds, IUnknown* outer)
{
    static auto real = ResolveReal<DirectSoundCreateFn>("DirectSoundCreate");
    return real ? real(guid, ds, outer) : E_FAIL;
}

extern "C" __declspec(dllexport) HRESULT WINAPI DirectSoundCreate8(const GUID* guid, void** ds, IUnknown* outer)
{
    static auto real = ResolveReal<DirectSoundCreate8Fn>("DirectSoundCreate8");
    return real ? real(guid, ds, outer) : E_FAIL;
}

extern "C" __declspec(dllexport) HRESULT WINAPI DirectSoundEnumerateW(void* callback, void* context)
{
    static auto real = ResolveReal<DirectSoundEnumerateWFn>("DirectSoundEnumerateW");
    return real ? real(callback, context) : E_FAIL;
}

extern "C" __declspec(dllexport) HRESULT WINAPI DirectSoundCaptureCreate(const GUID* guid, void** ds, IUnknown* outer)
{
    static auto real = ResolveReal<DirectSoundCaptureCreateFn>("DirectSoundCaptureCreate");
    return real ? real(guid, ds, outer) : E_FAIL;
}

extern "C" __declspec(dllexport) HRESULT WINAPI DirectSoundCaptureCreate8(const GUID* guid, void** ds, IUnknown* outer)
{
    static auto real = ResolveReal<DirectSoundCaptureCreate8Fn>("DirectSoundCaptureCreate8");
    return real ? real(guid, ds, outer) : E_FAIL;
}

extern "C" __declspec(dllexport) HRESULT WINAPI DirectSoundCaptureEnumerateW(void* callback, void* context)
{
    static auto real = ResolveReal<DirectSoundCaptureEnumerateWFn>("DirectSoundCaptureEnumerateW");
    return real ? real(callback, context) : E_FAIL;
}

BOOL APIENTRY DllMain(HMODULE, DWORD reason, LPVOID)
{
    switch (reason)
    {
    case DLL_PROCESS_ATTACH:
    {
        // Log file lives next to the game executable, alongside PalServer's own MystTiq-managed
        // console log -- not under any MystTiq-owned config path, since this DLL has no dependency
        // on the .NET application at all and needs to work if ever loaded standalone.
        wchar_t moduleDir[MAX_PATH];
        HMODULE self = GetModuleHandleW(L"dsound.dll");
        if (self && GetModuleFileNameW(self, moduleDir, MAX_PATH))
        {
            std::wstring path(moduleDir);
            auto slash = path.find_last_of(L'\\');
            std::wstring dir = slash == std::wstring::npos ? L"." : path.substr(0, slash);
            // _wfsopen with _SH_DENYWR (not plain _wfopen_s, which opens with no sharing at all on
            // the Windows UCRT -- confirmed live: MystTiq's own periodic tail-read of this file
            // failed with a sharing violation for the entire lifetime of the game process under
            // plain fopen). _SH_DENYWR keeps this process the only writer while still letting any
            // number of readers (MystTiq's .NET file reads included) open the file concurrently.
            std::wstring logPath = dir + L"\\MystTiqConsoleProxy.log";
            logFile = _wfsopen(logPath.c_str(), L"a", _SH_DENYWR);
            // Deliberately a separate file from the diagnostic log above: this one holds ONLY raw
            // captured console bytes, verbatim, so HeadlessMonitoringService can read it as a plain
            // console-source file without any MystTiqConsoleProxy-authored lines mixed in.
            std::wstring capturePath = dir + L"\\MystTiqConsoleProxy-Capture.log";
            captureLogFile = _wfsopen(capturePath.c_str(), L"a", _SH_DENYWR);
        }
        InitializeCriticalSection(&captureLock);
        captureLockReady = true;
        WriteLogLine("MystTiqConsoleProxy: injected (DLL_PROCESS_ATTACH).");

        if (!LoadRealDsound())
        {
            WriteLogLine("MystTiqConsoleProxy: FATAL -- could not load the real system dsound.dll; "
                "audio-related calls will fail. Refusing to proceed further.");
            return TRUE; // Still return TRUE: failing DllMain would abort the whole game process,
                          // which is strictly worse than a proxy that logs an error and does nothing.
        }
        WriteLogLine("MystTiqConsoleProxy: real dsound.dll loaded, export forwarding active.");

        InstallConsoleCaptureHook();
        break;
    }
    case DLL_PROCESS_DETACH:
        WriteLogLine("MystTiqConsoleProxy: detaching.");
        if (logFile) { fclose(logFile); logFile = nullptr; }
        if (captureLogFile) { fclose(captureLogFile); captureLogFile = nullptr; }
        if (captureLockReady) { DeleteCriticalSection(&captureLock); captureLockReady = false; }
        if (realDsound) { FreeLibrary(realDsound); realDsound = nullptr; }
        break;
    default:
        break;
    }
    return TRUE;
}
