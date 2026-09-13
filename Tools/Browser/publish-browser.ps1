param(
    [string]$Configuration = "Release",
    [string]$Output = "",
    [string]$Version = "dev",
    [ValidateSet("stable", "beta", "alpha", "nightly")]
    [string]$Channel = "stable",
    [ValidateSet("Full", "PracticeAndLastToDie")]
    [string]$Edition = "Full",
    [string]$ContentId = "dev",
    [string]$RoomServiceOrigin = "https://api.superganggarrison.com",
    [string]$ArtifactRoot = "",
    [string]$BuildRoot = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\\..")).Path
$projectPath = Join-Path $repoRoot "Client.Browser\\OpenGarrison.Client.Browser.csproj"

# Keep compiler intermediates independent of release/output directory names.
# A local override also lets machines with a small system drive keep them elsewhere.
if ([string]::IsNullOrWhiteSpace($BuildRoot)) {
    $BuildRoot = $env:OPENGARRISON_BROWSER_BUILD_ROOT
}
if ([string]::IsNullOrWhiteSpace($BuildRoot)) {
    $localConfigPath = Join-Path $repoRoot ".build/browser-publish.json"
    if (Test-Path -LiteralPath $localConfigPath) {
        $localConfig = Get-Content -LiteralPath $localConfigPath -Raw | ConvertFrom-Json
        $BuildRoot = $localConfig.buildRoot
    }
}
if ([string]::IsNullOrWhiteSpace($BuildRoot)) {
    $BuildRoot = Join-Path $repoRoot ".build/browser-cache"
}
$BuildRoot = [System.IO.Path]::GetFullPath($BuildRoot, $repoRoot)

if ([string]::IsNullOrWhiteSpace($Output)) {
    $Output = "artifacts/browser-publish-aot"
}

$outputPath = if ([System.IO.Path]::IsPathRooted($Output)) { [System.IO.Path]::GetFullPath($Output) }
    else { [System.IO.Path]::GetFullPath((Join-Path $repoRoot $Output)) }
if ([string]::IsNullOrWhiteSpace($ArtifactRoot)) { $ArtifactRoot = Join-Path $repoRoot "artifacts" }
$artifactsRoot = [System.IO.Path]::GetFullPath($ArtifactRoot).TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
if (!$outputPath.StartsWith($artifactsRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Browser publish output must be a child of the specified artifacts directory."
}
$outputPrefix = $outputPath.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
$buildPrefix = $BuildRoot.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
if ($outputPrefix.StartsWith($buildPrefix, [System.StringComparison]::OrdinalIgnoreCase) -or
    $buildPrefix.StartsWith($outputPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Browser compiler cache and publish output must be separate directories."
}

$workloadList = (& dotnet workload list | Out-String)
if ($LASTEXITCODE -ne 0) {
    throw "Failed to query installed .NET workloads."
}

if ($workloadList -notmatch "wasm-tools") {
    throw "AOT publish requires the 'wasm-tools' workload. Install it with 'dotnet workload install wasm-tools' after clearing any pending reboot, then rerun this script."
}

$publishArgs = @(
    "publish",
    $projectPath,
    "-c", $Configuration,
    "-o", $outputPath,
    "-maxcpucount:1",
    "-p:UseSharedCompilation=false",
    "-p:RunAnalyzers=false",
    "-nodeReuse:false",
    "-p:OpenGarrisonBrowserAot=true",
    "-p:OpenGarrisonBrowserBuildVersion=$Version",
    "-p:OpenGarrisonBrowserReleaseChannel=$Channel",
    "-p:OpenGarrisonBrowserEdition=$Edition",
    "-p:OpenGarrisonRoomContentId=$ContentId",
    "-p:OpenGarrisonRoomServiceOrigin=$RoomServiceOrigin",
    "-p:InformationalVersion=$Version",
    "-p:IncludeSourceRevisionInInformationalVersion=false",
    "-p:Version=$Version",
    "-p:DisableParallelAot=true",
    "-p:DisableParallelEmccCompile=true",
    "-p:WasmNativeDebugSymbols=false",
    "-p:WasmBitcodeCompileOptimizationFlag=-O1",
    "-p:EmccVerbose=false"
)
if (![string]::IsNullOrWhiteSpace($BuildRoot)) {
    $publishArgs += "-p:OpenGarrisonBuildRoot=$([System.IO.Path]::GetFullPath($BuildRoot))"
    $publishArgs += "-p:OpenGarrisonClientAsLibrary=true"
}

Write-Host "Publishing browser client from $projectPath"
Write-Host "Configuration: $Configuration"
Write-Host "Output: $outputPath"
Write-Host "AOT enabled: true"
Write-Host "Build version: $Version"
Write-Host "Release channel: $Channel"
Write-Host "Edition: $Edition"
Write-Host "Room content: $ContentId"
Write-Host "Reusable compiler and asset cache: $BuildRoot"

# Publish over the existing output. The project prunes obsolete web assets from
# the current SDK inventory after a successful publish, without clearing caches.
[System.IO.Directory]::CreateDirectory($BuildRoot) > $null
try {
    $publishLock = [System.IO.File]::Open((Join-Path $BuildRoot "browser-publish.lock"),
        [System.IO.FileMode]::OpenOrCreate, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
} catch [System.IO.IOException] {
    throw "The browser build cache is already in use by another publish: $BuildRoot"
}
try {
    & dotnet @publishArgs
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE."
    }

    $duplicateRootContentPath = Join-Path $outputPath "Content"
    if (Test-Path $duplicateRootContentPath) {
        $resolvedOutputPath = (Resolve-Path $outputPath).Path.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
        $resolvedDuplicateRootContentPath = (Resolve-Path $duplicateRootContentPath).Path
        if (!$resolvedDuplicateRootContentPath.StartsWith($resolvedOutputPath, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to clean duplicate browser content outside the publish output: $resolvedDuplicateRootContentPath"
        }

        Remove-Item -LiteralPath $resolvedDuplicateRootContentPath -Recurse -Force
        Write-Host "Removed duplicate root Content directory. Deployable browser app: $(Join-Path $outputPath "wwwroot")"
    }
} finally {
    $publishLock.Dispose()
}
