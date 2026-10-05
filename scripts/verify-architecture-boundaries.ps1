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

# Rule 8: Tests do not reflect on the world.
# Tests call the systems or the Test* seams (World/SimulationWorld.TestSeams.cs or a
# system's *.TestSeams.cs). Add an internal seam instead of reflecting.
foreach ($file in Get-CSharpFiles "Tests") {
    $content = Remove-CSharpComments (Get-Content -LiteralPath $file.FullName -Raw)
    if ($content -match 'typeof\s*\(\s*SimulationWorld\s*\)') {
        Add-Failure "Test reflects on SimulationWorld (use a system or a Test* seam): $($file.FullName.Substring($rootPath.Length + 1))"
    }
}

# Rule 9: World layout. The world's partials live in Core/Simulation/World/ (world-owned
# members) and Core/Simulation/World/Hosts/ (one file per host interface), and there are
# no forwarder files: callers use the systems directly.
foreach ($file in Get-CSharpFiles "Core/Simulation") {
    $relative = $file.FullName.Substring($rootPath.Length + 1).Replace('\', '/')
    if ($file.Name -like "*Forwarders.cs") {
        Add-Failure "Forwarder file reintroduced (call the system directly): $relative"
    }
    $content = Remove-CSharpComments (Get-Content -LiteralPath $file.FullName -Raw)
    if ($content -match '\bpartial\s+class\s+SimulationWorld\b' -and -not $relative.StartsWith("Core/Simulation/World/")) {
        Add-Failure "SimulationWorld partial outside Core/Simulation/World/: $relative"
    }
}

# Rule 10: SimulationWorld public-member ratchet. New behavior goes on a system; the world
# exposes the system as a property. This count may only go down.
# (Counted: 4-space-indented `public` member lines across Core/Simulation/World/.)
$maxSimulationWorldPublicMembers = 133
$simulationWorldPublicMemberCount = 0
foreach ($file in Get-CSharpFiles "Core/Simulation/World") {
    $content = Remove-CSharpComments (Get-Content -LiteralPath $file.FullName -Raw)
    $simulationWorldPublicMemberCount += [regex]::Matches($content, '(?m)^    public\b').Count
}
if ($simulationWorldPublicMemberCount -gt $maxSimulationWorldPublicMembers) {
    Add-Failure "SimulationWorld has $simulationWorldPublicMemberCount public members (limit $maxSimulationWorldPublicMembers). Put new behavior on a system."
} elseif ($simulationWorldPublicMemberCount -lt $maxSimulationWorldPublicMembers) {
    Write-Host "Note: SimulationWorld has $simulationWorldPublicMemberCount public members; lower `$maxSimulationWorldPublicMembers from $maxSimulationWorldPublicMembers to lock in the improvement."
}

# Rule 11: Host interface ceilings. A host lists what a system needs from the world; when a
# member is a straight forward to a sibling system, expose that system as a host property
# instead (see docs/architecture/simulation-cleanup-plan.md, B2). Counts may only go down;
# a new host starts with a ceiling of $defaultHostMemberCeiling.
# (Counted: 4-space-indented member lines inside `interface I...Host` bodies.)
$hostMemberCeilings = @{
    "IAdminCommandsHost" = 4
    "IAirblastRulesHost" = 19
    "IClassRulesHost" = 6
    "ICombatFeedbackHost" = 8
    "ICombatGeometryHost" = 8
    "ICombatSystemHost" = 0
    "IDamageRulesHost" = 5
    "IDecisionGateHost" = 7
    "IEntityPhaseHost" = 34
    "IExperimentalRulesHost" = 28
    "IExplosionRulesHost" = 24
    "IGameplayAbilityHost" = 32
    "IKillFeedHost" = 1
    "ILastToDieHost" = 22
    "IMapLifecycleHost" = 35
    "IMapLogicHost" = 9
    "IMatchObjectiveHost" = 32
    "IMatchPhaseHost" = 16
    "IMovementSystemHost" = 3
    "INetworkPlayerHost" = 32
    "IObjectiveRulesHost" = 36
    "IPickupHost" = 14
    "IPlayerCountHost" = 5
    "IPlayerDeathHost" = 29
    "IPlayerInputHost" = 24
    "IPlayerPresentationBoundsHost" = 2
    "IPlayerRemainsHost" = 10
    "IPracticeDummyHost" = 16
    "IProjectileSystemHost" = 0
    "IReadyUpHost" = 4
    "IRoomEffectsHost" = 2
    "IScorekeepingHost" = 1
    "IServerTuningHost" = 8
    "ISimulationTickHost" = 12
    "ISnapshotApplyHost" = 41
    "ISnapshotSystemHost" = 7
    "ISpawnHost" = 19
    "IStructureHost" = 25
    "ISupportRulesHost" = 13
    "IVipRulesHost" = 10
    "IWeaponFireHost" = 37
    "IWorldEffectsHost" = 9
}
$defaultHostMemberCeiling = 20
$hostMemberCounts = @{}
foreach ($file in Get-CSharpFiles "Core/Simulation/Systems") {
    $content = Remove-CSharpComments (Get-Content -LiteralPath $file.FullName -Raw)
    foreach ($match in [regex]::Matches($content, '\binterface\s+(I\w+Host)\b[^{;]*\{')) {
        $depth = 0
        $end = $match.Index + $match.Length - 1
        for ($i = $end; $i -lt $content.Length; $i++) {
            $character = $content[$i]
            if ($character -eq '{') { $depth++ }
            elseif ($character -eq '}') {
                $depth--
                if ($depth -eq 0) { $end = $i; break }
            }
        }
        $bodyStart = $match.Index + $match.Length
        $body = $content.Substring($bodyStart, $end - $bodyStart)
        $name = $match.Groups[1].Value
        $count = [regex]::Matches($body, '(?m)^    [A-Za-z_]').Count
        if ($hostMemberCounts.ContainsKey($name)) { $hostMemberCounts[$name] += $count } else { $hostMemberCounts[$name] = $count }
    }
}
foreach ($name in $hostMemberCounts.Keys) {
    $ceiling = if ($hostMemberCeilings.ContainsKey($name)) { $hostMemberCeilings[$name] } else { $defaultHostMemberCeiling }
    $count = $hostMemberCounts[$name]
    if ($count -gt $ceiling) {
        Add-Failure "$name has $count members (limit $ceiling). Expose a sibling system as a host property instead of forwarding each call."
    } elseif ($count -lt $ceiling -and $hostMemberCeilings.ContainsKey($name)) {
        Write-Host "Note: $name has $count members; lower its ceiling from $ceiling to lock in the improvement."
    }
}

# Rule 12: System property naming. A world property that exposes a system is named after
# its type without the `System` suffix (`KillFeedSystem KillFeed`), optionally plural
# (`SpawnSystem Spawns`, `PracticeDummySystem PracticeDummies`). Older names that predate
# the rule are listed here; do not add to the list.
$systemPropertyNameExceptions = @{
    "GameplayAbilitySystem" = "Abilities"
    "PlayerPresentationBoundsSystem" = "PresentationBounds"
}
foreach ($file in Get-CSharpFiles "Core/Simulation/World") {
    $content = Remove-CSharpComments (Get-Content -LiteralPath $file.FullName -Raw)
    foreach ($match in [regex]::Matches($content, '(?m)^    (?:public|internal)\s+(\w+System)\s+(\w+)\s*\{\s*get;')) {
        $typeName = $match.Groups[1].Value
        $propertyName = $match.Groups[2].Value
        $baseName = $typeName.Substring(0, $typeName.Length - "System".Length)
        $allowed = @($baseName, ($baseName + "s"))
        if ($baseName.EndsWith("y")) { $allowed += ($baseName.Substring(0, $baseName.Length - 1) + "ies") }
        if ($systemPropertyNameExceptions.ContainsKey($typeName)) { $allowed += $systemPropertyNameExceptions[$typeName] }
        if ($allowed -notcontains $propertyName) {
            Add-Failure "World property '$propertyName' of type $typeName should be named '$baseName' (or its plural)."
        }
    }
}

# Rule 13: Lua is the only plugin runtime. Product code must not load assemblies from disk.
foreach ($dir in @("Core", "Client", "Client.Shared", "Client.Browser", "Server", "SessionRuntime")) {
    foreach ($file in Get-CSharpFiles $dir) {
        $content = Remove-CSharpComments (Get-Content -LiteralPath $file.FullName -Raw)
        if ($content -match '\bLoadFromAssemblyPath\b|\bAssembly\.LoadFrom\b|\bAssembly\.LoadFile\b|\bAssembly\.UnsafeLoadFrom\b') {
            Add-Failure "Loads assemblies from disk (plugins are Lua-only): $($file.FullName.Substring($rootPath.Length + 1))"
        }
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
