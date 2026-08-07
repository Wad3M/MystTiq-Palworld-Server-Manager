using System.Windows;

namespace PalworldManager.Services.Infrastructure;

public interface IDialogService
{
    bool Confirm(string message, string title, MessageBoxImage image = MessageBoxImage.Question);
    void Info(string message, string title);
    void Warning(string message, string title);
    void Error(string message, string title);
}

public sealed class DialogService : IDialogService
{
    public bool Confirm(string message, string title, MessageBoxImage image = MessageBoxImage.Question)
        => AppDialog.Show(message, title, MessageBoxButton.YesNo, image, MessageBoxResult.No) == MessageBoxResult.Yes;

    public void Info(string message, string title)
        => AppDialog.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public void Warning(string message, string title)
        => AppDialog.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);

    public void Error(string message, string title)
        => AppDialog.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
}

/// <summary>
/// Central compatibility facade for all application dialogs. Keeping the MessageBox-shaped
/// overloads allows existing call sites to migrate without changing behavior while ensuring
/// future dark-theme dialogs, logging and automation hooks have one integration point.
/// </summary>
public static class AppDialog
{
    public static MessageBoxResult Show(string message)
        => MessageBox.Show(message);

    public static MessageBoxResult Show(string message, string caption)
        => MessageBox.Show(message, caption);

    public static MessageBoxResult Show(string message, string caption, MessageBoxButton button)
        => MessageBox.Show(message, caption, button);

    public static MessageBoxResult Show(string message, string caption, MessageBoxButton button, MessageBoxImage icon)
        => MessageBox.Show(message, caption, button, icon);

    public static MessageBoxResult Show(string message, string caption, MessageBoxButton button, MessageBoxImage icon, MessageBoxResult defaultResult)
        => MessageBox.Show(message, caption, button, icon, defaultResult);

    public static MessageBoxResult Show(Window owner, string message, string caption, MessageBoxButton button, MessageBoxImage icon)
        => MessageBox.Show(owner, message, caption, button, icon);

    public static MessageBoxResult Show(Window owner, string message, string caption, MessageBoxButton button, MessageBoxImage icon, MessageBoxResult defaultResult)
        => MessageBox.Show(owner, message, caption, button, icon, defaultResult);
}
