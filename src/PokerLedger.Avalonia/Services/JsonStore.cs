using System.Text.Json;
using System.Text.Json.Serialization;
using PokerLedger.Models;

namespace PokerLedger.Services;

public sealed class JsonStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private readonly AppPaths _paths;

    public JsonStore(AppPaths paths)
    {
        _paths = paths;
    }

    public AppSettings LoadSettings()
    {
        if (!File.Exists(_paths.SettingsPath))
        {
            return new AppSettings();
        }

        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_paths.SettingsPath), JsonOptions);
            return settings ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void SaveSettings(AppSettings settings)
    {
        File.WriteAllText(_paths.SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
    }

    public List<string> LoadPlayerNames()
    {
        if (!File.Exists(_paths.PlayerProfilesPath))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(File.ReadAllText(_paths.PlayerProfilesPath), JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public void SavePlayerNames(IEnumerable<string> names)
    {
        var clean = names
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        File.WriteAllText(_paths.PlayerProfilesPath, JsonSerializer.Serialize(clean, JsonOptions));
    }

    public IReadOnlyList<SessionArchiveItem> GetArchive()
    {
        return Directory.EnumerateFiles(_paths.DataDirectory, "*.json")
            .Where(path =>
            {
                var file = Path.GetFileName(path);
                return !file.Equals("settings.json", StringComparison.OrdinalIgnoreCase)
                    && !file.Equals("player-profiles.json", StringComparison.OrdinalIgnoreCase);
            })
            .Select(ToArchiveItem)
            .Where(item => item is not null)
            .Cast<SessionArchiveItem>()
            .OrderByDescending(item => item.Modified)
            .ToList();
    }

    public PokerSession LoadSession(string path)
    {
        var json = File.ReadAllText(path);
        return LoadSessionJson(json);
    }

    public PokerSession LoadSessionJson(string json)
    {
        PokerSession? session = null;
        using (var doc = JsonDocument.Parse(json))
        {
            if (doc.RootElement.TryGetProperty("Session", out var wrapped))
            {
                session = wrapped.Deserialize<PokerSession>(JsonOptions);
            }
            else
            {
                session = doc.RootElement.Deserialize<PokerSession>(JsonOptions);
            }
        }

        if (session is null)
        {
            throw new InvalidOperationException("That file does not look like a Poker Ledger session.");
        }

        Normalize(session);
        return session;
    }

    public string SaveSession(PokerSession session, string? existingPath = null)
    {
        Normalize(session);
        session.UpdatedAt = DateTimeOffset.Now;
        var path = string.IsNullOrWhiteSpace(existingPath)
            ? Path.Combine(_paths.DataDirectory, $"{session.StartedAt:yyyyMMdd-HHmmss}_{SafeFilePart(session.Name)}.json")
            : existingPath;
        if (File.Exists(path) && File.GetAttributes(path).HasFlag(FileAttributes.ReadOnly))
        {
            throw new InvalidOperationException("This session is locked read-only. Delete it from the archive or export a copy instead.");
        }
        File.WriteAllText(path, JsonSerializer.Serialize(session, JsonOptions));
        return path;
    }

    public string ExportSessionJson(PokerSession session, string path)
    {
        ClearReadOnlyIfFileExists(path);
        File.WriteAllText(path, BuildSessionExportJson(session));
        return path;
    }

    public string BuildSessionExportJson(PokerSession session)
    {
        Normalize(session);
        var envelope = new SessionExportEnvelope
        {
            ExportedAt = DateTimeOffset.Now,
            Session = session
        };
        return JsonSerializer.Serialize(envelope, JsonOptions);
    }

    public string BuildAppDataExportJson(bool includeDefaults, bool includeSessions, bool includeReceipts)
    {
        var bundle = new AppDataBundle
        {
            ExportedAt = DateTimeOffset.Now
        };

        if (includeDefaults)
        {
            bundle.Settings = LoadSettings();
            bundle.PlayerNames = LoadPlayerNames();
        }

        if (includeSessions)
        {
            bundle.Sessions = GetArchive()
                .Select(item => LoadSession(item.Path))
                .ToList();
        }

        if (includeReceipts)
        {
            bundle.Receipts = Directory.EnumerateFiles(_paths.ReceiptsDirectory, "*", SearchOption.AllDirectories)
                .Where(path => !File.GetAttributes(path).HasFlag(FileAttributes.Directory))
                .Select(path => new TextFileEntry
                {
                    RelativePath = Path.GetRelativePath(_paths.ReceiptsDirectory, path),
                    Content = File.ReadAllText(path)
                })
                .ToList();
        }

        return JsonSerializer.Serialize(bundle, JsonOptions);
    }

    public AppDataBundle LoadAppDataBundleJson(string json)
    {
        var bundle = JsonSerializer.Deserialize<AppDataBundle>(json, JsonOptions);
        if (bundle is null || !IsSupportedAppDataFileType(bundle.FileType))
        {
            throw new InvalidOperationException("That file does not look like a Poker Ledger app data export.");
        }
        return bundle;
    }

    private static bool IsSupportedAppDataFileType(string? fileType)
    {
        return fileType is not null
            && (fileType.Equals("PokerLedgerAppData", StringComparison.OrdinalIgnoreCase)
            || fileType.Equals("PokerHostAppData", StringComparison.OrdinalIgnoreCase));
    }

    public void ImportAppData(AppDataBundle bundle, bool overwriteDefaults, bool overwriteSessions, bool overwriteReceipts)
    {
        ValidateAppDataBundleForImport(bundle);

        if (bundle.Settings is not null || bundle.PlayerNames is not null)
        {
            if (overwriteDefaults)
            {
                if (bundle.Settings is not null)
                {
                    SaveSettings(bundle.Settings);
                }
                if (bundle.PlayerNames is not null)
                {
                    SavePlayerNames(bundle.PlayerNames);
                }
            }
            else if (bundle.PlayerNames is not null)
            {
                SavePlayerNames(LoadPlayerNames().Concat(bundle.PlayerNames));
            }
        }

        if (bundle.Sessions is { Count: > 0 })
        {
            if (overwriteSessions)
            {
                PurgeArchive();
            }
            foreach (var session in bundle.Sessions)
            {
                Normalize(session);
                var path = Path.Combine(_paths.DataDirectory, $"{session.StartedAt:yyyyMMdd-HHmmss}_{SafeFilePart(session.Name)}.json");
                if (!overwriteSessions && File.Exists(path))
                {
                    path = Path.Combine(_paths.DataDirectory, $"{session.StartedAt:yyyyMMdd-HHmmss}_{SafeFilePart(session.Name)}_{Guid.NewGuid():N}.json");
                }
                SaveSession(session, path);
            }
        }

        if (bundle.Receipts is { Count: > 0 })
        {
            if (overwriteReceipts)
            {
                PurgeReceipts();
            }
            foreach (var receipt in bundle.Receipts)
            {
                if (string.IsNullOrWhiteSpace(receipt.RelativePath))
                {
                    continue;
                }
                var target = SafeChildPath(_paths.ReceiptsDirectory, receipt.RelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                if (!overwriteReceipts && File.Exists(target))
                {
                    var dir = Path.GetDirectoryName(target)!;
                    var name = Path.GetFileNameWithoutExtension(target);
                    var ext = Path.GetExtension(target);
                    target = Path.Combine(dir, $"{name}_{Guid.NewGuid():N}{ext}");
                }
                File.WriteAllText(target, receipt.Content);
            }
        }
    }

    private void ValidateAppDataBundleForImport(AppDataBundle bundle)
    {
        if (bundle.Settings is not null)
        {
            _ = JsonSerializer.Serialize(bundle.Settings, JsonOptions);
        }

        if (bundle.PlayerNames is not null)
        {
            _ = JsonSerializer.Serialize(bundle.PlayerNames, JsonOptions);
        }

        if (bundle.Sessions is not null)
        {
            for (var i = 0; i < bundle.Sessions.Count; i++)
            {
                var session = bundle.Sessions[i];
                if (session is null)
                {
                    throw new InvalidOperationException($"Imported session #{i + 1} is empty.");
                }

                Normalize(session);
                _ = JsonSerializer.Serialize(session, JsonOptions);
            }
        }

        if (bundle.Receipts is not null)
        {
            for (var i = 0; i < bundle.Receipts.Count; i++)
            {
                var receipt = bundle.Receipts[i];
                if (receipt is null)
                {
                    throw new InvalidOperationException($"Imported receipt #{i + 1} is empty.");
                }

                if (string.IsNullOrWhiteSpace(receipt.RelativePath))
                {
                    continue;
                }

                if (receipt.Content is null)
                {
                    throw new InvalidOperationException($"Imported receipt \"{receipt.RelativePath}\" has no content.");
                }

                var target = SafeChildPath(_paths.ReceiptsDirectory, receipt.RelativePath);
                if (string.IsNullOrWhiteSpace(Path.GetFileName(target)))
                {
                    throw new InvalidOperationException($"Imported receipt \"{receipt.RelativePath}\" does not name a file.");
                }

                if (Path.GetDirectoryName(target) is null)
                {
                    throw new InvalidOperationException($"Imported receipt \"{receipt.RelativePath}\" has an invalid folder.");
                }
            }
        }
    }

    public void DeleteSession(string path)
    {
        var fullData = Path.GetFullPath(_paths.DataDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullTarget = Path.GetFullPath(path);
        if (!fullTarget.StartsWith(fullData + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("That file is outside the Poker Ledger archive.");
        }

        DeleteFileIfExists(fullTarget);
    }

    public void LockSessionFile(string path)
    {
        var fullData = Path.GetFullPath(_paths.DataDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullTarget = Path.GetFullPath(path);
        if (!fullTarget.StartsWith(fullData + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("That file is outside the Poker Ledger archive.");
        }

        if (File.Exists(fullTarget))
        {
            File.SetAttributes(fullTarget, File.GetAttributes(fullTarget) | FileAttributes.ReadOnly);
        }
    }

    public void ResetDefaults()
    {
        if (File.Exists(_paths.SettingsPath))
        {
            DeleteFileIfExists(_paths.SettingsPath);
        }
        if (File.Exists(_paths.PlayerProfilesPath))
        {
            DeleteFileIfExists(_paths.PlayerProfilesPath);
        }
    }

    public void PurgeArchive()
    {
        foreach (var item in GetArchive())
        {
            DeleteFileIfExists(item.Path);
        }
    }

    public void PurgeReceipts()
    {
        if (!Directory.Exists(_paths.ReceiptsDirectory))
        {
            Directory.CreateDirectory(_paths.ReceiptsDirectory);
            return;
        }

        foreach (var path in Directory.EnumerateFiles(_paths.ReceiptsDirectory, "*", SearchOption.AllDirectories))
        {
            DeleteFileIfExists(path);
        }
        foreach (var dir in Directory.EnumerateDirectories(_paths.ReceiptsDirectory, "*", SearchOption.AllDirectories).OrderByDescending(path => path.Length))
        {
            if (!Directory.EnumerateFileSystemEntries(dir).Any())
            {
                File.SetAttributes(dir, File.GetAttributes(dir) & ~FileAttributes.ReadOnly);
                Directory.Delete(dir);
            }
        }
    }

    public void Normalize(PokerSession session)
    {
        if (string.IsNullOrWhiteSpace(session.Id))
        {
            session.Id = Guid.NewGuid().ToString("N");
        }
        if (string.IsNullOrWhiteSpace(session.Name))
        {
            session.Name = $"Poker Session {DateTime.Now:yyyy-MM-dd}";
        }
        if (session.StartedAt == default)
        {
            session.StartedAt = DateTimeOffset.Now;
        }
        if (session.UpdatedAt == default)
        {
            session.UpdatedAt = DateTimeOffset.Now;
        }
        session.Blinds = session.Blinds
            .Where(blind => blind > 0)
            .ToList();
        if (session.Blinds.Count == 0)
        {
            session.Blinds = ParseMoneyList(session.Stakes).ToList();
        }
        if (session.Blinds.Count > 0)
        {
            session.BigBlind = session.Blinds.Count >= 2 ? session.Blinds[1] : session.Blinds[0];
            session.Stakes = string.Join(" / ", session.Blinds.Select(Money.Format));
        }
        session.ChipDenominations = session.ChipDenominations
            .Where(d => d > 0)
            .Distinct()
            .OrderBy(d => d)
            .DefaultIfEmpty(1m)
            .ToList();

        foreach (var player in session.Players)
        {
            if (string.IsNullOrWhiteSpace(player.Id))
            {
                player.Id = Guid.NewGuid().ToString("N");
            }
            if (string.IsNullOrWhiteSpace(player.Name))
            {
                player.Name = "Unnamed Player";
            }
            if (player.CreatedAt == default)
            {
                player.CreatedAt = DateTimeOffset.Now;
            }
            foreach (var transaction in player.Transactions)
            {
                if (string.IsNullOrWhiteSpace(transaction.Id))
                {
                    transaction.Id = Guid.NewGuid().ToString("N");
                }
                if (transaction.Time == default)
                {
                    transaction.Time = DateTimeOffset.Now;
                }
                if (string.IsNullOrWhiteSpace(transaction.Type))
                {
                    transaction.Type = "Buy-In";
                }
            }
        }

        PokerCalculator.SyncAllCashOutRows(session);
    }

    private SessionArchiveItem? ToArchiveItem(string path)
    {
        try
        {
            var file = new FileInfo(path);
            var session = LoadSession(path);
            return new SessionArchiveItem
            {
                Modified = file.LastWriteTime,
                Created = file.CreationTime,
                SessionName = session.Name,
                Players = session.Players.Count,
                IsReadOnly = file.Attributes.HasFlag(FileAttributes.ReadOnly),
                Path = path
            };
        }
        catch
        {
            return null;
        }
    }

    private static void DeleteFileIfExists(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        ClearReadOnlyIfFileExists(path);
        File.Delete(path);
    }

    private static void ClearReadOnlyIfFileExists(string path)
    {
        if (File.Exists(path))
        {
            File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly);
        }
    }

    public static string SafeFilePart(string text)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var clean = new string(text.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(clean) ? "session" : clean;
    }

    private static string SafeChildPath(string root, string relativePath)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullTarget = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
        if (!fullTarget.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("An imported file path tried to escape the Poker Ledger data folder.");
        }
        return fullTarget;
    }

    private static IEnumerable<decimal> ParseMoneyList(string text)
    {
        var parts = text.Split(['/', ',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            if (decimal.TryParse(part.Replace("$", ""), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var value) && value > 0)
            {
                yield return value;
            }
        }
    }
}
