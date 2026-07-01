namespace PokerLedger.Services;

public sealed class AppPaths
{
    private const string AppFolderName = "PokerLedger";
    private const string LegacyAppFolderName = "PokerHost";
    private const string AppDataOverrideVariable = "POKERLEDGER_APPDATA_ROOT";
    private const string LegacyAppDataOverrideVariable = "POKERHOST_APPDATA_ROOT";

    public string AppDataRoot { get; }
    public string DataDirectory { get; }
    public string ReceiptsDirectory { get; }
    public string LogsDirectory { get; }
    public string SettingsPath { get; }
    public string PlayerProfilesPath { get; }
    public string CrashLogPath { get; }

    public AppPaths()
    {
        var overrideRoot = Environment.GetEnvironmentVariable(AppDataOverrideVariable);
        var legacyOverrideRoot = Environment.GetEnvironmentVariable(LegacyAppDataOverrideVariable);
        if (!string.IsNullOrWhiteSpace(overrideRoot))
        {
            AppDataRoot = overrideRoot;
        }
        else if (!string.IsNullOrWhiteSpace(legacyOverrideRoot))
        {
            AppDataRoot = legacyOverrideRoot;
        }
        else
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            AppDataRoot = Path.Combine(appData, AppFolderName);
            CopyLegacyAppDataIfNeeded(
                Path.Combine(appData, LegacyAppFolderName),
                AppDataRoot);
        }

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

    private static void CopyLegacyAppDataIfNeeded(string legacyRoot, string appDataRoot)
    {
        if (Directory.Exists(appDataRoot) || !Directory.Exists(legacyRoot))
        {
            return;
        }

        CopyDirectory(legacyRoot, appDataRoot);
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var sourceDirectory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(source, sourceDirectory);
            Directory.CreateDirectory(Path.Combine(destination, relativePath));
        }

        foreach (var sourceFile in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(source, sourceFile);
            var destinationFile = Path.Combine(destination, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)!);
            File.Copy(sourceFile, destinationFile, overwrite: false);
            File.SetAttributes(destinationFile, File.GetAttributes(sourceFile));
        }
    }
}
