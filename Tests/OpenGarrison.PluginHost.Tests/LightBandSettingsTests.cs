using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class LightBandSettingsTests
{
    [Fact]
    public void BandSettingsRoundTripThroughMetadata()
    {
        var lighting = MapLightingMetadata.GetPresetDefaults(MapLightingPreset.Custom) with
        {
            Banded = true,
            BandSteps = 4,
            BandPixel = 6,
            BandClip = true,
        };
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        MapLightingMetadata.Write(metadata, lighting);

        var parsed = MapLightingMetadata.Parse(metadata);
        Assert.True(parsed.Banded);
        Assert.Equal(4, parsed.BandSteps);
        Assert.Equal(6, parsed.BandPixel);
        Assert.True(parsed.BandClip);
    }

    [Fact]
    public void OlderMapsGetDefaultBandsAndOutOfRangeValuesClamp()
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        MapLightingMetadata.Write(metadata, MapLightingMetadata.GetPresetDefaults(MapLightingPreset.Custom) with { Banded = true });
        metadata.Remove(MapLightingMetadata.BandStepsKey);
        metadata.Remove(MapLightingMetadata.BandPixelKey);
        metadata.Remove(MapLightingMetadata.BandClipKey);
        var parsed = MapLightingMetadata.Parse(metadata);
        Assert.Equal(MapLightingMetadata.DefaultBandSteps, parsed.BandSteps);
        Assert.Equal(MapLightingMetadata.DefaultBandPixel, parsed.BandPixel);
        Assert.False(parsed.BandClip);

        metadata[MapLightingMetadata.BandStepsKey] = "0";
        metadata[MapLightingMetadata.BandPixelKey] = "99";
        parsed = MapLightingMetadata.Parse(metadata);
        Assert.Equal(MapLightingMetadata.MinBandSteps, parsed.BandSteps);
        Assert.Equal(MapLightingMetadata.MaxBandPixel, parsed.BandPixel);
    }
}
