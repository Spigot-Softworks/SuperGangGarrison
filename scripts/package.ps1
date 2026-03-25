[CmdletBinding()]
param(
    [string[]]$Platforms = @("win-x64", "linux-x64"),
    [switch]$RunTests,
    [switch]$SkipTests
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$distRoot = Join-Path $repoRoot "dist"
$stagingRoot = Join-Path $distRoot "_staging"
$configuration = "Release"
$projects =
@(
    "OpenGarrison.Core/OpenGarrison.Core.csproj",
    "OpenGarrison.Protocol/OpenGarrison.Protocol.csproj",
    "OpenGarrison.Client/OpenGarrison.Client.csproj",
    "OpenGarrison.Server/OpenGarrison.Server.csproj",
    "OpenGarrison.ServerLauncher/OpenGarrison.ServerLauncher.csproj"
)

function Invoke-DotNet {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Get-ArchiveName {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RuntimeIdentifier
    )

    switch ($RuntimeIdentifier) {
        "win-x64" { return "OpenGarrison-Windows-x64.zip" }
        "linux-x64" { return "OpenGarrison-Linux-x64.tar.gz" }
        "osx-x64" { return "OpenGarrison-macOS-x64.tar.gz" }
        "osx-arm64" { return "OpenGarrison-macOS-arm64.tar.gz" }
        default { return "OpenGarrison-$RuntimeIdentifier.tar.gz" }
    }
}

function Test-IsSelfContainedRuntime {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RuntimeIdentifier
    )

    return $RuntimeIdentifier -ne "win-x64"
}

function New-PackageArchive {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RuntimeIdentifier,
        [Parameter(Mandatory = $true)]
        [string]$SourceDirectory,
        [Parameter(Mandatory = $true)]
        [string]$ArchivePath
    )

    if ($RuntimeIdentifier -eq "win-x64") {
        Compress-Archive -Path (Join-Path $SourceDirectory "*") -DestinationPath $ArchivePath -Force
        return
    }

    & tar -C $SourceDirectory -czf $ArchivePath .
    if ($LASTEXITCODE -ne 0) {
        throw "tar failed while creating '$ArchivePath' for runtime '$RuntimeIdentifier'."
    }
}

function Get-AvailableOutputDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PreferredPath
    )

    for ($index = 0; $index -lt 100; $index += 1) {
        $candidate = if ($index -eq 0) {
            $PreferredPath
        }
        else {
            "$PreferredPath-package-$index"
        }

        if (-not (Test-Path $candidate)) {
            return $candidate
        }

        try {
            Remove-Item $candidate -Recurse -Force -ErrorAction Stop
            return $candidate
        }
        catch {
        }
    }

    throw "Could not acquire a writable output directory based on '$PreferredPath'."
}

function Copy-DirectoryContents {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourceDirectory,
        [Parameter(Mandatory = $true)]
        [string]$DestinationDirectory
    )

    New-Item -ItemType Directory -Path $DestinationDirectory -Force | Out-Null
    Copy-Item (Join-Path $SourceDirectory "*") $DestinationDirectory -Recurse -Force
}

function New-UnixLauncherScript {
    param(
        [Parameter(Mandatory = $true)]
        [string]$DestinationPath,
        [Parameter(Mandatory = $true)]
        [string]$ExecutableName
    )

    $scriptContents = @'
#!/bin/sh
set -eu

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
cd "$SCRIPT_DIR"
chmod +x "./__EXECUTABLE__"
exec "./__EXECUTABLE__" "$@"
'@.Replace("__EXECUTABLE__", $ExecutableName)

    [System.IO.File]::WriteAllText($DestinationPath, $scriptContents, [System.Text.Encoding]::ASCII)
}

function Add-UnixLaunchers {
    param(
        [Parameter(Mandatory = $true)]
        [string]$OutputDirectory
    )

    New-UnixLauncherScript -DestinationPath (Join-Path $OutputDirectory "run-client.sh") -ExecutableName "OpenGarrison.Client"
    New-UnixLauncherScript -DestinationPath (Join-Path $OutputDirectory "run-server.sh") -ExecutableName "OpenGarrison.Server"
    New-UnixLauncherScript -DestinationPath (Join-Path $OutputDirectory "run-server-launcher.sh") -ExecutableName "OpenGarrison.ServerLauncher"
}

function Move-PluginArtifactsToPluginDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$OutputDirectory
    )

    $pluginDirectory = Join-Path $OutputDirectory "Plugins"
    New-Item -ItemType Directory -Path $pluginDirectory -Force | Out-Null

    $pluginArtifacts = Get-ChildItem -Path $OutputDirectory -File |
        Where-Object {
            $_.Name -like "OpenGarrison.Server.Plugins.*" -and
            $_.Name -notlike "OpenGarrison.Server.Plugins.Abstractions.*"
        }

    foreach ($artifact in $pluginArtifacts) {
        $destinationPath = Join-Path $pluginDirectory $artifact.Name
        Copy-Item $artifact.FullName $destinationPath -Force
        Remove-Item $artifact.FullName -Force
    }
}

New-Item -ItemType Directory -Path $distRoot -Force | Out-Null
New-Item -ItemType Directory -Path $stagingRoot -Force | Out-Null

$toolManifestPaths = @(
    (Join-Path $repoRoot ".config/dotnet-tools.json"),
    (Join-Path $repoRoot "dotnet-tools.json")
)
if ($toolManifestPaths | Where-Object { Test-Path $_ }) {
    Invoke-DotNet -Arguments @("tool", "restore")
}

if ($RunTests -and -not $SkipTests) {
    Invoke-DotNet -Arguments @("test", (Join-Path $repoRoot "OpenGarrison.sln"), "-c", $configuration)
}

$builtOutputs = @()

foreach ($runtimeIdentifier in $Platforms) {
    $stagingDirectory = Join-Path $stagingRoot $runtimeIdentifier
    if (Test-Path $stagingDirectory) {
        Remove-Item $stagingDirectory -Recurse -Force
    }

    New-Item -ItemType Directory -Path $stagingDirectory -Force | Out-Null

    foreach ($project in $projects) {
        $projectPath = Join-Path $repoRoot $project
        $selfContained = if (Test-IsSelfContainedRuntime -RuntimeIdentifier $runtimeIdentifier) { "true" } else { "false" }
        Invoke-DotNet -Arguments @(
            "restore",
            $projectPath,
            "-r", $runtimeIdentifier
        )

        Invoke-DotNet -Arguments @(
            "publish",
            $projectPath,
            "-c", $configuration,
            "-r", $runtimeIdentifier,
            "--self-contained", $selfContained,
            "--no-restore",
            "-o", $stagingDirectory
        )
    }

    Copy-DirectoryContents -SourceDirectory (Join-Path $repoRoot "OpenGarrison.Core/Content") -DestinationDirectory (Join-Path $stagingDirectory "Content")
    Copy-DirectoryContents -SourceDirectory (Join-Path $repoRoot "OpenGarrison.Client/Content") -DestinationDirectory (Join-Path $stagingDirectory "Content")
    Copy-DirectoryContents -SourceDirectory (Join-Path $repoRoot "packaging/config") -DestinationDirectory (Join-Path $stagingDirectory "config")
    Copy-Item (Join-Path $repoRoot "sampleMapRotation.txt") (Join-Path $stagingDirectory "config/sampleMapRotation.txt") -Force
    Copy-Item (Join-Path $repoRoot "packaging/README.txt") (Join-Path $stagingDirectory "README.txt") -Force

    if (Test-IsSelfContainedRuntime -RuntimeIdentifier $runtimeIdentifier) {
        Add-UnixLaunchers -OutputDirectory $stagingDirectory
    }

    Move-PluginArtifactsToPluginDirectory -OutputDirectory $stagingDirectory

    $finalDirectory = Get-AvailableOutputDirectory -PreferredPath (Join-Path $distRoot $runtimeIdentifier)
    Copy-DirectoryContents -SourceDirectory $stagingDirectory -DestinationDirectory $finalDirectory

    $archivePath = Join-Path $distRoot (Get-ArchiveName -RuntimeIdentifier $runtimeIdentifier)
    if (Test-Path $archivePath) {
        Remove-Item $archivePath -Force
    }

    New-PackageArchive -RuntimeIdentifier $runtimeIdentifier -SourceDirectory $stagingDirectory -ArchivePath $archivePath

    $builtOutputs += [pscustomobject]@{
        Runtime = $runtimeIdentifier
        Directory = $finalDirectory
        Archive = $archivePath
    }
}

Write-Host ""
Write-Host "Packaged outputs:"
foreach ($output in $builtOutputs) {
    Write-Host "  $($output.Runtime)"
    Write-Host "    folder:  $($output.Directory)"
    Write-Host "    archive: $($output.Archive)"
}
