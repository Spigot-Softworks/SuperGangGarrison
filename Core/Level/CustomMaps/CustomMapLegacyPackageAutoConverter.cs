using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenGarrison.Core;

public static class CustomMapLegacyPackageAutoConverter
{
    private const string MarkerFileName = ".opengarrison-auto-converted";

    public static void ConvertTopLevelLegacyPngs(IEnumerable<string> customMapsDirectories)
    {
        if (OperatingSystem.IsBrowser())
        {
            return;
        }

        foreach (var customMapsDirectory in customMapsDirectories)
        {
            ConvertTopLevelLegacyPngs(customMapsDirectory);
        }
    }

    public static bool TryConvertLegacyPng(string legacyPngPath, out string manifestPath, out string error)
    {
        try { return TryConvertLegacyPngCore(legacyPngPath, out manifestPath, out error); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            manifestPath = string.Empty;
            error = $"Legacy map conversion failed: {ex.Message}";
            return false;
        }
    }

    private static bool TryConvertLegacyPngCore(string legacyPngPath, out string manifestPath, out string error)
    {
        manifestPath = string.Empty;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(legacyPngPath)
            || !File.Exists(legacyPngPath)
            || !Path.GetExtension(legacyPngPath).Equals(".png", StringComparison.OrdinalIgnoreCase))
        {
            error = "Legacy map PNG was not found.";
            return false;
        }

        var fullLegacyPath = Path.GetFullPath(legacyPngPath);
        var mapName = Path.GetFileNameWithoutExtension(fullLegacyPath);
        if (string.IsNullOrWhiteSpace(mapName))
        {
            error = "Legacy map PNG must have a file name.";
            return false;
        }

        var mapDirectory = Path.GetDirectoryName(fullLegacyPath);
        if (string.IsNullOrWhiteSpace(mapDirectory))
        {
            error = "Legacy map PNG must be inside a map directory.";
            return false;
        }

        var packageDirectory = Path.Combine(mapDirectory, mapName);
        manifestPath = Path.Combine(packageDirectory, $"{mapName}.json");
        if (File.Exists(manifestPath))
        {
            var marker = Path.Combine(packageDirectory, MarkerFileName);
            if (!File.Exists(marker)) return true; // A user-authored package is not a conversion cache.
            var values = File.ReadAllLines(marker).Where(line => line.Contains('='))
                .Select(line => line.Split('=', 2)).ToDictionary(pair => pair[0], pair => pair[1]);
            var sourceHash = CustomMapHashService.ComputeSha256(fullLegacyPath);
            if (values.GetValueOrDefault("sourceHash") == sourceHash) return true;
            if (!values.TryGetValue("packageHash", out var originalPackageHash)
                || originalPackageHash != CustomMapHashService.ComputePackageSha256(manifestPath))
            {
                error = "The PNG and its converted package may both have changed. Open the intended file in Builder and use Save As to resolve the duplicate.";
                return false;
            }
            var updated = CustomMapBuilderPngImporter.Import(fullLegacyPath);
            if (updated is null) { error = "The edited PNG could not be loaded."; return false; }
            CustomMapPackageExporter.Export(updated, manifestPath);
            WriteMarkerFile(packageDirectory, fullLegacyPath);
            return true;
        }

        if (Directory.Exists(packageDirectory)
            && Directory.EnumerateFileSystemEntries(packageDirectory).Any())
        {
            error = "Package directory already exists and is not empty.";
            return false;
        }

        var document = CustomMapBuilderPngImporter.Import(fullLegacyPath);
        if (document is null)
        {
            error = "Legacy map PNG does not contain editable custom-map data.";
            return false;
        }

        var tempDirectory = Path.Combine(mapDirectory, $".{mapName}.convert-{Guid.NewGuid():N}");
        try
        {
            var tempManifestPath = Path.Combine(tempDirectory, $"{mapName}.json");
            CustomMapPackageExporter.Export(
                document.NormalizeForEditing() with
                {
                    Name = mapName,
                    BackgroundImagePath = fullLegacyPath,
                },
                tempManifestPath);

            if (CustomMapPackageImporter.Import(tempManifestPath) is null)
            {
                error = "Converted package could not be imported.";
                return false;
            }

            if (Directory.Exists(packageDirectory))
            {
                Directory.Delete(packageDirectory, recursive: false);
            }

            Directory.Move(tempDirectory, packageDirectory);
            WriteMarkerFile(packageDirectory, fullLegacyPath);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            error = $"Legacy map conversion failed: {ex.Message}";
            return false;
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    private static void ConvertTopLevelLegacyPngs(string customMapsDirectory)
    {
        if (string.IsNullOrWhiteSpace(customMapsDirectory) || !Directory.Exists(customMapsDirectory))
        {
            return;
        }

        foreach (var legacyPngPath in Directory
            .EnumerateFiles(customMapsDirectory, "*.png", SearchOption.TopDirectoryOnly)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase))
        {
            _ = TryConvertLegacyPng(legacyPngPath, out _, out _);
        }
    }

    private static void WriteMarkerFile(string packageDirectory, string sourcePath)
    {
        var markerPath = Path.Combine(packageDirectory, MarkerFileName);
        try
        {
            File.WriteAllLines(markerPath, new[]
            {
                "This package was generated automatically from a legacy OpenGarrison custom-map PNG.",
                $"source={Path.GetFileName(sourcePath)}",
                $"convertedUtc={DateTime.UtcNow:O}",
                $"sourceHash={CustomMapHashService.ComputeSha256(sourcePath)}",
                $"packageHash={CustomMapHashService.ComputePackageSha256(Path.Combine(packageDirectory, Path.GetFileNameWithoutExtension(sourcePath) + ".json"))}",
            });
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch
        {
        }
    }
}
