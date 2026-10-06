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
    public void FirefliesParseNormalizeAndOnlyShowTheirOwnRows()
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["weather"] = "Fireflies",
            ["weatherFireflyColor"] = "#88ccff",
            ["weatherFireflyDensity"] = "250",
            ["weatherFireflyGlow"] = "nope",
        };

        var weather = MapWeatherMetadata.Parse(metadata);
        Assert.Equal(MapWeatherKind.Fireflies, weather.Kind);
        Assert.Equal(new MapLightingColor(0x88, 0xcc, 0xff), weather.ResolvedFireflyColor);
        Assert.Equal(100, weather.FireflyDensity);
        Assert.Equal(MapWeatherMetadata.DefaultFireflyGlow, weather.FireflyGlow);

        MapWeatherMetadata.Normalize(metadata);
        Assert.Equal("88ccff", metadata["weatherFireflyColor"]);
        Assert.Equal("100", metadata["weatherFireflyDensity"]);
        Assert.Equal(weather, MapWeatherMetadata.Parse(metadata));

        // Switching to other weather drops the firefly keys.
        metadata["weather"] = "rain";
        MapWeatherMetadata.Normalize(metadata);
        Assert.False(metadata.ContainsKey("weatherFireflyColor"));
        Assert.Equal(new MapWeather(MapWeatherKind.Rain), MapWeatherMetadata.Parse(metadata));

        Assert.True(MapWeatherMetadata.IsDetailKeyRelevant("weatherFireflyGlow", MapWeatherKind.Fireflies));
        Assert.False(MapWeatherMetadata.IsDetailKeyRelevant("weatherFireflyGlow", MapWeatherKind.Snow));
        Assert.False(MapWeatherMetadata.IsDetailKeyRelevant("weatherIntensity", MapWeatherKind.Fireflies));
        Assert.True(MapWeatherMetadata.IsDetailKeyRelevant("weatherWind", MapWeatherKind.Fireflies));
        Assert.True(ControlPointMapSettingsMetadata.IsEditableMapMetadataKey("weatherFireflyDensity"));
        Assert.Equal(MapWeatherMetadata.DefaultFireflyColor, new MapWeather(MapWeatherKind.Fireflies).ResolvedFireflyColor);
    }

    [Fact]
    public void FirefliesHoverInOpenAirAndScaleWithDensity()
    {
        // Floor at y = 300 across a 1000 x 400 map, with a solid block over x 400..600.
        var mask = WeatherSkyMask.Build(
            [new LevelSolid(0f, 300f, 1000f, 100f), new LevelSolid(400f, 0f, 200f, 300f)],
            new WorldBounds(1000f, 400f),
            null);
        var fireflies = new List<FireflyInstance>();
        var counts = new List<int>();
        foreach (var density in new[] { 0, 25, 100 })
        {
            var total = 0;
            for (var frame = 0; frame < 30; frame += 1)
            {
                FireflyField.Collect(fireflies, mask, 0f, 0f, 1000f, 400f, 1000f, 400f, 100d + (frame * 0.5d), MapWeatherWind.Calm, density, 1f, null);
                foreach (var firefly in fireflies)
                {
                    Assert.InRange(firefly.Brightness, 0f, 1f);
                    Assert.True(firefly.Y < 300f, "above the floor");
                    Assert.False(firefly.X > 401f && firefly.X < 599f, "never inside the block");
                }

                total += fireflies.Count;
            }

            counts.Add(total);
        }

        Assert.Equal(0, counts[0]);
        Assert.True(counts[2] > counts[1] && counts[1] > 0);
    }

    [Fact]
    public void SubtleMotesDriftSteadilyWithoutBlinking()
    {
        var mask = WeatherSkyMask.Build([new LevelSolid(0f, 300f, 1000f, 100f)], new WorldBounds(1000f, 400f), null);
        var before = new List<FireflyInstance>();
        var after = new List<FireflyInstance>();
        FireflyField.Collect(before, mask, 0f, 0f, 1000f, 400f, 1000f, 400f, 200d, MapWeatherWind.Calm, 60, 1f, null, MapFireflyStyle.Subtle);
        FireflyField.Collect(after, mask, 0f, 0f, 1000f, 400f, 1000f, 400f, 200.1d, MapWeatherWind.Calm, 60, 1f, null, MapFireflyStyle.Subtle);

        // Over a tenth of a second nothing appears or vanishes and nobody darts or flickers.
        Assert.NotEmpty(before);
        Assert.InRange(after.Count, before.Count - 2, before.Count + 2);
        var steady = 0;
        foreach (var mote in before)
        {
            Assert.True(mote.Y < 300f, "above the floor");
            foreach (var later in after)
            {
                if (MathF.Abs(later.X - mote.X) < 3f && MathF.Abs(later.Y - mote.Y) < 3f)
                {
                    Assert.InRange(later.Brightness, mote.Brightness - 0.05f, mote.Brightness + 0.05f);
                    steady += 1;
                    break;
                }
            }
        }

        Assert.True(steady >= before.Count - 2);
        Assert.Equal(MapFireflyStyle.Subtle, MapWeatherMetadata.ParseFireflyStyle("Subtle"));
        Assert.True(MapWeatherMetadata.TryCyclePropertyValue("weatherFireflyStyle", "fireflies", out var next));
        Assert.Equal("subtle", next);
    }

    [Fact]
    public void CyclesVisitEveryValueAndWrap()
    {
        Assert.Equal("rain", MapWeatherMetadata.CycleKindPropertyValue(null));
        Assert.Equal("snow", MapWeatherMetadata.CycleKindPropertyValue("rain"));
        Assert.Equal("leaves", MapWeatherMetadata.CycleKindPropertyValue("SNOW"));
        Assert.Equal("fireflies", MapWeatherMetadata.CycleKindPropertyValue("leaves"));
        Assert.Equal("none", MapWeatherMetadata.CycleKindPropertyValue("fireflies"));
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
    public void RainColorParsesNormalizesAndOnlyShowsForRain()
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["weather"] = "rain",
            ["weatherRainColor"] = "#C0302A",
        };

        var weather = MapWeatherMetadata.Parse(metadata);
        Assert.Equal(new MapLightingColor(0xC0, 0x30, 0x2A), weather.RainColor);
        Assert.Equal(weather.RainColor, weather.ResolvedRainColor);
        MapWeatherMetadata.Normalize(metadata);
        Assert.Equal("c0302a", metadata["weatherRainColor"]);

        // The default colour (or junk) is not stored, so plain rain stays plain.
        metadata["weatherRainColor"] = MapWeatherMetadata.DefaultRainColor.ToHex();
        Assert.Equal(new MapWeather(MapWeatherKind.Rain), MapWeatherMetadata.Parse(metadata));
        MapWeatherMetadata.Normalize(metadata);
        Assert.False(metadata.ContainsKey("weatherRainColor"));
        metadata["weatherRainColor"] = "purple";
        Assert.Equal(MapWeatherMetadata.DefaultRainColor, MapWeatherMetadata.Parse(metadata).ResolvedRainColor);

        // Other weather drops the key and ignores it.
        metadata["weather"] = "snow";
        metadata["weatherRainColor"] = "ff0000";
        Assert.Equal(new MapWeather(MapWeatherKind.Snow), MapWeatherMetadata.Parse(metadata));
        MapWeatherMetadata.Normalize(metadata);
        Assert.False(metadata.ContainsKey("weatherRainColor"));

        Assert.True(MapWeatherMetadata.IsDetailKeyRelevant("weatherRainColor", MapWeatherKind.Rain));
        Assert.False(MapWeatherMetadata.IsDetailKeyRelevant("weatherRainColor", MapWeatherKind.Snow));
        Assert.False(MapWeatherMetadata.IsDetailKeyRelevant("weatherRainColor", MapWeatherKind.Fireflies));
        Assert.True(ControlPointMapSettingsMetadata.IsEditableMapMetadataKey("weatherRainColor"));
        Assert.Equal("Rain colour (hex): c0302a", MapWeatherMetadata.TryFormatRowLabel("weatherRainColor", "c0302a"));
    }

    [Fact]
    public void CustomRainColorTintsEveryDrop()
    {
        var mask = WeatherSkyMask.Build([new LevelSolid(0f, 380f, 1000f, 20f)], new WorldBounds(1000f, 400f));
        var field = new RetroWeatherField();
        field.Configure(MapWeatherKind.Rain, MapWeatherIntensity.Heavy);
        field.SetRainColor(new XnaColor(200, 20, 20));
        var sink = new ColorSink(new List<XnaColor>());
        for (var frame = 0; frame < 30; frame += 1)
        {
            field.Emit(ref sink, mask, 0f, 0f, 1000f, 400f, 1000f, 400f, frame / 30d, MapWeatherWind.Calm, 1f);
        }

        Assert.NotEmpty(sink.Colors);
        Assert.All(sink.Colors, color => Assert.True(color.R >= color.G * 4 && color.R >= color.B * 4, $"{color} is not red"));

        field.SetRainColor(null);
        sink.Colors.Clear();
        field.Emit(ref sink, mask, 0f, 0f, 1000f, 400f, 1000f, 400f, 1d, MapWeatherWind.Calm, 1f);
        Assert.All(sink.Colors, color => Assert.True(color.B >= color.R, $"{color} is not the default blue"));
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

    private readonly struct ColorSink : IWeatherPixelSink
    {
        public ColorSink(List<XnaColor> colors)
        {
            Colors = colors;
        }

        public List<XnaColor> Colors { get; }

        public void Fill(int x, int y, int width, int height, XnaColor color)
        {
            Colors.Add(color);
        }
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
