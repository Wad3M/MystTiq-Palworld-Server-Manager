using Microsoft.Win32;
using PalworldManager.Models;
using PalworldManager.Services;
using PalworldManager.Services.Infrastructure;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PalworldManager;

public partial class MainWindow
{
    private void HandleRuntimeStateChanged(RuntimeStateSnapshot state)
    {
        Log($"[RUNTIME STATE] Revision {state.Revision} • Session {(state.SessionActive ? state.SessionId : "Inactive")} • Loaded aliases {state.LoadedCount} • Errors {state.ErrorCount}");

        // v0.2.15.17: runtime evidence is event-driven. When the authoritative
        // session state gains load evidence, synchronize existing Library and
        // Dashboard rows immediately rather than waiting for a manual Verify All.
        // Do not force a new filesystem/log scan here; that could recurse through
        // RuntimeStateService.Observe(). The cached inventory receives the new
        // immutable runtime snapshot instead.
        _ = Dispatcher.BeginInvoke(() =>
        {
            try
            {
                var snapshot = modCoordinator.RefreshInventory("Runtime evidence update", force: false);
                var installed = snapshot.Mods.ToList();
                runtimeState.ApplyTo(installed);
                ModsGrid.Items.Refresh();
                RefreshModDashboard(installed);
                RefreshModRuntime(installed);
            }
            catch (Exception ex)
            {
                Log("[RUNTIME EVIDENCE] UI synchronization failed: " + ex.Message);
            }
        });
    }

    private void RefreshMods_Click(object s,RoutedEventArgs e)=>RefreshMods();
    private void RefreshMods()
    {
        var snapshot = modCoordinator.RefreshInventory("Scan Library", force: true);
        var installed = snapshot.Mods.ToList();
        runtimeState.ApplyTo(installed);
        ModsGrid.ItemsSource = installed;
        localModRows = new ObservableCollection<LocalModRow>(snapshot.LocalMods);
        LocalModsGrid.ItemsSource = localModRows;
        ModDashLastVerified.Text = $"Last Scan: {snapshot.ScannedAt:MMM d yyyy}  {snapshot.ScannedAt:h:mm tt}  •  Duration: {snapshot.Duration.TotalSeconds:0.0} sec  •  One Scan";
        ModLibrarySummaryText.Text = $"Installed: {installed.Count}  •  Enabled: {installed.Count(x => x.Enabled)}  •  Disabled: {installed.Count(x => !x.Enabled)}";
        RefreshModDashboard(installed);
        RefreshModRuntime(installed);

        // Local workshop packages are often identified only by their numeric Steam ID.
        // Resolve friendly Workshop titles in the background and keep the ID visible
        // in brackets so the name is readable without losing the exact package identity.
        _ = RefreshWorkshopDisplayNamesAsync(installed, localModRows, forceRefresh: false);
    }

    private async Task RefreshWorkshopDisplayNamesAsync(IList<ModRow> installed, IList<LocalModRow> localRows, bool forceRefresh)
    {
        try
        {
            var ids = localRows.Select(row => row.WorkshopId)
                .Concat(installed.Select(GetWorkshopId))
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var resolved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var id in ids)
            {
                try
                {
                    var metadata = await GetWorkshopMetadataAsync(id, forceRefresh);
                    var title = CleanWorkshopTitle(metadata.Title);
                    if (!string.IsNullOrWhiteSpace(title))
                        resolved[id] = $"{title} ({id})";
                }
                catch (Exception ex)
                {
                    Log($"Workshop name lookup failed for {id}: {ex.Message}");
                }
            }

            if (resolved.Count == 0)
                return;

            foreach (var row in localRows)
                if (resolved.TryGetValue(row.WorkshopId, out var displayName))
                    row.Name = displayName;

            foreach (var row in installed)
            {
                var id = GetWorkshopId(row);
                if (!string.IsNullOrWhiteSpace(id) && resolved.TryGetValue(id, out var displayName))
                    row.Name = displayName;
            }

            // These row models do not implement INotifyPropertyChanged, so explicitly
            // refresh the views after title enrichment. Preserve current selections.
            var selectedInstalledPackage = (ModsGrid.SelectedItem as ModRow)?.Package;
            var selectedLocalId = (LocalModsGrid.SelectedItem as LocalModRow)?.WorkshopId;
            ModsGrid.Items.Refresh();
            LocalModsGrid.Items.Refresh();
            RefreshModDashboard(installed);

            if (!string.IsNullOrWhiteSpace(selectedInstalledPackage))
                ModsGrid.SelectedItem = installed.FirstOrDefault(row => row.Package.Equals(selectedInstalledPackage, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(selectedLocalId))
                LocalModsGrid.SelectedItem = localRows.FirstOrDefault(row => row.WorkshopId.Equals(selectedLocalId, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            Log("Workshop display-name refresh failed: " + ex.Message);
        }
    }

    private static string GetWorkshopId(ModRow row)
    {
        const string prefix = "Steam Workshop ";
        return row.Source.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? row.Source[prefix.Length..].Trim()
            : string.Empty;
    }

    private static string CleanWorkshopTitle(string title)
    {
        var value = WebUtility.HtmlDecode(title ?? string.Empty).Trim();
        foreach (var prefix in new[] { "Steam Workshop::", "Steam Community :: Workshop :: " })
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                value = value[prefix.Length..].Trim();
        return value;
    }

    private void RefreshModDashboard(IEnumerable<ModRow> installed)
    {
        var rows = installed.ToList();
        var previousRows = modDashboardRows.Count > 0 ? modDashboardRows.ToList() : LoadPersistedModScanResults();
        var updated = modDashboardState.RefreshFromInventory(rows, previousRows, server.IsRunning());
        modDashboardRows = new ObservableCollection<ModDashboardRow>(updated);
        ModDashboardGrid.ItemsSource = modDashboardRows;
        SelectFirstModDashboardRow();
        UpdateModDashboardSummary(rows.Count, modDashboardRows, modDashboardRows.Any(row => row.LastVerified != "Never"));
    }

    private List<ModDashboardRow> LoadPersistedModScanResults()
    {
        try
        {
            if (!File.Exists(ModScanResultsPath)) return [];
            return System.Text.Json.JsonSerializer.Deserialize<List<ModDashboardRow>>(File.ReadAllText(ModScanResultsPath)) ?? [];
        }
        catch (Exception ex)
        {
            Log("[MODS] Persisted scan results could not be loaded: " + ex.Message);
            return [];
        }
    }

    private void SavePersistedModScanResults(IEnumerable<ModDashboardRow> rows)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ModScanResultsPath)!);
            File.WriteAllText(ModScanResultsPath,
                System.Text.Json.JsonSerializer.Serialize(rows.ToList(), new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            Log("[MODS] Scan results could not be persisted: " + ex.Message);
        }
    }

    private void ModDashboardGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateSelectedModDetails(ModDashboardGrid.SelectedItem as ModDashboardRow);
    }

    private void UpdateSelectedModDetails(ModDashboardRow? row)
    {
        if (row is null)
        {
            ModDetailName.Text = "Select a mod";
            ModDetailType.Text = string.Empty;
            ModDetailHealth.Text = "—";
            ModDetailScore.Text = "—";
            ModDetailInstallation.Text = string.Empty;
            ModDetailRuntime.Text = string.Empty;
            ModDetailCompatibility.Text = string.Empty;
            ModDetailEvidence.Text = "Select a mod to view verification evidence.";
            return;
        }

        ModDetailName.Text = row.Name;
        ModDetailType.Text = row.Type;
        ModDetailHealth.Text = row.Health;
        ModDetailScore.Text = row.Health.ToUpperInvariant();
        ModDetailInstallation.Text = $"Files: {row.FilesStatus}\nEnabled: {row.EnabledStatus}";
        ModDetailRuntime.Text = $"Runtime: {row.RuntimeStatus}\nErrors: {row.ErrorStatus}\nLast verified: {row.LastVerified}";
        ModDetailCompatibility.Text = $"Dependencies: {row.DependencyStatus}\nConflicts: {row.ConflictStatus}\nVersion: {row.VersionStatus}\nStatic compatibility: {row.Compatibility}";
        ModDetailEvidence.Text = string.IsNullOrWhiteSpace(row.Details) ? "No additional evidence has been recorded." : row.Details;
    }

    private void SelectFirstModDashboardRow()
    {
        if (modDashboardRows.Count > 0)
        {
            ModDashboardGrid.SelectedIndex = 0;
            UpdateSelectedModDetails(modDashboardRows[0]);
        }
        else
        {
            UpdateSelectedModDetails(null);
        }
    }

    private void VerifySelectedMod_Click(object sender, RoutedEventArgs e)
    {
        if (ModDashboardGrid.SelectedItem is not ModDashboardRow selected)
        {
            AppDialog.Show("Select a mod first.", "MOD Verification", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        try
        {
            var installed = modCoordinator.RefreshInventory("Verify selected MOD", force: true).Mods.ToList();
            var mod = installed.FirstOrDefault(item => item.Package.Equals(selected.Package, StringComparison.OrdinalIgnoreCase));
            if (mod is null)
                throw new InvalidOperationException("The selected mod is no longer installed.");

            var result = modCoordinator.VerifyOne(mod);
            selected.FilesStatus = result.FilesStatus;
            selected.EnabledStatus = result.Enabled ? "Enabled" : "Disabled";
            selected.RuntimeStatus = result.RuntimeStatus;
            selected.ErrorStatus = result.ErrorSummary;
            selected.Health = ModHealthEvaluationService.ToDisplayText(result.HealthStatus);
            selected.HealthScore = result.HealthScore;
            selected.Details = result.Details;
            selected.LastVerified = result.VerifiedAt.ToString("g");

            // Rebind because ModDashboardRow is intentionally a simple view model.
            ModDashboardGrid.ItemsSource = null;
            ModDashboardGrid.ItemsSource = modDashboardRows;
            ModDashboardGrid.SelectedItem = selected;
            UpdateSelectedModDetails(selected);
            UpdateModDashboardSummary(installed.Count, modDashboardRows, true);
        }
        catch (Exception ex)
        {
            AppDialog.Show(ex.Message, "MOD Verification", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateModDashboardSummary(int installedCount, IEnumerable<ModDashboardRow> results, bool runtimeChecked)
    {
        var rows = results.ToList();
        var summary = modDashboardState.Summarize(rows);
        var healthyCount = summary.Healthy;
        var runtimeUnverifiedCount = summary.RuntimeUnverified;
        var failedCount = summary.Failed;
        var attentionCount = summary.Attention + summary.Failed + summary.Unknown;
        var updates = summary.Updates;
        var conflicts = summary.Conflicts;
        var missingDependencies = summary.MissingDependencies;

        ModDashInstalled.Text = installedCount.ToString();
        ModDashHealthy.Text = healthyCount.ToString();
        ModDashUpdates.Text = updates.ToString();
        ModDashConflicts.Text = conflicts.ToString();
        ModDashDependencies.Text = missingDependencies.ToString();
        ModDashFailed.Text = failedCount.ToString();
        if (runtimeChecked) ModDashLastVerified.Text = $"Last Scan: {DateTime.Now:MMM d yyyy}  {DateTime.Now:h:mm tt}  •  Verification complete";

        if (installedCount == 0)
        {
            ModDashHealthText.Text = "No mods detected";
            ModDashHealthDetails.Text = "Install mods from the MOD Library to begin verification.";
            ModDashScore.Text = "—";
            ModDashHealthBanner.Background = new SolidColorBrush(Color.FromRgb(70, 85, 104));
            return;
        }

        ModDashScore.Text = failedCount > 0 ? "FAILED"
            : attentionCount > 0 ? "ATTENTION"
            : runtimeUnverifiedCount > 0 ? "UNVERIFIED"
            : runtimeChecked ? "WORKING"
            : "UNKNOWN";
        if (runtimeChecked && failedCount == 0 && attentionCount == 0)
        {
            ModDashHealthText.Text = "All detected mods are healthy";
            ModDashHealthDetails.Text = "All detected mods passed their centralized health rules. UE4SS/Lua mods have matching runtime load evidence; non-UE4SS mods passed installation and enabled-state verification.";
            ModDashHealthBanner.Background = new SolidColorBrush(Color.FromRgb(23, 58, 42));
        }
        else if (runtimeChecked && attentionCount == 0 && runtimeUnverifiedCount > 0)
        {
            ModDashHealthText.Text = $"{runtimeUnverifiedCount} mod{(runtimeUnverifiedCount == 1 ? "" : "s")} awaiting runtime confirmation";
            ModDashHealthDetails.Text = "No failures were detected. These MODs are installed, enabled, and active, but MystTiq has not observed mod-specific positive runtime evidence in this session.";
            ModDashHealthBanner.Background = new SolidColorBrush(Color.FromRgb(32, 64, 96));
        }
        else if (attentionCount == 0)
        {
            ModDashHealthText.Text = runtimeChecked ? "Runtime verification complete" : "Mods detected — verification required";
            ModDashHealthDetails.Text = runtimeChecked
                ? "No failures were detected. Run Scan Compatibility to check dependencies, local version differences, and mod overlap."
                : "Inventory refresh completed. Health remains Unknown until Verify All Mods establishes runtime evidence.";
            ModDashHealthBanner.Background = runtimeChecked
                ? new SolidColorBrush(Color.FromRgb(23, 58, 42))
                : new SolidColorBrush(Color.FromRgb(32, 64, 96));
        }
        else
        {
            ModDashHealthText.Text = $"{attentionCount} mod{(attentionCount == 1 ? "" : "s")} need attention";
            ModDashHealthDetails.Text = failedCount > 0
                ? $"{failedCount} failed or missing. Review runtime, error, and evidence details."
                : runtimeUnverifiedCount > 0
                    ? $"{runtimeUnverifiedCount} UE4SS mod{(runtimeUnverifiedCount == 1 ? "" : "s")} are active but runtime-unverified. Start/refresh the server and verify again for UE4SS load evidence."
                    : "Review misconfigured mods, state mismatches, duplicates, or other verification evidence.";
            ModDashHealthBanner.Background = new SolidColorBrush(Color.FromRgb(74, 54, 24));
        }
    }

    private void VerifyAllMods_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ModDashHealthText.Text = "Verifying installed mods…";
            ModDashHealthDetails.Text = "Checking files, enabled state, recent UE4SS/server logs, runtime evidence, and errors.";
            ModDashScore.Text = "…";

            var workflow = modCoordinator.VerifyAll();
            var inventory = workflow.Inventory;
            var installed = inventory.Mods.ToList();
            ModsGrid.ItemsSource = installed;
            localModRows = new ObservableCollection<LocalModRow>(inventory.LocalMods);
            LocalModsGrid.ItemsSource = localModRows;
            ModLibrarySummaryText.Text = $"Installed: {installed.Count}  •  Enabled: {installed.Count(x => x.Enabled)}  •  Disabled: {installed.Count(x => !x.Enabled)}";

            modDashboardRows = new ObservableCollection<ModDashboardRow>(
                modDashboardState.FromVerification(workflow.Verification, workflow.Compatibility));
            ModDashboardGrid.ItemsSource = modDashboardRows;
            SavePersistedModScanResults(modDashboardRows);
            SelectFirstModDashboardRow();
            UpdateModDashboardSummary(installed.Count, modDashboardRows, true);
            ModDashLastVerified.Text = $"Last Scan: {inventory.ScannedAt:MMM d yyyy}  {inventory.ScannedAt:h:mm tt}  •  Duration: {inventory.Duration.TotalSeconds:0.0} sec  •  One Scan";

            var summary = modDashboardState.Summarize(modDashboardRows);
            AppDialog.Show(
                summary.Failed == 0 && summary.Attention == 0 && summary.RuntimeUnverified == 0 && summary.Unknown == 0
                    ? $"Verification completed. {summary.Healthy} healthy; {summary.Disabled} disabled. All enabled detected mods satisfy their centralized health rules."
                    : $"Verification completed. Healthy: {summary.Healthy}; Active / Unverified: {summary.RuntimeUnverified}; Attention/Misconfigured: {summary.Attention}; Disabled: {summary.Disabled}; Failed/Missing: {summary.Failed}; Unknown: {summary.Unknown}. Review the MOD Dashboard for details.",
                "MOD Runtime Verification",
                MessageBoxButton.OK,
                summary.Failed > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            AppDialog.Show($"MOD verification could not be completed.\n\n{ex.Message}", "MOD Verification", MessageBoxButton.OK, MessageBoxImage.Error);
            RefreshMods();
        }
    }

    private void ExportModVerificationReport_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var workflow = modCoordinator.ExportVerification();
            var exported = workflow.Export;
            Log($"MOD verification report exported: {exported.TextPath}");
            AppDialog.Show(
                $"Verification report exported for {exported.ModCount} MOD(s).\n\nText: {exported.TextPath}\nJSON: {exported.JsonPath}",
                "MOD Verification Report", MessageBoxButton.OK, MessageBoxImage.Information);
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{exported.TextPath}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppDialog.Show($"The MOD verification report could not be exported.\n\n{ex.Message}",
                "MOD Verification Report", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ScanCompatibility_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ModDashHealthText.Text = "Scanning mod compatibility…";
            ModDashHealthDetails.Text = "Checking declared dependencies, managed file overlap, known conflict rules, feature overlap, and local Workshop versions.";

            var workflow = modCoordinator.ScanCompatibility();
            var installed = workflow.Inventory.Mods.ToList();
            ModsGrid.ItemsSource = installed;
            localModRows = new ObservableCollection<LocalModRow>(workflow.Inventory.LocalMods);
            LocalModsGrid.ItemsSource = localModRows;

            modDashboardRows = new ObservableCollection<ModDashboardRow>(
                modDashboardState.ApplyCompatibility(installed, modDashboardRows, workflow.Compatibility, localModRows, server.IsRunning()));
            ModDashboardGrid.ItemsSource = modDashboardRows;
            SelectFirstModDashboardRow();
            UpdateModDashboardSummary(installed.Count, modDashboardRows, modDashboardRows.Any(row => row.RuntimeStatus != "Not checked"));

            var compatibleCount = modDashboardRows.Count(row => row.Compatibility == "Compatible");
            var updateCount = modDashboardRows.Count(row => row.VersionStatus.StartsWith("Update", StringComparison.OrdinalIgnoreCase));
            var conflictCount = modDashboardRows.Count(row => row.Compatibility == "Conflict");
            var missingCount = modDashboardRows.Count(row => row.DependencyStatus.StartsWith("Missing ", StringComparison.OrdinalIgnoreCase));
            var attentionCount = modDashboardRows.Count(row => row.Compatibility == "Attention");
            var message = $"Compatibility scan complete. Compatible: {compatibleCount}; Updates: {updateCount}; Conflicts: {conflictCount}; Missing dependencies: {missingCount}; Attention: {attentionCount}.";
            ModDashHealthDetails.Text = message + " Runtime verification remains separate; use Verify All Mods to confirm loading.";
            AppDialog.Show(message, "MOD Compatibility Scan", MessageBoxButton.OK,
                conflictCount > 0 || missingCount > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            AppDialog.Show($"Compatibility scan could not be completed.\n\n{ex.Message}", "MOD Compatibility", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenModLibrary_Click(object sender, RoutedEventArgs e) => NavigateToPage(8);

    private void OpenModsRoot_Click(object sender, RoutedEventArgs e)
    {
        var path = ue4ssRuntimeResolver.GetActiveModsRoot();
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
    }

    private void MigrateLegacyMods_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var preview = mods.InspectLegacyMigration();
            if (!preview.IsMigrationRequired || preview.CandidateCount == 0)
            {
                AppDialog.Show(
                    $"No user MOD folders require migration.\n\nLegacy: {preview.LegacyRoot}\nActive: {preview.ActiveRoot}",
                    "UE4SS Legacy MOD Migration",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            if (server.IsRunning())
            {
                AppDialog.Show(
                    "Stop PalServer before migrating legacy UE4SS MOD files. The migration is copy-first and non-destructive, but MOD files should not be changing while they are copied.",
                    "Stop Server Before Migration",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var legacyOnly = preview.LegacyOnlyMods.Count;
            var alreadyPresent = preview.AlreadyPresentMods.Count;
            var skipped = preview.SkippedRuntimeComponents.Count;
            var choice = AppDialog.Show(
                $"UE4SS mod path mismatch detected.\n\nManaged / legacy:\n{preview.LegacyRoot}\n\nActive UE4SS:\n{preview.ActiveRoot}\n\n" +
                $"Legacy-only user MODs: {legacyOnly}\nAlready present in active root: {alreadyPresent}\nUE4SS runtime component folders skipped: {skipped}\n\n" +
                "MystTiq will COPY missing user MOD files into the active root. Existing active files will never be overwritten, and the legacy copies will not be deleted. Continue?",
                "Migrate Legacy UE4SS MODs",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            if (choice != MessageBoxResult.Yes) return;

            var result = mods.MigrateLegacyMods();
            RefreshMods();
            Log($"UE4SS legacy migration completed: {result.CopiedModCount} mod folder(s), {result.CopiedFileCount} file(s) copied, {result.ConflictCount} conflict(s) preserved.");

            var message = $"Migration completed.\n\nMOD folders copied: {result.CopiedModCount}\nFiles copied: {result.CopiedFileCount}\nConflicts preserved: {result.ConflictCount}\n\nLegacy copies were retained for rollback safety.";
            if (result.Warnings.Count > 0)
                message += "\n\nNotes:\n" + string.Join("\n", result.Warnings.Select(item => "• " + item));

            AppDialog.Show(message, "UE4SS Legacy MOD Migration", MessageBoxButton.OK,
                result.ConflictCount == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            Log($"UE4SS legacy migration failed: {ex.Message}");
            AppDialog.Show(ex.Message, "UE4SS Legacy MOD Migration Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    private void ModsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModsGrid.SelectedItem is not ModRow mod) return;
        LocalModsGrid.SelectedItem = null;
        ModInfoName.Text = mod.Name;
        ModInfoSource.Text = $"Installed server mod • {mod.Source} • {mod.Type}";
        ModInfoStatus.Text = mod.Status;
        ModInfoVersion.Text = string.IsNullOrWhiteSpace(mod.Version) ? "Version not provided" : mod.Version;
        ModInfoAuthor.Text = "Unknown";
        ModInfoDescription.Text = mod.Description;
        ModInfoDetails.Text = $"Package: {mod.Package}\r\nDeployed: {(mod.Deployed ? "Yes" : "No")}\r\nEnabled: {(mod.Enabled ? "Yes" : "No")}";
        ModInfoCompatibility.Text = BuildInstalledCompatibility(mod);
        ModInfoOnlineStatus.Text = "No Workshop ID is associated with this installed package. Use Search Online to look for public information.";
        SetModHealth(mod.Deployed && mod.Enabled ? "READY TO USE" : mod.Deployed ? "DISABLED" : "ATTENTION REQUIRED", mod.Deployed && mod.Enabled ? "ready" : mod.Deployed ? "warning" : "error");
    }

    private void LocalModsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LocalModsGrid.SelectedItem is not LocalModRow mod) return;
        ModsGrid.SelectedItem = null;
        ModInfoName.Text = mod.Name;
        ModInfoSource.Text = $"Steam Workshop • ID {mod.WorkshopId}";
        ModInfoStatus.Text = mod.UpdateStatus;
        ModInfoVersion.Text = $"Steam copy: {mod.AvailableVersion}\r\nInstalled: {mod.InstalledVersion}";
        ModInfoAuthor.Text = mod.Author;
        ModInfoDescription.Text = mod.Description;
        ModInfoDetails.Text = $"Type: {mod.Type}\r\nOptions: {mod.Variants}\r\nSize: {mod.Size}\r\nLocal update time: {mod.LastUpdated:g}";
        ModInfoCompatibility.Text = BuildWorkshopCompatibility(mod);
        ModInfoOnlineStatus.Text = "Checking the Steam Workshop for current information...";
        SetModHealth(mod.UpdateStatus == "UPDATE AVAILABLE" ? "UPDATE RECOMMENDED" : mod.UpdateStatus == "CURRENT" ? "READY TO USE" : "AVAILABLE TO INSTALL", mod.UpdateStatus == "UPDATE AVAILABLE" ? "warning" : "ready");
        _ = LoadOnlineWorkshopInfoAsync(mod, forceRefresh: false);
    }


    private string BuildInstalledCompatibility(ModRow mod)
    {
        var ue4ssReady = environment.VerifyComponent("UE4SS Runtime").Success;
        try
        {
            var result = modCompatibility.Scan(mods.Scan()).Results
                .FirstOrDefault(item => item.Package.Equals(mod.Package, StringComparison.OrdinalIgnoreCase));
            if (result is not null)
            {
                return $"Palworld Dedicated Server: {(mod.Deployed ? "Detected" : "Files missing")}\r\n" +
                       $"UE4SS Runtime: {(ue4ssReady ? "Installed" : "Needs attention")}\r\n" +
                       $"Dependencies: {result.DependencyStatus}\r\n" +
                       $"Conflicts: {result.ConflictStatus}\r\n" +
                       $"Version: {result.VersionStatus}\r\n" +
                       $"Overall: {result.OverallStatus}\r\n" +
                       "Crossplay: Unknown unless declared by the mod author";
            }
        }
        catch { }

        return $"Palworld Dedicated Server: {(mod.Deployed ? "Detected" : "Files missing")}\r\n" +
               $"UE4SS Runtime: {(ue4ssReady ? "Installed" : "Needs attention")}\r\n" +
               "Compatibility scan unavailable\r\nCrossplay: Unknown";
    }

    private string BuildWorkshopCompatibility(LocalModRow mod)
    {
        var ue4ssReady = environment.VerifyComponent("UE4SS Runtime").Success;
        return $"Palworld version: {mod.Compatibility}\r\n" +
               $"UE4SS Runtime: {(ue4ssReady ? "Installed" : "Needs attention")}\r\n" +
               "Dedicated server: Verify with the author\r\nClient installation: Check description\r\nCrossplay: Check description\r\nDependencies: Check Workshop requirements";
    }

    private void SetModHealth(string text, string state)
    {
        ModHealthText.Text = text;
        ModHealthBanner.Background = state switch
        {
            "ready" => new SolidColorBrush(Color.FromRgb(31, 139, 76)),
            "warning" => new SolidColorBrush(Color.FromRgb(179, 122, 24)),
            "error" => new SolidColorBrush(Color.FromRgb(176, 57, 57)),
            _ => new SolidColorBrush(Color.FromRgb(70, 85, 104))
        };
    }

    private async void RefreshSelectedModInfo_Click(object sender, RoutedEventArgs e)
    {
        // REFRESH INFO is a local/runtime refresh action. Searching the web is kept
        // exclusively behind SEARCH ONLINE so the two buttons never perform the same
        // action or unexpectedly launch a browser.
        if (ModsGrid.SelectedItem is ModRow installed)
        {
            var package = installed.Package;
            RefreshMods();
            var refreshed = (ModsGrid.ItemsSource as IEnumerable<ModRow>)?
                .FirstOrDefault(row => row.Package.Equals(package, StringComparison.OrdinalIgnoreCase));
            if (refreshed is not null)
            {
                ModsGrid.SelectedItem = refreshed;
                ModsGrid.ScrollIntoView(refreshed);
                ModInfoOnlineStatus.Text = "Local metadata, deployment state, and current-session runtime status refreshed.";
            }
            return;
        }

        if (LocalModsGrid.SelectedItem is LocalModRow local)
        {
            var workshopId = local.WorkshopId;
            RefreshMods();
            var refreshed = localModRows.FirstOrDefault(row =>
                row.WorkshopId.Equals(workshopId, StringComparison.OrdinalIgnoreCase));
            if (refreshed is not null)
            {
                LocalModsGrid.SelectedItem = refreshed;
                LocalModsGrid.ScrollIntoView(refreshed);
                if (!string.IsNullOrWhiteSpace(refreshed.WorkshopId))
                    await LoadOnlineWorkshopInfoAsync(refreshed, forceRefresh: true);
                else
                    ModInfoOnlineStatus.Text = "Local metadata refreshed. No Workshop ID is available for an online metadata refresh.";
            }
            return;
        }

        AppDialog.Show("Select an installed or local MOD first.", "Refresh MOD Info", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void SearchSelectedModOnline_Click(object sender, RoutedEventArgs e)
    {
        var name = LocalModsGrid.SelectedItem is LocalModRow local ? local.Name : ModsGrid.SelectedItem is ModRow installed ? installed.Name : string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            AppDialog.Show("Select a mod first.", "Search Online", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var query = Uri.EscapeDataString($"Palworld mod {name}");
        Process.Start(new ProcessStartInfo($"https://www.google.com/search?q={query}") { UseShellExecute = true });
    }

    private async Task LoadOnlineWorkshopInfoAsync(LocalModRow mod, bool forceRefresh)
    {
        if (string.IsNullOrWhiteSpace(mod.WorkshopId)) return;
        try
        {
            ModInfoOnlineStatus.Text = forceRefresh ? "Refreshing Workshop information..." : "Loading Workshop information...";
            var metadata = await GetWorkshopMetadataAsync(mod.WorkshopId, forceRefresh);
            if (LocalModsGrid.SelectedItem is not LocalModRow selected || selected.WorkshopId != mod.WorkshopId) return;

            if (!string.IsNullOrWhiteSpace(metadata.Title))
            {
                var title = CleanWorkshopTitle(metadata.Title);
                ModInfoName.Text = string.IsNullOrWhiteSpace(title) ? $"Workshop Mod ({mod.WorkshopId})" : $"{title} ({mod.WorkshopId})";
            }
            if (!string.IsNullOrWhiteSpace(metadata.Author)) ModInfoAuthor.Text = metadata.Author;
            if (!string.IsNullOrWhiteSpace(metadata.Description)) ModInfoDescription.Text = metadata.Description;
            ModInfoOnlineStatus.Text = $"Steam Workshop information refreshed {metadata.FetchedUtc.ToLocalTime():g}.";
            if (!string.IsNullOrWhiteSpace(metadata.LastUpdated))
                ModInfoVersion.Text = $"Steam copy: {mod.AvailableVersion}\r\nInstalled: {mod.InstalledVersion}\r\nWorkshop updated: {metadata.LastUpdated}";
        }
        catch (Exception ex)
        {
            if (LocalModsGrid.SelectedItem is LocalModRow selected && selected.WorkshopId == mod.WorkshopId)
                ModInfoOnlineStatus.Text = "Online information could not be retrieved. Local metadata is still available. " + ex.Message;
            Log("Workshop metadata lookup failed: " + ex.Message);
        }
    }

    private async Task<WorkshopMetadata> GetWorkshopMetadataAsync(string workshopId, bool forceRefresh)
    {
        var cacheDirectory = Path.Combine(ApplicationPathService.Current.CacheRoot, "Mods");
        Directory.CreateDirectory(cacheDirectory);
        var cachePath = Path.Combine(cacheDirectory, workshopId + ".json");
        if (!forceRefresh && File.Exists(cachePath))
        {
            var age = DateTime.UtcNow - File.GetLastWriteTimeUtc(cachePath);
            if (age < TimeSpan.FromDays(7))
            {
                var cached = JsonSerializer.Deserialize<WorkshopMetadata>(await File.ReadAllTextAsync(cachePath));
                if (cached is not null) return cached;
            }
        }

        var url = $"https://steamcommunity.com/sharedfiles/filedetails/?id={Uri.EscapeDataString(workshopId)}";
        var html = await modMetadataClient.GetStringAsync(url);
        var metadata = new WorkshopMetadata
        {
            WorkshopId = workshopId,
            Title = ExtractMeta(html, "og:title"),
            Description = ExtractMeta(html, "og:description"),
            Author = ExtractAuthor(html),
            LastUpdated = ExtractWorkshopUpdated(html),
            FetchedUtc = DateTime.UtcNow
        };
        await File.WriteAllTextAsync(cachePath, JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }));
        return metadata;
    }

    private static string ExtractMeta(string html, string property)
    {
        var pattern = $"<meta[^>]+property=[\\\"']{Regex.Escape(property)}[\\\"'][^>]+content=[\\\"'](?<value>.*?)[\\\"'][^>]*>";
        var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!match.Success)
        {
            pattern = $"<meta[^>]+content=[\\\"'](?<value>.*?)[\\\"'][^>]+property=[\\\"']{Regex.Escape(property)}[\\\"'][^>]*>";
            match = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        }
        return match.Success ? WebUtility.HtmlDecode(match.Groups["value"].Value).Trim() : string.Empty;
    }

    private static string ExtractAuthor(string html)
    {
        var match = Regex.Match(html, "<div[^>]+class=[\\\"'][^\\\"']*friendBlockContent[^\\\"']*[\\\"'][^>]*>(?<value>.*?)<", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var raw = match.Success ? Regex.Replace(match.Groups["value"].Value, "<.*?>", string.Empty) : string.Empty;
        return WebUtility.HtmlDecode(raw).Trim();
    }

    private static string ExtractWorkshopUpdated(string html)
    {
        var match = Regex.Match(html, "Updated</div>\\s*<div[^>]*>(?<value>.*?)</div>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? WebUtility.HtmlDecode(Regex.Replace(match.Groups["value"].Value, "<.*?>", string.Empty)).Trim() : string.Empty;
    }

    private sealed class WorkshopMetadata
    {
        public string WorkshopId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string LastUpdated { get; set; } = string.Empty;
        public DateTime FetchedUtc { get; set; }
    }

    private void OpenSelectedModFolder_Click(object sender, RoutedEventArgs e)
    {
        string? path = null;
        if (ModsGrid.SelectedItem is ModRow installed)
        {
            var managed = Path.Combine(settings.ManagedModsRoot, installed.Package);
            var ue4ss = Path.Combine(ue4ssRuntimeResolver.GetActiveModsRoot(), installed.Package);
            path = Directory.Exists(managed) ? managed : Directory.Exists(ue4ss) ? ue4ss : null;
        }
        else if (LocalModsGrid.SelectedItem is LocalModRow local)
            path = local.SourcePath;

        if (path is null || !Directory.Exists(path))
        {
            AppDialog.Show("The selected mod folder could not be found.", "Open Mod Folder", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
    }

    private void OpenSelectedWorkshop_Click(object sender, RoutedEventArgs e)
    {
        if (LocalModsGrid.SelectedItem is not LocalModRow mod || string.IsNullOrWhiteSpace(mod.WorkshopId))
        {
            AppDialog.Show("Select a local Steam Workshop mod first.", "Open Workshop", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        Process.Start(new ProcessStartInfo($"https://steamcommunity.com/sharedfiles/filedetails/?id={mod.WorkshopId}") { UseShellExecute = true });
    }

    private void EnableSelectedMod_Click(object sender, RoutedEventArgs e) => SetSelectedModEnabled(true);

    private void DisableSelectedMod_Click(object sender, RoutedEventArgs e) => SetSelectedModEnabled(false);

    private void SetSelectedModEnabled(bool enabled)
    {
        if (ModsGrid.SelectedItem is not ModRow selected)
        {
            AppDialog.Show("Select an installed mod first.", enabled ? "Enable Mod" : "Disable Mod", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (server.IsRunning())
        {
            var proceed = AppDialog.Show(
                $"'{selected.Name}' will be {(enabled ? "enabled" : "disabled")} on disk, but PalServer is currently running.\n\nThe server must be restarted before the runtime state changes. Continue?",
                enabled ? "Enable Mod" : "Disable Mod",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (proceed != MessageBoxResult.Yes) return;
        }

        try
        {
            // Re-scan from disk so this explicit action is authoritative and is not
            // affected by unsaved checkbox edits elsewhere in the grid.
            var currentRows = mods.Scan().ToList();
            var target = currentRows.FirstOrDefault(row => row.Package.Equals(selected.Package, StringComparison.OrdinalIgnoreCase));
            if (target is null)
                throw new InvalidOperationException("The selected mod could not be found in the current server inventory.");

            target.Enabled = enabled;
            var result = mods.Apply(currentRows);
            // RefreshMods() also refreshes the MOD Dashboard using the current scan.
            RefreshMods();

            var action = enabled ? "enabled" : "disabled";
            Log($"Mod '{selected.Name}' {action}. {result.ChangedItemCount} runtime file/folder change(s).");
            var message = $"{selected.Name} has been {action}." +
                          (server.IsRunning() ? "\n\nRestart PalServer for the change to take effect." : "\n\nThe next server start will use this state.");
            var selectedWarnings = result.Warnings
                .Where(warning => warning.StartsWith(selected.Name + ":", StringComparison.OrdinalIgnoreCase) ||
                                  warning.StartsWith(selected.Package + ":", StringComparison.OrdinalIgnoreCase))
                .Select(warning =>
                {
                    var colon = warning.IndexOf(':');
                    return colon >= 0 ? warning[(colon + 1)..].Trim() : warning;
                })
                .ToList();
            if (selectedWarnings.Count > 0)
                message += "\n\nWarnings:\n" + string.Join("\n", selectedWarnings.Take(8));
            AppDialog.Show(message, enabled ? "Mod Enabled" : "Mod Disabled", MessageBoxButton.OK,
                selectedWarnings.Count == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            Log($"Failed to {(enabled ? "enable" : "disable")} mod '{selected.Name}': {ex.Message}");
            AppDialog.Show(ex.Message, enabled ? "Enable Mod Failed" : "Disable Mod Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }


    private void EnableAllMods_Click(object sender, RoutedEventArgs e)
    {
        var currentRows = mods.Scan().ToList();
        if (currentRows.Count == 0)
        {
            AppDialog.Show("No installed mods were found.", "Enable All Mods", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (server.IsRunning())
        {
            var proceed = AppDialog.Show("Enable all discovered mods on disk? PalServer must be restarted before runtime state changes.", "Enable All Mods", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (proceed != MessageBoxResult.Yes) return;
        }
        foreach (var row in currentRows) row.Enabled = true;
        var result = mods.Apply(currentRows);
        modInventory.Invalidate();
        RefreshMods();
        Log($"Enabled all mods: {result.EnabledCount} enabled, {result.ChangedItemCount} runtime file/folder change(s).");
        AppDialog.Show($"All discovered mods have been enabled. Files/folders changed: {result.ChangedItemCount}." + (server.IsRunning() ? "\n\nRestart PalServer for the runtime state to change." : ""), "All Mods Enabled", MessageBoxButton.OK, result.Warnings.Count == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }

    private void DisableAllMods_Click(object sender, RoutedEventArgs e)
    {
        var currentRows = mods.Scan().ToList();
        if (currentRows.Count == 0)
        {
            AppDialog.Show("No installed mods were found.", "Disable All Mods", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var answer = AppDialog.Show(
            $"Disable all {currentRows.Count} installed server mod(s)?\n\nWorkshop downloads and ZIP-installed files will be retained so they can be enabled again later." +
            (server.IsRunning() ? "\n\nPalServer is running. Restart the server after applying this change." : string.Empty),
            "Disable All Mods", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes)
            return;

        foreach (var row in currentRows)
            row.Enabled = false;

        var result = mods.Apply(currentRows);
        RefreshMods();
        Log($"Disabled all mods: {result.DisabledCount} disabled, {result.ChangedItemCount} runtime file/folder change(s).");

        var message = $"All installed mods have been disabled.\n\nFiles/folders changed: {result.ChangedItemCount}.";
        if (result.Warnings.Count > 0)
            message += "\n\nWarnings:\n" + string.Join("\n", result.Warnings.Take(8));
        if (server.IsRunning())
            message += "\n\nRestart PalServer for the runtime state to change.";
        AppDialog.Show(message, "All Mods Disabled", MessageBoxButton.OK,
            result.Warnings.Count == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }

    private void RepairModStates_Click(object sender, RoutedEventArgs e)
    {
        if (server.IsRunning())
        {
            var proceed = AppDialog.Show(
                "Repairing UE4SS activation state changes files on disk. The currently running PalServer will not change until it is restarted. Continue?",
                "Repair MOD States", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (proceed != MessageBoxResult.Yes) return;
        }

        try
        {
            var result = mods.RepairUe4ssStates();
            RefreshMods();
            Log($"Repaired UE4SS mod activation state: {result.RepairedMarkers} enabled.txt override(s) neutralized, {result.EntriesAdded} mods.txt entr{(result.EntriesAdded == 1 ? "y" : "ies")} added.");

            var message = $"UE4SS state reconciliation complete.\n\n" +
                          $"enabled.txt overrides neutralized: {result.RepairedMarkers}\n" +
                          $"mods.txt entries added: {result.EntriesAdded}\n\n" +
                          "mods.txt is now the authoritative activation source for managed UE4SS user mods.";
            if (result.Warnings.Count > 0)
                message += "\n\nWarnings:\n" + string.Join("\n", result.Warnings.Take(8));
            if (server.IsRunning())
                message += "\n\nRestart PalServer for runtime state to match the repaired configuration.";

            AppDialog.Show(message, "MOD States Repaired", MessageBoxButton.OK,
                result.Warnings.Count == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            Log($"Failed to repair UE4SS mod states: {ex.Message}");
            AppDialog.Show(ex.Message, "Repair MOD States Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApplyMods_Click(object sender, RoutedEventArgs e)
    {
        if (ModsGrid.ItemsSource is not IEnumerable<ModRow> rows)
            return;

        var result = mods.Apply(rows);
        RefreshMods();

        var message = $"Applied mod states.\n\nEnabled: {result.EnabledCount}\nDisabled: {result.DisabledCount}\nFiles/folders changed: {result.ChangedItemCount}";
        if (result.Warnings.Count > 0)
            message += "\n\nWarnings:\n" + string.Join("\n", result.Warnings.Take(8));
        message += "\n\nRestart the Palworld server for all changes to take effect.";

        Log($"Applied mod states: {result.EnabledCount} enabled, {result.DisabledCount} disabled, {result.ChangedItemCount} file/folder changes.");
        AppDialog.Show(message, "Mod Changes Applied", MessageBoxButton.OK,
            result.Warnings.Count == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }

    private void DeleteMod_Click(object sender, RoutedEventArgs e)
    {
        if (ModsGrid.SelectedItem is not ModRow mod)
        {
            AppDialog.Show("Select a mod first.", "Delete Mod", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var choice = AppDialog.Show(
            $"Permanently remove '{mod.Name}' and all files recorded for this mod?\n\n" +
            "Shared files that belong to another separately installed mod are not tracked automatically. " +
            "Only continue when this is the mod you intend to remove.",
            "Delete Mod",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (choice != MessageBoxResult.Yes)
            return;

        _ = RunExclusive(_ =>
        {
            var result = mods.Delete(mod.Package);
            RefreshMods();
            Log($"Deleted mod '{mod.Name}': {result.DeletedFileCount} associated file(s) removed" +
                (result.MissingFileCount > 0 ? $", {result.MissingFileCount} already missing." : "."));
            return Task.CompletedTask;
        });
    }

    private void BrowseModZip_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select a Palworld mod package",
            Filter = "Mod packages (*.zip;*.rar;*.7z)|*.zip;*.rar;*.7z|ZIP archives (*.zip)|*.zip|RAR archives (*.rar)|*.rar|7Z archives (*.7z)|*.7z",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == true)
            _ = InstallModZipAsync(dialog.FileName);
    }

    private Task InstallModZipAsync(string zipPath) => RunExclusive(async ct =>
    {
        Log("Checking mod package: " + zipPath);
        var preview = await Task.Run(() => mods.InspectZip(zipPath), ct);
        var overwrite = false;

        var dependencyText = preview.Dependencies.Count == 0
            ? "None detected"
            : string.Join(", ", preview.Dependencies);
        var analysisChoice = AppDialog.Show(
            $"Package Analysis\n\n" +
            $"Name: {preview.Name}\n" +
            $"Type: {preview.PackageType}\n" +
            $"Install location: {preview.InstallLocation}\n" +
            $"Dependencies: {dependencyText}\n\n" +
            "Continue with installation?",
            "Smart MOD Installer",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information,
            MessageBoxResult.Yes);

        if (analysisChoice != MessageBoxResult.Yes)
        {
            Log($"Installation cancelled after package analysis for '{preview.Name}'.");
            return;
        }

        var requiresStoppedServer = preview.PackageType.Contains("Win64 Loader", StringComparison.OrdinalIgnoreCase);
        if (requiresStoppedServer && server.IsRunning())
        {
            var stopChoice = AppDialog.Show(
                $"{preview.Name} installs native DLL files directly into the Palworld Win64 folder. PalServer must be stopped before these files can be installed safely.\n\nStop the server and continue?",
                "Stop Server for Native MOD Install",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.Yes);
            if (stopChoice != MessageBoxResult.Yes)
            {
                Log($"Installation cancelled because PalServer remained running for '{preview.Name}'.");
                return;
            }

            Log($"Stopping PalServer before installing native MOD '{preview.Name}'.");
            await server.ForceStopAsync();
            if (server.IsRunning())
                throw new InvalidOperationException("PalServer could not be stopped. The native mod installation was cancelled before any files were changed.");
        }

        if (preview.AlreadyExists)
        {
            var conflictSummary = preview.ExistingFiles.Count == 1
                ? "1 installed file will be replaced."
                : $"{preview.ExistingFiles.Count} installed files will be replaced.";

            var choice = AppDialog.Show(
                $"{preview.Name} is already installed.\n\n" +
                conflictSummary + "\n\n" +
                "MystTiq will stage and validate the new package, back up the current installation, replace it as an upgrade, preserve its enabled state, remove obsolete files, and roll back automatically if any step fails.\n\nUpgrade this mod?",
                "Upgrade Installed Mod",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.Yes);

            if (choice != MessageBoxResult.Yes)
            {
                Log($"Upgrade cancelled. Existing mod '{preview.Name}' was not changed.");
                return;
            }

            if (server.IsRunning())
            {
                var stopChoice = AppDialog.Show(
                    "PalServer is running and may have mod files locked. MystTiq must stop it before performing this upgrade.\n\nStop the server and continue?",
                    "Stop Server for MOD Upgrade",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning,
                    MessageBoxResult.Yes);
                if (stopChoice != MessageBoxResult.Yes)
                {
                    Log($"Upgrade cancelled because PalServer remained running for '{preview.Name}'.");
                    return;
                }

                Log($"Stopping PalServer before upgrading '{preview.Name}'.");
                await server.ForceStopAsync();
                if (server.IsRunning())
                    throw new InvalidOperationException("PalServer could not be stopped. The mod upgrade was cancelled before any files were changed.");
            }

            overwrite = true;
            Log($"Transactional upgrade approved for existing mod '{preview.Name}'.");
        }

        Log((overwrite ? "Staging mod upgrade" : "Installing mod package") + ": " + zipPath);
        var result = await Task.Run(() => mods.InstallZip(zipPath, overwrite), ct);
        RefreshMods();

        Log($"{(overwrite ? "Upgraded transactionally" : "Installed")} {result.PackageType} '{result.Name}' ({result.InstalledFileCount} files). It now appears in the Mods list.");
        if (result.SkippedFiles.Count > 0)
            Log($"Skipped {result.SkippedFiles.Count} unrecognized documentation or unsupported files.");

        AppDialog.Show(
            $"{(overwrite ? "Upgraded" : "Installed")} {result.Name}.\n\nFiles installed: {result.InstalledFileCount}\n" +
            "The mod has been added to the Mods list. Enable it if required, apply the enabled list, and restart the server.",
            overwrite ? "Mod Updated" : "Mod Installed",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    });

}
