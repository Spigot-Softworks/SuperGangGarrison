using OpenGarrison.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class LevelCatalogCollisionTests
{
    [Fact]
    public void ShouldWarnWhenUnsavedMapSharesNameWithBuiltInCatalogEntry()
    {
        var catalog = new[]
        {
            CreateCatalogEntry("Harvest", "stock/Harvest.json", isCustomMap: false),
        };

        Assert.True(LevelCatalogCollisions.ShouldWarnAboutMapNameCollision(catalog, "Harvest"));
        Assert.False(LevelCatalogCollisions.ShouldWarnAboutMapNameCollision(catalog, "Harvest", "stock/Harvest.json"));
    }

    [Fact]
    public void ShouldNotWarnWhenEditingSavedCustomMapThatSharesBuiltInName()
    {
        var catalog = new[]
        {
            CreateCatalogEntry("Harvest", "stock/Harvest.json", isCustomMap: false),
            CreateCatalogEntry("Harvest", "custom/Harvest.json", isCustomMap: true),
        };

        Assert.True(LevelCatalogCollisions.HasDuplicateMapName(catalog, "Harvest"));
        Assert.Equal(2, LevelCatalogCollisions.FindEntriesByName(catalog, "Harvest").Count);
        Assert.False(LevelCatalogCollisions.ShouldWarnAboutMapNameCollision(catalog, "Harvest", "custom/Harvest.json"));
        Assert.True(LevelCatalogCollisions.ShouldWarnAboutMapNameCollision(catalog, "Harvest"));
    }

    [Fact]
    public void ShouldNotWarnWhenNameOnlyMatchesAnotherCustomMap()
    {
        var catalog = new[]
        {
            CreateCatalogEntry("MyMap", "custom/MyMap.json", isCustomMap: true),
        };

        Assert.False(LevelCatalogCollisions.ShouldWarnAboutMapNameCollision(catalog, "MyMap"));
        Assert.False(LevelCatalogCollisions.ShouldWarnAboutMapNameCollision(catalog, "MyMap", "custom/MyMap.json"));
    }

    [Fact]
    public void CatalogLookupPrefersFirstEntryWhenCustomMapSharesStockName()
    {
        using var workspace = TempWorkspace.Create();
        var previousMapsDirectory = Environment.GetEnvironmentVariable("OPENGARRISON_MAPS_DIR");
        Environment.SetEnvironmentVariable("OPENGARRISON_MAPS_DIR", workspace.PathFor("Maps"));
        try
        {
            Directory.CreateDirectory(RuntimePaths.MapsDirectory);
            var backgroundPath = workspace.PathFor("background.png");
            var walkmaskPath = workspace.PathFor("walkmask.png");
            WriteSolidPng(backgroundPath, 8, 4, new Rgba32(32, 64, 96, 255));
            WriteWalkmaskPng(walkmaskPath);

            var markerX = 99f;
            var markerY = 88f;
            var document = CreateSpawnOnlyDocument(backgroundPath, walkmaskPath, 12f, 36f) with
            {
                Name = "Harvest",
                Entities =
                [
                    CustomMapBuilderEntity.Create("redspawn", 12f, 6f),
                    CustomMapBuilderEntity.Create("bluespawn", 36f, 6f),
                    CustomMapBuilderEntity.Create("logicCustomSprite", markerX, markerY, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        [CustomMapCustomSpriteMetadata.ImagePropertyKey] = "prop",
                    }),
                ],
                Resources = new Dictionary<string, CustomMapBuilderResource>(StringComparer.OrdinalIgnoreCase)
                {
                    ["prop"] = CreateSpriteResource(workspace.PathFor("prop.png")),
                },
            };

            var packageDirectory = Path.Combine(RuntimePaths.MapsDirectory, "Harvest");
            CustomMapPackageExporter.Export(document, packageDirectory);

            SimpleLevelFactory.ClearCachedCatalog();
            var catalog = SimpleLevelFactory.GetAvailableSourceLevels();
            var harvestEntries = LevelCatalogCollisions.FindEntriesByName(catalog, "Harvest");
            Assert.True(harvestEntries.Count >= 2, "Expected stock and custom Harvest entries in the catalog.");

            var loaded = SimpleLevelFactory.CreateImportedLevel("Harvest");
            Assert.NotNull(loaded);
            Assert.DoesNotContain(
                loaded!.RoomObjects,
                marker => marker.Type == RoomObjectType.CustomMapSprite
                    && MathF.Abs(marker.CenterX - markerX) < 0.01f
                    && MathF.Abs(marker.CenterY - markerY) < 0.01f);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENGARRISON_MAPS_DIR", previousMapsDirectory);
            SimpleLevelFactory.ClearCachedCatalog();
        }
    }

    [Fact]
    public void QuickTestLevelName_LoadsCustomPackageInsteadOfStockMap()
    {
        using var workspace = TempWorkspace.Create();
        var previousMapsDirectory = Environment.GetEnvironmentVariable("OPENGARRISON_MAPS_DIR");
        Environment.SetEnvironmentVariable("OPENGARRISON_MAPS_DIR", workspace.PathFor("Maps"));
        try
        {
            Directory.CreateDirectory(RuntimePaths.MapsDirectory);
            var backgroundPath = workspace.PathFor("background.png");
            var walkmaskPath = workspace.PathFor("walkmask.png");
            WriteSolidPng(backgroundPath, 8, 4, new Rgba32(32, 64, 96, 255));
            WriteWalkmaskPng(walkmaskPath);

            var markerX = 77f;
            var markerY = 66f;
            var quickTestLevelName = GarrisonBuilderQuickTestNaming.BuildQuickTestLevelName("Harvest");
            var document = CreateSpawnOnlyDocument(backgroundPath, walkmaskPath, 12f, 36f) with
            {
                Name = quickTestLevelName,
                Entities =
                [
                    CustomMapBuilderEntity.Create("redspawn", 12f, 6f),
                    CustomMapBuilderEntity.Create("bluespawn", 36f, 6f),
                    CustomMapBuilderEntity.Create("logicCustomSprite", markerX, markerY, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        [CustomMapCustomSpriteMetadata.ImagePropertyKey] = "prop",
                    }),
                ],
                Resources = new Dictionary<string, CustomMapBuilderResource>(StringComparer.OrdinalIgnoreCase)
                {
                    ["prop"] = CreateSpriteResource(workspace.PathFor("prop.png")),
                },
            };

            var packageDirectory = Path.Combine(RuntimePaths.MapsDirectory, quickTestLevelName);
            CustomMapPackageExporter.Export(document, packageDirectory);

            SimpleLevelFactory.ClearCachedCatalog();
            var loaded = SimpleLevelFactory.CreateImportedLevel(quickTestLevelName);
            Assert.NotNull(loaded);
            Assert.Contains(
                loaded!.RoomObjects,
                marker => marker.Type == RoomObjectType.CustomMapSprite
                    && MathF.Abs(marker.CenterX - markerX) < 0.01f
                    && MathF.Abs(marker.CenterY - markerY) < 0.01f);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENGARRISON_MAPS_DIR", previousMapsDirectory);
            SimpleLevelFactory.ClearCachedCatalog();
        }
    }

    [Fact]
    public void BuildQuickTestLevelName_UsesPrefixedSanitizedName()
    {
        Assert.Equal("_garrison_quicktest_Harvest", GarrisonBuilderQuickTestNaming.BuildQuickTestLevelName("Harvest"));
        Assert.Equal("_garrison_quicktest_My_Map", GarrisonBuilderQuickTestNaming.BuildQuickTestLevelName("My Map!"));
    }

    private static SimpleLevelFactory.LevelCatalogEntry CreateCatalogEntry(
        string name,
        string sourcePath,
        bool isCustomMap)
    {
        return new SimpleLevelFactory.LevelCatalogEntry(
            name,
            GameModeKind.CaptureTheFlag,
            sourcePath,
            null,
            CustomMapSourceKind.Package,
            isCustomMap);
    }

    private static CustomMapBuilderDocument CreateSpawnOnlyDocument(
        string backgroundPath,
        string walkmaskPath,
        float redSpawnX,
        float blueSpawnX)
    {
        return new CustomMapBuilderDocument(
            Name: "test_map",
            BackgroundImagePath: backgroundPath,
            WalkmaskImagePath: walkmaskPath,
            Scale: 6f,
            VisualScale: 0f,
            Metadata: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            Entities:
            [
                CustomMapBuilderEntity.Create("redspawn", redSpawnX, 6f),
                CustomMapBuilderEntity.Create("bluespawn", blueSpawnX, 6f),
            ],
            Resources: new Dictionary<string, CustomMapBuilderResource>(StringComparer.OrdinalIgnoreCase),
            ParallaxLayers: Array.Empty<CustomMapBuilderParallaxLayer>());
    }

    private static CustomMapBuilderResource CreateSpriteResource(string path)
    {
        WriteSolidPng(path, 8, 8, new Rgba32(200, 100, 50, 255));
        return CustomMapBuilderResourceCodec.FromFile("prop", path, CustomMapBuilderResourceKind.CustomSprite);
    }

    private static void WriteSolidPng(string path, int width, int height, Rgba32 color)
    {
        using var image = new Image<Rgba32>(width, height);
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y += 1)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x += 1)
                {
                    row[x] = color;
                }
            }
        });
        image.SaveAsPng(path);
    }

    private static void WriteWalkmaskPng(string path)
    {
        WriteSolidPng(path, 8, 4, new Rgba32(255, 255, 255, 255));
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
            var root = Path.Combine(Path.GetTempPath(), $"opengarrison-collision-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            return new TempWorkspace(root);
        }

        public string PathFor(string fileName) => Path.Combine(_root, fileName);

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
    }
}
