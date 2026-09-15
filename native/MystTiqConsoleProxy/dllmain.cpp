// MystTiq Console Proxy -- v0.7.57.0 Native Console Capture (PalServer DLL Proxy Logger)
//
// Why this exists: PalServer's own console output isn't reachable through normal process
// redirection. Live testing this session (see docs/architecture/v0.7.57.0-native-console-capture.md)
// confirmed that stdio-pipe redirection captures nothing even when the game process is launched
// directly, and that "-ABSLOG" produces no file -- Unreal's own file-logging subsystem appears
// compiled out of this build. The one proven, working technique other tools in this space use
// (PalServerLogger, and the load-order UE4SS/PalDefender already rely on) is DLL proxying: ship a
// DLL with the same name as one the game already imports, forward its real exports to the genuine
// system DLL so the game behaves identically, and use the loader's own DllMain entry point to run
// arbitrary code inside the game's process.
//
// Target DLL: DSOUND.dll. Confirmed via a live `dumpbin /imports` against the real
// PalServer-Win64-Shipping-Cmd.exe (v0.7.51.0-era investigation, re-confirmed here) that it imports
// exactly 6 functions from DSOUND.dll, all by ordinal, none of which UE4SS or PalDefender's own
// d3d9.dll-based proxy already occupies -- chosen specifically to avoid colliding with either.
//
// What this file does NOT do: install any hook into Unreal's own logging function
// (FOutputDevice::LogfImpl). That requires a live signature scan against the loaded module to find
// the function's actual address (it isn't exported, and its offset isn't stable across builds), and
// this session has no way to safely test a memory hook against a real, running PalServer process --
// the production server on this machine currently can't launch cleanly at all (a separate,
// unresolved environment issue investigated the same session this proxy was built). Shipping an
// unverified memory patch that could corrupt or crash a real game server is not something to do
// without the ability to test it. TryInstallLogHook() below is a real, callable scaffold -- it logs
// its own no-op status honestly rather than pretending to have installed a hook -- ready for a
// future session to fill in once live verification is possible again.

#include <windows.h>
#include <cstdio>
#include <ctime>
#include <string>

namespace {

HMODULE realDsound = nullptr;
FILE* logFile = nullptr;

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

// Real hook target -- deliberately not implemented. See the file-level comment above for why:
// no safe way to verify a memory patch against a real running process in this environment.
// Honestly reports that nothing was installed rather than claiming success.
void TryInstallLogHook()
{
    WriteLogLine("TryInstallLogHook: not implemented -- no live PalServer process was available "
        "to verify a memory signature scan against in the session that built this proxy. "
        "This is a real, disclosed gap, not a silent no-op.");
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
            std::wstring logPath = dir + L"\\MystTiqConsoleProxy.log";
            _wfopen_s(&logFile, logPath.c_str(), L"a");
        }
        WriteLogLine("MystTiqConsoleProxy: injected (DLL_PROCESS_ATTACH).");

        if (!LoadRealDsound())
        {
            WriteLogLine("MystTiqConsoleProxy: FATAL -- could not load the real system dsound.dll; "
                "audio-related calls will fail. Refusing to proceed further.");
            return TRUE; // Still return TRUE: failing DllMain would abort the whole game process,
                          // which is strictly worse than a proxy that logs an error and does nothing.
        }
        WriteLogLine("MystTiqConsoleProxy: real dsound.dll loaded, export forwarding active.");

        TryInstallLogHook();
        break;
    }
    case DLL_PROCESS_DETACH:
        WriteLogLine("MystTiqConsoleProxy: detaching.");
        if (logFile) { fclose(logFile); logFile = nullptr; }
        if (realDsound) { FreeLibrary(realDsound); realDsound = nullptr; }
        break;
    default:
        break;
    }
    return TRUE;
}
