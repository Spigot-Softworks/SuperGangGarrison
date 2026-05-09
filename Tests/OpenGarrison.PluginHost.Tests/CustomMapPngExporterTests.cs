using OpenGarrison.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class CustomMapPngExporterTests
{
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
