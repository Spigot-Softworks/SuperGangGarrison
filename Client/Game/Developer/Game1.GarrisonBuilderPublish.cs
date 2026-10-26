#nullable enable

using System;
using System.IO;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

/// <summary>
/// Work in progress versus finished maps. Saving keeps a draft wherever you choose; quick
/// tests run on a temporary copy that is deleted when you return to the editor; "Publish
/// final map" exports the finished, validated map into the game's maps folder, where it
/// shows up in Practice and hosting like any other custom map.
/// </summary>
public partial class Game1
{
    /// <summary>Marks a maps-folder package as made by "Publish final map", so publishing again may replace it.</summary>
    private const string GarrisonBuilderPublishedMarkerFileName = ".builder-published";

    /// <summary>The temporary package the running quick test plays (deleted on return to the editor).</summary>
    private string _garrisonBuilderQuickTestPackageDirectory = string.Empty;

    /// <summary>
    /// Deletes quick test copies from the maps folder: the one just played, and any left by a
    /// quick test that ended some other way (game closed, crash). Never while one is running.
    /// </summary>
    private void CleanUpGarrisonBuilderQuickTestCopies()
    {
        if (_garrisonBuilderQuickTestActive)
        {
            return;
        }

        _garrisonBuilderQuickTestPackageDirectory = string.Empty;
        try
        {
            if (!Directory.Exists(RuntimePaths.MapsDirectory))
            {
                return;
            }

            var removed = 0;
            foreach (var directory in Directory.GetDirectories(RuntimePaths.MapsDirectory, GarrisonBuilderQuickTestNaming.FolderPrefix + "*", SearchOption.TopDirectoryOnly))
            {
                // Only the builder's own quick test folders (and their staging/backup copies).
                if (!GarrisonBuilderQuickTestNaming.IsQuickTestLevel(Path.GetFileName(directory)))
                {
                    continue;
                }

                TryDeleteGarrisonBuilderDirectory(directory);
                if (!Directory.Exists(directory))
                {
                    removed += 1;
                }
            }

            if (removed > 0)
            {
                SimpleLevelFactory.ClearCachedCatalog();
                AddConsoleLine($"builder: removed {removed} quick test cop{(removed == 1 ? "y" : "ies")}");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Leftovers are hidden from map lists anyway; try again next time.
        }
    }

    /// <summary>The name a published map gets: the map's own name, made safe for a folder and level name.</summary>
    private string GetGarrisonBuilderPublishName()
    {
        var mapName = IsGarrisonBuilderRecoveryPath(_builderSavePath) ? _builderDocument.Name : GetGarrisonBuilderEffectiveMapName();
        return GarrisonBuilderQuickTestNaming.SanitizeDocumentName(mapName);
    }

    /// <summary>File menu: export the finished map into the maps folder (after it passes validation).</summary>
    private void PublishGarrisonBuilderFinalMap()
    {
        if (!CommitGarrisonBuilderActiveEdits()) return;
        if (TryPromptGarrisonBuilderMapNameCollisionIfNeeded(GarrisonBuilderMapNameCollisionPendingAction.Save))
        {
            return;
        }

        if (!CanQuickTestGarrisonBuilderDocument())
        {
            SetGarrisonBuilderPublishStatus("Not published: the map needs a background and a walkmask.");
            return;
        }

        var publishName = GetGarrisonBuilderPublishName();
        if (GarrisonBuilderQuickTestNaming.IsQuickTestLevel(publishName))
        {
            SetGarrisonBuilderPublishStatus("Not published: rename the map first (that name is reserved for quick tests).");
            return;
        }

        try
        {
            UpdateGarrisonBuilderDocumentEntities();
            ApplyGarrisonBuilderMapModeMetadata();
            ApplyGarrisonBuilderEntitySchemaMetadata();
            var exportDocument = _builderDocument with
            {
                Name = publishName,
                Entities = CustomMapBuilderEntityNormalization.ResolveForExport(_builderDocument.Entities),
            };
            var validation = CustomMapBuilderValidator.Validate(exportDocument, _builderSelectedGameMode);
            if (!validation.IsValid)
            {
                SetGarrisonBuilderPublishStatus("Not published, the map is unfinished: " + validation.Issues[0].Message);
                return;
            }

            Directory.CreateDirectory(RuntimePaths.MapsDirectory);
            var packageDirectory = Path.Combine(RuntimePaths.MapsDirectory, publishName);
            if (Directory.Exists(packageDirectory)
                && !File.Exists(Path.Combine(packageDirectory, GarrisonBuilderPublishedMarkerFileName)))
            {
                SetGarrisonBuilderPublishStatus($"Not published: Maps/{publishName} already holds another map. Rename this map, or move that folder.");
                return;
            }

            var stagingDirectory = Path.Combine(RuntimePaths.MapsDirectory, $"{publishName}.tmp-{Guid.NewGuid():N}");
            try
            {
                CustomMapPackageExporter.Export(exportDocument, stagingDirectory);
                File.WriteAllText(
                    Path.Combine(stagingDirectory, GarrisonBuilderPublishedMarkerFileName),
                    "Published from the Garrison Builder. Publishing this map again replaces this folder.");
                ReplaceGarrisonBuilderQuickTestPackageDirectory(stagingDirectory, packageDirectory);
            }
            finally
            {
                TryDeleteGarrisonBuilderDirectory(stagingDirectory);
            }

            SimpleLevelFactory.ClearCachedCatalog();
            var manifestPath = CustomMapPackageExporter.ResolveManifestOutputPath(exportDocument, packageDirectory);
            SetGarrisonBuilderPublishStatus($"Published {publishName}: ready to play in Practice and hosting.");
            AddConsoleLine($"builder published: {manifestPath}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or InvalidDataException or ArgumentException)
        {
            SetGarrisonBuilderPublishStatus("Publishing failed: " + ex.Message);
        }
    }

    private void SetGarrisonBuilderPublishStatus(string message)
    {
        _builderStatus = message;
        AddConsoleLine("builder: " + message);
    }
}
