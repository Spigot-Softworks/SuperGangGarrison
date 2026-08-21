using OpenGarrison.Core;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using OpenGarrison.Core.BotBrain;

namespace OpenGarrison.Core.BotBrain;

public sealed class BotBrainPracticeBotController : IPracticeBotController
{
    private static readonly bool EnableParallelBotThinkForDiagnostics =
        Environment.GetEnvironmentVariable("OG_CLIENT_PERF_BOT_THINK_PARALLEL") is "1" or "true" or "TRUE";
    private static readonly bool BotThinkSpikeTracingEnabled =
        Environment.GetEnvironmentVariable("OG_CLIENT_PERF_BOT_TRACE") is "1" or "true" or "TRUE"
        || Environment.GetEnvironmentVariable("OG_CLIENT_PERF_BOT_THINK_TRACE") is "1" or "true" or "TRUE";
    private static readonly double BotThinkSpikeTraceThresholdMilliseconds = ResolveBotThinkSpikeTraceThresholdMilliseconds();
    private static readonly string? BotThinkSpikeTracePath = BotThinkSpikeTracingEnabled
        ? RuntimePaths.GetLogPath($"bot-think-spikes-{DateTime.Now.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture)}.log")
        : null;
    private static readonly object BotThinkTraceSync = new();

    private static double ResolveBotThinkSpikeTraceThresholdMilliseconds()
    {
        var configured = Environment.GetEnvironmentVariable("OG_CLIENT_PERF_BOT_TRACE_THRESHOLD_MS");
        return double.TryParse(configured, NumberStyles.Float, CultureInfo.InvariantCulture, out var threshold)
            ? Math.Max(0d, threshold)
            : 100d;
    }

    private readonly record struct BotThinkWorkItem(
        byte Slot,
        ControlledBotSlot ControlledSlot,
        PlayerEntity Player,
        BotBrainController Controller);

    private readonly record struct BotThinkResult(
        bool HasInput,
        byte Slot,
        PlayerTeam Team,
        PlayerEntity Player,
        BotBrainController Controller,
        PlayerInputSnapshot Input);

    private readonly Dictionary<byte, BotBrainController> _controllersBySlot = new();
    private readonly Dictionary<byte, ControlledBotSlot> _configuredSlots = new();
    private readonly Dictionary<byte, PlayerTeam> _controlledTeamsBySlot = new();
    private readonly BotBrainChatBubbleController _chatBubbles = new();
    private readonly List<BotControllerDiagnosticsEntry> _diagnosticEntries = new();
    private BotControllerDiagnosticsSnapshot _lastDiagnostics = BotControllerDiagnosticsSnapshot.Empty;
    private readonly bool _disableShippedNavigationGraphs;

    public BotBrainPracticeBotController(bool disableShippedNavigationGraphs = false)
    {
        _disableShippedNavigationGraphs = disableShippedNavigationGraphs;
    }

    public bool CollectDiagnostics { get; set; }

    public BotControllerDiagnosticsSnapshot LastDiagnostics => _lastDiagnostics;

    public BotBrainPracticeBotRuntimeSnapshot RuntimeSnapshot
    {
        get
        {
            var activeControllerCount = 0;
            var navigationLoadedCount = 0;
            var navigationMissingCount = 0;
            var objectiveTapeLoadedCount = 0;
            var activePathCount = 0;
            foreach (var slot in _configuredSlots.Keys)
            {
                if (!_controllersBySlot.TryGetValue(slot, out var controller))
                {
                    continue;
                }

                activeControllerCount += 1;
                if (controller.HasNavigationGraph)
                {
                    navigationLoadedCount += 1;
                }
                else
                {
                    navigationMissingCount += 1;
                }

                if (controller.HasObjectiveTapeAsset)
                {
                    objectiveTapeLoadedCount += 1;
                }

                if (controller.HasActivePath)
                {
                    activePathCount += 1;
                }
            }

            return new BotBrainPracticeBotRuntimeSnapshot(
                ActiveControllerCount: activeControllerCount,
                NavigationLoadedCount: navigationLoadedCount,
                NavigationMissingCount: navigationMissingCount,
                ObjectiveTapeLoadedCount: objectiveTapeLoadedCount,
                ActivePathCount: activePathCount);
        }
    }

    public void Reset()
    {
        foreach (var controller in _controllersBySlot.Values)
        {
            controller.Reset();
        }

        _controllersBySlot.Clear();
        _configuredSlots.Clear();
        _controlledTeamsBySlot.Clear();
        _chatBubbles.Reset();
        _diagnosticEntries.Clear();
        _lastDiagnostics = BotControllerDiagnosticsSnapshot.Empty;
    }

    /// <summary>
    /// Test/diagnostic affordance: look up the per-slot brain controller. Intended for
    /// xUnit harnesses and debug overlays that need to read trace strings and graph state.
    /// </summary>
    public bool TryGetBotBrainController(byte slot, out BotBrainController? controller)
    {
        return _controllersBySlot.TryGetValue(slot, out controller);
    }

    public bool RequiresPerTickNavigationThink(byte slot)
    {
        return _controllersBySlot.TryGetValue(slot, out var controller)
            && controller.RequiresPerTickNavigationThink;
    }

    public bool RequiresImmediateNavigationThink(byte slot)
    {
        return _controllersBySlot.TryGetValue(slot, out var controller)
            && controller.RequiresImmediateNavigationThink;
    }

    public void ConfigureSpawnOverrides(
        SimulationWorld world,
        IReadOnlyDictionary<byte, ControlledBotSlot> controlledSlots)
    {
        foreach (var slot in _controllersBySlot.Keys.Except(controlledSlots.Keys).ToArray())
        {
            _controllersBySlot.Remove(slot);
            _configuredSlots.Remove(slot);
            _chatBubbles.RemoveSlot(slot);
        }

        foreach (var (slot, controlledSlot) in controlledSlots)
        {
            if (!_controllersBySlot.TryGetValue(slot, out var controller))
            {
                controller = new BotBrainController(_disableShippedNavigationGraphs);
                controller.PreferEnemyPlayerObjective = controlledSlot.PreferEnemyPlayerObjective;
                _controllersBySlot[slot] = controller;
                _configuredSlots[slot] = controlledSlot;
                continue;
            }

            if (_configuredSlots.TryGetValue(slot, out var previousSlot)
                && previousSlot.Team == controlledSlot.Team
                && previousSlot.ClassId == controlledSlot.ClassId
                && previousSlot.PreferEnemyPlayerObjective == controlledSlot.PreferEnemyPlayerObjective)
            {
                continue;
            }

            _configuredSlots[slot] = controlledSlot;
            controller.Reset();
            controller.PreferEnemyPlayerObjective = controlledSlot.PreferEnemyPlayerObjective;
        }
    }

    public IReadOnlyDictionary<byte, PlayerInputSnapshot> BuildInputs(
        SimulationWorld world,
        IReadOnlyDictionary<byte, ControlledBotSlot> controlledSlots)
    {
        return BuildInputsForSlots(world, controlledSlots, new List<byte>(controlledSlots.Keys));
    }

    public IReadOnlyDictionary<byte, PlayerInputSnapshot> BuildInputsForSlots(
        SimulationWorld world,
        IReadOnlyDictionary<byte, ControlledBotSlot> controlledSlots,
        IReadOnlyCollection<byte> slotsToThink)
    {
        ConfigureSpawnOverrides(world, controlledSlots);

        var inputs = new Dictionary<byte, PlayerInputSnapshot>(slotsToThink.Count);
        _controlledTeamsBySlot.Clear();
        foreach (var (slot, controlledSlot) in controlledSlots)
        {
            _controlledTeamsBySlot[slot] = controlledSlot.Team;
        }

        var workItems = BuildBotThinkWorkItems(world, controlledSlots, slotsToThink);
        if (workItems.Length == 0)
        {
            return inputs;
        }

        var thinkResults = BuildBotThinkResults(world, workItems, _controlledTeamsBySlot);
        for (var index = 0; index < thinkResults.Length; index += 1)
        {
            var result = thinkResults[index];
            if (!result.HasInput)
            {
                continue;
            }

            inputs[result.Slot] = _chatBubbles.Update(
                world,
                result.Slot,
                result.Player,
                result.Team,
                result.Controller,
                result.Input,
            _controlledTeamsBySlot);
        }

        _lastDiagnostics = BuildDiagnostics(world, controlledSlots);

        return inputs;
    }

    public IReadOnlyDictionary<byte, PlayerInputSnapshot> AdvanceCachedNavigationForSlots(
        SimulationWorld world,
        IReadOnlyDictionary<byte, ControlledBotSlot> controlledSlots,
        IReadOnlyCollection<byte> slotsToAdvance,
        IReadOnlyDictionary<byte, PlayerInputSnapshot> cachedInputs)
    {
        var inputs = new Dictionary<byte, PlayerInputSnapshot>(slotsToAdvance.Count);
        foreach (var slot in slotsToAdvance)
        {
            if (!controlledSlots.TryGetValue(slot, out var controlledSlot)
                || !cachedInputs.TryGetValue(slot, out var cachedInput)
                || !world.TryGetNetworkPlayer(slot, out var player)
                || !_controllersBySlot.TryGetValue(slot, out var controller))
            {
                continue;
            }

            if (controller.TryAdvanceCachedNavigation(
                    player,
                    world,
                    controlledSlot.Team,
                    cachedInput,
                    out var input))
            {
                inputs[slot] = input;
            }
        }

        return inputs;
    }

    private BotControllerDiagnosticsSnapshot BuildDiagnostics(
        SimulationWorld world,
        IReadOnlyDictionary<byte, ControlledBotSlot> controlledSlots)
    {
        if (!CollectDiagnostics)
        {
            return BotControllerDiagnosticsSnapshot.Empty;
        }

        _diagnosticEntries.Clear();
        if (_diagnosticEntries.Capacity < controlledSlots.Count)
        {
            _diagnosticEntries.Capacity = controlledSlots.Count;
        }

        var aliveBotCount = 0;
        var visibleEnemyCount = 0;
        var healFocusCount = 0;
        foreach (var (slot, controlledSlot) in controlledSlots)
        {
            if (!_controllersBySlot.TryGetValue(slot, out var controller)
                || !world.TryGetNetworkPlayer(slot, out var player))
            {
                continue;
            }

            var alive = player.IsAlive;
            if (alive)
            {
                aliveBotCount += 1;
            }

            if (controller.LastCombatTarget is not null)
            {
                visibleEnemyCount += 1;
            }

            if (controller.LastMedicHealTargetId.HasValue)
            {
                healFocusCount += 1;
            }

            var pathIndex = controller.CurrentPathIndex;
            var hasObjectiveRoute = alive && controller.HasActivePath;
            var state = hasObjectiveRoute
                ? BotStateKind.TravelObjective
                : alive
                    ? BotStateKind.None
                    : BotStateKind.Respawning;
            var focusKind = hasObjectiveRoute ? BotFocusKind.Objective : BotFocusKind.None;
            var steering = controller.LastSteeringOutput;
            var moveDebug = hasObjectiveRoute
                ? $"exec={(pathIndex > 0 ? "ExecuteLinkProgram" : "AttachRoute")} link={Math.Max(0, pathIndex)}"
                : string.Empty;
            var goal = controller.CurrentGoalPosition;
            var currentPoint = controller.CurrentPathNode;
            _diagnosticEntries.Add(new BotControllerDiagnosticsEntry(
                Slot: slot,
                DisplayName: $"bot-{slot}",
                Team: controlledSlot.Team,
                ClassId: controlledSlot.ClassId,
                Role: BotRole.None,
                State: state,
                FocusKind: focusKind,
                FocusLabel: focusKind == BotFocusKind.Objective ? "objective" : string.Empty,
                RouteLabel: hasObjectiveRoute ? "alpha" : string.Empty,
                HasVisibleEnemy: controller.LastCombatTarget is not null,
                Health: player.Health,
                MaxHealth: player.MaxHealth,
                StuckTicks: 0,
                ModernStuckTicks: 0,
                UnstickTicks: 0,
                CurrentPointId: currentPoint,
                NextPointId: -1,
                NextPoint2Id: -1,
                MovementTargetX: goal.X,
                MovementTargetY: goal.Y,
                RequestedHorizontal: Math.Sign(steering.MoveDirection),
                MoveDebug: moveDebug,
                RequestedJump: steering.Jump,
                JumpDebug: steering.Jump ? "route" : string.Empty,
                RouteGoalNodeId: controller.CurrentGoalNode,
                RouteGoalX: goal.X,
                RouteGoalY: goal.Y,
                PreviousCurrentPointId: -1,
                PreviousNextPointId: -1,
                IsGrounded: player.IsGrounded,
                ProbeGrounded: player.IsGrounded,
                SecondAnchorBlockPointId: -1,
                SecondAnchorBlockTicksRemaining: 0,
                NoNextPointTicks: 0,
                FallbackRouteLabel: string.Empty,
                FallbackTriggerLabel: string.Empty,
                NavigationIssueLabel: string.Empty,
                BranchFromPointId: -1,
                BranchToPointId: -1,
                BranchTicks: 0,
                BranchNoProgressTicks: 0,
                DirectTargetTicks: 0,
                DirectTargetNoProgressTicks: 0));
        }

        return new BotControllerDiagnosticsSnapshot(
            _diagnosticEntries,
            aliveBotCount,
            visibleEnemyCount,
            healFocusCount,
            CabinetSeekCount: 0,
            UnstickCount: 0);
    }

    private BotThinkWorkItem[] BuildBotThinkWorkItems(
        SimulationWorld world,
        IReadOnlyDictionary<byte, ControlledBotSlot> controlledSlots,
        IReadOnlyCollection<byte> slotsToThink)
    {
        var workItems = new List<BotThinkWorkItem>(slotsToThink.Count);
        foreach (var slot in slotsToThink)
        {
            if (!controlledSlots.TryGetValue(slot, out var controlledSlot))
            {
                continue;
            }

            if (!world.TryGetNetworkPlayer(slot, out var player))
            {
                continue;
            }

            if (!_controllersBySlot.TryGetValue(slot, out var controller))
            {
                controller = new BotBrainController(_disableShippedNavigationGraphs);
                controller.PreferEnemyPlayerObjective = controlledSlot.PreferEnemyPlayerObjective;
                _controllersBySlot[slot] = controller;
                _configuredSlots[slot] = controlledSlot;
            }
            else if (controller.PreferEnemyPlayerObjective != controlledSlot.PreferEnemyPlayerObjective)
            {
                controller.Reset();
                controller.PreferEnemyPlayerObjective = controlledSlot.PreferEnemyPlayerObjective;
                _configuredSlots[slot] = controlledSlot;
            }

            workItems.Add(new BotThinkWorkItem(slot, controlledSlot, player, controller));
        }

        return workItems.Count == 0 ? Array.Empty<BotThinkWorkItem>() : [.. workItems];
    }

    private static BotThinkResult[] BuildBotThinkResults(
        SimulationWorld world,
        BotThinkWorkItem[] workItems,
        IReadOnlyDictionary<byte, PlayerTeam> controlledTeamsBySlot)
    {
        var results = new BotThinkResult[workItems.Length];
        if (!EnableParallelBotThinkForDiagnostics)
        {
            for (var index = 0; index < workItems.Length; index += 1)
            {
                results[index] = ThinkForBot(world, workItems[index], controlledTeamsBySlot);
            }

            return results;
        }

        // This is an opt-in diagnostic comparison path. The default is the
        // deterministic sequential full-roster pass: launching a Parallel.For
        // for every simulation tick creates thread-pool scheduling and GC
        // contention that is more expensive than the warmed brain work itself.
        // Chat/input application remains ordered below, and no combat decision
        // or input is changed by the scheduling choice.
        Parallel.For(
            0,
            workItems.Length,
            index =>
            {
                results[index] = ThinkForBot(world, workItems[index], controlledTeamsBySlot);
            });

        return results;
    }

    private static BotThinkResult ThinkForBot(
        SimulationWorld world,
        BotThinkWorkItem workItem,
        IReadOnlyDictionary<byte, PlayerTeam> controlledTeamsBySlot)
    {
        var startTimestamp = BotThinkSpikeTracingEnabled ? Stopwatch.GetTimestamp() : 0L;
        // A scheduled think is always the complete brain pass.  In
        // particular, an active top-down route must not turn this into a
        // navigation-only heartbeat or the controller will stop refreshing
        // combat target selection and fire decisions indefinitely.  The
        // scheduler advances stale cached routes separately through
        // AdvanceCachedNavigationForSlots.
        var input = workItem.Controller.Think(
            workItem.Player,
            world,
            workItem.ControlledSlot.Team,
            controlledTeamsBySlot);
        if (BotThinkSpikeTracingEnabled)
        {
            var elapsedMilliseconds = (Stopwatch.GetTimestamp() - startTimestamp) * 1000d / Stopwatch.Frequency;
            if (elapsedMilliseconds >= BotThinkSpikeTraceThresholdMilliseconds)
            {
                WriteBotThinkSpikeTrace(workItem, elapsedMilliseconds);
            }
        }

        return new BotThinkResult(
            true,
            workItem.Slot,
            workItem.ControlledSlot.Team,
            workItem.Player,
            workItem.Controller,
            input);
    }

    private static void WriteBotThinkSpikeTrace(BotThinkWorkItem workItem, double elapsedMilliseconds)
    {
        if (string.IsNullOrWhiteSpace(BotThinkSpikeTracePath))
        {
            return;
        }

        var player = workItem.Player;
        var controller = workItem.Controller;
        var line = string.Create(
            CultureInfo.InvariantCulture,
            $"{DateTime.Now:O} slot={workItem.Slot} team={workItem.ControlledSlot.Team} class={workItem.ControlledSlot.ClassId} " +
            $"elapsedMs={elapsedMilliseconds:F1} pos=({player.X:F1},{player.Y:F1}) grounded={player.IsGrounded} " +
            $"pathNode={controller.CurrentPathNode} pathIndex={controller.CurrentPathIndex} pathCount={controller.CurrentPathCount} " +
            $"goalNode={controller.CurrentGoalNode} direct=\"{controller.LastDirectDriveTrace}\" objective=\"{controller.LastObjectiveTapeTrace}\" proof=\"{controller.LastProofGraphTrace}\" " +
            $"timing=\"{controller.LastThinkTimingTrace}\"{Environment.NewLine}");
        lock (BotThinkTraceSync)
        {
            File.AppendAllText(BotThinkSpikeTracePath, line);
        }
    }
}

public readonly record struct BotBrainPracticeBotRuntimeSnapshot(
    int ActiveControllerCount,
    int NavigationLoadedCount,
    int NavigationMissingCount,
    int ObjectiveTapeLoadedCount,
    int ActivePathCount);
