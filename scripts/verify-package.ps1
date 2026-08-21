[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("win-x64", "linux-x64")]
    [string]$RuntimeIdentifier,

    [Parameter(Mandatory = $true)]
    [string]$ArchivePath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Test-IsPathWithinDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$CandidatePath,

        [Parameter(Mandatory = $true)]
        [string]$Directory
    )

    $candidate = [System.IO.Path]::GetFullPath($CandidatePath)
    $root = [System.IO.Path]::GetFullPath($Directory)
    if ($candidate.Equals($root, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $true
    }

    if (-not $root.EndsWith([System.IO.Path]::DirectorySeparatorChar)) {
        $root += [System.IO.Path]::DirectorySeparatorChar
    }

    return $candidate.StartsWith($root, [System.StringComparison]::OrdinalIgnoreCase)
}

function Assert-RequiredFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root,

        [Parameter(Mandatory = $true)]
        [string]$RelativePath
    )

    $path = Join-Path $Root $RelativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Archive is missing '$RelativePath'."
    }
}

function Assert-FileAbsent {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root,

        [Parameter(Mandatory = $true)]
        [string]$RelativePath
    )

    $path = Join-Path $Root $RelativePath
    if (Test-Path -LiteralPath $path) {
        throw "Archive must not contain '$RelativePath'."
    }
}

$archiveFullPath = (Resolve-Path -LiteralPath $ArchivePath).Path
$tempParent = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
$extractRoot = Join-Path $tempParent "OpenGarrison.PackageVerify.$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $extractRoot -Force | Out-Null

try {
    if ($RuntimeIdentifier -eq "win-x64") {
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        [System.IO.Compression.ZipFile]::ExtractToDirectory($archiveFullPath, $extractRoot)
    }
    else {
        & tar -xzf $archiveFullPath -C $extractRoot
        if ($LASTEXITCODE -ne 0) {
            throw "tar failed while extracting '$archiveFullPath'."
        }
    }

    foreach ($relativePath in @(
        "README.txt",
        "version.txt",
        "release-channel.txt",
        "app/Content/_gamemaker-asset-manifest.json",
        "app/Content/Gameplay/stock.gg2/runtime.json",
        "app/Content/Browser/Manifests/stock-pack-atlas-manifest.json",
        "app/Maps/cp_docking_v2/cp_docking_v2.json",
        "app/Maps/Docking/Docking.json"
    )) {
        Assert-RequiredFile -Root $extractRoot -RelativePath $relativePath
    }

    if ($RuntimeIdentifier -eq "win-x64") {
        foreach ($relativePath in @(
            "Super Gang Garrison.exe",
            "app/OG2.Game.exe",
            "app/OG2.Server.exe",
            "app/OG2.ServerLauncher.exe"
        )) {
            Assert-RequiredFile -Root $extractRoot -RelativePath $relativePath
        }

        foreach ($relativePath in @(
            "app/coreclr.dll",
            "app/hostfxr.dll",
            "app/hostpolicy.dll"
        )) {
            Assert-FileAbsent -Root $extractRoot -RelativePath $relativePath
        }
    }
    else {
        foreach ($relativePath in @(
            "OG2",
            "app/OG2.Game",
            "app/OG2.Server",
            "app/OG2.ServerLauncher",
            "app/OG2.Updater",
            "app/libcoreclr.so",
            "app/libhostfxr.so",
            "app/libhostpolicy.so",
            "app/libmsquic.so",
            "app/libmsquic.so.2",
            "app/libmsquic.bundle.txt"
        )) {
            Assert-RequiredFile -Root $extractRoot -RelativePath $relativePath
        }

        $versionedMsQuicPath = Join-Path $extractRoot "app/libmsquic.so.2"
        $unversionedMsQuicPath = Join-Path $extractRoot "app/libmsquic.so"
        $bundleMetadata = Get-Content -LiteralPath (Join-Path $extractRoot "app/libmsquic.bundle.txt") -Raw
        $versionedHash = (Get-FileHash -LiteralPath $versionedMsQuicPath -Algorithm SHA256).Hash.ToLowerInvariant()
        $unversionedHash = (Get-FileHash -LiteralPath $unversionedMsQuicPath -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($versionedHash -ne $unversionedHash -or $bundleMetadata -notmatch "(?m)^sha256=$versionedHash\s*$") {
            throw "Bundled libmsquic metadata does not match the archived native library."
        }

        $ldd = Get-Command ldd -ErrorAction SilentlyContinue
        if ($null -ne $ldd) {
            $lddOutput = (& $ldd.Source $versionedMsQuicPath 2>&1 | Out-String)
            if ($LASTEXITCODE -ne 0 -or $lddOutput -match "not found") {
                throw "Bundled libmsquic has unresolved native dependencies:`n$lddOutput"
            }
        }
    }

    & (Join-Path $PSScriptRoot "verify-packaged-content.ps1") -Path (Join-Path $extractRoot "app/Content")
    Write-Host "[verify-package] PASS: $RuntimeIdentifier archive '$archiveFullPath'."
}
finally {
    if ((Test-Path -LiteralPath $extractRoot) -and
        (Test-IsPathWithinDirectory -CandidatePath $extractRoot -Directory $tempParent)) {
        Remove-Item -LiteralPath $extractRoot -Recurse -Force
    }
}
