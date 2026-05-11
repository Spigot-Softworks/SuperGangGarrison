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
