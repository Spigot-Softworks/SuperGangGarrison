using OpenGarrison.Client;
using OpenGarrison.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;
using XnaColor = Microsoft.Xna.Framework.Color;
using XnaRectangle = Microsoft.Xna.Framework.Rectangle;

namespace OpenGarrison.PluginHost.Tests;

public sealed class MapWeatherTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "map-weather-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void MissingOrUnknownMetadataMeansNoWeather()
    {
        Assert.Equal(MapWeather.None, MapWeatherMetadata.Parse(null));
        Assert.Equal(MapWeather.None, MapWeatherMetadata.Parse(new Dictionary<string, string>()));
        var parsed = MapWeatherMetadata.Parse(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["weather"] = "hail",
            ["weatherIntensity"] = "extreme",
            ["weatherWind"] = "up",
        });
        Assert.False(parsed.IsActive);
        Assert.Equal(MapWeather.None, parsed);
        Assert.Equal(MapWeatherIntensity.Medium, MapWeatherMetadata.ParseIntensity("extreme"));
        Assert.Equal(MapWeatherWind.Calm, MapWeatherMetadata.ParseWind("up"));
    }

    [Fact]
    public void CyclesVisitEveryValueAndWrap()
    {
        Assert.Equal("rain", MapWeatherMetadata.CycleKindPropertyValue(null));
        Assert.Equal("snow", MapWeatherMetadata.CycleKindPropertyValue("rain"));
        Assert.Equal("leaves", MapWeatherMetadata.CycleKindPropertyValue("SNOW"));
        Assert.Equal("none", MapWeatherMetadata.CycleKindPropertyValue("leaves"));
        Assert.Equal("heavy", MapWeatherMetadata.CycleIntensityPropertyValue("medium"));
        Assert.Equal("storm", MapWeatherMetadata.CycleIntensityPropertyValue("heavy"));
        Assert.Equal("light", MapWeatherMetadata.CycleIntensityPropertyValue("storm"));
        Assert.Equal("left", MapWeatherMetadata.CycleWindPropertyValue("calm"));
        Assert.True(MapWeatherMetadata.TryCyclePropertyValue("weatherWind", "right", out var wind));
        Assert.Equal("calm", wind);
        Assert.False(MapWeatherMetadata.TryCyclePropertyValue("background", "ffffff", out _));
    }

    [Fact]
    public void NormalizeDropsAllKeysWhenWeatherIsNone()
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["weather"] = "none",
            ["weatherIntensity"] = "heavy",
            ["weatherWind"] = "left",
            ["background"] = "ffffff",
        };
        MapWeatherMetadata.Normalize(metadata);
        Assert.Equal(new[] { "background" }, metadata.Keys.ToArray());

        metadata["weather"] = "Snow";
        MapWeatherMetadata.Normalize(metadata);
        Assert.Equal("snow", metadata["weather"]);
        Assert.Equal("medium", metadata["weatherIntensity"]);
        Assert.Equal("calm", metadata["weatherWind"]);
    }

    [Fact]
    public void WeatherKeysAreEditableMapProperties()
    {
        Assert.True(ControlPointMapSettingsMetadata.IsEditableMapMetadataKey("weather"));
        Assert.True(ControlPointMapSettingsMetadata.IsEditableMapMetadataKey("weatherIntensity"));
        Assert.True(ControlPointMapSettingsMetadata.IsEditableMapMetadataKey("weatherWind"));
    }

    [Fact]
    public void WeatherSurvivesPngAndPackageRoundTripsToRuntime()
    {
        Directory.CreateDirectory(_root);
        var background = Path.Combine(_root, "bg.png");
        var walkmask = Path.Combine(_root, "wm.png");
        using (var image = new Image<Rgba32>(24, 16, new Rgba32(64, 128, 192, 255))) image.SaveAsPng(background);
        using (var image = new Image<Rgba32>(24, 16)) { image[0, 0] = new Rgba32(255, 255, 255, 255); image.SaveAsPng(walkmask); }
        var document = CustomMapBuilderDocument.CreateEmpty("weather_map") with
        {
            BackgroundImagePath = background,
            WalkmaskImagePath = walkmask,
            Metadata = new Dictionary<string, string>(CustomMapBuilderDocument.CreateEmpty("weather_map").Metadata, StringComparer.OrdinalIgnoreCase)
            {
                ["weather"] = "snow",
                ["weatherIntensity"] = "heavy",
                ["weatherWind"] = "right",
            },
            Entities = [CustomMapBuilderEntity.Create("redspawn", 12, 18), CustomMapBuilderEntity.Create("bluespawn", 42, 18)],
        };
        var expected = new MapWeather(MapWeatherKind.Snow, MapWeatherIntensity.Heavy, MapWeatherWind.Right);

        var png = Path.Combine(_root, "weather_map.png");
        CustomMapPngExporter.Export(document.NormalizeForEditing(), png);
        Assert.Equal(expected, CustomMapPngImporter.Import(png)!.Room.Weather);

        var reopened = CustomMapBuilderPngImporter.Import(png)!;
        Assert.Equal(expected, MapWeatherMetadata.Parse(reopened.Metadata));

        var package = Path.Combine(_root, "weather_map.json");
        CustomMapPackageExporter.Export(reopened, package);
        Assert.Equal(expected, MapWeatherMetadata.Parse(CustomMapPackageImporter.ImportDocument(package)!.Metadata));
        Assert.Equal(expected, CustomMapPackageImporter.Import(package)!.Room.Weather);
    }

    [Fact]
    public void SkyMaskStopsAtFirstSurfaceAndSkipsTopBorderCeilings()
    {
        var mask = WeatherSkyMask.Build(
            [
                new LevelSolid(0f, 0f, 20f, 10f),     // ceiling border over x 0..20
                new LevelSolid(0f, 80f, 100f, 20f),   // floor
                new LevelSolid(40f, 30f, 20f, 5f),    // ledge over x 40..60
            ],
            new WorldBounds(100f, 100f));

        Assert.Equal(80f, mask.GroundAt(5f));    // sky starts under the border, falls to the floor
        Assert.Equal(80f, mask.GroundAt(30f));
        Assert.Equal(30f, mask.GroundAt(50f));   // ledge shelters the floor below it
        Assert.Equal(80f, mask.GroundAt(70f));
        Assert.Equal(80f, mask.GroundAt(-5f));   // out-of-range lookups clamp
        Assert.Equal(80f, mask.GroundAt(500f));
    }

    [Fact]
    public void SkyMaskTreatsStackedTopStripsAsOneCeiling()
    {
        // Walkmask imports produce one strip per pixel row.
        var mask = WeatherSkyMask.Build(
            [
                new LevelSolid(0f, 0f, 50f, 6f),
                new LevelSolid(0f, 6f, 50f, 6f),
                new LevelSolid(0f, 12f, 50f, 6f),
                new LevelSolid(0f, 90f, 50f, 6f),
                new LevelSolid(0f, 96f, 50f, 6f),
            ],
            new WorldBounds(50f, 120f));

        Assert.Equal(18f, mask.SkyStartAt(10f));
        Assert.Equal(90f, mask.GroundAt(10f));
    }

    [Theory]
    [InlineData(MapWeatherKind.Rain, MapWeatherIntensity.Heavy)]
    [InlineData(MapWeatherKind.Snow, MapWeatherIntensity.Heavy)]
    [InlineData(MapWeatherKind.Leaves, MapWeatherIntensity.Heavy)]
    [InlineData(MapWeatherKind.Rain, MapWeatherIntensity.Storm)]
    [InlineData(MapWeatherKind.Snow, MapWeatherIntensity.Storm)]
    [InlineData(MapWeatherKind.Leaves, MapWeatherIntensity.Storm)]
    public void WeatherNeverDrawsBelowTheSkySurface(MapWeatherKind kind, MapWeatherIntensity intensity)
    {
        const float Floor = 300f;
        var mask = WeatherSkyMask.Build([new LevelSolid(0f, Floor, 2000f, 100f)], new WorldBounds(2000f, 400f));
        var field = new RetroWeatherField();
        field.Configure(kind, intensity);
        var sink = new CollectingSink(new List<XnaRectangle>());

        for (var frame = 0; frame < 120; frame += 1)
        {
            field.Emit(ref sink, mask, 0f, 0f, 960f, 400f, 2000f, 400f, 1000d + (frame / 60d), MapWeatherWind.Right, 1f);
        }

        Assert.NotEmpty(sink.Rectangles);
        Assert.All(sink.Rectangles, rectangle => Assert.True(rectangle.Bottom <= Floor + 0.5f, $"{rectangle} crosses the floor"));
        Assert.Contains(sink.Rectangles, rectangle => rectangle.Bottom >= Floor - 2);
    }

    [Fact]
    public void ShelteredAreasStayDry()
    {
        // A roof spanning the whole map at y=100; the room underneath must stay dry.
        var mask = WeatherSkyMask.Build(
            [new LevelSolid(0f, 100f, 1000f, 10f), new LevelSolid(0f, 390f, 1000f, 10f)],
            new WorldBounds(1000f, 400f));
        var field = new RetroWeatherField();
        field.Configure(MapWeatherKind.Rain, MapWeatherIntensity.Heavy);
        var sink = new CollectingSink(new List<XnaRectangle>());
        for (var frame = 0; frame < 60; frame += 1)
        {
            field.Emit(ref sink, mask, 0f, 0f, 1000f, 400f, 1000f, 400f, frame / 30d, MapWeatherWind.Calm, 1f);
        }

        Assert.NotEmpty(sink.Rectangles);
        Assert.All(sink.Rectangles, rectangle => Assert.True(rectangle.Bottom <= 100, $"{rectangle} is under the roof"));
    }

    [Fact]
    public void DensityScaleAndNoneControlWork()
    {
        var mask = WeatherSkyMask.Build([], new WorldBounds(4000f, 4000f));
        var field = new RetroWeatherField();
        var sink = new CollectingSink(new List<XnaRectangle>());

        field.Configure(MapWeatherKind.None, MapWeatherIntensity.Heavy);
        Assert.Equal(0, field.Emit(ref sink, mask, 0f, 0f, 960f, 540f, 4000f, 4000f, 5d, MapWeatherWind.Calm, 1f));

        field.Configure(MapWeatherKind.Rain, MapWeatherIntensity.Medium);
        var full = field.Emit(ref sink, mask, 500f, 500f, 960f, 540f, 4000f, 4000f, 5d, MapWeatherWind.Calm, 1f);
        var half = field.Emit(ref sink, mask, 500f, 500f, 960f, 540f, 4000f, 4000f, 5d, MapWeatherWind.Calm, 0.5f);
        Assert.InRange((double)half, full * 0.3, full * 0.7);
        Assert.Equal(0, field.Emit(ref sink, mask, 500f, 500f, 960f, 540f, 4000f, 4000f, 5d, MapWeatherWind.Calm, 0f));

        // Outside the map nothing is drawn.
        Assert.Equal(0, field.Emit(ref sink, mask, -2000f, -2000f, 960f, 540f, 4000f, 4000f, 5d, MapWeatherWind.Calm, 1f));
    }

    [Fact]
    public void HugeViewsAreCappedPerFrame()
    {
        var mask = WeatherSkyMask.Build([], new WorldBounds(20000f, 20000f));
        var field = new RetroWeatherField();
        field.Configure(MapWeatherKind.Rain, MapWeatherIntensity.Heavy);
        var sink = new CollectingSink(new List<XnaRectangle>());
        var emitted = field.Emit(ref sink, mask, 0f, 0f, 20000f, 20000f, 20000f, 20000f, 3d, MapWeatherWind.Left, 1f);
        Assert.True(emitted <= RetroWeatherField.MaxRectanglesPerFrame);
    }

    [Fact]
    public void LightningIsRareAndBounded()
    {
        var flashingSamples = 0;
        for (var sample = 0; sample < 60 * 600; sample += 1)
        {
            var flash = RetroWeatherField.GetLightningFlash(sample / 60d);
            Assert.InRange(flash, 0f, 1.7f);
            if (flash > 0f)
            {
                flashingSamples += 1;
            }
        }

        // Ten minutes at 60 fps: some flashes, but well under 2% of frames.
        Assert.InRange(flashingSamples, 1, 60 * 600 / 50);
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
