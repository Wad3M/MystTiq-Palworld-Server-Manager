namespace PalworldManager;

public partial class App : Application
{
    private static string CrashLogPath => Path.Combine(
        Services.ApplicationPathService.Current.LogsRoot,
        $"startup-{DateTime.Now:yyyyMMdd}.log");

    protected override void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
                WriteCrashLog("AppDomain unhandled exception", exception);
        };

        DispatcherUnhandledException += (_, args) =>
        {
            WriteCrashLog("WPF dispatcher exception", args.Exception);
            AppDialog.Show(
                args.Exception.Message + "\n\nDiagnostic log:\n" + CrashLogPath,
                "MystTiq Palworld Server",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        try
        {
            Services.ApplicationPathService.Current.EnsureApplicationDirectories();
            base.OnStartup(e);
            var window = new MainWindow();
            MainWindow = window;
            window.Show();
        }
        catch (Exception exception)
        {
            WriteCrashLog("Fatal startup exception", exception);
            AppDialog.Show(
                "MystTiq Palworld Server could not start.\n\n" + exception +
                "\n\nDiagnostic log:\n" + CrashLogPath,
                "Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    private static void WriteCrashLog(string heading, Exception exception)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CrashLogPath)!);
            File.AppendAllText(
                CrashLogPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {heading}{Environment.NewLine}" +
                exception + Environment.NewLine + new string('-', 80) + Environment.NewLine);
        }
        catch { }
    }
}
