[CmdletBinding()]
param(
    [string[]]$Platforms = @("win-x64", "linux-x64"),
    [switch]$RunTests,
    [switch]$SkipTests
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ($RunTests -or $SkipTests) {
    Write-Host "[package] test flags are ignored; packaging performs publish only."
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$distRoot = Join-Path $repoRoot "dist"
$stagingRoot = Join-Path $distRoot "_staging"
$configuration = "Release"
$projects =
@(
    "Core/OpenGarrison.Core.csproj",
    "Protocol/OpenGarrison.Protocol.csproj",
    "Client/OpenGarrison.Client.csproj",
    "Server/OpenGarrison.Server.csproj",
    "ServerLauncher/OpenGarrison.ServerLauncher.csproj"
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
'@.Replace("`r`n", "`n").Replace("__EXECUTABLE__", $ExecutableName)

    [System.IO.File]::WriteAllText($DestinationPath, $scriptContents, [System.Text.Encoding]::ASCII)
}

function Add-UnixLaunchers {
    param(
        [Parameter(Mandatory = $true)]
        [string]$OutputDirectory
    )

    New-UnixLauncherScript -DestinationPath (Join-Path $OutputDirectory "run-client.sh") -ExecutableName "OG2"
    New-UnixLauncherScript -DestinationPath (Join-Path $OutputDirectory "run-server.sh") -ExecutableName "OG2.Server"
    New-UnixLauncherScript -DestinationPath (Join-Path $OutputDirectory "run-server-launcher.sh") -ExecutableName "OG2.ServerLauncher"
}

function Get-BundledPluginProjects {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepoRoot
    )

    $pluginsRoot = Join-Path $RepoRoot "Plugins"
    if (-not (Test-Path $pluginsRoot)) {
        return @()
    }

    $pluginProjects = Get-ChildItem -Path $pluginsRoot -Recurse -Filter *.csproj -File |
        Where-Object { $_.BaseName -notlike "*.Abstractions" }

    $bundledPlugins = foreach ($project in $pluginProjects) {
        $scope = if ($project.BaseName -like "OpenGarrison.Client.Plugins.*") {
            "Client"
        }
        elseif ($project.BaseName -like "OpenGarrison.Server.Plugins.*") {
            "Server"
        }
        else {
            continue
        }

        $folder = $project.BaseName -replace '^OpenGarrison\.(Client|Server)\.Plugins\.', ''
        if ([string]::IsNullOrWhiteSpace($folder) -or $folder -eq $project.BaseName) {
            $folder = $project.Directory.Name
        }

        [pscustomobject]@{
            Project = $project.FullName
            Scope = $scope
            Folder = $folder
        }
    }

    return $bundledPlugins |
        Sort-Object Scope, Folder
}

function Publish-BundledPlugins {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepoRoot,
        [Parameter(Mandatory = $true)]
        [string]$OutputDirectory
    )

    $bundledPlugins = Get-BundledPluginProjects -RepoRoot $RepoRoot
    foreach ($plugin in $bundledPlugins) {
        $projectPath = $plugin.Project
        $pluginOutputDirectory = Join-Path $OutputDirectory (Join-Path "Plugins\\$($plugin.Scope)" $plugin.Folder)
        New-Item -ItemType Directory -Path $pluginOutputDirectory -Force | Out-Null

        Invoke-DotNet -Arguments @(
            "restore",
            $projectPath
        )

        Invoke-DotNet -Arguments @(
            "publish",
            $projectPath,
            "-c", $configuration,
            "--no-restore",
            "-o", $pluginOutputDirectory
        )
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

    Copy-DirectoryContents -SourceDirectory (Join-Path $repoRoot "Core/Content") -DestinationDirectory (Join-Path $stagingDirectory "Content")
    Copy-DirectoryContents -SourceDirectory (Join-Path $repoRoot "Client/Content") -DestinationDirectory (Join-Path $stagingDirectory "Content")
    Copy-DirectoryContents -SourceDirectory (Join-Path $repoRoot "packaging/config") -DestinationDirectory (Join-Path $stagingDirectory "config")
    Copy-Item (Join-Path $repoRoot "sampleMapRotation.txt") (Join-Path $stagingDirectory "config/sampleMapRotation.txt") -Force
    Copy-Item (Join-Path $repoRoot "packaging/README.txt") (Join-Path $stagingDirectory "README.txt") -Force

    if (Test-IsSelfContainedRuntime -RuntimeIdentifier $runtimeIdentifier) {
        Add-UnixLaunchers -OutputDirectory $stagingDirectory
    }

    Publish-BundledPlugins -RepoRoot $repoRoot -OutputDirectory $stagingDirectory

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
