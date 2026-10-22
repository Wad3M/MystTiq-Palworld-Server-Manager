// MystTiq v1.1.5.0: file reviewed for this release (2026-10-22).
// v1.0.7.5 (roadmap Q-10): behaves like SteamCMD installing or updating Palworld into +force_install_dir, as the long test
// set needs it. What it does comes from "mode.txt" beside it (so a test can change it between runs):
//   complete: writes the app manifest as fully installed (StateFlags 4) and exits 0, like a finished update.
//   hang:     writes the manifest as an update in progress (StateFlags 1026) and waits until killed, like an update
//             interrupted half way.
//   fail:     touches nothing and exits 8, like SteamCMD failing before it starts ("No connection").
// Every run is appended to "runs.txt" beside it. Test use only.
var mode = "complete";
try { mode = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "mode.txt")).Trim().ToLowerInvariant(); } catch { }
string? installDir = null;
for (var i = 0; i < args.Length - 1; i++)
    if (args[i].Equals("+force_install_dir", StringComparison.OrdinalIgnoreCase)) installDir = args[i + 1];
try { File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "runs.txt"), $"{DateTimeOffset.UtcNow:O} {mode} {string.Join(' ', args)}{Environment.NewLine}"); } catch { }
Console.WriteLine($"FakeSteamCmd ({mode}) for {installDir}");
if (installDir is null) { Console.WriteLine("No +force_install_dir"); return 1; }
if (mode == "fail") { Console.WriteLine("ERROR! Failed to install app '2394010' (No Connection)"); return 8; }

var steamapps = Path.Combine(installDir, "steamapps");
Directory.CreateDirectory(steamapps);
void Manifest(int flags) => File.WriteAllText(Path.Combine(steamapps, "appmanifest_2394010.acf"),
    $"\"AppState\"\n{{\n\t\"appid\"\t\t\"2394010\"\n\t\"StateFlags\"\t\t\"{flags}\"\n\t\"buildid\"\t\t\"25247047\"\n}}\n");
if (mode == "hang")
{
    Manifest(1026);
    Console.WriteLine(" Update state (0x61) downloading, progress: 42.00 (1000 / 2381)");
    await Task.Delay(Timeout.Infinite);
}
Manifest(4);
Console.WriteLine("Success! App '2394010' fully installed.");
return 0;
