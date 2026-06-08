param(
    [string]$Configuration = "Release",
    [string]$Output = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\\..")).Path
$projectPath = Join-Path $repoRoot "Client.Browser\\OpenGarrison.Client.Browser.csproj"

if ([string]::IsNullOrWhiteSpace($Output)) {
    $Output = "artifacts/browser-publish-aot"
}

$outputPath = Join-Path $repoRoot $Output

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
    "-nodeReuse:false",
    "-p:OpenGarrisonBrowserAot=true",
    "-p:DisableParallelAot=true",
    "-p:DisableParallelEmccCompile=true",
    "-p:WasmNativeDebugSymbols=false",
    "-p:WasmBitcodeCompileOptimizationFlag=-O1",
    "-p:EmccVerbose=false"
)

Write-Host "Publishing browser client from $projectPath"
Write-Host "Configuration: $Configuration"
Write-Host "Output: $outputPath"
Write-Host "AOT enabled: true"

if (Test-Path $outputPath) {
    Remove-Item -LiteralPath $outputPath -Recurse -Force
}

& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$duplicateRootContentPath = Join-Path $outputPath "Content"
if (Test-Path $duplicateRootContentPath) {
    $resolvedOutputPath = (Resolve-Path $outputPath).Path
    $resolvedDuplicateRootContentPath = (Resolve-Path $duplicateRootContentPath).Path
    if (!$resolvedDuplicateRootContentPath.StartsWith($resolvedOutputPath, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean duplicate browser content outside the publish output: $resolvedDuplicateRootContentPath"
    }

    Remove-Item -LiteralPath $resolvedDuplicateRootContentPath -Recurse -Force
    Write-Host "Removed duplicate root Content directory. Deployable browser app: $(Join-Path $outputPath "wwwroot")"
}
