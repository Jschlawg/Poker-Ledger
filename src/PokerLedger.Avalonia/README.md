# Poker Ledger Avalonia Port

This folder is the C# + Avalonia rewrite of Poker Ledger. Development and support are focused on Windows only.

## Current Port Scope

Implemented in the Avalonia port:

- Avalonia desktop project structure for the Windows-focused app.
- AppData-compatible storage under `PokerLedger/data` and `PokerLedger/receipts`.
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
dotnet restore .\PokerLedger.sln
dotnet run --project .\src\PokerLedger.Avalonia\PokerLedger.Avalonia.csproj
```

Publish:

```powershell
.\src\PokerLedger.Avalonia\Publish-PokerLedger.ps1
```

Published output goes under:

```text
dist/
```

Avalonia package versions were chosen from NuGet current listings during migration:

- `Avalonia.Desktop` `12.0.5`
- `Avalonia.Themes.Fluent` `12.0.5`
