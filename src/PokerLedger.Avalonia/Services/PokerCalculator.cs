using PokerLedger.Models;

namespace PokerLedger.Services;

public static class PokerCalculator
{
    public static decimal PlayerTotalIn(Player player)
    {
        return player.Transactions.Sum(t => t.Amount);
    }

    public static decimal PlayerCashOutTotal(Player player)
    {
        if (player.ManualCashOut.HasValue)
        {
            return player.ManualCashOut.Value;
        }

        return player.CashOuts.Sum(row => row.Denomination * row.Count);
    }

    public static decimal SessionTotalIn(PokerSession session)
    {
        return session.Players.Sum(PlayerTotalIn);
    }

    public static decimal SessionTotalOut(PokerSession session)
    {
        return session.Players.Sum(PlayerCashOutTotal);
    }

    public static decimal ReconciliationDifference(PokerSession session)
    {
        return SessionTotalIn(session) - SessionTotalOut(session) - session.FinalRake;
    }

    public static void SyncAllCashOutRows(PokerSession session)
    {
        foreach (var player in session.Players)
        {
            SyncPlayerCashOutRows(session, player);
        }
    }

    public static void SyncPlayerCashOutRows(PokerSession session, Player player)
    {
        var existing = player.CashOuts.ToDictionary(row => Money.Key(row.Denomination), row => row.Count);
        player.CashOuts = session.ChipDenominations
            .Where(d => d > 0)
            .Distinct()
            .OrderBy(d => d)
            .Select(d => new CashOutEntry
            {
                Denomination = d,
                Count = existing.TryGetValue(Money.Key(d), out var count) ? count : 0
            })
            .ToList();
    }

    public static void SetCashOutCount(PokerSession session, Player player, decimal denomination, int count)
    {
        SyncPlayerCashOutRows(session, player);
        player.ManualCashOut = null;
        var key = Money.Key(denomination);
        var row = player.CashOuts.FirstOrDefault(r => Money.Key(r.Denomination) == key);
        if (row is not null)
        {
            row.Count = Math.Max(0, count);
        }
    }

    public static void AddHistory(PokerSession session, string action, string detail)
    {
        session.Events.Add(new SessionEvent
        {
            Time = DateTimeOffset.Now,
            Action = action,
            Detail = detail
        });
        session.UpdatedAt = DateTimeOffset.Now;
    }
}
