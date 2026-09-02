[CmdletBinding()]
param(
    [ValidateSet("stable", "beta")]
    [string]$Channel = "stable",

    [string[]]$Platforms = @("win-x64", "linux-x64"),

    [string]$UpdateBaseUrl = "https://api.superganggarrison.com/updates",

    [string]$OutputDirectory = "",

    [switch]$Required
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$resolvedOutputDirectory = if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    Join-Path $repoRoot "dist/delta-bases"
}
else {
    [System.IO.Path]::GetFullPath($OutputDirectory)
}
New-Item -ItemType Directory -Path $resolvedOutputDirectory -Force | Out-Null

function Get-PlatformSegment {
    param([string]$RuntimeIdentifier)

    switch ($RuntimeIdentifier) {
        "win-x64" { return "windows-x64" }
        "linux-x64" { return "linux-x64" }
        "osx-x64" { return "macos-x64" }
        "osx-arm64" { return "macos-arm64" }
        default { return $RuntimeIdentifier }
    }
}

$downloaded = 0
foreach ($runtimeIdentifier in $Platforms) {
    $platformSegment = Get-PlatformSegment -RuntimeIdentifier $runtimeIdentifier
    $manifestUrl = "$($UpdateBaseUrl.TrimEnd('/'))/$platformSegment/$Channel/latest.json"
    try {
        Write-Host "[delta-base] fetching $manifestUrl"
        $manifest = Invoke-RestMethod -Uri $manifestUrl -Method Get -TimeoutSec 30
        $fullPackageProperty = $manifest.PSObject.Properties["fullPackage"]
        $package = if ($null -ne $fullPackageProperty -and
                       $null -ne $fullPackageProperty.Value -and
                       -not [string]::IsNullOrWhiteSpace([string]$fullPackageProperty.Value.url)) {
            $fullPackageProperty.Value
        }
        else {
            [pscustomobject]@{
                url = $manifest.url
                sha256 = $manifest.sha256
                size = $manifest.size
            }
        }

        if ([string]::IsNullOrWhiteSpace([string]$package.url) -or
            [string]::IsNullOrWhiteSpace([string]$package.sha256)) {
            throw "Published manifest has no verifiable full package."
        }

        $packageUri = [System.Uri]::new([System.Uri]::new($manifestUrl), [string]$package.url)
        $runtimeDirectory = Join-Path $resolvedOutputDirectory $runtimeIdentifier
        New-Item -ItemType Directory -Path $runtimeDirectory -Force | Out-Null
        $destinationPath = Join-Path $runtimeDirectory ([System.IO.Path]::GetFileName($packageUri.LocalPath))
        $temporaryPath = $destinationPath + ".download"
        Invoke-WebRequest -Uri $packageUri -OutFile $temporaryPath -TimeoutSec 600

        $downloadedItem = Get-Item -LiteralPath $temporaryPath
        if ([long]$package.size -gt 0 -and $downloadedItem.Length -ne [long]$package.size) {
            throw "Downloaded package size mismatch: expected $($package.size), got $($downloadedItem.Length)."
        }

        $actualSha256 = (Get-FileHash -LiteralPath $temporaryPath -Algorithm SHA256).Hash.ToLowerInvariant()
        if (-not $actualSha256.Equals(([string]$package.sha256).Trim(), [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Downloaded package hash mismatch: expected $($package.sha256), got $actualSha256."
        }

        Move-Item -LiteralPath $temporaryPath -Destination $destinationPath -Force
        $schemaVersionProperty = $manifest.PSObject.Properties["schemaVersion"]
        $manifestVersionProperty = $manifest.PSObject.Properties["version"]
        $packageVersionProperty = $manifest.PSObject.Properties["packageVersion"]
        $channelProperty = $manifest.PSObject.Properties["channel"]
        $publishedManifestVersion = if ($null -eq $manifestVersionProperty) { "" } else { [string]$manifestVersionProperty.Value }
        $publishedPackageVersion = if ($null -eq $packageVersionProperty -or
            [string]::IsNullOrWhiteSpace([string]$packageVersionProperty.Value)) {
            $publishedManifestVersion
        }
        else {
            [string]$packageVersionProperty.Value
        }
        $baseMetadata = [ordered]@{
            schemaVersion = if ($null -eq $schemaVersionProperty) { 1 } else { [int]$schemaVersionProperty.Value }
            version = $publishedManifestVersion
            packageVersion = $publishedPackageVersion
            channel = if ($null -eq $channelProperty) { $Channel } else { [string]$channelProperty.Value }
            sourceManifestUrl = $manifestUrl
            packageUrl = $packageUri.ToString()
            sha256 = $actualSha256
            size = $downloadedItem.Length
        }
        $baseMetadata | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $runtimeDirectory "base.json") -Encoding UTF8
        Write-Host "[delta-base] $runtimeIdentifier ${publishedPackageVersion}: $destinationPath"
        $downloaded += 1
    }
    catch {
        $message = "Unable to prepare delta base for $runtimeIdentifier from '$manifestUrl': $($_.Exception.Message)"
        if ($Required) {
            throw $message
        }

        Write-Warning "$message Full-package publishing will remain available."
    }
}

if ($Required -and $downloaded -ne $Platforms.Count) {
    throw "Only $downloaded of $($Platforms.Count) required delta bases were downloaded."
}

Write-Host "[delta-base] prepared $downloaded base package(s) under $resolvedOutputDirectory"
