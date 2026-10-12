using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class MuzzleFlashSettingsTests
{
    [Fact]
    public void MuzzleFlashAndBeamLightRoundTripAndClamp()
    {
        var lighting = MapLightingMetadata.GetPresetDefaults(MapLightingPreset.Custom) with
        {
            MuzzleFlash = 80,
            MuzzleFlashSize = 150,
            MuzzleFlashLength = 5,
            MedicBeamLight = 30,
        };
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        MapLightingMetadata.Write(metadata, lighting);
        var parsed = MapLightingMetadata.Parse(metadata);
        Assert.Equal(80, parsed.MuzzleFlash);
        Assert.Equal(150, parsed.MuzzleFlashSize);
        Assert.Equal(5, parsed.MuzzleFlashLength);
        Assert.Equal(30, parsed.MedicBeamLight);

        metadata[MapLightingMetadata.MuzzleFlashLengthKey] = "0";
        Assert.Equal(MapLightingMetadata.MinMuzzleFlashLength, MapLightingMetadata.Parse(metadata).MuzzleFlashLength);
        metadata[MapLightingMetadata.MuzzleFlashLengthKey] = "99";
        Assert.Equal(MapLightingMetadata.MaxMuzzleFlashLength, MapLightingMetadata.Parse(metadata).MuzzleFlashLength);
    }

    [Fact]
    public void OlderLitMapsGetTheDefaults()
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        MapLightingMetadata.Write(metadata, MapLightingMetadata.GetPresetDefaults(MapLightingPreset.Custom));
        metadata.Remove(MapLightingMetadata.MuzzleFlashKey);
        metadata.Remove(MapLightingMetadata.MedicBeamLightKey);
        var parsed = MapLightingMetadata.Parse(metadata);
        Assert.Equal(60, parsed.MuzzleFlash);
        Assert.Equal(45, parsed.MedicBeamLight);
    }
}
