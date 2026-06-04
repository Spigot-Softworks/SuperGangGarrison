using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class CustomMapBuilderEntityNormalizationTests
{
    [Fact]
    public void NormalizeSpawnForwardConvertsToEditorSpawn()
    {
        var entity = CustomMapBuilderEntity.Create("redspawn2", 40f, 50f);
        var normalized = CustomMapBuilderEntityNormalization.NormalizeEntityForEditor(entity);

        Assert.Equal("spawn", normalized.Type);
        Assert.Equal("red", normalized.Properties["team"]);
        Assert.Equal("true", normalized.Properties["forward"]);
        Assert.Equal("2", normalized.Properties["objectiveIndex"]);
    }

    [Fact]
    public void ResolveSpawnForwardExportsLegacyType()
    {
        var entity = CustomMapBuilderEntity.Create(
            "spawn",
            40f,
            50f,
            new Dictionary<string, string>
            {
                ["team"] = "blue",
                ["forward"] = "true",
                ["objectiveIndex"] = "3",
            });

        var exported = CustomMapBuilderEntityNormalization.ResolveEntityForExport(entity);

        Assert.Equal("bluespawn3", exported.Type);
    }

    [Fact]
    public void NormalizeBarrierTeamGatePreservesBlockingFlags()
    {
        var entity = CustomMapBuilderEntity.Create("redteamgate", 10f, 20f, new Dictionary<string, string> { ["xscale"] = "2", ["yscale"] = "1" }, 2f, 1f);
        var normalized = CustomMapBuilderEntityNormalization.NormalizeEntityForEditor(entity);

        Assert.Equal("barrier", normalized.Type);
        Assert.Equal(BarrierTargetFilterMetadata.BlockValue, normalized.Properties[BarrierTargetFilterMetadata.BluePlayersPropertyKey]);
        Assert.Equal(BarrierTargetFilterMetadata.BlockValue, normalized.Properties[BarrierTargetFilterMetadata.BlueShotsPropertyKey]);
    }

    [Fact]
    public void CountsAsTeamSpawnIncludesEditorBaseSpawn()
    {
        var entities = new[]
        {
            CustomMapBuilderEntity.Create("spawn", 1f, 2f, new Dictionary<string, string> { ["team"] = "red", ["forward"] = "false" }),
            CustomMapBuilderEntity.Create("spawn", 3f, 4f, new Dictionary<string, string> { ["team"] = "blue", ["forward"] = "true", ["objectiveIndex"] = "1" }),
        };

        Assert.True(CustomMapBuilderEntityNormalization.CountsAsTeamSpawn(entities[0], "red"));
        Assert.False(CustomMapBuilderEntityNormalization.CountsAsTeamSpawn(entities[1], "blue"));
    }
}
