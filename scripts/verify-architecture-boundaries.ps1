[CmdletBinding()]
param(
    [string]$Root = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($Root)) {
    $Root = Join-Path $PSScriptRoot ".."
}

$rootPath = [System.IO.Path]::GetFullPath($Root)
if (-not (Test-Path -LiteralPath $rootPath -PathType Container)) {
    throw "Repository root does not exist: $rootPath"
}

$failures = [System.Collections.Generic.List[string]]::new()

function Add-Failure([string]$message) {
    $failures.Add($message)
}

function Get-CSharpFiles([string]$relativeDir) {
    $dir = Join-Path $rootPath $relativeDir
    if (-not (Test-Path -LiteralPath $dir -PathType Container)) { return @() }
    return Get-ChildItem -LiteralPath $dir -Recurse -Filter "*.cs" -File |
        Where-Object { $_.FullName -notmatch '[\\/]bin[\\/]' -and $_.FullName -notmatch '[\\/]obj[\\/]' }
}

# Rule 1: Core/Simulation/Systems must not reference SimulationWorld.
# The systems are owned by SimulationWorld; they must not depend back on it.
foreach ($file in Get-CSharpFiles "Core/Simulation/Systems") {
    $content = Get-Content -LiteralPath $file.FullName -Raw
    if ($content -match '\bSimulationWorld\b') {
        Add-Failure "Core/Simulation/Systems references SimulationWorld: $($file.FullName.Substring($rootPath.Length + 1))"
    }
}

# Rule 2: No new SimulationWorld fields duplicating system-owned projectile collections.
# ProjectileSystem owns projectile storage; SimulationWorld must not reintroduce
# its own _shots/_mines/_grenades/etc. lists.
$simWorldCore = Join-Path $rootPath "Core/Simulation/Core/SimulationWorld.cs"
if (Test-Path -LiteralPath $simWorldCore) {
    $content = Get-Content -LiteralPath $simWorldCore -Raw
    $legacyProjectileFields = @(
        '_shots', '_bubbles', '_blades', '_needles', '_revolverShots',
        '_stabAnimations', '_stabMasks', '_flames', '_flares', '_rockets',
        '_mines', '_grenades', '_pendingNewRocketIds'
    )
    foreach ($field in $legacyProjectileFields) {
        # Allow the field if it is assigned from the ProjectileSystem (alias during migration),
        # but flag standalone `new()` initializations which indicate duplicated ownership.
        $declPattern = "private\s+readonly\s+List<[^>]+>\s+" + [regex]::Escape($field) + "\s*=\s*new\(\)"
        if ($content -match $declPattern) {
            Add-Failure "SimulationWorld reintroduces system-owned projectile collection: $field"
        }
    }
}

# Rule 3: New Core/BotAI references from navigation code need explicit allowlist entry.
# The navigation quarantine keeps legacy BotAI isolated; new dependencies must be deliberate.
$navDirs = @("Core/BotBrain/Navigation")
$legacyAllowlist = @(
    # Existing legitimate references (as of 2026-09-28).
    "Core/BotBrain/Navigation/Og2NavigationGraphBuilder.cs",
    "Core/BotBrain/Navigation/Og2AlphaNavigationDiagnostics.cs"
)
foreach ($navDir in $navDirs) {
    foreach ($file in Get-CSharpFiles $navDir) {
        $relative = $file.FullName.Substring($rootPath.Length + 1).Replace('\', '/')
        $content = Get-Content -LiteralPath $file.FullName -Raw
        if ($content -match 'OpenGarrison\.BotAI\b' -and $legacyAllowlist -notcontains $relative) {
            Add-Failure "Navigation file references legacy BotAI without allowlist entry: $relative"
        }
    }
}

# Rule 4: New manager/controller files must not add direct Game1/SimulationWorld
# dependencies outside the context bridge (Client/Managers/Game1.ManagerContexts.cs
# and its per-domain splits).
$managerFiles = Get-CSharpFiles "Client/Managers"
$contextBridgeFiles = @(
    "Client/Managers/Game1.ManagerContexts.cs",
    "Client/Managers/Game1.AudioContext.cs",
    "Client/Managers/Game1.GameplayContext.cs",
    "Client/Managers/Game1.MenuContext.cs",
    "Client/Managers/Game1.SessionContext.cs",
    "Client/Managers/Game1.HostingContext.cs",
    "Client/Managers/Game1.PluginContext.cs",
    "Client/Managers/Game1.HudContext.cs",
    "Client/Managers/Game1.RenderContext.cs"
)
foreach ($file in $managerFiles) {
    $relative = $file.FullName.Substring($rootPath.Length + 1).Replace('\', '/')
    if ($contextBridgeFiles -contains $relative) { continue }
    $content = Get-Content -LiteralPath $file.FullName -Raw
    # Flag direct Game1 type references (not IGameplayContext etc. interfaces).
    if ($content -match '(?<![\w])Game1(?![\w])' -and $content -match ':\s*Game1\b|\(Game1\b|Game1\s+\w+\s*[=;,)]') {
        Add-Failure "Manager file takes direct Game1 dependency outside context bridge: $relative"
    }
}

if ($failures.Count -gt 0) {
    Write-Host "Architecture boundary violations found:"
    foreach ($failure in $failures) {
        Write-Host "  - $failure"
    }
    exit 1
}

Write-Host "Architecture boundaries verified: no violations."
exit 0
