using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ControlPointOwnershipResolverTests
{
    [Fact]
    public void ModeDefaultOwnershipUsesRedNeutralBlueLayoutForThreeControlPoints()
    {
        var context = new ControlPointOwnershipContext(2, 3, ControlPointSetupMode: false, GameModeKind.ControlPoint);
        Assert.Null(ControlPointOwnershipResolver.ResolveModeDefaultTeam(in context));

        context = context with { PointIndex = 1 };
        Assert.Equal(PlayerTeam.Red, ControlPointOwnershipResolver.ResolveModeDefaultTeam(in context));

        context = context with { PointIndex = 3 };
        Assert.Equal(PlayerTeam.Blue, ControlPointOwnershipResolver.ResolveModeDefaultTeam(in context));
    }

    [Fact]
    public void ExplicitInitialOwnershipOverridesModeDefault()
    {
        var marker = new RoomObjectMarker(
            RoomObjectType.ControlPoint,
            0f,
            0f,
            42f,
            42f,
            "ControlPointNeutralS",
            SourceName: "controlPoint2",
            InitialOwnership: ControlPointInitialOwnership.Blue);
        var context = new ControlPointOwnershipContext(2, 3, false, GameModeKind.ControlPoint, OverrideInitialOwnership: true);

        Assert.Equal(PlayerTeam.Blue, ControlPointOwnershipResolver.ResolveInitialTeam(marker, in context));

        var disabledContext = context with { OverrideInitialOwnership = false };
        Assert.Null(ControlPointOwnershipResolver.ResolveInitialTeam(marker, in disabledContext));
    }

    [Fact]
    public void BuilderPreviewMatchesAssignedOwnershipForThreePointMap()
    {
        var entities = new[]
        {
            CustomMapBuilderEntity.Create("controlPoint", 10f, 20f, new Dictionary<string, string> { ["index"] = "1" }),
            CustomMapBuilderEntity.Create("controlPoint", 30f, 20f, new Dictionary<string, string> { ["index"] = "2" }),
            CustomMapBuilderEntity.Create("controlPoint", 50f, 20f, new Dictionary<string, string> { ["index"] = "3" }),
        };

        Assert.Equal(PlayerTeam.Red, ControlPointOwnershipResolver.ResolveBuilderInitialTeam(entities[0], CustomMapBuilderGameMode.ControlPoint, entities));
        Assert.Null(ControlPointOwnershipResolver.ResolveBuilderInitialTeam(entities[1], CustomMapBuilderGameMode.ControlPoint, entities));
        Assert.Equal(PlayerTeam.Blue, ControlPointOwnershipResolver.ResolveBuilderInitialTeam(entities[2], CustomMapBuilderGameMode.ControlPoint, entities));
        Assert.Equal("ControlPointRedS", ControlPointOwnershipResolver.ResolveBuilderControlPointSpriteName(entities[0], CustomMapBuilderGameMode.ControlPoint, entities));
        Assert.Equal("ControlPointNeutralS", ControlPointOwnershipResolver.ResolveBuilderControlPointSpriteName(entities[1], CustomMapBuilderGameMode.ControlPoint, entities));
        Assert.Equal("ControlPointBlueS", ControlPointOwnershipResolver.ResolveBuilderControlPointSpriteName(entities[2], CustomMapBuilderGameMode.ControlPoint, entities));
    }
}
