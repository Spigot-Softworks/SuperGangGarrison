[CmdletBinding()]
param(
    [string]$MusicDirectory = "",
    [int]$VorbisQuality = 4,
    [switch]$Overwrite
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not (Get-Command ffmpeg -ErrorAction SilentlyContinue)) {
    throw "ffmpeg was not found on PATH."
}

if ([string]::IsNullOrWhiteSpace($MusicDirectory)) {
    $scriptRoot = if (-not [string]::IsNullOrWhiteSpace($PSScriptRoot)) {
        $PSScriptRoot
    }
    else {
        Split-Path -Parent $MyInvocation.MyCommand.Path
    }

    $MusicDirectory = Join-Path (Split-Path -Parent $scriptRoot) "Core\\Content\\Sounds\\Music"
}

if (-not (Test-Path $MusicDirectory)) {
    throw "Music directory '$MusicDirectory' does not exist."
}

$wavFiles = Get-ChildItem $MusicDirectory -Filter *.wav -File | Sort-Object Name
foreach ($wavFile in $wavFiles) {
    $oggPath = [System.IO.Path]::ChangeExtension($wavFile.FullName, ".ogg")
    if ((Test-Path $oggPath) -and -not $Overwrite) {
        Write-Host "[convert] skipping existing $($wavFile.Name)"
        continue
    }

    Write-Host "[convert] $($wavFile.Name) -> $([System.IO.Path]::GetFileName($oggPath))"
    & ffmpeg -y -i $wavFile.FullName -vn -c:a libvorbis -q:a $VorbisQuality $oggPath
    if ($LASTEXITCODE -ne 0) {
        throw "ffmpeg failed while converting '$($wavFile.FullName)'."
    }
}
