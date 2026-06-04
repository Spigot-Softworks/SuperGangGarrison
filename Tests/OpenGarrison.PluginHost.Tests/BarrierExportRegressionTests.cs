using System.Collections.Generic;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class BarrierExportRegressionTests
{
    [Fact]
    public void ResolveForExportKeepsModernBarrierWhenShotsAreBlocked()
    {
        var entity = CustomMapBuilderEntity.Create(
            "barrier",
            12f,
            34f,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [BarrierTargetFilterMetadata.RedPlayersPropertyKey] = BarrierTargetFilterMetadata.BlockValue,
                [BarrierTargetFilterMetadata.BluePlayersPropertyKey] = BarrierTargetFilterMetadata.BlockValue,
                [BarrierTargetFilterMetadata.RedShotsPropertyKey] = BarrierTargetFilterMetadata.BlockValue,
                [BarrierTargetFilterMetadata.BlueShotsPropertyKey] = BarrierTargetFilterMetadata.BlockValue,
                [BarrierTargetFilterMetadata.RedIntelPropertyKey] = BarrierTargetFilterMetadata.AllowValue,
                [BarrierTargetFilterMetadata.BlueIntelPropertyKey] = BarrierTargetFilterMetadata.AllowValue,
            });

        var exported = CustomMapBuilderEntityNormalization.ResolveEntityForExport(entity);

        Assert.Equal("barrier", exported.Type);
        Assert.Equal(BarrierTargetFilterMetadata.BlockValue, exported.Properties[BarrierTargetFilterMetadata.RedShotsPropertyKey]);
        Assert.Equal(BarrierTargetFilterMetadata.BlockValue, exported.Properties[BarrierTargetFilterMetadata.BlueShotsPropertyKey]);
    }
}
