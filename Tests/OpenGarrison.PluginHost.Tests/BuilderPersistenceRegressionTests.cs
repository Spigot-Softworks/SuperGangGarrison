using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using OpenGarrison.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class BuilderPersistenceRegressionTests : IDisposable
{
    [Theory]
    [InlineData("cp_dirtbowl")]
    [InlineData("ctf_2dfort")]
    [InlineData("koth_harvest")]
    public void StockMapsLoadEditSaveReopenAndRun(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "Core", "Content", "StockMaps"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var source = Path.Combine(directory!.FullName, "Core", "Content", "StockMaps", name + ".png");
        var before = File.ReadAllBytes(source);
        var document = CustomMapBuilderPngImporter.Import(source)!.NormalizeForEditing();
        Assert.NotEmpty(document.Entities);
        BuilderImageValidation.Validate(before);
        var validation = CustomMapBuilderValidator.Validate(document);
        Assert.True(validation.IsValid, string.Join("; ", validation.Issues.Select(issue => issue.Message)));
        var entities = document.Entities.ToArray();
        entities[0] = (entities[0] with { X = entities[0].X + 72 }).NormalizeForEditing();
        var edited = document with { Entities = [..entities.SkipLast(1), CustomMapBuilderEntity.Create("medCabinet", 102, 60)] };
        var output = Path.Combine(_root, name + ".png");
        CustomMapPngExporter.Export(edited, output);
        var reopened = CustomMapBuilderPngImporter.Import(output)!;
        Assert.Equal(entities[0].X, reopened.Entities[0].X);
        Assert.Equal("medCabinet", reopened.Entities[^1].Type);
        Assert.NotNull(CustomMapPngImporter.Import(output));
        Assert.True(EmbeddedWalkmaskDecoder.TryDecodeSolidCells(document.EmbeddedWalkmaskSection, out var w, out var h, out var cells));
        Assert.True(EmbeddedWalkmaskDecoder.TryDecodeSolidCells(reopened.EmbeddedWalkmaskSection, out var w2, out var h2, out var cells2));
        Assert.Equal((w, h), (w2, h2)); Assert.Equal(cells, cells2);
        CustomMapPngExporter.Export(reopened, output);
        var package = Path.Combine(_root, name, name + ".json");
        CustomMapPackageExporter.Export(reopened, package);
        Assert.Equal(entities[0].X, CustomMapPackageImporter.ImportDocument(package)!.Entities[0].X);
        Assert.NotNull(CustomMapPackageImporter.Import(package));
        Assert.Equal(before, File.ReadAllBytes(source));
    }

    [Fact]
    public void RemovedLayerStaysRemovedButReusableResourceRemains()
    {
        var resource = CustomMapBuilderResourceCodec.FromFile("sky", _document.BackgroundImagePath, CustomMapBuilderResourceKind.ParallaxLayer);
        var original = _document with { Resources = new Dictionary<string, CustomMapBuilderResource> { ["sky"] = resource }, ParallaxLayers = [new(1, "sky", .2f, .4f)] };
        var png = Path.Combine(_root, "layer.png"); CustomMapPngExporter.Export(original, png);
        var imported = CustomMapBuilderPngImporter.Import(png)!;
        Assert.Single(imported.ParallaxLayers);
        CustomMapPngExporter.Export(imported with { ParallaxLayers = [] }, png);
        var edited = CustomMapBuilderPngImporter.Import(png)!;
        Assert.Empty(edited.ParallaxLayers);
        Assert.Contains("sky", edited.Resources.Keys);
    }

    [Fact]
    public void StaticGifIsTranscodedAndAnimationIsRejectedWithoutOverwriting()
    {
        var gif = Path.Combine(_root, "static.gif");
        using (var image = new Image<Rgba32>(4, 4, new Rgba32(255, 0, 0, 255))) image.SaveAsGif(gif);
        var document = _document with { Resources = new Dictionary<string, CustomMapBuilderResource> { ["sprite"] = CustomMapBuilderResourceCodec.FromFile("sprite", gif) } };
        var output = Path.Combine(_root, "gif-map.json"); CustomMapPackageExporter.Export(document, output);
        Assert.EndsWith(".png", CustomMapPackageImporter.ImportDocument(output)!.Resources["sprite"].SourcePath);
        var before = File.ReadAllBytes(output);
        using (var image = new Image<Rgba32>(4, 4)) { using var other = new Image<Rgba32>(4, 4, new Rgba32(0,255,0,255)); image.Frames.AddFrame(other.Frames[0]); image.SaveAsGif(gif); }
        document = document with { Resources = new Dictionary<string, CustomMapBuilderResource> { ["sprite"] = CustomMapBuilderResourceCodec.FromFile("sprite", gif) } };
        Assert.Throws<InvalidOperationException>(() => CustomMapPackageExporter.Export(document, output));
        Assert.Equal(before, File.ReadAllBytes(output));
    }

    [Fact]
    public void MalformedInputsFailCleanlyAndCannotReplaceSavedDraft()
    {
        var png = Path.Combine(_root, "bad.png"); File.WriteAllBytes(png, [137,80,78,71,13,10,26,10,0,0]);
        Assert.ThrowsAny<IOException>(() => CustomMapBuilderPngImporter.Import(png));
        Assert.Null(CustomMapPngImporter.Import(png));
        var manifest = Path.Combine(_root, "bad.json"); File.WriteAllText(manifest, "{\"entities\":[null]}");
        Assert.Null(CustomMapPackageImporter.ImportDocument(manifest));
        Assert.False(EmbeddedWalkmaskDecoder.TryDecodeSolidCells("9999999999 9999999999\n_", out _, out _, out _));
        var path = Path.Combine(_root, "draft.ogmap"); BuilderProjectStore.Save(_document, path); var before = File.ReadAllBytes(path);
        Assert.Throws<InvalidDataException>(() => BuilderProjectStore.Save(_document with { Entities = [null!] }, path));
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    private readonly string _root = Path.Combine(Path.GetTempPath(), "builder-regression-" + Guid.NewGuid().ToString("N"));
    private readonly CustomMapBuilderDocument _document;

    [Fact]
    public void AlreadyBrokenSaveRecoversNewestValidExplicitPayload()
    {
        var legacy = Path.Combine(_root, "broken.png"); WriteLegacy(legacy, "zTXt");
        var bytes = File.ReadAllBytes(legacy);
        var edited = _document with { Entities = [CustomMapBuilderEntity.Create("redspawn", 72, 18)] };
        using (var output = File.Create(legacy))
        {
            output.Write(bytes.AsSpan(0, bytes.Length - 12));
            Chunk(output, "tEXt", Encoding.Latin1.GetBytes("OpenGarrisonLevelData\0" + CustomMapPngExporter.BuildLevelData(edited)));
            // An incomplete later attempt must not displace the intact newer save.
            Chunk(output, "tEXt", Encoding.Latin1.GetBytes("OpenGarrisonLevelData\0{ENTITIES}broken{END ENTITIES}{WALKMASK}broken{END WALKMASK}"));
            output.Write(bytes.AsSpan(bytes.Length - 12));
        }
        Assert.Equal(72, CustomMapBuilderPngImporter.Import(legacy, out var warning)!.Entities[0].X);
        Assert.NotEmpty(warning); Assert.NotNull(CustomMapPngImporter.Import(legacy));
        var before = File.ReadAllBytes(legacy);
        CustomMapPngExporter.Export(edited, legacy);
        Assert.Equal(before, File.ReadAllBytes(legacy + ".bak"));
    }

    [Fact]
    public void EmptyNonSixDivisibleCollisionSurvivesBothFormats()
    {
        var wm = Path.Combine(_root, "empty-wm.png"); using (var image = new Image<Rgba32>(5,7)) image.SaveAsPng(wm);
        var doc = _document with { WalkmaskImagePath = wm, Metadata = new Dictionary<string,string>(_document.Metadata) { [MapMovementModeMetadata.MovementModePropertyKey] = MapMovementModeMetadata.TopDownPropertyValue } };
        var output = Path.Combine(_root, "empty.png"); CustomMapPngExporter.Export(doc, output);
        var reopened = CustomMapBuilderPngImporter.Import(output)!;
        Assert.True(EmbeddedWalkmaskDecoder.TryDecodeSolidCells(reopened.EmbeddedWalkmaskSection, out var w, out var h, out var cells));
        Assert.Equal((5,7), (w,h)); Assert.All(cells, c => Assert.False(c));
        var package = Path.Combine(_root, "empty.json"); CustomMapPackageExporter.Export(reopened, package);
        using var saved = Image.Load<Rgba32>(CustomMapPackageImporter.ImportDocument(package)!.WalkmaskImagePath);
        Assert.Equal(5, saved.Width); Assert.Equal(7, saved.Height); Assert.Equal((byte)0, saved[4,6].A);
    }

    [Fact]
    public void LockedWindowsDestinationPreservesThePreviousCompleteSave()
    {
        if (!OperatingSystem.IsWindows()) return; // Unix file locks do not prevent renaming an open file.
        var output = Path.Combine(_root, "locked.png"); CustomMapPngExporter.Export(_document, output);
        var before = File.ReadAllBytes(output);
        using (var locked = new FileStream(output, FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.ThrowsAny<IOException>(() => CustomMapPngExporter.Export(_document with { Entities = [] }, output));
        Assert.Equal(before, File.ReadAllBytes(output));
        var package = Path.Combine(_root, "locked.json"); CustomMapPackageExporter.Export(_document, package); before = File.ReadAllBytes(package);
        using (var locked = new FileStream(package, FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.ThrowsAny<IOException>(() => CustomMapPackageExporter.Export(_document with { Entities = [] }, package));
        Assert.Equal(before, File.ReadAllBytes(package)); Assert.NotNull(CustomMapPackageImporter.Import(package));
    }

    public BuilderPersistenceRegressionTests()
    {
        Directory.CreateDirectory(_root);
        var bg = Path.Combine(_root, "source-bg.png");
        var wm = Path.Combine(_root, "source-wm.png");
        using (var image = new Image<Rgba32>(24, 16, new Rgba32(64, 128, 192, 255))) image.SaveAsPng(bg);
        using (var image = new Image<Rgba32>(24, 16)) { image[0, 0] = new Rgba32(255, 255, 255, 255); image.SaveAsPng(wm); }
        _document = CustomMapBuilderDocument.CreateEmpty("map") with
        {
            BackgroundImagePath = bg, WalkmaskImagePath = wm,
            Entities = [CustomMapBuilderEntity.Create("redspawn", 12, 18), CustomMapBuilderEntity.Create("bluespawn", 42, 18)],
        };
    }

    [Theory]
    [InlineData("tEXt", false)] [InlineData("tEXt", true)]
    [InlineData("zTXt", false)] [InlineData("zTXt", true)]
    [InlineData("iTXt", false)] [InlineData("iTXt", true)]
    [InlineData("iTXt-compressed", false)] [InlineData("iTXt-compressed", true)]
    [InlineData("split", false)] [InlineData("split", true)]
    public void EditedLegacyMapSurvivesRepeatedSaveAndRuntimeLoad(string encoding, bool overwrite)
    {
        var source = Path.Combine(_root, "legacy.png");
        WriteLegacy(source, encoding);
        var loaded = CustomMapBuilderPngImporter.Import(source)!;
        var edited = loaded with { Entities = [CustomMapBuilderEntity.Create("redspawn", 72, 18), CustomMapBuilderEntity.Create("medCabinet", 60, 30)] };
        var output = overwrite ? source : Path.Combine(_root, "edited.png");
        CustomMapPngExporter.Export(edited, output);
        var reopened = CustomMapBuilderPngImporter.Import(output)!;
        Assert.Equal(72, reopened.Entities[0].X);
        Assert.Equal("medCabinet", reopened.Entities[1].Type);
        Assert.NotNull(CustomMapPngImporter.Import(output));
        CustomMapPngExporter.Export(reopened, output);
        Assert.Equal(72, CustomMapBuilderPngImporter.Import(output)!.Entities[0].X);
        Assert.True(EmbeddedWalkmaskDecoder.TryDecodeSolidCells(reopened.EmbeddedWalkmaskSection, out _, out _, out var cells));
        Assert.True(cells[0]); Assert.Equal(1, cells.Count(c => c));
    }

    [Fact]
    public void FailedPngSaveLeavesPreviousMapIntact()
    {
        var path = Path.Combine(_root, "map.png"); CustomMapPngExporter.Export(_document, path);
        var before = File.ReadAllBytes(path);
        var bad = Path.Combine(_root, "bad.png"); File.WriteAllText(bad, "not a PNG");
        Assert.ThrowsAny<Exception>(() => CustomMapPngExporter.Export(_document with { BackgroundImagePath = bad }, path));
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public void FailedPackageSaveLeavesManifestAndAllReferencedAssetsIntact()
    {
        var path = Path.Combine(_root, "package", "map.json"); CustomMapPackageExporter.Export(_document, path);
        var before = CustomMapHashService.ComputePackageSha256(path);
        var bg = Path.Combine(_root, "green.png"); using (var image = new Image<Rgba32>(24, 16, new Rgba32(0,255,0,255))) image.SaveAsPng(bg);
        var bad = _document with { BackgroundImagePath = bg, Resources = new Dictionary<string, CustomMapBuilderResource> { ["missing"] = new("missing", Path.Combine(_root, "missing.png"), CustomMapBuilderResourceKind.GenericImage) } };
        Assert.ThrowsAny<Exception>(() => CustomMapPackageExporter.Export(bad, path));
        Assert.Equal(before, CustomMapHashService.ComputePackageSha256(path));
        CustomMapPackageExporter.Export(_document with { BackgroundImagePath = bg }, path);
        Assert.NotEqual(before, CustomMapHashService.ComputePackageSha256(path));
        Assert.True(File.Exists(path + ".bak"));
    }

    [Fact]
    public void DefaultsUnicodeAndEmptyEntityListsRoundTrip()
    {
        var entity = CustomMapBuilderEntity.Create("SetupGate", 20, 20, xScale: 2, yScale: 2).NormalizeForEditing();
        entity = (entity with { XScale = 1, YScale = 1 }).NormalizeForEditing();
        var path = Path.Combine(_root, "map.png");
        CustomMapPngExporter.Export(_document with { Entities = [entity], Metadata = new Dictionary<string,string> { ["author"] = "Ω 日本語" } }, path);
        var read = CustomMapBuilderPngImporter.Import(path)!;
        Assert.Equal(1, read.Entities[0].XScale); Assert.Equal("Ω 日本語", read.Metadata["author"]);
        CustomMapPngExporter.Export(_document with { Entities = [] }, path);
        Assert.Empty(CustomMapBuilderPngImporter.Import(path)!.Entities);
    }

    [Fact]
    public void DraftsPreserveIncompleteMapsAndEmbeddedAssets()
    {
        var path = Path.Combine(_root, "draft.ogmap");
        BuilderProjectStore.Save(CustomMapBuilderDocument.CreateEmpty(), path);
        Assert.Empty(BuilderProjectStore.Load(path, Path.Combine(_root, "cache")).Entities);
        BuilderProjectStore.Save(_document, path);
        var loaded = BuilderProjectStore.Load(path, Path.Combine(_root, "cache"));
        Assert.Equal(File.ReadAllBytes(_document.BackgroundImagePath), File.ReadAllBytes(loaded.BackgroundImagePath));
        Assert.Equal(_document.Entities.Count, loaded.Entities.Count);
    }

    [Fact]
    public void ConvertedPackageRefreshesOnlyWhenItsOwnContentIsUnchanged()
    {
        var source = Path.Combine(_root, "converted.png"); CustomMapPngExporter.Export(_document, source);
        Assert.True(CustomMapLegacyPackageAutoConverter.TryConvertLegacyPng(source, out var package, out var error), error);
        var edited = _document with { Entities = [CustomMapBuilderEntity.Create("redspawn", 72, 18)] };
        CustomMapPngExporter.Export(edited, source);
        Assert.True(CustomMapLegacyPackageAutoConverter.TryConvertLegacyPng(source, out _, out error), error);
        Assert.Equal(72, CustomMapPackageImporter.ImportDocument(package)!.Entities[0].X);
        CustomMapPackageExporter.Export(edited with { Entities = [CustomMapBuilderEntity.Create("redspawn", 90,18)] }, package);
        CustomMapPngExporter.Export(_document, source);
        Assert.False(CustomMapLegacyPackageAutoConverter.TryConvertLegacyPng(source, out _, out _));
        Assert.Equal(90, CustomMapPackageImporter.ImportDocument(package)!.Entities[0].X);
    }

    [Fact]
    public void ExplicitManifestPathDoesNotChange()
    {
        var path = Path.Combine(_root, "collection", "map.json");
        Assert.Equal(path, CustomMapPackageExporter.ResolvePackageManifestPath(_document, path));
    }

    [Fact]
    public void MissingSoundsCannotBeSilentlyDiscardedByPngExport()
    {
        var document = _document with { Resources = new Dictionary<string,CustomMapBuilderResource> { ["sound"] = new("sound", "", CustomMapBuilderResourceKind.MessageSound, Encoding.ASCII.GetBytes("OggS")) } };
        Assert.Throws<InvalidOperationException>(() => CustomMapPngExporter.Export(document, Path.Combine(_root, "sound.png")));
    }

    private void WriteLegacy(string path, string format)
    {
        var background = File.ReadAllBytes(_document.BackgroundImagePath);
        using var output = File.Create(path); output.Write(background.AsSpan(0, background.Length - 12));
        var data = CustomMapPngExporter.BuildLevelData(_document);
        if (format == "split")
        {
            Chunk(output, "tEXt", Encoding.Latin1.GetBytes("Entities\0" + CustomMapPngExporter.BuildEntitiesSection(_document)));
            Chunk(output, "tEXt", Encoding.Latin1.GetBytes("Walkmask\0" + CustomMapPngExporter.BuildWalkmaskSection(_document.WalkmaskImagePath)));
        }
        else
        {
            var type = format.StartsWith("iTXt") ? "iTXt" : format;
            var payload = format.StartsWith("iTXt") ? Encoding.UTF8.GetBytes(data) : Encoding.Latin1.GetBytes(data);
            var compress = format is "zTXt" or "iTXt-compressed";
            if (compress) { using var m = new MemoryStream(); using (var z = new ZLibStream(m, CompressionLevel.Optimal, true)) z.Write(payload); payload = m.ToArray(); }
            var header = format switch { "tEXt" => "Level\0", "zTXt" => "Level\0\0", "iTXt" => "Level\0\0\0\0\0", _ => "Level\0\x01\0\0\0" };
            Chunk(output, type, Encoding.ASCII.GetBytes(header).Concat(payload).ToArray());
        }
        output.Write(background.AsSpan(background.Length - 12));
    }
    private static void Chunk(Stream stream, string type, byte[] data)
    {
        Span<byte> b = stackalloc byte[4]; BinaryPrimitives.WriteInt32BigEndian(b, data.Length); stream.Write(b);
        var name = Encoding.ASCII.GetBytes(type); stream.Write(name); stream.Write(data); uint crc = 0xffffffff;
        foreach (var v in name.Concat(data)) { crc ^= v; for (var i = 0; i < 8; i++) crc = (crc & 1) != 0 ? 0xedb88320 ^ (crc >> 1) : crc >> 1; }
        BinaryPrimitives.WriteUInt32BigEndian(b, crc ^ 0xffffffff); stream.Write(b);
    }
    public void Dispose() => Directory.Delete(_root, recursive: true);
}
