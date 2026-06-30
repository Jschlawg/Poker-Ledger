namespace PokerHost.Services;

public sealed class AppPaths
{
    public string AppDataRoot { get; }
    public string DataDirectory { get; }
    public string ReceiptsDirectory { get; }
    public string LogsDirectory { get; }
    public string SettingsPath { get; }
    public string PlayerProfilesPath { get; }
    public string CrashLogPath { get; }

    public AppPaths()
    {
        var overrideRoot = Environment.GetEnvironmentVariable("POKERHOST_APPDATA_ROOT");
        AppDataRoot = string.IsNullOrWhiteSpace(overrideRoot)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PokerHost")
            : overrideRoot;
        DataDirectory = Path.Combine(AppDataRoot, "data");
        ReceiptsDirectory = Path.Combine(AppDataRoot, "receipts");
        LogsDirectory = Path.Combine(AppDataRoot, "logs");
        SettingsPath = Path.Combine(DataDirectory, "settings.json");
        PlayerProfilesPath = Path.Combine(DataDirectory, "player-profiles.json");
        CrashLogPath = Path.Combine(LogsDirectory, "crash.log");

        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(ReceiptsDirectory);
        Directory.CreateDirectory(LogsDirectory);
    }
}
