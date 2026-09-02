[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("win-x64", "linux-x64")]
    [string]$RuntimeIdentifier,

    [Parameter(Mandatory = $true)]
    [string]$ArchivePath,

    [string]$BaseArchivePath = "",

    [string]$UpdaterAssemblyPath = ""
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

    $comparison = if ($RuntimeIdentifier -eq "win-x64") {
        [System.StringComparison]::OrdinalIgnoreCase
    }
    else {
        [System.StringComparison]::Ordinal
    }
    $candidate = [System.IO.Path]::GetFullPath($CandidatePath)
    $root = [System.IO.Path]::GetFullPath($Directory)
    if ($candidate.Equals($root, $comparison)) {
        return $true
    }

    if (-not $root.EndsWith([System.IO.Path]::DirectorySeparatorChar)) {
        $root += [System.IO.Path]::DirectorySeparatorChar
    }

    return $candidate.StartsWith($root, $comparison)
}

function Get-NormalizedUpdatePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $normalized = $Path.Trim().Replace('\', '/')
    $segments = @($normalized.Split('/', [System.StringSplitOptions]::RemoveEmptyEntries))
    if ([string]::IsNullOrWhiteSpace($normalized) -or
        $normalized.StartsWith('/') -or
        [System.IO.Path]::IsPathRooted($normalized) -or
        $segments.Count -eq 0 -or
        $segments -contains "." -or
        $segments -contains "..") {
        throw "Delta metadata contains an unsafe path '$Path'."
    }

    $normalized = $segments -join '/'
    $firstSegment = $segments[0]
    if ($firstSegment.Equals("config", [System.StringComparison]::OrdinalIgnoreCase) -or
        $firstSegment.Equals("logs", [System.StringComparison]::OrdinalIgnoreCase) -or
        $firstSegment.Equals("replays", [System.StringComparison]::OrdinalIgnoreCase) -or
        $firstSegment.Equals(".opengarrison-update-transaction", [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Delta metadata targets protected local data '$Path'."
    }

    return $normalized
}

function Get-RequiredProperty {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Object,

        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property -or $null -eq $property.Value) {
        throw "$Description is missing '$Name'."
    }

    return $property.Value
}

function Assert-FileMatchesEntry {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root,

        [Parameter(Mandatory = $true)]
        [object]$Entry,

        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    $relativePath = Get-NormalizedUpdatePath -Path ([string](Get-RequiredProperty -Object $Entry -Name "path" -Description $Description))
    $filePath = [System.IO.Path]::GetFullPath((Join-Path $Root $relativePath))
    if (-not (Test-IsPathWithinDirectory -CandidatePath $filePath -Directory $Root) -or
        -not (Test-Path -LiteralPath $filePath -PathType Leaf)) {
        throw "$Description file is missing or outside its root: '$relativePath'."
    }

    $expectedSize = [long](Get-RequiredProperty -Object $Entry -Name "size" -Description $Description)
    $expectedSha256 = [string](Get-RequiredProperty -Object $Entry -Name "sha256" -Description $Description)
    $actualSize = (Get-Item -LiteralPath $filePath).Length
    if ($expectedSize -lt 0 -or $actualSize -ne $expectedSize) {
        throw "$Description size mismatch for '$relativePath'."
    }

    $actualSha256 = (Get-FileHash -LiteralPath $filePath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ([string]::IsNullOrWhiteSpace($expectedSha256) -or
        -not $actualSha256.Equals($expectedSha256.Trim(), [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "$Description hash mismatch for '$relativePath'."
    }

    return $relativePath
}

function Expand-UpdateArchive {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$Destination
    )

    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    if ($Path.EndsWith(".zip", [System.StringComparison]::OrdinalIgnoreCase)) {
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        [System.IO.Compression.ZipFile]::ExtractToDirectory($Path, $Destination)
        return
    }

    if ($Path.EndsWith(".tar.gz", [System.StringComparison]::OrdinalIgnoreCase) -or
        $Path.EndsWith(".tgz", [System.StringComparison]::OrdinalIgnoreCase)) {
        & tar -xzf $Path -C $Destination
        if ($LASTEXITCODE -ne 0) {
            throw "tar failed while extracting '$Path'."
        }
        return
    }

    throw "Unsupported update archive '$Path'."
}

function Resolve-PackageRoot {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ExtractDirectory
    )

    if (Test-Path -LiteralPath (Join-Path $ExtractDirectory "version.txt") -PathType Leaf) {
        return $ExtractDirectory
    }

    $children = @(Get-ChildItem -LiteralPath $ExtractDirectory -Directory)
    if ($children.Count -eq 1 -and
        (Test-Path -LiteralPath (Join-Path $children[0].FullName "version.txt") -PathType Leaf)) {
        return $children[0].FullName
    }

    throw "Base archive does not contain a recognizable package root."
}

if ([string]::IsNullOrWhiteSpace($BaseArchivePath) -xor
    [string]::IsNullOrWhiteSpace($UpdaterAssemblyPath)) {
    throw "BaseArchivePath and UpdaterAssemblyPath must be supplied together for an install test."
}

$archiveFullPath = (Resolve-Path -LiteralPath $ArchivePath).Path
$tempParent = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
$extractRoot = Join-Path $tempParent "OpenGarrison.DeltaVerify.$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $extractRoot -Force | Out-Null

try {
    Expand-UpdateArchive -Path $archiveFullPath -Destination $extractRoot

    $planPath = Join-Path $extractRoot "delta-plan.json"
    $targetManifestPath = Join-Path $extractRoot "target-package-manifest.json"
    $payloadRoot = Join-Path $extractRoot "payload"
    foreach ($requiredPath in @($planPath, $targetManifestPath, $payloadRoot)) {
        if (-not (Test-Path -LiteralPath $requiredPath)) {
            throw "Delta archive is missing '$([System.IO.Path]::GetFileName($requiredPath))'."
        }
    }

    $plan = Get-Content -LiteralPath $planPath -Raw | ConvertFrom-Json
    $targetManifest = Get-Content -LiteralPath $targetManifestPath -Raw | ConvertFrom-Json
    if ([int](Get-RequiredProperty -Object $plan -Name "schemaVersion" -Description "Delta plan") -ne 1 -or
        [int](Get-RequiredProperty -Object $targetManifest -Name "schemaVersion" -Description "Target package manifest") -ne 1) {
        throw "Delta archive uses an unsupported metadata schema."
    }

    $fromVersion = [string](Get-RequiredProperty -Object $plan -Name "fromVersion" -Description "Delta plan")
    $toVersion = [string](Get-RequiredProperty -Object $plan -Name "toVersion" -Description "Delta plan")
    $targetVersion = [string](Get-RequiredProperty -Object $targetManifest -Name "version" -Description "Target package manifest")
    if ([string]::IsNullOrWhiteSpace($fromVersion) -or
        [string]::IsNullOrWhiteSpace($toVersion) -or
        $fromVersion.Equals($toVersion, [System.StringComparison]::OrdinalIgnoreCase) -or
        -not $toVersion.Equals($targetVersion, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Delta version metadata is inconsistent."
    }

    $expectedTargetManifestSha256 = [string](Get-RequiredProperty -Object $plan -Name "targetManifestSha256" -Description "Delta plan")
    $actualTargetManifestSha256 = (Get-FileHash -LiteralPath $targetManifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ([string]::IsNullOrWhiteSpace($expectedTargetManifestSha256) -or
        -not $actualTargetManifestSha256.Equals($expectedTargetManifestSha256.Trim(), [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Delta target package manifest hash does not match its plan."
    }

    $pathComparer = if ($RuntimeIdentifier -eq "win-x64") {
        [System.StringComparer]::OrdinalIgnoreCase
    }
    else {
        [System.StringComparer]::Ordinal
    }
    $targetPaths = [System.Collections.Generic.HashSet[string]]::new($pathComparer)
    $targetEntriesByPath = [System.Collections.Generic.Dictionary[string, object]]::new($pathComparer)
    foreach ($entry in @(Get-RequiredProperty -Object $targetManifest -Name "files" -Description "Target package manifest")) {
        $relativePath = Get-NormalizedUpdatePath -Path ([string](Get-RequiredProperty -Object $entry -Name "path" -Description "Target package manifest entry"))
        if (-not $targetPaths.Add($relativePath)) {
            throw "Target package manifest contains duplicate path '$relativePath'."
        }
        $targetEntriesByPath[$relativePath] = $entry
    }

    $changedPaths = [System.Collections.Generic.HashSet[string]]::new($pathComparer)
    foreach ($entry in @(Get-RequiredProperty -Object $plan -Name "files" -Description "Delta plan")) {
        $relativePath = Assert-FileMatchesEntry -Root $payloadRoot -Entry $entry -Description "Delta payload"
        if (-not $changedPaths.Add($relativePath) -or -not $targetEntriesByPath.ContainsKey($relativePath)) {
            throw "Delta plan contains an invalid or duplicate changed path '$relativePath'."
        }

        $targetEntry = $targetEntriesByPath[$relativePath]
        if ([long]$entry.size -ne [long]$targetEntry.size -or
            -not ([string]$entry.sha256).Equals([string]$targetEntry.sha256, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Delta plan entry does not match the target manifest for '$relativePath'."
        }
    }

    $deletedPaths = [System.Collections.Generic.HashSet[string]]::new($pathComparer)
    foreach ($deletedFile in @(Get-RequiredProperty -Object $plan -Name "deletedFiles" -Description "Delta plan")) {
        $relativePath = Get-NormalizedUpdatePath -Path ([string]$deletedFile)
        if (-not $deletedPaths.Add($relativePath) -or
            $changedPaths.Contains($relativePath) -or
            $targetPaths.Contains($relativePath)) {
            throw "Delta plan contains an invalid, duplicate, or conflicting deleted path '$relativePath'."
        }
    }

    $payloadFiles = @(Get-ChildItem -LiteralPath $payloadRoot -File -Recurse)
    if ($payloadFiles.Count -ne $changedPaths.Count) {
        throw "Delta payload contains unlisted files or is missing listed files."
    }

    if (-not [string]::IsNullOrWhiteSpace($BaseArchivePath)) {
        $baseArchiveFullPath = (Resolve-Path -LiteralPath $BaseArchivePath).Path
        $updaterAssemblyFullPath = (Resolve-Path -LiteralPath $UpdaterAssemblyPath).Path
        $baseExtractDirectory = Join-Path $extractRoot ".install-test-base"
        Expand-UpdateArchive -Path $baseArchiveFullPath -Destination $baseExtractDirectory
        $installRoot = Resolve-PackageRoot -ExtractDirectory $baseExtractDirectory

        $installedBaseVersion = (Get-Content -LiteralPath (Join-Path $installRoot "version.txt") -Raw).Trim()
        if (-not $installedBaseVersion.Equals($fromVersion, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Base archive version '$installedBaseVersion' does not match delta source '$fromVersion'."
        }

        & dotnet $updaterAssemblyFullPath --apply-delta $extractRoot $installRoot $toVersion 0 -- --no-launch-after-update
        if ($LASTEXITCODE -ne 0) {
            throw "Updater delta apply mode exited with code $LASTEXITCODE."
        }

        foreach ($targetEntry in $targetManifest.files) {
            Assert-FileMatchesEntry -Root $installRoot -Entry $targetEntry -Description "Installed target" | Out-Null
        }
        foreach ($deletedPath in $deletedPaths) {
            $deletedFilePath = [System.IO.Path]::GetFullPath((Join-Path $installRoot $deletedPath))
            if (Test-Path -LiteralPath $deletedFilePath) {
                throw "Updater did not remove '$deletedPath'."
            }
        }

        $installedManifestPath = Join-Path $installRoot "package-manifest.json"
        $installedManifestSha256 = (Get-FileHash -LiteralPath $installedManifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
        if (-not $installedManifestSha256.Equals($actualTargetManifestSha256, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Updater did not install the exact target package manifest."
        }
        if (Test-Path -LiteralPath (Join-Path $installRoot ".opengarrison-update-transaction")) {
            throw "Updater left an incomplete transaction after a successful delta apply."
        }

        Write-Host "[verify-update-delta] INSTALL PASS: $fromVersion -> $toVersion using '$baseArchiveFullPath'."
    }

    Write-Host "[verify-update-delta] PASS: $RuntimeIdentifier $fromVersion -> $toVersion '$archiveFullPath' ($($changedPaths.Count) changed/new, $($deletedPaths.Count) deleted)."
}
finally {
    if ((Test-Path -LiteralPath $extractRoot) -and
        (Test-IsPathWithinDirectory -CandidatePath $extractRoot -Directory $tempParent)) {
        Remove-Item -LiteralPath $extractRoot -Recurse -Force
    }
}
