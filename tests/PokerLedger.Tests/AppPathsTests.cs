using PokerLedger.Services;
using Xunit;

namespace PokerLedger.Tests;

public sealed class AppPathsTests
{
    [Fact]
    public void Constructor_UsesPokerLedgerOverrideRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "PokerLedger.Tests", Guid.NewGuid().ToString("N"));
        var previousRoot = Environment.GetEnvironmentVariable("POKERLEDGER_APPDATA_ROOT");

        try
        {
            Environment.SetEnvironmentVariable("POKERLEDGER_APPDATA_ROOT", root);

            var paths = new AppPaths();

            Assert.Equal(root, paths.AppDataRoot);
            Assert.True(Directory.Exists(paths.DataDirectory));
            Assert.True(Directory.Exists(paths.ReceiptsDirectory));
            Assert.True(Directory.Exists(paths.LogsDirectory));
        }
        finally
        {
            Environment.SetEnvironmentVariable("POKERLEDGER_APPDATA_ROOT", previousRoot);

            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void Constructor_DoesNotCopyLegacyPokerHostAppData()
    {
        var appDataBase = Path.Combine(Path.GetTempPath(), "PokerLedger.Tests", Guid.NewGuid().ToString("N"));
        var previousRoot = Environment.GetEnvironmentVariable("POKERLEDGER_APPDATA_ROOT");
        var legacyRoot = Path.Combine(appDataBase, "PokerHost");
        var legacySession = Path.Combine(legacyRoot, "data", "old-session.json");

        try
        {
            Environment.SetEnvironmentVariable("POKERLEDGER_APPDATA_ROOT", null);
            Directory.CreateDirectory(Path.GetDirectoryName(legacySession)!);
            File.WriteAllText(legacySession, "{}");

            var paths = new AppPaths(appDataDirectoryOverride: appDataBase);

            Assert.Equal(Path.Combine(appDataBase, "PokerLedger"), paths.AppDataRoot);
            Assert.True(Directory.Exists(paths.DataDirectory));
            Assert.False(File.Exists(Path.Combine(paths.DataDirectory, "old-session.json")));
        }
        finally
        {
            Environment.SetEnvironmentVariable("POKERLEDGER_APPDATA_ROOT", previousRoot);

            if (Directory.Exists(appDataBase))
            {
                Directory.Delete(appDataBase, recursive: true);
            }
        }
    }
}
