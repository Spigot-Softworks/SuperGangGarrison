[CmdletBinding()]
param(
    [string]$ArtifactsRoot = "artifacts/botbrain-canaries",
    [string]$OutputDirectory = "artifacts/botbrain-stock-coverage",
    [string[]]$Classes = @("Scout", "Engineer", "Pyro", "Soldier", "Demoman", "Heavy", "Sniper", "Medic", "Spy", "Quote"),
    [string[]]$Teams = @("Red", "Blue")
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$catalogPath = Join-Path $repoRoot "Core\Configuration\Gg2PreferencesDocument.cs"
$botNavPath = Join-Path $repoRoot "Core\Content\BotNav"
$scoreRoutePath = Join-Path $repoRoot "Core\Content\BotNavScoreRoutes"
$hintPath = Join-Path $repoRoot "Core\Content\BotNavHints"
$runtimeCachePath = Join-Path $repoRoot "BotBrain.Tools\bin\Debug\net10.0\config\botbrain-nav"
$artifactsPath = if ([System.IO.Path]::IsPathRooted($ArtifactsRoot)) { $ArtifactsRoot } else { Join-Path $repoRoot $ArtifactsRoot }
$outputPath = if ([System.IO.Path]::IsPathRooted($OutputDirectory)) { $OutputDirectory } else { Join-Path $repoRoot $OutputDirectory }

New-Item -ItemType Directory -Path $outputPath -Force | Out-Null

function Get-StockMaps {
    $source = Get-Content -Raw -Path $catalogPath
    $pattern = 'new\("(?<ini>[^"]+)",\s*"(?<level>[^"]+)",\s*"(?<display>[^"]+)",\s*GameModeKind\.(?<mode>[A-Za-z]+),\s*(?<order>\d+)'
    foreach ($match in [regex]::Matches($source, $pattern)) {
        [pscustomobject]@{
            Order = [int]$match.Groups["order"].Value
            IniKey = $match.Groups["ini"].Value
            LevelName = $match.Groups["level"].Value
            DisplayName = $match.Groups["display"].Value
            Mode = $match.Groups["mode"].Value
        }
    }
}

function Get-FileNameSet {
    param(
        [string]$Path,
        [string]$Filter
    )

    $set = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    if (Test-Path $Path) {
        Get-ChildItem -Path $Path -File -Filter $Filter | ForEach-Object {
            [void]$set.Add($_.Name)
        }
    }

    return $set
}

function Get-ProofRows {
    $proof = @{}
    if (-not (Test-Path $artifactsPath)) {
        return $proof
    }

    Get-ChildItem -Path $artifactsPath -Recurse -File -Filter "summary.csv" | ForEach-Object {
        $runStamp = Split-Path -Leaf (Split-Path -Parent $_.FullName)
        $summaryPath = $_.FullName
        Import-Csv -Path $summaryPath | ForEach-Object {
            $id = [string](Get-OptionalProperty $_ "Id" "")
            $parts = $id -split "-"
            if ($parts.Count -lt 5) {
                return
            }

            $map = $parts[0]
            $areaText = $parts[1]
            $area = if ($areaText -match '^area(?<area>\d+)$') { [int]$Matches["area"] } else { 1 }
            $team = $parts[2]
            $class = $parts[3]
            $key = "$map|$area|$team|$class"
            $actualValue = [string](Get-OptionalProperty $_ "Actual" "")
            $rank = switch ($actualValue) {
                "Scored" { 4 }
                "PickedIntel" { 3 }
                "Progressed" { 2 }
                "NoUsefulProgress" { 1 }
                "NoPath" { 0 }
                default { -1 }
            }

            $existing = $proof[$key]
            if ($null -eq $existing -or $runStamp -gt $existing.RunStamp) {
                $proof[$key] = [pscustomobject]@{
                    Rank = $rank
                    RunStamp = $runStamp
                    Status = [string](Get-OptionalProperty $_ "Status" "")
                    Actual = $actualValue
                    FailureBucket = [string](Get-OptionalProperty $_ "FailureBucket" "")
                    ScoreTick = [string](Get-OptionalProperty $_ "ScoreTick" "")
                    Progress = [string](Get-OptionalProperty $_ "Progress" "")
                    SummaryPath = $summaryPath
                }
            }
        }
    }

    return $proof
}

function Get-OptionalProperty {
    param(
        [Parameter(Mandatory = $true)]
        $Object,
        [Parameter(Mandatory = $true)]
        [string]$Name,
        $Default = $null
    )

    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return $Default
    }

    return $property.Value
}

$modernAssets = Get-FileNameSet $botNavPath "*.modern.botnav.json"
$scoreRoutes = Get-FileNameSet $scoreRoutePath "*.botnavscore.json"
$hints = Get-FileNameSet $hintPath "*.botnavhints.json"
$runtimeCaches = Get-FileNameSet $runtimeCachePath "*.botnav.json"
$proofRows = Get-ProofRows

$stockRows = New-Object System.Collections.Generic.List[object]
$classRows = New-Object System.Collections.Generic.List[object]

foreach ($map in (Get-StockMaps | Sort-Object Order)) {
    $level = $map.LevelName
    $areas = New-Object System.Collections.Generic.List[int]
    foreach ($assetName in $modernAssets) {
        $pattern = '^' + [regex]::Escape($level) + '\.a(?<area>\d+)\.modern\.botnav\.json$'
        $match = [regex]::Match($assetName, $pattern, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
        if ($match.Success) {
            $areas.Add([int]$match.Groups["area"].Value) | Out-Null
        }
    }

    if ($areas.Count -eq 0) {
        $areas.Add(1) | Out-Null
    }

    foreach ($area in ($areas | Sort-Object -Unique)) {
        $modernArea = "$level.a$area.modern.botnav.json"
        $runtimePrefix = "$($level.ToLowerInvariant()).a$area."
        $hasRuntimeCache = $false
        foreach ($cacheName in $runtimeCaches) {
            if ($cacheName.StartsWith($runtimePrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
                $hasRuntimeCache = $true
                break
            }
        }

        $hasScoreRoute = $scoreRoutes.Contains("$level.a$area.botnavscore.json")
        $hasHints = $hints.Contains("$level.a$area.botnavhints.json")

        $tested = 0
        $scored = 0
        $bestActuals = New-Object System.Collections.Generic.List[string]
        foreach ($team in $Teams) {
            foreach ($class in $Classes) {
                $key = "$($level.ToLowerInvariant())|$area|$($team.ToLowerInvariant())|$($class.ToLowerInvariant())"
                $proof = $proofRows[$key]
                $actual = "Untested"
                $bucket = ""
                $scoreTick = ""
                $runStamp = ""
                if ($null -ne $proof) {
                    $tested += 1
                    $actual = $proof.Actual
                    $bucket = $proof.FailureBucket
                    $scoreTick = $proof.ScoreTick
                    $runStamp = $proof.RunStamp
                    if ($actual -eq "Scored") {
                        $scored += 1
                    }
                }

                $bestActuals.Add("$team/$class=$actual") | Out-Null
                $classRows.Add([pscustomobject]@{
                    Order = $map.Order
                    Map = $level
                    Area = $area
                    Mode = $map.Mode
                    Team = $team
                    Class = $class
                    Actual = $actual
                    FailureBucket = $bucket
                    ScoreTick = $scoreTick
                    RunStamp = $runStamp
                    HasModernBotNav = $modernAssets.Contains($modernArea)
                    HasBotBrainRuntimeCache = $hasRuntimeCache
                    HasScoreRoute = $hasScoreRoute
                    HasHints = $hasHints
                }) | Out-Null
            }
        }

        $assetStatus = if (-not $modernAssets.Contains($modernArea)) {
            "MissingModernBotNav"
        } elseif (-not $hasRuntimeCache) {
            "MissingBotBrainRuntimeCache"
        } else {
            "RuntimeCachePresent"
        }

        $stockRows.Add([pscustomobject]@{
            Order = $map.Order
            Map = $level
            Area = $area
            Mode = $map.Mode
            AssetStatus = $assetStatus
            HasModernBotNav = $modernAssets.Contains($modernArea)
            HasBotBrainRuntimeCache = $hasRuntimeCache
            HasScoreRoute = $hasScoreRoute
            HasHints = $hasHints
            TestedClassTeamPairs = $tested
            ScoredClassTeamPairs = $scored
            UntestedClassTeamPairs = ($Classes.Count * $Teams.Count) - $tested
            ProofSummary = [string]::Join("; ", $bestActuals)
        }) | Out-Null
    }
}

$stockCsv = Join-Path $outputPath "stock-map-coverage.csv"
$classCsv = Join-Path $outputPath "stock-class-team-coverage.csv"
$stockRows | Export-Csv -NoTypeInformation -Path $stockCsv
$classRows | Export-Csv -NoTypeInformation -Path $classCsv

$missingRuntime = @($stockRows | Where-Object { -not $_.HasBotBrainRuntimeCache })
$unscoredTested = @($classRows | Where-Object { $_.Actual -ne "Untested" -and $_.Actual -ne "Scored" })
$untested = @($classRows | Where-Object { $_.Actual -eq "Untested" })

Write-Host "[botbrain-stock-coverage] maps=$($stockRows.Count) classTeamPairs=$($classRows.Count)"
Write-Host "[botbrain-stock-coverage] missingRuntimeCaches=$($missingRuntime.Count) unscoredTested=$($unscoredTested.Count) untested=$($untested.Count)"
Write-Host "[botbrain-stock-coverage] stockCsv=$stockCsv"
Write-Host "[botbrain-stock-coverage] classTeamCsv=$classCsv"

if ($missingRuntime.Count -gt 0) {
    Write-Host "[botbrain-stock-coverage] maps missing BotBrain runtime cache:"
    $missingRuntime | ForEach-Object { Write-Host "  $($_.Map) area=$($_.Area) mode=$($_.Mode) modern=$($_.HasModernBotNav)" }
}

if ($unscoredTested.Count -gt 0) {
    Write-Host "[botbrain-stock-coverage] tested but not scored:"
    $unscoredTested | Select-Object -First 30 | ForEach-Object {
        Write-Host "  $($_.Map) area=$($_.Area) $($_.Team) $($_.Class) actual=$($_.Actual) bucket=$($_.FailureBucket) run=$($_.RunStamp)"
    }
}
