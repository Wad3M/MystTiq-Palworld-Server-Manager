namespace PalworldManager.Services;

/// <summary>
/// Owns top-level page navigation and sidebar selection synchronization.
/// Keeping this behavior out of MainWindow prevents dashboard links and sidebar
/// buttons from developing separate navigation rules.
/// </summary>
public sealed class NavigationCoordinator
{
    private const string NavigationGroupName = "MainNavigation";
    private readonly Window root;
    private readonly TabControl tabs;

    public NavigationCoordinator(Window root, TabControl tabs)
    {
        this.root = root ?? throw new ArgumentNullException(nameof(root));
        this.tabs = tabs ?? throw new ArgumentNullException(nameof(tabs));
    }

    public int SelectedIndex => tabs.SelectedIndex;

    public bool TryNavigate(int index)
    {
        if (index < 0 || index >= tabs.Items.Count)
            return false;

        tabs.SelectedIndex = index;
        SynchronizeSidebarSelection(index);
        return true;
    }

    public bool TryGetIndex(object? tag, out int index)
    {
        index = -1;
        return tag is not null &&
               int.TryParse(tag.ToString(), out index) &&
               index >= 0 &&
               index < tabs.Items.Count;
    }

    private void SynchronizeSidebarSelection(int index)
    {
        var buttons = FindLogicalChildren<RadioButton>(root)
            .Where(button => string.Equals(button.GroupName, NavigationGroupName, StringComparison.Ordinal))
            .ToList();

        var destination = buttons.FirstOrDefault(button =>
            int.TryParse(button.Tag?.ToString(), out var buttonIndex) && buttonIndex == index);

        if (destination is null)
            return;

        // Navigation buttons are hosted by separate Expander trees. Explicitly
        // reset them because WPF does not always enforce GroupName across those trees.
        foreach (var button in buttons)
            button.IsChecked = false;

        destination.IsChecked = true;
        ExpandContainingSection(destination);
    }

    private static void ExpandContainingSection(DependencyObject destination)
    {
        DependencyObject? parent = destination;
        while ((parent = LogicalTreeHelper.GetParent(parent)) is not null)
        {
            if (parent is not Expander section)
                continue;

            section.IsExpanded = true;
            return;
        }
    }

    private static IEnumerable<T> FindLogicalChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent))
        {
            if (child is T match)
                yield return match;

            if (child is not DependencyObject dependencyChild)
                continue;

            foreach (var descendant in FindLogicalChildren<T>(dependencyChild))
                yield return descendant;
        }
    }
}

/// <summary>Stable page indexes used by navigation and page lifecycle code.</summary>
public static class MainPageIndex
{
    public const int Dashboard = 0;
    public const int Console = 3;
    public const int Players = 4;
    public const int Guilds = 5;
    public const int ModLibrary = 8;
    public const int ModRuntime = 13;
    public const int WorldInspector = 14;
    public const int Recovery = 15;
    public const int BaseManager = 16;
    public const int ActivityAudit = 17;
    public const int Notifications = 18;
    public const int SaveTools = 19;
    public const int WorldValidator = 20;
}
