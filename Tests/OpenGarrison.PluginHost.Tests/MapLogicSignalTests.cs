using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class MapLogicSignalTests
{
    [Fact]
    public void CpTriggerImpulseFiresOnCapture()
    {
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "cp",
                Kind = MapLogicNodeKind.CpTrigger,
                LinkedControlPointIndex = 1,
                SignalMode = MapLogicSignalMode.Impulse,
                CpCaptureDetectMode = MapLogicCpCaptureDetectMode.RedCapture,
            },
        ]);

        var marker = new RoomObjectMarker(RoomObjectType.ControlPoint, 0f, 0f, 32f, 32f, "ControlPointRedS", PlayerTeam.Red, "controlPoint1");
        var points = new[] { new ControlPointState(1, marker) { Team = PlayerTeam.Blue } };
        graph.ResetCpTriggerStates(points);
        graph.EvaluateCombinatorial(points);
        Assert.False(graph.GetOutput(graph.NodeIndexByKey["cp"]));

        points[0].Team = PlayerTeam.Red;
        graph.EvaluateCombinatorial(points);
        Assert.True(graph.GetOutput(graph.NodeIndexByKey["cp"]));

        graph.EvaluateCombinatorial(points);
        Assert.False(graph.GetOutput(graph.NodeIndexByKey["cp"]));
    }

    [Fact]
    public void PlayerTriggerImpulseFiresOnEnter()
    {
        var zone = CreatePlayerTriggerZone(0f, 0f, 42f, 42f, PlayerTriggerTeamFilter.Any, "trigger");
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "trigger",
                Kind = MapLogicNodeKind.PlayerTrigger,
                PlayerTriggerRoomObjectIndex = 0,
                PlayerTriggerTeamFilter = PlayerTriggerTeamFilter.Any,
                SignalMode = MapLogicSignalMode.Impulse,
                PlayerDetectMode = MapLogicPlayerDetectMode.PlayerEnter,
            },
        ]);

        var player = CreatePlayer(PlayerTeam.Red, 10f, 10f);
        var context = new PlayerTriggerEvaluationContext([], [zone], _ => true);
        graph.ResetPlayerTriggerStates(context);
        graph.EvaluateCombinatorial([], context);
        Assert.False(graph.GetOutput(graph.NodeIndexByKey["trigger"]));

        context = new PlayerTriggerEvaluationContext([player], [zone], _ => true);
        graph.EvaluateCombinatorial([], context);
        Assert.True(graph.GetOutput(graph.NodeIndexByKey["trigger"]));

        graph.EvaluateCombinatorial([], context);
        Assert.False(graph.GetOutput(graph.NodeIndexByKey["trigger"]));
    }

    [Fact]
    public void PlayerTriggerImpulseFiresOnExit()
    {
        var zone = CreatePlayerTriggerZone(0f, 0f, 42f, 42f, PlayerTriggerTeamFilter.Any, "trigger");
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "trigger",
                Kind = MapLogicNodeKind.PlayerTrigger,
                PlayerTriggerRoomObjectIndex = 0,
                PlayerTriggerTeamFilter = PlayerTriggerTeamFilter.Any,
                SignalMode = MapLogicSignalMode.Impulse,
                PlayerDetectMode = MapLogicPlayerDetectMode.PlayerExit,
            },
        ]);

        var player = CreatePlayer(PlayerTeam.Red, 10f, 10f);
        var occupiedContext = new PlayerTriggerEvaluationContext([player], [zone], _ => true);
        graph.ResetPlayerTriggerStates(occupiedContext);
        graph.EvaluateCombinatorial([], occupiedContext);
        Assert.False(graph.GetOutput(graph.NodeIndexByKey["trigger"]));

        var emptyContext = new PlayerTriggerEvaluationContext([], [zone], _ => true);
        graph.EvaluateCombinatorial([], emptyContext);
        Assert.True(graph.GetOutput(graph.NodeIndexByKey["trigger"]));
    }

    [Fact]
    public void TimerImpulseCompletesAfterShortCpCapturePulse()
    {
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "cp",
                Kind = MapLogicNodeKind.CpTrigger,
                LinkedControlPointIndex = 1,
                SignalMode = MapLogicSignalMode.Impulse,
                CpCaptureDetectMode = MapLogicCpCaptureDetectMode.AnyCapture,
            },
            new MapLogicNodeDefinition
            {
                LogicKey = "timer",
                Kind = MapLogicNodeKind.Timer,
                InputRef = "node:cp",
                CountdownSeconds = 0.1f,
                DelayedTrue = true,
                SignalMode = MapLogicSignalMode.Impulse,
            },
        ]);

        var marker = new RoomObjectMarker(RoomObjectType.ControlPoint, 0f, 0f, 32f, 32f, "ControlPointRedS", PlayerTeam.Red, "controlPoint1");
        var points = new[] { new ControlPointState(1, marker) { Team = PlayerTeam.Blue } };
        var timerIndex = graph.NodeIndexByKey["timer"];

        graph.ResetCpTriggerStates(points);
        graph.ResetTimerStates();
        graph.EvaluateCombinatorial(points);
        Assert.False(graph.GetOutput(timerIndex));

        points[0].Team = PlayerTeam.Red;
        graph.EvaluateCombinatorial(points);
        Assert.True(graph.GetOutput(graph.NodeIndexByKey["cp"]));
        graph.AdvanceTimers(0f);
        Assert.False(graph.GetOutput(timerIndex));

        graph.EvaluateCombinatorial(points);
        Assert.False(graph.GetOutput(graph.NodeIndexByKey["cp"]));
        graph.AdvanceTimers(0.05f);
        Assert.False(graph.GetOutput(timerIndex));

        graph.AdvanceTimers(0.05f);
        Assert.True(graph.GetOutput(timerIndex));

        graph.AdvanceTimers(0f);
        Assert.False(graph.GetOutput(timerIndex));
    }

    [Fact]
    public void TimerImpulseFiresOnceAfterCountdown()
    {
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "src",
                Kind = MapLogicNodeKind.CpTrigger,
                LinkedControlPointIndex = 1,
                OwnerRequirement = MapLogicCpTriggerOwnerRequirement.Red,
            },
            new MapLogicNodeDefinition
            {
                LogicKey = "timer",
                Kind = MapLogicNodeKind.Timer,
                InputRef = "node:src",
                CountdownSeconds = 1f,
                DelayedTrue = true,
                SignalMode = MapLogicSignalMode.Impulse,
            },
        ]);

        var marker = new RoomObjectMarker(RoomObjectType.ControlPoint, 0f, 0f, 32f, 32f, "ControlPointRedS", PlayerTeam.Red, "controlPoint1");
        var points = new[] { new ControlPointState(1, marker) { Team = PlayerTeam.Red } };

        graph.ResetTimerStates();
        graph.EvaluateCombinatorial(points);
        graph.AdvanceTimers(0f);
        graph.AdvanceTimers(1f);
        Assert.True(graph.GetOutput(graph.NodeIndexByKey["timer"]));

        graph.AdvanceTimers(0f);
        Assert.False(graph.GetOutput(graph.NodeIndexByKey["timer"]));
    }

    [Fact]
    public void OscillatorImpulseFiresOnStartAndEveryPeriod()
    {
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "osc",
                Kind = MapLogicNodeKind.Oscillator,
                Autostart = true,
                SignalMode = MapLogicSignalMode.Impulse,
                SignalPeriodSeconds = 1f,
            },
        ]);

        graph.ResetOscillatorStates();
        graph.EvaluateCombinatorial([]);
        graph.AdvanceOscillators(0f);
        Assert.True(graph.GetOutput(graph.NodeIndexByKey["osc"]));

        graph.AdvanceOscillators(0f);
        Assert.False(graph.GetOutput(graph.NodeIndexByKey["osc"]));

        graph.AdvanceOscillators(1f);
        Assert.True(graph.GetOutput(graph.NodeIndexByKey["osc"]));
    }

    [Fact]
    public void DamageTriggerImpulseFiresOnThresholdCrossing()
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
                SignalMode = MapLogicSignalMode.Impulse,
            },
        ]);

        var context = new DamageTriggerEvaluationContext(_ => 1f);
        graph.ResetDamageTriggerStates(context);
        graph.EvaluateCombinatorial([]);
        graph.EvaluateDamageTriggers(new DamageTriggerEvaluationContext(_ => 0.4f));
        Assert.True(graph.GetOutput(graph.NodeIndexByKey["dmg"]));

        graph.EvaluateDamageTriggers(new DamageTriggerEvaluationContext(_ => 0.4f));
        Assert.False(graph.GetOutput(graph.NodeIndexByKey["dmg"]));
    }

    private static RoomObjectMarker CreatePlayerTriggerZone(
        float x,
        float y,
        float width,
        float height,
        PlayerTriggerTeamFilter filter,
        string logicKey)
    {
        return new RoomObjectMarker(
            RoomObjectType.PlayerTriggerZone,
            x,
            y,
            width,
            height,
            string.Empty,
            SourceName: logicKey,
            PlayerTriggerZone: new PlayerTriggerZoneConfiguration(filter));
    }

    private static PlayerEntity CreatePlayer(PlayerTeam team, float x, float y)
    {
        var player = new PlayerEntity(1, CharacterClassCatalog.Scout, "Test");
        player.Spawn(team, x, y);
        return player;
    }
}
