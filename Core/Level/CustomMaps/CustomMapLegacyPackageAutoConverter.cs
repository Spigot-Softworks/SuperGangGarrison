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
