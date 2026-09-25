using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using MystTiq.Desktop.Models;

namespace MystTiq.Desktop.Services;

/// <summary>Shared, lazily decoded artwork. Theme changes reuse bitmaps instead of decoding again.</summary>
public static class ArtworkCatalog
{
    private static readonly Dictionary<string, Bitmap> Cache = new();

    public static string Category(NavigationPage page) => page switch
    {
        NavigationPage.Dashboard => "home",
        NavigationPage.ServerSetup or NavigationPage.Configuration or NavigationPage.Console or NavigationPage.Workspace => "server",
        NavigationPage.Inspector or NavigationPage.WorldTransactions or NavigationPage.Players or NavigationPage.Bases or NavigationPage.Guilds or NavigationPage.Map => "world",
        NavigationPage.Backups => "backups",
        NavigationPage.ModDashboard or NavigationPage.ModLibrary or NavigationPage.Ue4ss => "mods",
        NavigationPage.UpdateCenter or NavigationPage.Doctor or NavigationPage.CrashAnalyzer or NavigationPage.SaveTools or NavigationPage.DiagnosticsCenter => "tools",
        NavigationPage.Settings or NavigationPage.Notifications or NavigationPage.ActivityAudit or NavigationPage.Automation or NavigationPage.Security or NavigationPage.AlertCenter or NavigationPage.Fleet => "system",
        // v0.8.17.0: the HOST tab shows the System art until it has its own.
        // v0.8.25.0: the user's own HOST art (it borrowed the System art since v0.8.17.0).
        NavigationPage.Host => "host",
        _ => throw new ArgumentOutOfRangeException(nameof(page), page, "Assign new pages an artwork category.")
    };

    public static Bitmap Page(NavigationPage page, bool light) =>
        Load($"Artwork/page-art-{Category(page)}-{(light ? "light" : "dark")}.png", 1600);

    public static Bitmap Icon(string name) => Load($"Icons/icon-{name}.png", 112);

    private static Bitmap Load(string path, int width)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue(path, out var bitmap)) return bitmap;
            using var stream = AssetLoader.Open(new Uri($"avares://MystTiq.Desktop/Assets/{path}"));
            bitmap = Bitmap.DecodeToWidth(stream, width);
            Cache.Add(path, bitmap);
            return bitmap;
        }
    }
}

/// <summary>Decode navigation artwork at 112px, over twice its 50-DIP display size (56 until v0.8.0.0), retaining alpha.</summary>
public sealed class IconArtExtension : MarkupExtension
{
    public IconArtExtension(string name) => Name = name;
    public string Name { get; }
    public override object ProvideValue(IServiceProvider serviceProvider) => ArtworkCatalog.Icon(Name);
}
