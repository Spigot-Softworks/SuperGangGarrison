#nullable enable

using System;
using System.IO;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

/// <summary>
/// Recovery drafts: the builder's periodic safety copy of unsaved work. Each editing session
/// writes one draft named after the map (plus a short session id so two sessions of the same
/// map do not overwrite each other), and nothing is written for an empty map.
/// </summary>
public partial class Game1
{
    private const int GarrisonBuilderRecoveryNameLength = 48;

    /// <summary>Drafts this small may be empty leftovers; only they are opened to check.</summary>
    private const long GarrisonBuilderRecoveryPruneMaxBytes = 8 * 1024;

    /// <summary>The draft this session last wrote (so a renamed map does not leave its old draft behind).</summary>
    private string? _builderRecoveryWrittenPath;

    /// <summary>This session's draft path: "mapname_1a2b3c4d.ogmap" in the recovery folder.</summary>
    private string GetGarrisonBuilderRecoveryPath()
    {
        // A draft reopened from the recovery folder keeps the map's own name, not the draft's.
        var mapName = IsGarrisonBuilderRecoveryPath(_builderSavePath) ? _builderDocument.Name : GetGarrisonBuilderEffectiveMapName();
        var name = GarrisonBuilderQuickTestNaming.SanitizeDocumentName(mapName);
        if (name.Length > GarrisonBuilderRecoveryNameLength)
        {
            name = name[..GarrisonBuilderRecoveryNameLength];
        }

        var session = _builderRecoveryId.Length > 8 ? _builderRecoveryId[..8] : _builderRecoveryId;
        return Path.Combine(BuilderRecoveryDirectory, $"{name}_{session}{BuilderProjectStore.Extension}");
    }

    private static bool IsGarrisonBuilderRecoveryPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            var folder = Path.GetFullPath(BuilderRecoveryDirectory) + Path.DirectorySeparatorChar;
            return Path.GetFullPath(path).StartsWith(folder, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    /// <summary>Nothing worth recovering: no entities, images, collision, resources or layers.</summary>
    internal static bool IsGarrisonBuilderDocumentEmpty(CustomMapBuilderDocument document)
    {
        return document.Entities.Count == 0
            && string.IsNullOrWhiteSpace(document.BackgroundImagePath)
            && string.IsNullOrWhiteSpace(document.WalkmaskImagePath)
            && string.IsNullOrEmpty(document.EmbeddedWalkmaskSection)
            && document.Resources.Count == 0
            && document.ParallaxLayers.Count == 0;
    }

    /// <summary>
    /// Writes a recovery draft unless the map is empty, then removes this session's previous
    /// draft if the name changed. Safe to run off the main thread.
    /// </summary>
    private static void WriteGarrisonBuilderRecoveryDraft(CustomMapBuilderDocument document, string path, string? previousPath)
    {
        if (IsGarrisonBuilderDocumentEmpty(document))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        BuilderProjectStore.Save(document, path);
        if (!string.IsNullOrEmpty(previousPath)
            && !previousPath.Equals(path, StringComparison.OrdinalIgnoreCase)
            && File.Exists(previousPath))
        {
            try
            {
                File.Delete(previousPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A leftover draft is harmless.
            }
        }
    }

    /// <summary>Starts this session's draft write (returns the background task).</summary>
    private System.Threading.Tasks.Task StartGarrisonBuilderRecoveryDraftWrite(CustomMapBuilderDocument document)
    {
        var path = GetGarrisonBuilderRecoveryPath();
        var previous = _builderRecoveryWrittenPath;
        if (!IsGarrisonBuilderDocumentEmpty(document))
        {
            _builderRecoveryWrittenPath = path;
        }

        return System.Threading.Tasks.Task.Run(() => WriteGarrisonBuilderRecoveryDraft(document, path, previous));
    }

    /// <summary>The last draft write, done now (on exit, when no later frame will finish it).</summary>
    private void WriteGarrisonBuilderRecoveryDraftNow(CustomMapBuilderDocument document)
    {
        var path = GetGarrisonBuilderRecoveryPath();
        var previous = _builderRecoveryWrittenPath;
        WriteGarrisonBuilderRecoveryDraft(document, path, previous);
        if (!IsGarrisonBuilderDocumentEmpty(document))
        {
            _builderRecoveryWrittenPath = path;
        }
    }

    /// <summary>
    /// Deletes empty drafts left by earlier versions (sessions that never had anything in
    /// them), so the recovery list only holds real work. Only small drafts are opened.
    /// </summary>
    private void PruneEmptyGarrisonBuilderRecoveryDrafts()
    {
        try
        {
            if (!Directory.Exists(BuilderRecoveryDirectory))
            {
                return;
            }

            var removed = 0;
            foreach (var path in Directory.GetFiles(BuilderRecoveryDirectory, "*" + BuilderProjectStore.Extension, SearchOption.TopDirectoryOnly))
            {
                try
                {
                    if (new FileInfo(path).Length > GarrisonBuilderRecoveryPruneMaxBytes
                        || path.Equals(_builderRecoveryWrittenPath, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var document = BuilderProjectStore.Load(path, Path.Combine(BuilderRecoveryDirectory, "assets"));
                    if (IsGarrisonBuilderDocumentEmpty(document))
                    {
                        File.Delete(path);
                        removed += 1;
                    }
                }
                catch (Exception)
                {
                    // Unreadable drafts stay; the person may still want to look at them.
                }
            }

            if (removed > 0)
            {
                AddConsoleLine($"builder: removed {removed} empty recovery draft{(removed == 1 ? string.Empty : "s")}");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Listing failed; the chooser still opens.
        }
    }
}
