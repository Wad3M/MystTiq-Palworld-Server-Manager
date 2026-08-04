using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PalworldManager.Models;
using PalworldManager.Services;

namespace PalworldManager;

public partial class MainWindow
{
    private void RefreshSelectedPlayerTimeline()
    {
        if (PlayerTimelineList is null) return;
        if (PlayersGrid.SelectedItem is not PlayerRow player)
        {
            PlayerTimelineList.ItemsSource = Array.Empty<string>();
            return;
        }
        var tokens = new[] { player.Name, player.UserId, player.SteamId, player.PlayerId }
            .Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        var rows = auditEntries
            .Where(e => e.Category.Equals("Players", StringComparison.OrdinalIgnoreCase) || e.Action.Contains("player", StringComparison.OrdinalIgnoreCase))
            .Where(e => tokens.Length == 0 || tokens.Any(t => (e.Action + " " + e.Details).Contains(t, StringComparison.OrdinalIgnoreCase)))
            .Take(100)
            .Select(e => $"{e.TimestampDisplay}  [{e.Severity}]  {e.Action} — {e.Details}")
            .ToList();
        if (rows.Count == 0) rows.Add("No player-specific activity has been recorded yet.");
        PlayerTimelineList.ItemsSource = rows;
    }

    private string GetPlayerHealth(PlayerRow p)
    {
        var save = ResolvePlayerSavePath(p);
        if (p.Status.Equals("ONLINE", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(save)) return "Attention • online, save unresolved";
        if (string.IsNullOrWhiteSpace(save)) return "Missing save";
        try
        {
            var info = new FileInfo(save);
            if (!info.Exists || info.Length < 32) return "Critical • save unreadable";
            var dps = Path.Combine(info.DirectoryName!, Path.GetFileNameWithoutExtension(save) + "_dps.sav");
            return File.Exists(dps) ? "Healthy" : "Healthy • no DPS companion";
        }
        catch { return "Unknown"; }
    }

    private void BackupSelectedPlayer_Click(object sender, RoutedEventArgs e)
    {
        if (PlayersGrid.SelectedItem is not PlayerRow player) { MessageBox.Show("Select a player first.", "Player Backup", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        var path = ResolvePlayerSavePath(player);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) { MessageBox.Show("No player save was found. Run Discover Saves first.", "Player Backup", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        try
        {
            var output = CreatePlayerBackupPackage(player, path, "ManualBackup");
            RecordAudit("Success", "Players", "Player backup created", $"{player.Name} • {output}", 4);
            RefreshSelectedPlayerTimeline();
            MessageBox.Show($"Player backup created:\n\n{output}", "Player Backup", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { RecordAudit("Critical", "Players", "Player backup failed", $"{player.Name}: {ex.Message}", 4); MessageBox.Show(ex.Message, "Player Backup", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void ExportSelectedPlayer_Click(object sender, RoutedEventArgs e)
    {
        if (PlayersGrid.SelectedItem is not PlayerRow player) { MessageBox.Show("Select a player first.", "Export Player", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        var path = ResolvePlayerSavePath(player);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) { MessageBox.Show("No player save was found.", "Export Player", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "ZIP archive (*.zip)|*.zip", FileName = $"Myst_Player_{SafePlayerFileName(player.Name)}_{DateTime.Now:yyyyMMdd_HHmmss}.zip" };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            var temp = CreatePlayerBackupPackage(player, path, "Export");
            File.Copy(temp, dlg.FileName, true);
            RecordAudit("Success", "Players", "Player exported", $"{player.Name} • {dlg.FileName}", 4);
            RefreshSelectedPlayerTimeline();
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Export Player", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private string CreatePlayerBackupPackage(PlayerRow player, string primaryPath, string purpose)
    {
        var root = Path.Combine(settings.BackupRoot, "PlayerManagement");
        Directory.CreateDirectory(root);
        var output = Path.Combine(root, $"{purpose}_{SafePlayerFileName(player.Name)}_{DateTime.Now:yyyyMMdd_HHmmss}.zip");
        var dps = Path.Combine(Path.GetDirectoryName(primaryPath)!, Path.GetFileNameWithoutExtension(primaryPath) + "_dps.sav");
        using var archive = ZipFile.Open(output, ZipArchiveMode.Create);
        archive.CreateEntryFromFile(primaryPath, "Player/" + Path.GetFileName(primaryPath), CompressionLevel.Optimal);
        if (File.Exists(dps)) archive.CreateEntryFromFile(dps, "Player/" + Path.GetFileName(dps), CompressionLevel.Optimal);
        var info = new { player.Name, player.UserId, player.SteamId, player.PlayerId, player.Platform, player.Level, player.FirstSeen, player.LastSeen, player.Source, player.Notes };
        WriteJsonEntry(archive, "PlayerInfo.json", info);
        WriteJsonEntry(archive, "Manifest.json", new { format = "MystPlayerBackup", version = 1, createdAt = DateTimeOffset.Now, purpose, primaryFile = Path.GetFileName(primaryPath), dpsFile = File.Exists(dps) ? Path.GetFileName(dps) : null, warning = "Player package does not independently contain all Level.sav references." });
        WriteJsonEntry(archive, "Hashes.json", new { primary = Sha256(primaryPath), dps = File.Exists(dps) ? Sha256(dps) : null });
        return output;
    }

    private static void WriteJsonEntry(ZipArchive archive, string name, object value)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
    }
    private static string Sha256(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
    private static string SafePlayerFileName(string value) => string.Concat((string.IsNullOrWhiteSpace(value) ? "Player" : value).Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));

    private void PlayerRestoreComingSoon_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Transactional character restore remains planned for a later Player Toolkit release. v2.11.7.0 adds the transactional Character Clone Engine; full character restore remains planned for the Complete Player Toolkit.", "Restore Character", MessageBoxButton.OK, MessageBoxImage.Information);
    private void ResetCharacter_Click(object sender, RoutedEventArgs e)
    {
        if (PlayersGrid.SelectedItem is not PlayerRow player)
        {
            MessageBox.Show("Select a player first.", "Reset Character", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (player.Status.Equals("ONLINE", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show("The selected player is online. Kick or disconnect the player before resetting the character.", "Reset Character", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var worldPath = SaveInspector.FindActiveWorldPath();
            if (string.IsNullOrWhiteSpace(worldPath)) throw new DirectoryNotFoundException("No active Palworld world containing Level.sav was found.");
            var engine = new CharacterResetService(settings);
            var preview = engine.Preview(player, worldPath, server.IsRunning());
            var findings = string.Join(Environment.NewLine, preview.Findings.Select(x => "• " + x));
            var identities = string.Join(Environment.NewLine, preview.Identifiers.Select(x => "  " + x));
            var summary = $"RESET CHARACTER PREVIEW\n\nPlayer: {player.Name}\nWorld: {worldPath}\nPlayer save: {BlankDash(preview.PlayerSavePath)}\nPlayer identifiers:\n{identities}\n\nExact Level.sav references found: {preview.ExactReferenceCount}\n\nPlanned transaction:\n• Create a complete rollback ZIP\n• Decode and repair Level.sav\n• Remove matching registration, guild/member, owner, and respawn references\n• Re-encode and verify Level.sav before activation\n• Remove the player save and companion save\n• Force Create New Character on the next login\n\nFindings:\n{findings}\n\nThis operation is transactional and attempts automatic rollback if any stage fails.";

            if (!preview.CanApply)
            {
                MessageBox.Show(summary + "\n\nThe reset cannot be applied until every blocking finding is resolved.", "Reset Character — Preview", MessageBoxButton.OK, MessageBoxImage.Warning);
                RecordAudit("Warning", "Players", "Character reset blocked", $"{player.Name} • {string.Join("; ", preview.Findings)}", 4);
                return;
            }

            if (MessageBox.Show(summary + "\n\nApply this reset now?", "Reset Character — Confirm Transaction", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
                return;

            if (MessageBox.Show(
                    $"FINAL CONFIRMATION\n\nReset {player.Name} and force character creation on the next login?\n\n" +
                    "A rollback package has not been created yet; it will be created as the first transaction step.",
                    "Reset Character — Final Confirmation",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Stop,
                    MessageBoxResult.No) != MessageBoxResult.Yes)
                return;

            Mouse.OverrideCursor = Cursors.Wait;
            var result = engine.Apply(preview);
            playerHistory.RemoveMatching(player);
            RefreshPlayerHistoryGrid();
            RecordAudit("Warning", "Players", "Character reset completed", $"{player.Name} • transaction {result.TransactionId} • backup {result.BackupPath}", 5);
            Log($"[CHARACTER RESET] Completed for {player.Name}. References removed: {result.ReferencesRemoved}. Backup: {result.BackupPath}");
            MessageBox.Show($"Character reset completed successfully.\n\nReferences removed: {result.ReferencesRemoved}\nPlayer save removed: {result.PlayerSaveRemoved}\nVerification: {(result.VerificationPassed ? "PASS" : "FAIL")}\n\nRollback backup:\n{result.BackupPath}\n\nReport:\n{result.ReportPath}\n\nThe player should receive Create New Character on the next login.", "Reset Character Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            RecordAudit("Critical", "Players", "Character reset failed", $"{player.Name}: {ex.Message}", 5);
            Log("[CHARACTER RESET] Failed: " + ex);
            MessageBox.Show("The character reset failed. MystTiq attempted to restore the original world and player files. Review the rollback ZIP and logs before starting PalServer.\n\n" + ex.Message, "Reset Character Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { Mouse.OverrideCursor = null; }
    }
    private void PlayerOpenGuild_Click(object sender, RoutedEventArgs e) => NavigateToPage(5);
    private void PlayerOpenBases_Click(object sender, RoutedEventArgs e) => NavigateToPage(16);
    private void PlayerOpenBackups_Click(object sender, RoutedEventArgs e) => NavigateToPage(3);

    private void CloneCharacter_Click(object sender, RoutedEventArgs e)
    {
        if (PlayersGrid.SelectedItem is not PlayerRow source)
        {
            MessageBox.Show("Select the source player first.", "Character Clone", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (source.Status.Equals("ONLINE", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show("The source player is online. Disconnect the player before cloning character data.", "Character Clone", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var candidates = playerHistory.Snapshot()
            .Select(ToPlayerRow)
            .Where(x => !PlayerKey(x).Equals(PlayerKey(source), StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        if (candidates.Count == 0)
        {
            MessageBox.Show("No other known player is available as a clone destination. Refresh players or run Discover Saves first.", "Character Clone", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var destinationCombo = new ComboBox
        {
            ItemsSource = candidates,
            DisplayMemberPath = nameof(PlayerRow.Name),
            SelectedIndex = 0,
            MinWidth = 320,
            Margin = new Thickness(0, 5, 0, 12)
        };
        var inventory = CloneOption("Inventory", true);
        var equipment = CloneOption("Equipment", true);
        var technology = CloneOption("Technology", true);
        var levelStats = CloneOption("Level and Stats", false);
        var appearance = CloneOption("Appearance", false);
        var fastTravel = CloneOption("Fast Travel", false);
        var mapDiscovery = CloneOption("Map Discovery", false);
        var paldeck = CloneOption("Paldeck", false);
        var palbox = CloneOption("Palbox (when present in player save)", false);

        var applyButton = new Button
        {
            Content = "PREVIEW CLONE",
            MinWidth = 150,
            Margin = new Thickness(6, 0, 0, 0),
            IsDefault = true
        };
        var cancelButton = new Button
        {
            Content = "CANCEL",
            MinWidth = 100,
            IsCancel = true
        };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        actions.Children.Add(cancelButton);
        actions.Children.Add(applyButton);

        var body = new StackPanel { Margin = new Thickness(18) };
        body.Children.Add(new TextBlock { Text = "Character Clone", FontSize = 22, FontWeight = FontWeights.Bold, Foreground = Brushes.White });
        body.Children.Add(new TextBlock
        {
            Text = "Copy selected character data from the source player into a different destination player. Every change is previewed, backed up, rebuilt, and verified before activation.",
            Foreground = new SolidColorBrush(Color.FromRgb(175, 201, 232)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 0, 12)
        });
        body.Children.Add(new TextBlock { Text = "SOURCE PLAYER", Foreground = new SolidColorBrush(Color.FromRgb(159, 196, 234)), FontWeight = FontWeights.Bold });
        body.Children.Add(new TextBlock { Text = source.Name + "  •  " + BlankDash(source.PlayerId), Foreground = Brushes.White, Margin = new Thickness(0, 4, 0, 10) });
        body.Children.Add(new TextBlock { Text = "DESTINATION PLAYER", Foreground = new SolidColorBrush(Color.FromRgb(159, 196, 234)), FontWeight = FontWeights.Bold });
        body.Children.Add(destinationCombo);
        body.Children.Add(new TextBlock { Text = "DATA TO COPY", Foreground = new SolidColorBrush(Color.FromRgb(159, 196, 234)), FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 6) });
        foreach (var option in new[] { inventory, equipment, technology, levelStats, appearance, fastTravel, mapDiscovery, paldeck, palbox }) body.Children.Add(option);
        body.Children.Add(new TextBlock
        {
            Text = "Level/Stats and Appearance can overwrite meaningful destination progress. Palbox data is copied only when matching nodes exist in both individual player saves.",
            Foreground = Brushes.Gold,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 10, 0, 0)
        });
        body.Children.Add(actions);

        var dialog = new Window
        {
            Title = "MystTiq Palworld Server — Character Clone",
            Owner = this,
            Width = 620,
            Height = 680,
            MinWidth = 540,
            MinHeight = 600,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.CanResize,
            Background = new SolidColorBrush(Color.FromRgb(11, 17, 24)),
            Content = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }
        };
        applyButton.Click += (_, _) => { dialog.DialogResult = true; dialog.Close(); };
        if (dialog.ShowDialog() != true || destinationCombo.SelectedItem is not PlayerRow destination) return;

        if (destination.Status.Equals("ONLINE", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show("The destination player is online. Disconnect the player before cloning character data.", "Character Clone", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var options = new CharacterCloneOptions
        {
            Inventory = inventory.IsChecked == true,
            Equipment = equipment.IsChecked == true,
            Technology = technology.IsChecked == true,
            LevelAndStats = levelStats.IsChecked == true,
            Appearance = appearance.IsChecked == true,
            FastTravel = fastTravel.IsChecked == true,
            MapDiscovery = mapDiscovery.IsChecked == true,
            Paldeck = paldeck.IsChecked == true,
            Palbox = palbox.IsChecked == true
        };
        if (options.SelectedCategories().Count == 0)
        {
            MessageBox.Show("Select at least one character category to clone.", "Character Clone", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var worldPath = SaveInspector.FindActiveWorldPath();
            if (string.IsNullOrWhiteSpace(worldPath)) throw new DirectoryNotFoundException("No active Palworld world containing Level.sav was found.");
            var engine = new CharacterCloneService(settings);
            Mouse.OverrideCursor = Cursors.Wait;
            var preview = engine.Preview(source, destination, worldPath, server.IsRunning(), options);
            Mouse.OverrideCursor = null;

            var categoryLines = string.Join(Environment.NewLine, preview.Categories.Select(x =>
                "• " + x.Category + ": " + x.Status + " (source " + x.SourceNodes + ", destination " + x.DestinationNodes + ")"));
            var findings = string.Join(Environment.NewLine, preview.Findings.Select(x => "• " + x));
            var message =
                "CHARACTER CLONE PREVIEW\n\n" +
                "Source: " + source.Name + "\n" +
                "Destination: " + destination.Name + "\n" +
                "World: " + worldPath + "\n\n" +
                "Selected data:\n" + categoryLines + "\n\n" +
                "Findings:\n" + findings + "\n\n" +
                "Transaction steps:\n" +
                "• Back up both player saves\n" +
                "• Verify neither save changed after preview\n" +
                "• Copy only matching selected data nodes\n" +
                "• Rebuild and independently decode the destination save\n" +
                "• Restore the original destination automatically if the transaction fails";

            if (!preview.CanApply)
            {
                MessageBox.Show(message + "\n\nThe clone cannot be applied until every blocking finding is resolved.", "Character Clone — Preview", MessageBoxButton.OK, MessageBoxImage.Warning);
                RecordAudit("Warning", "Players", "Character clone blocked", source.Name + " to " + destination.Name + " • " + string.Join("; ", preview.Findings), 4);
                return;
            }

            if (MessageBox.Show(message + "\n\nApply this character clone now?", "Character Clone — Confirm Transaction", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
                return;
            if (MessageBox.Show(
                    "FINAL CONFIRMATION\n\n" +
                    "Copy selected data from " + source.Name + " into " + destination.Name + "?\n\n" +
                    "The destination character data in the selected categories will be replaced. A rollback package will be created first.",
                    "Character Clone — Final Confirmation",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Stop,
                    MessageBoxResult.No) != MessageBoxResult.Yes)
                return;

            Mouse.OverrideCursor = Cursors.Wait;
            var result = engine.Apply(preview);
            RecordAudit("Warning", "Players", "Character clone completed", source.Name + " to " + destination.Name + " • transaction " + result.TransactionId + " • backup " + result.BackupPath, 5);
            Log("[CHARACTER CLONE] Completed " + source.Name + " to " + destination.Name + ". Nodes copied: " + result.NodesCopied + ". Backup: " + result.BackupPath);
            var complete =
                "Character clone completed successfully.\n\n" +
                "Source: " + source.Name + "\n" +
                "Destination: " + destination.Name + "\n" +
                "Categories copied: " + string.Join(", ", result.CategoriesCopied) + "\n" +
                "Nodes copied: " + result.NodesCopied + "\n" +
                "Verification: " + (result.VerificationPassed ? "PASS" : "FAIL") + "\n\n" +
                "Rollback backup:\n" + result.BackupPath + "\n\n" +
                "Report:\n" + result.ReportPath;
            MessageBox.Show(complete, "Character Clone Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            RecordAudit("Critical", "Players", "Character clone failed", source.Name + " to " + destination.Name + ": " + ex.Message, 5);
            Log("[CHARACTER CLONE] Failed: " + ex);
            MessageBox.Show(
                "The character clone failed. MystTiq attempted to restore the original destination save. Review the rollback ZIP and logs before starting PalServer.\n\n" + ex.Message,
                "Character Clone Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally { Mouse.OverrideCursor = null; }
    }

    private static CheckBox CloneOption(string label, bool selected) => new()
    {
        Content = label,
        IsChecked = selected,
        Foreground = Brushes.White,
        Margin = new Thickness(0, 3, 0, 3)
    };

}
