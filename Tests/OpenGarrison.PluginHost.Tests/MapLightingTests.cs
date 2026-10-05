using OpenGarrison.Client;
using OpenGarrison.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class MapLightingTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "map-lighting-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void LightingIsOffUnlessAPresetIsChosen()
    {
        Assert.False(MapLightingMetadata.Parse(null).IsActive);
        Assert.False(MapLightingMetadata.Parse(new Dictionary<string, string> { ["lightGlow"] = "80" }).IsActive);
        Assert.False(MapLightingMetadata.Parse(new Dictionary<string, string> { ["lighting"] = "none" }).IsActive);
    }

    [Fact]
    public void ExplicitValuesOverridePresetDefaults()
    {
        var lighting = MapLightingMetadata.Parse(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["lighting"] = "night",
            ["lightGlow"] = "90",
            ["lightSkyTint"] = "#102030",
            ["lightBrightness"] = "250",
            ["lightStyle"] = "retro",
        });

        var night = MapLightingMetadata.GetPresetDefaults(MapLightingPreset.Night);
        Assert.Equal(MapLightingPreset.Night, lighting.Preset);
        Assert.Equal(90, lighting.Glow);
        Assert.Equal(new MapLightingColor(0x10, 0x20, 0x30), lighting.SkyTint);
        Assert.Equal(MapLightingMetadata.MaxScale, lighting.Brightness); // clamped to 200
        Assert.True(lighting.Banded);
        Assert.Equal(night.GroundTint, lighting.GroundTint);
        Assert.Equal(night.EffectLights, lighting.EffectLights);
        Assert.Equal(100, lighting.PlayerRange);
        Assert.Equal(100, lighting.EffectRange);
    }

    [Fact]
    public void RangesParseClampAndRoundTrip()
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["lighting"] = "dusk",
            ["lightPlayerRange"] = "40",
            ["lightEffectRange"] = "999",
            ["lightGlow"] = "150",
        };

        var lighting = MapLightingMetadata.Parse(metadata);
        Assert.Equal(40, lighting.PlayerRange);
        Assert.Equal(MapLightingMetadata.MaxScale, lighting.EffectRange);
        Assert.Equal(100, lighting.Glow); // plain percentages still stop at 100

        var written = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        MapLightingMetadata.Write(written, lighting with { Brightness = 160 });
        Assert.Equal("160", written["lightBrightness"]);
        Assert.Equal("40", written["lightPlayerRange"]);
        Assert.Equal(lighting with { Brightness = 160 }, MapLightingMetadata.Parse(written));
    }

    [Fact]
    public void WriteRoundTripsAndClearsWhenOff()
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["background"] = "ffffff" };
        var lighting = MapLightingMetadata.GetPresetDefaults(MapLightingPreset.BloodMoon) with { Pulse = 61, Banded = true };
        MapLightingMetadata.Write(metadata, lighting);
        Assert.Equal(lighting, MapLightingMetadata.Parse(metadata));
        Assert.Equal("bloodMoon", metadata["lighting"]);

        MapLightingMetadata.Write(metadata, MapLighting.None);
        Assert.Equal(new[] { "background" }, metadata.Keys.ToArray());
    }

    [Theory]
    [InlineData("dusk", MapLightingPreset.Dusk)]
    [InlineData("BloodMoon", MapLightingPreset.BloodMoon)]
    [InlineData("blood moon", MapLightingPreset.BloodMoon)]
    [InlineData("custom", MapLightingPreset.Custom)]
    [InlineData("disco", MapLightingPreset.None)]
    public void PresetNamesParse(string value, MapLightingPreset expected)
    {
        Assert.Equal(expected, MapLightingMetadata.ParsePreset(value));
    }

    [Fact]
    public void LightPropertiesParseWithSafeFallbacks()
    {
        var light = MapLightMetadata.FromProperties(10f, 20f, new Dictionary<string, string>
        {
            ["color"] = "ff0000",
            ["radius"] = "99999",
            ["intensity"] = "-5",
            ["flicker"] = "pulse",
        });

        Assert.Equal(new MapLightingColor(255, 0, 0), light.Color);
        Assert.Equal(MapLightMetadata.MaxRadius, light.Radius);
        Assert.Equal(0, light.Intensity);
        Assert.Equal(MapLightFlicker.Pulse, light.Flicker);
        Assert.Equal("flicker", MapLightMetadata.CycleFlickerValue("steady"));

        var defaults = MapLightMetadata.FromProperties(0f, 0f, new Dictionary<string, string> { ["color"] = "nope" });
        Assert.Equal(MapLightMetadata.DefaultColor, defaults.Color);
        Assert.Equal(MapLightMetadata.DefaultRadius, defaults.Radius);
    }

    [Fact]
    public void LightingAndLightsReachTheRuntimeRoom()
    {
        Directory.CreateDirectory(_root);
        var background = Path.Combine(_root, "bg.png");
        var walkmask = Path.Combine(_root, "wm.png");
        using (var image = new Image<Rgba32>(24, 16, new Rgba32(64, 128, 192, 255))) image.SaveAsPng(background);
        using (var image = new Image<Rgba32>(24, 16)) { image[0, 0] = new Rgba32(255, 255, 255, 255); image.SaveAsPng(walkmask); }
        var metadata = new Dictionary<string, string>(CustomMapBuilderDocument.CreateEmpty("lit_map").Metadata, StringComparer.OrdinalIgnoreCase);
        var lighting = MapLightingMetadata.GetPresetDefaults(MapLightingPreset.Moonlit) with { Glow = 12 };
        MapLightingMetadata.Write(metadata, lighting);
        Assert.True(CustomMapBuilderEntityCatalog.TryGetDefinition(MapLightMetadata.EntityType, out var definition));
        var document = CustomMapBuilderDocument.CreateEmpty("lit_map") with
        {
            BackgroundImagePath = background,
            WalkmaskImagePath = walkmask,
            Metadata = metadata,
            Entities = [CustomMapBuilderEntity.Create("redspawn", 12, 18), CustomMapBuilderEntity.Create("bluespawn", 42, 18), definition.CreateEntity(30f, 40f)],
        };

        var png = Path.Combine(_root, "lit_map.png");
        CustomMapPngExporter.Export(document.NormalizeForEditing(), png);
        var room = CustomMapPngImporter.Import(png)!.Room;
        Assert.Equal(lighting, room.Lighting);
        var light = Assert.Single(room.MapLights);
        Assert.Equal(30f, light.X);
        Assert.Equal(40f, light.Y);
        Assert.Equal(MapLightMetadata.DefaultRadius, light.Radius);
        Assert.DoesNotContain(MapLightMetadata.EntityType, room.UnsupportedEntities, StringComparer.OrdinalIgnoreCase);

        var reopened = CustomMapBuilderPngImporter.Import(png)!;
        Assert.Equal(lighting, MapLightingMetadata.Parse(reopened.Metadata));
    }

    [Fact]
    public void StormHazePassesUnderThinLedgesButNotIndoors()
    {
        var mask = WeatherSkyMask.Build(
            [
                new LevelSolid(0f, 200f, 100f, 6f),   // thin ledge over x 0..100
                new LevelSolid(0f, 300f, 400f, 100f), // floor
                new LevelSolid(200f, 120f, 100f, 60f), // thick block over x 200..300
            ],
            new WorldBounds(400f, 400f),
            [new IndoorRegionMarker(320f, 150f, 60f, 150f)]);

        Assert.Equal(200f, mask.GroundAt(50f));
        Assert.Equal(300f, mask.HazeBottomAt(50f));   // under the ledge
        Assert.Equal(120f, mask.HazeBottomAt(250f));  // thick block stops it
        Assert.Equal(150f, mask.HazeBottomAt(350f));  // indoor region stops it
        Assert.Equal(300f, mask.HazeBottomAt(150f));
    }

    [Fact]
    public void WeatherKindsAndStormParseAndCycle()
    {
        Assert.Equal(MapWeatherKind.Leaves, MapWeatherMetadata.ParseKind("leaves"));
        Assert.Equal(MapWeatherKind.Leaves, MapWeatherMetadata.ParseKind("Autumn"));
        Assert.Equal(MapWeatherIntensity.Storm, MapWeatherMetadata.ParseIntensity("STORM"));
        Assert.Equal("Storm", MapWeatherMetadata.GetIntensityDisplayLabel("storm"));
        Assert.Equal("Autumn leaves", MapWeatherMetadata.GetKindDisplayLabel("leaves"));
    }

    [Fact]
    public void StormGustAndThunderstormLightningStayBounded()
    {
        var flashes = 0;
        for (var sample = 0; sample < 60 * 300; sample += 1)
        {
            var time = sample / 60d;
            Assert.InRange(RetroWeatherField.GetStormGust(time), 0f, 1f);
            if (RetroWeatherField.GetLightningFlash(time, storm: true) > 0f)
            {
                flashes += 1;
            }
        }

        Assert.InRange(flashes, 1, 60 * 300 / 20);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
