[CmdletBinding()]
param(
    [switch]$Apply
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$packRoot = Join-Path $repoRoot "Core/Content/Gameplay/stock.gg2"
$spritesRoot = Join-Path $packRoot "sprites"
$assetsRoot = Join-Path $packRoot "assets"
$unreferencedRoot = Join-Path $repoRoot "SourceAssets/Sprites/StockGameplay/Unreferenced"

function Assert-PathWithinDirectory {
    param(
        [Parameter(Mandatory = $true)][string]$Directory,
        [Parameter(Mandatory = $true)][string]$Candidate
    )

    $fullDirectory = [System.IO.Path]::GetFullPath($Directory).TrimEnd([char[]]@('\', '/'))
    $fullCandidate = [System.IO.Path]::GetFullPath($Candidate)
    $prefix = $fullDirectory + [System.IO.Path]::DirectorySeparatorChar
    if (-not $fullCandidate.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Path '$fullCandidate' is outside '$fullDirectory'."
    }

    return $fullCandidate
}

function Get-RelativePath {
    param(
        [Parameter(Mandatory = $true)][string]$Directory,
        [Parameter(Mandatory = $true)][string]$Candidate
    )

    $directoryWithSeparator = [System.IO.Path]::GetFullPath($Directory).TrimEnd([char[]]@('\', '/')) + [System.IO.Path]::DirectorySeparatorChar
    $directoryUri = [System.Uri]::new($directoryWithSeparator)
    $candidateUri = [System.Uri]::new([System.IO.Path]::GetFullPath($Candidate))
    return [System.Uri]::UnescapeDataString($directoryUri.MakeRelativeUri($candidateUri).ToString()).Replace('/', [System.IO.Path]::DirectorySeparatorChar)
}

$referencedFrames = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
foreach ($definitionFile in Get-ChildItem -LiteralPath $spritesRoot -File -Recurse -Filter "*.json") {
    $definition = Get-Content -LiteralPath $definitionFile.FullName -Raw | ConvertFrom-Json
    foreach ($framePath in @($definition.framePaths)) {
        if ($framePath -isnot [string] -or [string]::IsNullOrWhiteSpace([string]$framePath)) {
            continue
        }

        $resolvedPath = Assert-PathWithinDirectory `
            -Directory $packRoot `
            -Candidate (Join-Path $packRoot ([string]$framePath).Replace('/', [System.IO.Path]::DirectorySeparatorChar))
        [void]$referencedFrames.Add($resolvedPath)
    }
}

$moves = [System.Collections.Generic.List[object]]::new()
foreach ($assetFile in Get-ChildItem -LiteralPath $assetsRoot -File -Recurse -Filter "*.png") {
    if ($referencedFrames.Contains($assetFile.FullName)) {
        continue
    }

    $relativePath = Get-RelativePath -Directory $assetsRoot -Candidate $assetFile.FullName
    $destination = Assert-PathWithinDirectory -Directory $unreferencedRoot -Candidate (Join-Path $unreferencedRoot $relativePath)
    if (Test-Path -LiteralPath $destination) {
        throw "Unreferenced source destination already exists: $destination"
    }

    $moves.Add([pscustomobject]@{
        Source = $assetFile.FullName
        Destination = $destination
        RelativePath = $relativePath.Replace('\', '/')
    })
}

Write-Host "Unreferenced stock sprite sources: $($moves.Count) file(s)."
foreach ($move in $moves) {
    Write-Host "  $($move.RelativePath)"
}

if (-not $Apply) {
    Write-Host "Dry run only. Re-run with -Apply to move these files under SourceAssets."
    exit 0
}

foreach ($move in $moves) {
    New-Item -ItemType Directory -Path (Split-Path -Parent $move.Destination) -Force | Out-Null
    Move-Item -LiteralPath $move.Source -Destination $move.Destination
}

foreach ($directory in Get-ChildItem -LiteralPath $assetsRoot -Directory -Recurse | Sort-Object { $_.FullName.Length } -Descending) {
    if (@(Get-ChildItem -LiteralPath $directory.FullName -Force).Count -eq 0) {
        Remove-Item -LiteralPath $directory.FullName
    }
}

Write-Host "Unreferenced stock sprite sources moved to '$unreferencedRoot'."
