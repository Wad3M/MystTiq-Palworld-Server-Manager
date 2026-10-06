# MystTiq v1.0.5.0: file reviewed for this release (2026-10-06).
#requires -Version 7.0
[CmdletBinding()]
param(
    # Where the window capture and the report go.
    [string]$OutputDirectory = (Join-Path ([IO.Path]::GetTempPath()) "mysttiq-v0.8.26.0-contrast-$(Get-Date -Format 'yyyyMMdd-HHmmss')"),
    # How close (per colour channel, 0-255) a pixel must be to count as a system colour.
    [int]$Tolerance = 3
)

# v0.8.26.0: checks, on the real Windows desktop, that MystTiq follows a Windows contrast theme (v0.8.25.0). Before
# running it:
#   1. switch a contrast theme on (Settings > Accessibility > Contrast themes, or Left Alt + Left Shift + Print Screen);
#   2. start MystTiq and set Appearance to "High contrast" or "Follow the system".
# The script then reads the theme from Windows itself (SPI_GETHIGHCONTRAST and GetSysColor, the same calls MystTiq
# makes), captures the MystTiq window (PrintWindow, so it works even behind other windows) and checks its colours:
# the most common colour is the theme's window colour, and its text and highlight colours are there. Nothing is changed.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'Windows contrast themes exist only on Windows.' }

Add-Type -ReferencedAssemblies System.Drawing.Common, System.Drawing.Primitives, System.Private.Windows.GdiPlus, System.Private.Windows.Core, System.Collections, System.Runtime.InteropServices -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
public static class MystTiqContrastProbe
{
    [StructLayout(LayoutKind.Sequential)] struct HighContrast { public uint Size; public uint Flags; public IntPtr Scheme; }
    [StructLayout(LayoutKind.Sequential)] struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll", SetLastError = true)] static extern bool SystemParametersInfo(uint action, uint param, ref HighContrast info, uint winIni);
    [DllImport("user32.dll")] static extern uint GetSysColor(int index);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr hwnd);

    public static bool IsOn(out string scheme)
    {
        var info = new HighContrast { Size = (uint)Marshal.SizeOf<HighContrast>() };
        scheme = "";
        if (!SystemParametersInfo(0x0042, info.Size, ref info, 0)) return false;
        if (info.Scheme != IntPtr.Zero) scheme = Marshal.PtrToStringUni(info.Scheme) ?? "";
        return (info.Flags & 1) != 0;
    }
    public static int[] Sys(int index) { var v = GetSysColor(index); return new[] { (int)(v & 0xFF), (int)((v >> 8) & 0xFF), (int)((v >> 16) & 0xFF) }; }
    public static bool Minimized(IntPtr hwnd) => IsIconic(hwnd);

    public static Bitmap Capture(IntPtr hwnd)
    {
        GetWindowRect(hwnd, out var r);
        var bmp = new Bitmap(Math.Max(1, r.Right - r.Left), Math.Max(1, r.Bottom - r.Top), PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            var hdc = g.GetHdc();
            try { PrintWindow(hwnd, hdc, 2); } finally { g.ReleaseHdc(hdc); }
        }
        return bmp;
    }

    // Every pixel as 0xRRGGBB, skipping an 8-pixel border (the window frame and its shadow).
    public static int[] Pixels(Bitmap bmp)
    {
        int m = 8, w = Math.Max(0, bmp.Width - 2 * m), h = Math.Max(0, bmp.Height - 2 * m);
        var result = new int[w * h];
        var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var row = new int[bmp.Width];
            for (int y = 0; y < h; y++)
            {
                Marshal.Copy(data.Scan0 + (y + m) * data.Stride, row, 0, bmp.Width);
                for (int x = 0; x < w; x++) result[y * w + x] = row[x + m] & 0xFFFFFF;
            }
        }
        finally { bmp.UnlockBits(data); }
        return result;
    }

    static bool Near(int p, int[] c, int tol) =>
        Math.Abs(((p >> 16) & 0xFF) - c[0]) <= tol && Math.Abs(((p >> 8) & 0xFF) - c[1]) <= tol && Math.Abs((p & 0xFF) - c[2]) <= tol;
    public static bool IsNear(int p, int[] c, int tol) => Near(p, c, tol);

    // The share of pixels near any of the given colours.
    public static double Share(int[] pixels, int[][] colours, int tol)
    {
        if (pixels.Length == 0) return 0;
        long hits = 0;
        foreach (var p in pixels) foreach (var c in colours) if (Near(p, c, tol)) { hits++; break; }
        return (double)hits / pixels.Length;
    }

    // The most common colours, as "0xRRGGBB count" pairs.
    public static long[][] Top(int[] pixels, int count)
    {
        var counts = new System.Collections.Generic.Dictionary<int, long>();
        foreach (var p in pixels) { counts.TryGetValue(p, out var n); counts[p] = n + 1; }
        var list = new System.Collections.Generic.List<long[]>();
        foreach (var kv in counts) list.Add(new long[] { kv.Key, kv.Value });
        list.Sort((a, b) => b[1].CompareTo(a[1]));
        return list.GetRange(0, Math.Min(count, list.Count)).ToArray();
    }
}
'@

New-Item $OutputDirectory -ItemType Directory -Force | Out-Null
$results = [System.Collections.Generic.List[object]]::new()
function Add-Result([string]$Name, [bool]$Ok, [string]$Detail) {
    $results.Add([pscustomobject]@{ Name = $Name; Ok = $Ok; Detail = $Detail })
    Write-Host ("[{0}] {1}{2}" -f ($(if ($Ok) { 'PASS' } else { 'FAIL' }), $Name, $(if ($Detail) { " -- $Detail" } else { '' }))) -ForegroundColor $(if ($Ok) { 'Green' } else { 'Red' })
}
function Format-Rgb([int[]]$c) { '#{0:X2}{1:X2}{2:X2}' -f $c[0], $c[1], $c[2] }
Write-Host '==> MystTiq v0.8.26.0 contrast theme check' -ForegroundColor Cyan

# 1. Windows: a contrast theme is on, and its colours.
$scheme = ''
$on = [MystTiqContrastProbe]::IsOn([ref]$scheme)
Add-Result 'A Windows contrast theme is on' $on $(if ($on) { "theme: $(if ($scheme) { $scheme } else { '(unnamed)' })" } else { 'switch one on (Left Alt + Left Shift + Print Screen) and run this again' })
if (-not $on) { exit 1 }
# The same indexes MystTiq reads (SystemContrastPalette): window, text, highlight, highlight text, grey text, link, button face, button text.
$sys = [ordered]@{ Window = [MystTiqContrastProbe]::Sys(5); WindowText = [MystTiqContrastProbe]::Sys(8); Highlight = [MystTiqContrastProbe]::Sys(13)
    HighlightText = [MystTiqContrastProbe]::Sys(14); GrayText = [MystTiqContrastProbe]::Sys(17); Hotlight = [MystTiqContrastProbe]::Sys(26)
    ButtonFace = [MystTiqContrastProbe]::Sys(15); ButtonText = [MystTiqContrastProbe]::Sys(18) }
Write-Host ('Theme colours: ' + (($sys.GetEnumerator() | ForEach-Object { "$($_.Key) $(Format-Rgb $_.Value)" }) -join ', '))

# 2. MystTiq's own setting: the contrast theme applies in High contrast and in Follow the system.
$prefsPath = Join-Path $env:APPDATA 'MystTiq\theme-preferences.json'
$mode = if (Test-Path $prefsPath) { (Get-Content $prefsPath -Raw | ConvertFrom-Json).variant } else { 'Dark' }
Add-Result 'MystTiq Appearance is High contrast or Follow the system' ($mode -in 'HighContrast', 'System') "saved mode: $mode"

# 3. The MystTiq window.
$process = Get-Process -Name 'MystTiq.Desktop' -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne [IntPtr]::Zero } | Select-Object -First 1
Add-Result 'The MystTiq window is open' ($null -ne $process) $(if ($process) { "process $($process.Id): $($process.MainWindowTitle)" } else { 'start MystTiq and run this again' })
if (-not $process) { exit 1 }
if ([MystTiqContrastProbe]::Minimized($process.MainWindowHandle)) { Add-Result 'The window is not minimised' $false 'restore it and run this again'; exit 1 }
$bitmap = [MystTiqContrastProbe]::Capture($process.MainWindowHandle)
$shot = Join-Path $OutputDirectory 'mysttiq-window.png'
$bitmap.Save($shot, [System.Drawing.Imaging.ImageFormat]::Png)
$pixels = [MystTiqContrastProbe]::Pixels($bitmap)
$bitmap.Dispose()
Add-Result 'The window was captured' ($pixels.Length -gt 10000) "$shot ($($pixels.Length) pixels)"

# 4. The colours. The theme's window colour covers the most of the window (pages, cards, lists); its text colour and
# highlight (the selected navigation entry) are present. Art is still shown, so only these are required.
$top = [MystTiqContrastProbe]::Top($pixels, 5)
$dominant = [int]$top[0][0]
$topText = ($top | ForEach-Object { '#{0:X6} {1:P1}' -f [int]$_[0], ($_[1] / $pixels.Length) }) -join ', '
Add-Result "The most common colour is the theme's window colour ($(Format-Rgb $sys.Window))" ([MystTiqContrastProbe]::IsNear($dominant, $sys.Window, $Tolerance)) "most common: $topText"
foreach ($key in 'WindowText', 'Highlight') {
    $share = [MystTiqContrastProbe]::Share($pixels, [int[][]]@(, $sys[$key]), $Tolerance)
    Add-Result "The theme's $key colour ($(Format-Rgb $sys[$key])) is used" ($share -ge 0.0005) ('{0:P2} of the window' -f $share)
}
$systemShare = [MystTiqContrastProbe]::Share($pixels, [int[][]]@($sys.Values), $Tolerance)
Add-Result 'Most of the window is in the theme''s colours' ($systemShare -ge 0.5) ('{0:P1} of pixels are one of its eight colours' -f $systemShare)

# 5. Report.
$passed = @($results | Where-Object Ok).Count
$report = Join-Path $OutputDirectory 'report.md'
$lines = @('# MystTiq v0.8.26.0 contrast theme check', '', "Run: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')  ", "Windows theme: $(if ($scheme) { $scheme } else { '(unnamed)' })  ",
    "Capture: $shot", '', '| Colour | Value |', '|---|---|')
$lines += $sys.GetEnumerator() | ForEach-Object { "| $($_.Key) | $(Format-Rgb $_.Value) |" }
$lines += '', "## Results ($passed/$($results.Count))", '', '| Check | Result | Detail |', '|---|---|---|'
$lines += $results | ForEach-Object { "| $($_.Name) | $(if ($_.Ok) { 'PASS' } else { 'FAIL' }) | $($_.Detail -replace '\|', '/') |" }
$lines | Set-Content -LiteralPath $report -Encoding utf8
Write-Host "Report: $report"
Write-Host "Passed: $passed/$($results.Count)" -ForegroundColor $(if ($passed -eq $results.Count) { 'Green' } else { 'Red' })
if ($passed -ne $results.Count) { exit 1 }
