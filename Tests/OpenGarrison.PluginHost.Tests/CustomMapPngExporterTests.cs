using System.Net;
using System.Net.Http.Headers;
using OpenGarrison.Client;
using OpenGarrison.Core;
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
            VisualScale: 0f,
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
            VisualScale: 0f,
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
            VisualScale: 0f,
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
                VisualScale: 0f,
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
        }
        finally
        {
            SimpleLevelFactory.ClearCachedCatalog();
        }
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
            VisualScale: 0f,
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
        VisualScale: 0f,
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
}
