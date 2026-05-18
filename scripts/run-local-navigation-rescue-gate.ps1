param(
    [string]$Map = "Truefort",
    [int]$Area = 1,
    [string]$Class = "Pyro",
    [int]$Ticks = 6200,
    [string]$ArtifactRoot = "",
    [string]$XOffsets = "-8,0,8",
    [string]$BottomOffsets = "0",
    [string]$HorizontalSpeeds = "-20,0,20",
    [string]$VerticalSpeeds = "0",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$project = Join-Path $repoRoot "BotBrain.Tools\OpenGarrison.BotBrain.Tools.csproj"

if ([string]::IsNullOrWhiteSpace($ArtifactRoot)) {
    $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $ArtifactRoot = Join-Path $repoRoot "artifacts\local-navigation-rescue-current\gate-$stamp"
}
elseif (-not [System.IO.Path]::IsPathRooted($ArtifactRoot)) {
    $ArtifactRoot = Join-Path $repoRoot $ArtifactRoot
}

New-Item -ItemType Directory -Force -Path $ArtifactRoot | Out-Null

if (-not $SkipBuild) {
    & dotnet build $project
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed with exit code $LASTEXITCODE"
    }
}

function Invoke-TopologyGateTeam {
    param(
        [string]$Team,
        [string]$TeamArtifactDir
    )

    New-Item -ItemType Directory -Force -Path $TeamArtifactDir | Out-Null
    $arguments = @(
        "run",
        "--no-build",
        "--project",
        $project,
        "--",
        "--topology-local-motion-lab",
        "true",
        "--objective",
        "capture",
        "--map",
        $Map,
        "--area",
        $Area.ToString([System.Globalization.CultureInfo]::InvariantCulture),
        "--team",
        $Team,
        "--class",
        $Class,
        "--case",
        "spawn-capture",
        "--ticks",
        $Ticks.ToString([System.Globalization.CultureInfo]::InvariantCulture),
        "--report-every",
        "180",
        "--validation-x-offsets",
        $XOffsets,
        "--validation-bottom-offsets",
        $BottomOffsets,
        "--validation-horizontal-speeds",
        $HorizontalSpeeds,
        "--validation-vertical-speeds",
        $VerticalSpeeds,
        "--artifact-mode",
        "failures",
        "--artifacts-dir",
        $TeamArtifactDir
    )

    $rawOutput = & dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        $joinedOutput = $rawOutput -join [Environment]::NewLine
        throw "Topology local motion lab failed for $Team with exit code $LASTEXITCODE.`n$joinedOutput"
    }

    $jsonText = $rawOutput -join [Environment]::NewLine
    $summary = $jsonText | ConvertFrom-Json
    $summaryPath = Join-Path $TeamArtifactDir "summary.json"
    $jsonText | Set-Content -Path $summaryPath -Encoding UTF8

    $failures = @($summary.caseSummaries | Where-Object { -not $_.passed })
    return [pscustomobject]@{
        team = $Team
        artifactDirectory = $TeamArtifactDir
        summaryPath = $summaryPath
        cases = [int]$summary.cases
        passed = [int]$summary.passed
        failed = [int]$summary.failed
        failures = @($failures | ForEach-Object {
            [pscustomobject]@{
                caseIndex = $_.caseIndex
                scenario = $_.scenario
                failureReason = $_.failureReason
                pickedUpIntel = $_.pickedUpIntel
                captured = $_.captured
                pickupTick = $_.pickupTick
                scoreTick = $_.scoreTick
                finalX = $_.finalX
                finalBottom = $_.finalBottom
                reportPath = $_.reportPath
                overlayPath = $_.overlayPath
            }
        })
    }
}

$teamResults = @()
foreach ($team in @("Red", "Blue")) {
    $teamDir = Join-Path $ArtifactRoot $team.ToLowerInvariant()
    Write-Host "Running $team 9-case local-navigation gate..."
    $teamResults += Invoke-TopologyGateTeam -Team $team -TeamArtifactDir $teamDir
}

$totalCases = ($teamResults | Measure-Object -Property cases -Sum).Sum
$totalPassed = ($teamResults | Measure-Object -Property passed -Sum).Sum
$totalFailed = ($teamResults | Measure-Object -Property failed -Sum).Sum
$passedAll = $totalFailed -eq 0

$scoreboard = [pscustomobject]@{
    generatedAt = (Get-Date).ToString("o", [System.Globalization.CultureInfo]::InvariantCulture)
    map = $Map
    area = $Area
    class = $Class
    ticks = $Ticks
    xOffsets = $XOffsets
    bottomOffsets = $BottomOffsets
    horizontalSpeeds = $HorizontalSpeeds
    verticalSpeeds = $VerticalSpeeds
    artifactMode = "failures"
    artifactRoot = $ArtifactRoot
    passedAll = $passedAll
    cases = [int]$totalCases
    passed = [int]$totalPassed
    failed = [int]$totalFailed
    teams = $teamResults
}

$scoreboardPath = Join-Path $ArtifactRoot "scoreboard.json"
$scoreboard | ConvertTo-Json -Depth 8 | Set-Content -Path $scoreboardPath -Encoding UTF8

$markdownPath = Join-Path $ArtifactRoot "scoreboard.md"
$markdown = New-Object System.Collections.Generic.List[string]
$markdown.Add("# Local Navigation Rescue Gate")
$markdown.Add("")
$markdown.Add("- Generated: $($scoreboard.generatedAt)")
$markdown.Add("- Map: $Map area $Area")
$markdown.Add("- Class: $Class")
$markdown.Add("- Cases: $totalPassed / $totalCases passed")
$markdown.Add("- Passed all: $passedAll")
$markdown.Add("- Artifact mode: failures")
$markdown.Add("")
$markdown.Add("| Team | Passed | Failed | Artifact Directory |")
$markdown.Add("| --- | ---: | ---: | --- |")
foreach ($teamResult in $teamResults) {
    $markdown.Add(('| {0} | {1} / {2} | {3} | `{4}` |' -f $teamResult.team, $teamResult.passed, $teamResult.cases, $teamResult.failed, $teamResult.artifactDirectory))
}

$markdown.Add("")
$markdown.Add("## Failures")
if ($totalFailed -eq 0) {
    $markdown.Add("")
    $markdown.Add("None.")
}
else {
    foreach ($teamResult in $teamResults) {
        foreach ($failure in $teamResult.failures) {
            $markdown.Add("")
            $markdown.Add(('- {0} case {1} `{2}`: {3}' -f $teamResult.team, $failure.caseIndex, $failure.scenario, $failure.failureReason))
            $markdown.Add(('  - pickupTick: {0}, scoreTick: {1}, final: ({2:0.0}, {3:0.0})' -f $failure.pickupTick, $failure.scoreTick, $failure.finalX, $failure.finalBottom))
            if (-not [string]::IsNullOrWhiteSpace($failure.reportPath)) {
                $markdown.Add(('  - report: `{0}`' -f $failure.reportPath))
            }
            if (-not [string]::IsNullOrWhiteSpace($failure.overlayPath)) {
                $markdown.Add(('  - overlay: `{0}`' -f $failure.overlayPath))
            }
        }
    }
}

$markdown | Set-Content -Path $markdownPath -Encoding UTF8

Write-Host "Gate scoreboard: $scoreboardPath"
Write-Host "Gate markdown:   $markdownPath"
Write-Host "Gate result:     $totalPassed / $totalCases passed"

if (-not $passedAll) {
    exit 1
}
