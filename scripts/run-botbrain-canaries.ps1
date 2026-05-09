[CmdletBinding()]
param(
    [string]$Suite = "BotBrain.Tools/canaries/current-passing-canaries.json",
    [string]$Waivers = "",
    [switch]$NoBuild,
    [switch]$FailOnKnownFailRegression,
    [switch]$KeepLogs
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "BotBrain.Tools\OpenGarrison.BotBrain.Tools.csproj"
$suitePath = if ([System.IO.Path]::IsPathRooted($Suite)) { $Suite } else { Join-Path $repoRoot $Suite }

if (-not (Test-Path $suitePath)) {
    throw "BotBrain canary suite not found: $suitePath"
}

$runStamp = Get-Date -Format "yyyyMMdd-HHmmss"
$logRoot = Join-Path $repoRoot "artifacts\botbrain-canaries\$runStamp"
New-Item -ItemType Directory -Path $logRoot -Force | Out-Null

function ConvertTo-Array {
    param($Value)

    if ($null -eq $Value) {
        return @()
    }

    if ($Value -is [System.Array]) {
        return @($Value)
    }

    return @($Value)
}

function Get-PropertyValue {
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

function Get-ResultRank {
    param([string]$Result)

    switch ($Result) {
        "NoPath" { return 0 }
        "NoUsefulProgress" { return 1 }
        "Progressed" { return 2 }
        "PickedIntel" { return 3 }
        "Scored" { return 4 }
        default { return -1 }
    }
}

function Get-LineValue {
    param(
        [string[]]$Lines,
        [string]$Prefix
    )

    foreach ($line in $Lines) {
        if ($line.StartsWith($Prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
            return $line.Substring($Prefix.Length)
        }
    }

    return ""
}

function Get-SummaryField {
    param(
        [string]$Summary,
        [string]$Name
    )

    if ([string]::IsNullOrWhiteSpace($Summary)) {
        return ""
    }

    $pattern = "(?:^|\s)" + [regex]::Escape($Name) + ":([^\s]+)"
    $match = [regex]::Match($Summary, $pattern)
    if (-not $match.Success) {
        return ""
    }

    return $match.Groups[1].Value
}

function Test-CaseWaived {
    param(
        [object[]]$WaiverCases,
        [string]$CaseId,
        [string]$ActualResult
    )

    foreach ($waiver in $WaiverCases) {
        $waiverId = [string](Get-PropertyValue $waiver "id" "")
        if ($waiverId -ne $CaseId) {
            continue
        }

        $allowedActual = [string](Get-PropertyValue $waiver "allowedActual" "")
        if ([string]::IsNullOrWhiteSpace($allowedActual) -or $allowedActual -eq $ActualResult) {
            return $true
        }
    }

    return $false
}

function Get-WaiverReason {
    param(
        [object[]]$WaiverCases,
        [string]$CaseId
    )

    foreach ($waiver in $WaiverCases) {
        $waiverId = [string](Get-PropertyValue $waiver "id" "")
        if ($waiverId -eq $CaseId) {
            return [string](Get-PropertyValue $waiver "reason" "waived")
        }
    }

    return ""
}

$suiteJson = Get-Content -Raw -Path $suitePath | ConvertFrom-Json
$cases = @(ConvertTo-Array (Get-PropertyValue $suiteJson "cases" @()))

$waiverCases = @()
if (-not [string]::IsNullOrWhiteSpace($Waivers)) {
    $waiverPath = if ([System.IO.Path]::IsPathRooted($Waivers)) { $Waivers } else { Join-Path $repoRoot $Waivers }
    if (-not (Test-Path $waiverPath)) {
        throw "BotBrain waiver file not found: $waiverPath"
    }

    $waiverJson = Get-Content -Raw -Path $waiverPath | ConvertFrom-Json
    $waiverCases = @(ConvertTo-Array (Get-PropertyValue $waiverJson "waivers" @()))
}

if ($cases.Count -eq 0) {
    throw "BotBrain canary suite has no cases: $suitePath"
}

Write-Host "[botbrain-canaries] suite=$suitePath cases=$($cases.Count) noBuild=$($NoBuild.IsPresent) logs=$logRoot"

$rows = New-Object System.Collections.Generic.List[object]
$failed = $false
$index = 0

foreach ($case in $cases) {
    $index += 1
    $id = [string](Get-PropertyValue $case "id" "case-$index")
    $map = [string](Get-PropertyValue $case "map" "Truefort")
    $area = [int](Get-PropertyValue $case "area" 1)
    $team = [string](Get-PropertyValue $case "team" "Red")
    $class = [string](Get-PropertyValue $case "class" "Scout")
    $ticks = [int](Get-PropertyValue $case "ticks" 900)
    $reportEvery = [int](Get-PropertyValue $case "reportEvery" 30)
    $expected = [string](Get-PropertyValue $case "expected" "Pass")
    $minimumResult = [string](Get-PropertyValue $case "minimumResult" "Scored")
    $protected = [bool](Get-PropertyValue $case "protected" ($expected -eq "Pass"))

    $arguments = New-Object System.Collections.Generic.List[string]
    $arguments.Add("run")
    $arguments.Add("--project")
    $arguments.Add($projectPath)
    if ($NoBuild) {
        $arguments.Add("--no-build")
    }
    $arguments.Add("--")
    $arguments.Add("--map")
    $arguments.Add($map)
    $arguments.Add("--area")
    $arguments.Add([string]$area)
    $arguments.Add("--team")
    $arguments.Add($team)
    $arguments.Add("--class")
    $arguments.Add($class)
    $arguments.Add("--ticks")
    $arguments.Add([string]$ticks)
    $arguments.Add("--report-every")
    $arguments.Add([string]$reportEvery)

    $safeId = $id -replace "[^A-Za-z0-9_.-]", "_"
    $logPath = Join-Path $logRoot "$safeId.log"

    Write-Host "[$index/$($cases.Count)] $id map=$map area=$area team=$team class=$class ticks=$ticks expected=$expected min=$minimumResult"

    $output = & dotnet @arguments 2>&1
    $exitCode = $LASTEXITCODE
    $lines = @($output | ForEach-Object { [string]$_ })
    Set-Content -Path $logPath -Value $lines

    $actualResult = Get-LineValue $lines "result="
    if ([string]::IsNullOrWhiteSpace($actualResult)) {
        $actualResult = if ($exitCode -eq 0) { "UnknownPass" } else { "UnknownFail" }
    }

    $assetLine = ""
    $assetValidationLine = ""
    $blockerLine = ""
    $edgeMaxLine = ""
    foreach ($line in $lines) {
        if ($line.StartsWith("asset=", [System.StringComparison]::OrdinalIgnoreCase)) { $assetLine = $line }
        if ($line.StartsWith("assetValidation=", [System.StringComparison]::OrdinalIgnoreCase)) { $assetValidationLine = $line }
        if ([string]::IsNullOrWhiteSpace($blockerLine) -and $line.StartsWith("blocker=", [System.StringComparison]::OrdinalIgnoreCase)) { $blockerLine = $line }
        if ($line.StartsWith("edgeMax=", [System.StringComparison]::OrdinalIgnoreCase)) { $edgeMaxLine = $line }
    }

    $summary = Get-LineValue $lines "summary="
    $scoreTick = Get-SummaryField $summary "scoreTick"
    $carryingIntelTick = Get-SummaryField $summary "carryingIntelTick"
    $progress = Get-SummaryField $summary "progress"
    $stagnantWindows = Get-SummaryField $summary "stagnantWindows"

    $actualRank = Get-ResultRank $actualResult
    $minimumRank = Get-ResultRank $minimumResult
    $meetsThreshold = $actualRank -ge $minimumRank -and $minimumRank -ge 0
    $hasAssetValidationIssue = -not [string]::IsNullOrWhiteSpace($assetValidationLine)
    $waived = Test-CaseWaived $waiverCases $id $actualResult
    $waiverReason = if ($waived) { Get-WaiverReason $waiverCases $id } else { "" }

    $status = "PASS"
    if ($expected -eq "KnownFail") {
        if ($meetsThreshold) {
            $status = "IMPROVED"
        }
        else {
            $status = "KNOWNFAIL"
            if ($FailOnKnownFailRegression -and $exitCode -ne 0) {
                $failed = $true
            }
        }
    }
    elseif (-not $meetsThreshold -or ($protected -and $hasAssetValidationIssue)) {
        if ($waived) {
            $status = "CONTROLLED"
        }
        else {
            $status = "FAIL"
            $failed = $true
        }
    }

    $row = [pscustomobject]@{
        Status = $status
        Id = $id
        Expected = $expected
        Minimum = $minimumResult
        Actual = $actualResult
        ExitCode = $exitCode
        ScoreTick = $scoreTick
        CarryTick = $carryingIntelTick
        Progress = $progress
        Stagnant = $stagnantWindows
        AssetIssue = if ($hasAssetValidationIssue) { "yes" } else { "no" }
        Waiver = $waiverReason
        Log = $logPath
    }
    $rows.Add($row) | Out-Null

    $detail = "result=$actualResult exit=$exitCode progress=$progress scoreTick=$scoreTick carryTick=$carryingIntelTick stagnant=$stagnantWindows"
    if ($hasAssetValidationIssue) { $detail += " assetIssue=1" }
    if (-not [string]::IsNullOrWhiteSpace($blockerLine)) { $detail += " blocker=1" }
    if ($status -eq "CONTROLLED") { $detail += " waiver='$waiverReason'" }
    Write-Host ("{0,-10} {1} {2}" -f $status, $id, $detail)
}

$summaryPath = Join-Path $logRoot "summary.csv"
$rows | Export-Csv -Path $summaryPath -NoTypeInformation

Write-Host ""
Write-Host "[botbrain-canaries] summary"
$rows | Format-Table -AutoSize Status, Id, Minimum, Actual, ExitCode, ScoreTick, Progress, Stagnant, AssetIssue
Write-Host "[botbrain-canaries] summaryCsv=$summaryPath"

if (-not $KeepLogs) {
    Write-Host "[botbrain-canaries] logs retained for this run. Use -KeepLogs for intent documentation; logs are always kept currently."
}

if ($failed) {
    Write-Error "BotBrain canary suite failed. See $summaryPath"
    exit 1
}

exit 0
