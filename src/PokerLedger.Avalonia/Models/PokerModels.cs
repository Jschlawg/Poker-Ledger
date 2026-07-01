using System.Text.Json.Serialization;

namespace PokerLedger.Models;

public sealed class PokerSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Stakes { get; set; } = "";
    public List<decimal> Blinds { get; set; } = [];
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset? FinalizedAt { get; set; }
    public List<decimal> ChipDenominations { get; set; } = [];
    public decimal DefaultBuyIn { get; set; }
    public decimal BigBlind { get; set; }
    public decimal FinalRake { get; set; }
    public List<Player> Players { get; set; } = [];
    public List<SessionEvent> Events { get; set; } = [];
}

public sealed class Player
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public List<TransactionEntry> Transactions { get; set; } = [];
    public List<CashOutEntry> CashOuts { get; set; } = [];
    public decimal? ManualCashOut { get; set; }
}

public sealed class TransactionEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset Time { get; set; } = DateTimeOffset.Now;
    public string Type { get; set; } = "Buy-In";
    public decimal Amount { get; set; }
    public string Note { get; set; } = "";
}

public sealed class CashOutEntry
{
    public decimal Denomination { get; set; }
    public int Count { get; set; }
}

public sealed class SessionEvent
{
    public DateTimeOffset Time { get; set; } = DateTimeOffset.Now;
    public string Action { get; set; } = "";
    public string Detail { get; set; } = "";
}

public sealed class SetupPreset
{
    public string Name { get; set; } = "";
    public List<decimal> Blinds { get; set; } = [];
    public List<decimal> Denominations { get; set; } = [];
}

public sealed class AppSettings
{
    public decimal DefaultBuyIn { get; set; } = 100;
    public List<SetupPreset> SetupPresets { get; set; } = [];
    public string ContrastMode { get; set; } = "BlueGreenGold";
}

public sealed class SessionExportEnvelope
{
    public string FileType { get; set; } = "PokerLedgerSession";
    public DateTimeOffset ExportedAt { get; set; } = DateTimeOffset.Now;
    public PokerSession? Session { get; set; }
}

public sealed class AppDataBundle
{
    public string FileType { get; set; } = "PokerLedgerAppData";
    public DateTimeOffset ExportedAt { get; set; } = DateTimeOffset.Now;
    public AppSettings? Settings { get; set; }
    public List<string>? PlayerNames { get; set; }
    public List<PokerSession>? Sessions { get; set; }
    public List<TextFileEntry>? Receipts { get; set; }
}

public sealed class TextFileEntry
{
    public string RelativePath { get; set; } = "";
    public string Content { get; set; } = "";
}

public sealed class SessionArchiveItem
{
    public DateTimeOffset Modified { get; set; }
    public DateTimeOffset Created { get; set; }
    public string SessionName { get; set; } = "";
    public int Players { get; set; }
    public bool IsReadOnly { get; set; }
    public string Path { get; set; } = "";

    public override string ToString()
    {
        var name = SessionName.Length > 18 ? SessionName[..15] + "..." : SessionName;
        var status = IsReadOnly ? "  Read only" : "";
        return $"{Modified:MM-dd HH:mm}  {name,-18}  {Players}p{status}";
    }
}

public sealed class PlayerTableItem
{
    public required Player Player { get; init; }
    public decimal TotalIn { get; init; }
    public decimal CashOut { get; init; }
    public decimal Net => CashOut - TotalIn;

    public override string ToString()
    {
        return $"{Player.Name,-24} In: {Money.Format(TotalIn),10}   Out: {Money.Format(CashOut),10}   Net: {Money.Format(Net),10}";
    }
}

public sealed class TransactionTableItem
{
    public required TransactionEntry Transaction { get; init; }

    public override string ToString()
    {
        if (Transaction.Type.Equals("Note", StringComparison.OrdinalIgnoreCase))
        {
            return $"{Transaction.Time:yyyy-MM-dd HH:mm}    {"Note",-7}    {Transaction.Note.Replace("\r", " ").Replace("\n", " ")}";
        }

        var note = string.IsNullOrWhiteSpace(Transaction.Note)
            ? ""
            : "    " + Transaction.Note.Replace("\r", " ").Replace("\n", " ");
        return $"{Transaction.Time:yyyy-MM-dd HH:mm}    {Transaction.Type,-7}    {Money.Format(Transaction.Amount),10}{note}";
    }
}

public static class Money
{
    public static string Format(decimal value)
    {
        var text = Math.Abs(value).ToString("C2", System.Globalization.CultureInfo.GetCultureInfo("en-US"));
        return value < 0 ? $"({text})" : text;
    }

    public static string Key(decimal value)
    {
        return value.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
    }
}
