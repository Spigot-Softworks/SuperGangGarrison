using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Text.Json;
using OpenGarrison.Client;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace OpenGarrison.PluginHost.Tests;

public sealed class BrowserAtlasPixelConversionTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(3, 2)]
    [InlineData(17, 9)]
    [InlineData(257, 3)]
    public void RowConversionMatchesLegacyCopyPipeline(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y += 1)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x += 1)
                {
                    var index = y * width + x;
                    row[x] = new Rgba32(
                        unchecked((byte)(17 + (index * 13))),
                        unchecked((byte)(251 - (index * 19))),
                        unchecked((byte)(index * 29)),
                        unchecked((byte)(index * 37)));
                }
            }
        });

        var expected = ConvertWithLegacyCopyPipeline(image);
        var actual = BrowserAtlasPixelConverter.ConvertToPremultipliedColors(image);

        Assert.Equal(expected, actual);
        Assert.Equal(XnaColor.Transparent, actual[0]);
        if (width * height > 256)
        {
            Assert.Equal(256, actual.Select(pixel => pixel.A).Distinct().Count());
        }
    }

    [Fact]
    public void FullyTransparentPixelsDiscardHiddenRgbExactlyAsBefore()
    {
        using var image = new Image<Rgba32>(3, 2);
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y += 1)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x += 1)
                {
                    row[x] = new Rgba32((byte)(200 - (x * 31)), (byte)(30 + (y * 61)), 173, 0);
                }
            }
        });

        var actual = BrowserAtlasPixelConverter.ConvertToPremultipliedColors(image);

        Assert.All(actual, pixel => Assert.Equal(XnaColor.Transparent, pixel));
    }

    [Fact]
    public void PngDecodeThenConversionPreservesTransparentColoredPixels()
    {
        using var source = new Image<Rgba32>(5, 3);
        source.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y += 1)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x += 1)
                {
                    row[x] = new Rgba32(
                        (byte)(31 + (x * 37)),
                        (byte)(17 + (y * 61)),
                        (byte)(211 - (x * 19)),
                        unchecked((byte)((x + y) % 2 == 0 ? 0 : 1 + (x * 51) + (y * 67))));
                }
            }

            accessor.GetRowSpan(0)[0] = new Rgba32(231, 19, 144, 0);
        });

        using var encoded = new MemoryStream();
        source.SaveAsPng(encoded, new PngEncoder { TransparentColorMode = PngTransparentColorMode.Preserve });
        using var decoded = Image.Load<Rgba32>(encoded.ToArray());

        Assert.Equal(new Rgba32(231, 19, 144, 0), decoded[0, 0]);
        Assert.True(
            BrowserAtlasPixelConverter.ConvertToPremultipliedColors(decoded)
                .AsSpan()
                .SequenceEqual(ConvertWithLegacyCopyPipeline(decoded)),
            "PNG-decoded pixels must convert identically to the previous copy pipeline.");
    }

    [Fact]
    public void WarmedLargeAtlasConversionReportsAllocationsAndCpuTime()
    {
        const int width = 2048;
        const int height = 2048;
        var pixelCount = width * height;
        using var image = CreateBenchmarkImage(width, height);

        // Warm both conversion paths on the actual atlas dimensions before measuring.
        _ = BrowserAtlasPixelConverter.ConvertToPremultipliedColors(image);
        _ = ConvertWithLegacyCopyPipeline(image);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var productionAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var productionTimer = Stopwatch.StartNew();
        var productionOutput = BrowserAtlasPixelConverter.ConvertToPremultipliedColors(image);
        productionTimer.Stop();
        var productionAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - productionAllocatedBefore;

        var legacyAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var legacyTimer = Stopwatch.StartNew();
        var legacyOutput = ConvertWithLegacyCopyPipeline(image);
        legacyTimer.Stop();
        var legacyAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - legacyAllocatedBefore;

        Assert.True(
            productionOutput.AsSpan().SequenceEqual(legacyOutput),
            "The production row converter must match the legacy copy pipeline byte for byte.");

        var allocationReductionBytes = legacyAllocatedBytes - productionAllocatedBytes;
        var removedSourceBufferBytes = (long)pixelCount * Unsafe.SizeOf<Rgba32>();
        Assert.True(
            allocationReductionBytes >= removedSourceBufferBytes - (64 * 1024),
            $"Expected roughly one full-page RGBA buffer ({removedSourceBufferBytes:N0} bytes) of allocation reduction; observed {allocationReductionBytes:N0} bytes.");

        var benchmark = new
        {
            schema_version = 1,
            width,
            height,
            warmup_runs_per_path = 1,
            byte_equal = true,
            production = new
            {
                allocated_bytes = productionAllocatedBytes,
                elapsed_ms = productionTimer.Elapsed.TotalMilliseconds,
            },
            legacy_copy_pipeline = new
            {
                allocated_bytes = legacyAllocatedBytes,
                elapsed_ms = legacyTimer.Elapsed.TotalMilliseconds,
            },
            allocation_reduction_bytes = allocationReductionBytes,
            removed_rgba_source_buffer_bytes = removedSourceBufferBytes,
            runtime = new
            {
                framework = RuntimeInformation.FrameworkDescription,
                process_architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                os = RuntimeInformation.OSDescription,
            },
        };

        var benchmarkPath = Environment.GetEnvironmentVariable("OPENGARRISON_ATLAS_BENCHMARK_PATH");
        if (!string.IsNullOrWhiteSpace(benchmarkPath))
        {
            var fullPath = Path.GetFullPath(benchmarkPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, JsonSerializer.Serialize(benchmark, new JsonSerializerOptions { WriteIndented = true }));
        }

        GC.KeepAlive(productionOutput);
        GC.KeepAlive(legacyOutput);
    }

    private static XnaColor[] ConvertWithLegacyCopyPipeline(Image<Rgba32> image)
    {
        var source = new Rgba32[image.Width * image.Height];
        image.CopyPixelDataTo(source);

        var converted = new XnaColor[source.Length];
        for (var index = 0; index < source.Length; index += 1)
        {
            var pixel = source[index];
            if (pixel.A == 0)
            {
                converted[index] = XnaColor.Transparent;
                continue;
            }

            converted[index] = new XnaColor(
                (byte)((pixel.R * pixel.A + 127) / 255),
                (byte)((pixel.G * pixel.A + 127) / 255),
                (byte)((pixel.B * pixel.A + 127) / 255),
                pixel.A);
        }

        return converted;
    }

    private static Image<Rgba32> CreateBenchmarkImage(int width, int height)
    {
        var image = new Image<Rgba32>(width, height);
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y += 1)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x += 1)
                {
                    row[x] = new Rgba32(
                        unchecked((byte)(x * 17 + y * 13)),
                        unchecked((byte)(x * 7 + y * 29)),
                        unchecked((byte)(x * 23 + y * 3)),
                        unchecked((byte)(x * 11 + y * 19)));
                }
            }
        });

        return image;
    }
}
