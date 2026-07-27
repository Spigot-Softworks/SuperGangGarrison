[CmdletBinding(DefaultParameterSetName = "Verify")]
param(
    [Parameter(Position = 0, ParameterSetName = "Verify")]
    [string]$Path = "",

    [Parameter(Mandatory = $true, ParameterSetName = "SelfTest")]
    [switch]$SelfTest
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

    $candidateFullPath = [System.IO.Path]::GetFullPath($CandidatePath)
    $directoryFullPath = [System.IO.Path]::GetFullPath($Directory)
    if ($candidateFullPath.Equals($directoryFullPath, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $true
    }

    if (-not $directoryFullPath.EndsWith([System.IO.Path]::DirectorySeparatorChar)) {
        $directoryFullPath += [System.IO.Path]::DirectorySeparatorChar
    }

    return $candidateFullPath.StartsWith($directoryFullPath, [System.StringComparison]::OrdinalIgnoreCase)
}

function Resolve-PackagedContentRoot {
    param(
        [Parameter(Mandatory = $true)]
        [string]$InputPath
    )

    if ([string]::IsNullOrWhiteSpace($InputPath)) {
        throw "Pass a packaged Content directory or an extracted distribution root. Use -SelfTest to run the built-in policy tests."
    }

    if (-not (Test-Path -LiteralPath $InputPath -PathType Container)) {
        throw "Packaged content path does not exist or is not a directory: '$InputPath'."
    }

    $resolvedInputPath = (Resolve-Path -LiteralPath $InputPath).Path
    $candidates = [System.Collections.Generic.List[string]]::new()

    if ([System.IO.Path]::GetFileName($resolvedInputPath).Equals("Content", [System.StringComparison]::OrdinalIgnoreCase)) {
        $candidates.Add($resolvedInputPath)
    }

    foreach ($relativePath in @("Content", "app/Content")) {
        $candidate = Join-Path $resolvedInputPath $relativePath
        if (Test-Path -LiteralPath $candidate -PathType Container) {
            $candidateFullPath = (Resolve-Path -LiteralPath $candidate).Path
            if (-not $candidates.Contains($candidateFullPath)) {
                $candidates.Add($candidateFullPath)
            }
        }
    }

    if ($candidates.Count -eq 0) {
        throw "Could not find a packaged Content directory at '$resolvedInputPath', '$resolvedInputPath/Content', or '$resolvedInputPath/app/Content'."
    }

    if ($candidates.Count -gt 1) {
        throw "Found multiple packaged Content directories under '$resolvedInputPath': $($candidates -join ', '). Pass the intended Content directory explicitly."
    }

    return $candidates[0]
}

function Get-ContentRelativePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ContentRoot,

        [Parameter(Mandatory = $true)]
        [string]$CandidatePath
    )

    $contentFullPath = [System.IO.Path]::GetFullPath($ContentRoot).TrimEnd([char[]]@('\', '/'))
    $candidateFullPath = [System.IO.Path]::GetFullPath($CandidatePath)
    if (-not (Test-IsPathWithinDirectory -CandidatePath $candidateFullPath -Directory $contentFullPath)) {
        throw "Path '$candidateFullPath' is not inside Content root '$contentFullPath'."
    }

    return $candidateFullPath.Substring($contentFullPath.Length).TrimStart([char[]]@('\', '/')).Replace('\', '/')
}

function Add-MissingFileViolation {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [System.Collections.Generic.List[string]]$Violations,

        [Parameter(Mandatory = $true)]
        [string]$ContentRoot,

        [Parameter(Mandatory = $true)]
        [string]$RelativePath,

        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    $fullPath = Join-Path $ContentRoot $RelativePath
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        $Violations.Add("missing ${Description}: $RelativePath")
    }
}

function Test-PackagedContentPolicy {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ContentRoot,

        [switch]$Quiet
    )

    $contentFullPath = (Resolve-Path -LiteralPath $ContentRoot).Path
    $violations = [System.Collections.Generic.List[string]]::new()

    $requiredFiles = [ordered]@{
        "Gameplay/stock.gg2/pack.json" = "stock gameplay pack definition"
        "Gameplay/stock.gg2/runtime.json" = "compact stock gameplay runtime metadata"
        "Browser/Manifests/bootstrap-manifest.json" = "bootstrap atlas manifest"
        "Browser/Manifests/stock-pack-atlas-manifest.json" = "stock gameplay atlas manifest"
        "Browser/Manifests/gamemaker-atlas-manifest.json" = "GameMaker atlas manifest"
    }

    foreach ($entry in $requiredFiles.GetEnumerator()) {
        Add-MissingFileViolation -Violations $violations -ContentRoot $contentFullPath -RelativePath $entry.Key -Description $entry.Value
    }

    foreach ($runtimeJsonDirectory in @("Gameplay/stock.gg2/classes", "Gameplay/stock.gg2/items")) {
        $fullDirectory = Join-Path $contentFullPath $runtimeJsonDirectory
        $runtimeJson = if (Test-Path -LiteralPath $fullDirectory -PathType Container) {
            Get-ChildItem -LiteralPath $fullDirectory -File -Recurse -Filter "*.json" | Select-Object -First 1
        }
        else {
            $null
        }

        if ($null -eq $runtimeJson) {
            $violations.Add("missing stock gameplay runtime definitions: $runtimeJsonDirectory/*.json")
        }
    }

    $collisionMapsDirectory = Join-Path $contentFullPath "Sprites/Collision Maps"
    $collisionMapFrame = if (Test-Path -LiteralPath $collisionMapsDirectory -PathType Container) {
        Get-ChildItem -LiteralPath $collisionMapsDirectory -File -Recurse -Filter "*.png" | Select-Object -First 1
    }
    else {
        $null
    }

    if ($null -eq $collisionMapFrame) {
        $violations.Add("missing runtime collision-map frames: Sprites/Collision Maps/**/*.png")
    }

    $atlasDirectory = Join-Path $contentFullPath "Browser/Atlases"
    $atlasPage = if (Test-Path -LiteralPath $atlasDirectory -PathType Container) {
        Get-ChildItem -LiteralPath $atlasDirectory -File -Recurse -Filter "*.png" | Select-Object -First 1
    }
    else {
        $null
    }

    if ($null -eq $atlasPage) {
        $violations.Add("missing generated runtime atlas pages: Browser/Atlases/**/*.png")
    }

    $spritesDirectory = Join-Path $contentFullPath "Sprites"
    $stockAssetsDirectory = Join-Path $contentFullPath "Gameplay/stock.gg2/assets"
    foreach ($xmlFile in Get-ChildItem -LiteralPath $contentFullPath -File -Recurse -Filter "*.xml") {
        $siblingImagesDirectory = Join-Path $xmlFile.DirectoryName "$($xmlFile.BaseName).images"
        $isGameMakerSpriteXml = (Test-IsPathWithinDirectory -CandidatePath $xmlFile.FullName -Directory $spritesDirectory) `
            -or (Test-Path -LiteralPath $siblingImagesDirectory -PathType Container) `
            -or (Test-IsPathWithinDirectory -CandidatePath $xmlFile.FullName -Directory $stockAssetsDirectory)

        if ($isGameMakerSpriteXml) {
            $relativePath = Get-ContentRelativePath -ContentRoot $contentFullPath -CandidatePath $xmlFile.FullName
            $violations.Add("GameMaker sprite XML is source-only: $relativePath")
        }
    }

    foreach ($imagesDirectory in Get-ChildItem -LiteralPath $contentFullPath -Directory -Recurse -Force | Where-Object { $_.Name.EndsWith(".images", [System.StringComparison]::OrdinalIgnoreCase) }) {
        if (Test-IsPathWithinDirectory -CandidatePath $imagesDirectory.FullName -Directory $collisionMapsDirectory) {
            continue
        }

        $relativePath = Get-ContentRelativePath -ContentRoot $contentFullPath -CandidatePath $imagesDirectory.FullName
        $violations.Add("loose sprite frame directory is source-only: $relativePath")
    }

    $stockSpritesDirectory = Join-Path $contentFullPath "Gameplay/stock.gg2/sprites"
    if (Test-Path -LiteralPath $stockSpritesDirectory -PathType Container) {
        foreach ($spriteDefinition in Get-ChildItem -LiteralPath $stockSpritesDirectory -File -Recurse -Filter "*.json") {
            $relativePath = Get-ContentRelativePath -ContentRoot $contentFullPath -CandidatePath $spriteDefinition.FullName
            $violations.Add("stock sprite JSON is atlas-build input, not runtime data: $relativePath")
        }
    }

    foreach ($sourceDirectory in @($stockSpritesDirectory, $stockAssetsDirectory)) {
        if (Test-Path -LiteralPath $sourceDirectory -PathType Container) {
            $relativePath = Get-ContentRelativePath -ContentRoot $contentFullPath -CandidatePath $sourceDirectory
            $violations.Add("stock sprite source directory is build-only: $relativePath")
        }
    }

    $looseVisualExtensions = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($extension in @(".png", ".apng", ".bmp", ".gif", ".jpg", ".jpeg", ".tga", ".tif", ".tiff", ".webp", ".ase", ".aseprite", ".psd", ".xcf")) {
        [void]$looseVisualExtensions.Add($extension)
    }

    if (Test-Path -LiteralPath $stockAssetsDirectory -PathType Container) {
        foreach ($assetFile in Get-ChildItem -LiteralPath $stockAssetsDirectory -File -Recurse -Force) {
            if (-not $looseVisualExtensions.Contains($assetFile.Extension)) {
                continue
            }

            $relativePath = Get-ContentRelativePath -ContentRoot $contentFullPath -CandidatePath $assetFile.FullName
            $violations.Add("loose stock gameplay visual is source-only: $relativePath")
        }
    }

    $manifestRelativePaths = @(
        "Browser/Manifests/bootstrap-manifest.json",
        "Browser/Manifests/stock-pack-atlas-manifest.json",
        "Browser/Manifests/gamemaker-atlas-manifest.json"
    )

    foreach ($manifestRelativePath in $manifestRelativePaths) {
        $manifestPath = Join-Path $contentFullPath $manifestRelativePath
        if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
            continue
        }

        try {
            $manifestDocument = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
            $manifest = if ($null -ne $manifestDocument.PSObject.Properties["Manifest"]) {
                $manifestDocument.Manifest
            }
            else {
                $manifestDocument
            }

            $atlasesProperty = $manifest.PSObject.Properties["Atlases"]
            [object[]]$atlases = if ($null -ne $atlasesProperty) { @($atlasesProperty.Value) } else { @() }
            if ($atlases.Count -eq 0) {
                $violations.Add("runtime atlas manifest contains no atlas pages: $manifestRelativePath")
                continue
            }

            foreach ($atlas in $atlases) {
                $imagePathProperty = $atlas.PSObject.Properties["ImagePath"]
                if ($null -eq $imagePathProperty -or [string]::IsNullOrWhiteSpace([string]$imagePathProperty.Value)) {
                    $violations.Add("runtime atlas manifest has an entry without ImagePath: $manifestRelativePath")
                    continue
                }

                $imagePath = ([string]$imagePathProperty.Value).Replace('/', [System.IO.Path]::DirectorySeparatorChar)
                if ($imagePath.StartsWith("Content$([System.IO.Path]::DirectorySeparatorChar)", [System.StringComparison]::OrdinalIgnoreCase)) {
                    $imagePath = $imagePath.Substring("Content$([System.IO.Path]::DirectorySeparatorChar)".Length)
                }

                $atlasPath = [System.IO.Path]::GetFullPath((Join-Path $contentFullPath $imagePath))
                if (-not (Test-IsPathWithinDirectory -CandidatePath $atlasPath -Directory $contentFullPath)) {
                    $violations.Add("runtime atlas manifest references a path outside Content: $manifestRelativePath -> $($imagePathProperty.Value)")
                    continue
                }

                if (-not (Test-Path -LiteralPath $atlasPath -PathType Leaf)) {
                    $violations.Add("runtime atlas manifest references a missing page: $manifestRelativePath -> $($imagePathProperty.Value)")
                }
            }
        }
        catch {
            $violations.Add("runtime atlas manifest is not valid JSON: $manifestRelativePath ($($_.Exception.Message))")
        }
    }

    $runtimeMetadataPath = Join-Path $contentFullPath "Gameplay/stock.gg2/runtime.json"
    $stockAtlasManifestPath = Join-Path $contentFullPath "Browser/Manifests/stock-pack-atlas-manifest.json"
    if ((Test-Path -LiteralPath $runtimeMetadataPath -PathType Leaf) -and
        (Test-Path -LiteralPath $stockAtlasManifestPath -PathType Leaf)) {
        try {
            $runtimeDocument = Get-Content -LiteralPath $runtimeMetadataPath -Raw | ConvertFrom-Json
            $stockAtlasDocument = Get-Content -LiteralPath $stockAtlasManifestPath -Raw | ConvertFrom-Json
            $runtimeSpriteIds = @($runtimeDocument.Assets.Sprites.PSObject.Properties.Name | Sort-Object -Unique)
            $atlasSpriteIds = @($stockAtlasDocument.Manifest.Sprites.PSObject.Properties.Name | Sort-Object -Unique)

            if ($runtimeSpriteIds.Count -eq 0) {
                $violations.Add("compact stock gameplay runtime metadata contains no sprite IDs: Gameplay/stock.gg2/runtime.json")
            }
            if ($atlasSpriteIds.Count -eq 0) {
                $violations.Add("stock gameplay atlas manifest contains no sprite IDs: Browser/Manifests/stock-pack-atlas-manifest.json")
            }

            $spriteIdDifferences = @(Compare-Object -ReferenceObject $runtimeSpriteIds -DifferenceObject $atlasSpriteIds)
            foreach ($difference in $spriteIdDifferences) {
                if ($difference.SideIndicator -eq "<=") {
                    $violations.Add("stock runtime sprite ID is missing from the generated atlas: $($difference.InputObject)")
                }
                else {
                    $violations.Add("stock atlas contains a sprite ID missing from runtime metadata: $($difference.InputObject)")
                }
            }
        }
        catch {
            $violations.Add("could not compare stock runtime and atlas sprite IDs: $($_.Exception.Message)")
        }
    }

    if ($violations.Count -gt 0) {
        $details = ($violations | Sort-Object -Unique | ForEach-Object { "  - $_" }) -join [Environment]::NewLine
        throw "Packaged content policy failed for '$contentFullPath':$([Environment]::NewLine)$details"
    }

    if (-not $Quiet) {
        Write-Host "[verify-packaged-content] OK: '$contentFullPath' contains runtime definitions, collision maps, and generated atlases without loose sprite sources."
    }
}

function New-PolicyTestFixture {
    param(
        [Parameter(Mandatory = $true)]
        [string]$TestRoot
    )

    $contentRoot = Join-Path $TestRoot "app/Content"
    foreach ($directory in @(
        "Gameplay/stock.gg2/classes",
        "Gameplay/stock.gg2/items",
        "Browser/Atlases",
        "Browser/Manifests",
        "Sprites/Collision Maps/TestMapS.images"
    )) {
        New-Item -ItemType Directory -Path (Join-Path $contentRoot $directory) -Force | Out-Null
    }

    Set-Content -LiteralPath (Join-Path $contentRoot "Gameplay/stock.gg2/pack.json") -Value '{"id":"stock.gg2"}' -Encoding UTF8
    Set-Content -LiteralPath (Join-Path $contentRoot "Gameplay/stock.gg2/runtime.json") -Value '{"assets":{"sprites":{"test":{}}}}' -Encoding UTF8
    Set-Content -LiteralPath (Join-Path $contentRoot "Gameplay/stock.gg2/classes/test.json") -Value '{}' -Encoding UTF8
    Set-Content -LiteralPath (Join-Path $contentRoot "Gameplay/stock.gg2/items/test.json") -Value '{}' -Encoding UTF8
    Set-Content -LiteralPath (Join-Path $contentRoot "Sprites/Collision Maps/TestMapS.images/image 0.png") -Value 'collision-runtime-data' -Encoding ASCII
    Set-Content -LiteralPath (Join-Path $contentRoot "Browser/Atlases/test.png") -Value 'generated-atlas-data' -Encoding ASCII

    $bootstrapManifest = '{"Atlases":[{"ImagePath":"Content/Browser/Atlases/test.png"}]}'
    $wrappedManifest = '{"Manifest":{"Atlases":[{"ImagePath":"Content/Browser/Atlases/test.png"}],"Sprites":{"test":{}}}}'
    Set-Content -LiteralPath (Join-Path $contentRoot "Browser/Manifests/bootstrap-manifest.json") -Value $bootstrapManifest -Encoding UTF8
    Set-Content -LiteralPath (Join-Path $contentRoot "Browser/Manifests/stock-pack-atlas-manifest.json") -Value $wrappedManifest -Encoding UTF8
    Set-Content -LiteralPath (Join-Path $contentRoot "Browser/Manifests/gamemaker-atlas-manifest.json") -Value $wrappedManifest -Encoding UTF8

    return $contentRoot
}

function Invoke-PolicySelfTest {
    $tempDirectory = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
    $testRoot = Join-Path $tempDirectory "OpenGarrison.PackagedContentPolicy.$([System.Guid]::NewGuid().ToString('N'))"

    try {
        $cleanRoot = Join-Path $testRoot "clean"
        $cleanContent = New-PolicyTestFixture -TestRoot $cleanRoot
        $resolvedCleanContent = Resolve-PackagedContentRoot -InputPath $cleanRoot
        Test-PackagedContentPolicy -ContentRoot $resolvedCleanContent -Quiet
        Write-Host "[verify-packaged-content:self-test] PASS clean distribution (including collision-map .images exception)"

        $testCases = @(
            [pscustomobject]@{
                Name = "GameMaker sprite XML"
                RelativePath = "Sprites/PlayerS.xml"
                Content = '<sprite />'
                Expected = "GameMaker sprite XML is source-only"
            },
            [pscustomobject]@{
                Name = "loose .images directory"
                RelativePath = "Sprites/PlayerS.images/image 0.png"
                Content = "loose-frame"
                Expected = "loose sprite frame directory is source-only"
            },
            [pscustomobject]@{
                Name = "stock sprite JSON"
                RelativePath = "Gameplay/stock.gg2/sprites/PlayerS.json"
                Content = '{}'
                Expected = "stock sprite JSON is atlas-build input"
            },
            [pscustomobject]@{
                Name = "loose stock gameplay frame"
                RelativePath = "Gameplay/stock.gg2/assets/player/image 0.png"
                Content = "loose-frame"
                Expected = "loose stock gameplay visual is source-only"
            }
        )

        foreach ($testCase in $testCases) {
            $caseRoot = Join-Path $testRoot ([System.Guid]::NewGuid().ToString('N'))
            $caseContent = New-PolicyTestFixture -TestRoot $caseRoot
            $testPath = Join-Path $caseContent $testCase.RelativePath
            New-Item -ItemType Directory -Path (Split-Path -Parent $testPath) -Force | Out-Null
            Set-Content -LiteralPath $testPath -Value $testCase.Content -Encoding UTF8

            $failureMessage = ""
            try {
                Test-PackagedContentPolicy -ContentRoot $caseContent -Quiet
            }
            catch {
                $failureMessage = $_.Exception.Message
            }

            if ($failureMessage.IndexOf($testCase.Expected, [System.StringComparison]::OrdinalIgnoreCase) -lt 0) {
                throw "Self-test '$($testCase.Name)' did not produce the expected policy failure. Actual error: $failureMessage"
            }

            Write-Host "[verify-packaged-content:self-test] PASS $($testCase.Name)"
        }

        Write-Host "[verify-packaged-content:self-test] All policy tests passed."
    }
    finally {
        if ((Test-Path -LiteralPath $testRoot) -and (Test-IsPathWithinDirectory -CandidatePath $testRoot -Directory $tempDirectory)) {
            Remove-Item -LiteralPath $testRoot -Recurse -Force
        }
    }
}

if ($SelfTest) {
    Invoke-PolicySelfTest
}
else {
    $contentRoot = Resolve-PackagedContentRoot -InputPath $Path
    Test-PackagedContentPolicy -ContentRoot $contentRoot
}
