# Manual client performance capture

The launcher starts the normal Release desktop client and keeps the current graphics, audio, VSync, frame cap, gameplay, and bot settings.

Run this from the repository root in PowerShell 7:

```powershell
pwsh -NoProfile -File scripts/start-performance-capture.ps1
```

The launcher expects `.build/manual-performance-capture/bin/OpenGarrison.Client/Release/net10.0/OG2.exe`. If it is missing, opt into a Release build with:

```powershell
pwsh -NoProfile -File scripts/start-performance-capture.ps1 -BuildIfMissing
```

Before launching, the script checks that the generated GameMaker manifest's sound and background files exist, that the bootstrap, stock-gameplay, and GameMaker atlas manifests and their pages are present, and that any source-tracked rotated-weapon bake is complete. It intentionally does not require legacy loose sprite PNGs when the packaged atlas is available. To run only this check without opening the game, pass `-ValidateOnly`.

If the launcher reports missing runtime content, rebuild the desktop output with the normal copy targets enabled:

```powershell
$buildRoot = Join-Path (Get-Location).Path ".build/manual-performance-capture"
dotnet build Client/OpenGarrison.Client.csproj -c Release -m:1 "-p:OpenGarrisonBuildRoot=$buildRoot"
```

Do not pass `OpenGarrisonPackageScriptOwnsContent=true` for this desktop capture build; that packaging mode intentionally omits the loose `Core/Content` copy items. Keep packaging and test builds on separate `OpenGarrisonBuildRoot` values from the capture output so an incremental clean from another build mode cannot leave this launcher pointed at partial content.

When the game opens, set up the same Practice map and bots that reproduce the problem, then keep playing through several stutters. Exit normally when you have enough examples so the client can save the capture. Each timestamped output directory under `.build/manual-performance-capture/captures/` contains `frames.csv`, `frames.summary.json`, and `session.json`. Forced termination can prevent the in-memory frame data and summary from being written. The session sidecar records the executable, capture path, timing, selected inherited diagnostic flags, and runtime logs directory; it does not copy existing logs.

The client retains the latest 12,000 draws and up to 2,000 slow frames at 25 ms or more. The archive keeps the earliest qualifying slow frames; after it fills, later ones are omitted from the CSV, and `hitch_archive_omitted_count` records the number. `capture_region` identifies recent and archived rows. Use `frames.summary.json` for whole-session gameplay percentiles and hitch rates. `dropped_frames` counts overwritten recent-buffer rows, not display or GPU drops. If the summary sidecar is missing, the analyzer labels its CSV-only fallback as selection-biased.

```powershell
pwsh -NoProfile -File scripts/analyze-performance-capture.ps1 `
    -InputPath ".build/manual-performance-capture/captures/<timestamp>/frames.csv" `
    -ReportPath analysis.md
```

The analyzer reports whole-session gameplay frame-time percentiles, equivalent FPS percentiles, hitch counts at 25/33.34/50 ms, GC/allocation association, and the slowest recorded frames with individual stage timings. It also separates loading-overlay and inactive-window rows in the CSV. The overlay flag does not cover every loading transition; inactive rows include menus, selections, and warmup. CSV flags describe state at draw completion, while stage values accumulate from the previous draw, so a transition row can mix contexts. Stage measurements can overlap and must not be added. Allocation and GC deltas are process-wide across threads; correlation shows association, not cause.

The capture reports CPU-side client timing. The signed interval remainder includes frame-cap waits, prior presentation/framework time, scheduling, capture overhead, and other unmeasured work. The data does not measure GPU execution or prove a GPU bottleneck. Use a graphics or system trace if the measured CPU stages do not explain a drop.

On desktop captures, `previous_framework_end_draw_ms` records game-thread wall time spent inside MonoGame's framework `EndDraw` after the previous completed draw; that call's duration belongs to the interval ending at the current row. The analyzer shows this duration alongside `interval_gap_ms` and the remaining gap (`interval_gap_ms - previous_framework_end_draw_ms`) without changing interval or summary calculations. This can isolate time spent inside framework presentation from other unmeasured time, but it is not GPU execution time; older CSV files without this column remain supported.

The CSV records VSync, frame-rate limit, viewport size, bot count, and entity count per frame. The launcher disables performance-test and online-smoke autoplay, synchronous performance logging, and extra simulation/bot tracing in the child game process. It leaves the existing bot-diagnostics preference and the user's game settings intact.

## Focused corpse-acid pixel benchmark

`CorpseAcidDissolvePixelsTests.DeterministicBenchmarkComparesFullAndIncrementalPixelWork` compares the production pixel helper with a full-image reference across forward progress and rewinds. It reconstructs the uploaded texture buffer from each partial region using the production row-based `startIndex` and `elementCount`. A second controlled sample advances through an untouched transparent region to count skipped uploads when progress changes but pixels do not.

Run it after restoring the test project packages, with a separate absolute build root:

```powershell
$testBuildRoot = [System.IO.Path]::GetFullPath('.build/performance-regression-tests')
$env:OPENGARRISON_CORPSE_ACID_BENCHMARK_PATH = 'artifacts/manual-performance-capture-verification/corpse-acid-benchmark.json'
dotnet test Tests/OpenGarrison.PluginHost.Tests/OpenGarrison.PluginHost.Tests.csproj -c Release --no-restore -p:OpenGarrisonBuildRoot="$testBuildRoot" -p:OpenGarrisonPackageScriptOwnsContent=true --filter 'FullyQualifiedName~DeterministicBenchmarkComparesFullAndIncrementalPixelWork'
```

Never reuse the playable `.build/manual-performance-capture` root for this test build: SDK cleanup can remove copied content when `OpenGarrisonPackageScriptOwnsContent=true`. The report prints through xUnit and is saved to the requested JSON path. It measures CPU pixel processing and estimated upload calls/pixels, not `Texture2D.SetData` transfer or OpenGL synchronization time.
