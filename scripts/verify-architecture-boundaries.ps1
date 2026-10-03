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

# Removes // and /* */ comments (including /// doc comments) so rules only see code.
function Remove-CSharpComments([string]$text) {
    return [regex]::Replace($text, '(?s)/\*.*?\*/|//[^\r\n]*', '')
}

# Rule 1: Core/Simulation/Systems must not reference SimulationWorld.
# The systems are owned by SimulationWorld; they must not depend back on it.
# Comments may mention it; only code references count.
foreach ($file in Get-CSharpFiles "Core/Simulation/Systems") {
    $content = Remove-CSharpComments (Get-Content -LiteralPath $file.FullName -Raw)
    if ($content -match '\bSimulationWorld\b') {
        Add-Failure "Core/Simulation/Systems references SimulationWorld: $($file.FullName.Substring($rootPath.Length + 1))"
    }
}

# The world's main partial (construction, stores, system properties). Located by name so
# the rules do not depend on which folder holds the world partials.
$simWorldCore = Get-CSharpFiles "Core/Simulation" |
    Where-Object { $_.Name -eq "SimulationWorld.cs" } |
    Select-Object -First 1 -ExpandProperty FullName
if (-not $simWorldCore) {
    Add-Failure "Core/Simulation must contain SimulationWorld.cs"
}

# Rule 2: No new SimulationWorld fields duplicating system-owned projectile collections.
# ProjectileSystem owns projectile storage; SimulationWorld must not reintroduce
# its own _shots/_mines/_grenades/etc. lists.
if ($simWorldCore) {
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
    $content = Remove-CSharpComments (Get-Content -LiteralPath $file.FullName -Raw)
    # Flag Game1 used as an instance type (not IGameplayContext etc. interfaces):
    #   - a parameter, field, or local declared as `Game1 name`
    #   - a base type (`class X : Game1`) followed by `{`, `,`, or `where`
    # Static access such as `Game1.GetLabel(...)` and `using static ...Game1;` is allowed.
    $game1Declaration = '(?<![\w.])Game1\s+[A-Za-z_]\w*\s*[=;,)]'
    $game1BaseType = ':\s*Game1\s*(?:[,{]|\bwhere\b)'
    if ($content -match $game1Declaration -or $content -match $game1BaseType) {
        Add-Failure "Manager file takes direct Game1 dependency outside context bridge: $relative"
    }
}

# Rule 5: SimulationRuntime owns tick ordering.
# The runtime type must exist, SimulationWorld.AdvanceOneTick must delegate to it, and the
# retired RuntimeController/RuntimePhaseController pass-through layers must not come back.
$simulationRuntimeFile = Join-Path $rootPath "Core/Simulation/Systems/SimulationRuntime.cs"
if (-not (Test-Path -LiteralPath $simulationRuntimeFile -PathType Leaf) `
        -or (Remove-CSharpComments (Get-Content -LiteralPath $simulationRuntimeFile -Raw)) -notmatch '\bclass\s+SimulationRuntime\b') {
    Add-Failure "Core/Simulation/Systems/SimulationRuntime.cs must define SimulationRuntime (tick ordering owner)"
}
if (-not $simWorldCore `
        -or (Remove-CSharpComments (Get-Content -LiteralPath $simWorldCore -Raw)) -notmatch '_runtime\s*\.\s*Tick\s*\(') {
    Add-Failure "SimulationWorld.AdvanceOneTick must delegate to SimulationRuntime.Tick()"
}
foreach ($file in Get-CSharpFiles "Core/Simulation") {
    $content = Remove-CSharpComments (Get-Content -LiteralPath $file.FullName -Raw)
    if ($content -match '\bclass\s+(RuntimeController|RuntimePhaseController)\b') {
        Add-Failure "Retired tick-ordering controller reintroduced (ordering belongs in SimulationRuntime): $($file.FullName.Substring($rootPath.Length + 1))"
    }
}

# Rule 6: SimulationWorld instance-field ratchet.
# SimulationWorld is meant to shrink toward pure orchestration; state belongs in owned
# systems/stores. This count may only go down: when you extract state, lower the limit.
# (Counted: 4-space-indented instance fields named _xxx across SimulationWorld*.cs. HEAD
# before the store extractions was 170, 101 after chunk 1, and 63 after chunk 2.)
$maxSimulationWorldFields = 4
$instanceFieldPattern = '(?m)^    (?:private|internal|public|protected)[ \t]+(?!(?:const|static)\b)(?:readonly[ \t]+)?[\w<>\[\],\.\?\(\) ]+?[ \t]+_\w+[ \t]*(?:=|;)'
$simulationWorldFieldCount = 0
foreach ($file in Get-CSharpFiles "Core/Simulation" | Where-Object { $_.Name -like "SimulationWorld*.cs" }) {
    $content = Remove-CSharpComments (Get-Content -LiteralPath $file.FullName -Raw)
    $simulationWorldFieldCount += [regex]::Matches($content, $instanceFieldPattern).Count
}
if ($simulationWorldFieldCount -gt $maxSimulationWorldFields) {
    Add-Failure "SimulationWorld has $simulationWorldFieldCount instance fields (limit $maxSimulationWorldFields). Put new state in an owned system/store instead of the world."
} elseif ($simulationWorldFieldCount -lt $maxSimulationWorldFields) {
    Write-Host "Note: SimulationWorld has $simulationWorldFieldCount instance fields; lower `$maxSimulationWorldFields from $maxSimulationWorldFields to lock in the improvement."
}

# Rule 7: Back-references to SimulationWorld inside Core/Simulation need an allowlist entry.
# Nested controllers that store `_world` are legacy seams being retired; new ones must not
# appear. Extracted systems use narrow host interfaces instead (see Rule 1).
# FixedStepSimulator is the external tick driver (it owns the accumulator and calls AdvanceOneTick),
# so it legitimately holds the world; it is not a system.
$worldBackReferenceAllowlist = @(
    "Core/Simulation/Core/FixedStepSimulator.cs"
)
$usedBackReferenceAllowlist = [System.Collections.Generic.HashSet[string]]::new()
$worldBackReferencePattern = '(?:(?:private|internal|public|protected)[ \t]+(?:readonly[ \t]+)?SimulationWorld[ \t]+_?world\b)|(?:\bclass[ \t]+\w+[ \t]*\([^)]*\bSimulationWorld[ \t]+\w+)'
foreach ($file in Get-CSharpFiles "Core/Simulation") {
    $relative = $file.FullName.Substring($rootPath.Length + 1).Replace('\', '/')
    if ($relative.StartsWith("Core/Simulation/Systems/")) { continue } # covered by Rule 1
    $content = Remove-CSharpComments (Get-Content -LiteralPath $file.FullName -Raw)
    if ($content -match $worldBackReferencePattern) {
        if ($worldBackReferenceAllowlist -contains $relative) {
            [void]$usedBackReferenceAllowlist.Add($relative)
        } else {
            Add-Failure "Stores a SimulationWorld back-reference without allowlist entry (use a host interface): $relative"
        }
    }
}
# The allowlist may only shrink: an entry that no longer holds a back-reference must be removed.
foreach ($entry in $worldBackReferenceAllowlist) {
    if (-not $usedBackReferenceAllowlist.Contains($entry)) {
        Add-Failure "Stale back-reference allowlist entry (remove it from verify-architecture-boundaries.ps1): $entry"
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
