using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenGarrison.Core;

public static class CustomMapPackageDirectoryPublisher
{
    public sealed record Result(
        int CopiedTopLevelLegacyPngCount,
        int ConvertedLegacyPngCount,
        int KeptLegacyPngCount,
        int DroppedLegacyPngCount,
        int CopiedStockPackageCount);

    public static Result Publish(
        string? sourceMapsDirectory,
        string? stockMapsDirectory,
        string destinationMapsDirectory,
        bool removeConvertedLegacyPngs = true,
        bool dropUnconvertedLegacyPngs = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationMapsDirectory);

        Directory.CreateDirectory(destinationMapsDirectory);

        var copiedTopLevelLegacyPngCount = 0;
        if (!string.IsNullOrWhiteSpace(sourceMapsDirectory) && Directory.Exists(sourceMapsDirectory))
        {
            CopyDirectoryContents(sourceMapsDirectory, destinationMapsDirectory);
            copiedTopLevelLegacyPngCount = Directory
                .EnumerateFiles(sourceMapsDirectory, "*.png", SearchOption.TopDirectoryOnly)
                .Count();
        }

        var convertedLegacyPngCount = 0;
        var keptLegacyPngCount = 0;
        var droppedLegacyPngCount = 0;
        foreach (var legacyPngPath in Directory
            .EnumerateFiles(destinationMapsDirectory, "*.png", SearchOption.TopDirectoryOnly)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase))
        {
            if (CustomMapLegacyPackageAutoConverter.TryConvertLegacyPng(legacyPngPath, out var manifestPath, out _)
                && File.Exists(manifestPath))
            {
                convertedLegacyPngCount += 1;
                if (removeConvertedLegacyPngs)
                {
                    File.Delete(legacyPngPath);
                }
            }
            else
            {
                if (dropUnconvertedLegacyPngs)
                {
                    File.Delete(legacyPngPath);
                    droppedLegacyPngCount += 1;
                }
                else
                {
                    keptLegacyPngCount += 1;
                }
            }
        }

        var copiedStockPackageCount = 0;
        if (!string.IsNullOrWhiteSpace(stockMapsDirectory) && Directory.Exists(stockMapsDirectory))
        {
            foreach (var stockPackageDirectory in Directory
                .EnumerateDirectories(stockMapsDirectory, "*", SearchOption.TopDirectoryOnly)
                .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase))
            {
                var packageName = Path.GetFileName(Path.TrimEndingDirectorySeparator(stockPackageDirectory));
                if (string.IsNullOrWhiteSpace(packageName))
                {
                    continue;
                }

                var manifestPath = Path.Combine(stockPackageDirectory, $"{packageName}.json");
                if (!CustomMapPackageImporter.TryReadManifest(manifestPath, out _, out _))
                {
                    continue;
                }

                var destinationPackageDirectory = Path.Combine(destinationMapsDirectory, packageName);
                if (Directory.Exists(destinationPackageDirectory))
                {
                    continue;
                }

                CopyDirectoryContents(stockPackageDirectory, destinationPackageDirectory);
                copiedStockPackageCount += 1;
            }
        }

        return new Result(
            copiedTopLevelLegacyPngCount,
            convertedLegacyPngCount,
            keptLegacyPngCount,
            droppedLegacyPngCount,
            copiedStockPackageCount);
    }

    private static void CopyDirectoryContents(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (var sourceFile in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.TopDirectoryOnly))
        {
            File.Copy(sourceFile, Path.Combine(destinationDirectory, Path.GetFileName(sourceFile)), overwrite: true);
        }

        foreach (var sourceSubdirectory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.TopDirectoryOnly))
        {
            CopyDirectoryContents(
                sourceSubdirectory,
                Path.Combine(destinationDirectory, Path.GetFileName(sourceSubdirectory)));
        }
    }
}
