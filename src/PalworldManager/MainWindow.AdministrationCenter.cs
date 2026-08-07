using PalworldManager.Models;

namespace PalworldManager;

public partial class MainWindow
{
    private void RefreshPlayerAdministrationSummary(PlayerRow player)
    {
        var key = PlayerKey(player);
        var summary = playerAdministration.GetSummary(key);
        var banText = summary.IsBanned
            ? summary.TemporaryBanUntilUtc is DateTime until
                ? "Temporary until " + until.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
                : "Banned"
            : "No";
        PlayerAdministrationSummaryText.Text =
            $"Admin: {(summary.IsAdmin ? "Yes" : "No")}   Whitelisted: {(summary.IsWhitelisted ? "Yes" : "No")}\n" +
            $"Banned: {banText}   Notes: {summary.NoteCount}   Active warnings: {summary.ActiveWarningCount}";

        PlayerAdministrationReadinessText.Text = player.Status.Equals("ONLINE", StringComparison.OrdinalIgnoreCase)
            ? "STATUS: ONLINE • KICK/BAN READY • SAVE EDITORS REQUIRE DISCONNECT"
            : "STATUS: OFFLINE • NOTES/WARNINGS/UNBAN READY";
    }

    private bool TryGetAdministrationPlayer(out PlayerRow player, out string key)
    {
        if (PlayersGrid.SelectedItem is not PlayerRow selected)
        {
            AppDialog.Show("Select a player first.", "Administration Center", MessageBoxButton.OK, MessageBoxImage.Information);
            player = default!;
            key = string.Empty;
            return false;
        }
        player = selected;
        key = PlayerKey(selected);
        if (string.IsNullOrWhiteSpace(key))
        {
            AppDialog.Show("The selected player does not have a stable identifier yet.", "Administration Center", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        return true;
    }

    private void PromoteAdmin_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetAdministrationPlayer(out var player, out var key)) return;
        playerAdministration.SetAdmin(key, player.Name, true);
        RecordAudit("Success", "Players", "Player promoted in Administration Center", player.Name, 4);
        Log($"[ADMIN] {player.Name} marked as an administrator in MystTiq.");
        RefreshPlayerAdministrationSummary(player);
        AppDialog.Show(
            "MystTiq now records this player as an administrator.\n\n" +
            "Vanilla Palworld does not expose a persistent remote promotion command. The player must still authenticate in game with /AdminPassword, or be configured through a compatible permissions mod.",
            "Promote Admin",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void RemoveAdmin_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetAdministrationPlayer(out var player, out var key)) return;
        playerAdministration.SetAdmin(key, player.Name, false);
        RecordAudit("Information", "Players", "Player admin designation removed", player.Name, 4);
        Log($"[ADMIN] Removed MystTiq administrator designation from {player.Name}.");
        RefreshPlayerAdministrationSummary(player);
    }

    private void WhitelistPlayer_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetAdministrationPlayer(out var player, out var key)) return;
        playerAdministration.SetWhitelisted(key, player.Name, true);
        RecordAudit("Success", "Players", "Player added to MystTiq whitelist", player.Name, 4);
        RefreshPlayerAdministrationSummary(player);
    }

    private void RemoveWhitelist_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetAdministrationPlayer(out var player, out var key)) return;
        playerAdministration.SetWhitelisted(key, player.Name, false);
        RecordAudit("Information", "Players", "Player removed from MystTiq whitelist", player.Name, 4);
        RefreshPlayerAdministrationSummary(player);
    }

    private void AddAdministrationNote_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetAdministrationPlayer(out var player, out var key)) return;
        var dialog = BuildTextEntryDialog(
            "Add Player Note",
            "Add a private administration note for " + player.Name + ".",
            "General",
            true);
        if (dialog.ShowDialog() != true) return;
        var category = dialog.Tag as string ?? "General";
        var text = dialog.DataContext as string ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text)) return;
        playerAdministration.AddNote(key, player.Name, category, text, Environment.UserName);
        RecordAudit("Information", "Players", "Administration note added", player.Name + " • " + category, 4);
        RefreshPlayerAdministrationSummary(player);
    }

    private void IssuePlayerWarning_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetAdministrationPlayer(out var player, out var key)) return;
        var dialog = BuildTextEntryDialog(
            "Issue Player Warning",
            "Record a warning for " + player.Name + ".",
            "Warning",
            false);
        if (dialog.ShowDialog() != true) return;
        var reason = dialog.DataContext as string ?? string.Empty;
        if (string.IsNullOrWhiteSpace(reason)) return;
        playerAdministration.IssueWarning(key, player.Name, reason, null, Environment.UserName);
        RecordAudit("Warning", "Players", "Player warning issued", player.Name + " • " + reason, 4);
        RefreshPlayerAdministrationSummary(player);
    }

    private void ClearPlayerWarnings_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetAdministrationPlayer(out var player, out var key)) return;
        var count = playerAdministration.ClearActiveWarnings(key, Environment.UserName);
        RecordAudit("Information", "Players", "Player warnings cleared", player.Name + " • " + count + " warning(s)", 4);
        RefreshPlayerAdministrationSummary(player);
        AppDialog.Show($"Cleared {count} active warning(s).", "Warnings", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void TemporaryBan_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetAdministrationPlayer(out var player, out var key)) return;
        var choice = AppDialog.Show(
            "Apply a 24-hour temporary ban to this player?\n\n" +
            "MystTiq will record the expiration and attempt to unban the player after the period expires while the manager and RCON are available.",
            "Temporary Ban",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (choice != MessageBoxResult.Yes) return;

        var id = string.IsNullOrWhiteSpace(player.UserId) ? player.SteamId : player.UserId;
        if (string.IsNullOrWhiteSpace(id))
        {
            AppDialog.Show("The selected player has no UserID or SteamID.", "Temporary Ban", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            if (player.Status.Equals("ONLINE", StringComparison.OrdinalIgnoreCase))
            {
                using var api = Api();
                await api.BanAsync(id, "Temporary 24-hour ban issued by administrator.");
            }
            else
            {
                await EnsureRconConnectedAsync();
                await rcon.ExecuteAsync("BanPlayer " + id);
            }
            var until = DateTime.UtcNow.AddHours(24);
            playerAdministration.SetBan(key, player.Name, until, false);
            playerHistory.MarkBanned(playerHistory.ResolveKey(player), true);
            RecordAudit("Warning", "Players", "Temporary ban issued", player.Name + " • expires " + until.ToLocalTime().ToString("yyyy-MM-dd HH:mm"), 4);
            RefreshPlayerHistoryGrid();
        }
        catch (Exception ex)
        {
            Log("Temporary ban failed: " + ex.Message);
            AppDialog.Show(ex.Message, "Temporary Ban", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private Window BuildTextEntryDialog(string title, string description, string defaultCategory, bool includeCategory)
    {
        var textBox = new TextBox { Height = 110, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 10) };
        var categoryBox = new ComboBox { Margin = new Thickness(0, 8, 0, 4), SelectedIndex = 0 };
        foreach (var category in new[] { defaultCategory, "General", "Griefing", "Exploiting", "Event", "Staff", "Donation" }.Distinct(StringComparer.OrdinalIgnoreCase))
            categoryBox.Items.Add(new ComboBoxItem { Content = category });
        var okay = new Button { Content = "SAVE", IsDefault = true, MinWidth = 100, Margin = new Thickness(6, 0, 0, 0) };
        var cancel = new Button { Content = "CANCEL", IsCancel = true, MinWidth = 100 };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(cancel);
        buttons.Children.Add(okay);
        var panel = new StackPanel { Margin = new Thickness(18) };
        panel.Children.Add(new TextBlock { Text = title, FontSize = 22, FontWeight = FontWeights.Bold, Foreground = Brushes.White });
        panel.Children.Add(new TextBlock { Text = description, Foreground = new SolidColorBrush(Color.FromRgb(175, 201, 232)), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 4) });
        if (includeCategory) panel.Children.Add(categoryBox);
        panel.Children.Add(textBox);
        panel.Children.Add(buttons);
        var window = new Window
        {
            Title = "MystTiq Palworld Server — " + title,
            Owner = this,
            Width = 520,
            Height = includeCategory ? 340 : 300,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            Background = new SolidColorBrush(Color.FromRgb(11, 17, 24)),
            Content = panel
        };
        okay.Click += (_, _) =>
        {
            window.Tag = (categoryBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? defaultCategory;
            window.DataContext = textBox.Text;
            window.DialogResult = true;
        };
        return window;
    }
    private async Task ProcessExpiredTemporaryBansAsync()
    {
        if (!server.IsRunning()) return;
        var expired = playerAdministration.GetExpiredTemporaryBans();
        if (expired.Count == 0) return;
        foreach (var item in expired)
        {
            var id = item.PlayerKey.StartsWith("user:", StringComparison.OrdinalIgnoreCase)
                ? item.PlayerKey[5..]
                : item.PlayerKey.StartsWith("steam:", StringComparison.OrdinalIgnoreCase)
                    ? item.PlayerKey[6..]
                    : string.Empty;
            if (string.IsNullOrWhiteSpace(id))
            {
                playerAdministration.MarkTemporaryBanProcessed(item.PlayerKey);
                continue;
            }
            try
            {
                await EnsureRconConnectedAsync();
                await rcon.ExecuteAsync("UnBanPlayer " + id);
                playerAdministration.MarkTemporaryBanProcessed(item.PlayerKey);
                RecordAudit("Success", "Players", "Temporary ban expired", item.DisplayName, 4);
                Log("[ADMIN] Temporary ban expired and unban was sent for " + item.DisplayName + ".");
            }
            catch (Exception ex)
            {
                Log("[ADMIN] Could not process expired temporary ban for " + item.DisplayName + ": " + ex.Message);
            }
        }
    }

}
