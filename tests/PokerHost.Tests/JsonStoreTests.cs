using PokerHost.Models;
using PokerHost.Services;
using Xunit;

namespace PokerHost.Tests;

public sealed class JsonStoreTests
{
    [Fact]
    public void BuildAndLoadSessionExportJson_RoundTripsWrappedSession()
    {
        using var temp = new TempAppData();
        var store = new JsonStore(temp.CreatePaths());
        var session = new PokerSession
        {
            Name = "Round Trip",
            Stakes = "$0.25 / $0.50",
            Blinds = [0.25m, 0.50m],
            ChipDenominations = [5, 1, 5],
            Players =
            [
                new Player
                {
                    Name = "Jane",
                    Transactions = [new TransactionEntry { Amount = 100 }]
                }
            ]
        };

        var json = store.BuildSessionExportJson(session);
        var loaded = store.LoadSessionJson(json);

        Assert.Equal("Round Trip", loaded.Name);
        Assert.Equal("$0.25 / $0.50", loaded.Stakes);
        Assert.Equal([1, 5], loaded.ChipDenominations);
        Assert.Single(loaded.Players);
    }

    [Fact]
    public void ImportAppData_ValidatesEntireBundleBeforeOverwritingExistingData()
    {
        using var temp = new TempAppData();
        var paths = temp.CreatePaths();
        var store = new JsonStore(paths);
        store.SaveSettings(new AppSettings { DefaultBuyIn = 75 });
        var originalSessionPath = store.SaveSession(new PokerSession
        {
            Name = "Original",
            StartedAt = new DateTimeOffset(2026, 6, 30, 19, 0, 0, TimeSpan.Zero),
            ChipDenominations = [1]
        });
        var originalReceipt = Path.Combine(paths.ReceiptsDirectory, "old", "receipt.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(originalReceipt)!);
        File.WriteAllText(originalReceipt, "keep me");

        var bundle = new AppDataBundle
        {
            Settings = new AppSettings { DefaultBuyIn = 999 },
            PlayerNames = ["New Name"],
            Sessions =
            [
                new PokerSession
                {
                    Name = "Imported",
                    StartedAt = new DateTimeOffset(2026, 6, 30, 20, 0, 0, TimeSpan.Zero),
                    ChipDenominations = [1]
                }
            ],
            Receipts =
            [
                new TextFileEntry
                {
                    RelativePath = Path.Combine("..", "escape.txt"),
                    Content = "bad"
                }
            ]
        };

        var ex = Assert.Throws<InvalidOperationException>(() => store.ImportAppData(
            bundle,
            overwriteDefaults: true,
            overwriteSessions: true,
            overwriteReceipts: true));

        Assert.Contains("escape", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(75, store.LoadSettings().DefaultBuyIn);
        Assert.True(File.Exists(originalSessionPath));
        Assert.Equal("keep me", File.ReadAllText(originalReceipt));
    }
}
