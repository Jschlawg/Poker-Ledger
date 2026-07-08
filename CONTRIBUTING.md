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

5. Build a release package:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ".\src\PokerLedger.Avalonia\Publish-PokerLedger.ps1" -Version 1.0.0
```

Release downloads and checksums are written to:

```text
dist/releases/
```

## GitHub Releases

Create and push a version tag to publish release assets automatically:

```powershell
git tag v1.0.0
git push origin v1.0.0
```

The release workflow builds the Windows app and attaches the versioned exe, zip, and `SHA256SUMS.txt` checksum manifest to the GitHub release.

## Guidelines

- Keep generated files out of commits. The repo ignores `dist/`, `bin/`, `obj/`, app data, receipts, logs, and audit screenshots.
- Add or update tests for calculator, storage, receipt, import/export, and finalization behavior when touching those areas.
- Avoid machine-specific paths in documentation or scripts.

## Pull Request Checklist

- The app builds with `dotnet build .\PokerLedger.sln -c Release`.
- Tests pass with `dotnet test .\PokerLedger.sln`.
- No generated build output or local data is included.
- User-facing behavior is documented when it changes.
