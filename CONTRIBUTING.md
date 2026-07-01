# Contributing

Thanks for helping improve Poker Ledger.

## Development Setup

1. Install the .NET 8 SDK.
2. Restore dependencies:

```powershell
dotnet restore .\PokerLedger.sln
```

3. Run the app:

```powershell
dotnet run --project .\src\PokerLedger.Avalonia\PokerLedger.Avalonia.csproj
```

4. Run tests:

```powershell
dotnet test .\PokerLedger.sln
```

## Guidelines

- Keep generated files out of commits. The repo ignores `dist/`, `bin/`, `obj/`, app data, receipts, logs, and audit screenshots.
- Prefer small, focused changes.
- Add or update tests for calculator, storage, receipt, import/export, and finalization behavior when touching those areas.
- Keep UI changes consistent with the existing desktop app flow.
- Avoid machine-specific paths in documentation or scripts.

## Pull Request Checklist

- The app builds with `dotnet build .\PokerLedger.sln -c Release`.
- Tests pass with `dotnet test .\PokerLedger.sln`.
- No generated build output or local data is included.
- User-facing behavior is documented when it changes.
