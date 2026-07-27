param(
    [switch]$Apply
)

$ErrorActionPreference = "Stop"

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$contentRoot = Join-Path $repoRoot "Core/Content"
$legacySpritesRoot = Join-Path $contentRoot "Sprites"
$packRoot = Join-Path $contentRoot "Gameplay/stock.gg2"
$packSpritesRoot = Join-Path $packRoot "sprites"
$packAssetsRoot = Join-Path $packRoot "assets"

function Assert-PathWithinRepo {
    param([Parameter(Mandatory)][string]$Path)

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $prefix = $repoRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    if (-not $fullPath.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to operate outside the repository: $fullPath"
    }

    return $fullPath
}

function Convert-ToPathSegment {
    param([Parameter(Mandatory)][string]$Value)

    return ($Value.Trim().ToLowerInvariant() -replace '[^a-z0-9._-]+', '-') -replace '(^-+|-+$)', ''
}

function Get-ClassSegment {
    param([Parameter(Mandatory)][string]$Value)

    $knownClasses = @(
        "civilian", "demoman", "engineer", "heavy", "medic",
        "pyro", "scout", "sniper", "soldier", "spy", "quote", "querly"
    )
    $normalized = Convert-ToPathSegment $Value
    if ($knownClasses -contains $normalized) {
        return $(if ($normalized -in @("quote", "querly")) { "civilian" } else { $normalized })
    }

    return $normalized
}

function Get-SpriteDomain {
    param(
        [Parameter(Mandatory)][string]$SpriteId,
        [string]$LegacyRelativePath,
        [string]$FirstFramePath
    )

    $normalizedLegacyPath = $LegacyRelativePath.Replace('\', '/')
    $parts = @($normalizedLegacyPath.Split('/', [System.StringSplitOptions]::RemoveEmptyEntries))
    if ($parts.Count -gt 0) {
        switch ($parts[0]) {
            "Characters" {
                $className = if ($parts.Count -gt 1) { Get-ClassSegment $parts[1] } else { "shared" }
                return "characters/$className"
            }
            "Weapons" {
                $subdomain = if ($parts.Count -gt 1) { Convert-ToPathSegment ($parts[1] -replace '\.images$', '') } else { "shared" }
                return "weapons/$subdomain"
            }
            "Projectiles" { return "projectiles" }
            "HUDs" {
                $subdomain = if ($parts.Count -gt 1) { Convert-ToPathSegment ($parts[1] -replace '\.images$', '') } else { "shared" }
                return "hud/$subdomain"
            }
            "InGameElements" { return "world/in-game-elements" }
            "GameElements" { return "world/game-elements" }
            "MapElements" { return "world/map-elements" }
            "Obstacles" { return "world/obstacles" }
        }
    }

    if ($SpriteId -match '^(Civvie|Querly)') { return "characters/civilian" }
    if ($SpriteId -match '^Mvp') { return "hud/mvp" }
    if ($SpriteId -match '^Spectator') { return "hud/spectator" }
    if ($SpriteId -match '^weapon\.') { return "weapons/alternates" }
    if ($SpriteId -match '^(Nail|Nailgun)') { return "weapons/nailgun" }
    if ($FirstFramePath -match '^assets/civvie/') { return "characters/civilian" }
    if ($FirstFramePath -match '^assets/mvp/') { return "hud/mvp" }
    if ($FirstFramePath -match '^assets/spectator/') { return "hud/spectator" }

    return "shared"
}

function Convert-LegacySpriteMetadataToJson {
    param(
        [Parameter(Mandatory)][string]$SpriteId,
        [Parameter(Mandatory)][string]$MetadataPath,
        [Parameter(Mandatory)][string]$ImagesDirectory,
        [Parameter(Mandatory)][string]$AssetRelativeDirectory
    )

    [xml]$metadata = Get-Content -LiteralPath $MetadataPath -Raw
    $frames = @(Get-ChildItem -LiteralPath $ImagesDirectory -File -Filter "*.png" | Sort-Object `
        @{ Expression = { if ($_.BaseName -match '(\d+)$') { [int]$Matches[1] } else { [int]::MaxValue } } }, `
        @{ Expression = { $_.Name } })
    if ($frames.Count -eq 0) {
        throw "Legacy sprite '$SpriteId' has no PNG frames: $ImagesDirectory"
    }

    $definition = [ordered]@{
        id = $SpriteId
        framePaths = @($frames | ForEach-Object { "$AssetRelativeDirectory/$($_.Name)" })
        originX = [int]$metadata.sprite.origin.x
        originY = [int]$metadata.sprite.origin.y
        mask = [ordered]@{
            separate = [System.Convert]::ToBoolean([string]$metadata.sprite.mask.separate)
            shape = [string]$metadata.sprite.mask.shape
            boundsMode = [string]$metadata.sprite.mask.bounds.mode
            left = $null
            top = $null
            right = $null
            bottom = $null
        }
    }

    return ($definition | ConvertTo-Json -Depth 8)
}

function Test-GlobalSpriteSource {
    param([Parameter(Mandatory)][string]$LegacyRelativePath)

    $normalized = $LegacyRelativePath.Replace('\', '/')
    return $normalized.StartsWith("Collision Maps/", [System.StringComparison]::OrdinalIgnoreCase) -or
        $normalized.StartsWith("Updater/", [System.StringComparison]::OrdinalIgnoreCase)
}

if (-not (Test-Path -LiteralPath $packSpritesRoot -PathType Container)) {
    throw "Stock gameplay sprite directory was not found: $packSpritesRoot"
}

$implicitStockSpriteSources = @(
    [pscustomobject]@{ Id = "ChargeJumpS"; LegacyImages = "HUDs/WeaponHUDS/Spy/ChargeJumpS.images"; Domain = "hud/weaponhuds" },
    [pscustomobject]@{ Id = "GrenadeLauncherAmmoS"; LegacyImages = "HUDs/Ammo/GrenadeLauncherAmmoS.images"; Domain = "hud/ammo" },
    [pscustomobject]@{ Id = "GrenadeLauncherS"; LegacyImages = "Weapons/GrenadeLauncherS.images"; Domain = "weapons/grenade-launchers" },
    [pscustomobject]@{ Id = "GrenadeLauncherFS"; LegacyImages = "Weapons/Firing/GrenadeLauncherFS.images"; Domain = "weapons/firing" },
    [pscustomobject]@{ Id = "GrenadeLauncherFRS"; LegacyImages = "Weapons/Reloading/GrenadeLauncherFRS.images"; Domain = "weapons/reloading" }
)

$definitionFiles = @(Get-ChildItem -LiteralPath $packSpritesRoot -File -Recurse -Filter "*.json" | Sort-Object FullName)
$topLevelDefinitionCount = @(Get-ChildItem -LiteralPath $packSpritesRoot -File -Filter "*.json").Count
$externalReferenceCount = @($definitionFiles | Where-Object {
    (Get-Content -LiteralPath $_.FullName -Raw).IndexOf('"Content/', [System.StringComparison]::OrdinalIgnoreCase) -ge 0
}).Count
$implicitImportCount = @($implicitStockSpriteSources | Where-Object {
    Test-Path -LiteralPath (Join-Path $legacySpritesRoot $_.LegacyImages) -PathType Container
}).Count
if ($externalReferenceCount -eq 0 -and $topLevelDefinitionCount -eq 0 -and $implicitImportCount -eq 0) {
    Write-Host "Stock sprite sources are already pack-relative and organized."
    exit 0
}

$operations = [System.Collections.Generic.List[object]]::new()

foreach ($definitionFile in $definitionFiles) {
    $rawJson = Get-Content -LiteralPath $definitionFile.FullName -Raw
    $isTopLevelDefinition = $definitionFile.DirectoryName.Equals($packSpritesRoot, [System.StringComparison]::OrdinalIgnoreCase)
    $hasExternalFrames = $rawJson.IndexOf('"Content/', [System.StringComparison]::OrdinalIgnoreCase) -ge 0
    if (-not $isTopLevelDefinition -and -not $hasExternalFrames) {
        continue
    }

    $definition = $rawJson | ConvertFrom-Json
    $spriteId = [string]$definition.id
    if ([string]::IsNullOrWhiteSpace($spriteId)) {
        throw "Sprite definition has no id: $($definitionFile.FullName)"
    }

    $framePaths = @($definition.framePaths | ForEach-Object { [string]$_ })
    if ($framePaths.Count -eq 0) {
        throw "Sprite definition has no frame paths: $($definitionFile.FullName)"
    }

    $contentFramePaths = @($framePaths | Where-Object { $_.StartsWith("Content/", [System.StringComparison]::OrdinalIgnoreCase) })
    $legacyRelativePath = ""
    $legacyImagesDirectory = $null
    $legacyMetadataPath = $null
    $rewrittenJson = $rawJson
    $removeDefinition = $false

    if ($contentFramePaths.Count -gt 0) {
        if ($contentFramePaths.Count -ne $framePaths.Count) {
            throw "Sprite definition mixes pack-relative and Content-relative frames: $($definitionFile.FullName)"
        }

        $frameParents = @($contentFramePaths |
            ForEach-Object { [System.IO.Path]::GetDirectoryName($_).Replace('\', '/') } |
            Sort-Object -Unique)
        if ($frameParents.Count -ne 1 -or -not $frameParents[0].EndsWith(".images", [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Expected one legacy .images source directory for sprite '$spriteId'."
        }

        $contentPrefix = "Content/Sprites/"
        if (-not $frameParents[0].StartsWith($contentPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Stock sprite '$spriteId' references unsupported external content: $($frameParents[0])"
        }

        $legacyRelativePath = $frameParents[0].Substring($contentPrefix.Length)
        if (Test-GlobalSpriteSource $legacyRelativePath) {
            $removeDefinition = $true
        }
    }

    if ($removeDefinition) {
        $operations.Add([pscustomobject]@{
            Kind = "RemoveGlobalDefinition"
            SpriteId = $spriteId
            Source = $definitionFile.FullName
            Destination = $null
            RewrittenJson = $null
            LegacyImagesDirectory = $null
            LegacyMetadataPath = $null
        })
        continue
    }

    $domain = Get-SpriteDomain -SpriteId $spriteId -LegacyRelativePath $legacyRelativePath -FirstFramePath $framePaths[0]
    $definitionDestinationDirectory = Assert-PathWithinRepo (Join-Path $packSpritesRoot $domain)
    $definitionDestination = Assert-PathWithinRepo (Join-Path $definitionDestinationDirectory $definitionFile.Name)

    if ($contentFramePaths.Count -gt 0) {
        $legacyImagesDirectory = Assert-PathWithinRepo (Join-Path $legacySpritesRoot $legacyRelativePath)
        if (-not (Test-Path -LiteralPath $legacyImagesDirectory -PathType Container)) {
            throw "Legacy sprite frame directory was not found: $legacyImagesDirectory"
        }

        $legacyMetadataPath = Assert-PathWithinRepo (Join-Path (Split-Path $legacyImagesDirectory -Parent) "$spriteId.xml")
        if (-not (Test-Path -LiteralPath $legacyMetadataPath -PathType Leaf)) {
            throw "Legacy sprite metadata was not found: $legacyMetadataPath"
        }

        $assetDestinationDirectory = Assert-PathWithinRepo (Join-Path (Join-Path $packAssetsRoot $domain) "$spriteId.images")
        $sourcePrefix = "Content/Sprites/$legacyRelativePath"
        $destinationPrefix = $assetDestinationDirectory.Substring($packRoot.Length).TrimStart('\', '/').Replace('\', '/')
        $rewrittenJson = $rewrittenJson.Replace($sourcePrefix, $destinationPrefix)

        $operations.Add([pscustomobject]@{
            Kind = "MoveExternalDefinition"
            SpriteId = $spriteId
            Source = $definitionFile.FullName
            Destination = $definitionDestination
            RewrittenJson = $rewrittenJson
            LegacyImagesDirectory = $legacyImagesDirectory
            AssetDestinationDirectory = $assetDestinationDirectory
            LegacyMetadataPath = $legacyMetadataPath
        })
    }
    else {
        $operations.Add([pscustomobject]@{
            Kind = "MovePackDefinition"
            SpriteId = $spriteId
            Source = $definitionFile.FullName
            Destination = $definitionDestination
            RewrittenJson = $rewrittenJson
            LegacyImagesDirectory = $null
            AssetDestinationDirectory = $null
            LegacyMetadataPath = $null
        })
    }
}

foreach ($implicitSource in $implicitStockSpriteSources) {
    $legacyImagesDirectory = Assert-PathWithinRepo (Join-Path $legacySpritesRoot $implicitSource.LegacyImages)
    if (-not (Test-Path -LiteralPath $legacyImagesDirectory -PathType Container)) {
        continue
    }

    $legacyMetadataPath = Assert-PathWithinRepo (Join-Path (Split-Path $legacyImagesDirectory -Parent) "$($implicitSource.Id).xml")
    if (-not (Test-Path -LiteralPath $legacyMetadataPath -PathType Leaf)) {
        throw "Legacy sprite metadata was not found: $legacyMetadataPath"
    }

    $definitionDestination = Assert-PathWithinRepo (Join-Path (Join-Path $packSpritesRoot $implicitSource.Domain) "$($implicitSource.Id).json")
    if (Test-Path -LiteralPath $definitionDestination) {
        throw "Stock sprite definition destination already exists: $definitionDestination"
    }

    $assetDestinationDirectory = Assert-PathWithinRepo (Join-Path (Join-Path $packAssetsRoot $implicitSource.Domain) "$($implicitSource.Id).images")
    $assetRelativeDirectory = $assetDestinationDirectory.Substring($packRoot.Length).TrimStart('\', '/').Replace('\', '/')
    $rewrittenJson = Convert-LegacySpriteMetadataToJson `
        -SpriteId $implicitSource.Id `
        -MetadataPath $legacyMetadataPath `
        -ImagesDirectory $legacyImagesDirectory `
        -AssetRelativeDirectory $assetRelativeDirectory

    $operations.Add([pscustomobject]@{
        Kind = "ImportImplicitStockDefinition"
        SpriteId = $implicitSource.Id
        Source = $null
        Destination = $definitionDestination
        RewrittenJson = $rewrittenJson
        LegacyImagesDirectory = $legacyImagesDirectory
        AssetDestinationDirectory = $assetDestinationDirectory
        LegacyMetadataPath = $legacyMetadataPath
    })
}

$summary = $operations | Group-Object Kind | Sort-Object Name | ForEach-Object {
    [pscustomobject]@{ Kind = $_.Name; Count = $_.Count }
}
$summary | Format-Table -AutoSize

if (-not $Apply) {
    Write-Host "Dry run only. Re-run with -Apply to migrate the stock sprite sources."
    exit 0
}

foreach ($operation in $operations) {
    if ($operation.Kind -eq "RemoveGlobalDefinition") {
        Remove-Item -LiteralPath $operation.Source
        continue
    }

    if ($operation.Kind -eq "ImportImplicitStockDefinition") {
        $definitionDestinationDirectory = Split-Path $operation.Destination -Parent
        $assetDestinationParent = Split-Path $operation.AssetDestinationDirectory -Parent
        New-Item -ItemType Directory -Path $definitionDestinationDirectory -Force | Out-Null
        New-Item -ItemType Directory -Path $assetDestinationParent -Force | Out-Null
        if (Test-Path -LiteralPath $operation.AssetDestinationDirectory) {
            throw "Sprite asset destination already exists: $($operation.AssetDestinationDirectory)"
        }

        Move-Item -LiteralPath $operation.LegacyImagesDirectory -Destination $operation.AssetDestinationDirectory
        Remove-Item -LiteralPath $operation.LegacyMetadataPath
        [System.IO.File]::WriteAllText($operation.Destination, $operation.RewrittenJson)
        continue
    }

    $definitionDestinationDirectory = Split-Path $operation.Destination -Parent
    New-Item -ItemType Directory -Path $definitionDestinationDirectory -Force | Out-Null

    if ($operation.Kind -eq "MoveExternalDefinition") {
        $assetDestinationParent = Split-Path $operation.AssetDestinationDirectory -Parent
        New-Item -ItemType Directory -Path $assetDestinationParent -Force | Out-Null
        if (Test-Path -LiteralPath $operation.AssetDestinationDirectory) {
            throw "Sprite asset destination already exists: $($operation.AssetDestinationDirectory)"
        }

        Move-Item -LiteralPath $operation.LegacyImagesDirectory -Destination $operation.AssetDestinationDirectory
        Remove-Item -LiteralPath $operation.LegacyMetadataPath
    }

    if (-not [string]::Equals($operation.Source, $operation.Destination, [System.StringComparison]::OrdinalIgnoreCase)) {
        Move-Item -LiteralPath $operation.Source -Destination $operation.Destination
    }

    if ($operation.Kind -eq "MoveExternalDefinition") {
        Set-Content -LiteralPath $operation.Destination -Value $operation.RewrittenJson -NoNewline
    }
}

Write-Host "Stock sprite source migration completed."
