# PokerHost

PokerHost is a desktop app for tracking a private poker home-game session: setup presets, players, buy-ins, rebuys, cash-outs, balance checks, session archives, and read-only text receipts.

The app is currently built with C#/.NET 8 and Avalonia. It is developed primarily on Windows, with the project structure kept portable for macOS and Linux publishing targets.

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
- Windows 11 is the current primary target.
- macOS and Linux builds are possible through Avalonia runtime identifiers, but should be tested on those systems before release.

## Run From Source

```powershell
dotnet restore .\PokerHost.sln
dotnet run --project .\src\PokerHost.Avalonia\PokerHost.Avalonia.csproj
```

## Build

```powershell
dotnet build .\PokerHost.sln -c Release
```

## Test

```powershell
dotnet test .\PokerHost.sln
```

## Publish

For a Windows x64 executable:

```powershell
.\src\PokerHost.Avalonia\Publish-PokerHost.ps1 -Runtime win-x64
```

Published builds are written to:

```text
dist/
```

The publish script also accepts other runtime identifiers, for example `osx-x64`, `osx-arm64`, and `linux-x64`.

## Data Storage

PokerHost stores app data outside the repository:

- Windows: `%APPDATA%\PokerHost`
- Test override: set `POKERHOST_APPDATA_ROOT` to a temporary folder.

Do not commit generated `data/`, `receipts/`, `logs/`, `dist/`, `bin/`, or `obj/` folders.

## Legal Note

PokerHost is recordkeeping software. It does not determine whether a game, payout, rake, or local setup is legal. Users are responsible for complying with their local rules and laws.
