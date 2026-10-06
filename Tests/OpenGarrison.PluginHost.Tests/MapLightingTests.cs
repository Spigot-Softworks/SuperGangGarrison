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
    public void LightFalloffParsesCyclesAndFollowsTheMapByDefault()
    {
        Assert.Equal(MapLightFalloff.Map, MapLightMetadata.FromProperties(0f, 0f, new Dictionary<string, string>()).Falloff);
        Assert.Equal(MapLightFalloff.Retro, MapLightMetadata.FromProperties(0f, 0f, new Dictionary<string, string> { ["falloff"] = "Retro" }).Falloff);
        Assert.Equal(MapLightFalloff.Smooth, MapLightMetadata.ParseFalloff("smooth"));
        Assert.Equal(MapLightFalloff.Map, MapLightMetadata.ParseFalloff("sideways"));
        Assert.Equal("smooth", MapLightMetadata.CycleFalloffValue("map"));
        Assert.Equal("retro", MapLightMetadata.CycleFalloffValue("smooth"));
        Assert.Equal("map", MapLightMetadata.CycleFalloffValue("retro"));

        Assert.True(MapLightMetadata.UsesRetroBands(MapLightFalloff.Map, mapBanded: true));
        Assert.False(MapLightMetadata.UsesRetroBands(MapLightFalloff.Map, mapBanded: false));
        Assert.True(MapLightMetadata.UsesRetroBands(MapLightFalloff.Retro, mapBanded: false));
        Assert.False(MapLightMetadata.UsesRetroBands(MapLightFalloff.Smooth, mapBanded: true));
    }

    [Fact]
    public void SpotlightDirectionAndSpreadParseAndClamp()
    {
        var round = MapLightMetadata.FromProperties(0f, 0f, new Dictionary<string, string>());
        Assert.Equal(MapLightMetadata.FullSpread, round.Spread);
        Assert.False(MapLightMetadata.IsDirectional(round.Spread));

        var spot = MapLightMetadata.FromProperties(0f, 0f, new Dictionary<string, string>
        {
            ["direction"] = "-90",
            ["spread"] = "2",
        });
        Assert.Equal(270f, spot.Direction);
        Assert.Equal(MapLightMetadata.MinSpread, spot.Spread);
        Assert.True(MapLightMetadata.IsDirectional(spot.Spread));

        var down = MapLightMetadata.GetDirectionVector(270f);
        Assert.InRange(down.X, -0.001f, 0.001f);
        Assert.InRange(down.Y, 0.999f, 1.001f); // screen y points down
        Assert.Equal(MapLightMetadata.FullSpread, MapLightMetadata.FromProperties(0f, 0f, new Dictionary<string, string> { ["spread"] = "nope" }).Spread);
    }

    [Fact]
    public void RimAndShadowSettingsRoundTripAndClamp()
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["lighting"] = "night",
            ["lightRim"] = "70",
            ["lightRimWidth"] = "9",
            ["lightRimColor"] = "light",
            ["lightRimSource"] = "sky",
            ["lightShadow"] = "80",
            ["lightShadowDistance"] = "0",
        };

        var lighting = MapLightingMetadata.Parse(metadata);
        Assert.Equal(70, lighting.RimLight);
        Assert.Equal(MapLightingMetadata.MaxRimWidth, lighting.RimWidth);
        Assert.Equal(MapRimColorMode.Light, lighting.RimColorMode);
        Assert.True(lighting.RimFromSky);
        Assert.Equal(80, lighting.CastShadow);
        Assert.Equal(MapLightingMetadata.MinShadowDistance, lighting.ShadowDistance);
        Assert.True(lighting.HasCharacterLighting);

        var written = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        MapLightingMetadata.Write(written, lighting);
        Assert.Equal(lighting, MapLightingMetadata.Parse(written));

        var plain = MapLightingMetadata.GetPresetDefaults(MapLightingPreset.Dusk);
        Assert.False(plain.HasCharacterLighting); // off unless the author turns it on
        Assert.Equal(MapRimColorMode.Light, plain.RimColorMode);
        Assert.False(plain.HasBodyAdjustment);

        Assert.Equal(MapRimColorMode.Additive, MapLightingMetadata.ParseRimColorMode("add"));
        Assert.Equal(MapRimColorMode.Light, MapLightingMetadata.ParseRimColorMode("nonsense"));
        Assert.Equal(MapRimColorMode.Team, MapLightingMetadata.ParseRimColorMode("team"));
        Assert.Equal(MapRimColorMode.Team, MapLightingMetadata.ParseRimColorMode("sprite")); // older maps
        Assert.Equal(MapRimColorMode.Additive, MapLightingMetadata.NextRimColorMode(MapRimColorMode.Light));
        Assert.Equal(MapRimColorMode.Team, MapLightingMetadata.NextRimColorMode(MapRimColorMode.Additive));
        Assert.Equal(MapRimColorMode.Custom, MapLightingMetadata.NextRimColorMode(MapRimColorMode.Team));
        Assert.Equal(MapRimColorMode.Light, MapLightingMetadata.NextRimColorMode(MapRimColorMode.Custom));
        Assert.Null(lighting.RimCustomColor);
        Assert.False(written.ContainsKey("lightRimCustomColor")); // only stored once picked
        Assert.Equal(MapLightingMetadata.DefaultRimCustomColor, lighting.ResolvedRimCustomColor);

        var custom = lighting with { RimColorMode = MapRimColorMode.Custom, RimCustomColor = new MapLightingColor(0, 0, 0) };
        var customMetadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        MapLightingMetadata.Write(customMetadata, custom);
        Assert.Equal("custom", customMetadata["lightRimColor"]);
        Assert.Equal("000000", customMetadata["lightRimCustomColor"]); // black is a valid pick
        Assert.Equal(custom, MapLightingMetadata.Parse(customMetadata));
        var additive = lighting with { RimColorMode = MapRimColorMode.Additive };
        var additiveMetadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        MapLightingMetadata.Write(additiveMetadata, additive);
        Assert.Equal("add", additiveMetadata["lightRimColor"]);
        Assert.Equal(additive, MapLightingMetadata.Parse(additiveMetadata));
    }

    [Fact]
    public void RimSourceSkyAmountAndUberRimRoundTrip()
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["lighting"] = "night",
            ["lightRimSource"] = "both",
            ["lightRimSky"] = "120",
            ["lightUberRim"] = "on",
        };

        var lighting = MapLightingMetadata.Parse(metadata);
        Assert.Equal(MapRimLightSource.Both, lighting.RimSource);
        Assert.False(lighting.RimFromSky);
        Assert.Equal(100, lighting.SkyRim);
        Assert.True(lighting.UberRim);
        Assert.True(lighting.HasCharacterLighting); // the uber rim alone turns it on

        var written = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        MapLightingMetadata.Write(written, lighting);
        Assert.Equal("both", written["lightRimSource"]);
        Assert.Equal("on", written["lightUberRim"]);
        Assert.Equal(lighting, MapLightingMetadata.Parse(written));

        var plain = MapLightingMetadata.GetPresetDefaults(MapLightingPreset.Night);
        Assert.Equal(MapRimLightSource.Lights, plain.RimSource);
        Assert.False(plain.UberRim);
        Assert.Equal(35, plain.SkyRim);
        Assert.Equal(MapRimLightSource.Lights, MapLightingMetadata.ParseRimSource("lights"));
        Assert.Equal(MapRimLightSource.Lights, MapLightingMetadata.ParseRimSource("nonsense"));
        Assert.Equal(MapRimLightSource.Both, MapLightingMetadata.NextRimSource(MapRimLightSource.Lights));
        Assert.Equal(MapRimLightSource.Sky, MapLightingMetadata.NextRimSource(MapRimLightSource.Both));
        Assert.Equal(MapRimLightSource.Lights, MapLightingMetadata.NextRimSource(MapRimLightSource.Sky));

        // Sky reach: off by default, stored, and capped so some fade always remains.
        Assert.Equal(0, plain.SkyReach);
        var reached = MapLightingMetadata.Parse(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["lighting"] = "night",
            ["lightSkyReach"] = "100",
        });
        Assert.Equal(MapLightingMetadata.MaxSkyReach, reached.SkyReach);
        var reachWritten = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        MapLightingMetadata.Write(reachWritten, reached with { SkyReach = 60 });
        Assert.Equal("60", reachWritten["lightSkyReach"]);
        Assert.Equal(60, MapLightingMetadata.Parse(reachWritten).SkyReach);
    }

    [Fact]
    public void RimBlendAndOpacityRoundTrip()
    {
        var plain = MapLightingMetadata.GetPresetDefaults(MapLightingPreset.Night);
        Assert.Equal(MapRimBlendMode.Normal, plain.RimBlend); // today's flat colour
        Assert.Equal(100, plain.RimOpacity);

        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["lighting"] = "night",
            ["lightRimBlend"] = "Screen",
            ["lightRimOpacity"] = "40",
        };
        var lighting = MapLightingMetadata.Parse(metadata);
        Assert.Equal(MapRimBlendMode.Screen, lighting.RimBlend);
        Assert.Equal(40, lighting.RimOpacity);

        var written = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        MapLightingMetadata.Write(written, lighting);
        Assert.Equal("screen", written["lightRimBlend"]);
        Assert.Equal(lighting, MapLightingMetadata.Parse(written));

        Assert.Equal(MapRimBlendMode.Normal, MapLightingMetadata.ParseRimBlend("nonsense"));
        Assert.Equal(MapRimBlendMode.ColorDodge, MapLightingMetadata.ParseRimBlend("color dodge"));
        Assert.Equal("colordodge", MapLightingMetadata.ToRimBlendValue(MapRimBlendMode.ColorDodge));
        Assert.Equal(MapRimBlendMode.ColorDodge, MapLightingMetadata.NextRimBlend(MapRimBlendMode.Screen));
        Assert.Equal(MapRimBlendMode.Add, MapLightingMetadata.NextRimBlend(MapRimBlendMode.Normal));
        Assert.Equal(MapRimBlendMode.Normal, MapLightingMetadata.NextRimBlend(MapRimBlendMode.Overlay));
    }

    [Fact]
    public void HalfRimWidthSavesAsPointFive()
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["lighting"] = "night",
            ["lightRimWidth"] = "0.5",
        };

        var lighting = MapLightingMetadata.Parse(metadata);
        Assert.Equal(MapLightingMetadata.HalfRimWidth, lighting.RimWidth);
        var written = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        MapLightingMetadata.Write(written, lighting);
        Assert.Equal("0.5", written["lightRimWidth"]);
        Assert.Equal(lighting, MapLightingMetadata.Parse(written));

        Assert.Equal("2", MapLightingMetadata.FormatRimWidth(2));
        metadata["lightRimWidth"] = "2";
        Assert.Equal(2, MapLightingMetadata.Parse(metadata).RimWidth);
        metadata["lightRimWidth"] = "junk";
        Assert.Equal(1, MapLightingMetadata.Parse(metadata).RimWidth); // preset default
    }

    [Fact]
    public void RimFacingLimitsWidenWithWrap()
    {
        var (cardinal, diagonal) = Game1.GetRimFacingLimits(0.25f);
        Assert.True(diagonal > cardinal); // diagonals are stricter
        Assert.True(Game1.GetRimFacingLimits(0.4f).Cardinal < cardinal);
        Assert.True(Game1.GetRimFacingLimits(0.8f).Cardinal < Game1.GetRimFacingLimits(0.4f).Cardinal);
    }

    [Fact]
    public void ArtPixelScaleFollowsUpscaledSprites()
    {
        // A 6x4 image whose art pixels are 2x2 blocks (like the stock characters).
        var red = new Microsoft.Xna.Framework.Color(200, 30, 30, 255);
        var clear = new Microsoft.Xna.Framework.Color(0, 0, 0, 0);
        var doubled = new Microsoft.Xna.Framework.Color[6 * 4];
        for (var y = 0; y < 4; y += 1)
        {
            for (var x = 0; x < 6; x += 1)
            {
                doubled[(y * 6) + x] = ((x / 2) + (y / 2)) % 2 == 0 ? red : clear;
            }
        }

        Assert.Equal(2, Game1.MeasureArtPixelScale(doubled, 6, 4));

        // Break one block: native-resolution art.
        doubled[1] = clear;
        Assert.Equal(1, Game1.MeasureArtPixelScale(doubled, 6, 4));

        // A blank frame tells nothing, so it stays at 1.
        Assert.Equal(1, Game1.MeasureArtPixelScale(new Microsoft.Xna.Framework.Color[16], 4, 4));
    }

    [Fact]
    public void BodyShadeAndSaturationRoundTripAndTurnOnCharacterLighting()
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["lighting"] = "dusk",
            ["lightBodyShade"] = "85",
            ["lightBodySaturation"] = "999",
        };

        var lighting = MapLightingMetadata.Parse(metadata);
        Assert.Equal(85, lighting.BodyShade);
        Assert.Equal(MapLightingMetadata.MaxScale, lighting.BodySaturation);
        Assert.True(lighting.HasBodyAdjustment);
        Assert.True(lighting.HasCharacterLighting); // no rim or shadow needed

        var written = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        MapLightingMetadata.Write(written, lighting);
        Assert.Equal("85", written["lightBodyShade"]);
        Assert.Equal(lighting, MapLightingMetadata.Parse(written));

        var greyOnly = MapLightingMetadata.GetPresetDefaults(MapLightingPreset.Dusk) with { BodySaturation = 0 };
        Assert.True(greyOnly.HasCharacterLighting);
        Assert.Equal(100, MapLightingMetadata.Parse(new Dictionary<string, string> { ["lighting"] = "night" }).BodySaturation);
    }

    [Fact]
    public void WallsStopLightRaysButLetTheWallFaceLight()
    {
        // A wall from x = 100 to 120; the light sits at x = 50.
        var field = LightOcclusionField.Build([new LevelSolid(100f, 0f, 20f, 200f)], new WorldBounds(400f, 200f));
        var hit = field.CastRay(50f, 100f, 1f, 0f, 300f);
        Assert.InRange(hit, 48f, 52f);
        Assert.Equal(300f, field.CastRay(50f, 100f, -1f, 0f, 300f)); // open the other way (leaves the map)

        var reach = field.ComputeVisibility(50f, 100f, 200f);
        Assert.InRange(reach[0], 50f, 50f + LightOcclusionField.WallLightDepth + 2f); // ray 0 points right
        Assert.True(LightOcclusionField.Reaches(reach, 50f, 100f, 90f, 100f));
        Assert.False(LightOcclusionField.Reaches(reach, 50f, 100f, 160f, 100f)); // behind the wall

        // A lamp sunk just inside a wall still shines out of it.
        Assert.True(field.CastRay(102f, 100f, -1f, 0f, 300f) > 100f);
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
