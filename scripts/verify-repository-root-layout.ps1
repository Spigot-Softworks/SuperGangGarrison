[CmdletBinding()]
param(
    [string]$Root = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($Root)) {
    $Root = Join-Path $PSScriptRoot ".."
}

$rootPath = [System.IO.Path]::GetFullPath($Root)
if (-not (Test-Path -LiteralPath $rootPath -PathType Container)) {
    throw "Repository root does not exist: $rootPath"
}

$allowedFiles = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
foreach ($name in @(
    ".git",
    ".editorconfig",
    ".gitattributes",
    ".gitignore",
    "Directory.Build.props",
    "Directory.Build.targets",
    "GPL.txt",
    "LICENSE",
    "OpenGarrison.sln",
    "README.md"
)) {
    [void]$allowedFiles.Add($name)
}

$allowedDirectories = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
foreach ($name in @(
    ".git", ".github", ".local", ".vscode",
    "artifacts", "bin", "dist", "obj",
    "Bootstrap", "Client", "Client.Browser", "Client.Shared", "Core", "docs",
    "Maps", "Modern", "Networking", "packaging", "Plugins", "Protocol", "scripts", "Server", "ServerLauncher",
    "services", "SourceAssets", "Tests", "Tools", "Updater"
)) {
    [void]$allowedDirectories.Add($name)
}

$violations = [System.Collections.Generic.List[string]]::new()
foreach ($file in Get-ChildItem -LiteralPath $rootPath -Force -File) {
    if (-not $allowedFiles.Contains($file.Name)) {
        $violations.Add("unowned root file: $($file.Name)")
    }
}

foreach ($directory in Get-ChildItem -LiteralPath $rootPath -Force -Directory) {
    $pathSpec = "$($directory.Name)/**"
    $trackedEntries = @(git -C $rootPath ls-files -- $pathSpec | Where-Object {
        Test-Path -LiteralPath (Join-Path $rootPath $_)
    })
    $untrackedEntries = @(git -C $rootPath ls-files --others --exclude-standard -- $pathSpec)
    if ($trackedEntries.Count -eq 0 -and $untrackedEntries.Count -eq 0) {
        continue
    }

    if (-not $allowedDirectories.Contains($directory.Name)) {
        $violations.Add("unowned root directory: $($directory.Name)")
    }
}

if ($violations.Count -gt 0) {
    $details = ($violations | Sort-Object | ForEach-Object { "  - $_" }) -join [Environment]::NewLine
    throw "Repository root layout contains unowned entries. Move runtime content to its owning project, editable originals to SourceAssets, packaging inputs to packaging, and local work to .local:$([Environment]::NewLine)$details"
}

Write-Host "[PASS] Repository root contains $($allowedFiles.Count) owned file(s) and no unowned files or directories."
