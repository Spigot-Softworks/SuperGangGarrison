using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class CustomMapBuilderDocumentTests
{
    [Fact]
    public void CreateEmptyStartsWithGg2CompatibleMetadata()
    {
        var document = CustomMapBuilderDocument.CreateEmpty(" test_map ");

        Assert.Equal("test_map", document.Name);
        Assert.Equal(CustomMapBuilderDocument.DefaultScale, document.Scale);
        Assert.Equal("meta", document.Metadata["type"]);
        Assert.Equal(CustomMapBuilderDocument.DefaultBackgroundColor, document.Metadata["background"]);
        Assert.Equal(CustomMapBuilderDocument.DefaultVoidColor, document.Metadata["void"]);
        Assert.Equal("6", document.Metadata["scale"]);
        Assert.Empty(document.Entities);
        Assert.Empty(document.Resources);
        Assert.Empty(document.ParallaxLayers);
    }

    [Fact]
    public void NormalizeForEditingPreservesEntityDataInImporterShape()
    {
        var entity = CustomMapBuilderEntity.Create(
            " redteamgate ",
            120f,
            240f,
            new Dictionary<string, string>
            {
                ["custom"] = "value",
            },
            xScale: 2f,
            yScale: 0f);

        var normalized = entity.NormalizeForEditing();

        Assert.Equal("redteamgate", normalized.Type);
        Assert.Equal(2f, normalized.XScale);
        Assert.Equal(1f, normalized.YScale);
        Assert.Equal("redteamgate", normalized.Properties["type"]);
        Assert.Equal("120", normalized.Properties["x"]);
        Assert.Equal("240", normalized.Properties["y"]);
        Assert.Equal("2", normalized.Properties["xscale"]);
        Assert.False(normalized.Properties.ContainsKey("yscale"));
        Assert.Equal("value", normalized.Properties["custom"]);
    }

    [Fact]
    public void BuildExportMetadataMergesDefaultsScaleAndParallaxLayers()
    {
        var document = new CustomMapBuilderDocument(
            Name: "  ",
            BackgroundImagePath: "bg.png",
            WalkmaskImagePath: "wm.png",
            Scale: -1f,
            Metadata: new Dictionary<string, string>
            {
                ["background"] = "112233",
                ["void"] = "445566",
                ["author"] = "builder",
            },
            Entities:
            [
                CustomMapBuilderEntity.Create("redspawn", 10f, 20f),
            ],
            Resources: new Dictionary<string, CustomMapBuilderResource>
            {
                ["sky"] = new(" sky ", " layer.png ", CustomMapBuilderResourceKind.ParallaxLayer),
            },
            ParallaxLayers:
            [
                new CustomMapBuilderParallaxLayer(10, "sky", 12f, -2f),
            ]);

        var normalized = document.NormalizeForEditing();
        var metadata = normalized.BuildExportMetadata();

        Assert.Equal("untitled", normalized.Name);
        Assert.Equal(CustomMapBuilderDocument.DefaultScale, normalized.Scale);
        Assert.True(normalized.Resources.ContainsKey("sky"));
        Assert.Single(normalized.ParallaxLayers);
        Assert.Equal(CustomMapBuilderParallaxLayer.MaxIndex, normalized.ParallaxLayers[0].Index);
        Assert.Equal("112233", metadata["background"]);
        Assert.Equal("445566", metadata["void"]);
        Assert.Equal("builder", metadata["author"]);
        Assert.Equal("6", metadata["scale"]);
        Assert.Equal("sky", metadata["bg_layer6"]);
        Assert.Equal("10", metadata["layer6xfactor"]);
        Assert.Equal("0", metadata["layer6yfactor"]);
    }

    [Fact]
    public void BuildExportMetadataEmbedsResourceBytesAsLegacyStrings()
    {
        var resourceBytes = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 1, 2, 3 };
        var document = CustomMapBuilderDocument.CreateEmpty("resources") with
        {
            Resources = new Dictionary<string, CustomMapBuilderResource>
            {
                ["sky"] = new("sky", string.Empty, CustomMapBuilderResourceKind.ParallaxLayer, resourceBytes),
            },
            ParallaxLayers =
            [
                new CustomMapBuilderParallaxLayer(0, "sky", 0.25f, 0.5f),
            ],
        };

        var metadata = document.BuildExportMetadata();
        var decoded = CustomMapBuilderResourceCodec.DecodeLegacyString(metadata["bg_layer0"]);

        Assert.Equal(resourceBytes, decoded);
        Assert.Equal(resourceBytes, CustomMapBuilderResourceCodec.DecodeLegacyString(metadata["sky"]));
        Assert.Equal("0.25", metadata["layer0xfactor"]);
        Assert.Equal("0.5", metadata["layer0yfactor"]);
    }

    [Fact]
    public void ResourceCodecRejectsNonImageLegacyStrings()
    {
        var decoded = CustomMapBuilderResourceCodec.TryDecodeLegacyString(
            "bad",
            "not image data",
            CustomMapBuilderResourceKind.GenericImage,
            out _);

        Assert.False(decoded);
    }

    [Fact]
    public void EntityCatalogIncludesLegacyBuilderInitEntitiesAndDefaults()
    {
        Assert.True(CustomMapBuilderEntityCatalog.TryGetDefinition("redspawn4", out var redSpawn4));
        Assert.Equal(46, redSpawn4.IconFrame);

        Assert.True(CustomMapBuilderEntityCatalog.TryGetDefinition("moving_platform", out var movingPlatform));
        Assert.Equal(112, movingPlatform.IconFrame);
        Assert.Equal("60", movingPlatform.DefaultProperties["top"]);
        Assert.Equal("3", movingPlatform.DefaultProperties["upspeed"]);

        Assert.True(CustomMapBuilderEntityCatalog.TryGetDefinition("medCabinet", out var medCabinet));
        var entity = medCabinet.CreateEntity(12f, 18f);
        Assert.Equal("medCabinet", entity.Properties["type"]);
        Assert.Equal("true", entity.Properties["heal"]);
        Assert.Equal("true", entity.Properties["refill"]);
        Assert.Equal("false", entity.Properties["uber"]);
    }

    [Fact]
    public void ValidatorReportsMissingCoreRequirements()
    {
        var document = CustomMapBuilderDocument.CreateEmpty("invalid") with
        {
            EmbeddedWalkmaskSection = "2\n1\n ",
            Entities =
            [
                CustomMapBuilderEntity.Create("redspawn", 6f, 6f),
                CustomMapBuilderEntity.Create("redintel", 12f, 6f),
            ],
        };

        var result = CustomMapBuilderValidator.Validate(document, CustomMapBuilderGameMode.CaptureTheFlag);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "missing_blue_spawn");
        Assert.Contains(result.Issues, issue => issue.Code == "ctf_blue_intel");
        Assert.Contains(result.Issues, issue => issue.Code == "missing_solids");
    }

    [Fact]
    public void ValidatorAcceptsGeneratorMapWithSpawnsGeneratorsAndSolids()
    {
        var document = CustomMapBuilderDocument.CreateEmpty("gen") with
        {
            EmbeddedWalkmaskSection = "2\n1\n@",
            Entities =
            [
                CustomMapBuilderEntity.Create("redspawn", 6f, 6f),
                CustomMapBuilderEntity.Create("bluespawn", 18f, 6f),
                CustomMapBuilderEntity.Create("GeneratorRed", 12f, 6f),
                CustomMapBuilderEntity.Create("GeneratorBlue", 24f, 6f),
            ],
        };

        var result = CustomMapBuilderValidator.Validate(document);

        Assert.True(result.IsValid);
        Assert.Equal(CustomMapBuilderGameMode.Generator, result.Mode);
    }
}
