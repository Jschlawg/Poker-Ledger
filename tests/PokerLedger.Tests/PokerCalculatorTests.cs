using PokerLedger.Models;
using PokerLedger.Services;
using Xunit;

namespace PokerLedger.Tests;

public sealed class PokerCalculatorTests
{
    [Fact]
    public void PlayerTotalIn_SumsBuyInsAndRebuys()
    {
        var player = new Player
        {
            Transactions =
            [
                new TransactionEntry { Type = "Buy-In", Amount = 50 },
                new TransactionEntry { Type = "Rebuy", Amount = 100 },
                new TransactionEntry { Type = "Note", Amount = 0, Note = "Dealer change" }
            ]
        };

        Assert.Equal(150, PokerCalculator.PlayerTotalIn(player));
    }

    [Fact]
    public void PlayerCashOutTotal_UsesManualCashOutWhenPresent()
    {
        var player = new Player
        {
            ManualCashOut = 125,
            CashOuts =
            [
                new CashOutEntry { Denomination = 25, Count = 10 }
            ]
        };

        Assert.Equal(125, PokerCalculator.PlayerCashOutTotal(player));
    }

    [Fact]
    public void SetCashOutCount_ClearsManualCashOutAndUsesChipTotal()
    {
        var session = new PokerSession { ChipDenominations = [5, 10] };
        var player = new Player { ManualCashOut = 123 };

        PokerCalculator.SetCashOutCount(session, player, 10, 7);

        Assert.Null(player.ManualCashOut);
        Assert.Equal(70, PokerCalculator.PlayerCashOutTotal(player));
    }

    [Fact]
    public void ReconciliationDifference_SubtractsCashOutsAndFinalRake()
    {
        var session = new PokerSession
        {
            FinalRake = 10,
            Players =
            [
                new Player
                {
                    Transactions = [new TransactionEntry { Amount = 100 }],
                    ManualCashOut = 80
                },
                new Player
                {
                    Transactions = [new TransactionEntry { Amount = 50 }],
                    ManualCashOut = 40
                }
            ]
        };

        Assert.Equal(20, PokerCalculator.ReconciliationDifference(session));
    }

    [Fact]
    public void SyncPlayerCashOutRows_KeepsCountsAndMatchesSessionDenominations()
    {
        var player = new Player
        {
            CashOuts =
            [
                new CashOutEntry { Denomination = 5, Count = 3 },
                new CashOutEntry { Denomination = 100, Count = 1 }
            ]
        };
        var session = new PokerSession
        {
            ChipDenominations = [25, 5, 1, 5]
        };

        PokerCalculator.SyncPlayerCashOutRows(session, player);

        Assert.Equal([1, 5, 25], player.CashOuts.Select(row => row.Denomination));
        Assert.Equal(3, player.CashOuts.Single(row => row.Denomination == 5).Count);
    }

    [Fact]
    public void PlayersWithCashOutsOutsideDenominations_FindsOnlyAffectedChipCountPlayers()
    {
        var affected = new Player
        {
            Name = "Affected",
            CashOuts = [new CashOutEntry { Denomination = 0.50m, Count = 10 }]
        };
        var unaffected = new Player
        {
            Name = "Unaffected",
            CashOuts = [new CashOutEntry { Denomination = 1m, Count = 10 }]
        };
        var manual = new Player
        {
            Name = "Manual",
            ManualCashOut = 20,
            CashOuts = [new CashOutEntry { Denomination = 0.50m, Count = 10 }]
        };
        var session = new PokerSession
        {
            Players = [affected, unaffected, manual]
        };

        var players = PokerCalculator.PlayersWithCashOutsOutsideDenominations(session, [1m, 5m]);

        Assert.Equal([affected], players);
    }

    [Fact]
    public void ApplyChipDenominations_PreservesCashOutTotalWhenRemovingUsedDenomination()
    {
        var player = new Player
        {
            CashOuts =
            [
                new CashOutEntry { Denomination = 0.50m, Count = 123 },
                new CashOutEntry { Denomination = 1m, Count = 10 }
            ]
        };
        var session = new PokerSession
        {
            ChipDenominations = [0.50m, 1m, 5m],
            Players = [player]
        };

        PokerCalculator.ApplyChipDenominations(session, [1m, 5m], preserveAffectedCashOutTotals: true);

        Assert.Equal(71.50m, player.ManualCashOut);
        Assert.Equal(71.50m, PokerCalculator.PlayerCashOutTotal(player));
        Assert.Equal([1m, 5m], session.ChipDenominations);
        Assert.Equal(10, player.CashOuts.Single(row => row.Denomination == 1m).Count);
    }

    [Fact]
    public void ApplyChipDenominations_DoesNotCreateManualCashOutWhenExistingCountsStillFit()
    {
        var player = new Player
        {
            CashOuts = [new CashOutEntry { Denomination = 1m, Count = 10 }]
        };
        var session = new PokerSession
        {
            ChipDenominations = [1m],
            Players = [player]
        };

        PokerCalculator.ApplyChipDenominations(session, [1m, 5m], preserveAffectedCashOutTotals: true);

        Assert.Null(player.ManualCashOut);
        Assert.Equal(10, PokerCalculator.PlayerCashOutTotal(player));
    }
}
