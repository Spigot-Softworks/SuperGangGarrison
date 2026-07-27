[CmdletBinding()]
param(
    [switch]$Apply
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$packRoot = Join-Path $repoRoot "Core/Content/Gameplay/stock.gg2"
$itemsRoot = Join-Path $packRoot "items"
$spritesRoot = Join-Path $packRoot "sprites"
$assetsRoot = Join-Path $packRoot "assets"

$assetDirectoryMoves = [ordered]@{
    "blackbox" = "weapons/variants/blackbox"
    "brassbeast" = "weapons/variants/brassbeast"
    "civvie" = "characters/civilian/custom"
    "diamondback" = "weapons/variants/diamondback"
    "directhit" = "weapons/variants/directhit"
    "mvp" = "hud/mvp"
    "scout-nailgun" = "weapons/nailgun"
    "soldier-shotgun" = "weapons/variants/soldier-shotgun"
    "spectator" = "hud/spectator"
    "tomislav" = "weapons/variants/tomislav"
}

$legacyDomainMoves = [ordered]@{}
foreach ($category in @("hud", "weapons")) {
    $categoryRoot = Join-Path $spritesRoot $category
    if (-not (Test-Path -LiteralPath $categoryRoot -PathType Container)) {
        continue
    }

    foreach ($directory in Get-ChildItem -LiteralPath $categoryRoot -Directory -Filter "*.images") {
        $cleanName = $directory.Name.Substring(0, $directory.Name.Length - ".images".Length)
        $legacyDomainMoves["$category/$($directory.Name)"] = "$category/$cleanName"
    }
}

function Get-ItemCategory {
    param([Parameter(Mandatory = $true)][string]$FileName)

    if ($FileName.StartsWith("ability.", [System.StringComparison]::OrdinalIgnoreCase)) {
        return "abilities"
    }

    if ($FileName.StartsWith("weapon.", [System.StringComparison]::OrdinalIgnoreCase)) {
        return "weapons"
    }

    if ($FileName.StartsWith("experimental.", [System.StringComparison]::OrdinalIgnoreCase)) {
        return "experimental"
    }

    throw "Stock item definition does not have an ownership category: $FileName"
}

$itemMoves = [System.Collections.Generic.List[object]]::new()
foreach ($file in Get-ChildItem -LiteralPath $itemsRoot -File -Recurse -Filter "*.json") {
    $category = Get-ItemCategory -FileName $file.Name
    $destination = Join-Path (Join-Path $itemsRoot $category) $file.Name
    if (-not $file.FullName.Equals($destination, [System.StringComparison]::OrdinalIgnoreCase)) {
        $itemMoves.Add([pscustomobject]@{ Source = $file.FullName; Destination = $destination })
    }
}

$spriteMoves = [System.Collections.Generic.List[object]]::new()
$spriteRewrites = [System.Collections.Generic.List[object]]::new()
foreach ($file in Get-ChildItem -LiteralPath $spritesRoot -File -Recurse -Filter "*.json") {
    $rawJson = [System.IO.File]::ReadAllText($file.FullName)
    $definition = $rawJson | ConvertFrom-Json
    $rewrittenJson = $rawJson
    foreach ($entry in $assetDirectoryMoves.GetEnumerator()) {
        $rewrittenJson = $rewrittenJson.Replace("assets/$($entry.Key)/", "assets/$($entry.Value)/")
    }
    foreach ($entry in $legacyDomainMoves.GetEnumerator()) {
        $rewrittenJson = $rewrittenJson.Replace("assets/$($entry.Key)/", "assets/$($entry.Value)/")
    }

    if ($rewrittenJson -ne $rawJson) {
        $spriteRewrites.Add([pscustomobject]@{ Path = $file.FullName; Contents = $rewrittenJson })
    }

    $spriteId = [string]$definition.id
    if ($spriteId.StartsWith("stock.gg2.weapon.", [System.StringComparison]::Ordinal)) {
        $destination = Join-Path (Join-Path $spritesRoot "weapons/variants") $file.Name
        if (-not $file.FullName.Equals($destination, [System.StringComparison]::OrdinalIgnoreCase)) {
            $spriteMoves.Add([pscustomobject]@{ Source = $file.FullName; Destination = $destination })
        }
    }
}

$assetMoves = [System.Collections.Generic.List[object]]::new()
foreach ($entry in $assetDirectoryMoves.GetEnumerator()) {
    $source = Join-Path $assetsRoot $entry.Key
    $destination = Join-Path $assetsRoot $entry.Value
    if (Test-Path -LiteralPath $source -PathType Container) {
        if (Test-Path -LiteralPath $destination) {
            throw "Stock asset destination already exists: $destination"
        }

        $assetMoves.Add([pscustomobject]@{ Source = $source; Destination = $destination })
    }
}

$domainDirectoryMoves = [System.Collections.Generic.List[object]]::new()
foreach ($entry in $legacyDomainMoves.GetEnumerator()) {
    foreach ($root in @($spritesRoot, $assetsRoot)) {
        $source = Join-Path $root $entry.Key
        $destination = Join-Path $root $entry.Value
        if (-not (Test-Path -LiteralPath $source -PathType Container)) {
            continue
        }
        if (Test-Path -LiteralPath $destination) {
            throw "Stock domain destination already exists: $destination"
        }

        $domainDirectoryMoves.Add([pscustomobject]@{ Source = $source; Destination = $destination })
    }
}

Write-Host "Stock pack layout operations: $($itemMoves.Count) item move(s), $($spriteMoves.Count) sprite definition move(s), $($assetMoves.Count) asset directory move(s), $($domainDirectoryMoves.Count) domain directory move(s), $($spriteRewrites.Count) definition rewrite(s)."
if (-not $Apply) {
    Write-Host "Dry run only. Re-run with -Apply to organize the stock pack."
    exit 0
}

foreach ($rewrite in $spriteRewrites) {
    [System.IO.File]::WriteAllText($rewrite.Path, $rewrite.Contents)
}

foreach ($operation in @($itemMoves) + @($spriteMoves) + @($assetMoves) + @($domainDirectoryMoves)) {
    $destinationDirectory = Split-Path -Parent $operation.Destination
    New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null
    Move-Item -LiteralPath $operation.Source -Destination $operation.Destination
}

Write-Host "Stock gameplay pack layout organized."
