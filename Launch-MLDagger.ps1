$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$clientProject = Join-Path $repoRoot "Client\OpenGarrison.Client.csproj"
$captureDataRoot = Join-Path $repoRoot ".mlbot-data"
$modelPath = Join-Path $repoRoot "Training\mlbot\out\twodforttwo-scout-mixed-upweighted-v2\model.onnx"
$modelDataPath = "$modelPath.data"

if (-not (Test-Path -LiteralPath $clientProject)) {
    throw "Client project not found: $clientProject"
}

if (-not (Test-Path -LiteralPath $modelPath)) {
    throw "Model not found: $modelPath"
}

if (-not (Test-Path -LiteralPath $modelDataPath)) {
    throw "Model sidecar not found: $modelDataPath"
}

$env:OG_BOT_MODE = "ml"
$env:OG_MLBOT_MODEL_PATH = $modelPath
$env:OG_MLBOT_DATA_ROOT = $captureDataRoot

Write-Host "OG_BOT_MODE=$env:OG_BOT_MODE"
Write-Host "OG_MLBOT_MODEL_PATH=$env:OG_MLBOT_MODEL_PATH"
Write-Host "OG_MLBOT_DATA_ROOT=$env:OG_MLBOT_DATA_ROOT"
Write-Host "Launching client..."

dotnet run --project $clientProject
