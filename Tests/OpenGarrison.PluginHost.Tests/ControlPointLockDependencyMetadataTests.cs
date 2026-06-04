using System.Collections.Generic;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ControlPointLockDependencyMetadataTests
{
    [Fact]
    public void ConflictingLockedAndUnlockedRulesAreDetected()
    {
        var rules = new ControlPointLockRules(
            new ControlPointLockDependency(2, PlayerTeam.Red),
            new ControlPointLockDependency(2, PlayerTeam.Red));

        Assert.True(ControlPointLockDependencyMetadata.HasConflictingRules(rules));
    }

    [Fact]
    public void LockWhenRuleSetsLockedWhileDependencyOwned()
    {
        var markers = new[]
        {
            CreateControlPointMarker("controlPoint1", ControlPointInitialOwnership.Red),
            CreateControlPointMarker("controlPoint2", ControlPointInitialOwnership.Blue),
        };
        var points = new[]
        {
            new ControlPointState(1, markers[0]) { Team = PlayerTeam.Red },
            new ControlPointState(2, markers[1]) { Team = PlayerTeam.Blue },
        };

        var rules = new ControlPointLockRules(
            new ControlPointLockDependency(2, PlayerTeam.Blue),
            default);
        var isLocked = ControlPointLockDependencyMetadata.GetInitialLocked(rules);
        ControlPointLockDependencyMetadata.ApplyMapLockTriggers(rules, points, null, ref isLocked);

        Assert.True(isLocked);
    }

    [Fact]
    public void UnlockWhenRuleUnlocksOnlyWhenDependencyOwned()
    {
        var markers = new[]
        {
            CreateControlPointMarker("controlPoint1", ControlPointInitialOwnership.Neutral),
            CreateControlPointMarker("controlPoint2", ControlPointInitialOwnership.Red),
        };
        var points = new[]
        {
            new ControlPointState(1, markers[0]) { Team = null },
            new ControlPointState(2, markers[1]) { Team = PlayerTeam.Red },
        };

        var rules = new ControlPointLockRules(
            default,
            new ControlPointLockDependency(2, PlayerTeam.Red),
            InitialLocked: true);
        var isLocked = ControlPointLockDependencyMetadata.GetInitialLocked(rules);
        ControlPointLockDependencyMetadata.ApplyMapLockTriggers(rules, points, null, ref isLocked);

        Assert.False(isLocked);
    }

    [Fact]
    public void UnlockWhenRuleDoesNotForceLockWhenConditionIsFalse()
    {
        var markers = new[]
        {
            CreateControlPointMarker("controlPoint1", ControlPointInitialOwnership.Neutral),
            CreateControlPointMarker("controlPoint2", ControlPointInitialOwnership.Red),
        };
        var points = new[]
        {
            new ControlPointState(1, markers[0]) { Team = null },
            new ControlPointState(2, markers[1]) { Team = PlayerTeam.Blue },
        };

        var rules = new ControlPointLockRules(
            default,
            new ControlPointLockDependency(2, PlayerTeam.Red),
            InitialLocked: true);
        var isLocked = ControlPointLockDependencyMetadata.GetInitialLocked(rules);
        ControlPointLockDependencyMetadata.ApplyMapLockTriggers(rules, points, null, ref isLocked);

        Assert.True(isLocked);
    }

    [Fact]
    public void InitialLockedWithoutTriggersStaysLocked()
    {
        var rules = new ControlPointLockRules(default, default, InitialLocked: true);
        var isLocked = ControlPointLockDependencyMetadata.GetInitialLocked(rules);
        ControlPointLockDependencyMetadata.ApplyMapLockTriggers(rules, Array.Empty<ControlPointState>(), null, ref isLocked);

        Assert.True(isLocked);
    }

    [Fact]
    public void InitialUnlockedWithoutTriggersStaysUnlocked()
    {
        var isLocked = ControlPointLockDependencyMetadata.GetInitialLocked(ControlPointLockRules.Empty);
        ControlPointLockDependencyMetadata.ApplyMapLockTriggers(
            ControlPointLockRules.Empty,
            Array.Empty<ControlPointState>(),
            null,
            ref isLocked);

        Assert.False(isLocked);
    }

    [Fact]
    public void LogicUnlockTriggerClearsLockWithoutReLockingWhenSignalEnds()
    {
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "unlock",
                Kind = MapLogicNodeKind.CpTrigger,
                LinkedControlPointIndex = 1,
                OwnerRequirement = MapLogicCpTriggerOwnerRequirement.Red,
            },
        ]);
        var marker = CreateControlPointMarker("controlPoint1", ControlPointInitialOwnership.Neutral);
        var points = new[] { new ControlPointState(1, marker) { Team = PlayerTeam.Blue } };
        graph.Evaluate(points);

        var rules = new ControlPointLockRules(
            default,
            default,
            UnlockedWhenLogicNodeIndex: 0,
            InitialLocked: true);
        var isLocked = ControlPointLockDependencyMetadata.GetInitialLocked(rules);
        ControlPointLockDependencyMetadata.ApplyMapLockTriggers(rules, points, graph, ref isLocked);

        Assert.True(isLocked);

        points[0].Team = PlayerTeam.Red;
        graph.Evaluate(points);
        ControlPointLockDependencyMetadata.ApplyMapLockTriggers(rules, points, graph, ref isLocked);
        Assert.False(isLocked);

        points[0].Team = PlayerTeam.Blue;
        graph.Evaluate(points);
        ControlPointLockDependencyMetadata.ApplyMapLockTriggers(rules, points, graph, ref isLocked);
        Assert.False(isLocked);
    }

    [Fact]
    public void ParsesLegacyStartLockedProperty()
    {
        var rules = ControlPointLockDependencyMetadata.Parse(new Dictionary<string, string>
        {
            ["startLocked"] = "true",
        });

        Assert.True(rules.InitialLocked);
    }

    [Fact]
    public void OverrideInitialOwnershipUsesMarkerPropertyOnlyWhenEnabled()
    {
        var marker = new RoomObjectMarker(
            RoomObjectType.ControlPoint,
            0f,
            0f,
            42f,
            42f,
            "ControlPointNeutralS",
            InitialOwnership: ControlPointInitialOwnership.Blue);
        var context = new ControlPointOwnershipContext(1, 3, false, GameModeKind.ControlPoint, OverrideInitialOwnership: true);

        Assert.Equal(PlayerTeam.Blue, ControlPointOwnershipResolver.ResolveInitialTeam(marker, in context));

        var disabledContext = context with { OverrideInitialOwnership = false };
        Assert.Equal(PlayerTeam.Red, ControlPointOwnershipResolver.ResolveInitialTeam(marker, in disabledContext));
    }

    private static RoomObjectMarker CreateControlPointMarker(
        string sourceName,
        ControlPointInitialOwnership ownership)
    {
        return new RoomObjectMarker(
            RoomObjectType.ControlPoint,
            0f,
            0f,
            42f,
            42f,
            "ControlPointNeutralS",
            SourceName: sourceName,
            InitialOwnership: ownership);
    }
}
