using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ForwardSpawnMetadataTests
{
    [Theory]
    [InlineData("owned", ForwardSpawnUseCondition.ObjectiveOwnedByTeam)]
    [InlineData("notOwned", ForwardSpawnUseCondition.ObjectiveNotOwnedByTeam)]
    [InlineData("neutral", ForwardSpawnUseCondition.ObjectiveNeutral)]
    [InlineData("enemyOwned", ForwardSpawnUseCondition.ObjectiveOwnedByEnemy)]
    public void TryParseUseConditionRecognizesStoredValues(string value, ForwardSpawnUseCondition expected)
    {
        Assert.True(ForwardSpawnMetadata.TryParseUseCondition(value, out var condition));
        Assert.Equal(expected, condition);
    }

    [Theory]
    [InlineData(ForwardSpawnUseCondition.ObjectiveOwnedByTeam, PlayerTeam.Red, PlayerTeam.Red, true)]
    [InlineData(ForwardSpawnUseCondition.ObjectiveOwnedByTeam, PlayerTeam.Red, PlayerTeam.Blue, false)]
    [InlineData(ForwardSpawnUseCondition.ObjectiveOwnedByTeam, PlayerTeam.Red, null, false)]
    [InlineData(ForwardSpawnUseCondition.ObjectiveNotOwnedByTeam, PlayerTeam.Red, PlayerTeam.Blue, true)]
    [InlineData(ForwardSpawnUseCondition.ObjectiveNotOwnedByTeam, PlayerTeam.Red, PlayerTeam.Red, false)]
    [InlineData(ForwardSpawnUseCondition.ObjectiveNotOwnedByTeam, PlayerTeam.Red, null, true)]
    [InlineData(ForwardSpawnUseCondition.ObjectiveNeutral, PlayerTeam.Red, null, true)]
    [InlineData(ForwardSpawnUseCondition.ObjectiveNeutral, PlayerTeam.Red, PlayerTeam.Red, false)]
    [InlineData(ForwardSpawnUseCondition.ObjectiveOwnedByEnemy, PlayerTeam.Red, PlayerTeam.Blue, true)]
    [InlineData(ForwardSpawnUseCondition.ObjectiveOwnedByEnemy, PlayerTeam.Red, PlayerTeam.Red, false)]
    [InlineData(ForwardSpawnUseCondition.ObjectiveOwnedByEnemy, PlayerTeam.Red, null, false)]
    public void EvaluateUseConditionMatchesObjectiveOwnership(
        ForwardSpawnUseCondition condition,
        PlayerTeam team,
        PlayerTeam? owner,
        bool expected)
    {
        Assert.Equal(expected, ForwardSpawnMetadata.EvaluateUseCondition(condition, team, owner));
    }

    [Fact]
    public void CycleUseWhenPropertyValueRotatesAllOptions()
    {
        var value = ForwardSpawnMetadata.DefaultUseWhenValue;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var step = 0; step < 4; step += 1)
        {
            Assert.True(seen.Add(value));
            value = ForwardSpawnMetadata.CycleUseWhenPropertyValue(value);
        }

        Assert.Equal(ForwardSpawnMetadata.DefaultUseWhenValue, value);
    }
}
