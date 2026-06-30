[CmdletBinding()]
param(
    [string[]]$Platforms = @("win-x64", "linux-x64"),
    [string]$Version = "",
    [ValidateSet("stable", "beta")]
    [string]$Channel = "stable",
    [string]$UpdateManifestVersion = "",
    [string]$UpdateManifestFileName = "latest.json",
    [string]$ArchiveNameSuffix = "",
    [string]$ChainedUpdateManifestUrl = "",
    [switch]$LegacyRootLayout,
    [switch]$RunTests,
    [switch]$SkipTests,
    [switch]$IncludeLegacyClrPlugins
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
$appPayloadDirectoryName = "app"
$projects =
@(
    "Updater/OpenGarrison.Updater.csproj",
    "Client/OpenGarrison.Client.csproj",
    "Server/OpenGarrison.Server.csproj",
    "ServerLauncher/OpenGarrison.ServerLauncher.csproj"
)
$payloadProjects =
@(
    "Client/OpenGarrison.Client.csproj",
    "Server/OpenGarrison.Server.csproj",
    "ServerLauncher/OpenGarrison.ServerLauncher.csproj"
)

$excludedPackagedPluginDirectories = @(
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

function Normalize-PackageVersion {
    param(
        [string]$Value
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return ""
    }

    $clean = $Value.Trim()
    if ($clean.StartsWith("refs/tags/", [System.StringComparison]::OrdinalIgnoreCase)) {
        $clean = $clean.Substring("refs/tags/".Length)
    }

    $clean = $clean.TrimStart([char[]]@('v', 'V'))
    $baseVersion = ($clean -split "[-+]", 2)[0]
    $versionParts = $baseVersion.Split(".")
    if ($versionParts.Count -lt 2 -or $versionParts.Count -gt 5) {
        return ""
    }

    foreach ($part in $versionParts) {
        $parsedPart = 0
        if (-not [int]::TryParse($part, [ref]$parsedPart) -or $parsedPart -lt 0) {
            return ""
        }
    }

    return $clean
}

function Get-GitPackageVersion {
    try {
        $description = (& git -C $repoRoot describe --tags --dirty --match "v[0-9]*" 2>$null | Select-Object -First 1)
        if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($description)) {
            return $description
        }
    }
    catch {
    }

    return ""
}

function Get-PackageVersion {
    param(
        [string]$RequestedVersion
    )

    $candidates = @(
        $RequestedVersion,
        $env:OPENGARRISON_PACKAGE_VERSION,
        $env:GITHUB_REF_NAME,
        $env:GITHUB_REF,
        (Get-GitPackageVersion)
    )

    foreach ($candidate in $candidates) {
        $normalized = Normalize-PackageVersion -Value $candidate
        if (-not [string]::IsNullOrWhiteSpace($normalized)) {
            return $normalized
        }
    }

    throw "Package version could not be resolved. Pass -Version explicitly, set OPENGARRISON_PACKAGE_VERSION, or build from a valid version tag."
}

function Get-AssemblyFileVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PackageVersion
    )

    $baseVersion = ($PackageVersion -split "[-+]", 2)[0]
    $parts = [System.Collections.Generic.List[string]]::new()
    foreach ($part in $baseVersion.Split(".")) {
        if ($parts.Count -lt 4) {
            $parts.Add($part)
        }
    }

    while ($parts.Count -lt 4) {
        $parts.Add("0")
    }

    return [string]::Join(".", $parts)
}

function Get-ArchiveNameSuffix {
    param(
        [string]$Value
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return ""
    }

    $suffix = $Value.Trim()
    if ($suffix.Contains("/") -or $suffix.Contains("\")) {
        throw "Archive name suffix '$Value' must be a simple file name segment."
    }

    if ($suffix.IndexOfAny([System.IO.Path]::GetInvalidFileNameChars()) -ge 0) {
        throw "Archive name suffix '$Value' contains invalid file name characters."
    }

    return $suffix
}

function Get-ArchiveName {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RuntimeIdentifier,
        [string]$Suffix = ""
    )

    $archiveName = switch ($RuntimeIdentifier) {
        "win-x64" { "OpenGarrison-Windows-x64.zip" }
        "linux-x64" { "OpenGarrison-Linux-x64.tar.gz" }
        "osx-x64" { "OpenGarrison-macOS-x64.tar.gz" }
        "osx-arm64" { "OpenGarrison-macOS-arm64.tar.gz" }
        default { "OpenGarrison-$RuntimeIdentifier.tar.gz" }
    }

    if ([string]::IsNullOrWhiteSpace($Suffix)) {
        return $archiveName
    }

    if ($archiveName.EndsWith(".tar.gz", [System.StringComparison]::OrdinalIgnoreCase)) {
        return "$($archiveName.Substring(0, $archiveName.Length - ".tar.gz".Length))-$Suffix.tar.gz"
    }

    return "$([System.IO.Path]::GetFileNameWithoutExtension($archiveName))-$Suffix$([System.IO.Path]::GetExtension($archiveName))"
}

function Get-UpdatePlatformSegment {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RuntimeIdentifier
    )

    switch ($RuntimeIdentifier) {
        "win-x64" { return "windows-x64" }
        "linux-x64" { return "linux-x64" }
        "osx-x64" { return "macos-x64" }
        "osx-arm64" { return "macos-arm64" }
        default { return $RuntimeIdentifier }
    }
}

function Get-RuntimeChainedUpdateManifestUrl {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RuntimeIdentifier,
        [string]$Template
    )

    if ([string]::IsNullOrWhiteSpace($Template)) {
        return ""
    }

    $platformSegment = Get-UpdatePlatformSegment -RuntimeIdentifier $RuntimeIdentifier
    $url = $Template.Replace("{platform}", $platformSegment)
    return $url.Replace("{runtime}", $RuntimeIdentifier)
}

function Get-UpdateManifestFileName {
    param(
        [string]$Value
    )

    $fileName = if ([string]::IsNullOrWhiteSpace($Value)) {
        "latest.json"
    }
    else {
        $Value.Trim()
    }

    if ($fileName -eq "." -or $fileName -eq ".." -or $fileName.Contains("/") -or $fileName.Contains("\")) {
        throw "Update manifest file name '$Value' must be a simple .json file name."
    }

    if ($fileName.IndexOfAny([System.IO.Path]::GetInvalidFileNameChars()) -ge 0) {
        throw "Update manifest file name '$Value' contains invalid file name characters."
    }

    if (-not $fileName.EndsWith(".json", [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Update manifest file name '$Value' must end with .json."
    }

    return $fileName
}

function Test-IsSelfContainedRuntime {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RuntimeIdentifier
    )

    return $RuntimeIdentifier -ne "win-x64"
}

function Test-IsWindowsRuntime {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RuntimeIdentifier
    )

    return $RuntimeIdentifier.StartsWith("win-", [System.StringComparison]::OrdinalIgnoreCase)
}

function Get-RuntimeExecutableName {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RuntimeIdentifier,
        [Parameter(Mandatory = $true)]
        [string]$BaseName
    )

    if (Test-IsWindowsRuntime -RuntimeIdentifier $RuntimeIdentifier) {
        return "$BaseName.exe"
    }

    return $BaseName
}

function Get-RootLauncherExecutableName {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RuntimeIdentifier
    )

    if (Test-IsWindowsRuntime -RuntimeIdentifier $RuntimeIdentifier) {
        return "Super Gang Garrison.exe"
    }

    return Get-RuntimeExecutableName -RuntimeIdentifier $RuntimeIdentifier -BaseName "OG2"
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

    if ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Windows)) {
        $portableUnixArchiveCreated = New-PortableUnixTarArchive -SourceDirectory $SourceDirectory -ArchivePath $ArchivePath
        if ($portableUnixArchiveCreated) {
            return
        }
    }

    & tar -C $SourceDirectory -czf $ArchivePath .
    if ($LASTEXITCODE -ne 0) {
        throw "tar failed while creating '$ArchivePath' for runtime '$RuntimeIdentifier'."
    }
}

function Write-UpdateManifest {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RuntimeIdentifier,
        [Parameter(Mandatory = $true)]
        [string]$ArchivePath,
        [Parameter(Mandatory = $true)]
        [string]$PackageVersion,
        [Parameter(Mandatory = $true)]
        [string]$ManifestVersion,
        [Parameter(Mandatory = $true)]
        [string]$ReleaseChannel,
        [Parameter(Mandatory = $true)]
        [string]$ManifestFileName
    )

    $platformSegment = Get-UpdatePlatformSegment -RuntimeIdentifier $RuntimeIdentifier
    $manifestDirectory = Join-Path $repoRoot "services/opengarrison-api/updates/$platformSegment/$ReleaseChannel"
    New-Item -ItemType Directory -Path $manifestDirectory -Force | Out-Null

    $manifestPath = Join-Path $manifestDirectory $ManifestFileName
    $archiveItem = Get-Item $ArchivePath
    $manifest = [ordered]@{
        version = $ManifestVersion
        packageVersion = $PackageVersion
        channel = $ReleaseChannel
        url = $archiveItem.Name
        sha256 = (Get-FileHash -Path $ArchivePath -Algorithm SHA256).Hash.ToLowerInvariant()
        size = $archiveItem.Length
        minLauncherVersion = "0.1.0"
        notesUrl = ""
    }

    $manifest | ConvertTo-Json | Set-Content -Path $manifestPath -Encoding UTF8
    return $manifestPath
}

function New-PortableUnixTarArchive {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourceDirectory,
        [Parameter(Mandatory = $true)]
        [string]$ArchivePath
    )

    $python = Get-Command python -ErrorAction SilentlyContinue
    if ($python -eq $null) {
        return $false
    }

    $pythonScript = @'
import sys
import tarfile
from pathlib import Path

source = Path(sys.argv[1])
archive = Path(sys.argv[2])
executable_names = {
    'OG2',
    'OG2.Game',
    'OG2.Launcher',
    'OG2.Server',
    'OG2.ServerLauncher',
    'OG2.Updater',
}

def normalize(info, path):
    name = path.name
    info.uid = 0
    info.gid = 0
    info.uname = ''
    info.gname = ''
    if info.isdir():
        info.mode = 0o755
    elif name in executable_names or name.endswith('.sh'):
        info.mode = 0o755
    else:
        info.mode = 0o644
    return info

temporary_archive = archive.with_suffix(archive.suffix + '.tmp')
if temporary_archive.exists():
    temporary_archive.unlink()

with tarfile.open(temporary_archive, 'w:gz', compresslevel=6) as tar:
    for path in sorted(source.rglob('*'), key=lambda item: item.relative_to(source).as_posix().lower()):
        arcname = './' + path.relative_to(source).as_posix()
        tar.add(path, arcname=arcname, recursive=False, filter=lambda info, current=path: normalize(info, current))

if archive.exists():
    archive.unlink()
temporary_archive.replace(archive)
'@

    & $python.Source -c $pythonScript $SourceDirectory $ArchivePath
    if ($LASTEXITCODE -ne 0) {
        throw "python tar packaging failed while creating '$ArchivePath'."
    }

    return $true
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

function Invoke-GenerateDistributionAssetManifest {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepoRoot,
        [Parameter(Mandatory = $true)]
        [string]$ContentDirectory
    )

    Invoke-DotNet -Arguments @(
        "run",
        "--project",
        (Join-Path $RepoRoot "Tools\BrowserAssetBuilder\OpenGarrison.Tools.BrowserAssetBuilder.csproj"),
        "--",
        $ContentDirectory,
        "--manifest-only"
    )
}

function Invoke-PublishDistributionMaps {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepoRoot,
        [Parameter(Mandatory = $true)]
        [string]$DestinationDirectory
    )

    Invoke-DotNet -Arguments @(
        "run",
        "--project",
        (Join-Path $RepoRoot "Tools\MapPackageBuilder\OpenGarrison.Tools.MapPackageBuilder.csproj"),
        "--",
        (Join-Path $RepoRoot "Maps"),
        (Join-Path $RepoRoot "Core\Content\StockMaps"),
        $DestinationDirectory,
        "--drop-unconverted-legacy-pngs"
    )
}

function Assert-RequiredDistributionMaps {
    param(
        [Parameter(Mandatory = $true)]
        [string]$MapsDirectory,
        [Parameter(Mandatory = $true)]
        [string]$ContentDirectory
    )

    $requiredCustomMaps = @(
        "cp_gully",
        "cp_thundermountain_d",
        "cp_coldfront_v7",
        "cp_docking_v2"
    )

    foreach ($mapName in $requiredCustomMaps) {
        $manifestPath = Join-Path (Join-Path $MapsDirectory $mapName) "$mapName.json"
        if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
            throw "Release maps are missing required packaged custom map '$mapName' at '$manifestPath'."
        }
    }

    $requiredStockMaps = @(
        [pscustomobject]@{ IniKey = "cp_dirtbowl"; RoomName = "Dirtbowl" },
        [pscustomobject]@{ IniKey = "cp_egypt"; RoomName = "Egypt" }
    )

    foreach ($map in $requiredStockMaps) {
        $stockMapPath = Join-Path (Join-Path $ContentDirectory "StockMaps") "$($map.IniKey).png"
        if (-not (Test-Path -LiteralPath $stockMapPath -PathType Leaf)) {
            throw "Release maps are missing required stock map image '$($map.IniKey)' at '$stockMapPath'."
        }

        $roomPath = Join-Path (Join-Path (Join-Path $ContentDirectory "Rooms") "Maps") "$($map.RoomName).xml"
        if (-not (Test-Path -LiteralPath $roomPath -PathType Leaf)) {
            throw "Release maps are missing required stock map room '$($map.RoomName)' at '$roomPath'."
        }
    }

    Write-Host "[package] verified required distribution maps: cp_dirtbowl, cp_gully, cp_thundermountain_d, cp_coldfront_v7, cp_egypt, cp_docking_v2"
}

function Restore-CollisionMaskImages {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepoRoot,
        [Parameter(Mandatory = $true)]
        [string]$ContentDirectory
    )

    $sourceDirectory = Join-Path $repoRoot "Core/Content/Sprites/Collision Maps"
    if (-not (Test-Path $sourceDirectory)) {
        return
    }

    $destinationDirectory = Join-Path $ContentDirectory "Sprites/Collision Maps"
    Copy-DirectoryContents -SourceDirectory $sourceDirectory -DestinationDirectory $destinationDirectory
}

function Test-IsPathWithinDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$Directory
    )

    $resolvedPath = [System.IO.Path]::GetFullPath($Path)
    $resolvedDirectory = [System.IO.Path]::GetFullPath($Directory)
    if (-not $resolvedDirectory.EndsWith([System.IO.Path]::DirectorySeparatorChar)) {
        $resolvedDirectory += [System.IO.Path]::DirectorySeparatorChar
    }

    return $resolvedPath.StartsWith($resolvedDirectory, [System.StringComparison]::OrdinalIgnoreCase)
}

function Remove-PackagedContentResidue {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ContentDirectory
    )

    if (-not (Test-Path $ContentDirectory)) {
        return
    }

    $contentRoot = [System.IO.Path]::GetFullPath($ContentDirectory)
    $removedDirectories = 0
    $removedFiles = 0

    $knownSourceDirectories = @(
        "BotNavScoreRoutes",
        "Builder",
        "MotionProof",
        "Objects",
        "Paths",
        "Scripts",
        "Time Lines",
        "TraversalLab"
    )

    foreach ($relativeDirectory in $knownSourceDirectories) {
        $directory = Join-Path $ContentDirectory $relativeDirectory
        if ((Test-Path $directory) -and (Test-IsPathWithinDirectory -Path $directory -Directory $contentRoot)) {
            Remove-Item $directory -Recurse -Force
            $removedDirectories += 1
        }
    }

    $buildDirectoryNames = @("bin", "obj")
    foreach ($directory in Get-ChildItem -Path $ContentDirectory -Directory -Recurse -Force | Sort-Object FullName -Descending) {
        if ($buildDirectoryNames -notcontains $directory.Name) {
            continue
        }

        if (-not (Test-IsPathWithinDirectory -Path $directory.FullName -Directory $contentRoot)) {
            continue
        }

        Remove-Item $directory.FullName -Recurse -Force
        $removedDirectories += 1
    }

    $sourceFilePatterns = @(
        "*.mgcb",
        "*.mgcontent",
        "*.mgstats",
        "*.spritefont",
        "*.fx",
        "*.cs"
    )

    foreach ($pattern in $sourceFilePatterns) {
        foreach ($file in Get-ChildItem -Path $ContentDirectory -File -Recurse -Force -Filter $pattern) {
            if (-not (Test-IsPathWithinDirectory -Path $file.FullName -Directory $contentRoot)) {
                continue
            }

            Remove-Item $file.FullName -Force
            $removedFiles += 1
        }
    }

    $deprecatedGameMakerMetadataDirectories = @(
        "Sprites",
        "Backgrounds",
        "Sounds",
        "Fonts",
        "Paths",
        "Time Lines",
        "Builder"
    )

    foreach ($relativeDirectory in $deprecatedGameMakerMetadataDirectories) {
        $directory = Join-Path $ContentDirectory $relativeDirectory
        if (-not (Test-Path $directory)) {
            continue
        }

        foreach ($file in Get-ChildItem -Path $directory -File -Recurse -Force -Filter "*.xml") {
            if (-not (Test-IsPathWithinDirectory -Path $file.FullName -Directory $contentRoot)) {
                continue
            }

            Remove-Item $file.FullName -Force
            $removedFiles += 1
        }
    }

    $rootConstantsFile = Join-Path $ContentDirectory "Constants.xml"
    if ((Test-Path $rootConstantsFile) -and (Test-IsPathWithinDirectory -Path $rootConstantsFile -Directory $contentRoot)) {
        Remove-Item $rootConstantsFile -Force
        $removedFiles += 1
    }

    $browserOnlyBundlePatterns = @(
        "_browser-bootstrap-assets.zip",
        "_browser-runtime-assets.zip"
    )

    foreach ($pattern in $browserOnlyBundlePatterns) {
        foreach ($file in Get-ChildItem -Path $ContentDirectory -File -Force -Filter $pattern) {
            if (-not (Test-IsPathWithinDirectory -Path $file.FullName -Directory $contentRoot)) {
                continue
            }

            Remove-Item $file.FullName -Force
            $removedFiles += 1
        }
    }

    Write-Host "[package] removed content residue: $removedDirectories directories, $removedFiles files"
}

function Remove-PackagedDesktopWebResidue {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PayloadDirectory
    )

    if (-not (Test-Path $PayloadDirectory)) {
        return
    }

    $payloadRoot = [System.IO.Path]::GetFullPath($PayloadDirectory)
    $contentDirectory = Join-Path $PayloadDirectory "Content"
    $removedDirectories = 0
    $removedFiles = 0

    $browserContentDirectory = Join-Path $contentDirectory "Browser"
    if ((Test-Path -LiteralPath $browserContentDirectory) -and (Test-IsPathWithinDirectory -Path $browserContentDirectory -Directory $payloadRoot)) {
        Remove-Item -LiteralPath $browserContentDirectory -Recurse -Force
        $removedDirectories += 1
    }

    $rootWebFilePatterns = @(
        "web.config",
        "*.staticwebassets*.json"
    )

    foreach ($pattern in $rootWebFilePatterns) {
        foreach ($file in Get-ChildItem -Path $PayloadDirectory -File -Force -Filter $pattern) {
            if (-not (Test-IsPathWithinDirectory -Path $file.FullName -Directory $payloadRoot)) {
                continue
            }

            Remove-Item -LiteralPath $file.FullName -Force
            $removedFiles += 1
        }
    }

    if (Test-Path $contentDirectory) {
        $contentRoot = [System.IO.Path]::GetFullPath($contentDirectory)
        $browserOnlyContentFileNames = @(
            "_browser-bootstrap-assets.zip",
            "_browser-runtime-assets.zip",
            "_browser-pack-assets.zip",
            "_browser-pack-definition.json"
        )

        foreach ($fileName in $browserOnlyContentFileNames) {
            foreach ($file in Get-ChildItem -Path $contentDirectory -File -Recurse -Force -Filter $fileName) {
                if (-not (Test-IsPathWithinDirectory -Path $file.FullName -Directory $contentRoot)) {
                    continue
                }

                Remove-Item -LiteralPath $file.FullName -Force
                $removedFiles += 1
            }
        }
    }

    Write-Host "[package] removed desktop web residue: $removedDirectories directories, $removedFiles files"
}

function Remove-PackagedDebugResidue {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PayloadDirectory
    )

    if (-not (Test-Path $PayloadDirectory)) {
        return
    }

    $payloadRoot = [System.IO.Path]::GetFullPath($PayloadDirectory)
    $removedFiles = 0

    foreach ($file in Get-ChildItem -Path $PayloadDirectory -File -Recurse -Force -Filter "*.pdb") {
        if (-not (Test-IsPathWithinDirectory -Path $file.FullName -Directory $payloadRoot)) {
            continue
        }

        Remove-Item -LiteralPath $file.FullName -Force
        $removedFiles += 1
    }

    $runtimeDiagnosticFileNames = @(
        "createdump",
        "libmscordaccore.so",
        "libmscordbi.so",
        "mscordaccore.dll",
        "mscordbi.dll"
    )

    foreach ($fileName in $runtimeDiagnosticFileNames) {
        $diagnosticFile = Join-Path $PayloadDirectory $fileName
        if ((Test-Path -LiteralPath $diagnosticFile -PathType Leaf) -and (Test-IsPathWithinDirectory -Path $diagnosticFile -Directory $payloadRoot)) {
            Remove-Item -LiteralPath $diagnosticFile -Force
            $removedFiles += 1
        }
    }

    Write-Host "[package] removed debug residue: $removedFiles files"
}

function Convert-PackagedBotBrainJsonAssetsToGzip {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ContentDirectory
    )

    if (-not (Test-Path $ContentDirectory)) {
        return
    }

    Add-Type -AssemblyName System.IO.Compression

    $contentRoot = [System.IO.Path]::GetFullPath($ContentDirectory)
    $directories = @(
        "BotBrainNav",
        "BotBrainProofGraphs",
        "BotBrainCorridors",
        "BotBrainTapes"
    )

    $compressedFiles = 0
    $rawBytes = 0L
    $compressedBytes = 0L

    foreach ($relativeDirectory in $directories) {
        $directory = Join-Path $ContentDirectory $relativeDirectory
        if (-not (Test-Path $directory)) {
            continue
        }

        foreach ($file in Get-ChildItem -Path $directory -File -Recurse -Force -Filter "*.json") {
            if (-not (Test-IsPathWithinDirectory -Path $file.FullName -Directory $contentRoot)) {
                continue
            }

            $compressedPath = "$($file.FullName).gz"
            if (Test-Path $compressedPath) {
                Remove-Item $compressedPath -Force
            }

            $sourceStream = [System.IO.File]::OpenRead($file.FullName)
            try {
                $destinationStream = [System.IO.File]::Create($compressedPath)
                try {
                    $gzipStream = [System.IO.Compression.GZipStream]::new($destinationStream, [System.IO.Compression.CompressionMode]::Compress)
                    try {
                        $sourceStream.CopyTo($gzipStream)
                    }
                    finally {
                        $gzipStream.Dispose()
                    }
                }
                finally {
                    $destinationStream.Dispose()
                }
            }
            finally {
                $sourceStream.Dispose()
            }

            $compressedFile = Get-Item $compressedPath
            $rawBytes += $file.Length
            $compressedBytes += $compressedFile.Length
            Remove-Item $file.FullName -Force
            $compressedFiles += 1
        }
    }

    $rawMb = [Math]::Round($rawBytes / 1MB, 2)
    $compressedMb = [Math]::Round($compressedBytes / 1MB, 2)
    Write-Host "[package] compressed BotBrain JSON assets: $compressedFiles files, $rawMb MB -> $compressedMb MB"
}

function Assert-PackagedContentPolicy {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ContentDirectory
    )

    if (-not (Test-Path $ContentDirectory)) {
        return
    }

    $motionProofDirectory = Join-Path $ContentDirectory "MotionProof"
    if (Test-Path $motionProofDirectory) {
        throw "Release content still contains removed MotionProof runtime assets: '$motionProofDirectory'."
    }

    $browserContentDirectory = Join-Path $ContentDirectory "Browser"
    if (Test-Path $browserContentDirectory) {
        throw "Release content still contains browser-only assets: '$browserContentDirectory'."
    }

    $browserOnlyContentFileNames = @(
        "_browser-bootstrap-assets.zip",
        "_browser-runtime-assets.zip",
        "_browser-pack-assets.zip",
        "_browser-pack-definition.json"
    )

    foreach ($fileName in $browserOnlyContentFileNames) {
        $browserOnlyFile = Get-ChildItem -Path $ContentDirectory -File -Recurse -Force -Filter $fileName | Select-Object -First 1
        if ($browserOnlyFile -ne $null) {
            throw "Release content still contains browser-only asset '$($browserOnlyFile.FullName)'."
        }
    }

    foreach ($pngFile in Get-ChildItem -Path $ContentDirectory -File -Recurse -Force -Filter "*.png") {
        $stream = [System.IO.File]::OpenRead($pngFile.FullName)
        try {
            if ($stream.Length -lt 8) {
                throw "Release PNG asset '$($pngFile.FullName)' is too small to be a valid PNG."
            }

            $signature = [byte[]]::new(8)
            $bytesRead = $stream.Read($signature, 0, $signature.Length)
            $expectedSignature = [byte[]](0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A)
            for ($index = 0; $index -lt $expectedSignature.Length; $index += 1) {
                if ($bytesRead -ne $expectedSignature.Length -or $signature[$index] -ne $expectedSignature[$index]) {
                    throw "Release PNG asset '$($pngFile.FullName)' does not have a PNG signature."
                }
            }
        }
        finally {
            $stream.Dispose()
        }
    }

    foreach ($relativeDirectory in @("BotBrainNav", "BotBrainProofGraphs", "BotBrainCorridors", "BotBrainTapes")) {
        $directory = Join-Path $ContentDirectory $relativeDirectory
        if (-not (Test-Path $directory)) {
            continue
        }

        $uncompressedJson = Get-ChildItem -Path $directory -File -Recurse -Force -Filter "*.json" | Select-Object -First 1
        if ($uncompressedJson -ne $null) {
            throw "Release content contains uncompressed BotBrain JSON asset '$($uncompressedJson.FullName)'. Expected packaged .json.gz assets."
        }
    }

    $contentRoot = [System.IO.Path]::GetFullPath($ContentDirectory)
    $stockPackDirectory = Join-Path $ContentDirectory "Gameplay\stock.gg2"
    $stockSpriteDirectory = Join-Path $ContentDirectory "Gameplay\stock.gg2\sprites"
    if (-not (Test-Path $stockSpriteDirectory)) {
        throw "Release content is missing stock gameplay sprite definitions: '$stockSpriteDirectory'."
    }

    $stockSpriteDefinitionFiles = @(Get-ChildItem -Path $stockSpriteDirectory -File -Force -Filter "*.json")
    if ($stockSpriteDefinitionFiles.Count -eq 0) {
        throw "Release content contains no stock gameplay sprite definitions in '$stockSpriteDirectory'."
    }

    foreach ($spriteDefinitionFile in $stockSpriteDefinitionFiles) {
        $spriteDefinition = Get-Content -LiteralPath $spriteDefinitionFile.FullName -Raw | ConvertFrom-Json
        $framePathsProperty = $spriteDefinition.PSObject.Properties.Match("framePaths") | Select-Object -First 1
        if ($framePathsProperty -eq $null) {
            continue
        }

        foreach ($framePathValue in @($framePathsProperty.Value)) {
            $framePath = [string]$framePathValue
            if ([string]::IsNullOrWhiteSpace($framePath)) {
                continue
            }

            $normalizedFramePath = $framePath.Replace("/", [string][System.IO.Path]::DirectorySeparatorChar)
            $contentPrefix = "Content$([System.IO.Path]::DirectorySeparatorChar)"
            $absoluteFramePath = if ($normalizedFramePath.StartsWith($contentPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
                Join-Path $ContentDirectory $normalizedFramePath.Substring($contentPrefix.Length)
            }
            else {
                Join-Path $stockPackDirectory $normalizedFramePath
            }

            if (-not (Test-IsPathWithinDirectory -Path $absoluteFramePath -Directory $contentRoot)) {
                throw "Release sprite definition '$($spriteDefinitionFile.FullName)' references frame outside Content: '$framePath'."
            }

            if (-not (Test-Path -LiteralPath $absoluteFramePath -PathType Leaf)) {
                throw "Release sprite definition '$($spriteDefinitionFile.FullName)' references missing frame asset '$framePath'."
            }
        }
    }
}

function Assert-PackagedDesktopPayloadPolicy {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PayloadDirectory
    )

    if (-not (Test-Path $PayloadDirectory)) {
        return
    }

    $rootWebFilePatterns = @(
        "web.config",
        "*.staticwebassets*.json"
    )

    foreach ($pattern in $rootWebFilePatterns) {
        $webFile = Get-ChildItem -Path $PayloadDirectory -File -Force -Filter $pattern | Select-Object -First 1
        if ($webFile -ne $null) {
            throw "Release payload still contains static-web artifact '$($webFile.FullName)'."
        }
    }
}

function New-UnixLauncherScript {
    param(
        [Parameter(Mandatory = $true)]
        [string]$DestinationPath,
        [Parameter(Mandatory = $true)]
        [string]$ExecutableName,
        [string]$WorkingDirectory = ""
    )

    $workingDirectoryCommand = if ([string]::IsNullOrWhiteSpace($WorkingDirectory)) {
        'cd "$SCRIPT_DIR"'
    }
    else {
        "cd `"`$SCRIPT_DIR/$WorkingDirectory`""
    }

    $scriptContents = @'
#!/bin/sh
set -eu

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
__WORKING_DIRECTORY_COMMAND__
chmod +x "./__EXECUTABLE__"
if [ -f "./OG2.Game" ]; then
    chmod +x "./OG2.Game"
fi
exec "./__EXECUTABLE__" "$@"
'@.
        Replace("`r`n", "`n").
        Replace("__WORKING_DIRECTORY_COMMAND__", $workingDirectoryCommand).
        Replace("__EXECUTABLE__", $ExecutableName)

    [System.IO.File]::WriteAllText($DestinationPath, $scriptContents, [System.Text.Encoding]::ASCII)
}

function New-UnixRootUpdaterEntrypoint {
    param(
        [Parameter(Mandatory = $true)]
        [string]$OutputDirectory,
        [Parameter(Mandatory = $true)]
        [string]$PayloadSubdirectory,
        [Parameter(Mandatory = $true)]
        [string]$RootEntrypointName,
        [Parameter(Mandatory = $true)]
        [string]$UpdaterExecutableName
    )

    $rootEntrypointPath = Join-Path $OutputDirectory $RootEntrypointName
    $scriptContents = @'
#!/bin/sh
set -eu

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
cd "$SCRIPT_DIR/__PAYLOAD_SUBDIRECTORY__"
chmod +x "./__UPDATER_EXECUTABLE__"
exec "./__UPDATER_EXECUTABLE__" "$@"
'@.
        Replace("`r`n", "`n").
        Replace("__PAYLOAD_SUBDIRECTORY__", $PayloadSubdirectory).
        Replace("__UPDATER_EXECUTABLE__", $UpdaterExecutableName)

    [System.IO.File]::WriteAllText($rootEntrypointPath, $scriptContents, [System.Text.Encoding]::ASCII)
    Set-UnixExecutable -Path $rootEntrypointPath
}

function Set-UnixExecutable {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path $Path)) {
        return
    }

    if ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Windows)) {
        return
    }

    $chmod = Get-Command chmod -ErrorAction SilentlyContinue
    if ($chmod -eq $null) {
        return
    }

    & chmod +x $Path
    if ($LASTEXITCODE -ne 0) {
        throw "chmod failed for '$Path'."
    }
}

function Set-ClientEntrypointLayout {
    param(
        [Parameter(Mandatory = $true)]
        [string]$OutputDirectory,
        [Parameter(Mandatory = $true)]
        [string]$RuntimeIdentifier
    )

    $clientExecutableName = Get-RuntimeExecutableName -RuntimeIdentifier $RuntimeIdentifier -BaseName "OG2"
    $gameExecutableName = Get-RuntimeExecutableName -RuntimeIdentifier $RuntimeIdentifier -BaseName "OG2.Game"
    $updaterExecutableName = Get-RuntimeExecutableName -RuntimeIdentifier $RuntimeIdentifier -BaseName "OG2.Updater"
    $legacyLauncherExecutableName = Get-RuntimeExecutableName -RuntimeIdentifier $RuntimeIdentifier -BaseName "OG2.Launcher"

    $clientExecutablePath = Join-Path $OutputDirectory $clientExecutableName
    $gameExecutablePath = Join-Path $OutputDirectory $gameExecutableName
    $updaterExecutablePath = Join-Path $OutputDirectory $updaterExecutableName
    $legacyLauncherExecutablePath = Join-Path $OutputDirectory $legacyLauncherExecutableName

    if (-not (Test-Path -LiteralPath $clientExecutablePath)) {
        throw "Package is missing client executable '$clientExecutableName'."
    }

    if (-not (Test-Path -LiteralPath $updaterExecutablePath)) {
        throw "Package is missing updater executable '$updaterExecutableName'."
    }

    Copy-Item -LiteralPath $clientExecutablePath -Destination $gameExecutablePath -Force
    Copy-Item -LiteralPath $updaterExecutablePath -Destination $clientExecutablePath -Force
    Copy-Item -LiteralPath $updaterExecutablePath -Destination $legacyLauncherExecutablePath -Force

    if (-not (Test-IsWindowsRuntime -RuntimeIdentifier $RuntimeIdentifier)) {
        Set-UnixExecutable -Path $clientExecutablePath
        Set-UnixExecutable -Path $gameExecutablePath
        Set-UnixExecutable -Path $updaterExecutablePath
        Set-UnixExecutable -Path $legacyLauncherExecutablePath
    }

    Write-Host "[package] client entrypoint: $clientExecutableName launches updater; game binary is $gameExecutableName; compatibility updater helper is $legacyLauncherExecutableName"
}

function Set-CleanClientPayloadEntrypointLayout {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PayloadDirectory,
        [Parameter(Mandatory = $true)]
        [string]$RuntimeIdentifier
    )

    $clientExecutableName = Get-RuntimeExecutableName -RuntimeIdentifier $RuntimeIdentifier -BaseName "OG2"
    $gameExecutableName = Get-RuntimeExecutableName -RuntimeIdentifier $RuntimeIdentifier -BaseName "OG2.Game"
    $serverExecutableName = Get-RuntimeExecutableName -RuntimeIdentifier $RuntimeIdentifier -BaseName "OG2.Server"
    $serverLauncherExecutableName = Get-RuntimeExecutableName -RuntimeIdentifier $RuntimeIdentifier -BaseName "OG2.ServerLauncher"

    $clientExecutablePath = Join-Path $PayloadDirectory $clientExecutableName
    $gameExecutablePath = Join-Path $PayloadDirectory $gameExecutableName

    if (-not (Test-Path -LiteralPath $clientExecutablePath)) {
        throw "Package is missing client executable '$clientExecutableName'."
    }

    Copy-Item -LiteralPath $clientExecutablePath -Destination $gameExecutablePath -Force
    Remove-Item -LiteralPath $clientExecutablePath -Force

    if (-not (Test-IsWindowsRuntime -RuntimeIdentifier $RuntimeIdentifier)) {
        foreach ($executableName in @($gameExecutableName, $serverExecutableName, $serverLauncherExecutableName)) {
            Set-UnixExecutable -Path (Join-Path $PayloadDirectory $executableName)
        }
    }

    Write-Host "[package] clean client payload: game binary is $appPayloadDirectoryName/$gameExecutableName; root launcher is published separately"
}

function Publish-RootUpdaterEntrypoint {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepoRoot,
        [Parameter(Mandatory = $true)]
        [string]$OutputDirectory,
        [Parameter(Mandatory = $true)]
        [string]$PayloadDirectory,
        [Parameter(Mandatory = $true)]
        [string]$ScratchDirectory,
        [Parameter(Mandatory = $true)]
        [string]$RuntimeIdentifier,
        [Parameter(Mandatory = $true)]
        [string]$AssemblyFileVersion,
        [Parameter(Mandatory = $true)]
        [string]$PackageVersion
    )

    $publishedUpdaterName = Get-RuntimeExecutableName -RuntimeIdentifier $RuntimeIdentifier -BaseName "OG2.Updater"
    $rootEntrypointName = Get-RootLauncherExecutableName -RuntimeIdentifier $RuntimeIdentifier

    if (-not (Test-IsWindowsRuntime -RuntimeIdentifier $RuntimeIdentifier)) {
        $payloadUpdaterPath = Join-Path $PayloadDirectory $publishedUpdaterName
        if (-not (Test-Path -LiteralPath $payloadUpdaterPath)) {
            throw "Package is missing payload updater executable '$publishedUpdaterName'."
        }

        Set-UnixExecutable -Path $payloadUpdaterPath
        New-UnixRootUpdaterEntrypoint `
            -OutputDirectory $OutputDirectory `
            -PayloadSubdirectory $appPayloadDirectoryName `
            -RootEntrypointName $rootEntrypointName `
            -UpdaterExecutableName $publishedUpdaterName
        Write-Host "[package] clean root entrypoint: $rootEntrypointName launches $appPayloadDirectoryName/$publishedUpdaterName"
        return
    }

    if (Test-Path $ScratchDirectory) {
        Remove-Item $ScratchDirectory -Recurse -Force
    }

    New-Item -ItemType Directory -Path $ScratchDirectory -Force | Out-Null

    $updaterProjectPath = Join-Path $RepoRoot "Updater/OpenGarrison.Updater.csproj"
    $selfContained = Test-IsSelfContainedRuntime -RuntimeIdentifier $RuntimeIdentifier
    Invoke-DotNet -Arguments @(
        "restore",
        $updaterProjectPath,
        "-r", $RuntimeIdentifier
    )

    $selfContainedArguments = if ($selfContained) {
        @("--self-contained", "true")
    }
    else {
        @("--no-self-contained", "-p:SelfContained=false")
    }

    $commonPublishArguments = @(
        "--no-restore",
        "/nr:false",
        "/m:1",
        "-p:PublishSingleFile=true",
        "-p:DebugType=None",
        "-p:DebugSymbols=false",
        "-p:Version=$AssemblyFileVersion",
        "-p:AssemblyVersion=$AssemblyFileVersion",
        "-p:FileVersion=$AssemblyFileVersion",
        "-p:InformationalVersion=$PackageVersion",
        "-p:IncludeSourceRevisionInInformationalVersion=false",
        "-o", $ScratchDirectory
    )

    $publishArguments = @(
        "publish",
        $updaterProjectPath,
        "-c", $configuration,
        "-r", $RuntimeIdentifier
    ) + $selfContainedArguments + $commonPublishArguments
    Invoke-DotNet -Arguments $publishArguments

    $publishedUpdaterPath = Join-Path $ScratchDirectory $publishedUpdaterName
    if (-not (Test-Path -LiteralPath $publishedUpdaterPath)) {
        throw "Package is missing published updater executable '$publishedUpdaterName'."
    }

    $rootEntrypointNames = @($rootEntrypointName)

    foreach ($entrypointName in $rootEntrypointNames) {
        $rootEntrypointPath = Join-Path $OutputDirectory $entrypointName
        Copy-Item -LiteralPath $publishedUpdaterPath -Destination $rootEntrypointPath -Force
    }

    if (-not (Test-IsWindowsRuntime -RuntimeIdentifier $RuntimeIdentifier)) {
        foreach ($entrypointName in $rootEntrypointNames) {
            Set-UnixExecutable -Path (Join-Path $OutputDirectory $entrypointName)
        }
    }

    Remove-Item $ScratchDirectory -Recurse -Force
    Write-Host "[package] clean root entrypoint: $rootEntrypointName is a single-file updater helper"
}

function Add-UnixLaunchers {
    param(
        [Parameter(Mandatory = $true)]
        [string]$OutputDirectory,
        [string]$PayloadSubdirectory = ""
    )

    $launchers = @(
        @{ Script = "run-client.sh"; Executable = "OG2" },
        @{ Script = "run-server.sh"; Executable = "OG2.Server" },
        @{ Script = "run-server-launcher.sh"; Executable = "OG2.ServerLauncher" }
    )

    foreach ($launcher in $launchers) {
        $scriptPath = Join-Path $OutputDirectory $launcher.Script
        $rootExecutablePath = Join-Path $OutputDirectory $launcher.Executable
        $payloadExecutablePath = if ([string]::IsNullOrWhiteSpace($PayloadSubdirectory)) {
            $rootExecutablePath
        }
        else {
            Join-Path $OutputDirectory (Join-Path $PayloadSubdirectory $launcher.Executable)
        }

        $workingDirectory = if (Test-Path -LiteralPath $rootExecutablePath) {
            ""
        }
        elseif (Test-Path -LiteralPath $payloadExecutablePath) {
            $PayloadSubdirectory
        }
        else {
            ""
        }

        $executablePath = if ([string]::IsNullOrWhiteSpace($workingDirectory)) {
            $rootExecutablePath
        }
        else {
            $payloadExecutablePath
        }

        New-UnixLauncherScript -DestinationPath $scriptPath -ExecutableName $launcher.Executable -WorkingDirectory $workingDirectory
        Set-UnixExecutable -Path $scriptPath
        Set-UnixExecutable -Path $executablePath
    }
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
        [string]$OutputDirectory,
        [Parameter(Mandatory = $true)]
        [string]$RuntimeIdentifier,
        [Parameter(Mandatory = $true)]
        [string]$RootDirectoryName
    )

    $sharedRootFiles = @{}
    foreach ($rootFile in Get-ChildItem $OutputDirectory -File) {
        $sharedRootFiles[$rootFile.Name] = $true
    }

    $bundledPlugins = Get-BundledPluginProjects -RepoRoot $RepoRoot
    foreach ($plugin in $bundledPlugins) {
        $projectPath = $plugin.Project
        $pluginOutputDirectory = Join-Path $OutputDirectory (Join-Path "$RootDirectoryName\\$($plugin.Scope)" $plugin.Folder)
        New-Item -ItemType Directory -Path $pluginOutputDirectory -Force | Out-Null

        Invoke-DotNet -Arguments @(
            "restore",
            $projectPath,
            "-r", $RuntimeIdentifier
        )

        Invoke-DotNet -Arguments @(
            "publish",
            $projectPath,
            "-c", $configuration,
            "-r", $RuntimeIdentifier,
            "--self-contained", "false",
            "--no-restore",
            "/nr:false",
            "/m:1",
            "-o", $pluginOutputDirectory
        )

        foreach ($pluginFile in Get-ChildItem $pluginOutputDirectory -File) {
            if ($sharedRootFiles.ContainsKey($pluginFile.Name)) {
                Remove-Item $pluginFile.FullName -Force
            }
        }
    }
}

function Publish-PackagedExamples {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepoRoot,
        [Parameter(Mandatory = $true)]
        [string]$OutputDirectory
    )

    $packagedRoot = Join-Path $RepoRoot "Plugins\Packaged"
    if (-not (Test-Path $packagedRoot)) {
        return
    }

    foreach ($scope in @("Client", "Server")) {
        $sourceScopeDirectory = Join-Path $packagedRoot $scope
        if (-not (Test-Path $sourceScopeDirectory)) {
            continue
        }

        $destinationScopeDirectory = Join-Path $OutputDirectory "Plugins\$scope"
        New-Item -ItemType Directory -Path $destinationScopeDirectory -Force | Out-Null

        foreach ($exampleDirectory in Get-ChildItem -Path $sourceScopeDirectory -Directory) {
            if ($excludedPackagedPluginDirectories -contains $exampleDirectory.Name) {
                continue
            }

            $destinationDirectory = Join-Path $destinationScopeDirectory $exampleDirectory.Name
            if (Test-Path $destinationDirectory) {
                Remove-Item $destinationDirectory -Recurse -Force
            }

            Copy-DirectoryContents -SourceDirectory $exampleDirectory.FullName -DestinationDirectory $destinationDirectory
        }
    }
}

New-Item -ItemType Directory -Path $distRoot -Force | Out-Null
New-Item -ItemType Directory -Path $stagingRoot -Force | Out-Null

$packageVersion = Get-PackageVersion -RequestedVersion $Version
$manifestVersion = if ([string]::IsNullOrWhiteSpace($UpdateManifestVersion)) {
    $packageVersion
}
else {
    $normalizedManifestVersion = Normalize-PackageVersion -Value $UpdateManifestVersion
    if ([string]::IsNullOrWhiteSpace($normalizedManifestVersion)) {
        throw "Update manifest version '$UpdateManifestVersion' is not a valid package version."
    }

    $normalizedManifestVersion
}

$releaseChannel = $Channel.Trim().ToLowerInvariant()
$resolvedUpdateManifestFileName = Get-UpdateManifestFileName -Value $UpdateManifestFileName
$resolvedArchiveNameSuffix = Get-ArchiveNameSuffix -Value $ArchiveNameSuffix
$chainedUpdateManifestUrl = $ChainedUpdateManifestUrl.Trim()
$assemblyFileVersion = Get-AssemblyFileVersion -PackageVersion $packageVersion
Write-Host "[package] version: $packageVersion"
if (-not [string]::Equals($manifestVersion, $packageVersion, [System.StringComparison]::OrdinalIgnoreCase)) {
    Write-Host "[package] update manifest version: $manifestVersion"
}

Write-Host "[package] channel: $releaseChannel"
if (-not [string]::Equals($resolvedUpdateManifestFileName, "latest.json", [System.StringComparison]::OrdinalIgnoreCase)) {
    Write-Host "[package] update manifest file: $resolvedUpdateManifestFileName"
}
if (-not [string]::IsNullOrWhiteSpace($resolvedArchiveNameSuffix)) {
    Write-Host "[package] archive suffix: $resolvedArchiveNameSuffix"
}
if (-not [string]::IsNullOrWhiteSpace($chainedUpdateManifestUrl)) {
    Write-Host "[package] chained update manifest: $chainedUpdateManifestUrl"
}

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
    $updaterScratchDirectory = Join-Path $stagingRoot "$runtimeIdentifier-root-updater"
    if (Test-Path $stagingDirectory) {
        Remove-Item $stagingDirectory -Recurse -Force
    }
    if (Test-Path $updaterScratchDirectory) {
        Remove-Item $updaterScratchDirectory -Recurse -Force
    }

    New-Item -ItemType Directory -Path $stagingDirectory -Force | Out-Null
    $payloadDirectory = if ($LegacyRootLayout) {
        $stagingDirectory
    }
    else {
        Join-Path $stagingDirectory $appPayloadDirectoryName
    }
    New-Item -ItemType Directory -Path $payloadDirectory -Force | Out-Null

    $projectsToPublish = if ($LegacyRootLayout) {
        $projects
    }
    elseif (-not (Test-IsWindowsRuntime -RuntimeIdentifier $runtimeIdentifier)) {
        $payloadProjects + @("Updater/OpenGarrison.Updater.csproj")
    }
    else {
        $payloadProjects
    }

    foreach ($project in $projectsToPublish) {
        $projectPath = Join-Path $repoRoot $project
        $selfContained = if (Test-IsSelfContainedRuntime -RuntimeIdentifier $runtimeIdentifier) { "true" } else { "false" }
        Invoke-DotNet -Arguments @(
            "restore",
            $projectPath,
            "-r", $runtimeIdentifier
        )

        $publishArguments = @(
            "publish",
            $projectPath,
            "-c", $configuration,
            "-r", $runtimeIdentifier,
            "--self-contained", $selfContained,
            "--no-restore",
            "/nr:false",
            "/m:1",
            "-p:OpenGarrisonPackageScriptOwnsContent=true",
            "-p:DebugType=None",
            "-p:DebugSymbols=false",
            "-p:Version=$assemblyFileVersion",
            "-p:AssemblyVersion=$assemblyFileVersion",
            "-p:FileVersion=$assemblyFileVersion",
            "-p:InformationalVersion=$packageVersion",
            "-p:IncludeSourceRevisionInInformationalVersion=false",
            "-o", $payloadDirectory
        )

        Invoke-DotNet -Arguments $publishArguments
    }

    if (-not $LegacyRootLayout) {
        Publish-RootUpdaterEntrypoint `
            -RepoRoot $repoRoot `
            -OutputDirectory $stagingDirectory `
            -PayloadDirectory $payloadDirectory `
            -ScratchDirectory $updaterScratchDirectory `
            -RuntimeIdentifier $runtimeIdentifier `
            -AssemblyFileVersion $assemblyFileVersion `
            -PackageVersion $packageVersion
    }

    Copy-DirectoryContents -SourceDirectory (Join-Path $repoRoot "Core/Content") -DestinationDirectory (Join-Path $payloadDirectory "Content")
    Copy-DirectoryContents -SourceDirectory (Join-Path $repoRoot "Client/Content") -DestinationDirectory (Join-Path $payloadDirectory "Content")
    Invoke-PublishDistributionMaps -RepoRoot $repoRoot -DestinationDirectory (Join-Path $payloadDirectory "Maps")

    Invoke-GenerateDistributionAssetManifest -RepoRoot $repoRoot -ContentDirectory (Join-Path $payloadDirectory "Content")
    Restore-CollisionMaskImages -RepoRoot $repoRoot -ContentDirectory (Join-Path $payloadDirectory "Content")
    Remove-PackagedContentResidue -ContentDirectory (Join-Path $payloadDirectory "Content")
    Remove-PackagedDesktopWebResidue -PayloadDirectory $payloadDirectory
    Remove-PackagedDebugResidue -PayloadDirectory $payloadDirectory
    Convert-PackagedBotBrainJsonAssetsToGzip -ContentDirectory (Join-Path $payloadDirectory "Content")
    Assert-PackagedContentPolicy -ContentDirectory (Join-Path $payloadDirectory "Content")
    Assert-PackagedDesktopPayloadPolicy -PayloadDirectory $payloadDirectory
    Assert-RequiredDistributionMaps -MapsDirectory (Join-Path $payloadDirectory "Maps") -ContentDirectory (Join-Path $payloadDirectory "Content")
    Copy-DirectoryContents -SourceDirectory (Join-Path $repoRoot "packaging/config") -DestinationDirectory (Join-Path $payloadDirectory "config")
    Copy-Item (Join-Path $repoRoot "Client/practice-bot-names.txt") (Join-Path $payloadDirectory "config/practice-bot-names.txt") -Force
    Copy-Item (Join-Path $repoRoot "sampleMapRotation.txt") (Join-Path $payloadDirectory "config/sampleMapRotation.txt") -Force
    Copy-Item (Join-Path $repoRoot "packaging/README.txt") (Join-Path $stagingDirectory "README.txt") -Force
    Set-Content -Path (Join-Path $stagingDirectory "version.txt") -Value $packageVersion -NoNewline -Encoding ASCII
    Set-Content -Path (Join-Path $stagingDirectory "release-channel.txt") -Value $releaseChannel -NoNewline -Encoding ASCII
    if (-not $LegacyRootLayout) {
        Set-Content -Path (Join-Path $payloadDirectory "version.txt") -Value $packageVersion -NoNewline -Encoding ASCII
        Set-Content -Path (Join-Path $payloadDirectory "release-channel.txt") -Value $releaseChannel -NoNewline -Encoding ASCII
    }
    $runtimeChainedUpdateManifestUrl = Get-RuntimeChainedUpdateManifestUrl -RuntimeIdentifier $runtimeIdentifier -Template $chainedUpdateManifestUrl
    if (-not [string]::IsNullOrWhiteSpace($runtimeChainedUpdateManifestUrl)) {
        Set-Content -Path (Join-Path $stagingDirectory "chained-update-manifest-url.txt") -Value $runtimeChainedUpdateManifestUrl -NoNewline -Encoding ASCII
    }

    if ($LegacyRootLayout) {
        Set-ClientEntrypointLayout -OutputDirectory $stagingDirectory -RuntimeIdentifier $runtimeIdentifier
    }
    else {
        Set-CleanClientPayloadEntrypointLayout -PayloadDirectory $payloadDirectory -RuntimeIdentifier $runtimeIdentifier
    }

    if (-not (Test-IsWindowsRuntime -RuntimeIdentifier $runtimeIdentifier)) {
        $launcherPayloadSubdirectory = if ($LegacyRootLayout) { "" } else { $appPayloadDirectoryName }
        Add-UnixLaunchers -OutputDirectory $stagingDirectory -PayloadSubdirectory $launcherPayloadSubdirectory
    }

    Publish-PackagedExamples -RepoRoot $repoRoot -OutputDirectory $payloadDirectory

    if ($IncludeLegacyClrPlugins) {
        Publish-BundledPlugins `
            -RepoRoot $repoRoot `
            -OutputDirectory $payloadDirectory `
            -RuntimeIdentifier $runtimeIdentifier `
            -RootDirectoryName "LegacyPlugins"
    }

    $finalDirectory = Get-AvailableOutputDirectory -PreferredPath (Join-Path $distRoot $runtimeIdentifier)
    Copy-DirectoryContents -SourceDirectory $stagingDirectory -DestinationDirectory $finalDirectory

    $archivePath = Join-Path $distRoot (Get-ArchiveName -RuntimeIdentifier $runtimeIdentifier -Suffix $resolvedArchiveNameSuffix)
    if (Test-Path $archivePath) {
        Remove-Item $archivePath -Force
    }

    New-PackageArchive -RuntimeIdentifier $runtimeIdentifier -SourceDirectory $stagingDirectory -ArchivePath $archivePath
    $manifestPath = Write-UpdateManifest `
        -RuntimeIdentifier $runtimeIdentifier `
        -ArchivePath $archivePath `
        -PackageVersion $packageVersion `
        -ManifestVersion $manifestVersion `
        -ReleaseChannel $releaseChannel `
        -ManifestFileName $resolvedUpdateManifestFileName

    $builtOutputs += [pscustomobject]@{
        Runtime = $runtimeIdentifier
        Directory = $finalDirectory
        Archive = $archivePath
        Manifest = $manifestPath
    }
}

Write-Host ""
Write-Host "Packaged outputs:"
foreach ($output in $builtOutputs) {
    Write-Host "  $($output.Runtime)"
    Write-Host "    folder:  $($output.Directory)"
    Write-Host "    archive: $($output.Archive)"
    Write-Host "    manifest: $($output.Manifest)"
}

if (Test-Path $stagingRoot) {
    Remove-Item $stagingRoot -Recurse -Force
}
