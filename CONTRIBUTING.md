# Contributing

Thanks for helping improve PokerHost.

## Development Setup

1. Install the .NET 8 SDK.
2. Restore dependencies:

```powershell
dotnet restore .\PokerHost.sln
```

3. Run the app:

```powershell
dotnet run --project .\src\PokerHost.Avalonia\PokerHost.Avalonia.csproj
```

4. Run tests:

```powershell
dotnet test .\PokerHost.sln
```

## Guidelines

- Keep generated files out of commits. The repo ignores `dist/`, `bin/`, `obj/`, app data, receipts, logs, and audit screenshots.
- Prefer small, focused changes.
- Add or update tests for calculator, storage, receipt, import/export, and finalization behavior when touching those areas.
- Keep UI changes consistent with the existing desktop app flow.
- Avoid machine-specific paths in documentation or scripts.

## Pull Request Checklist

- The app builds with `dotnet build .\PokerHost.sln -c Release`.
- Tests pass with `dotnet test .\PokerHost.sln`.
- No generated build output or local data is included.
- User-facing behavior is documented when it changes.
