using PalworldManager.Services;

namespace PalworldManager;

public partial class MainWindow
{
    private void OpenWorldValidatorFromSidebar_Click(object sender, RoutedEventArgs e)
    {
        Tabs.SelectedIndex = MainPageIndex.WorldInspector;
        WorldInspectorSectionTabs.SelectedItem = WorldValidatorInspectorTab;
        RefreshWorldValidator(forceRefresh: false);
    }

    private void OpenPlayersManager_Click(object sender, RoutedEventArgs e)
        => Tabs.SelectedIndex = MainPageIndex.Players;

    private void OpenGuildsManager_Click(object sender, RoutedEventArgs e)
        => Tabs.SelectedIndex = MainPageIndex.Guilds;

    private void OpenBasesManager_Click(object sender, RoutedEventArgs e)
        => Tabs.SelectedIndex = MainPageIndex.BaseManager;
}
