param(
    [string]$Commit = "14b0d1ff",
    [string]$OutputDirectory = "extracted"
)

$ErrorActionPreference = "Stop"

$repoRoot = git rev-parse --show-toplevel
$targetRoot = if ([System.IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory
} else {
    Join-Path (Get-Location) $OutputDirectory
}

$files = @(
    "Client/Game/Developer/Game1.BotNavEditor.cs",
    "Client/Game/Gameplay/Practice/Game1.PracticeNavigation.cs",
    "BotAI/BotNavigationAsset.cs",
    "BotAI/BotNavigationAssetBuilder.cs",
    "BotAI/BotNavigationAssetStore.cs",
    "BotAI/BotNavigationAssetValidator.cs",
    "BotAI/BotNavigationClasses.cs",
    "BotAI/BotNavigationDebugPlanner.cs",
    "BotAI/BotNavigationHintAsset.cs",
    "BotAI/BotNavigationHintStore.cs",
    "BotAI/BotNavigationLevelFingerprint.cs",
    "BotAI/BotNavigationModernGraphEditor.cs",
    "BotAI/BotNavigationModernGraphRepairer.cs",
    "BotAI/BotNavigationModernPointGraphBuilder.cs",
    "BotAI/BotNavigationMovementValidator.cs",
    "BotAI/BotNavigationProfile.cs",
    "BotAI/BotNavigationRuntimeGraph.cs",
    "BotAI/BotNavigationScoreRouteAsset.cs",
    "BotAI/BotNavigationScoreRouteStore.cs",
    "BotAI/ClientBotNavPoints.cs",
    "BotAI/ModernObstacleGeometry.cs",
    "BotAI/OpenGarrison.BotAI.csproj"
)

New-Item -ItemType Directory -Force -Path $targetRoot | Out-Null

foreach ($file in $files) {
    $destination = Join-Path $targetRoot $file
    $destinationDirectory = Split-Path -Parent $destination
    New-Item -ItemType Directory -Force -Path $destinationDirectory | Out-Null

    $content = git -C $repoRoot show "${Commit}:${file}"
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to extract $file from $Commit"
    }

    [System.IO.File]::WriteAllText($destination, ($content -join [Environment]::NewLine) + [Environment]::NewLine)
}

$manifestPath = Join-Path $targetRoot "LEGACY_NAV_AUTHORING_MANIFEST.txt"
$manifest = @(
    "Legacy nav authoring extraction",
    "Commit: $Commit",
    "Generated: $(Get-Date -Format o)",
    "",
    "Files:"
) + ($files | ForEach-Object { " - $_" })

[System.IO.File]::WriteAllText($manifestPath, ($manifest -join [Environment]::NewLine) + [Environment]::NewLine)
Write-Host "Extracted $($files.Count) files to $targetRoot"
