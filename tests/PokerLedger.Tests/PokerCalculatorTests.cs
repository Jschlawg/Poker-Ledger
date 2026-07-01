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
}
