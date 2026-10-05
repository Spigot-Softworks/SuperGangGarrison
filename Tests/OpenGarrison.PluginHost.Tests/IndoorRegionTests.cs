using OpenGarrison.Client;
using OpenGarrison.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;
using XnaColor = Microsoft.Xna.Framework.Color;
using XnaRectangle = Microsoft.Xna.Framework.Rectangle;

namespace OpenGarrison.PluginHost.Tests;

public sealed class IndoorRegionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "indoor-region-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void IndoorRegionsSurviveExportAndImportAsRectangles()
    {
        Directory.CreateDirectory(_root);
        var background = Path.Combine(_root, "bg.png");
        var walkmask = Path.Combine(_root, "wm.png");
        using (var image = new Image<Rgba32>(24, 16, new Rgba32(64, 128, 192, 255))) image.SaveAsPng(background);
        using (var image = new Image<Rgba32>(24, 16)) { image[0, 0] = new Rgba32(255, 255, 255, 255); image.SaveAsPng(walkmask); }
        Assert.True(CustomMapBuilderEntityCatalog.TryGetDefinition(IndoorRegionMetadata.EntityType, out var definition));
        var region = definition.CreateEntity(10f, 20f) with { XScale = 2f, YScale = 3f };
        var document = CustomMapBuilderDocument.CreateEmpty("indoor_map") with
        {
            BackgroundImagePath = background,
            WalkmaskImagePath = walkmask,
            Entities = [CustomMapBuilderEntity.Create("redspawn", 12, 18), CustomMapBuilderEntity.Create("bluespawn", 42, 18), region],
        };

        var png = Path.Combine(_root, "indoor_map.png");
        CustomMapPngExporter.Export(document.NormalizeForEditing(), png);
        var room = CustomMapPngImporter.Import(png)!.Room;

        var imported = Assert.Single(room.IndoorRegions);
        Assert.Equal(new IndoorRegionMarker(10f, 20f, 84f, 126f), imported);
        Assert.DoesNotContain(IndoorRegionMetadata.EntityType, room.UnsupportedEntities, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void IndoorRegionsScaleWithTheMap()
    {
        Assert.Equal(new IndoorRegionMarker(20f, 40f, 84f, 126f), new IndoorRegionMarker(10f, 20f, 42f, 63f).Scale(2f));
    }

    [Fact]
    public void IndoorRegionsRoofOverWeatherOnPlatformerMaps()
    {
        var mask = WeatherSkyMask.Build(
            [new LevelSolid(0f, 300f, 400f, 20f)],
            new WorldBounds(400f, 320f),
            [new IndoorRegionMarker(100f, 200f, 50f, 100f)]);

        Assert.Equal(300f, mask.GroundAt(20f));
        Assert.Equal(200f, mask.GroundAt(120f));
        Assert.Equal(300f, mask.GroundAt(200f));
    }

    [Fact]
    public void IndoorRegionsHideWeatherOnTopDownMaps()
    {
        var field = new RetroWeatherField();
        field.Configure(MapWeatherKind.Rain, MapWeatherIntensity.Heavy);
        var sink = new CollectingSink(new List<XnaRectangle>());
        var everywhere = field.Emit(ref sink, null, 0f, 0f, 400f, 300f, 400f, 300f, 7d, MapWeatherWind.Calm, 1f);
        Assert.True(everywhere > 0);

        var region = new IndoorRegionMarker(100f, 50f, 200f, 200f);
        sink.Rectangles.Clear();
        field.Emit(ref sink, null, 0f, 0f, 400f, 300f, 400f, 300f, 7d, MapWeatherWind.Calm, 1f, [region]);
        Assert.NotEmpty(sink.Rectangles);
        Assert.All(sink.Rectangles, rectangle => Assert.False(
            rectangle.Right > region.Left && rectangle.Left < region.Right
            && rectangle.Bottom > region.Top && rectangle.Top < region.Bottom,
            $"{rectangle} is inside the indoor region"));
    }

    [Theory]
    [InlineData("indoorRegion", GarrisonBuilderSelectionFilter.WeatherRegions)]
    [InlineData("killbox", GarrisonBuilderSelectionFilter.EffectBoxes)]
    [InlineData("spawnroom", GarrisonBuilderSelectionFilter.EffectBoxes)]
    [InlineData("redspawn", GarrisonBuilderSelectionFilter.PointEntities)]
    [InlineData("redintel", GarrisonBuilderSelectionFilter.PointEntities)]
    public void BuilderSelectionFilterClassifiesEntityTypes(string type, GarrisonBuilderSelectionFilter expected)
    {
        Assert.Equal(expected, Game1.ClassifyGarrisonBuilderEntityType(type));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private readonly struct CollectingSink : IWeatherPixelSink
    {
        public CollectingSink(List<XnaRectangle> rectangles)
        {
            Rectangles = rectangles;
        }

        public List<XnaRectangle> Rectangles { get; }

        public void Fill(int x, int y, int width, int height, XnaColor color)
        {
            Rectangles.Add(new XnaRectangle(x, y, width, height));
        }
    }
}
