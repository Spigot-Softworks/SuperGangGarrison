using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ForwardSpawnPriorityMetadataTests
{
    [Fact]
    public void ParsePriorityUsesExplicitPropertyBeforeLegacyObjectiveIndex()
    {
        var properties = new Dictionary<string, string>
        {
            [ForwardSpawnPriorityMetadata.PropertyKey] = "4",
            ["objectiveIndex"] = "2",
        };

        Assert.Equal(4, ForwardSpawnMetadata.ParsePriority(properties));
        Assert.Equal(2, ForwardSpawnMetadata.ParseLinkedControlPointIndex(properties));
    }

    [Fact]
    public void ParsePriorityClampsValuesAboveFour()
    {
        Assert.True(ForwardSpawnPriorityMetadata.TryParse("5", out var priority));
        Assert.Equal(4, priority);
    }

    [Fact]
    public void CyclePropertyValueWrapsBetweenOneAndFour()
    {
        Assert.Equal("1", ForwardSpawnPriorityMetadata.CyclePropertyValue("4"));
    }
}
