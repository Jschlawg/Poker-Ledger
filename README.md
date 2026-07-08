# Poker Ledger

Poker Ledger is a desktop app for tracking a private poker home-game session: setup presets, players, buy-ins, rebuys, cash-outs, balance checks, session archives, and read-only text receipts.

The app is currently built with C#/.NET 8 and Avalonia. Development and support are focused on Windows only.

## Features

- Configure session name, blinds/ante, and chip denominations.
- Save and apply up to three setup presets.
- Track players, buy-ins, rebuys, notes, cash-outs, and net results.
- Enter chip counts by denomination with live cash-out and balance previews.
- Export finalized session summaries and per-player receipts as read-only text files.
- Import/export session JSON and app-data backups.
- Browse saved sessions and view an in-app ledger.
- Choose from contrast modes inspired by poker chip color palettes.

## Requirements

- .NET 8 SDK for development.
- Windows 11 is the supported target.

## Run From Source

```powershell
dotnet restore .\PokerLedger.sln
dotnet run --project .\src\PokerLedger.Avalonia\PokerLedger.Avalonia.csproj
```

## Build

```powershell
dotnet build .\PokerLedger.sln -c Release
```

## Test

```powershell
dotnet test .\PokerLedger.sln
```

## Publish

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ".\src\PokerLedger.Avalonia\Publish-PokerLedger.ps1" -Version 1.0.0
```

Published builds are written to:

```text
dist/
```

The default publish target is `win-x64`. Release downloads are written to:

```text
dist/releases/PokerLedger-1.0.0-win-x64.exe
dist/releases/PokerLedger-1.0.0-win-x64.zip
```

## GitHub Releases

For public downloads, use GitHub Releases rather than GitHub Packages. Create and push a version tag to publish release assets automatically:

```powershell
git tag v1.0.0
git push origin v1.0.0
```

The release workflow builds the Windows app and attaches the versioned exe and zip to the GitHub release.

## Data Storage

Poker Ledger stores app data outside the repository:

- Windows: `%APPDATA%\PokerLedger`
- Test override: set `POKERLEDGER_APPDATA_ROOT` to a temporary folder.

Do not commit generated `data/`, `receipts/`, `logs/`, `dist/`, `bin/`, or `obj/` folders.

## Legal Note

Poker Ledger is recordkeeping software. It does not determine whether a game, payout, rake, or local setup is legal. Users are responsible for complying with their local rules and laws.
