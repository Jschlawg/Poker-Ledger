using PokerHost.Models;

namespace PokerHost.Services;

public sealed class ReceiptExporter
{
    private readonly AppPaths _paths;

    public ReceiptExporter(AppPaths paths)
    {
        _paths = paths;
    }

    public string Export(PokerSession session)
    {
        var folderName = $"{session.StartedAt:yyyyMMdd-HHmm}_{JsonStore.SafeFilePart(session.Name)}";
        var target = Path.Combine(_paths.ReceiptsDirectory, folderName);
        Directory.CreateDirectory(target);

        var exportEvent = new SessionEvent
        {
            Time = DateTimeOffset.Now,
            Action = "Export",
            Detail = $"Receipts written to {target}"
        };
        session.Events.Add(exportEvent);
        session.UpdatedAt = DateTimeOffset.Now;

        try
        {
            WriteReadOnly(Path.Combine(target, "session-summary.txt"), BuildSessionSummary(session));
            var usedReceiptNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var player in session.Players)
            {
                var receiptPath = UniquePlayerReceiptPath(target, player, usedReceiptNames);
                WriteReadOnly(receiptPath, BuildPlayerReceipt(session, player));
            }
        }
        catch
        {
            session.Events.Remove(exportEvent);
            throw;
        }
        return target;
    }

    private static string UniquePlayerReceiptPath(string targetFolder, Player player, HashSet<string> usedReceiptNames)
    {
        var baseName = JsonStore.SafeFilePart(player.Name);
        var fileName = $"{baseName}.txt";
        var suffix = 2;
        while (!usedReceiptNames.Add(fileName))
        {
            fileName = $"{baseName}_{suffix}.txt";
            suffix++;
        }
        return Path.Combine(targetFolder, fileName);
    }

    private static void WriteReadOnly(string path, string content)
    {
        if (File.Exists(path))
        {
            File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly);
        }

        File.WriteAllText(path, content);
        File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
    }

    public static string BuildPlayerReceipt(PokerSession session, Player player)
    {
        var totalIn = PokerCalculator.PlayerTotalIn(player);
        var cashOut = PokerCalculator.PlayerCashOutTotal(player);
        var lines = new List<string>
        {
            "PokerHost - Player Receipt",
            "Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            "",
            "Session: " + session.Name,
            "Stakes: " + session.Stakes,
            "Started: " + session.StartedAt.ToString("yyyy-MM-dd HH:mm"),
            "Player: " + player.Name,
            "",
            "Entries"
        };

        if (player.Transactions.Count == 0)
        {
            lines.Add("  No buy-ins recorded.");
        }
        else
        {
            lines.AddRange(player.Transactions.Select(tx =>
            {
                if (tx.Type.Equals("Note", StringComparison.OrdinalIgnoreCase))
                {
                    return $"  {tx.Time:yyyy-MM-dd HH:mm}  Note     {tx.Note.Replace("\r", " ").Replace("\n", " ")}";
                }

                var note = string.IsNullOrWhiteSpace(tx.Note)
                    ? ""
                    : "  Note: " + tx.Note.Replace("\r", " ").Replace("\n", " ");
                return $"  {tx.Time:yyyy-MM-dd HH:mm}  {tx.Type,-7}  {Money.Format(tx.Amount),12}{note}";
            }));
        }

        lines.Add("");
        lines.Add("Cash-Out Chips");
        if (player.ManualCashOut.HasValue)
        {
            lines.Add("  Manual cash-out total: " + Money.Format(player.ManualCashOut.Value));
        }

        foreach (var row in player.CashOuts.Where(row => row.Count > 0))
        {
            lines.Add($"  {Money.Format(row.Denomination),10} x {row.Count,-6} = {Money.Format(row.Denomination * row.Count),12}");
        }
        if (cashOut == 0)
        {
            lines.Add("  No cash-out chips recorded.");
        }

        lines.Add("");
        lines.Add("Total In:  " + Money.Format(totalIn));
        lines.Add("Cash Out:  " + Money.Format(cashOut));
        lines.Add("Net:       " + Money.Format(cashOut - totalIn));
        return string.Join(Environment.NewLine, lines);
    }

    public static string BuildSessionSummary(PokerSession session)
    {
        var totalIn = PokerCalculator.SessionTotalIn(session);
        var totalOut = PokerCalculator.SessionTotalOut(session);
        var difference = PokerCalculator.ReconciliationDifference(session);
        var lines = new List<string>
        {
            "PokerHost - Session Summary",
            "Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            "",
            "Session: " + session.Name,
            "Stakes: " + session.Stakes,
            "Started: " + session.StartedAt.ToString("yyyy-MM-dd HH:mm"),
            "Chip Denominations: " + string.Join(", ", session.ChipDenominations.Select(Money.Format)),
            "",
            "Totals",
            "  Buy-ins: " + Money.Format(totalIn),
            "  Player Cash-Outs: " + Money.Format(totalOut),
            "  Final Rake: " + Money.Format(session.FinalRake),
            "  Reconciliation Difference: " + Money.Format(difference),
            "",
            "Players"
        };

        foreach (var player in session.Players)
        {
            var playerIn = PokerCalculator.PlayerTotalIn(player);
            var playerOut = PokerCalculator.PlayerCashOutTotal(player);
            lines.Add($"  {player.Name,-24} In: {Money.Format(playerIn),12}  Out: {Money.Format(playerOut),12}  Net: {Money.Format(playerOut - playerIn),12}");
        }

        lines.Add("");
        lines.Add("History");
        if (session.Events.Count == 0)
        {
            lines.Add("  No history recorded.");
        }
        else
        {
            lines.AddRange(session.Events.Select(e => $"  {e.Time:yyyy-MM-dd HH:mm}  {e.Action}: {e.Detail}"));
        }

        return string.Join(Environment.NewLine, lines);
    }
}
