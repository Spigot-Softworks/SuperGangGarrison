param(
    [string[]]$Maps = @(
        'TwodFortTwo',
        'Harvest',
        'Valley',
        'Corinth',
        'Sixties',
        'Atalia',
        'Eiger',
        'Waterway',
        'Conflict',
        'ClassicWell',
        'Gallery',
        'Orange'
    ),
    [string[]]$Profiles = @(
        'Scout',
        'Demoman',
        'Heavy',
        'Soldier',
        'Fast',
        'Quote'
    ),
    [string[]]$Classes = @(),
    [ValidateSet('Blue', 'Red')]
    [string]$Team = 'Blue',
    [int]$MaxParallel = 4,
    [int]$GraphMaxExpanded = 1500,
    [int]$MaxExpanded = 100000,
    [int]$SearchBudgetMs = 10000,
    [double]$CoverageTargetRatio = 0.70,
    [double]$ObjectiveReachabilityTargetRatio = 0.60,
    [int]$CoverageSampleStride = 192,
    [int]$CoverageSampleRadius = 128,
    [int]$CoverageHubSearchBudgetMs = 2500,
    [int]$CoverageHubFanoutSearchBudgetMs = 4000,
    [int]$CoverageGapSearchBudgetMs = 6000,
    [int]$CoverageGapMaxSamples = 16,
    [int]$CoverageGapSamplesPerComponent = 2,
    [int]$CoveragePathParallelism = 8,
    [string]$Configuration = 'Release',
    [switch]$EnableCoverageConnectivity,
    [int]$CoverageNeighborLinks = 10,
    [int]$CoverageLinkMaxDistance = 760,
    [int]$CoverageLinkSearchBudgetMs = 350,
    [string[]]$RoamSeedPaths = @(),
    [switch]$UseProofTapeSeeds,
    [switch]$FastRoamGraph,
    [switch]$WriteFailedGraphs,
    [switch]$SkipObjectiveSeedPaths,
    [switch]$EnableCoverageHubPaths,
    [switch]$DisableCoverageHubPaths,
    [switch]$DisableDefaultCanarySeeds,
    [switch]$DisableCanaryProofs,
    [switch]$DisableMultiGoalSeedSearch,
    [switch]$ForceRebuild,
    [switch]$AuditOnly,
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$toolProject = Join-Path $repoRoot 'MotionProof.Tools\OpenGarrison.MotionProof.Tools.csproj'
$artifactRoot = Join-Path $repoRoot 'Core\Content\MotionProof'
$graphArtifactRoot = Join-Path $artifactRoot 'graphs'
$tapeArtifactRoot = Join-Path $artifactRoot 'tapes'
$logRoot = Join-Path $repoRoot 'MotionProof.Tools\logs'
$candidateRoot = Join-Path $repoRoot 'MotionProof.Tools\candidates'
New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null
New-Item -ItemType Directory -Force -Path $graphArtifactRoot | Out-Null
New-Item -ItemType Directory -Force -Path $tapeArtifactRoot | Out-Null
New-Item -ItemType Directory -Force -Path $logRoot | Out-Null
New-Item -ItemType Directory -Force -Path $candidateRoot | Out-Null

if (-not $NoBuild) {
    dotnet build $toolProject -c $Configuration /nodeReuse:false /m:1
    if ($LASTEXITCODE -ne 0) {
        throw "MotionProof tool build failed."
    }
}

$mapAliases = @{
    'TwoFortTwoD' = 'TwodFortTwo'
    'TwoDFortTwo' = 'TwodFortTwo'
    '2DFort' = 'TwodFortTwo'
    'Classicwell' = 'ClassicWell'
}

function Resolve-MapName {
    param([string]$Map)
    if ($mapAliases.ContainsKey($Map)) {
        return $mapAliases[$Map]
    }

    return $Map
}

function Quote-Argument {
    param([string]$Value)
    if ($Value -notmatch '[\s"]') {
        return $Value
    }

    return '"' + ($Value -replace '"', '\"') + '"'
}

function Get-GraphArtifactPath {
    param(
        [string]$Map,
        [string]$Profile,
        [switch]$AllowLegacyFallback
    )

    $fileName = "$Map.$Profile.graph.json.gz"
    $preferredPath = Join-Path $graphArtifactRoot $fileName
    if ($AllowLegacyFallback) {
        $legacyPath = Join-Path $artifactRoot $fileName
        if ((-not (Test-Path -LiteralPath $preferredPath)) -and (Test-Path -LiteralPath $legacyPath)) {
            return $legacyPath
        }
    }

    return $preferredPath
}

function Get-TapeArtifactPath {
    param(
        [string]$Map,
        [string]$Team,
        [string]$Class,
        [switch]$AllowLegacyFallback
    )

    $fileName = "$Map.$Team.$Class.json"
    $preferredPath = Join-Path $tapeArtifactRoot $fileName
    if ($AllowLegacyFallback) {
        $legacyPath = Join-Path $artifactRoot $fileName
        if ((-not (Test-Path -LiteralPath $preferredPath)) -and (Test-Path -LiteralPath $legacyPath)) {
            return $legacyPath
        }
    }

    return $preferredPath
}

function Resolve-ProfileRepresentativeClass {
    param([string]$Profile)
    switch -Regex ($Profile) {
        '^(Scout)$' { return 'Scout' }
        '^(Heavy)$' { return 'Heavy' }
        '^(Soldier)$' { return 'Soldier' }
        '^(Demoman)$' { return 'Demoman' }
        '^(Fast)$' { return 'Pyro' }
        '^(Quote)$' { return 'Quote' }
        default { return $Profile }
    }
}

$defaultCanaries = @(
    [pscustomobject]@{
        Map = 'TwodFortTwo'
        Profile = '*'
        SeedStarts = @('3215,1098')
        Proofs = @(
            [pscustomobject]@{ Start = '3060,918'; Goal = '3215,1098'; Radius = 72 },
            [pscustomobject]@{ Start = '3215,1098'; Goal = '330,690'; Radius = 72 }
        )
    },
    [pscustomobject]@{
        Map = 'Corinth'
        Profile = 'Heavy'
        SeedStarts = @('1934,1140')
        Proofs = @(
            [pscustomobject]@{ Start = '1934,1140'; Goal = '1818,687'; Radius = 72 }
        )
    }
)

function Get-DefaultCanaries {
    param(
        [string]$Map,
        [string]$Profile
    )

    if ($DisableDefaultCanarySeeds) {
        return @()
    }

    return @($defaultCanaries | Where-Object {
        $_.Map -ieq $Map -and ($_.Profile -eq '*' -or $_.Profile -ieq $Profile)
    })
}

function Start-BakeProcess {
    param(
        [string]$Map,
        [string]$Profile
    )

    $resolvedMap = Resolve-MapName $Map
    $safeName = "$resolvedMap.$Profile"
    $finalOutputPath = Get-GraphArtifactPath -Map $resolvedMap -Profile $Profile
    $candidateId = [guid]::NewGuid().ToString("N")
    $candidatePath = Join-Path $candidateRoot "$safeName.$candidateId.graph.json.gz"
    $stdoutPath = Join-Path $logRoot "$safeName.out.log"
    $stderrPath = Join-Path $logRoot "$safeName.err.log"
    $canaries = Get-DefaultCanaries -Map $resolvedMap -Profile $Profile
    $arguments = @(
        'run',
        '--project', $toolProject,
        '-c', $Configuration,
        '--no-build',
        '--',
        '--map', $resolvedMap,
        '--team', $Team,
        '--movement-profile', $Profile,
        '--bake-graph',
        '--seed-walkable-limit', '0',
        '--no-coverage-connectivity',
        '--coverage-sample-stride', $CoverageSampleStride.ToString(),
        '--coverage-sample-radius', $CoverageSampleRadius.ToString(),
        '--coverage-target-ratio', $CoverageTargetRatio.ToString([Globalization.CultureInfo]::InvariantCulture),
        '--objective-reachability-target-ratio', $ObjectiveReachabilityTargetRatio.ToString([Globalization.CultureInfo]::InvariantCulture),
        '--coverage-hub-search-budget-ms', $CoverageHubSearchBudgetMs.ToString(),
        '--coverage-hub-fanout-search-budget-ms', $CoverageHubFanoutSearchBudgetMs.ToString(),
        '--coverage-gap-search-budget-ms', $CoverageGapSearchBudgetMs.ToString(),
        '--coverage-gap-max-samples', $CoverageGapMaxSamples.ToString(),
        '--coverage-gap-samples-per-component', $CoverageGapSamplesPerComponent.ToString(),
        '--coverage-path-parallelism', $CoveragePathParallelism.ToString(),
        '--graph-max-expanded', $GraphMaxExpanded.ToString(),
        '--max-expanded', $MaxExpanded.ToString(),
        '--search-budget-ms', $SearchBudgetMs.ToString(),
        '--output', $candidatePath
    )
    if (-not $DisableMultiGoalSeedSearch) {
        $arguments += '--multi-goal-seed-search'
    }

    if ($SkipObjectiveSeedPaths) {
        $arguments += '--skip-objective-seed-paths'
    }

    if ($FastRoamGraph) {
        $arguments += @(
            '--skip-gameplay-seed-goals',
            '--skip-objective-seed-paths'
        )
    }

    if ($UseProofTapeSeeds) {
        $representativeClass = Resolve-ProfileRepresentativeClass $Profile
        $proofPath = Get-TapeArtifactPath -Map $resolvedMap -Team $Team -Class $representativeClass -AllowLegacyFallback
        if (Test-Path -LiteralPath $proofPath) {
            $arguments += @('--seed-proof-artifact', $proofPath)
        }
    }

    foreach ($seedPath in $RoamSeedPaths) {
        $arguments += @('--seed-path', $seedPath)
    }

    if ($DisableCoverageHubPaths -or -not $EnableCoverageHubPaths) {
        $arguments += '--no-coverage-hub-paths'
    }

    foreach ($canary in $canaries) {
        foreach ($seedStart in $canary.SeedStarts) {
            $arguments += @('--seed-start', $seedStart)
        }

    }

    if ($EnableCoverageConnectivity) {
        $arguments = @($arguments | Where-Object { $_ -ne '--no-coverage-connectivity' })
        $arguments += @(
            '--coverage-neighbor-links', $CoverageNeighborLinks.ToString(),
            '--coverage-link-max-distance', $CoverageLinkMaxDistance.ToString(),
            '--coverage-link-search-budget-ms', $CoverageLinkSearchBudgetMs.ToString()
        )
    }

    if ($WriteFailedGraphs) {
        $arguments += '--write-failed-graph'
    }

    $argumentLine = ($arguments | ForEach-Object { Quote-Argument $_ }) -join ' '
    $process = Start-Process -FilePath 'dotnet' `
        -ArgumentList $argumentLine `
        -WorkingDirectory $repoRoot `
        -RedirectStandardOutput $stdoutPath `
        -RedirectStandardError $stderrPath `
        -NoNewWindow `
        -PassThru
    return [pscustomobject]@{
        Map = $resolvedMap
        Class = $Profile
        Process = $process
        Output = $candidatePath
        FinalOutput = $finalOutputPath
        StdoutPath = $stdoutPath
        StderrPath = $stderrPath
        Canaries = $canaries
        Started = Get-Date
    }
}

function Invoke-CanaryProofs {
    param(
        [object]$Job
    )

    if ($DisableCanaryProofs -or $Job.Canaries.Count -eq 0) {
        return $true
    }

    $allPassed = $true
    foreach ($canary in $Job.Canaries) {
        foreach ($proof in $canary.Proofs) {
            $startParts = @($proof.Start.Split(',') | ForEach-Object { $_.Trim() })
            $goalParts = @($proof.Goal.Split(',') | ForEach-Object { $_.Trim() })
            $proofArgs = @(
                'run',
                '--project', $toolProject,
                '-c', $Configuration,
                '--no-build',
                '--',
                '--map', $Job.Map,
                '--team', $Team,
                '--movement-profile', $Job.Class,
                '--start-x', $startParts[0],
                '--start-bottom', $startParts[1],
                '--prove-graph',
                '--graph', $Job.Output,
                '--goal-x', $goalParts[0],
                '--goal-y', $goalParts[1],
                '--goal-radius', ([string]$proof.Radius),
                '--search-budget-ms', '30000'
            )
            $proofLine = ($proofArgs | ForEach-Object { Quote-Argument $_ }) -join ' '
            $proofOutput = & dotnet $proofArgs 2>&1
            $passed = $LASTEXITCODE -eq 0
            Add-Content -Path $Job.StdoutPath -Value ("motion-proof canary command={0}" -f $proofLine)
            Add-Content -Path $Job.StdoutPath -Value ($proofOutput | Out-String)
            if (-not $passed) {
                $allPassed = $false
                Write-Host ("canary failed {0}.{1} start={2} goal={3}" -f $Job.Map, $Job.Class, $proof.Start, $proof.Goal)
            }
        }
    }

    return $allPassed
}

function Try-ValidateExistingArtifact {
    param(
        [string]$Map,
        [string]$Profile
    )

    if ($ForceRebuild -or $DisableCanaryProofs) {
        return $null
    }

    $resolvedMap = Resolve-MapName $Map
    $safeName = "$resolvedMap.$Profile"
    $outputPath = Get-GraphArtifactPath -Map $resolvedMap -Profile $Profile -AllowLegacyFallback
    if (-not (Test-Path -LiteralPath $outputPath)) {
        return $null
    }

    $canaries = Get-DefaultCanaries -Map $resolvedMap -Profile $Profile
    if ($canaries.Count -eq 0) {
        return $null
    }

    $stdoutPath = Join-Path $logRoot "$safeName.out.log"
    $stderrPath = Join-Path $logRoot "$safeName.err.log"
    $started = Get-Date
    $probeJob = [pscustomobject]@{
        Map = $resolvedMap
        Class = $Profile
        Output = $outputPath
        StdoutPath = $stdoutPath
        StderrPath = $stderrPath
        Canaries = $canaries
    }

    Add-Content -Path $stdoutPath -Value ("motion-proof existing-artifact status=checking artifact={0}" -f $outputPath)
    $canaryPassed = Invoke-CanaryProofs -Job $probeJob
    if (-not $canaryPassed) {
        Add-Content -Path $stdoutPath -Value "motion-proof existing-artifact status=stale reason=canary_failed"
        return $null
    }

    $duration = (Get-Date) - $started
    Add-Content -Path $stdoutPath -Value "motion-proof existing-artifact status=pass reason=canary_verified"
    return [pscustomobject]@{
        Map = $resolvedMap
        Class = $Profile
        ExitCode = 0
        CanaryPassed = $true
        Promoted = $false
        DurationSeconds = [math]::Round($duration.TotalSeconds, 1)
        Output = $outputPath
        CandidateOutput = ''
        StdoutPath = $stdoutPath
        StderrPath = $stderrPath
        Cached = $true
    }
}

$workProfiles = if ($Classes.Count -gt 0) { $Classes } else { $Profiles }
$pending = @(foreach ($map in $Maps) {
    foreach ($class in $workProfiles) {
        [pscustomobject]@{
            Map = $map
            Class = $class
        }
    }
})

$running = New-Object System.Collections.Generic.List[object]
$completed = New-Object System.Collections.Generic.List[object]
$nextIndex = 0

while ($nextIndex -lt $pending.Count -or $running.Count -gt 0) {
    while ($nextIndex -lt $pending.Count -and $running.Count -lt $MaxParallel) {
        $job = $pending[$nextIndex]
        $nextIndex += 1
        $cached = Try-ValidateExistingArtifact -Map $job.Map -Profile $job.Class
        if ($null -ne $cached) {
            $completed.Add($cached)
            Write-Host ("skipped {0}.{1} cached-canary-pass seconds={2}" -f $cached.Map, $cached.Class, $cached.DurationSeconds)
            continue
        }

        if ($AuditOnly) {
            $resolvedMap = Resolve-MapName $job.Map
            $safeName = "$resolvedMap.$($job.Class)"
            $finalOutputPath = Get-GraphArtifactPath -Map $resolvedMap -Profile $job.Class -AllowLegacyFallback
            $stdoutPath = Join-Path $logRoot "$safeName.out.log"
            $stderrPath = Join-Path $logRoot "$safeName.err.log"
            $reason = if (Test-Path -LiteralPath $finalOutputPath) { 'canary_failed_or_missing_canary' } else { 'missing_artifact' }
            $completed.Add([pscustomobject]@{
                Map = $resolvedMap
                Class = $job.Class
                ExitCode = 6
                CanaryPassed = $false
                Promoted = $false
                DurationSeconds = 0
                Output = $finalOutputPath
                CandidateOutput = ''
                StdoutPath = $stdoutPath
                StderrPath = $stderrPath
                Cached = $false
                Reason = $reason
            })
            Write-Host ("audit {0}.{1} fail reason={2}" -f $resolvedMap, $job.Class, $reason)
            continue
        }

        $started = Start-BakeProcess -Map $job.Map -Profile $job.Class
        $running.Add($started)
        Write-Host ("started {0}.{1} pid={2}" -f $started.Map, $started.Class, $started.Process.Id)
    }

    Start-Sleep -Milliseconds 500
    for ($index = $running.Count - 1; $index -ge 0; $index -= 1) {
        $job = $running[$index]
        if (-not $job.Process.HasExited) {
            continue
        }

        $job.Process.WaitForExit()
        $job.Process.Refresh()
        $exitCode = $job.Process.ExitCode
        if ($null -eq $exitCode) {
            $passedLog = Select-String -Path $job.StdoutPath -Pattern 'motion-proof graph-bake status=pass' -Quiet -ErrorAction SilentlyContinue
            $exitCode = if ($passedLog) { 0 } else { 3 }
        }

        $duration = (Get-Date) - $job.Started
        $canaryPassed = $true
        $promoted = $false
        if ($exitCode -eq 0) {
            $canaryPassed = Invoke-CanaryProofs -Job $job
            if (-not $canaryPassed) {
                $exitCode = 4
            } elseif (Test-Path -LiteralPath $job.Output) {
                Copy-Item -LiteralPath $job.Output -Destination $job.FinalOutput -Force
                Add-Content -Path $job.StdoutPath -Value ("motion-proof promote status=pass candidate={0} artifact={1}" -f $job.Output, $job.FinalOutput)
                $promoted = $true
            } else {
                Add-Content -Path $job.StdoutPath -Value ("motion-proof promote status=fail reason=missing_candidate candidate={0} artifact={1}" -f $job.Output, $job.FinalOutput)
                $exitCode = 5
            }
        }

        $result = [pscustomobject]@{
            Map = $job.Map
            Class = $job.Class
            ExitCode = $exitCode
            CanaryPassed = $canaryPassed
            Promoted = $promoted
            DurationSeconds = [math]::Round($duration.TotalSeconds, 1)
            Output = $job.FinalOutput
            CandidateOutput = $job.Output
            StdoutPath = $job.StdoutPath
            StderrPath = $job.StderrPath
        }
        $completed.Add($result)
        $running.RemoveAt($index)
        $status = if ($result.ExitCode -eq 0) { 'pass' } else { 'fail' }
        Write-Host ("finished {0}.{1} {2} exit={3} seconds={4}" -f $result.Map, $result.Class, $status, $result.ExitCode, $result.DurationSeconds)
    }
}

$summaryPath = Join-Path $logRoot 'matrix-summary.csv'
$completed | Sort-Object Map, Class | Export-Csv -NoTypeInformation -Path $summaryPath
$failed = @($completed | Where-Object { $_.ExitCode -ne 0 })
Write-Host ("matrix complete total={0} failed={1} summary={2}" -f $completed.Count, $failed.Count, $summaryPath)
if ($failed.Count -gt 0) {
    $failed | Sort-Object Map, Class | Format-Table -AutoSize
    exit 3
}
