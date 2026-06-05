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
        ApplyMapLockTriggers(rules, points, null, ref isLocked);

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
        ApplyMapLockTriggers(rules, points, null, ref isLocked);

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
        ApplyMapLockTriggers(rules, points, null, ref isLocked);

        Assert.True(isLocked);
    }

    [Fact]
    public void InitialLockedWithoutTriggersStaysLocked()
    {
        var rules = new ControlPointLockRules(default, default, InitialLocked: true);
        var isLocked = ControlPointLockDependencyMetadata.GetInitialLocked(rules);
        ApplyMapLockTriggers(rules, Array.Empty<ControlPointState>(), null, ref isLocked);

        Assert.True(isLocked);
    }

    [Fact]
    public void InitialUnlockedWithoutTriggersStaysUnlocked()
    {
        var isLocked = ControlPointLockDependencyMetadata.GetInitialLocked(ControlPointLockRules.Empty);
        ApplyMapLockTriggers(
            ControlPointLockRules.Empty,
            Array.Empty<ControlPointState>(),
            null,
            ref isLocked);

        Assert.False(isLocked);
    }

    [Fact]
    public void AndGateOfTwoDamageTriggersUnlocksControlPoint()
    {
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "dmg1",
                Kind = MapLogicNodeKind.DamageTrigger,
                DamageableRoomObjectIndex = 0,
                TriggerBelowThreshold = true,
                TriggerBelowPercent = 50,
            },
            new MapLogicNodeDefinition
            {
                LogicKey = "dmg2",
                Kind = MapLogicNodeKind.DamageTrigger,
                DamageableRoomObjectIndex = 1,
                TriggerBelowThreshold = true,
                TriggerBelowPercent = 50,
            },
            new MapLogicNodeDefinition
            {
                LogicKey = "andGate",
                Kind = MapLogicNodeKind.Gate,
                GateType = MapLogicGateType.And,
                InputRef1 = "node:dmg1",
                InputRef2 = "node:dmg2",
            },
        ]);

        graph.ResetDamageTriggerStates(new DamageTriggerEvaluationContext(index => 1f));
        graph.EvaluateCombinatorial([]);
        graph.EvaluateDamageTriggers(new DamageTriggerEvaluationContext(index => index == 0 ? 0.4f : 0.3f));

        var rules = new ControlPointLockRules(
            default,
            default,
            UnlockedWhenLogicNodeIndex: graph.NodeIndexByKey["andGate"],
            InitialLocked: true);
        var isLocked = ControlPointLockDependencyMetadata.GetInitialLocked(rules);
        ApplyMapLockTriggers(rules, [], graph, ref isLocked);

        Assert.False(isLocked);
    }

    [Fact]
    public void LogicLockAndUnlockSignalsLatchUntilOppositeSignal()
    {
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "lock",
                Kind = MapLogicNodeKind.CpTrigger,
                LinkedControlPointIndex = 1,
                OwnerRequirement = MapLogicCpTriggerOwnerRequirement.Red,
            },
            new MapLogicNodeDefinition
            {
                LogicKey = "unlock",
                Kind = MapLogicNodeKind.CpTrigger,
                LinkedControlPointIndex = 2,
                OwnerRequirement = MapLogicCpTriggerOwnerRequirement.Red,
            },
        ]);

        var markers = new[]
        {
            CreateControlPointMarker("controlPoint1", ControlPointInitialOwnership.Neutral),
            CreateControlPointMarker("controlPoint2", ControlPointInitialOwnership.Neutral),
        };
        var points = new[]
        {
            new ControlPointState(1, markers[0]) { Team = null },
            new ControlPointState(2, markers[1]) { Team = null },
        };

        var rules = new ControlPointLockRules(
            default,
            default,
            LockedWhenLogicNodeIndex: graph.NodeIndexByKey["lock"],
            UnlockedWhenLogicNodeIndex: graph.NodeIndexByKey["unlock"],
            InitialLocked: true);
        var isLocked = ControlPointLockDependencyMetadata.GetInitialLocked(rules);

        ApplyMapLockTriggers(rules, points, graph, ref isLocked);
        Assert.True(isLocked);

        points[1].Team = PlayerTeam.Red;
        graph.Evaluate(points);
        ApplyMapLockTriggers(rules, points, graph, ref isLocked);
        Assert.False(isLocked);

        graph.Evaluate(points);
        ApplyMapLockTriggers(rules, points, graph, ref isLocked);
        Assert.False(isLocked);

        points[0].Team = PlayerTeam.Red;
        graph.Evaluate(points);
        ApplyMapLockTriggers(rules, points, graph, ref isLocked);
        Assert.True(isLocked);

        graph.Evaluate(points);
        ApplyMapLockTriggers(rules, points, graph, ref isLocked);
        Assert.True(isLocked);
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
        ApplyMapLockTriggers(rules, points, graph, ref isLocked);

        Assert.True(isLocked);

        points[0].Team = PlayerTeam.Red;
        graph.Evaluate(points);
        ApplyMapLockTriggers(rules, points, graph, ref isLocked);
        Assert.False(isLocked);

        points[0].Team = PlayerTeam.Blue;
        graph.Evaluate(points);
        ApplyMapLockTriggers(rules, points, graph, ref isLocked);
        Assert.False(isLocked);
    }

    [Fact]
    public void CpCaptureImpulseLocksControlPointWhenStatefulNodesAreNotReset()
    {
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "capture",
                Kind = MapLogicNodeKind.CpTrigger,
                LinkedControlPointIndex = 1,
                SignalMode = MapLogicSignalMode.Impulse,
                CpCaptureDetectMode = MapLogicCpCaptureDetectMode.AnyCapture,
            },
        ]);

        var marker = CreateControlPointMarker("controlPoint1", ControlPointInitialOwnership.Neutral);
        var points = new[] { new ControlPointState(1, marker) { Team = PlayerTeam.Blue, IsLocked = false } };
        var captureIndex = graph.NodeIndexByKey["capture"];
        var rules = new ControlPointLockRules(
            default,
            default,
            LockedWhenLogicNodeIndex: captureIndex,
            InitialLocked: false);

        graph.ResetCpTriggerStates(points);
        graph.EvaluateCombinatorial(points);
        Assert.False(graph.GetOutput(captureIndex));

        points[0].Team = PlayerTeam.Red;
        graph.EvaluateCombinatorial(points);
        Assert.True(graph.GetOutput(captureIndex));

        var isLocked = false;
        ApplyMapLockTriggers(rules, points, graph, ref isLocked);
        Assert.True(isLocked);

        graph.ResetCpTriggerStates(points);
        graph.EvaluateCombinatorial(points);
        Assert.False(graph.GetOutput(captureIndex));
    }

    [Fact]
    public void RisingEdgeLogicSignalUnlocksControlPointAndStaysUnlockedAfterPulseEnds()
    {
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "dmg",
                Kind = MapLogicNodeKind.DamageTrigger,
                DamageableRoomObjectIndex = 0,
                TriggerBelowThreshold = true,
                TriggerBelowPercent = 50,
            },
            new MapLogicNodeDefinition
            {
                LogicKey = "edge",
                Kind = MapLogicNodeKind.RisingEdge,
                InputRef = "node:dmg",
            },
        ]);

        var edgeIndex = graph.NodeIndexByKey["edge"];
        var rules = new ControlPointLockRules(
            default,
            default,
            UnlockedWhenLogicNodeIndex: edgeIndex,
            InitialLocked: true);
        var isLocked = ControlPointLockDependencyMetadata.GetInitialLocked(rules);

        graph.ResetDamageTriggerStates(new DamageTriggerEvaluationContext(_ => 1f));
        graph.ResetRisingEdgeStates();
        graph.EvaluateCombinatorial([]);
        graph.EvaluateDamageTriggers(new DamageTriggerEvaluationContext(_ => 0.4f));
        Assert.True(graph.GetOutput(edgeIndex));
        ApplyMapLockTriggers(rules, [], graph, ref isLocked);
        Assert.False(isLocked);

        graph.EvaluateDamageTriggers(new DamageTriggerEvaluationContext(_ => 0.4f));
        Assert.False(graph.GetOutput(edgeIndex));
        ApplyMapLockTriggers(rules, [], graph, ref isLocked);
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

    private static void ApplyMapLockTriggers(
        in ControlPointLockRules rules,
        IReadOnlyList<ControlPointState> points,
        MapLogicGraph? graph,
        ref bool isLocked)
    {
        ControlPointLockDependencyMetadata.ApplyMapLockTriggers(
            rules,
            points,
            graph,
            ref isLocked);
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
