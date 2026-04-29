param(
    [string[]]$Maps = @('TwodFortTwo', 'Corinth'),
    [ValidateSet('Blue', 'Red')]
    [string[]]$Teams = @('Blue', 'Red'),
    [string[]]$Classes = @(
        'Scout',
        'Engineer',
        'Pyro',
        'Soldier',
        'Demoman',
        'Heavy',
        'Sniper',
        'Medic',
        'Spy',
        'Quote'
    ),
    [int]$MaxParallel = 4,
    [int]$MaxExpanded = 100000,
    [int]$SearchBudgetMs = 30000,
    [string]$Configuration = 'Release',
    [switch]$ForceRebuild,
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$toolProject = Join-Path $repoRoot 'MotionProof.Tools\OpenGarrison.MotionProof.Tools.csproj'
$artifactRoot = Join-Path $repoRoot 'Core\Content\MotionProof'
$candidateRoot = Join-Path $repoRoot 'MotionProof.Tools\candidates'
$logRoot = Join-Path $repoRoot 'MotionProof.Tools\logs'
New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null
New-Item -ItemType Directory -Force -Path $candidateRoot | Out-Null
New-Item -ItemType Directory -Force -Path $logRoot | Out-Null

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

function Start-TapeBakeProcess {
    param(
        [string]$Map,
        [string]$Team,
        [string]$Class
    )

    $resolvedMap = Resolve-MapName $Map
    $safeName = "$resolvedMap.$Team.$Class"
    $finalOutputPath = Join-Path $artifactRoot "$safeName.json"
    $candidatePath = Join-Path $candidateRoot "$safeName.$([guid]::NewGuid().ToString('N')).json"
    $stdoutPath = Join-Path $logRoot "$safeName.tape.out.log"
    $stderrPath = Join-Path $logRoot "$safeName.tape.err.log"

    if (-not $ForceRebuild -and (Test-Path -LiteralPath $finalOutputPath)) {
        return [pscustomobject]@{
            Map = $resolvedMap
            Team = $Team
            Class = $Class
            Cached = $true
            ExitCode = 0
            Promoted = $false
            DurationSeconds = 0
            Output = $finalOutputPath
            CandidateOutput = ''
            StdoutPath = $stdoutPath
            StderrPath = $stderrPath
        }
    }

    $arguments = @(
        'run',
        '--project', $toolProject,
        '-c', $Configuration,
        '--no-build',
        '--',
        '--map', $resolvedMap,
        '--team', $Team,
        '--class', $Class,
        '--max-expanded', $MaxExpanded.ToString(),
        '--search-budget-ms', $SearchBudgetMs.ToString(),
        '--output', $candidatePath
    )

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
        Team = $Team
        Class = $Class
        Cached = $false
        Process = $process
        Output = $candidatePath
        FinalOutput = $finalOutputPath
        StdoutPath = $stdoutPath
        StderrPath = $stderrPath
        Started = Get-Date
    }
}

$pending = @(foreach ($map in $Maps) {
    foreach ($team in $Teams) {
        foreach ($class in $Classes) {
            [pscustomobject]@{
                Map = $map
                Team = $team
                Class = $class
            }
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
        $started = Start-TapeBakeProcess -Map $job.Map -Team $job.Team -Class $job.Class
        if ($started.Cached) {
            $completed.Add($started)
            Write-Host ("skipped {0}.{1}.{2} cached" -f $started.Map, $started.Team, $started.Class)
            continue
        }

        $running.Add($started)
        Write-Host ("started {0}.{1}.{2} pid={3}" -f $started.Map, $started.Team, $started.Class, $started.Process.Id)
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
            $passedLog = Select-String -Path $job.StdoutPath -Pattern 'motion-proof (result|koth result) status=pass' -Quiet -ErrorAction SilentlyContinue
            $exitCode = if ($passedLog) { 0 } else { 3 }
        }
        $duration = (Get-Date) - $job.Started
        $promoted = $false
        if ($exitCode -eq 0 -and (Test-Path -LiteralPath $job.Output)) {
            Copy-Item -LiteralPath $job.Output -Destination $job.FinalOutput -Force
            Add-Content -Path $job.StdoutPath -Value ("motion-proof tape-promote status=pass candidate={0} artifact={1}" -f $job.Output, $job.FinalOutput)
            $promoted = $true
        } elseif ($exitCode -eq 0) {
            $exitCode = 5
            Add-Content -Path $job.StdoutPath -Value ("motion-proof tape-promote status=fail reason=missing_candidate candidate={0} artifact={1}" -f $job.Output, $job.FinalOutput)
        }

        $result = [pscustomobject]@{
            Map = $job.Map
            Team = $job.Team
            Class = $job.Class
            ExitCode = $exitCode
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
        Write-Host ("finished {0}.{1}.{2} {3} exit={4} seconds={5}" -f $result.Map, $result.Team, $result.Class, $status, $result.ExitCode, $result.DurationSeconds)
    }
}

$summaryPath = Join-Path $logRoot 'tape-summary.csv'
$completed | Sort-Object Map, Team, Class | Export-Csv -NoTypeInformation -Path $summaryPath
$failed = @($completed | Where-Object { $_.ExitCode -ne 0 })
Write-Host ("tape bake complete total={0} failed={1} summary={2}" -f $completed.Count, $failed.Count, $summaryPath)
if ($failed.Count -gt 0) {
    $failed | Sort-Object Map, Team, Class | Format-Table -AutoSize
    exit 3
}
