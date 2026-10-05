[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$InputPath,
    [string]$ReportPath = "",
    [ValidateRange(1, 100)]
    [int]$WorstFrameCount = 12
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$invariantCulture = [System.Globalization.CultureInfo]::InvariantCulture
$numberStyle = [System.Globalization.NumberStyles]::Float
$stageColumns = @(
    "stage_update_ms",
    "stage_simulation_ms",
    "stage_presentation_ms",
    "stage_world_draw_ms",
    "stage_hud_draw_ms",
    "stage_modal_draw_ms",
    "stage_plugin_frame_ms",
    "stage_plugin_events_ms",
    "stage_interpolation_ms",
    "stage_render_states_ms",
    "stage_music_ms",
    "stage_bot_build_ms",
    "stage_bot_apply_ms",
    "stage_network_receive_ms",
    "stage_network_resolve_ms",
    "stage_network_apply_ms"
)

function Get-CsvField {
    param(
        [Parameter(Mandatory = $true)]$Row,
        [Parameter(Mandatory = $true)][string]$Name
    )

    $property = $Row.PSObject.Properties[$Name]
    if ($null -eq $property -or $null -eq $property.Value) {
        return ""
    }
    return [string]$property.Value
}

function Get-JsonField {
    param(
        [Parameter(Mandatory = $true)]$Object,
        [Parameter(Mandatory = $true)][string]$Name
    )

    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) { return $null }
    return $property.Value
}

function ConvertTo-NumberOrZero {
    param([AllowNull()][string]$Text)

    if ([string]::IsNullOrWhiteSpace($Text)) {
        return 0.0
    }
    $value = 0.0
    if ([double]::TryParse($Text, $numberStyle, $invariantCulture, [ref]$value) -and
        -not [double]::IsNaN($value) -and -not [double]::IsInfinity($value)) {
        return $value
    }
    return 0.0
}

function ConvertTo-BooleanOrFalse {
    param([AllowNull()][string]$Text)

    if ([string]::IsNullOrWhiteSpace($Text)) { return $false }
    switch ($Text.Trim().ToLowerInvariant()) {
        { $_ -in @("true", "1", "yes") } { return $true }
        default { return $false }
    }
}

function Format-Value {
    param(
        [AllowNull()][object]$Value,
        [string]$Format = "0.##"
    )

    if ($null -eq $Value) {
        return "n/a"
    }
    $numericValue = [double]$Value
    if ([double]::IsNaN($numericValue) -or [double]::IsInfinity($numericValue)) {
        return "n/a"
    }
    return $numericValue.ToString($Format, $invariantCulture)
}

function Format-MarkdownCell {
    param([AllowNull()][string]$Value)
    if ([string]::IsNullOrWhiteSpace($Value)) { return "—" }
    return ([regex]::Replace($Value, "[|\r\n]+", " ")).Trim()
}

function Get-Percentile {
    param(
        [Parameter(Mandatory = $true)][double[]]$SortedValues,
        [Parameter(Mandatory = $true)][double]$Percentile
    )

    if ($SortedValues.Length -eq 0) { return $null }
    if ($SortedValues.Length -eq 1) { return $SortedValues[0] }
    $position = ($SortedValues.Length - 1) * $Percentile
    $lowerIndex = [int][Math]::Floor($position)
    $upperIndex = [int][Math]::Ceiling($position)
    if ($lowerIndex -eq $upperIndex) { return $SortedValues[$lowerIndex] }
    $weight = $position - $lowerIndex
    return $SortedValues[$lowerIndex] + (($SortedValues[$upperIndex] - $SortedValues[$lowerIndex]) * $weight)
}

function Get-BucketMetrics {
    param([Parameter(Mandatory = $true)][AllowEmptyCollection()][object[]]$Rows)

    $intervals = [System.Collections.Generic.List[double]]::new()
    $totalMilliseconds = 0.0
    foreach ($row in $Rows) {
        if ($row.IntervalMs -gt 0) {
            $intervals.Add([double]$row.IntervalMs)
            $totalMilliseconds += [double]$row.IntervalMs
        }
    }
    $values = $intervals.ToArray()
    [Array]::Sort($values)
    if ($values.Length -eq 0) {
        return [PSCustomObject]@{
            Count = 0; MeanMs = $null; P5Ms = $null; P50Ms = $null; P95Ms = $null; P99Ms = $null
            MaxMs = $null; MeanFps = $null; FpsP5 = $null; FpsP50 = $null; FpsP95 = $null
            Hitch25 = 0; Hitch33 = 0; Hitch50 = 0; Hitch25Percent = 0.0; Hitch33Percent = 0.0; Hitch50Percent = 0.0
        }
    }

    $count = $values.Length
    $mean = $totalMilliseconds / $count
    $p5 = Get-Percentile -SortedValues $values -Percentile 0.05
    $p50 = Get-Percentile -SortedValues $values -Percentile 0.50
    $p95 = Get-Percentile -SortedValues $values -Percentile 0.95
    $p99 = Get-Percentile -SortedValues $values -Percentile 0.99
    $hitch25 = @($Rows | Where-Object { $_.IntervalMs -ge 25.0 }).Count
    $hitch33 = @($Rows | Where-Object { $_.IntervalMs -ge 33.34 }).Count
    $hitch50 = @($Rows | Where-Object { $_.IntervalMs -ge 50.0 }).Count
    return [PSCustomObject]@{
        Count = $count
        MeanMs = $mean
        P5Ms = $p5
        P50Ms = $p50
        P95Ms = $p95
        P99Ms = $p99
        MaxMs = $values[$values.Length - 1]
        MeanFps = if ($mean -gt 0) { 1000.0 / $mean } else { $null }
        FpsP5 = if ($p95 -gt 0) { 1000.0 / $p95 } else { $null }
        FpsP50 = if ($p50 -gt 0) { 1000.0 / $p50 } else { $null }
        FpsP95 = if ($p5 -gt 0) { 1000.0 / $p5 } else { $null }
        Hitch25 = $hitch25
        Hitch33 = $hitch33
        Hitch50 = $hitch50
        Hitch25Percent = 100.0 * $hitch25 / $count
        Hitch33Percent = 100.0 * $hitch33 / $count
        Hitch50Percent = 100.0 * $hitch50 / $count
    }
}

function Add-BucketSummary {
    param(
        [Parameter(Mandatory = $true)][AllowEmptyString()][AllowEmptyCollection()][System.Collections.Generic.List[string]]$Lines,
        [Parameter(Mandatory = $true)][string]$Title,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][object[]]$Rows
    )

    $metrics = Get-BucketMetrics -Rows $Rows
    [void]$Lines.Add("### $Title")
    if ($Rows.Length -eq 0 -or $metrics.Count -eq 0) {
        [void]$Lines.Add("No usable frame intervals.")
        [void]$Lines.Add("")
        return
    }

    $vsyncValues = @($Rows | ForEach-Object { $_.VSyncText } | Where-Object { $_ -ne "" } | Sort-Object -Unique)
    $capValues = @($Rows | ForEach-Object { $_.FrameRateLimitText } | Where-Object { $_ -ne "" } | Sort-Object -Unique)
    $viewports = @($Rows | ForEach-Object { $_.Viewport } | Where-Object { $_ -ne "" } | Sort-Object -Unique)
    $sessions = @($Rows | ForEach-Object { $_.SessionKind } | Where-Object { $_ -ne "" } | Sort-Object -Unique)
    [void]$Lines.Add("Frames with usable intervals: **$($metrics.Count)**")
    [void]$Lines.Add("Frame interval: mean **$(Format-Value $metrics.MeanMs) ms**, p50 **$(Format-Value $metrics.P50Ms) ms**, p95 **$(Format-Value $metrics.P95Ms) ms**, p99 **$(Format-Value $metrics.P99Ms) ms**, max **$(Format-Value $metrics.MaxMs) ms**.")
    [void]$Lines.Add("Equivalent FPS: mean **$(Format-Value $metrics.MeanFps)**, p5 **$(Format-Value $metrics.FpsP5)**, p50 **$(Format-Value $metrics.FpsP50)**, p95 **$(Format-Value $metrics.FpsP95)**. FPS percentiles are the inverse of frame-interval percentiles.")
    [void]$Lines.Add(("Hitches: >=25 ms **{0} ({1}%)**, >=33.34 ms **{2} ({3}%)**, >=50 ms **{4} ({5}%)**." -f $metrics.Hitch25, (Format-Value $metrics.Hitch25Percent), $metrics.Hitch33, (Format-Value $metrics.Hitch33Percent), $metrics.Hitch50, (Format-Value $metrics.Hitch50Percent)))
    [void]$Lines.Add("Recorded VSync values: $(if ($vsyncValues.Length -gt 0) { $vsyncValues -join ", " } else { "unavailable" }); frame-rate limits: $(if ($capValues.Length -gt 0) { $capValues -join ", " } else { "unavailable" }); viewports: $(if ($viewports.Length -gt 0) { $viewports -join ", " } else { "unavailable" }).")
    if ($sessions.Length -gt 0) {
        [void]$Lines.Add("Session kinds: $($sessions -join ", ").")
    }
    [void]$Lines.Add("")
}

function Add-SummaryBucket {
    param(
        [Parameter(Mandatory = $true)][AllowEmptyString()][AllowEmptyCollection()][System.Collections.Generic.List[string]]$Lines,
        [Parameter(Mandatory = $true)][string]$Title,
        [Parameter(Mandatory = $true)]$Summary
    )

    $sampleCount = [long](Get-JsonField $Summary "sample_count")
    [void]$Lines.Add("### $Title")
    if ($sampleCount -le 0) {
        [void]$Lines.Add("No qualifying frame intervals were recorded.")
        [void]$Lines.Add("")
        return
    }

    $totalElapsedMs = Get-JsonField $Summary "total_elapsed_ms"
    $averageFps = Get-JsonField $Summary "average_fps"
    $p50Ms = Get-JsonField $Summary "p50_ms"
    $p95Ms = Get-JsonField $Summary "p95_ms"
    $p99Ms = Get-JsonField $Summary "p99_ms"
    $maxMs = Get-JsonField $Summary "max_ms"
    $hitches25 = [long](Get-JsonField $Summary "frames_25ms_or_more")
    $hitches33 = [long](Get-JsonField $Summary "frames_33_34ms_or_more")
    $hitches50 = [long](Get-JsonField $Summary "frames_50ms_or_more")
    $fpsP5 = if ($null -ne $p95Ms -and [double]$p95Ms -gt 0) { 1000.0 / [double]$p95Ms } else { $null }
    $fpsP50 = if ($null -ne $p50Ms -and [double]$p50Ms -gt 0) { 1000.0 / [double]$p50Ms } else { $null }
    $averageFrameMs = if ($null -ne $averageFps -and [double]$averageFps -gt 0) { 1000.0 / [double]$averageFps } else { $null }
    [void]$Lines.Add("Whole-session intervals: **$sampleCount** over **$(Format-Value $totalElapsedMs) ms** of measured gameplay time.")
    [void]$Lines.Add("Frame interval: average **$(Format-Value $averageFrameMs) ms**, p50 **$(Format-Value $p50Ms) ms**, p95 **$(Format-Value $p95Ms) ms**, p99 **$(Format-Value $p99Ms) ms**, max **$(Format-Value $maxMs) ms**.")
    [void]$Lines.Add("FPS: average **$(Format-Value $averageFps)**, equivalent p5 **$(Format-Value $fpsP5)**, equivalent p50 **$(Format-Value $fpsP50)**; p95 FPS is unavailable because the whole-session summary does not store p5 frame time.")
    [void]$Lines.Add(("Hitches: >=25 ms **{0} ({1}%)**, >=33.34 ms **{2} ({3}%)**, >=50 ms **{4} ({5}%)**." -f $hitches25, (Format-Value (100.0 * $hitches25 / $sampleCount)), $hitches33, (Format-Value (100.0 * $hitches33 / $sampleCount)), $hitches50, (Format-Value (100.0 * $hitches50 / $sampleCount))))
    [void]$Lines.Add("")
}

function Get-PearsonCorrelation {
    param(
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][double[]]$XValues,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][double[]]$YValues
    )

    if ($XValues.Length -ne $YValues.Length -or $XValues.Length -lt 2) { return $null }
    $meanX = ($XValues | Measure-Object -Average).Average
    $meanY = ($YValues | Measure-Object -Average).Average
    $covariance = 0.0
    $varianceX = 0.0
    $varianceY = 0.0
    for ($index = 0; $index -lt $XValues.Length; $index += 1) {
        $differenceX = $XValues[$index] - $meanX
        $differenceY = $YValues[$index] - $meanY
        $covariance += $differenceX * $differenceY
        $varianceX += $differenceX * $differenceX
        $varianceY += $differenceY * $differenceY
    }
    $denominator = [Math]::Sqrt($varianceX * $varianceY)
    if ($denominator -le 0) { return $null }
    return $covariance / $denominator
}

function Add-GcAllocationAnalysis {
    param(
        [Parameter(Mandatory = $true)][AllowEmptyString()][AllowEmptyCollection()][System.Collections.Generic.List[string]]$Lines,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][object[]]$Rows
    )

    [void]$Lines.Add("## Allocation and GC association")
    if ($Rows.Length -eq 0) {
        [void]$Lines.Add("No active gameplay frames are available for comparison.")
        [void]$Lines.Add("")
        return
    }
    $gcFrames = @($Rows | Where-Object { ($_.Gc0 + $_.Gc1 + $_.Gc2) -gt 0 })
    $nonGcFrames = @($Rows | Where-Object { ($_.Gc0 + $_.Gc1 + $_.Gc2) -le 0 })
    $hitchFrames = @($Rows | Where-Object { $_.IntervalMs -ge 25.0 })
    $nonHitchFrames = @($Rows | Where-Object { $_.IntervalMs -lt 25.0 })
    $totalAllocated = ($Rows | Measure-Object -Property AllocatedBytes -Sum).Sum
    $meanAllocated = ($Rows | Measure-Object -Property AllocatedBytes -Average).Average
    $meanGc = ($Rows | ForEach-Object { $_.Gc0 + $_.Gc1 + $_.Gc2 } | Measure-Object -Average).Average
    $intervalValues = [double[]]@($Rows | ForEach-Object { $_.IntervalMs })
    $allocationValues = [double[]]@($Rows | ForEach-Object { $_.AllocatedBytes })
    $gcEventValues = [double[]]@($Rows | ForEach-Object { [double]([int]($_.Gc0 + $_.Gc1 + $_.Gc2 -gt 0)) })
    $gcCounts = @(
        ($Rows | Measure-Object -Property Gc0 -Sum).Sum,
        ($Rows | Measure-Object -Property Gc1 -Sum).Sum,
        ($Rows | Measure-Object -Property Gc2 -Sum).Sum
    )
    $gcMean = if ($gcFrames.Length -gt 0) { (Get-BucketMetrics -Rows $gcFrames).MeanMs } else { $null }
    $nonGcMean = if ($nonGcFrames.Length -gt 0) { (Get-BucketMetrics -Rows $nonGcFrames).MeanMs } else { $null }
    $hitchAllocMean = if ($hitchFrames.Length -gt 0) { ($hitchFrames | Measure-Object -Property AllocatedBytes -Average).Average } else { $null }
    $nonHitchAllocMean = if ($nonHitchFrames.Length -gt 0) { ($nonHitchFrames | Measure-Object -Property AllocatedBytes -Average).Average } else { $null }
    $allocationCorrelation = Get-PearsonCorrelation -XValues $intervalValues -YValues $allocationValues
    $gcCorrelation = Get-PearsonCorrelation -XValues $intervalValues -YValues $gcEventValues

    [void]$Lines.Add(("Active gameplay frames: {0}; allocations total **{1} bytes**, mean **{2} bytes/frame**; frames with any GC collection **{3}**; generation deltas (0/1/2): **{4}/{5}/{6}**." -f $Rows.Length, (Format-Value $totalAllocated "0"), (Format-Value $meanAllocated "0.##"), $gcFrames.Length, (Format-Value $gcCounts[0] "0"), (Format-Value $gcCounts[1] "0"), (Format-Value $gcCounts[2] "0")))
    [void]$Lines.Add(("Mean interval on GC frames: **{0} ms** ({1} frames); on other frames: **{2} ms** ({3} frames)." -f (Format-Value $gcMean), $gcFrames.Length, (Format-Value $nonGcMean), $nonGcFrames.Length))
    [void]$Lines.Add(("Mean allocation on >=25 ms hitch frames: **{0} bytes** ({1} frames); on shorter frames: **{2} bytes** ({3} frames)." -f (Format-Value $hitchAllocMean "0.##"), $hitchFrames.Length, (Format-Value $nonHitchAllocMean "0.##"), $nonHitchFrames.Length))
    [void]$Lines.Add("Pearson correlation with frame interval: allocation bytes **$(Format-Value $allocationCorrelation "0.###")**, frames containing a GC event **$(Format-Value $gcCorrelation "0.###")**. These are associations within this capture; they do not show that allocation or GC caused a hitch.")
    [void]$Lines.Add("")
}

function Add-StageSummary {
    param(
        [Parameter(Mandatory = $true)][AllowEmptyString()][AllowEmptyCollection()][System.Collections.Generic.List[string]]$Lines,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][object[]]$Rows,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][string[]]$Columns
    )

    [void]$Lines.Add("## Stage timings during active gameplay")
    if ($Rows.Length -eq 0) {
        [void]$Lines.Add("No active gameplay frames are available.")
        [void]$Lines.Add("")
        return
    }
    [void]$Lines.Add('Each stage duration accumulates work since the previous completed draw. Stage values can overlap; do not add them together. `stage_draw_ms` is omitted because it duplicates `draw_cpu_ms`.')
    [void]$Lines.Add("")
    [void]$Lines.Add("| Stage | Mean ms/frame | p95 ms/frame | Max ms/frame | Frames with time |")
    [void]$Lines.Add("| --- | ---: | ---: | ---: | ---: |")
    foreach ($column in $Columns) {
        $stageName = $column -replace "^stage_", "" -replace "_ms$", ""
        $values = [System.Collections.Generic.List[double]]::new()
        foreach ($row in $Rows) {
            if ($row.Stages.ContainsKey($column)) {
                $values.Add([double]$row.Stages[$column])
            }
        }
        if ($values.Count -eq 0) { continue }
        $sortedValues = $values.ToArray()
        [Array]::Sort($sortedValues)
        $mean = ($sortedValues | Measure-Object -Average).Average
        $p95 = Get-Percentile -SortedValues $sortedValues -Percentile 0.95
        $max = $sortedValues[$sortedValues.Length - 1]
        $withTime = @($sortedValues | Where-Object { $_ -gt 0 }).Count
        [void]$Lines.Add(("| {0} | {1} | {2} | {3} | {4} |" -f $stageName, (Format-Value $mean), (Format-Value $p95), (Format-Value $max), $withTime))
    }
    [void]$Lines.Add("")
}

if (-not (Test-Path -LiteralPath $InputPath -PathType Leaf)) {
    throw "Capture CSV not found: $InputPath"
}
$resolvedInputPath = (Resolve-Path -LiteralPath $InputPath).Path
$rawRows = @(Import-Csv -LiteralPath $resolvedInputPath)
if ($rawRows.Length -eq 0) {
    throw "Capture CSV has no frame rows. If the game did not exit normally, its ring buffer may not have been flushed: $resolvedInputPath"
}

$availableColumns = @($rawRows[0].PSObject.Properties | ForEach-Object { $_.Name })
$frameworkEndDrawColumn = "previous_framework_end_draw_ms"
$hasFrameworkEndDrawColumn = $frameworkEndDrawColumn -in $availableColumns
$requiredColumns = @("frame_index", "draw_interval_ms", "draw_cpu_ms", "interval_gap_ms", "gameplay_active", "loading", "window_active")
$missingColumns = @($requiredColumns | Where-Object { $_ -notin $availableColumns })
if ($missingColumns.Length -gt 0) {
    throw "Capture CSV is missing required columns: $($missingColumns -join ", ")"
}
$presentStageColumns = @($stageColumns | Where-Object { $_ -in $availableColumns })
$summaryPath = [System.IO.Path]::Combine((Split-Path -Parent $resolvedInputPath), ([System.IO.Path]::GetFileNameWithoutExtension($resolvedInputPath) + ".summary.json"))
$captureSummary = $null
$summaryReadError = ""
if (Test-Path -LiteralPath $summaryPath -PathType Leaf) {
    try {
        $captureSummary = Get-Content -LiteralPath $summaryPath -Raw | ConvertFrom-Json -Depth 16
        if ($null -eq (Get-JsonField $captureSummary "active_gameplay") -or
            $null -eq (Get-JsonField $captureSummary "all_frames")) {
            $summaryReadError = "Summary JSON lacks active_gameplay or all_frames metrics."
            $captureSummary = $null
        }
    } catch {
        $summaryReadError = "Could not read summary JSON: $($_.Exception.Message)"
        $captureSummary = $null
    }
}

$samples = [System.Collections.Generic.List[object]]::new()
foreach ($rawRow in $rawRows) {
    $stages = @{}
    foreach ($column in $presentStageColumns) {
        $stages[$column] = ConvertTo-NumberOrZero (Get-CsvField $rawRow $column)
    }
    $viewportWidth = Get-CsvField $rawRow "viewport_width"
    $viewportHeight = Get-CsvField $rawRow "viewport_height"
    $viewport = if ($viewportWidth -ne "" -and $viewportHeight -ne "") { "${viewportWidth}x${viewportHeight}" } else { "" }
    $gc0 = ConvertTo-NumberOrZero (Get-CsvField $rawRow "gc0_delta")
    $gc1 = ConvertTo-NumberOrZero (Get-CsvField $rawRow "gc1_delta")
    $gc2 = ConvertTo-NumberOrZero (Get-CsvField $rawRow "gc2_delta")
    $intervalMs = ConvertTo-NumberOrZero (Get-CsvField $rawRow "draw_interval_ms")
    $intervalGapMs = ConvertTo-NumberOrZero (Get-CsvField $rawRow "interval_gap_ms")
    $frameworkEndDrawMs = $null
    $intervalGapAfterEndDrawMs = $null
    if ($hasFrameworkEndDrawColumn) {
        $frameworkEndDrawMs = ConvertTo-NumberOrZero (Get-CsvField $rawRow $frameworkEndDrawColumn)
        $intervalGapAfterEndDrawMs = $intervalGapMs - $frameworkEndDrawMs
    }
    $samples.Add([PSCustomObject]@{
        FrameIndex = Get-CsvField $rawRow "frame_index"
        CompletedUtcTicks = Get-CsvField $rawRow "completed_utc_ticks"
        IntervalMs = $intervalMs
        DrawCpuMs = ConvertTo-NumberOrZero (Get-CsvField $rawRow "draw_cpu_ms")
        IntervalGapMs = $intervalGapMs
        PreviousFrameworkEndDrawMs = $frameworkEndDrawMs
        IntervalGapAfterEndDrawMs = $intervalGapAfterEndDrawMs
        SessionKind = Get-CsvField $rawRow "session_kind"
        Map = Get-CsvField $rawRow "map"
        GameplayActive = ConvertTo-BooleanOrFalse (Get-CsvField $rawRow "gameplay_active")
        Loading = ConvertTo-BooleanOrFalse (Get-CsvField $rawRow "loading")
        WindowActive = ConvertTo-BooleanOrFalse (Get-CsvField $rawRow "window_active")
        LocalPlayerAwaitingJoin = ConvertTo-BooleanOrFalse (Get-CsvField $rawRow "local_player_awaiting_join")
        BotCount = ConvertTo-NumberOrZero (Get-CsvField $rawRow "practice_bot_count")
        EntityCount = ConvertTo-NumberOrZero (Get-CsvField $rawRow "entity_count")
        Viewport = $viewport
        VSyncText = Get-CsvField $rawRow "vsync"
        FrameRateLimitText = Get-CsvField $rawRow "frame_rate_limit"
        CaptureRegion = Get-CsvField $rawRow "capture_region"
        AllocatedBytes = ConvertTo-NumberOrZero (Get-CsvField $rawRow "allocated_bytes_delta")
        Gc0 = $gc0
        Gc1 = $gc1
        Gc2 = $gc2
        DroppedFrames = ConvertTo-NumberOrZero (Get-CsvField $rawRow "dropped_frames")
        Stages = $stages
    })
}
$allSamples = $samples.ToArray()
$frameIndexValues = @($allSamples | ForEach-Object { [long](ConvertTo-NumberOrZero $_.FrameIndex) })
$utcTickValues = @($allSamples | ForEach-Object {
    $tickValue = 0L
    if ([long]::TryParse($_.CompletedUtcTicks, [System.Globalization.NumberStyles]::Integer, $invariantCulture, [ref]$tickValue)) {
        $tickValue
    }
})
$firstFrameIndex = if ($frameIndexValues.Length -gt 0) { ($frameIndexValues | Measure-Object -Minimum).Minimum } else { "n/a" }
$lastFrameIndex = if ($frameIndexValues.Length -gt 0) { ($frameIndexValues | Measure-Object -Maximum).Maximum } else { "n/a" }
$retainedSpanSeconds = $null
if ($utcTickValues.Length -gt 1) {
    $retainedSpanSeconds = (($utcTickValues | Measure-Object -Maximum).Maximum - ($utcTickValues | Measure-Object -Minimum).Minimum) / 10000000.0
}
$overwrittenRows = if ($allSamples.Length -gt 0) { ($allSamples | Measure-Object -Property DroppedFrames -Maximum).Maximum } else { 0 }
$activeGameplay = @($allSamples | Where-Object { $_.GameplayActive -and -not $_.Loading -and $_.WindowActive -and -not $_.LocalPlayerAwaitingJoin })
$loadingSamples = @($allSamples | Where-Object { $_.Loading })
$inactiveWindowSamples = @($allSamples | Where-Object { -not $_.WindowActive })
$otherSamples = @($allSamples | Where-Object { -not $_.GameplayActive -and -not $_.Loading })
$recentActiveGameplay = @($activeGameplay | Where-Object { $_.CaptureRegion -match "(^|\+)recent($|\+)" })

$lines = [System.Collections.Generic.List[string]]::new()
[void]$lines.Add("# Manual performance capture analysis")
[void]$lines.Add("")
[void]$lines.Add(('Source: `{0}`' -f $resolvedInputPath))
[void]$lines.Add(("CSV coverage: merged rows include frame indexes **{0}–{1}**, about **{2} seconds** of completed-draw timestamps. This CSV contains the recent ring plus archived slow frames and is selection-biased; do not use it alone for whole-session percentiles or hitch rates." -f $firstFrameIndex, $lastFrameIndex, (Format-Value $retainedSpanSeconds)))
[void]$lines.Add(("Captured frame rows: **{0}**. The primary gameplay bucket requires gameplay_active=true, loading=false, window_active=true, and local_player_awaiting_join=false; other CSV sections keep loading and inactive-window rows visible." -f $allSamples.Length))
if ($null -ne $captureSummary) {
    $totalFrames = Get-JsonField $captureSummary "total_frame_count"
    $firstSummaryIndex = Get-JsonField $captureSummary "first_frame_index"
    $lastSummaryIndex = Get-JsonField $captureSummary "last_frame_index"
    $elapsedDrawMs = Get-JsonField $captureSummary "total_elapsed_ms"
    $recentCount = Get-JsonField $captureSummary "retained_recent_count"
    $overwrittenCount = Get-JsonField $captureSummary "recent_overwritten_count"
    $hitchRetained = Get-JsonField $captureSummary "retained_hitch_count"
    $hitchCapacity = Get-JsonField $captureSummary "hitch_archive_capacity"
    $hitchOmitted = Get-JsonField $captureSummary "hitch_archive_omitted_count"
    $mergedCount = Get-JsonField $captureSummary "merged_csv_row_count"
    $duplicateCount = Get-JsonField $captureSummary "merged_duplicate_count"
    $archiveThreshold = Get-JsonField $captureSummary "hitch_archive_threshold_ms"
    [void]$lines.Add(("Whole-capture summary: **{0} completed draws** (frame {1}–{2}); **{3} ms** of completed-draw intervals. The CSV includes {4} recent frames and {5} archived hitch frames; the recent ring overwrote {6} older rows, and the hitch archive omitted {7} qualifying frames after its capacity of {8} (threshold {9} ms). Merged rows: {10}; duplicate rows removed: {11}." -f $totalFrames, $firstSummaryIndex, $lastSummaryIndex, (Format-Value $elapsedDrawMs), $recentCount, $hitchRetained, $overwrittenCount, $hitchOmitted, $hitchCapacity, (Format-Value $archiveThreshold), $mergedCount, $duplicateCount))
} else {
    [void]$lines.Add(("Whole-session summary unavailable. Results use the merged CSV only and can be selection-biased; its largest dropped_frames value is {0} overwritten recent-ring rows, not display/GPU drops." -f (Format-Value $overwrittenRows "0")))
    if ($summaryReadError -ne "") { [void]$lines.Add("Summary sidecar status: $summaryReadError") }
}
[void]$lines.Add("")
[void]$lines.Add("## Frame pacing")
[void]$lines.Add("")
if ($null -ne $captureSummary) {
    Add-SummaryBucket -Lines $lines -Title "Active gameplay (whole-session endpoint-filtered intervals)" -Summary (Get-JsonField $captureSummary "active_gameplay")
    Add-SummaryBucket -Lines $lines -Title "All completed-draw intervals (whole session)" -Summary (Get-JsonField $captureSummary "all_frames")
} else {
    Add-BucketSummary -Lines $lines -Title "Active gameplay (CSV-only fallback; selection-biased)" -Rows $activeGameplay
}
Add-BucketSummary -Lines $lines -Title "CSV merged rows (selection-biased subset)" -Rows $allSamples
Add-BucketSummary -Lines $lines -Title "Loading overlay visible (CSV rows only; overlay visibility does not cover every loading transition)" -Rows $loadingSamples
Add-BucketSummary -Lines $lines -Title "Window inactive (CSV rows only; includes menus, selections, and warmup)" -Rows $inactiveWindowSamples
Add-BucketSummary -Lines $lines -Title "Menu or other non-gameplay frames (CSV rows only)" -Rows $otherSamples

Add-StageSummary -Lines $lines -Rows $activeGameplay -Columns $presentStageColumns
Add-GcAllocationAnalysis -Lines $lines -Rows $activeGameplay

[void]$lines.Add("## Worst active-gameplay frames")
if ($activeGameplay.Length -eq 0) {
    [void]$lines.Add("No active gameplay frames were recorded.")
} else {
    [void]$lines.Add('Stage figures are individual measurements and overlap; the stage column is not a sum. `interval_gap_ms` is a signed coarse remainder after measured update and draw work, not a GPU timing. Flags describe state at draw completion, while stage work accumulates from the previous completed draw to the current one, so transition rows can mix contexts. Allocations and GC deltas are process-wide across threads.')
    [void]$lines.Add("")
    [void]$lines.Add("| Frame | Source region | Interval ms | Draw CPU ms | Coarse gap ms | GC 0/1/2 | Allocated bytes | Bots | Entities | Map | Largest measured stages |")
    [void]$lines.Add("| ---: | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- | --- |")
    $worstFrames = @($activeGameplay | Sort-Object -Property IntervalMs -Descending | Select-Object -First $WorstFrameCount)
    foreach ($frame in $worstFrames) {
        $stageValues = @()
        foreach ($column in $presentStageColumns) {
            $value = [double]$frame.Stages[$column]
            if ($value -gt 0) {
                $stageValues += [PSCustomObject]@{ Name = ($column -replace "^stage_", "" -replace "_ms$", ""); Value = $value }
            }
        }
        $topStages = @($stageValues | Sort-Object -Property Value -Descending | Select-Object -First 4)
        $stageText = if ($topStages.Length -gt 0) {
            ($topStages | ForEach-Object { "{0} {1} ms" -f $_.Name, (Format-Value $_.Value) }) -join "; "
        } else {
            "no measured stage time"
        }
        $gcText = "{0}/{1}/{2}" -f (Format-Value $frame.Gc0 "0"), (Format-Value $frame.Gc1 "0"), (Format-Value $frame.Gc2 "0")
        [void]$lines.Add(("| {0} | {1} | {2} | {3} | {4} | {5} | {6} | {7} | {8} | {9} | {10} |" -f (Format-MarkdownCell $frame.FrameIndex), (Format-MarkdownCell $frame.CaptureRegion), (Format-Value $frame.IntervalMs), (Format-Value $frame.DrawCpuMs), (Format-Value $frame.IntervalGapMs), $gcText, (Format-Value $frame.AllocatedBytes "0"), (Format-Value $frame.BotCount "0"), (Format-Value $frame.EntityCount "0"), (Format-MarkdownCell $frame.Map), (Format-MarkdownCell $stageText)))
    }
}
[void]$lines.Add("")

[void]$lines.Add("## Framework EndDraw timing")
if (-not $hasFrameworkEndDrawColumn) {
    [void]$lines.Add('This CSV predates `previous_framework_end_draw_ms`; the analyzer remains compatible, but it cannot split framework EndDraw wall time from the coarse interval gap.')
} else {
    [void]$lines.Add('`previous_framework_end_draw_ms` is wall time on the game thread inside framework `EndDraw`, after the previous completed draw. The remaining gap is `interval_gap_ms - previous_framework_end_draw_ms`; these fields separate measured framework call time from the remainder without changing the interval or summary statistics.')
    $endDrawRows = $recentActiveGameplay
    $endDrawRowsLabel = "active gameplay rows from the recent ring"
    if ($endDrawRows.Length -eq 0) {
        $endDrawRows = $activeGameplay
        $endDrawRowsLabel = "active gameplay CSV rows (recent-ring rows could not be identified)"
    }
    $endDrawExamples = @($endDrawRows | Where-Object { $_.IntervalMs -ge 25.0 } | Sort-Object -Property IntervalMs -Descending | Select-Object -First $WorstFrameCount)
    [void]$lines.Add("")
    [void]$lines.Add("Recent hitch examples ($endDrawRowsLabel). Values are CPU wall-time accounting, not GPU execution measurements.")
    if ($endDrawExamples.Length -eq 0) {
        [void]$lines.Add("No >=25 ms active-gameplay intervals were available in these rows.")
    } else {
        [void]$lines.Add("")
        [void]$lines.Add("| Frame | Source region | Interval ms | Draw CPU ms | Coarse gap ms | Previous EndDraw ms | Gap after EndDraw ms |")
        [void]$lines.Add("| ---: | --- | ---: | ---: | ---: | ---: | ---: |")
        foreach ($frame in $endDrawExamples) {
            [void]$lines.Add(("| {0} | {1} | {2} | {3} | {4} | {5} | {6} |" -f (Format-MarkdownCell $frame.FrameIndex), (Format-MarkdownCell $frame.CaptureRegion), (Format-Value $frame.IntervalMs), (Format-Value $frame.DrawCpuMs), (Format-Value $frame.IntervalGapMs), (Format-Value $frame.PreviousFrameworkEndDrawMs), (Format-Value $frame.IntervalGapAfterEndDrawMs)))
        }
    }
}
[void]$lines.Add("")
[void]$lines.Add("## Reading the timing fields")
[void]$lines.Add("")
[void]$lines.Add('The captured timings measure CPU-side client work and completed-draw intervals. `draw_cpu_ms` covers the render pipeline before framework presentation; `interval_gap_ms` is a signed coarse remainder after measured update and draw, including frame-cap wait, prior presentation/framework time, scheduling, capture overhead, and other unmeasured work. It can be negative when multiple updates occur within an interval. `previous_framework_end_draw_ms` isolates wall time spent inside framework `EndDraw` from that coarse remainder; waits inside the call may involve the OS or graphics driver. Neither the EndDraw duration nor the residual gap measures GPU execution or proves a GPU bottleneck. Use a graphics or system trace if the CPU-side stages do not explain a drop.')
[void]$lines.Add("GC and allocation deltas are process-wide across threads. Comparisons show timing association within the capture only; correlation does not establish cause.")
$report = $lines -join [Environment]::NewLine
Write-Output $report

if (-not [string]::IsNullOrWhiteSpace($ReportPath)) {
    if (-not [System.IO.Path]::IsPathRooted($ReportPath)) {
        $ReportPath = Join-Path (Split-Path -Parent $resolvedInputPath) $ReportPath
    }
    $resolvedReportPath = [System.IO.Path]::GetFullPath($ReportPath)
    $reportDirectory = Split-Path -Parent $resolvedReportPath
    if (-not (Test-Path -LiteralPath $reportDirectory -PathType Container)) {
        $null = New-Item -ItemType Directory -Path $reportDirectory -Force
    }
    [System.IO.File]::WriteAllText($resolvedReportPath, $report + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))
    Write-Host "Report saved to $resolvedReportPath"
}
