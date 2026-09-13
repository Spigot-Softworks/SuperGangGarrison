# Diagnostic only: exercise the existing AOT reader's legacy graph path without
# publishing. Release packaging uses compact gzip envelopes instead.
param([Parameter(Mandatory=$true)][string]$Bundle)
$ErrorActionPreference = 'Stop'
$bundlePath = (Resolve-Path -LiteralPath $Bundle).Path
$artifactRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../../artifacts')).Path + [IO.Path]::DirectorySeparatorChar
if (!$bundlePath.StartsWith($artifactRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Use an isolated build under artifacts' }
$inputBytes = [IO.File]::ReadAllBytes($bundlePath)
$inputBuffer = [IO.MemoryStream]::new($inputBytes, $false)
$source = [IO.Compression.ZipArchive]::new($inputBuffer, [IO.Compression.ZipArchiveMode]::Read)
$outputBuffer = [IO.MemoryStream]::new()
$target = [IO.Compression.ZipArchive]::new($outputBuffer, [IO.Compression.ZipArchiveMode]::Create, $true)
$converted = 0
try {
    foreach ($item in $source.Entries) {
        $entry = $target.CreateEntry($item.FullName, [IO.Compression.CompressionLevel]::SmallestSize)
        $readerStream = $item.Open()
        $writerStream = $entry.Open()
        try {
            if ($item.FullName.EndsWith('.og2nav.bin')) {
                $header = [IO.BinaryReader]::new($readerStream, [Text.Encoding]::UTF8, $true)
                if ($header.ReadUInt32() -ne 0x32474F47 -or $header.ReadInt32() -ne 4 -or $header.ReadByte() -ne 1) { throw 'Expected a Brotli navigation envelope' }
                $expectedLength = $header.ReadInt64()
                $decoder = [IO.Compression.BrotliStream]::new($readerStream, [IO.Compression.CompressionMode]::Decompress, $true)
                $plain = [IO.MemoryStream]::new()
                try {
                    $decoder.CopyTo($plain)
                    if ($plain.Length -ne $expectedLength) { throw 'Incomplete graph' }
                    $plain.Position = 0
                    $plain.CopyTo($writerStream)
                } finally { $plain.Dispose(); $decoder.Dispose(); $header.Dispose() }
                $converted++
            } else { $readerStream.CopyTo($writerStream) }
        } finally { $readerStream.Dispose(); $writerStream.Dispose() }
    }
} finally { $target.Dispose(); $source.Dispose(); $inputBuffer.Dispose() }
try { [IO.File]::WriteAllBytes($bundlePath, $outputBuffer.ToArray()) }
finally { $outputBuffer.Dispose() }
Write-Output "Converted $converted graphs for a diagnostic run; this bundle is not the gzip release."
