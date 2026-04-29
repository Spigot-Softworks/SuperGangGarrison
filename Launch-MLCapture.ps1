param(
    [string]$ModelPath = "",
    [int]$ShortTicks = 1200,
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$clientDll = Join-Path $repoRoot ("Client\bin\{0}\net10.0\OG2.dll" -f $Configuration)
$captureDataRoot = Join-Path $repoRoot ".mlbot-data"
$captureModelRoot = Join-Path $repoRoot "Training\mlbot\out"

if (-not (Test-Path -LiteralPath $clientDll)) {
    throw "Built client not found: $clientDll`nRun: dotnet build `"$repoRoot\\Client\\OpenGarrison.Client.csproj`" -c $Configuration"
}

if (-not [string]::IsNullOrWhiteSpace($ModelPath)) {
    $modelDataPath = "$ModelPath.data"
    if (-not (Test-Path -LiteralPath $ModelPath)) {
        throw "Model not found: $ModelPath"
    }

    if (-not (Test-Path -LiteralPath $modelDataPath)) {
        throw "Model sidecar not found: $modelDataPath"
    }

    $env:OG_MLBOT_MODEL_PATH = $ModelPath
} else {
    Remove-Item Env:OG_MLBOT_MODEL_PATH -ErrorAction SilentlyContinue
}

$env:OG_BOT_MODE = "ml_capture"
$env:OG_MLBOT_CAPTURE_MAX_TICKS = [Math]::Max(1, $ShortTicks).ToString()
$env:OG_MLBOT_DATA_ROOT = $captureDataRoot
$env:OG_MLBOT_CAPTURE_MODEL_ROOT = $captureModelRoot

Write-Host "OG_BOT_MODE=$env:OG_BOT_MODE"
Write-Host "OG_MLBOT_CAPTURE_MAX_TICKS=$env:OG_MLBOT_CAPTURE_MAX_TICKS"
Write-Host "OG_MLBOT_DATA_ROOT=$env:OG_MLBOT_DATA_ROOT"
Write-Host "OG_MLBOT_CAPTURE_MODEL_ROOT=$env:OG_MLBOT_CAPTURE_MODEL_ROOT"
if ([string]::IsNullOrWhiteSpace($env:OG_MLBOT_MODEL_PATH)) {
    Write-Host "OG_MLBOT_MODEL_PATH=<unset> (capture mode will auto-resolve class/team/phase models from OG_MLBOT_CAPTURE_MODEL_ROOT)"
} else {
    Write-Host "OG_MLBOT_MODEL_PATH=$env:OG_MLBOT_MODEL_PATH (explicit override)"
}
Write-Host "Hotkeys: F3 stop/status, F9 attack short (auto-chains to return after pickup), F10 return short (+intel), F7 attack DAgger (auto return), F8 return DAgger (+intel), F11 auto DAgger"
Write-Host "Launching built client: $clientDll"

dotnet $clientDll
