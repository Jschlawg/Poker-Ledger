using PokerLedger.Services;
using Xunit;

namespace PokerLedger.Tests;

public sealed class AppPathsTests
{
    [Fact]
    public void Constructor_UsesLegacyOverrideRootWhenNewOverrideIsUnset()
    {
        var root = Path.Combine(Path.GetTempPath(), "PokerLedger.Tests", Guid.NewGuid().ToString("N"));
        var previousRoot = Environment.GetEnvironmentVariable("POKERLEDGER_APPDATA_ROOT");
        var previousLegacyRoot = Environment.GetEnvironmentVariable("POKERHOST_APPDATA_ROOT");

        try
        {
            Environment.SetEnvironmentVariable("POKERLEDGER_APPDATA_ROOT", null);
            Environment.SetEnvironmentVariable("POKERHOST_APPDATA_ROOT", root);

            var paths = new AppPaths();

            Assert.Equal(root, paths.AppDataRoot);
            Assert.True(Directory.Exists(paths.DataDirectory));
            Assert.True(Directory.Exists(paths.ReceiptsDirectory));
            Assert.True(Directory.Exists(paths.LogsDirectory));
        }
        finally
        {
            Environment.SetEnvironmentVariable("POKERLEDGER_APPDATA_ROOT", previousRoot);
            Environment.SetEnvironmentVariable("POKERHOST_APPDATA_ROOT", previousLegacyRoot);

            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
