[CmdletBinding()]
param(
    [string]$ExecutablePath = "",
    [string]$OutputDirectory = "",
    [string[]]$ArgumentList = @(),
    [switch]$BuildIfMissing,
    [switch]$ValidateOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$captureRoot = Join-Path $repoRoot ".build\manual-performance-capture"
$defaultExecutablePath = Join-Path $captureRoot "bin\OpenGarrison.Client\Release\net10.0\OG2.exe"
$useDefaultExecutable = [string]::IsNullOrWhiteSpace($ExecutablePath)
if ($useDefaultExecutable) {
    $ExecutablePath = $defaultExecutablePath
} elseif (-not [System.IO.Path]::IsPathRooted($ExecutablePath)) {
    $ExecutablePath = Join-Path (Get-Location).Path $ExecutablePath
}
$ExecutablePath = [System.IO.Path]::GetFullPath($ExecutablePath)

if (-not (Test-Path -LiteralPath $ExecutablePath -PathType Leaf) -and $BuildIfMissing) {
    if (-not $useDefaultExecutable) {
        throw "-BuildIfMissing only builds the default isolated Release output. Supply that output path or build it separately: $defaultExecutablePath"
    }

    Write-Host "Building the Release client into $captureRoot ..."
    $buildArguments = @(
        "build",
        "Client/OpenGarrison.Client.csproj",
        "-c", "Release",
        "-m:1",
        "-p:RunAnalyzers=false",
        "-p:OpenGarrisonBuildRoot=$captureRoot"
    )
    Push-Location $repoRoot
    try {
        & dotnet @buildArguments
        if ($LASTEXITCODE -ne 0) {
            throw "Release build failed with exit code $LASTEXITCODE."
        }
    } finally {
        Pop-Location
    }
}

if (-not (Test-Path -LiteralPath $ExecutablePath -PathType Leaf)) {
    throw "Client executable not found: $ExecutablePath`nBuild the isolated Release client first, or add -BuildIfMissing to build it into $captureRoot."
}

$executableDirectory = Split-Path -Parent $ExecutablePath
$contentDirectory = Join-Path $executableDirectory "Content"
$missingRuntimeContent = [System.Collections.Generic.List[string]]::new()
$contentPrefix = [System.IO.Path]::GetFullPath($contentDirectory).TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar

function Add-RequiredRuntimeContentFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RelativePath,
        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    if ([string]::IsNullOrWhiteSpace($RelativePath)) {
        $missingRuntimeContent.Add("$Description (empty path)")
        return
    }

    $normalizedPath = $RelativePath.Replace('/', [System.IO.Path]::DirectorySeparatorChar).Replace('\', [System.IO.Path]::DirectorySeparatorChar)
    if ($normalizedPath.StartsWith("Content$([System.IO.Path]::DirectorySeparatorChar)", [System.StringComparison]::OrdinalIgnoreCase)) {
        $normalizedPath = $normalizedPath.Substring("Content$([System.IO.Path]::DirectorySeparatorChar)".Length)
    }

    if ([System.IO.Path]::IsPathRooted($normalizedPath) -or
        $normalizedPath.Split([System.IO.Path]::DirectorySeparatorChar) -contains '..') {
        $missingRuntimeContent.Add("$Description (unsafe path: $RelativePath)")
        return
    }

    $resolvedPath = [System.IO.Path]::GetFullPath((Join-Path $contentDirectory $normalizedPath))
    if (-not $resolvedPath.StartsWith($contentPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        $missingRuntimeContent.Add("$Description (outside Content: $RelativePath)")
        return
    }

    if (-not (Test-Path -LiteralPath $resolvedPath -PathType Leaf)) {
        $missingRuntimeContent.Add("$Description ($RelativePath)")
    }
}

function Add-RequiredAtlasPages {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RelativeManifestPath
    )

    $manifestPath = Join-Path $contentDirectory $RelativeManifestPath
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        $missingRuntimeContent.Add("atlas manifest ($RelativeManifestPath)")
        return
    }

    try {
        $document = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
        $manifestProperty = $document.PSObject.Properties['Manifest']
        $manifest = if ($null -ne $manifestProperty) { $manifestProperty.Value } else { $document }
        $atlasesProperty = $manifest.PSObject.Properties['Atlases']
        [object[]]$atlases = if ($null -ne $atlasesProperty) { @($atlasesProperty.Value) } else { @() }
        if ($atlases.Count -eq 0) {
            $missingRuntimeContent.Add("atlas manifest has no pages ($RelativeManifestPath)")
            return
        }

        foreach ($atlas in $atlases) {
            $imagePathProperty = $atlas.PSObject.Properties['ImagePath']
            $imagePath = if ($null -ne $imagePathProperty) { [string]$imagePathProperty.Value } else { '' }
            Add-RequiredRuntimeContentFile -RelativePath $imagePath -Description "atlas page in $RelativeManifestPath"
        }
    } catch {
        $missingRuntimeContent.Add("invalid atlas manifest ($RelativeManifestPath): $($_.Exception.Message)")
    }
}

$gameMakerManifestPath = Join-Path $contentDirectory "_gamemaker-asset-manifest.json"
if (-not (Test-Path -LiteralPath $gameMakerManifestPath -PathType Leaf)) {
    $missingRuntimeContent.Add("runtime GameMaker asset manifest (Content/_gamemaker-asset-manifest.json)")
} else {
    try {
        $gameMakerManifest = Get-Content -LiteralPath $gameMakerManifestPath -Raw | ConvertFrom-Json
        $soundProperties = @($gameMakerManifest.sounds.PSObject.Properties)
        $backgroundProperties = @($gameMakerManifest.backgrounds.PSObject.Properties)
        if ($soundProperties.Count -eq 0) {
            $missingRuntimeContent.Add("GameMaker manifest contains no sounds")
        }
        if ($backgroundProperties.Count -eq 0) {
            $missingRuntimeContent.Add("GameMaker manifest contains no backgrounds")
        }

        foreach ($sound in $soundProperties) {
            Add-RequiredRuntimeContentFile -RelativePath ([string]$sound.Value.audioPath) -Description "sound $($sound.Name)"
        }
        foreach ($background in $backgroundProperties) {
            Add-RequiredRuntimeContentFile -RelativePath ([string]$background.Value.imagePath) -Description "background $($background.Name)"
        }
    } catch {
        $missingRuntimeContent.Add("invalid GameMaker asset manifest: $($_.Exception.Message)")
    }
}

foreach ($atlasManifest in @(
    "Browser/Manifests/bootstrap-manifest.json",
    "Browser/Manifests/stock-pack-atlas-manifest.json",
    "Browser/Manifests/gamemaker-atlas-manifest.json"
)) {
    Add-RequiredAtlasPages -RelativeManifestPath $atlasManifest
}

$rotatedSpritesSourceMaster = Join-Path $repoRoot "Core/Content/Sprites/WeaponsRotated/_master-manifest.json"
$rotatedSpritesMaster = Join-Path $contentDirectory "Sprites/WeaponsRotated/_master-manifest.json"
if (Test-Path -LiteralPath $rotatedSpritesSourceMaster -PathType Leaf) {
    if (-not (Test-Path -LiteralPath $rotatedSpritesMaster -PathType Leaf)) {
        $missingRuntimeContent.Add("baked weapon sprite master manifest (Content/Sprites/WeaponsRotated/_master-manifest.json)")
    } else {
        try {
            $rotatedManifest = Get-Content -LiteralPath $rotatedSpritesMaster -Raw | ConvertFrom-Json
            $spriteNames = @($rotatedManifest.spriteNames)
            if ($spriteNames.Count -eq 0) {
                $missingRuntimeContent.Add("baked weapon sprite master manifest has no sprites")
            }
            foreach ($spriteName in $spriteNames) {
                $spriteDirectory = Join-Path $contentDirectory (Join-Path "Sprites/WeaponsRotated" ([string]$spriteName))
                $spriteManifestPath = Join-Path $spriteDirectory "manifest.json"
                if (-not (Test-Path -LiteralPath $spriteManifestPath -PathType Leaf)) {
                    $missingRuntimeContent.Add("baked weapon sprite manifest (Content/Sprites/WeaponsRotated/$spriteName/manifest.json)")
                    continue
                }

                try {
                    $spriteManifest = Get-Content -LiteralPath $spriteManifestPath -Raw | ConvertFrom-Json
                    $frameCount = [int]$spriteManifest.frameCount
                    if ($frameCount -le 0) {
                        $missingRuntimeContent.Add("baked weapon sprite manifest has no frames (Content/Sprites/WeaponsRotated/$spriteName/manifest.json)")
                        continue
                    }
                    for ($frameIndex = 0; $frameIndex -lt $frameCount; $frameIndex++) {
                        $frameName = "frame{0:D2}.png" -f $frameIndex
                        $framePath = Join-Path $spriteDirectory $frameName
                        if (-not (Test-Path -LiteralPath $framePath -PathType Leaf)) {
                            $missingRuntimeContent.Add("baked weapon sprite strip (Content/Sprites/WeaponsRotated/$spriteName/$frameName)")
                        }
                    }
                } catch {
                    $missingRuntimeContent.Add("invalid baked weapon sprite manifest (Content/Sprites/WeaponsRotated/$spriteName/manifest.json): $($_.Exception.Message)")
                }
            }
        } catch {
            $missingRuntimeContent.Add("invalid baked weapon sprite master manifest: $($_.Exception.Message)")
        }
    }
}

if ($missingRuntimeContent.Count -gt 0) {
    $examples = @($missingRuntimeContent | Select-Object -First 12)
    $message = @(
        "The capture client runtime content is incomplete under $contentDirectory."
        "Missing or invalid content entries: $($missingRuntimeContent.Count)."
        ($examples | ForEach-Object { "  - $_" })
        "Build the desktop client into an isolated build root with the default content-copy targets, for example:"
        "  dotnet build Client/OpenGarrison.Client.csproj -c Release -m:1 -p:OpenGarrisonBuildRoot=<absolute-path-to-.build/manual-performance-capture>"
        "Do not pass OpenGarrisonPackageScriptOwnsContent=true for a desktop capture build; that property intentionally skips Core/Content copy items. Keep package and test builds on separate OpenGarrisonBuildRoot values."
    ) -join [Environment]::NewLine
    throw $message
}

if ($ValidateOnly) {
    Write-Host "Runtime content preflight passed for $ExecutablePath."
    return
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $captureSessionsRoot = Join-Path $captureRoot "captures"
    $sessionStamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $OutputDirectory = Join-Path $captureSessionsRoot $sessionStamp
    $suffix = 1
    while (Test-Path -LiteralPath $OutputDirectory) {
        $OutputDirectory = Join-Path $captureSessionsRoot ("{0}-{1:D2}" -f $sessionStamp, $suffix)
        $suffix += 1
    }
} elseif (-not [System.IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory = Join-Path (Get-Location).Path $OutputDirectory
}

$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
$null = New-Item -ItemType Directory -Path $OutputDirectory -Force
$csvPath = [System.IO.Path]::GetFullPath((Join-Path $OutputDirectory "frames.csv"))
$metadataPath = [System.IO.Path]::GetFullPath((Join-Path $OutputDirectory "session.json"))
if ((Test-Path -LiteralPath $csvPath) -or (Test-Path -LiteralPath $metadataPath)) {
    throw "Capture output already contains frames.csv or session.json: $OutputDirectory"
}

$traceFlagsToClear = @(
    "OG_CLIENT_ASSET_TRACE",
    "OPENGARRISON_VERIFY_ATLAS_UPLOADS",
    "OG_CLIENT_PERF_SIM_TRACE",
    "OG_CLIENT_PERF_SIM_TRACE_THRESHOLD_MS",
    "OG_CLIENT_PERF_SIM_PLAYER_TRACE_THRESHOLD_MS",
    "OG_CLIENT_PERF_SIM_PLAYER_PHASE_TRACE_THRESHOLD_MS",
    "OG_CLIENT_PERF_BOT_TRACE",
    "OG_CLIENT_PERF_BOT_TRACE_CONTACTS",
    "OG_CLIENT_PERF_BOT_THINK_TRACE"
)
$captureFlagsToClear = @(
    "OG_CLIENT_PERF_TEST",
    "OG_CLIENT_PERF_AUTO_EXIT",
    "OG_CLIENT_PERF_LOG",
    "OG_CLIENT_ONLINE_SMOKE",
    "OG_CLIENT_ONLINE_SMOKE_AUTO_EXIT"
)
$knownFlags = @(
    "OG_CLIENT_ASSET_TRACE",
    "OPENGARRISON_VERIFY_ATLAS_UPLOADS",
    "OG_CLIENT_PERF_CAPTURE",
    "OG_CLIENT_PERF_CAPTURE_PATH",
    "OG_CLIENT_PERF_TEST",
    "OG_CLIENT_PERF_AUTO_EXIT",
    "OG_CLIENT_PERF_LOG",
    "OG_CLIENT_PERF_SIM_TRACE",
    "OG_CLIENT_PERF_SIM_TRACE_THRESHOLD_MS",
    "OG_CLIENT_PERF_SIM_PLAYER_TRACE_THRESHOLD_MS",
    "OG_CLIENT_PERF_SIM_PLAYER_PHASE_TRACE_THRESHOLD_MS",
    "OG_CLIENT_PERF_BOT_TRACE",
    "OG_CLIENT_PERF_BOT_TRACE_CONTACTS",
    "OG_CLIENT_PERF_BOT_THINK_TRACE",
    "OG_CLIENT_PERF_BOT_DIAGNOSTICS",
    "OG_CLIENT_ONLINE_SMOKE",
    "OG_CLIENT_ONLINE_SMOKE_AUTO_EXIT"
)
$inheritedFlags = [ordered]@{}
foreach ($flagName in $knownFlags) {
    $inheritedFlags[$flagName] = [Environment]::GetEnvironmentVariable($flagName, "Process")
}

$executableHash = (Get-FileHash -LiteralPath $ExecutablePath -Algorithm SHA256).Hash
$managedAssemblyCandidatePath = [System.IO.Path]::ChangeExtension($ExecutablePath, ".dll")
if (-not (Test-Path -LiteralPath $managedAssemblyCandidatePath -PathType Leaf) -and
    [System.IO.Path]::GetFileNameWithoutExtension($ExecutablePath) -eq "OG2.Game") {
    # Packaging renames the apphost while retaining the managed assembly name.
    $managedAssemblyCandidatePath = Join-Path $executableDirectory "OG2.dll"
}
$managedAssemblyPath = if (Test-Path -LiteralPath $managedAssemblyCandidatePath -PathType Leaf) {
    $managedAssemblyCandidatePath
} else {
    $null
}
$managedAssemblyHash = if ($null -ne $managedAssemblyPath) {
    (Get-FileHash -LiteralPath $managedAssemblyPath -Algorithm SHA256).Hash
} else {
    $null
}
$executableVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($ExecutablePath).FileVersion
$startInfo = [System.Diagnostics.ProcessStartInfo]::new()
$startInfo.FileName = $ExecutablePath
$startInfo.WorkingDirectory = $executableDirectory
$startInfo.UseShellExecute = $false
$startInfo.CreateNoWindow = $true
foreach ($argument in $ArgumentList) {
    [void]$startInfo.ArgumentList.Add($argument)
}

# ProcessStartInfo owns a child environment snapshot, so these capture-only
# changes cannot leak into PowerShell or another game process.
if ($null -ne $startInfo.PSObject.Properties["Environment"]) {
    $childEnvironment = $startInfo.Environment
} else {
    $childEnvironment = $startInfo.EnvironmentVariables
}
foreach ($flagName in ($traceFlagsToClear + $captureFlagsToClear)) {
    [void]$childEnvironment.Remove($flagName)
}
$childEnvironment["OG_CLIENT_PERF_CAPTURE"] = "1"
$childEnvironment["OG_CLIENT_PERF_CAPTURE_PATH"] = $csvPath

$metadata = [ordered]@{
    schema = "manual-client-performance-capture/1"
    started_local = (Get-Date).ToString("o")
    started_utc = [DateTimeOffset]::UtcNow.ToString("o")
    ended_local = $null
    ended_utc = $null
    process_id = $null
    exit_code = $null
    executable_path = $ExecutablePath
    executable_sha256 = $executableHash
    managed_assembly_path = $managedAssemblyPath
    managed_assembly_sha256 = $managedAssemblyHash
    executable_file_version = $executableVersion
    arguments = @($ArgumentList)
    working_directory = $executableDirectory
    capture_csv_path = $csvPath
    runtime_logs_directory = (Join-Path $executableDirectory "logs")
    environment = [ordered]@{
        capture_enabled = $true
        test_autoplay = "cleared"
        online_smoke_autoplay = "cleared"
        synchronous_performance_log = "cleared"
        extra_simulation_and_bot_traces = "cleared"
        bot_diagnostics = $inheritedFlags["OG_CLIENT_PERF_BOT_DIAGNOSTICS"]
        inherited_known_performance_flags = $inheritedFlags
    }
    notes = @(
        "Graphics, audio, frame cap, VSync, gameplay, and bot settings were not modified by the launcher.",
        "Frame timing data is held in memory by the client and written to the CSV on normal game exit.",
        "The runtime logs directory is recorded for discovery; existing user logs are not copied."
    )
}
$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
[System.IO.File]::WriteAllText($metadataPath, (($metadata | ConvertTo-Json -Depth 8) + [Environment]::NewLine), $utf8NoBom)

Write-Host "Opening the game for a manual performance capture."
Write-Host "Play normally; close the game when finished so it can flush the frame ring buffer."
Write-Host "Capture output: $OutputDirectory"
Write-Host "The game keeps its existing graphics, audio, VSync, frame cap, and practice settings."

$process = $null
try {
    $process = [System.Diagnostics.Process]::Start($startInfo)
    if ($null -eq $process) {
        throw "The client process could not be started."
    }

    $metadata.process_id = $process.Id
    [System.IO.File]::WriteAllText($metadataPath, (($metadata | ConvertTo-Json -Depth 8) + [Environment]::NewLine), $utf8NoBom)
    $process.WaitForExit()
    $process.Refresh()
    $metadata.ended_local = (Get-Date).ToString("o")
    $metadata.ended_utc = [DateTimeOffset]::UtcNow.ToString("o")
    $metadata.exit_code = $process.ExitCode
    [System.IO.File]::WriteAllText($metadataPath, (($metadata | ConvertTo-Json -Depth 8) + [Environment]::NewLine), $utf8NoBom)
    Write-Host "Game exited with code $($process.ExitCode)."
    Write-Host "Frame capture: $csvPath"
} finally {
    if ($null -ne $process) {
        $process.Dispose()
    }
}
