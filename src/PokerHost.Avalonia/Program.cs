using Avalonia;
using PokerHost.Services;

namespace PokerHost;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                LogFatal("UnhandledException", ex);
            }
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            LogFatal("UnobservedTaskException", e.Exception);
        };

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            LogFatal("StartupOrFatal", ex);
            throw;
        }
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
    }

    private static void LogFatal(string context, Exception ex)
    {
        try
        {
            var paths = new AppPaths();
            var line = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}] {context}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}";
            File.AppendAllText(paths.CrashLogPath, line);
        }
        catch
        {
            // Best-effort crash logging.
        }
    }
}
