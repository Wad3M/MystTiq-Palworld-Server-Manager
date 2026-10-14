# MystTiq v1.0.8.5: file reviewed for this release (2026-10-12).
[CmdletBinding()]
param([Parameter(Mandatory)][int]$ProcessId, [ValidateSet('CtrlBreak', 'CtrlC')][string]$Signal = 'CtrlBreak')
$ErrorActionPreference = 'Stop'
# v1.0.7.5 (roadmap Q-10): asks a console process (an isolated "mysttiq-server api-run" in a test) to stop the way a
# console stop does (Ctrl+Break), so its normal shutdown runs. Ctrl+C is not used: a process started from a parent that
# ignores Ctrl+C inherits that, and then never sees it (seen running the long set from a bash-started pwsh). Attaching to
# another console detaches this one, so a separate short-lived pwsh does it, with a handler that keeps itself alive; the
# command goes as -EncodedCommand (passed as plain arguments, its quotes were mangled). Every process sharing the target's
# console gets the stop too. -Signal CtrlC sends Ctrl+C instead (as a person pressing it in the terminal). Test use only.
if (-not $IsWindows) { throw 'Send-MystTiqConsoleStop is for Windows; on Linux send SIGTERM.' }
$code = @"
Add-Type -TypeDefinition 'using System; using System.Runtime.InteropServices;
public static class MystTiqConsoleStop {
  delegate bool Handler(uint ev);
  static readonly Handler Keep = _ => true;
  [DllImport("kernel32.dll", SetLastError = true)] static extern bool AttachConsole(uint id);
  [DllImport("kernel32.dll", SetLastError = true)] static extern bool FreeConsole();
  [DllImport("kernel32.dll", SetLastError = true)] static extern bool SetConsoleCtrlHandler(Handler handler, bool add);
  [DllImport("kernel32.dll", SetLastError = true)] static extern bool GenerateConsoleCtrlEvent(uint ev, uint group);
  public static int Send(uint pid, uint ev) {
    FreeConsole();
    if (!AttachConsole(pid)) return 3;
    SetConsoleCtrlHandler(Keep, true);
    var sent = GenerateConsoleCtrlEvent(ev, 0);
    System.Threading.Thread.Sleep(500);
    FreeConsole();
    return sent ? 0 : 4;
  }
}'
exit [MystTiqConsoleStop]::Send($ProcessId, $(if ($Signal -eq 'CtrlC') { 0 } else { 1 }))
"@
$encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($code))
$p = Start-Process pwsh -ArgumentList @('-NoProfile', '-NonInteractive', '-EncodedCommand', $encoded) -WindowStyle Hidden -PassThru -Wait
if ($p.ExitCode -ne 0) { throw "The console stop could not be sent to process $ProcessId (code $($p.ExitCode))." }
