[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string] $Root,

    [ValidateSet("Stock", "All", "None")]
    [string] $StrictContentPaths = "Stock"
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($Root)) {
    $scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
    $Root = Join-Path $scriptDirectory "../Core/Content/Gameplay"
}

function Write-Violation {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Message
    )

    $script:ViolationCount++
    [Console]::Error.WriteLine("[ERROR] $Message")
}

function Get-RelativeDisplayPath {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    try {
        $rootWithSeparator = $script:ScanRoot.TrimEnd(
            [System.IO.Path]::DirectorySeparatorChar,
            [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
        $rootUri = [System.Uri]::new($rootWithSeparator)
        $pathUri = [System.Uri]::new([System.IO.Path]::GetFullPath($Path))
        return [System.Uri]::UnescapeDataString($rootUri.MakeRelativeUri($pathUri).ToString()).Replace('\', '/')
    }
    catch {
        return $Path
    }
}

function Get-ContentDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.DirectoryInfo] $PackDirectory
    )

    $candidate = $PackDirectory
    while ($null -ne $candidate) {
        if ($candidate.Name -ieq "Content") {
            return $candidate.FullName
        }

        $candidate = $candidate.Parent
    }

    return $null
}

function Test-PathContainedByDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Directory,

        [Parameter(Mandatory = $true)]
        [string] $Candidate
    )

    $fullDirectory = [System.IO.Path]::GetFullPath($Directory).TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)
    $fullCandidate = [System.IO.Path]::GetFullPath($Candidate)
    $directoryPrefix = $fullDirectory + [System.IO.Path]::DirectorySeparatorChar

    return $fullCandidate.Equals($fullDirectory, [System.StringComparison]::OrdinalIgnoreCase) -or
        $fullCandidate.StartsWith($directoryPrefix, [System.StringComparison]::OrdinalIgnoreCase)
}

function Test-PathHasExactCasing {
    param(
        [Parameter(Mandatory = $true)]
        [string] $BaseDirectory,

        [Parameter(Mandatory = $true)]
        [string] $RelativePath
    )

    $currentDirectory = [System.IO.Path]::GetFullPath($BaseDirectory)
    foreach ($segment in $RelativePath.Replace('\', '/').Split('/', [System.StringSplitOptions]::RemoveEmptyEntries)) {
        if (-not $script:DirectoryEntryNames.ContainsKey($currentDirectory)) {
            $entryNames = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
            foreach ($entry in [System.IO.Directory]::EnumerateFileSystemEntries($currentDirectory)) {
                [void]$entryNames.Add([System.IO.Path]::GetFileName($entry))
            }
            $script:DirectoryEntryNames[$currentDirectory] = $entryNames
        }

        if (-not $script:DirectoryEntryNames[$currentDirectory].Contains($segment)) {
            return $false
        }

        $currentDirectory = [System.IO.Path]::Combine($currentDirectory, $segment)
    }

    return $true
}

function Register-SpriteReferences {
    param(
        [AllowNull()]
        [object] $Value,

        [Parameter(Mandatory = $true)]
        [System.IO.DirectoryInfo] $PackDirectory,

        [Parameter(Mandatory = $true)]
        [string] $DefinitionPath,

        [string] $PropertyPath = ""
    )

    if ($null -eq $Value) {
        return
    }

    if ($Value -is [System.Management.Automation.PSCustomObject]) {
        foreach ($property in $Value.PSObject.Properties) {
            $nextPath = if ([string]::IsNullOrWhiteSpace($PropertyPath)) { $property.Name } else { "$PropertyPath.$($property.Name)" }
            if ($property.Name -match 'Sprite(Name)?$' -and
                $property.Value -is [string] -and
                -not [string]::IsNullOrWhiteSpace([string]$property.Value)) {
                $script:SpriteReferences.Add([pscustomobject]@{
                    PackDirectory = $PackDirectory
                    DefinitionPath = $DefinitionPath
                    PropertyPath = $nextPath
                    SpriteId = ([string]$property.Value).Trim()
                })
            }

            Register-SpriteReferences -Value $property.Value -PackDirectory $PackDirectory -DefinitionPath $DefinitionPath -PropertyPath $nextPath
        }
        return
    }

    if ($Value -is [System.Collections.IEnumerable] -and $Value -isnot [string]) {
        foreach ($item in $Value) {
            Register-SpriteReferences -Value $item -PackDirectory $PackDirectory -DefinitionPath $DefinitionPath -PropertyPath $PropertyPath
        }
    }
}

function Test-FramePath {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.DirectoryInfo] $PackDirectory,

        [Parameter(Mandatory = $true)]
        [string] $DefinitionPath,

        [Parameter(Mandatory = $true)]
        [string] $SpriteId,

        [Parameter(Mandatory = $true)]
        [int] $Index,

        [AllowNull()]
        [object] $FramePath
    )

    $definitionDisplayPath = Get-RelativeDisplayPath $DefinitionPath
    $location = "$definitionDisplayPath framePaths[$Index]"

    if ($FramePath -isnot [string] -or [string]::IsNullOrWhiteSpace([string] $FramePath)) {
        Write-Violation "$location for sprite '$SpriteId' must be a non-empty string."
        return
    }

    $originalPath = [string] $FramePath
    $normalizedPath = $originalPath.Trim().Replace('\', '/')

    if ([System.IO.Path]::IsPathRooted($normalizedPath) -or
        $normalizedPath.StartsWith("/", [System.StringComparison]::Ordinal) -or
        $normalizedPath.StartsWith("\\", [System.StringComparison]::Ordinal)) {
        Write-Violation "$location for sprite '$SpriteId' is rooted; use a pack-relative path: '$originalPath'."
        return
    }

    $segments = $normalizedPath.Split('/', [System.StringSplitOptions]::RemoveEmptyEntries)
    if ($segments -contains "..") {
        Write-Violation "$location for sprite '$SpriteId' contains parent traversal: '$originalPath'."
        return
    }

    $isContentPath = $normalizedPath.StartsWith("Content/", [System.StringComparison]::OrdinalIgnoreCase)
    $rejectContentPath = $StrictContentPaths -eq "All" -or
        ($StrictContentPaths -eq "Stock" -and $PackDirectory.Name -ieq "stock.gg2")

    if ($isContentPath -and $rejectContentPath) {
        Write-Violation "$location for sprite '$SpriteId' crosses out of '$($PackDirectory.Name)' through '$originalPath'. Move the frame under the pack and use a pack-relative path."
    }

    if ($isContentPath) {
        $contentDirectory = Get-ContentDirectory $PackDirectory
        if ([string]::IsNullOrWhiteSpace($contentDirectory)) {
            Write-Violation "$location for sprite '$SpriteId' uses '$originalPath', but no ancestor Content directory could be found for '$($PackDirectory.FullName)'."
            return
        }

        $contentRelativePath = $normalizedPath.Substring("Content/".Length)
        if ([string]::IsNullOrWhiteSpace($contentRelativePath)) {
            Write-Violation "$location for sprite '$SpriteId' does not name a file beneath Content: '$originalPath'."
            return
        }

        $resolvedPath = [System.IO.Path]::GetFullPath(
            [System.IO.Path]::Combine($contentDirectory, $contentRelativePath.Replace('/', [System.IO.Path]::DirectorySeparatorChar)))
        $casingBaseDirectory = $contentDirectory
        $casingRelativePath = $contentRelativePath
        if (-not (Test-PathContainedByDirectory -Directory $contentDirectory -Candidate $resolvedPath)) {
            Write-Violation "$location for sprite '$SpriteId' escapes the Content directory: '$originalPath'."
            return
        }
    }
    else {
        $resolvedPath = [System.IO.Path]::GetFullPath(
            [System.IO.Path]::Combine($PackDirectory.FullName, $normalizedPath.Replace('/', [System.IO.Path]::DirectorySeparatorChar)))
        $casingBaseDirectory = $PackDirectory.FullName
        $casingRelativePath = $normalizedPath
        if (-not (Test-PathContainedByDirectory -Directory $PackDirectory.FullName -Candidate $resolvedPath)) {
            Write-Violation "$location for sprite '$SpriteId' escapes '$($PackDirectory.Name)': '$originalPath'."
            return
        }
    }

    if (-not [System.IO.File]::Exists($resolvedPath)) {
        Write-Violation "$location for sprite '$SpriteId' does not exist: '$originalPath' (resolved to '$resolvedPath')."
        return
    }

    if (-not (Test-PathHasExactCasing -BaseDirectory $casingBaseDirectory -RelativePath $casingRelativePath)) {
        Write-Violation "$location for sprite '$SpriteId' does not match on-disk path casing: '$originalPath'."
    }

    [void]$script:ReferencedFramePaths.Add($resolvedPath)
}

function Register-DefinitionId {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Kind,

        [Parameter(Mandatory = $true)]
        [string] $Id,

        [Parameter(Mandatory = $true)]
        [string] $DefinitionPath
    )

    $key = "$Kind`:$Id"
    if ($script:DefinitionIds.ContainsKey($key)) {
        $firstPath = Get-RelativeDisplayPath $script:DefinitionIds[$key]
        $duplicatePath = Get-RelativeDisplayPath $DefinitionPath
        Write-Violation "Duplicate $Kind id '$Id' in '$duplicatePath'; it was first declared in '$firstPath'. IDs must be globally unique across all scanned packs."
        return
    }

    $script:DefinitionIds[$key] = $DefinitionPath
}

function Inspect-Definition {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.DirectoryInfo] $PackDirectory,

        [Parameter(Mandatory = $true)]
        [ValidateSet("class", "item", "sprite")]
        [string] $Kind,

        [Parameter(Mandatory = $true)]
        [System.IO.FileInfo] $DefinitionFile
    )

    $displayPath = Get-RelativeDisplayPath $DefinitionFile.FullName
    try {
        $definition = Get-Content -LiteralPath $DefinitionFile.FullName -Raw | ConvertFrom-Json
    }
    catch {
        Write-Violation "Invalid JSON in '$displayPath': $($_.Exception.Message)"
        return
    }

    if ($null -eq $definition -or $definition -isnot [System.Management.Automation.PSCustomObject]) {
        Write-Violation "Definition '$displayPath' must contain a JSON object."
        return
    }

    $idProperty = $definition.PSObject.Properties | Where-Object { $_.Name -ieq "id" } | Select-Object -First 1
    if ($null -eq $idProperty -or $idProperty.Value -isnot [string] -or [string]::IsNullOrWhiteSpace([string] $idProperty.Value)) {
        Write-Violation "$Kind definition '$displayPath' must declare a non-empty string id."
        return
    }

    $id = ([string] $idProperty.Value).Trim()
    Register-DefinitionId -Kind $Kind -Id $id -DefinitionPath $DefinitionFile.FullName
    $script:DefinitionCounts[$Kind]++

    if ($Kind -ne "sprite") {
        Register-SpriteReferences -Value $definition -PackDirectory $PackDirectory -DefinitionPath $DefinitionFile.FullName
        return
    }

    $framePathsProperty = $definition.PSObject.Properties | Where-Object { $_.Name -ieq "framePaths" } | Select-Object -First 1
    if ($null -eq $framePathsProperty -or $null -eq $framePathsProperty.Value) {
        Write-Violation "Sprite definition '$displayPath' (id '$id') must declare a non-empty framePaths array."
        return
    }

    if ($framePathsProperty.Value -is [string] -or $framePathsProperty.Value -isnot [System.Collections.IEnumerable]) {
        Write-Violation "Sprite definition '$displayPath' (id '$id') must declare framePaths as an array."
        return
    }

    $framePaths = @($framePathsProperty.Value)
    if ($framePaths.Count -eq 0) {
        Write-Violation "Sprite definition '$displayPath' (id '$id') must declare at least one frame path."
        return
    }

    for ($index = 0; $index -lt $framePaths.Count; $index++) {
        $script:FramePathCount++
        Test-FramePath -PackDirectory $PackDirectory -DefinitionPath $DefinitionFile.FullName -SpriteId $id -Index $index -FramePath $framePaths[$index]
    }
}

$script:ViolationCount = 0
$script:FramePathCount = 0
$script:DefinitionCounts = @{
    class = 0
    item = 0
    sprite = 0
}
$script:DefinitionIds = [System.Collections.Generic.Dictionary[string, string]]::new(
    [System.StringComparer]::OrdinalIgnoreCase)
$script:ReferencedFramePaths = [System.Collections.Generic.HashSet[string]]::new(
    [System.StringComparer]::OrdinalIgnoreCase)
$script:SpriteReferences = [System.Collections.Generic.List[object]]::new()
$script:DirectoryEntryNames = [System.Collections.Generic.Dictionary[string, object]]::new(
    [System.StringComparer]::OrdinalIgnoreCase)

try {
    $script:ScanRoot = [System.IO.Path]::GetFullPath($Root)
}
catch {
    [Console]::Error.WriteLine("[ERROR] Invalid scan root '$Root': $($_.Exception.Message)")
    exit 1
}

if (-not [System.IO.Directory]::Exists($script:ScanRoot)) {
    [Console]::Error.WriteLine("[ERROR] Gameplay pack scan root does not exist: '$($script:ScanRoot)'.")
    exit 1
}

$rootDirectory = [System.IO.DirectoryInfo]::new($script:ScanRoot)
if ($rootDirectory.Name.EndsWith(".gg2", [System.StringComparison]::OrdinalIgnoreCase)) {
    $packDirectories = @($rootDirectory)
}
else {
    $packDirectories = @(Get-ChildItem -LiteralPath $script:ScanRoot -Directory -Filter "*.gg2" -Recurse | Sort-Object FullName)
}

if ($packDirectories.Count -eq 0) {
    [Console]::Error.WriteLine("[ERROR] No *.gg2 gameplay pack directories were found beneath '$($script:ScanRoot)'.")
    exit 1
}

foreach ($packDirectory in $packDirectories) {
    foreach ($category in @(
        @{ Directory = "classes"; Kind = "class" },
        @{ Directory = "items"; Kind = "item" },
        @{ Directory = "sprites"; Kind = "sprite" }
    )) {
        $categoryDirectory = [System.IO.Path]::Combine($packDirectory.FullName, $category.Directory)
        if (-not [System.IO.Directory]::Exists($categoryDirectory)) {
            continue
        }

        $definitionFiles = Get-ChildItem -LiteralPath $categoryDirectory -File -Filter "*.json" -Recurse | Sort-Object FullName
        foreach ($definitionFile in $definitionFiles) {
            Inspect-Definition -PackDirectory $packDirectory -Kind $category.Kind -DefinitionFile $definitionFile
        }
    }
}

foreach ($reference in $script:SpriteReferences) {
    $enforceReference = $StrictContentPaths -eq "All" -or
        ($StrictContentPaths -eq "Stock" -and $reference.PackDirectory.Name -ieq "stock.gg2")
    if ($enforceReference -and -not $script:DefinitionIds.ContainsKey("sprite:$($reference.SpriteId)")) {
        $displayPath = Get-RelativeDisplayPath $reference.DefinitionPath
        Write-Violation "Sprite reference '$($reference.SpriteId)' at '$displayPath' property '$($reference.PropertyPath)' has no sprite definition in the scanned gameplay packs."
    }
}

foreach ($packDirectory in $packDirectories) {
    $assetsDirectory = Join-Path $packDirectory.FullName "assets"
    if (-not (Test-Path -LiteralPath $assetsDirectory -PathType Container)) {
        continue
    }

    foreach ($assetFile in Get-ChildItem -LiteralPath $assetsDirectory -File -Recurse) {
        if ($assetFile.Extension -ieq ".png" -and -not $script:ReferencedFramePaths.Contains($assetFile.FullName)) {
            Write-Violation "Orphaned sprite frame is not referenced by any sprite definition: '$(Get-RelativeDisplayPath $assetFile.FullName)'."
        }
    }
}

$summary = "Scanned {0} pack(s): {1} class(es), {2} item(s), {3} sprite(s), and {4} frame path(s)." -f
    $packDirectories.Count,
    $script:DefinitionCounts.class,
    $script:DefinitionCounts.item,
    $script:DefinitionCounts.sprite,
    $script:FramePathCount

if ($script:ViolationCount -gt 0) {
    [Console]::Error.WriteLine("[FAIL] $summary Found $($script:ViolationCount) violation(s).")
    exit 1
}

[Console]::Out.WriteLine("[PASS] $summary StrictContentPaths=$StrictContentPaths.")
exit 0
