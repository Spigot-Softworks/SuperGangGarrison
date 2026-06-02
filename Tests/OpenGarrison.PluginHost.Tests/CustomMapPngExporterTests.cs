using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Reflection;
using OpenGarrison.Client;
using OpenGarrison.Core;
using OpenGarrison.Server;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class CustomMapPngExporterTests
{
    [Fact]
    public void UserMapsDirectoryIsDiscoveredAndCustomRotationPersists()
    {
        using var workspace = TempWorkspace.Create();
        var previousMapsDirectory = Environment.GetEnvironmentVariable("OPENGARRISON_MAPS_DIR");
        Environment.SetEnvironmentVariable("OPENGARRISON_MAPS_DIR", workspace.PathFor("Maps"));
        try
        {
            Directory.CreateDirectory(RuntimePaths.MapsDirectory);
            var backgroundPath = workspace.PathFor("background.png");
            var walkmaskPath = workspace.PathFor("walkmask.png");
            var customMapPath = Path.Combine(RuntimePaths.MapsDirectory, "menu_custom.png");
            WriteSolidPng(backgroundPath, 4, 2, new Rgba32(32, 64, 96, 255));
            WriteWalkmaskPng(walkmaskPath);
            CustomMapPngExporter.Export(CreateSpawnOnlyDocument(backgroundPath, walkmaskPath, 6f, 18f), customMapPath);

            SimpleLevelFactory.ClearCachedCatalog();
            var discovered = SimpleLevelFactory.GetAvailableSourceLevels();
            Assert.Contains(discovered, entry => entry.Name == "menu_custom");

            var preferencesPath = workspace.PathFor("OpenGarrison.ini");
            var preferences = new OpenGarrisonPreferencesDocument
            {
                HostSettings = new OpenGarrisonHostSettings
                {
                    StockMapRotation = OpenGarrisonStockMapCatalog.CreateDefaultEntries()
                        .Append(new OpenGarrisonMapRotationEntry
                        {
                            IniKey = "menu_custom",
                            LevelName = "menu_custom",
                            DisplayName = "menu_custom",
                            Mode = GameModeKind.CaptureTheFlag,
                            IsCustomMap = true,
                            DefaultOrder = 21,
                            Order = 3,
                        })
                        .ToList(),
                },
            };
            preferences.Save(preferencesPath);

            SimpleLevelFactory.ClearCachedCatalog();
            var loaded = OpenGarrisonPreferencesDocument.Load(preferencesPath);
            var customEntry = Assert.Single(
                loaded.HostSettings.StockMapRotation,
                entry => entry.LevelName == "menu_custom");
            Assert.True(customEntry.IsCustomMap);
            Assert.Equal(3, customEntry.Order);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENGARRISON_MAPS_DIR", previousMapsDirectory);
            SimpleLevelFactory.ClearCachedCatalog();
        }
    }

    [Fact]
    public void LegacyCustomMapPngAutoConvertsToPackageOnDiscovery()
    {
        using var workspace = TempWorkspace.Create();
        var previousMapsDirectory = Environment.GetEnvironmentVariable("OPENGARRISON_MAPS_DIR");
        Environment.SetEnvironmentVariable("OPENGARRISON_MAPS_DIR", workspace.PathFor("Maps"));
        try
        {
            Directory.CreateDirectory(RuntimePaths.MapsDirectory);
            var backgroundPath = workspace.PathFor("background.png");
            var walkmaskPath = workspace.PathFor("walkmask.png");
            var legacyMapPath = Path.Combine(RuntimePaths.MapsDirectory, "legacy_auto.png");
            WriteSolidPng(backgroundPath, 4, 2, new Rgba32(32, 64, 96, 255));
            WriteWalkmaskPng(walkmaskPath);
            CustomMapPngExporter.Export(CreateSpawnOnlyDocument(backgroundPath, walkmaskPath, 6f, 18f), legacyMapPath);

            SimpleLevelFactory.ClearCachedCatalog();
            var discovered = SimpleLevelFactory.GetAvailableSourceLevels();
            var entry = Assert.Single(discovered, entry => entry.Name == "legacy_auto");
            var manifestPath = Path.Combine(RuntimePaths.MapsDirectory, "legacy_auto", "legacy_auto.json");

            Assert.Equal(CustomMapSourceKind.Package, entry.SourceKind);
            Assert.Equal(manifestPath, entry.RoomSourcePath);
            Assert.True(File.Exists(legacyMapPath));
            Assert.True(File.Exists(manifestPath));
            Assert.NotNull(CustomMapPackageImporter.Import(manifestPath));

            var level = SimpleLevelFactory.CreateImportedLevel("legacy_auto");
            Assert.NotNull(level);
            Assert.NotEmpty(level.Solids);
            Assert.Single(level.RedSpawns);
            Assert.Single(level.BlueSpawns);

            Assert.True(CustomMapDescriptorResolver.TryResolve("legacy_auto", out var descriptor));
            Assert.Equal(CustomMapSourceKind.Package, descriptor.SourceKind);
            Assert.StartsWith("sha256:", descriptor.ContentHash, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENGARRISON_MAPS_DIR", previousMapsDirectory);
            SimpleLevelFactory.ClearCachedCatalog();
        }
    }

    [Fact]
    public void LegacyCustomMapPngFallsBackWhenPackageDirectoryBlocksAutoConversion()
    {
        using var workspace = TempWorkspace.Create();
        var previousMapsDirectory = Environment.GetEnvironmentVariable("OPENGARRISON_MAPS_DIR");
        Environment.SetEnvironmentVariable("OPENGARRISON_MAPS_DIR", workspace.PathFor("Maps"));
        try
        {
            Directory.CreateDirectory(RuntimePaths.MapsDirectory);
            var backgroundPath = workspace.PathFor("background.png");
            var walkmaskPath = workspace.PathFor("walkmask.png");
            var legacyMapPath = Path.Combine(RuntimePaths.MapsDirectory, "legacy_blocked.png");
            WriteSolidPng(backgroundPath, 4, 2, new Rgba32(32, 64, 96, 255));
            WriteWalkmaskPng(walkmaskPath);
            CustomMapPngExporter.Export(CreateSpawnOnlyDocument(backgroundPath, walkmaskPath, 6f, 18f), legacyMapPath);

            var packageDirectory = Path.Combine(RuntimePaths.MapsDirectory, "legacy_blocked");
            Directory.CreateDirectory(packageDirectory);
            File.WriteAllText(Path.Combine(packageDirectory, "notes.txt"), "reserved");

            SimpleLevelFactory.ClearCachedCatalog();
            var discovered = SimpleLevelFactory.GetAvailableSourceLevels();
            var entry = Assert.Single(discovered, entry => entry.Name == "legacy_blocked");

            Assert.Equal(CustomMapSourceKind.LegacyPng, entry.SourceKind);
            Assert.Equal(legacyMapPath, entry.RoomSourcePath);
            Assert.False(File.Exists(Path.Combine(packageDirectory, "legacy_blocked.json")));
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENGARRISON_MAPS_DIR", previousMapsDirectory);
            SimpleLevelFactory.ClearCachedCatalog();
        }
    }

    [Fact]
    public void ExportWritesImportableCustomMapPng()
    {
        using var workspace = TempWorkspace.Create();
        var backgroundPath = workspace.PathFor("background.png");
        var walkmaskPath = workspace.PathFor("walkmask.png");
        var outputPath = workspace.PathFor("exported.png");
        WriteSolidPng(backgroundPath, 4, 2, new Rgba32(32, 64, 96, 255));
        WriteWalkmaskPng(walkmaskPath);

        var document = new CustomMapBuilderDocument(
            Name: "roundtrip",
            BackgroundImagePath: backgroundPath,
            WalkmaskImagePath: walkmaskPath,
            Scale: 6f,
            Metadata: new Dictionary<string, string>
            {
                ["background"] = "ffffff",
            },
            Entities:
            [
                CustomMapBuilderEntity.Create("redspawn", 6f, 6f),
                CustomMapBuilderEntity.Create("bluespawn", 18f, 6f),
                CustomMapBuilderEntity.Create("redintel", 8f, 6f),
                CustomMapBuilderEntity.Create("blueintel", 16f, 6f),
                CustomMapBuilderEntity.Create("redteamgate", 12f, 0f, xScale: 2f, yScale: 1f),
            ],
            Resources: new Dictionary<string, CustomMapBuilderResource>(),
            ParallaxLayers: []);

        CustomMapPngExporter.Export(document, outputPath);
        var imported = CustomMapPngImporter.Import(outputPath);

        Assert.NotNull(imported);
        Assert.Equal("exported", imported.Room.Name);
        Assert.Equal(24f, imported.Room.Bounds.Width);
        Assert.Equal(12f, imported.Room.Bounds.Height);
        Assert.Single(imported.Room.RedSpawns);
        Assert.Single(imported.Room.BlueSpawns);
        Assert.Equal(2, imported.Room.IntelBases.Count);
        Assert.Equal(2, imported.Solids.Count);
        Assert.Contains(imported.Solids, solid => solid.X == 0f && solid.Y == 0f && solid.Width == 12f && solid.Height == 6f);
        Assert.Contains(imported.Solids, solid => solid.X == 18f && solid.Y == 6f && solid.Width == 6f && solid.Height == 6f);

        var gate = Assert.Single(imported.Room.RoomObjects, roomObject => roomObject.Type == RoomObjectType.TeamGate);
        Assert.Equal(PlayerTeam.Red, gate.Team);
        Assert.Equal(12f, gate.Width);
        Assert.Equal(60f, gate.Height);
        Assert.Equal("redteamgate", gate.SourceName);
    }

    [Fact]
    public void ExportReplacesExistingEmbeddedLevelData()
    {
        using var workspace = TempWorkspace.Create();
        var backgroundPath = workspace.PathFor("background.png");
        var walkmaskPath = workspace.PathFor("walkmask.png");
        var outputPath = workspace.PathFor("exported.png");
        WriteSolidPng(backgroundPath, 4, 2, new Rgba32(16, 32, 48, 255));
        WriteWalkmaskPng(walkmaskPath);

        var firstDocument = CreateSpawnOnlyDocument(backgroundPath, walkmaskPath, redSpawnX: 6f, blueSpawnX: 18f);
        CustomMapPngExporter.Export(firstDocument, outputPath);

        var secondDocument = CreateSpawnOnlyDocument(outputPath, walkmaskPath, redSpawnX: 12f, blueSpawnX: 18f);
        CustomMapPngExporter.Export(secondDocument, outputPath);

        var imported = CustomMapPngImporter.Import(outputPath);

        Assert.NotNull(imported);
        var redSpawn = Assert.Single(imported.Room.RedSpawns);
        Assert.Equal(12f, redSpawn.X);
    }

    [Fact]
    public void BuilderImporterRehydratesEditableMapFromExportedPng()
    {
        using var workspace = TempWorkspace.Create();
        var backgroundPath = workspace.PathFor("background.png");
        var walkmaskPath = workspace.PathFor("walkmask.png");
        var outputPath = workspace.PathFor("exported.png");
        var reexportedPath = workspace.PathFor("reexported.png");
        WriteSolidPng(backgroundPath, 4, 2, new Rgba32(20, 40, 60, 255));
        WriteWalkmaskPng(walkmaskPath);

        var document = new CustomMapBuilderDocument(
            Name: "editable",
            BackgroundImagePath: backgroundPath,
            WalkmaskImagePath: walkmaskPath,
            Scale: 6f,
            Metadata: new Dictionary<string, string>
            {
                ["background"] = "ffffff",
                ["layer0xfactor"] = "0.5",
                ["layer0yfactor"] = "0.75",
            },
            Entities:
            [
                CustomMapBuilderEntity.Create("redspawn", 6f, 6f),
                CustomMapBuilderEntity.Create("bluespawn", 18f, 6f),
                CustomMapBuilderEntity.Create("redintel", 8f, 6f),
            ],
            Resources: new Dictionary<string, CustomMapBuilderResource>(),
            ParallaxLayers:
            [
                new CustomMapBuilderParallaxLayer(0, string.Empty, 0.5f, 0.75f),
            ]);

        CustomMapPngExporter.Export(document, outputPath);
        var editable = CustomMapBuilderPngImporter.Import(outputPath);

        Assert.NotNull(editable);
        Assert.Equal(outputPath, editable.BackgroundImagePath);
        Assert.Empty(editable.WalkmaskImagePath);
        Assert.False(string.IsNullOrWhiteSpace(editable.EmbeddedWalkmaskSection));
        Assert.Contains(editable.Entities, entity => entity.Type == "redspawn" && entity.X == 6f);
        Assert.Contains(editable.Entities, entity => entity.Type == "bluespawn" && entity.X == 18f);
        Assert.Contains(editable.Entities, entity => entity.Type == "redintel" && entity.X == 8f);
        var layer = Assert.Single(editable.ParallaxLayers);
        Assert.Equal(0.5f, layer.XFactor);
        Assert.Equal(0.75f, layer.YFactor);

        CustomMapPngExporter.Export(editable, reexportedPath);
        var runtimeImport = CustomMapPngImporter.Import(reexportedPath);
        Assert.NotNull(runtimeImport);
        Assert.Single(runtimeImport.Room.RedSpawns);
        Assert.Single(runtimeImport.Room.BlueSpawns);
    }

    [Fact]
    public void BuilderImporterRehydratesEmbeddedResourcesFromExportedMetadata()
    {
        using var workspace = TempWorkspace.Create();
        var backgroundPath = workspace.PathFor("background.png");
        var walkmaskPath = workspace.PathFor("walkmask.png");
        var layerPath = workspace.PathFor("layer.png");
        var foregroundPath = workspace.PathFor("foreground.gif");
        var outputPath = workspace.PathFor("exported.png");
        WriteSolidPng(backgroundPath, 4, 2, new Rgba32(20, 40, 60, 255));
        WriteWalkmaskPng(walkmaskPath);
        WriteSolidPng(layerPath, 2, 2, new Rgba32(100, 120, 140, 255));
        File.WriteAllBytes(foregroundPath, "GIF89a"u8.ToArray());

        var document = new CustomMapBuilderDocument(
            Name: "resources",
            BackgroundImagePath: backgroundPath,
            WalkmaskImagePath: walkmaskPath,
            Scale: 6f,
            Metadata: new Dictionary<string, string>(),
            Entities:
            [
                CustomMapBuilderEntity.Create("redspawn", 6f, 6f),
                CustomMapBuilderEntity.Create("bluespawn", 18f, 6f),
            ],
            Resources: new Dictionary<string, CustomMapBuilderResource>
            {
                ["clouds"] = CustomMapBuilderResourceCodec.FromFile("clouds", layerPath, CustomMapBuilderResourceKind.ParallaxLayer),
                ["front"] = CustomMapBuilderResourceCodec.FromFile("front", foregroundPath, CustomMapBuilderResourceKind.Foreground),
            },
            ParallaxLayers:
            [
                new CustomMapBuilderParallaxLayer(0, "clouds", 0.5f, 0.75f),
            ]);

        CustomMapPngExporter.Export(document, outputPath);
        var editable = CustomMapBuilderPngImporter.Import(outputPath);

        Assert.NotNull(editable);
        Assert.True(editable.Resources.ContainsKey("bg_layer0"));
        Assert.True(editable.Resources.ContainsKey("bg_foreground"));
        Assert.True(editable.Resources.ContainsKey("clouds"));
        Assert.Equal(CustomMapBuilderResourceKind.ParallaxLayer, editable.Resources["bg_layer0"].Kind);
        Assert.Equal(CustomMapBuilderResourceKind.Foreground, editable.Resources["bg_foreground"].Kind);
        Assert.Equal("bg_layer0", Assert.Single(editable.ParallaxLayers).ResourceName);
        Assert.Equal(File.ReadAllBytes(layerPath), editable.Resources["bg_layer0"].EmbeddedBytes);
        Assert.Equal(File.ReadAllBytes(foregroundPath), editable.Resources["bg_foreground"].EmbeddedBytes);

        var runtimeImport = CustomMapPngImporter.Import(outputPath);
        Assert.NotNull(runtimeImport);
        var runtimeLayer = Assert.Single(runtimeImport.Room.CustomMapVisuals.ParallaxLayers);
        Assert.Equal(0, runtimeLayer.Index);
        Assert.Equal(0.5f, runtimeLayer.XFactor);
        Assert.Equal(0.75f, runtimeLayer.YFactor);
        Assert.Equal(File.ReadAllBytes(layerPath), runtimeLayer.Resource.Bytes);
        Assert.NotNull(runtimeImport.Room.CustomMapVisuals.Foreground);
        Assert.Equal(File.ReadAllBytes(foregroundPath), runtimeImport.Room.CustomMapVisuals.Foreground.Bytes);
    }

    [Fact]
    public void PackageFolderIsDiscoveredAndImportsRuntimeMap()
    {
        using var workspace = TempWorkspace.Create();
        var previousMapsDirectory = Environment.GetEnvironmentVariable("OPENGARRISON_MAPS_DIR");
        Environment.SetEnvironmentVariable("OPENGARRISON_MAPS_DIR", workspace.PathFor("Maps"));
        try
        {
            Directory.CreateDirectory(RuntimePaths.MapsDirectory);
            var packageDirectory = Path.Combine(RuntimePaths.MapsDirectory, "package_custom");
            Directory.CreateDirectory(packageDirectory);
            var backgroundPath = workspace.PathFor("background.png");
            var walkmaskPath = workspace.PathFor("walkmask.png");
            var layerPath = workspace.PathFor("clouds.png");
            var foregroundPath = workspace.PathFor("foreground.png");
            WriteSolidPng(backgroundPath, 4, 2, new Rgba32(32, 64, 96, 255));
            WriteWalkmaskPng(walkmaskPath);
            WriteSolidPng(layerPath, 2, 2, new Rgba32(100, 120, 140, 255));
            WriteSolidPng(foregroundPath, 2, 2, new Rgba32(200, 120, 80, 255));

            var document = new CustomMapBuilderDocument(
                Name: "package_custom",
                BackgroundImagePath: backgroundPath,
                WalkmaskImagePath: walkmaskPath,
                Scale: 6f,
                Metadata: new Dictionary<string, string>
                {
                    ["background"] = "ffffff",
                    ["void"] = "000000",
                },
                Entities:
                [
                    CustomMapBuilderEntity.Create("redspawn", 6f, 6f),
                    CustomMapBuilderEntity.Create("bluespawn", 18f, 6f),
                    CustomMapBuilderEntity.Create("redintel", 8f, 6f),
                    CustomMapBuilderEntity.Create("blueintel", 16f, 6f),
                ],
                Resources: new Dictionary<string, CustomMapBuilderResource>
                {
                    ["clouds"] = CustomMapBuilderResourceCodec.FromFile("clouds", layerPath, CustomMapBuilderResourceKind.ParallaxLayer),
                    ["front"] = CustomMapBuilderResourceCodec.FromFile("front", foregroundPath, CustomMapBuilderResourceKind.Foreground),
                },
                ParallaxLayers:
                [
                    new CustomMapBuilderParallaxLayer(0, "clouds", 0.5f, 0.75f),
                ]);
            CustomMapPackageExporter.Export(document, Path.Combine(packageDirectory, "package_custom.json"));

            SimpleLevelFactory.ClearCachedCatalog();
            var discovered = SimpleLevelFactory.GetAvailableSourceLevels();
            var entry = Assert.Single(discovered, entry => entry.Name == "package_custom");
            Assert.True(entry.IsCustomMap);
            Assert.Equal(CustomMapSourceKind.Package, entry.SourceKind);

            var level = SimpleLevelFactory.CreateImportedLevel("package_custom");

            Assert.NotNull(level);
            Assert.Equal("package_custom", level.Name);
            Assert.Equal(24f, level.Bounds.Width);
            Assert.Equal(12f, level.Bounds.Height);
            Assert.Single(level.RedSpawns);
            Assert.Single(level.BlueSpawns);
            Assert.Equal(2, level.IntelBases.Count);
            Assert.Single(level.CustomMapVisuals.ParallaxLayers);
            Assert.NotNull(level.CustomMapVisuals.Foreground);
            Assert.EndsWith(Path.Combine("package_custom", "background.png"), level.BackgroundAssetName);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENGARRISON_MAPS_DIR", previousMapsDirectory);
            SimpleLevelFactory.ClearCachedCatalog();
        }
    }

    [Fact]
    public void StockHarvestUsesPackageTemplate()
    {
        try
        {
            SimpleLevelFactory.ClearCachedCatalog();
            var entry = Assert.Single(
                SimpleLevelFactory.GetAvailableSourceLevels(),
                candidate => candidate.Name == "Harvest");

            Assert.False(entry.IsCustomMap);
            Assert.Equal(CustomMapSourceKind.Package, entry.SourceKind);
            Assert.EndsWith(
                Path.Combine("StockMaps", "Harvest", "Harvest.json"),
                entry.RoomSourcePath,
                StringComparison.OrdinalIgnoreCase);

            var level = SimpleLevelFactory.CreateImportedLevel("Harvest");

            Assert.NotNull(level);
            Assert.Equal("Harvest", level.Name);
            Assert.Equal(GameModeKind.KingOfTheHill, level.Mode);
            Assert.NotEmpty(level.Solids);
            Assert.NotEmpty(level.RedSpawns);
            Assert.NotEmpty(level.BlueSpawns);
            Assert.Contains(level.RoomObjects, roomObject => roomObject.IsSingleKothControlPoint());
            var layers = level.CustomMapVisuals.ParallaxLayers.ToDictionary(static layer => layer.Index);
            Assert.Equal(10f, layers[0].XFactor);
            Assert.Equal(6f, layers[4].XFactor);
            Assert.Equal(5f, layers[5].XFactor);
            Assert.Equal(4f, layers[6].XFactor);
        }
        finally
        {
            SimpleLevelFactory.ClearCachedCatalog();
        }
    }

    [Fact]
    public void StockConflictUsesPackageTemplateWithSeparateVisualLayers()
    {
        try
        {
            SimpleLevelFactory.ClearCachedCatalog();
            var entry = Assert.Single(
                SimpleLevelFactory.GetAvailableSourceLevels(),
                candidate => candidate.Name == "Conflict");

            Assert.False(entry.IsCustomMap);
            Assert.Equal(CustomMapSourceKind.Package, entry.SourceKind);
            Assert.EndsWith(
                Path.Combine("StockMaps", "Conflict", "Conflict.json"),
                entry.RoomSourcePath,
                StringComparison.OrdinalIgnoreCase);
            Assert.Null(entry.CollisionMaskSourcePath);

            var level = SimpleLevelFactory.CreateImportedLevel("Conflict");

            Assert.NotNull(level);
            Assert.Equal("Conflict", level.Name);
            Assert.Equal(GameModeKind.CaptureTheFlag, level.Mode);
            Assert.Equal(4458f, level.Bounds.Width);
            Assert.Equal(1614f, level.Bounds.Height);
            Assert.NotEmpty(level.Solids);
            Assert.NotEmpty(level.RedSpawns);
            Assert.NotEmpty(level.BlueSpawns);
            Assert.Equal(2, level.IntelBases.Count);
            Assert.EndsWith(
                Path.Combine("StockMaps", "Conflict", "background.png"),
                level.BackgroundAssetName,
                StringComparison.OrdinalIgnoreCase);
            Assert.Equal(2f, level.CustomMapVisuals.ImageScale);
            Assert.NotNull(level.CustomMapVisuals.Foreground);
            Assert.Equal("bg_foreground", level.CustomMapVisuals.Foreground.Name);
            Assert.Equal(0f, level.CustomMapVisuals.ForegroundOffsetX);
            Assert.Equal(0f, level.CustomMapVisuals.ForegroundOffsetY);
            Assert.Equal(File.ReadAllBytes(FindRepoFile("Core", "Content", "StockMaps", "Conflict", "bg_foreground.png")), level.CustomMapVisuals.Foreground.Bytes);
            Assert.NotEqual(File.ReadAllBytes(FindRepoFile("ctf_conflict.png")), level.CustomMapVisuals.Foreground.Bytes);

            var layers = level.CustomMapVisuals.ParallaxLayers.ToDictionary(static layer => layer.Index);
            Assert.Equal([0, 1, 2, 3, 4, 5], layers.Keys.OrderBy(static key => key).ToArray());
            Assert.Equal(10f, layers[0].XFactor);
            Assert.Equal(9f, layers[1].XFactor);
            Assert.Equal(8f, layers[2].XFactor);
            Assert.Equal(7f, layers[3].XFactor);
            Assert.Equal(6f, layers[4].XFactor);
            Assert.Equal(5f, layers[5].XFactor);

            using var sourceImage = Image.Load<Rgba32>(FindRepoFile("Core", "Content", "StockMaps", "Conflict", "background.png"));
            using var foregroundImage = Image.Load<Rgba32>(level.CustomMapVisuals.Foreground.Bytes);
            Assert.Equal(sourceImage.Width, foregroundImage.Width);
            Assert.Equal(sourceImage.Height, foregroundImage.Height);
        }
        finally
        {
            SimpleLevelFactory.ClearCachedCatalog();
        }
    }

    [Fact]
    public void StockMapPackageTemplateInMapsFolderDoesNotCreateCustomDuplicate()
    {
        using var workspace = TempWorkspace.Create();
        var previousMapsDirectory = Environment.GetEnvironmentVariable("OPENGARRISON_MAPS_DIR");
        Environment.SetEnvironmentVariable("OPENGARRISON_MAPS_DIR", workspace.PathFor("Maps"));
        try
        {
            var sourceHarvestDirectory = Path.GetDirectoryName(FindRepoFile("Core", "Content", "StockMaps", "Harvest", "Harvest.json"));
            Assert.False(string.IsNullOrWhiteSpace(sourceHarvestDirectory));
            CopyDirectoryContents(sourceHarvestDirectory!, Path.Combine(RuntimePaths.MapsDirectory, "Harvest"));

            SimpleLevelFactory.ClearCachedCatalog();
            var harvestEntries = SimpleLevelFactory.GetAvailableSourceLevels()
                .Where(entry => entry.Name == "Harvest")
                .ToArray();

            var harvestEntry = Assert.Single(harvestEntries);
            Assert.False(harvestEntry.IsCustomMap);
            Assert.Equal(CustomMapSourceKind.Package, harvestEntry.SourceKind);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENGARRISON_MAPS_DIR", previousMapsDirectory);
            SimpleLevelFactory.ClearCachedCatalog();
        }
    }

    [Fact]
    public void PackageDirectoryPublisherConvertsLegacyPngsAndCopiesStockTemplates()
    {
        using var workspace = TempWorkspace.Create();
        var sourceMapsDirectory = workspace.PathFor("SourceMaps");
        var sourceStockMapsDirectory = workspace.PathFor("SourceStockMaps");
        var destinationMapsDirectory = workspace.PathFor("PublishedMaps");
        Directory.CreateDirectory(sourceMapsDirectory);
        Directory.CreateDirectory(sourceStockMapsDirectory);

        var backgroundPath = workspace.PathFor("background.png");
        var walkmaskPath = workspace.PathFor("walkmask.png");
        WriteSolidPng(backgroundPath, 4, 2, new Rgba32(32, 64, 96, 255));
        WriteWalkmaskPng(walkmaskPath);

        CustomMapPngExporter.Export(
            CreateSpawnOnlyDocument(backgroundPath, walkmaskPath, 6f, 18f) with
            {
                Name = "legacy_template",
            },
            Path.Combine(sourceMapsDirectory, "legacy_template.png"));

        var stockHarvestDirectory = Path.Combine(sourceStockMapsDirectory, "Harvest");
        Directory.CreateDirectory(stockHarvestDirectory);
        CustomMapPackageExporter.Export(
            CreateSpawnOnlyDocument(backgroundPath, walkmaskPath, 6f, 18f) with
            {
                Name = "Harvest",
            },
            Path.Combine(stockHarvestDirectory, "Harvest.json"));

        var result = CustomMapPackageDirectoryPublisher.Publish(
            sourceMapsDirectory,
            sourceStockMapsDirectory,
            destinationMapsDirectory);

        Assert.Equal(1, result.CopiedTopLevelLegacyPngCount);
        Assert.Equal(1, result.ConvertedLegacyPngCount);
        Assert.Equal(0, result.KeptLegacyPngCount);
        Assert.Equal(0, result.DroppedLegacyPngCount);
        Assert.Equal(1, result.CopiedStockPackageCount);
        Assert.False(File.Exists(Path.Combine(destinationMapsDirectory, "legacy_template.png")));
        Assert.NotNull(CustomMapPackageImporter.Import(Path.Combine(destinationMapsDirectory, "legacy_template", "legacy_template.json")));
        Assert.NotNull(CustomMapPackageImporter.Import(Path.Combine(destinationMapsDirectory, "Harvest", "Harvest.json")));
    }

    [Fact]
    public void PackageImporterReadsBrowserCatalogPackageFiles()
    {
        using var workspace = TempWorkspace.Create();
        var packageDirectory = workspace.PathFor("browser_package");
        Directory.CreateDirectory(packageDirectory);
        var backgroundPath = workspace.PathFor("background.png");
        var walkmaskPath = workspace.PathFor("walkmask.png");
        WriteSolidPng(backgroundPath, 4, 2, new Rgba32(32, 64, 96, 255));
        WriteWalkmaskPng(walkmaskPath);

        var manifestPath = Path.Combine(packageDirectory, "browser_package.json");
        CustomMapPackageExporter.Export(CreateSpawnOnlyDocument(backgroundPath, walkmaskPath, 6f, 18f) with
        {
            Name = "browser_package",
        }, manifestPath);

        var assets = CustomMapPackageImporter.GetPackageContentFiles(manifestPath)
            .Select(file => new KeyValuePair<string, byte[]>(
                $"Content/StockMaps/browser_package/{file.RelativePath}",
                File.ReadAllBytes(file.FullPath)))
            .ToArray();
        using var browserCatalog = BrowserCatalogScope.Create("Content", assets);
        var browserManifestPath = ContentRoot.GetPath("StockMaps", "browser_package", "browser_package.json");

        var imported = CustomMapPackageImporter.Import(browserManifestPath);

        Assert.NotNull(imported);
        Assert.Equal("browser_package", imported.Room.Name);
        Assert.Equal(24f, imported.Room.Bounds.Width);
        Assert.Equal(12f, imported.Room.Bounds.Height);
        Assert.NotEmpty(imported.Solids);
        Assert.Single(imported.Room.RedSpawns);
        Assert.Single(imported.Room.BlueSpawns);
    }

    [Fact]
    public void PackageImporterRejectsUnsafeOrMissingImagePaths()
    {
        using var workspace = TempWorkspace.Create();
        var packageDirectory = workspace.PathFor("unsafe_map");
        Directory.CreateDirectory(packageDirectory);
        WriteWalkmaskPng(Path.Combine(packageDirectory, "walkmask.png"));

        var unsafeManifestPath = Path.Combine(packageDirectory, "unsafe_map.json");
        File.WriteAllText(
            unsafeManifestPath,
            """
            {
              "formatVersion": 1,
              "name": "unsafe_map",
              "scale": 6,
              "backgroundImage": "../background.png",
              "walkmaskImage": "walkmask.png",
              "entities": [
                { "type": "redspawn", "x": 6, "y": 6 },
                { "type": "bluespawn", "x": 18, "y": 6 }
              ]
            }
            """);
        Assert.Null(CustomMapPackageImporter.Import(unsafeManifestPath));

        var missingManifestPath = Path.Combine(packageDirectory, "missing_map.json");
        File.WriteAllText(
            missingManifestPath,
            """
            {
              "formatVersion": 1,
              "name": "missing_map",
              "scale": 6,
              "backgroundImage": "missing.png",
              "walkmaskImage": "walkmask.png",
              "entities": [
                { "type": "redspawn", "x": 6, "y": 6 },
                { "type": "bluespawn", "x": 18, "y": 6 }
              ]
            }
            """);
        Assert.Null(CustomMapPackageImporter.Import(missingManifestPath));
    }

    [Fact]
    public void PackageHashChangesWhenManifestOrReferencedPngChanges()
    {
        using var workspace = TempWorkspace.Create();
        var packageDirectory = workspace.PathFor("hash_map");
        Directory.CreateDirectory(packageDirectory);
        var backgroundPath = workspace.PathFor("background.png");
        var walkmaskPath = workspace.PathFor("walkmask.png");
        WriteSolidPng(backgroundPath, 4, 2, new Rgba32(32, 64, 96, 255));
        WriteWalkmaskPng(walkmaskPath);

        var manifestPath = Path.Combine(packageDirectory, "hash_map.json");
        CustomMapPackageExporter.Export(CreateSpawnOnlyDocument(backgroundPath, walkmaskPath, 6f, 18f) with
        {
            Name = "hash_map",
        }, manifestPath);

        var firstHash = CustomMapHashService.ComputePackageSha256(manifestPath);
        WriteSolidPng(Path.Combine(packageDirectory, "background.png"), 4, 2, new Rgba32(80, 40, 20, 255));
        var imageChangedHash = CustomMapHashService.ComputePackageSha256(manifestPath);
        File.WriteAllText(
            manifestPath,
            File.ReadAllText(manifestPath).Replace("\"background\": \"ffffff\"", "\"background\": \"010203\""));
        var manifestChangedHash = CustomMapHashService.ComputePackageSha256(manifestPath);

        Assert.NotEmpty(firstHash);
        Assert.NotEqual(firstHash, imageChangedHash);
        Assert.NotEqual(imageChangedHash, manifestChangedHash);
    }

    [Fact]
    public void CustomMapSyncDownloadsPackageManifestAndImagesAtomically()
    {
        using var workspace = TempWorkspace.Create();
        var previousMapsDirectory = Environment.GetEnvironmentVariable("OPENGARRISON_MAPS_DIR");
        Environment.SetEnvironmentVariable("OPENGARRISON_MAPS_DIR", workspace.PathFor("ClientMaps"));
        try
        {
            Directory.CreateDirectory(RuntimePaths.MapsDirectory);
            var serverPackageDirectory = workspace.PathFor(Path.Combine("ServerMaps", "sync_map"));
            Directory.CreateDirectory(serverPackageDirectory);
            var backgroundPath = workspace.PathFor("server-background.png");
            var walkmaskPath = workspace.PathFor("server-walkmask.png");
            WriteSolidPng(backgroundPath, 4, 2, new Rgba32(20, 70, 120, 255));
            WriteWalkmaskPng(walkmaskPath);
            var sourceManifestPath = Path.Combine(serverPackageDirectory, "sync_map.json");
            CustomMapPackageExporter.Export(CreateSpawnOnlyDocument(backgroundPath, walkmaskPath, 6f, 18f) with
            {
                Name = "sync_map",
            }, sourceManifestPath);

            File.WriteAllBytes(CustomMapLocatorStore.GetMapPath("sync_map"), "not the package"u8.ToArray());
            var packageHash = CustomMapHashService.ComputePackageSha256(sourceManifestPath);
            var manifestUrl = "https://example.invalid/maps/sync_map/sync_map.json";
            using var httpClient = CreatePackageHttpClient(sourceManifestPath, manifestUrl);

            var available = CustomMapSyncService.EnsureMapAvailable(
                "sync_map",
                isCustomMap: true,
                manifestUrl,
                $"sha256:{packageHash}",
                httpClient,
                out var error);

            Assert.True(available, error);
            Assert.False(File.Exists(CustomMapLocatorStore.GetMapPath("sync_map")));
            var finalManifestPath = CustomMapLocatorStore.GetPackageManifestPath("sync_map");
            Assert.True(File.Exists(finalManifestPath));
            Assert.NotNull(CustomMapPackageImporter.Import(finalManifestPath));
            Assert.Empty(Directory.EnumerateDirectories(RuntimePaths.MapsDirectory, "*.download"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENGARRISON_MAPS_DIR", previousMapsDirectory);
            SimpleLevelFactory.ClearCachedCatalog();
        }
    }

    [Fact]
    public void CustomMapSyncResolvesServerRelativePackageUrl()
    {
        using var workspace = TempWorkspace.Create();
        var previousMapsDirectory = Environment.GetEnvironmentVariable("OPENGARRISON_MAPS_DIR");
        Environment.SetEnvironmentVariable("OPENGARRISON_MAPS_DIR", workspace.PathFor("ClientMaps"));
        try
        {
            Directory.CreateDirectory(RuntimePaths.MapsDirectory);
            var serverPackageDirectory = workspace.PathFor(Path.Combine("ServerMaps", "relative_sync"));
            Directory.CreateDirectory(serverPackageDirectory);
            var backgroundPath = workspace.PathFor("server-background.png");
            var walkmaskPath = workspace.PathFor("server-walkmask.png");
            WriteSolidPng(backgroundPath, 4, 2, new Rgba32(20, 70, 120, 255));
            WriteWalkmaskPng(walkmaskPath);
            var sourceManifestPath = Path.Combine(serverPackageDirectory, "relative_sync.json");
            CustomMapPackageExporter.Export(CreateSpawnOnlyDocument(backgroundPath, walkmaskPath, 6f, 18f) with
            {
                Name = "relative_sync",
            }, sourceManifestPath);

            var relativeManifestUrl = "/opengarrison/maps/relative_sync/relative_sync.json";
            var absoluteManifestUrl = $"http://example.invalid{relativeManifestUrl}";
            var packageHash = CustomMapHashService.ComputePackageSha256(sourceManifestPath);
            using var httpClient = CreatePackageHttpClient(sourceManifestPath, absoluteManifestUrl);

            var available = CustomMapSyncService.EnsureMapAvailable(
                "relative_sync",
                isCustomMap: true,
                relativeManifestUrl,
                $"sha256:{packageHash}",
                new Uri("http://example.invalid:8190/"),
                httpClient,
                out var error);

            Assert.True(available, error);
            var finalManifestPath = CustomMapLocatorStore.GetPackageManifestPath("relative_sync");
            Assert.True(File.Exists(finalManifestPath));
            Assert.NotNull(CustomMapPackageImporter.Import(finalManifestPath));
            Assert.Equal(
                "http://example.invalid:8190/opengarrison/maps/relative_sync/relative_sync.json",
                CustomMapLocatorStore.TryReadMapUrl("relative_sync"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENGARRISON_MAPS_DIR", previousMapsDirectory);
            SimpleLevelFactory.ClearCachedCatalog();
        }
    }

    [Theory]
    [InlineData("file:///tmp/bad_scheme.png", "file")]
    [InlineData(",file:///tmp/bad_scheme.png", ",file")]
    public void CustomMapSyncRejectsUnsupportedMapDownloadScheme(string mapUrl, string expectedScheme)
    {
        using var workspace = TempWorkspace.Create();
        var previousMapsDirectory = Environment.GetEnvironmentVariable("OPENGARRISON_MAPS_DIR");
        Environment.SetEnvironmentVariable("OPENGARRISON_MAPS_DIR", workspace.PathFor("ClientMaps"));
        try
        {
            using var httpClient = new HttpClient(new StubHttpMessageHandler(new Dictionary<string, StubHttpResponse>()));

            var available = CustomMapSyncService.EnsureMapAvailable(
                "bad_scheme",
                isCustomMap: true,
                mapUrl,
                string.Empty,
                httpClient,
                out var error);

            Assert.False(available);
            Assert.Contains("Unsupported map download URL scheme", error, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(expectedScheme, error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENGARRISON_MAPS_DIR", previousMapsDirectory);
            SimpleLevelFactory.ClearCachedCatalog();
        }
    }

    [Fact]
    public void CustomMapSyncRejectsUnsupportedCachedMapDownloadScheme()
    {
        using var workspace = TempWorkspace.Create();
        var previousMapsDirectory = Environment.GetEnvironmentVariable("OPENGARRISON_MAPS_DIR");
        Environment.SetEnvironmentVariable("OPENGARRISON_MAPS_DIR", workspace.PathFor("ClientMaps"));
        try
        {
            CustomMapLocatorStore.WriteMapUrl("cached_bad_scheme", ",file:///tmp/cached_bad_scheme.png");
            using var httpClient = new HttpClient(new StubHttpMessageHandler(new Dictionary<string, StubHttpResponse>()));

            var available = CustomMapSyncService.EnsureMapAvailable(
                "cached_bad_scheme",
                isCustomMap: true,
                mapDownloadUrl: string.Empty,
                mapContentHash: string.Empty,
                httpClient,
                out var error);

            Assert.False(available);
            Assert.Contains("Unsupported map download URL scheme", error, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(",file", error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENGARRISON_MAPS_DIR", previousMapsDirectory);
            SimpleLevelFactory.ClearCachedCatalog();
        }
    }

    [Fact]
    public async Task ServerMapDownloadEndpointServesPackageManifestAndImages()
    {
        using var workspace = TempWorkspace.Create();
        var previousMapsDirectory = Environment.GetEnvironmentVariable("OPENGARRISON_MAPS_DIR");
        Environment.SetEnvironmentVariable("OPENGARRISON_MAPS_DIR", workspace.PathFor("ServerMaps"));
        try
        {
            Directory.CreateDirectory(RuntimePaths.MapsDirectory);
            var packageDirectory = Path.Combine(RuntimePaths.MapsDirectory, "served_pkg");
            Directory.CreateDirectory(packageDirectory);
            var backgroundPath = workspace.PathFor("served-background.png");
            var walkmaskPath = workspace.PathFor("served-walkmask.png");
            WriteSolidPng(backgroundPath, 20, 10, new Rgba32(88, 120, 44, 255));
            WriteSolidPng(walkmaskPath, 20, 10, new Rgba32(255, 255, 255, 255));
            CustomMapPackageExporter.Export(CreateSpawnOnlyDocument(backgroundPath, walkmaskPath, 6f, 18f) with
            {
                Name = "served_pkg",
            }, Path.Combine(packageDirectory, "served_pkg.json"));
            SimpleLevelFactory.ClearCachedCatalog();

            Assert.True(CustomMapDescriptorResolver.TryResolve("served_pkg", out var descriptor));
            var port = GetFreeTcpPort();
            using var udp = new UdpClient(0);
            using var host = new WebSocketServerHost(
                port,
                certificatePath: null,
                certificatePassword: null,
                new CompositeServerMessageTransport(udp),
                _ => { },
                enableWebSocket: false,
                enableMapDownloads: true);
            host.Start();

            using var httpClient = new HttpClient();
            var manifestUri = new Uri($"http://127.0.0.1:{port}{ServerMapDownloadEndpoint.BuildRelativeDownloadUrl(descriptor)}");
            var manifestResponse = await httpClient.GetAsync(manifestUri);
            var manifestJson = await manifestResponse.Content.ReadAsStringAsync();
            var backgroundBytes = await httpClient.GetByteArrayAsync(new Uri(manifestUri, "background.png"));
            var missingResponse = await httpClient.GetAsync(new Uri(manifestUri, "missing.png"));
            using var optionsRequest = new HttpRequestMessage(HttpMethod.Options, manifestUri);
            var optionsResponse = await httpClient.SendAsync(optionsRequest);

            Assert.Equal(HttpStatusCode.OK, manifestResponse.StatusCode);
            Assert.Contains("\"name\": \"served_pkg\"", manifestJson, StringComparison.Ordinal);
            Assert.Equal("*", manifestResponse.Headers.GetValues("Access-Control-Allow-Origin").Single());
            Assert.NotEmpty(backgroundBytes);
            Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
            Assert.Equal("*", missingResponse.Headers.GetValues("Access-Control-Allow-Origin").Single());
            Assert.Equal(HttpStatusCode.NoContent, optionsResponse.StatusCode);
            Assert.Equal("*", optionsResponse.Headers.GetValues("Access-Control-Allow-Origin").Single());
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENGARRISON_MAPS_DIR", previousMapsDirectory);
            SimpleLevelFactory.ClearCachedCatalog();
        }
    }

    [Fact]
    public void ServerMapMetadataResolverAdvertisesHostedCustomMapUrl()
    {
        using var workspace = TempWorkspace.Create();
        var previousMapsDirectory = Environment.GetEnvironmentVariable("OPENGARRISON_MAPS_DIR");
        Environment.SetEnvironmentVariable("OPENGARRISON_MAPS_DIR", workspace.PathFor("ServerMaps"));
        try
        {
            Directory.CreateDirectory(RuntimePaths.MapsDirectory);
            var packageDirectory = Path.Combine(RuntimePaths.MapsDirectory, "metadata_pkg");
            Directory.CreateDirectory(packageDirectory);
            var backgroundPath = workspace.PathFor("metadata-background.png");
            var walkmaskPath = workspace.PathFor("metadata-walkmask.png");
            WriteSolidPng(backgroundPath, 20, 10, new Rgba32(40, 90, 120, 255));
            WriteSolidPng(walkmaskPath, 20, 10, new Rgba32(255, 255, 255, 255));
            CustomMapPackageExporter.Export(CreateSpawnOnlyDocument(backgroundPath, walkmaskPath, 6f, 18f) with
            {
                Name = "metadata_pkg",
            }, Path.Combine(packageDirectory, "metadata_pkg.json"));
            SimpleLevelFactory.ClearCachedCatalog();

            var world = new SimulationWorld();
            Assert.True(world.TryLoadLevel("metadata_pkg", mapAreaIndex: 1, preservePlayerStats: false));
            var resolver = new ServerMapMetadataResolver(world, ServerMapDownloadEndpoint.BuildRelativeDownloadUrl);

            var metadata = resolver.GetCurrentMapMetadata();

            Assert.True(metadata.IsCustomMap);
            Assert.Equal("/opengarrison/maps/metadata_pkg/metadata_pkg.json", metadata.MapDownloadUrl);
            Assert.StartsWith("sha256:", metadata.MapContentHash, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENGARRISON_MAPS_DIR", previousMapsDirectory);
            SimpleLevelFactory.ClearCachedCatalog();
        }
    }

    [Fact]
    public void ServerMapMetadataResolverDoesNotAdvertiseUnsupportedSourceUrl()
    {
        using var workspace = TempWorkspace.Create();
        var previousMapsDirectory = Environment.GetEnvironmentVariable("OPENGARRISON_MAPS_DIR");
        Environment.SetEnvironmentVariable("OPENGARRISON_MAPS_DIR", workspace.PathFor("ServerMaps"));
        try
        {
            Directory.CreateDirectory(RuntimePaths.MapsDirectory);
            var packageDirectory = Path.Combine(RuntimePaths.MapsDirectory, "unsupported_source_pkg");
            Directory.CreateDirectory(packageDirectory);
            var backgroundPath = workspace.PathFor("unsupported-source-background.png");
            var walkmaskPath = workspace.PathFor("unsupported-source-walkmask.png");
            WriteSolidPng(backgroundPath, 20, 10, new Rgba32(40, 90, 120, 255));
            WriteSolidPng(walkmaskPath, 20, 10, new Rgba32(255, 255, 255, 255));
            var manifestPath = Path.Combine(packageDirectory, "unsupported_source_pkg.json");
            CustomMapPackageExporter.Export(CreateSpawnOnlyDocument(backgroundPath, walkmaskPath, 6f, 18f) with
            {
                Name = "unsupported_source_pkg",
            }, manifestPath);
            CustomMapLocatorStore.WriteMapUrl("unsupported_source_pkg", ",file:///tmp/unsupported_source_pkg.json");
            SimpleLevelFactory.ClearCachedCatalog();

            var world = new SimulationWorld();
            Assert.True(world.TryLoadLevel("unsupported_source_pkg", mapAreaIndex: 1, preservePlayerStats: false));
            var resolver = new ServerMapMetadataResolver(world);

            var metadata = resolver.GetCurrentMapMetadata();

            Assert.True(metadata.IsCustomMap);
            Assert.Empty(metadata.MapDownloadUrl);
            Assert.StartsWith("sha256:", metadata.MapContentHash, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENGARRISON_MAPS_DIR", previousMapsDirectory);
            SimpleLevelFactory.ClearCachedCatalog();
        }
    }

    [Fact]
    public void CustomMapSyncCleansTempPackageFilesOnFailure()
    {
        using var workspace = TempWorkspace.Create();
        var previousMapsDirectory = Environment.GetEnvironmentVariable("OPENGARRISON_MAPS_DIR");
        Environment.SetEnvironmentVariable("OPENGARRISON_MAPS_DIR", workspace.PathFor("ClientMaps"));
        try
        {
            Directory.CreateDirectory(RuntimePaths.MapsDirectory);
            var manifestUrl = "https://example.invalid/maps/broken_map/broken_map.json";
            var manifestBytes = """
                {
                  "formatVersion": 1,
                  "name": "broken_map",
                  "scale": 6,
                  "backgroundImage": "background.png",
                  "walkmaskImage": "walkmask.png",
                  "entities": [
                    { "type": "redspawn", "x": 6, "y": 6 },
                    { "type": "bluespawn", "x": 18, "y": 6 }
                  ]
                }
                """u8.ToArray();
            using var httpClient = new HttpClient(new StubHttpMessageHandler(new Dictionary<string, StubHttpResponse>
            {
                [new Uri(manifestUrl).AbsolutePath] = new(manifestBytes, "application/json"),
            }));

            var available = CustomMapSyncService.EnsureMapAvailable(
                "broken_map",
                isCustomMap: true,
                manifestUrl,
                "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                httpClient,
                out _);

            Assert.False(available);
            Assert.False(Directory.Exists(CustomMapLocatorStore.GetPackageDirectory("broken_map")));
            Assert.Empty(Directory.EnumerateDirectories(RuntimePaths.MapsDirectory, "*.download"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENGARRISON_MAPS_DIR", previousMapsDirectory);
            SimpleLevelFactory.ClearCachedCatalog();
        }
    }

    [Fact]
    public void CustomMapSyncStillDownloadsLegacyPng()
    {
        using var workspace = TempWorkspace.Create();
        var previousMapsDirectory = Environment.GetEnvironmentVariable("OPENGARRISON_MAPS_DIR");
        Environment.SetEnvironmentVariable("OPENGARRISON_MAPS_DIR", workspace.PathFor("ClientMaps"));
        try
        {
            Directory.CreateDirectory(RuntimePaths.MapsDirectory);
            var backgroundPath = workspace.PathFor("background.png");
            var walkmaskPath = workspace.PathFor("walkmask.png");
            var serverMapPath = workspace.PathFor("legacy_sync.png");
            WriteSolidPng(backgroundPath, 4, 2, new Rgba32(32, 64, 96, 255));
            WriteWalkmaskPng(walkmaskPath);
            CustomMapPngExporter.Export(CreateSpawnOnlyDocument(backgroundPath, walkmaskPath, 6f, 18f), serverMapPath);
            var mapUrl = "https://example.invalid/maps/legacy_sync.png";
            using var httpClient = new HttpClient(new StubHttpMessageHandler(new Dictionary<string, StubHttpResponse>
            {
                [new Uri(mapUrl).AbsolutePath] = new(File.ReadAllBytes(serverMapPath), "image/png"),
            }));

            var available = CustomMapSyncService.EnsureMapAvailable(
                "legacy_sync",
                isCustomMap: true,
                mapUrl,
                CustomMapHashService.ComputeMd5(serverMapPath),
                httpClient,
                out var error);

            Assert.True(available, error);
            var localPath = CustomMapLocatorStore.GetMapPath("legacy_sync");
            Assert.True(File.Exists(localPath));
            Assert.NotNull(CustomMapPngImporter.Import(localPath));
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENGARRISON_MAPS_DIR", previousMapsDirectory);
            SimpleLevelFactory.ClearCachedCatalog();
        }
    }

    [Fact]
    public void RuntimeImporterReadsUntypedLegacyVisualMetadata()
    {
        var harvestPath = ProjectSourceLocator.FindFile(Path.Combine("Core", "Content", "StockMaps", "koth_harvest.png"));
        Assert.False(string.IsNullOrWhiteSpace(harvestPath));

        var imported = CustomMapPngImporter.Import(harvestPath!);

        Assert.NotNull(imported);
        Assert.Equal(3f, imported.Room.CustomMapVisuals.ImageScale);
        Assert.NotEmpty(imported.Room.CustomMapVisuals.ParallaxLayers);
        Assert.NotNull(imported.Room.CustomMapVisuals.Foreground);
    }

    [Fact]
    public void BuilderImporterKeepsLegacyParallaxDefaultsWhenFactorsAreMissing()
    {
        var harvestPath = FindRepoFile("Core", "Content", "StockMaps", "koth_harvest.png");

        var editable = CustomMapBuilderPngImporter.Import(harvestPath);

        Assert.NotNull(editable);
        var layers = editable.ParallaxLayers.ToDictionary(static layer => layer.Index);
        Assert.Equal(10f, layers[0].XFactor);
        Assert.Equal(10f, layers[0].YFactor);
        Assert.Equal(6f, layers[4].XFactor);
        Assert.Equal(5f, layers[5].XFactor);
        Assert.Equal(4f, layers[6].XFactor);
    }

    [Fact]
    public void RuntimeImporterCreatesMovingPlatformsFromCustomMapEntities()
    {
        using var workspace = TempWorkspace.Create();
        var backgroundPath = workspace.PathFor("background.png");
        var walkmaskPath = workspace.PathFor("walkmask.png");
        var outputPath = workspace.PathFor("moving_platform.png");
        WriteSolidPng(backgroundPath, 10, 10, new Rgba32(16, 32, 48, 255));
        WriteSolidPng(walkmaskPath, 10, 10, new Rgba32(255, 255, 255, 255));

        var document = new CustomMapBuilderDocument(
            Name: "moving_platform",
            BackgroundImagePath: backgroundPath,
            WalkmaskImagePath: walkmaskPath,
            Scale: 6f,
            Metadata: new Dictionary<string, string>(),
            Entities:
            [
                CustomMapBuilderEntity.Create("redspawn", 6f, 6f),
                CustomMapBuilderEntity.Create("bluespawn", 18f, 6f),
                CustomMapBuilderEntity.Create(
                    "moving_platform",
                    30f,
                    42f,
                    new Dictionary<string, string>
                    {
                        ["scale"] = "2",
                        ["left"] = "18",
                        ["top"] = "36",
                        ["upspeed"] = "4",
                        ["downspeed"] = "2",
                        ["trigger"] = "1",
                        ["resetMoveStatus"] = "0",
                        ["resource"] = "lift",
                    }),
            ],
            Resources: new Dictionary<string, CustomMapBuilderResource>
            {
                ["lift"] = CustomMapBuilderResourceCodec.FromFile(
                    "lift",
                    backgroundPath,
                    CustomMapBuilderResourceKind.EntitySprite),
            },
            ParallaxLayers: []);

        CustomMapPngExporter.Export(document, outputPath);
        var imported = CustomMapPngImporter.Import(outputPath);

        Assert.NotNull(imported);
        var platform = Assert.Single(imported.Room.MovingPlatforms);
        Assert.Equal(30f, platform.X);
        Assert.Equal(42f, platform.Y);
        Assert.Equal(120f, platform.Width);
        Assert.Equal(12f, platform.Height);
        Assert.Equal(18f, platform.TravelX);
        Assert.Equal(-36f, platform.TravelY);
        Assert.Equal(4f, platform.UpSpeed);
        Assert.Equal(2f, platform.DownSpeed);
        Assert.Equal(1, platform.TriggerMode);
        Assert.False(platform.ResetMovementState);
        Assert.Equal("lift", platform.ResourceName);
        Assert.DoesNotContain("moving_platform", imported.Room.UnsupportedEntities);
        Assert.True(imported.Room.CustomMapVisuals.Resources.ContainsKey("lift"));
    }

    [Fact]
    public void SimulationWorldMovesStandingPlayersWithMovingPlatforms()
    {
        var world = new SimulationWorld();
        var platformTop = 160f;
        var level = new SimpleLevel(
            "moving_platform_runtime",
            GameModeKind.CaptureTheFlag,
            new WorldBounds(500f, 300f),
            mapScale: 1f,
            backgroundAssetName: null,
            mapAreaIndex: 1,
            mapAreaCount: 1,
            localSpawn: new SpawnPoint(100f, 100f),
            redSpawns: [new SpawnPoint(100f, 100f)],
            blueSpawns: [new SpawnPoint(300f, 100f)],
            intelBases: [],
            roomObjects: [],
            floorY: 260f,
            solids: [],
            importedFromSource: true,
            movingPlatforms:
            [
                new MovingPlatformMarker(
                    80f,
                    platformTop,
                    80f,
                    6f,
                    TravelX: 30f,
                    TravelY: 0f,
                    UpSpeed: 3f,
                    DownSpeed: 3f,
                    TriggerMode: 0,
                    ResetMovementState: true),
            ]);
        SetWorldLevel(world, level);
        world.LocalPlayer.TeleportTo(100f, platformTop - world.LocalPlayer.CollisionBottomOffset);
        var playerStartX = world.LocalPlayer.X;
        var platformStartX = world.MovingPlatforms[0].X;

        world.AdvanceOneTick();

        Assert.True(world.MovingPlatforms[0].X > platformStartX);
        Assert.True(world.LocalPlayer.X > playerStartX);
        Assert.True(world.LocalPlayer.IsGrounded);
    }

    [Fact]
    public void RuntimeImporterKeepsGg2CompatibleScaleWhenEmbeddedScaleWouldClipEntities()
    {
        using var workspace = TempWorkspace.Create();
        var backgroundPath = workspace.PathFor("background.png");
        var walkmaskPath = workspace.PathFor("walkmask.png");
        var outputPath = workspace.PathFor("exported.png");
        WriteSolidPng(backgroundPath, 10, 10, new Rgba32(16, 32, 48, 255));
        WriteSolidPng(walkmaskPath, 10, 10, new Rgba32(255, 255, 255, 255));

        var document = new CustomMapBuilderDocument(
            Name: "legacy-scale",
            BackgroundImagePath: backgroundPath,
            WalkmaskImagePath: walkmaskPath,
            Scale: 3f,
            Metadata: new Dictionary<string, string>(),
            Entities:
            [
                CustomMapBuilderEntity.Create("redspawn", 50f, 10f),
                CustomMapBuilderEntity.Create("bluespawn", 55f, 10f),
            ],
            Resources: new Dictionary<string, CustomMapBuilderResource>(),
            ParallaxLayers: []);

        CustomMapPngExporter.Export(document, outputPath);
        var imported = CustomMapPngImporter.Import(outputPath);

        Assert.NotNull(imported);
        Assert.Equal(60f, imported.Room.Bounds.Width);
        Assert.Equal(60f, imported.Room.Bounds.Height);
        Assert.Equal(50f, Assert.Single(imported.Room.RedSpawns).X);
        Assert.Equal(55f, Assert.Single(imported.Room.BlueSpawns).X);
    }

    [Fact]
    public void RuntimeImporterReadsRealSavedGg2StockMap()
    {
        var mapPath = FindRepoFile("Core", "Content", "StockMaps", "ctf_2dfort.png");

        var imported = CustomMapPngImporter.Import(mapPath);

        Assert.NotNull(imported);
        Assert.Equal("ctf_2dfort", imported.Room.Name);
        Assert.NotEmpty(imported.Solids);
        Assert.NotEmpty(imported.Room.RedSpawns);
        Assert.NotEmpty(imported.Room.BlueSpawns);
        Assert.Equal(2, imported.Room.IntelBases.Count);
    }

    private static CustomMapBuilderDocument CreateSpawnOnlyDocument(
        string backgroundPath,
        string walkmaskPath,
        float redSpawnX,
        float blueSpawnX) => new(
        Name: "replace",
        BackgroundImagePath: backgroundPath,
        WalkmaskImagePath: walkmaskPath,
        Scale: 6f,
        Metadata: new Dictionary<string, string>(),
        Entities:
        [
            CustomMapBuilderEntity.Create("redspawn", redSpawnX, 6f),
            CustomMapBuilderEntity.Create("bluespawn", blueSpawnX, 6f),
        ],
        Resources: new Dictionary<string, CustomMapBuilderResource>(),
        ParallaxLayers: []);

    private static void WriteWalkmaskPng(string path)
    {
        using var image = new Image<Rgba32>(4, 2);
        image[0, 0] = new Rgba32(255, 255, 255, 255);
        image[1, 0] = new Rgba32(255, 255, 255, 255);
        image[3, 1] = new Rgba32(255, 255, 255, 255);
        image.SaveAsPng(path);
    }

    private static void WriteSolidPng(string path, int width, int height, Rgba32 color)
    {
        using var image = new Image<Rgba32>(width, height);
        for (var y = 0; y < height; y += 1)
        {
            for (var x = 0; x < width; x += 1)
            {
                image[x, y] = color;
            }
        }

        image.SaveAsPng(path);
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

    private static HttpClient CreatePackageHttpClient(string manifestPath, string manifestUrl)
    {
        var manifestUri = new Uri(manifestUrl);
        var responses = new Dictionary<string, StubHttpResponse>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in CustomMapPackageImporter.GetPackageContentFiles(manifestPath))
        {
            var uri = file.RelativePath.Equals(Path.GetFileName(manifestPath), StringComparison.OrdinalIgnoreCase)
                ? manifestUri
                : new Uri(manifestUri, file.RelativePath);
            var contentType = file.RelativePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                ? "application/json"
                : "image/png";
            responses[uri.AbsolutePath] = new StubHttpResponse(File.ReadAllBytes(file.FullPath), contentType);
        }

        return new HttpClient(new StubHttpMessageHandler(responses));
    }

    private static int GetFreeTcpPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private readonly record struct StubHttpResponse(byte[] Content, string ContentType);

    private sealed class StubHttpMessageHandler(IReadOnlyDictionary<string, StubHttpResponse> responses) : HttpMessageHandler
    {
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return CreateResponse(request);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(CreateResponse(request));
        }

        private HttpResponseMessage CreateResponse(HttpRequestMessage request)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (!responses.TryGetValue(path, out var response))
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("not found"),
                };
            }

            var content = new ByteArrayContent(response.Content);
            content.Headers.ContentType = new MediaTypeHeaderValue(response.ContentType);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = content,
            };
        }
    }

    private sealed class TempWorkspace : IDisposable
    {
        private readonly string _root;

        private TempWorkspace(string root)
        {
            _root = root;
        }

        public static TempWorkspace Create()
        {
            var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"opengarrison-builder-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            return new TempWorkspace(root);
        }

        public string PathFor(string fileName) => System.IO.Path.Combine(_root, fileName);

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
    }

    private sealed class BrowserCatalogScope : IDisposable
    {
        private readonly string _originalContentRoot;

        private BrowserCatalogScope(string rootPath, IEnumerable<KeyValuePair<string, byte[]>> assets)
        {
            _originalContentRoot = ContentRoot.Path;
            ContentRoot.Initialize(rootPath);
            BrowserContentCatalog.SetBinaryAssets(assets);
        }

        public static BrowserCatalogScope Create(string rootPath, IEnumerable<KeyValuePair<string, byte[]>> assets)
        {
            return new BrowserCatalogScope(rootPath, assets);
        }

        public void Dispose()
        {
            BrowserContentCatalog.SetBinaryAssets([]);
            ContentRoot.Initialize(_originalContentRoot);
            SimpleLevelFactory.ClearCachedCatalog();
        }
    }

    private static string FindRepoFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = System.IO.Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find repo file {string.Join('/', parts)}.");
    }

    private static void SetWorldLevel(SimulationWorld world, SimpleLevel level)
    {
        var method = typeof(SimulationWorld).GetMethod("CombatTestSetLevel", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method.Invoke(world, [level]);
    }
}
