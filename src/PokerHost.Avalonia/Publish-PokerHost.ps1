param(
    [string[]]$Runtime = @("win-x64"),
    [switch]$NoSingleFile
)

$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "PokerHost.Avalonia.csproj"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$publishRoot = Join-Path $repoRoot "dist"
$singleFile = if ($NoSingleFile) { "false" } else { "true" }

foreach ($rid in $Runtime) {
    $output = Join-Path $publishRoot "PokerHost-Avalonia-$rid"
    & dotnet publish $project `
        -c Release `
        -r $rid `
        --self-contained true `
        -p:PublishSingleFile=$singleFile `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -o $output

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed for $rid"
    }
}

Write-Host "PokerHost publish complete:"
foreach ($rid in $Runtime) {
    Write-Host "  $(Join-Path $publishRoot "PokerHost-Avalonia-$rid")"
}
