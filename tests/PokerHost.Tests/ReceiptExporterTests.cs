using PokerHost.Models;
using PokerHost.Services;
using Xunit;

namespace PokerHost.Tests;

public sealed class ReceiptExporterTests
{
    [Fact]
    public void Export_WritesReadOnlyReceiptsWithUniqueNamesForDuplicatePlayers()
    {
        using var temp = new TempAppData();
        var exporter = new ReceiptExporter(temp.CreatePaths());
        var session = new PokerSession
        {
            Name = "Duplicate Names",
            StartedAt = new DateTimeOffset(2026, 6, 30, 20, 0, 0, TimeSpan.Zero),
            Stakes = "$0.25 / $0.50",
            ChipDenominations = [1, 5, 25],
            Players =
            [
                PlayerNamed("John"),
                PlayerNamed("John")
            ]
        };

        var folder = exporter.Export(session);

        Assert.True(File.Exists(Path.Combine(folder, "session-summary.txt")));
        Assert.True(File.Exists(Path.Combine(folder, "John.txt")));
        Assert.True(File.Exists(Path.Combine(folder, "John_2.txt")));
        Assert.All(Directory.EnumerateFiles(folder, "*.txt"), path =>
        {
            Assert.True(File.GetAttributes(path).HasFlag(FileAttributes.ReadOnly), path);
        });
    }

    private static Player PlayerNamed(string name)
    {
        return new Player
        {
            Name = name,
            Transactions = [new TransactionEntry { Type = "Buy-In", Amount = 50 }]
        };
    }
}
