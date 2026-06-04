using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class DirectionalWallCollisionTests
{
    [Fact]
    public void PassRightBlocksMovementFromEast()
    {
        var configuration = new DirectionalWallConfiguration(
            DirectionalWallPassDirection.Right,
            DirectionalWallAffectSetting.Affect,
            DirectionalWallAffectSetting.Ignore);
        var marker = DirectionalWallConfiguration.CreateMarker(10f, 0f, 1f, 1f, configuration);

        Assert.True(DirectionalWallCollision.BlocksPlayerMovement(
            configuration,
            PlayerTeam.Red,
            isCarryingIntel: false,
            marker,
            previousLeft: 14f,
            previousRight: 15f,
            previousTop: 20f,
            previousBottom: 21f,
            nextLeft: 10f,
            nextRight: 11f,
            nextTop: 20f,
            nextBottom: 21f));

        Assert.False(DirectionalWallCollision.BlocksPlayerMovement(
            configuration,
            PlayerTeam.Red,
            isCarryingIntel: false,
            marker,
            previousLeft: 4f,
            previousRight: 5f,
            previousTop: 20f,
            previousBottom: 21f,
            nextLeft: 10f,
            nextRight: 11f,
            nextTop: 20f,
            nextBottom: 21f));
    }

    [Fact]
    public void IgnoredPlayersDoNotBlock()
    {
        var configuration = new DirectionalWallConfiguration(
            DirectionalWallPassDirection.Right,
            DirectionalWallAffectSetting.Ignore,
            DirectionalWallAffectSetting.Affect);
        var marker = DirectionalWallConfiguration.CreateMarker(0f, 0f, 1f, 1f, configuration);

        Assert.False(DirectionalWallCollision.BlocksPlayerMovement(
            configuration,
            PlayerTeam.Red,
            isCarryingIntel: false,
            marker,
            previousLeft: 14f,
            previousRight: 15f,
            previousTop: 1f,
            previousBottom: 2f,
            nextLeft: 4f,
            nextRight: 5f,
            nextTop: 1f,
            nextBottom: 2f));
    }

    [Fact]
    public void LeftDoorNormalizesToPassRight()
    {
        var normalized = CustomMapBuilderEntityNormalization.NormalizeEntityForEditor(
            CustomMapBuilderEntity.Create("leftdoor", 0f, 0f));

        Assert.Equal("directionalWall", normalized.Type);
        Assert.Equal(DirectionalWallConfiguration.PassDirectionRightValue, normalized.Properties[DirectionalWallConfiguration.PassDirectionPropertyKey]);
        Assert.Equal(DirectionalWallConfiguration.AffectValue, normalized.Properties[DirectionalWallConfiguration.PlayersPropertyKey]);
        Assert.Equal(DirectionalWallConfiguration.IgnoreValue, normalized.Properties[DirectionalWallConfiguration.ProjectilesPropertyKey]);
    }
}
