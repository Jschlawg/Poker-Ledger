# PokerHost Avalonia Port

This folder is the C# + Avalonia rewrite of PokerHost. It is intended to replace the current PowerShell/WinForms prototype with a cross-platform desktop app for Windows and macOS, with Linux builds possible later.

## Current Port Scope

Implemented in the Avalonia port:

- Cross-platform Avalonia desktop project structure.
- AppData-compatible storage under `PokerHost/data` and `PokerHost/receipts`.
- Session/player/transaction/cash-out data models matching the existing JSON shape.
- Session archive load/save/delete and ledger viewing.
- Setup presets with three managed slots.
- Saved player name suggestions and saved-name management.
- Player buy-ins/rebuys, undo last entry, and selected ledger entry deletion.
- Cash-out chip entry and running balance preview.
- Session conclusion and TXT receipt export.
- Active-session editing for session name, stakes/blinds, and chip denominations.
- JSON session import/export.
- Player CSV import/export.
- App-data export/import, reset defaults, archive purge, and receipt purge.

Intentionally not ported from deprecated PowerShell-era features:

- GUI scale slider.
- Old dark-mode checkbox.
- Removed quick-add buy-in/rebuy preset buttons.
- PowerShell-specific custom WinForms drawing hacks.

## Build

Install the .NET 8 SDK first.

```powershell
dotnet restore .\PokerHost.sln
dotnet run --project .\src\PokerHost.Avalonia\PokerHost.Avalonia.csproj
```

Publish examples:

```powershell
dotnet publish .\src\PokerHost.Avalonia\PokerHost.Avalonia.csproj -c Release -r win-x64 --self-contained true
dotnet publish .\src\PokerHost.Avalonia\PokerHost.Avalonia.csproj -c Release -r osx-arm64 --self-contained true
dotnet publish .\src\PokerHost.Avalonia\PokerHost.Avalonia.csproj -c Release -r osx-x64 --self-contained true
```

Or publish all primary desktop targets:

```powershell
.\src\PokerHost.Avalonia\Publish-PokerHost.ps1
```

Published output goes under:

```text
dist/
```

Avalonia package versions were chosen from NuGet current listings during migration:

- `Avalonia.Desktop` `12.0.5`
- `Avalonia.Themes.Fluent` `12.0.5`
