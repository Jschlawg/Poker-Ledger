param(
    [string]$Version = "1.0.0",
    [string[]]$Runtime = @("win-x64"),
    [switch]$NoSingleFile,
    [switch]$NoReleasePackage
)

$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "PokerLedger.Avalonia.csproj"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$publishRoot = Join-Path $repoRoot "dist"
$releaseRoot = Join-Path $publishRoot "releases"
$singleFile = if ($NoSingleFile) { "false" } else { "true" }
$releaseVersion = $Version.Trim().TrimStart("v")

if ($releaseVersion -notmatch "^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$") {
    throw "Version must look like 1.0.0 or 1.0.0-beta.1"
}

$numericVersion = ($releaseVersion -split "-", 2)[0]
$fileVersion = "$numericVersion.0"
$checksumPath = Join-Path $releaseRoot "SHA256SUMS.txt"

if (-not $NoReleasePackage -and (Test-Path -LiteralPath $releaseRoot)) {
    Remove-Item -LiteralPath $releaseRoot -Recurse -Force
}

foreach ($rid in $Runtime) {
    $output = Join-Path $publishRoot "PokerLedger-Avalonia-$rid"

    if (Test-Path -LiteralPath $output) {
        Remove-Item -LiteralPath $output -Recurse -Force
    }

    & dotnet publish $project `
        -c Release `
        -r $rid `
        --self-contained true `
        -p:PublishSingleFile=$singleFile `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:DebugType=none `
        -p:DebugSymbols=false `
        -p:Version=$releaseVersion `
        -p:AssemblyVersion=$fileVersion `
        -p:FileVersion=$fileVersion `
        -p:InformationalVersion=$releaseVersion `
        -o $output

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed for $rid"
    }

    if (-not $NoReleasePackage) {
        New-Item -ItemType Directory -Force -Path $releaseRoot | Out-Null

        $exePath = Join-Path $output "PokerLedger.exe"
        $releaseExe = Join-Path $releaseRoot "PokerLedger-$releaseVersion-$rid.exe"
        $releaseZip = Join-Path $releaseRoot "PokerLedger-$releaseVersion-$rid.zip"

        Copy-Item -LiteralPath $exePath -Destination $releaseExe -Force
        Compress-Archive -LiteralPath $releaseExe -DestinationPath $releaseZip -Force

        foreach ($asset in @($releaseExe, $releaseZip)) {
            $hash = Get-FileHash -LiteralPath $asset -Algorithm SHA256
            "{0}  {1}" -f $hash.Hash, (Split-Path -Leaf $asset) | Add-Content -LiteralPath $checksumPath
        }
    }
}

Write-Host "Poker Ledger publish complete:"
foreach ($rid in $Runtime) {
    Write-Host "  $(Join-Path $publishRoot "PokerLedger-Avalonia-$rid")"
}

if (-not $NoReleasePackage) {
    Write-Host "Release downloads:"
    foreach ($rid in $Runtime) {
        Write-Host "  $(Join-Path $releaseRoot "PokerLedger-$releaseVersion-$rid.exe")"
        Write-Host "  $(Join-Path $releaseRoot "PokerLedger-$releaseVersion-$rid.zip")"
    }
    Write-Host "  $checksumPath"
}
