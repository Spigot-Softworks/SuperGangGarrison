using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenGarrison.ClientShared;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;
using OpenGarrison.Tools.BrowserAssetBuilder.Atlas;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace OpenGarrison.Tools.BrowserAssetBuilder;

internal static class BrowserAssetBuildPipeline
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public static BrowserAssetBuildReport Run(BrowserAssetBuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        ContentRoot.Initialize(context.ContentRoot);
        EnsureDirectories(context);

        var generatedFiles = new List<string>();
        var warnings = new List<string>();

        var legacyManifest = BuildLegacyGameMakerManifest(context, generatedFiles);
        var stockPackDefinition = BuildLegacyStockPackDefinition(context, generatedFiles);
        BuildLegacyBundles(context, legacyManifest, generatedFiles);
        BuildClientPluginBundle(context, generatedFiles);

        var bootstrapAtlasManifest = BuildBootstrapAtlasManifest(context, warnings, generatedFiles);
        var stockGameplayAtlasManifest = BuildStockGameplayAtlasManifest(context, stockPackDefinition, warnings, generatedFiles);
        var gameMakerAtlasManifest = BuildGameMakerAtlasManifest(context, legacyManifest, warnings, generatedFiles);

        WriteJson(Path.Combine(context.BrowserManifestsRoot, "bootstrap-manifest.json"), bootstrapAtlasManifest, generatedFiles);
        WriteJson(Path.Combine(context.BrowserManifestsRoot, "stock-pack-atlas-manifest.json"), stockGameplayAtlasManifest, generatedFiles);
        WriteJson(Path.Combine(context.BrowserManifestsRoot, "gamemaker-atlas-manifest.json"), gameMakerAtlasManifest, generatedFiles);

        var report = new BrowserAssetBuildReport(
            GeneratedAtlasCount: 3,
            GeneratedAtlasPageCount: bootstrapAtlasManifest.Atlases.Count + stockGameplayAtlasManifest.Manifest.Atlases.Count + gameMakerAtlasManifest.Manifest.Atlases.Count,
            GeneratedSpriteCount: bootstrapAtlasManifest.Sprites.Count + stockGameplayAtlasManifest.Manifest.Sprites.Count + gameMakerAtlasManifest.Manifest.Sprites.Count,
            GeneratedFiles: generatedFiles,
            Warnings: warnings);

        PruneLegacyDistributionArtifacts(context);
        if (context.PruneDeprecatedGameMakerMetadata)
        {
            PruneDeprecatedGameMakerMetadata(context);
        }

        return report;
    }

    public static string WriteGameMakerManifestOnly(BrowserAssetBuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        ContentRoot.Initialize(context.ContentRoot);
        Directory.CreateDirectory(context.OutputContentRoot);

        var manifest = GameMakerAssetManifestImporter.ImportProjectAssets();
        var document = BrowserGameMakerAssetManifestDocument.FromManifest(manifest);
        var outputPath = Path.Combine(context.OutputContentRoot, GameMakerRuntimeAssetManifestLoader.ManifestRelativePath);
        WriteText(outputPath, JsonSerializer.Serialize(document, JsonOptions), []);
        return outputPath;
    }

    private static void EnsureDirectories(BrowserAssetBuildContext context)
    {
        if (Directory.Exists(context.BrowserOutputRoot))
        {
            Directory.Delete(context.BrowserOutputRoot, recursive: true);
        }

        var legacyStockPackBundlePath = Path.Combine(context.OutputContentRoot, "Gameplay", "stock.gg2", "_browser-pack-assets.zip");
        if (File.Exists(legacyStockPackBundlePath))
        {
            File.Delete(legacyStockPackBundlePath);
        }

        Directory.CreateDirectory(context.OutputContentRoot);
        Directory.CreateDirectory(context.BrowserWwwRoot);
        Directory.CreateDirectory(context.BrowserOutputRoot);
        Directory.CreateDirectory(context.BrowserAtlasesRoot);
        Directory.CreateDirectory(context.BrowserManifestsRoot);
        Directory.CreateDirectory(context.BrowserBootstrapRoot);
        Directory.CreateDirectory(Path.Combine(context.OutputContentRoot, "Gameplay", "stock.gg2"));
    }

    private static GameMakerAssetManifest BuildLegacyGameMakerManifest(BrowserAssetBuildContext context, List<string> generatedFiles)
    {
        var manifest = GameMakerAssetManifestImporter.ImportProjectAssets();
        var document = BrowserGameMakerAssetManifestDocument.FromManifest(manifest);
        WriteText(Path.Combine(context.OutputContentRoot, "_gamemaker-asset-manifest.json"), JsonSerializer.Serialize(document, JsonOptions), generatedFiles);
        return manifest;
    }

    private static GameplayModPackDefinition BuildLegacyStockPackDefinition(BrowserAssetBuildContext context, List<string> generatedFiles)
    {
        var stockPackDirectory = Path.Combine(context.ContentRoot, "Gameplay", "stock.gg2");
        var stockPackDefinition = GameplayModPackDirectoryLoader.LoadFromDirectory(stockPackDirectory);
        WriteText(
            Path.Combine(context.OutputContentRoot, "Gameplay", "stock.gg2", "_browser-pack-definition.json"),
            JsonSerializer.Serialize(BrowserGameplayModPackDefinitionDocument.FromDefinition(stockPackDefinition), JsonOptions),
            generatedFiles);
        return stockPackDefinition;
    }

    private static void BuildLegacyBundles(
        BrowserAssetBuildContext context,
        GameMakerAssetManifest manifest,
        List<string> generatedFiles)
    {
        BuildBundle(Path.Combine(context.OutputContentRoot, "_browser-bootstrap-assets.zip"), EnumerateBootstrapBundleEntries(context), generatedFiles);
        BuildBundle(Path.Combine(context.OutputContentRoot, "_browser-runtime-assets.zip"), EnumerateRuntimeBundleEntries(context, manifest), generatedFiles);
    }

    private static void BuildClientPluginBundle(BrowserAssetBuildContext context, List<string> generatedFiles)
    {
        if (string.IsNullOrWhiteSpace(context.PackagedClientPluginSourceRoot)
            || !Directory.Exists(context.PackagedClientPluginSourceRoot))
        {
            DeleteFileIfExists(Path.Combine(context.BrowserPluginsRoot, "_browser-client-plugins.zip"));
            return;
        }

        Directory.CreateDirectory(context.BrowserPluginsRoot);
        var outputPath = Path.Combine(context.BrowserPluginsRoot, "_browser-client-plugins.zip");
        BuildBundle(outputPath, EnumerateClientPluginBundleEntries(context), generatedFiles);
    }

    private static BrowserAtlasManifest BuildBootstrapAtlasManifest(
        BrowserAssetBuildContext context,
        List<string> warnings,
        List<string> generatedFiles)
    {
        var assets = BrowserBootstrapAssetCatalog.DefaultBinaryAssetPaths
            .Where(static path => path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => CreateSingleFrameSpriteInput(context, path, AtlasGroupingPolicy.GetBootstrapGroup(path), warnings))
            .Where(static input => input is not null)
            .Cast<AtlasSpriteInput>()
            .ToArray();

        return BuildAtlasManifest(context, "bootstrap", assets, generatedFiles);
    }

    private static BrowserGameplayAtlasManifest BuildStockGameplayAtlasManifest(
        BrowserAssetBuildContext context,
        GameplayModPackDefinition stockPackDefinition,
        List<string> warnings,
        List<string> generatedFiles)
    {
        var sprites = stockPackDefinition.Assets.Sprites.Values
            .OrderBy(static sprite => sprite.Id, StringComparer.Ordinal)
            .Select(sprite => CreateGameplaySpriteInput(context, sprite, warnings))
            .Where(static sprite => sprite is not null)
            .Cast<AtlasSpriteInput>()
            .ToArray();

        return new BrowserGameplayAtlasManifest(
            stockPackDefinition.Id,
            BuildAtlasManifest(context, "stock-pack", sprites, generatedFiles));
    }

    private static BrowserGameMakerAtlasManifest BuildGameMakerAtlasManifest(
        BrowserAssetBuildContext context,
        GameMakerAssetManifest manifest,
        List<string> warnings,
        List<string> generatedFiles)
    {
        var sprites = manifest.Sprites.Values
            .OrderBy(static sprite => sprite.Name, StringComparer.Ordinal)
            .Select(sprite => CreateGameMakerSpriteInput(context, sprite, warnings))
            .Where(static sprite => sprite is not null)
            .Cast<AtlasSpriteInput>()
            .ToArray();

        return new BrowserGameMakerAtlasManifest(
            BuildAtlasManifest(context, "gamemaker", sprites, generatedFiles));
    }

    private static BrowserAtlasManifest BuildAtlasManifest(
        BrowserAssetBuildContext context,
        string atlasPrefix,
        IReadOnlyList<AtlasSpriteInput> sprites,
        List<string> generatedFiles)
    {
        var pagesByGroup = new Dictionary<string, List<AtlasPageBuilder>>(StringComparer.OrdinalIgnoreCase);
        var atlasPages = new List<BrowserAtlasPageManifest>();
        var spriteEntries = new Dictionary<string, BrowserAtlasSpriteManifest>(StringComparer.OrdinalIgnoreCase);

        foreach (var sprite in sprites)
        {
            if (!pagesByGroup.TryGetValue(sprite.GroupId, out var pages))
            {
                pages = [];
                pagesByGroup[sprite.GroupId] = pages;
            }

            var frameEntries = new List<BrowserAtlasFrameManifest>(sprite.Frames.Count);
            foreach (var frame in sprite.Frames)
            {
                var page = GetOrCreatePage(pages, sprite.GroupId);
                if (!page.TryPlace(frame, out var placedFrame))
                {
                    page = CreatePage(pages, sprite.GroupId);
                    if (!page.TryPlace(frame, out placedFrame))
                    {
                        throw new InvalidOperationException($"Could not pack frame for sprite \"{sprite.SpriteId}\" into atlas group \"{sprite.GroupId}\".");
                    }
                }

                frameEntries.Add(new BrowserAtlasFrameManifest(
                    BuildAtlasId(atlasPrefix, page.Group.Id, page.PageIndex),
                    placedFrame.X,
                    placedFrame.Y,
                    placedFrame.Width,
                    placedFrame.Height,
                    placedFrame.Source.SourceImageIndex,
                    placedFrame.Source.SourceFrameIndex));
            }

            spriteEntries[sprite.SpriteId] = new BrowserAtlasSpriteManifest(
                sprite.OriginX,
                sprite.OriginY,
                sprite.FrameWidth,
                sprite.FrameHeight,
                frameEntries,
                sprite.Mask,
                sprite.SourceHash);
        }

        foreach (var (groupId, pages) in pagesByGroup.OrderBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var page in pages.OrderBy(static page => page.PageIndex))
            {
                var atlasId = BuildAtlasId(atlasPrefix, groupId, page.PageIndex);
                var outputPath = Path.Combine(context.BrowserAtlasesRoot, $"{atlasId}.png");
                AtlasImageWriter.WritePng(outputPath, page);
                generatedFiles.Add(outputPath);
                atlasPages.Add(new BrowserAtlasPageManifest(atlasId, $"Content/Browser/Atlases/{atlasId}.png", page.OutputWidth, page.OutputHeight, groupId));
            }
        }

        return new BrowserAtlasManifest(1, atlasPages, spriteEntries);
    }

    private static AtlasPageBuilder GetOrCreatePage(List<AtlasPageBuilder> pages, string groupId)
        => pages.Count == 0 ? CreatePage(pages, groupId) : pages[^1];

    private static AtlasPageBuilder CreatePage(List<AtlasPageBuilder> pages, string groupId)
    {
        var page = new AtlasPageBuilder(new BrowserAtlasGroup(groupId), pages.Count);
        pages.Add(page);
        return page;
    }

    private static string BuildAtlasId(string atlasPrefix, string groupId, int pageIndex) => $"{atlasPrefix}-{groupId}-{pageIndex}";

    private static AtlasSpriteInput? CreateSingleFrameSpriteInput(
        BrowserAssetBuildContext context,
        string relativePath,
        string groupId,
        List<string> warnings)
    {
        if (!TryResolveStagedContentPath(context, relativePath, out var sourcePath) || !File.Exists(sourcePath))
        {
            warnings.Add($"Bootstrap atlas source was missing: {relativePath}");
            return null;
        }

        var frame = LoadWholeImageFrame(relativePath, sourcePath, groupId, 0, 0);
        return new AtlasSpriteInput(relativePath, groupId, 0, 0, frame.Width, frame.Height, null, [frame], ComputeSourceHash([File.ReadAllBytes(sourcePath)]));
    }

    private static AtlasSpriteInput? CreateGameplaySpriteInput(
        BrowserAssetBuildContext context,
        GameplaySpriteAssetDefinition sprite,
        List<string> warnings)
    {
        var frames = new List<AtlasFrameSource>();
        var sourceBytes = new List<byte[]>();
        for (var sourceImageIndex = 0; sourceImageIndex < sprite.FramePaths.Count; sourceImageIndex += 1)
        {
            var relativePath = ResolveGameplaySpriteContentPath(sprite.FramePaths[sourceImageIndex]);
            if (!TryResolveStagedContentPath(context, relativePath, out var sourcePath) || !File.Exists(sourcePath))
            {
                warnings.Add($"Gameplay atlas source was missing for sprite {sprite.Id}: {relativePath}");
                return null;
            }

            var bytes = File.ReadAllBytes(sourcePath);
            sourceBytes.Add(bytes);
            frames.AddRange(SliceGameplaySpriteFrames(sprite, relativePath, bytes, sourceImageIndex));
        }

        if (frames.Count == 0)
        {
            warnings.Add($"Gameplay atlas sprite produced no frames: {sprite.Id}");
            return null;
        }

        return new AtlasSpriteInput(
            sprite.Id,
            AtlasGroupingPolicy.GetStockGameplayGroup(sprite),
            sprite.OriginX,
            sprite.OriginY,
            sprite.FrameWidth,
            sprite.FrameHeight,
            NormalizeMask(sprite.Mask),
            frames,
            ComputeSourceHash(sourceBytes));
    }

    private static string ResolveGameplaySpriteContentPath(string framePath)
    {
        var normalizedPath = framePath.Replace('\\', '/').TrimStart('/');
        return normalizedPath.StartsWith("Content/", StringComparison.OrdinalIgnoreCase)
            ? normalizedPath
            : $"Content/Gameplay/stock.gg2/{normalizedPath}";
    }

    private static AtlasSpriteInput? CreateGameMakerSpriteInput(
        BrowserAssetBuildContext context,
        GameMakerSpriteAsset sprite,
        List<string> warnings)
    {
        if (sprite.FramePaths.Count == 0)
        {
            return null;
        }

        var frames = new List<AtlasFrameSource>(sprite.FramePaths.Count);
        var sourceBytes = new List<byte[]>(sprite.FramePaths.Count);
        for (var frameIndex = 0; frameIndex < sprite.FramePaths.Count; frameIndex += 1)
        {
            var relativePath = NormalizeContentRelativePath(sprite.FramePaths[frameIndex]);
            if (!TryResolveStagedContentPath(context, relativePath, out var sourcePath) || !File.Exists(sourcePath))
            {
                warnings.Add($"GameMaker atlas source was missing for sprite {sprite.Name}: {relativePath}");
                return null;
            }

            var bytes = File.ReadAllBytes(sourcePath);
            sourceBytes.Add(bytes);
            frames.Add(LoadImageBytesFrame(
                sprite.Name,
                relativePath,
                AtlasGroupingPolicy.GetGameMakerGroup(sprite.Name),
                bytes,
                frameIndex,
                0));
        }

        if (frames.Count == 0)
        {
            warnings.Add($"GameMaker atlas sprite produced no frames: {sprite.Name}");
            return null;
        }

        return new AtlasSpriteInput(
            sprite.Name,
            AtlasGroupingPolicy.GetGameMakerGroup(sprite.Name),
            sprite.OriginX,
            sprite.OriginY,
            null,
            null,
            NormalizeMask(sprite.Mask),
            frames,
            ComputeSourceHash(sourceBytes));
    }

    private static AtlasFrameSource LoadWholeImageFrame(string spriteId, string sourcePath, string groupId, int sourceImageIndex, int sourceFrameIndex)
    {
        using var image = Image.Load<Rgba32>(sourcePath);
        var pixels = new byte[image.Width * image.Height * 4];
        image.CopyPixelDataTo(pixels);
        return new AtlasFrameSource(spriteId, groupId, sourcePath, sourceImageIndex, sourceFrameIndex, image.Width, image.Height, pixels);
    }

    private static AtlasFrameSource LoadImageBytesFrame(
        string spriteId,
        string sourcePath,
        string groupId,
        byte[] imageBytes,
        int sourceImageIndex,
        int sourceFrameIndex)
    {
        using var image = Image.Load<Rgba32>(imageBytes);
        var pixels = new byte[image.Width * image.Height * 4];
        image.CopyPixelDataTo(pixels);
        return new AtlasFrameSource(spriteId, groupId, sourcePath, sourceImageIndex, sourceFrameIndex, image.Width, image.Height, pixels);
    }

    private static List<AtlasFrameSource> SliceGameplaySpriteFrames(
        GameplaySpriteAssetDefinition sprite,
        string relativePath,
        byte[] imageBytes,
        int sourceImageIndex)
    {
        using var image = Image.Load<Rgba32>(imageBytes);
        var frameWidth = sprite.FrameWidth ?? image.Width;
        var frameHeight = sprite.FrameHeight ?? image.Height;
        if (frameWidth <= 0 || frameHeight <= 0 || image.Width % frameWidth != 0 || image.Height % frameHeight != 0)
        {
            throw new InvalidOperationException($"Gameplay sprite asset \"{sprite.Id}\" frame dimensions do not evenly divide source image \"{relativePath}\".");
        }

        var columns = image.Width / frameWidth;
        var rows = image.Height / frameHeight;
        var frames = new List<AtlasFrameSource>(columns * rows);
        var sourceFrameIndex = 0;
        for (var row = 0; row < rows; row += 1)
        {
            for (var column = 0; column < columns; column += 1)
            {
                var framePixels = new byte[frameWidth * frameHeight * 4];
                var destinationIndex = 0;
                for (var y = 0; y < frameHeight; y += 1)
                {
                    var sourceY = (row * frameHeight) + y;
                    for (var x = 0; x < frameWidth; x += 1)
                    {
                        var sourceX = (column * frameWidth) + x;
                        var pixel = image[sourceX, sourceY];
                        framePixels[destinationIndex++] = pixel.R;
                        framePixels[destinationIndex++] = pixel.G;
                        framePixels[destinationIndex++] = pixel.B;
                        framePixels[destinationIndex++] = pixel.A;
                    }
                }

                frames.Add(new AtlasFrameSource(
                    sprite.Id,
                    AtlasGroupingPolicy.GetStockGameplayGroup(sprite),
                    relativePath,
                    sourceImageIndex,
                    sourceFrameIndex,
                    frameWidth,
                    frameHeight,
                    framePixels));
                sourceFrameIndex += 1;
            }
        }

        return frames;
    }

    private static BrowserAtlasMaskManifest? NormalizeMask(GameplaySpriteMaskDefinition? mask)
    {
        return mask is null
            ? null
            : new BrowserAtlasMaskManifest(mask.Separate, mask.Shape ?? string.Empty, mask.BoundsMode ?? string.Empty, mask.Left, mask.Top, mask.Right, mask.Bottom);
    }

    private static BrowserAtlasMaskManifest? NormalizeMask(GameMakerSpriteMask? mask)
    {
        return mask is null
            ? null
            : new BrowserAtlasMaskManifest(mask.Separate, mask.Shape, mask.BoundsMode, mask.Left, mask.Top, mask.Right, mask.Bottom);
    }

    private static IEnumerable<(string EntryPath, string SourcePath)> EnumerateBootstrapBundleEntries(BrowserAssetBuildContext context)
    {
        foreach (var path in BrowserBootstrapAssetCatalog.DefaultBinaryAssetPaths.Concat(BrowserBootstrapAssetCatalog.DefaultTextAssetPaths))
        {
            if (path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (TryResolveStagedContentPath(context, path, out var sourcePath))
            {
                yield return (BrowserAssetBundleLoader.NormalizeRelativePath(path), sourcePath);
            }
        }
    }

    private static IEnumerable<(string EntryPath, string SourcePath)> EnumerateRuntimeBundleEntries(BrowserAssetBuildContext context, GameMakerAssetManifest manifest)
    {
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in manifest.Backgrounds.Values.Select(static background => background.ImagePath)
                     .Concat(manifest.Sounds.Values.Select(static sound => sound.AudioPath))
                     .Concat(EnumerateDirectoryFiles(context, "StockMaps", "*.png", SearchOption.AllDirectories))
                     .Concat(EnumerateDirectoryFiles(context, "StockMaps", "*.json", SearchOption.AllDirectories))
                     .Concat(EnumerateRuntimeContentPaths(context))
                     .Where(static path => !string.IsNullOrWhiteSpace(path)))
        {
            var normalizedPath = NormalizeContentRelativePath(path);
            if (seenPaths.Add(normalizedPath) && TryResolveStagedContentPath(context, normalizedPath, out var sourcePath))
            {
                yield return (normalizedPath, sourcePath);
            }
        }
    }

    private static IEnumerable<string> EnumerateRuntimeContentPaths(BrowserAssetBuildContext context)
    {
        foreach (var definition in OpenGarrisonStockMapCatalog.Definitions)
        {
            yield return NormalizeContentRelativePath(Path.Combine(context.ContentRoot, "Rooms", "Maps", $"{definition.LevelName}.xml"));
            yield return NormalizeContentRelativePath(Path.Combine(context.ContentRoot, "Sprites", "Collision Maps", $"{definition.LevelName}S.images", "image 0.png"));
        }

        foreach (var path in EnumerateDirectoryFiles(context, "BotNav", "*.json", SearchOption.TopDirectoryOnly))
        {
            yield return path;
        }

        foreach (var path in EnumerateDirectoryFiles(context, "BotNavHints", "*.json", SearchOption.TopDirectoryOnly))
        {
            yield return path;
        }

        foreach (var path in EnumerateDirectoryFiles(context, "BotNavScoreRoutes", "*.json", SearchOption.TopDirectoryOnly))
        {
            yield return path;
        }

        foreach (var path in EnumerateDirectoryFiles(context, "BotBrainNav", "*.json", SearchOption.TopDirectoryOnly))
        {
            yield return path;
        }

        foreach (var path in EnumerateDirectoryFiles(context, "BotBrainTapes", "*.json", SearchOption.TopDirectoryOnly))
        {
            yield return path;
        }

        foreach (var path in EnumerateDirectoryFiles(context, "BotBrainProofGraphs", "*.json", SearchOption.TopDirectoryOnly))
        {
            yield return path;
        }

        foreach (var path in EnumerateDirectoryFiles(context, "BotBrainCorridors", "*.json", SearchOption.TopDirectoryOnly))
        {
            yield return path;
        }

    }

    private static IEnumerable<string> EnumerateDirectoryFiles(
        BrowserAssetBuildContext context,
        string directoryName,
        string pattern,
        SearchOption searchOption)
    {
        var directory = Path.Combine(context.ContentRoot, directoryName);
        if (!Directory.Exists(directory))
        {
            yield break;
        }

        foreach (var path in Directory.EnumerateFiles(directory, pattern, searchOption)
                     .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase))
        {
            yield return NormalizeContentRelativePath(path);
        }
    }

    private static IEnumerable<(string EntryPath, string SourcePath)> EnumerateClientPluginBundleEntries(BrowserAssetBuildContext context)
    {
        if (string.IsNullOrWhiteSpace(context.PackagedClientPluginSourceRoot)
            || !Directory.Exists(context.PackagedClientPluginSourceRoot))
        {
            yield break;
        }

        foreach (var sourcePath in Directory.EnumerateFiles(context.PackagedClientPluginSourceRoot, "*", SearchOption.AllDirectories)
                     .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase))
        {
            if (BrowserClientPluginCompatibility.IsBrowserDisabledPackagedClientPluginPath(context.PackagedClientPluginSourceRoot, sourcePath))
            {
                continue;
            }

            var relativePath = Path.GetRelativePath(context.PackagedClientPluginSourceRoot, sourcePath);
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                continue;
            }

            yield return (BrowserAssetBundleLoader.NormalizeRelativePath(Path.Combine("Plugins", "Client", relativePath)), sourcePath);
        }
    }

    private static void PruneLegacyDistributionArtifacts(BrowserAssetBuildContext context)
    {
        DeleteFiles(EnumerateFiles(Path.Combine(context.OutputContentRoot, "Sprites"), "*.png", SearchOption.AllDirectories));
        DeleteFiles(EnumerateFiles(Path.Combine(context.OutputContentRoot, "Gameplay", "stock.gg2", "assets"), "*.png", SearchOption.AllDirectories));
        DeleteFiles(EnumerateFiles(Path.Combine(context.OutputContentRoot, "Gameplay", "stock.gg2", "sprites"), "*.json", SearchOption.TopDirectoryOnly));

        DeleteFileIfExists(Path.Combine(context.OutputContentRoot, "Sprites", "_frame-index.txt"));
        DeleteFileIfExists(Path.Combine(context.OutputContentRoot, "Gameplay", "stock.gg2", "sprites", "_sprite-index.txt"));
        DeleteFileIfExists(Path.Combine(context.OutputContentRoot, "Gameplay", "stock.gg2", "_browser-pack-assets.zip"));
    }

    private static void PruneDeprecatedGameMakerMetadata(BrowserAssetBuildContext context)
    {
        DeleteFiles(EnumerateFiles(Path.Combine(context.OutputContentRoot, "Sprites"), "*.xml", SearchOption.AllDirectories));
        DeleteFiles(EnumerateFiles(Path.Combine(context.OutputContentRoot, "Backgrounds"), "*.xml", SearchOption.TopDirectoryOnly));
        DeleteFiles(EnumerateFiles(Path.Combine(context.OutputContentRoot, "Sounds"), "*.xml", SearchOption.TopDirectoryOnly));
        DeleteFiles(EnumerateFiles(Path.Combine(context.OutputContentRoot, "Fonts"), "*.xml", SearchOption.TopDirectoryOnly));
        DeleteFiles(EnumerateFiles(Path.Combine(context.OutputContentRoot, "Paths"), "*.xml", SearchOption.TopDirectoryOnly));
        DeleteFiles(EnumerateFiles(Path.Combine(context.OutputContentRoot, "Time Lines"), "*.xml", SearchOption.TopDirectoryOnly));
        DeleteFiles(EnumerateFiles(Path.Combine(context.OutputContentRoot, "Builder"), "*.xml", SearchOption.TopDirectoryOnly));

        DeleteFileIfExists(Path.Combine(context.OutputContentRoot, "Constants.xml"));
        DeleteDirectoryIfExists(Path.Combine(context.OutputContentRoot, "Objects"));
        DeleteDirectoryIfExists(Path.Combine(context.OutputContentRoot, "Scripts"));
    }

    private static IEnumerable<string> EnumerateFiles(string directory, string searchPattern, SearchOption searchOption)
    {
        return Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory, searchPattern, searchOption)
            : Array.Empty<string>();
    }

    private static void DeleteFiles(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            DeleteFileIfExists(path);
        }
    }

    private static void DeleteFileIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void DeleteDirectoryIfExists(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static void BuildBundle(string outputPath, IEnumerable<(string EntryPath, string SourcePath)> entries, List<string> generatedFiles)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        using var stream = File.Create(outputPath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false);
        foreach (var (entryPath, sourcePath) in entries.DistinctBy(static entry => entry.EntryPath, StringComparer.OrdinalIgnoreCase).OrderBy(static entry => entry.EntryPath, StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(sourcePath))
            {
                continue;
            }

            var entry = archive.CreateEntry(entryPath, CompressionLevel.SmallestSize);
            using var entryStream = entry.Open();
            using var sourceStream = File.OpenRead(sourcePath);
            sourceStream.CopyTo(entryStream);
        }

        generatedFiles.Add(outputPath);
    }

    private static bool TryResolveStagedContentPath(BrowserAssetBuildContext context, string contentRelativePath, out string sourcePath)
    {
        sourcePath = string.Empty;
        var normalizedPath = BrowserAssetBundleLoader.NormalizeRelativePath(contentRelativePath);
        const string contentPrefix = "Content/";
        if (!normalizedPath.StartsWith(contentPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        sourcePath = Path.Combine(context.OutputContentRoot, normalizedPath[contentPrefix.Length..].Replace('/', Path.DirectorySeparatorChar));
        return true;
    }

    private static string NormalizeContentRelativePath(string path)
    {
        var normalizedPath = path.Replace('\\', '/');
        const string contentMarker = "Content/";
        var contentIndex = normalizedPath.IndexOf(contentMarker, StringComparison.OrdinalIgnoreCase);
        return contentIndex >= 0 ? normalizedPath[contentIndex..] : normalizedPath.TrimStart('/');
    }

    private static string ComputeSourceHash(IEnumerable<byte[]> sourceBytes)
    {
        using var sha256 = SHA256.Create();
        foreach (var bytes in sourceBytes)
        {
            sha256.TransformBlock(bytes, 0, bytes.Length, null, 0);
        }

        sha256.TransformFinalBlock([], 0, 0);
        return Convert.ToHexString(sha256.Hash!);
    }

    private static void WriteJson<T>(string path, T value, List<string> generatedFiles)
        => WriteText(path, JsonSerializer.Serialize(value, JsonOptions), generatedFiles);

    private static void WriteText(string path, string contents, List<string> generatedFiles)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
        generatedFiles.Add(path);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed record AtlasSpriteInput(
        string SpriteId,
        string GroupId,
        int OriginX,
        int OriginY,
        int? FrameWidth,
        int? FrameHeight,
        BrowserAtlasMaskManifest? Mask,
        IReadOnlyList<AtlasFrameSource> Frames,
        string SourceHash);
}
