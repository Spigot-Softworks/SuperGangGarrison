[CmdletBinding()]
param(
    [ValidateSet("stable", "beta")]
    [string]$Channel = "stable",

    [string[]]$Platforms = @("win-x64", "linux-x64"),

    [string]$UpdateBaseUrl = "https://api.superganggarrison.com/updates",

    [string]$GitHubRepository = "",

    [string]$ExcludeReleaseTag = "",

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

function Get-ReleaseManifestAssetName {
    param([string]$RuntimeIdentifier)

    switch ($RuntimeIdentifier) {
        "win-x64" { return "OpenGarrison-Windows-x64.latest.json" }
        "linux-x64" { return "OpenGarrison-Linux-x64.latest.json" }
        "osx-x64" { return "OpenGarrison-macOS-x64.latest.json" }
        "osx-arm64" { return "OpenGarrison-macOS-arm64.latest.json" }
        default { return "OpenGarrison-$RuntimeIdentifier.latest.json" }
    }
}

$previousReleases = @()
if (-not [string]::IsNullOrWhiteSpace($GitHubRepository)) {
    if ($GitHubRepository -notmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$') {
        throw "GitHub repository must use the owner/name form: '$GitHubRepository'."
    }

    $headers = @{
        Accept = "application/vnd.github+json"
        "User-Agent" = "OpenGarrison-release"
        "X-GitHub-Api-Version" = "2022-11-28"
    }
    $githubToken = [Environment]::GetEnvironmentVariable("GITHUB_TOKEN")
    if (-not [string]::IsNullOrWhiteSpace($githubToken)) {
        $headers.Authorization = "Bearer $githubToken"
    }

    try {
        $releaseApiUrl = "https://api.github.com/repos/$GitHubRepository/releases?per_page=100"
        $previousReleases = @(Invoke-RestMethod -Uri $releaseApiUrl -Headers $headers -Method Get -TimeoutSec 30 |
            Where-Object {
                -not $_.draft -and
                ([string]::IsNullOrWhiteSpace($ExcludeReleaseTag) -or
                    -not $_.tag_name.Equals($ExcludeReleaseTag, [System.StringComparison]::OrdinalIgnoreCase)) -and
                (($Channel -eq "stable" -and -not $_.prerelease) -or
                    ($Channel -eq "beta" -and $_.prerelease))
            } |
            Sort-Object { [DateTimeOffset]$_.published_at } -Descending)
    }
    catch {
        Write-Warning "Unable to inspect prior GitHub releases: $($_.Exception.Message). Falling back to the update API."
        $previousReleases = @()
    }
}

function Get-PreviousReleaseManifestUrl {
    param([string]$RuntimeIdentifier)

    $assetName = Get-ReleaseManifestAssetName -RuntimeIdentifier $RuntimeIdentifier
    foreach ($release in $previousReleases) {
        $asset = @($release.assets | Where-Object {
            $_.name.Equals($assetName, [System.StringComparison]::OrdinalIgnoreCase)
        } | Select-Object -First 1)
        if ($asset.Count -eq 1 -and -not [string]::IsNullOrWhiteSpace([string]$asset[0].browser_download_url)) {
            return [string]$asset[0].browser_download_url
        }
    }

    return ""
}

$downloaded = 0
foreach ($runtimeIdentifier in $Platforms) {
    $platformSegment = Get-PlatformSegment -RuntimeIdentifier $runtimeIdentifier
    $releaseManifestUrl = Get-PreviousReleaseManifestUrl -RuntimeIdentifier $runtimeIdentifier
    $manifestUrl = if ([string]::IsNullOrWhiteSpace($releaseManifestUrl)) {
        "$($UpdateBaseUrl.TrimEnd('/'))/$platformSegment/$Channel/latest.json"
    }
    else {
        $releaseManifestUrl
    }
    $temporaryPath = ""
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
        if (-not [string]::IsNullOrWhiteSpace($temporaryPath) -and
            (Test-Path -LiteralPath $temporaryPath)) {
            Remove-Item -LiteralPath $temporaryPath -Force -ErrorAction SilentlyContinue
        }
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
