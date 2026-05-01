using OpenGarrison.Core;

namespace OpenGarrison.BotAI;

public sealed partial class ModernPracticeBotController
{
    private const float LocalMotionNoProgressImprovementDistance = 1f;
    private const float LocalMotionNoMovementDistance = 2f;
    private const float LocalMotionGoalDistanceEpsilon = 1f;
    private const float LocalMotionInertFastForwardSeconds = 0.3f;
    private const float WaterwayTerminalProgramStartX = 1000.2f;
    private const float WaterwayTerminalProgramStartBottom = 1194f;
    private const float WaterwayTerminalProgramGoalX = 976.2f;
    private const float WaterwayTerminalProgramGoalBottom = 1044f;
    private const float WaterwayTerminalProgramStartToleranceX = 56f;
    private const float WaterwayTerminalProgramStartToleranceY = 48f;
    private const float WaterwayAttackProgramStartX = 918f;
    private const float WaterwayAttackProgramStartBottom = 1194f;
    private const float WaterwayAttackProgramGoalBottom = 1044f;
    private const float WaterwayAttackProgramStartToleranceX = 18f;
    private const float WaterwayAttackProgramStartToleranceY = 18f;
    private const float WaterwayReturnProgramStartX = 1031f;
    private const float WaterwayReturnProgramStartBottom = 1044f;
    private const float WaterwayReturnProgramGoalX = 1812f;
    private const float WaterwayReturnProgramGoalBottom = 1062f;
    private const float WaterwayReturnProgramStartToleranceX = 18f;
    private const float WaterwayReturnProgramStartToleranceY = 18f;
    private const float WaterwayCapProgramStartX = 5276.7f;
    private const float WaterwayCapProgramStartBottom = 1194f;
    private const float WaterwayCapProgramGoalX = 5285.3f;
    private const float WaterwayCapProgramGoalBottom = 1044f;
    private const float WaterwayCapProgramStartToleranceX = 18f;
    private const float WaterwayCapProgramStartToleranceY = 24f;
    private const float WaterwayHeavyCapProgramStartX = 5351f;
    private const float WaterwayHeavyCapProgramStartBottom = 1194f;
    private const float WaterwayHeavyCapProgramStartToleranceX = 18f;
    private const float WaterwayHeavyCapProgramStartToleranceY = 24f;
    private const float WaterwayPyroCapProgramStartX = 5351f;
    private const float WaterwayPyroCapProgramStartBottom = 1188f;
    private const float WaterwayPyroCapProgramStartToleranceX = 18f;
    private const float WaterwayPyroCapProgramStartToleranceY = 24f;

    private static readonly LocalMotionProgram WaterwayBlueScoutTerminalProgram = new(
        "waterway_terminal_probe",
        new[]
        {
            new LocalMotionStep(LocalMotionStepKind.Run, Direction: -1, Ticks: 14),
            new LocalMotionStep(LocalMotionStepKind.Drop, Direction: -1, Ticks: 7),
            new LocalMotionStep(LocalMotionStepKind.Jump, Direction: 1, Ticks: 19),
            new LocalMotionStep(LocalMotionStepKind.Run, Direction: 1, Ticks: 5),
        },
        DurationTicks: 60,
        NoProgressTicks: 28,
        CooldownTicks: 90,
        AllowInertFastForward: true,
        GoalWindow: new LocalMotionGoalWindow(WaterwayTerminalProgramGoalX, WaterwayTerminalProgramGoalBottom, 28f, 28f, "waterway_terminal_probe_goal"));

    private static readonly LocalMotionProgram WaterwayBluePyroAttackProgram = new(
        "waterway_pyro_attack_probe",
        new[]
        {
            new LocalMotionStep(LocalMotionStepKind.Run, Direction: -1, Ticks: 14),
            new LocalMotionStep(LocalMotionStepKind.Jump, Direction: 1, Ticks: 20),
            new LocalMotionStep(LocalMotionStepKind.Run, Direction: 1, Ticks: 8),
        },
        DurationTicks: 56,
        NoProgressTicks: 28,
        CooldownTicks: 90,
        AllowInertFastForward: true,
        GoalWindow: new LocalMotionGoalWindow(973.5f, WaterwayAttackProgramGoalBottom, 28f, 24f, "waterway_pyro_attack_probe_goal"));

    private static readonly LocalMotionProgram WaterwayBlueHeavyAttackProgram = new(
        "waterway_heavy_attack_probe",
        new[]
        {
            new LocalMotionStep(LocalMotionStepKind.Run, Direction: -1, Ticks: 14),
            new LocalMotionStep(LocalMotionStepKind.Jump, Direction: 1, Ticks: 17),
            new LocalMotionStep(LocalMotionStepKind.Run, Direction: 1, Ticks: 13),
        },
        DurationTicks: 60,
        NoProgressTicks: 28,
        CooldownTicks: 90,
        AllowInertFastForward: true,
        GoalWindow: new LocalMotionGoalWindow(971.1f, WaterwayAttackProgramGoalBottom, 28f, 24f, "waterway_heavy_attack_probe_goal"));

    private static readonly LocalMotionProgram WaterwayBlueScoutReturnProgram = new(
        "waterway_return_probe",
        new[]
        {
            new LocalMotionStep(LocalMotionStepKind.Jump, Direction: 1, Ticks: 17),
            new LocalMotionStep(LocalMotionStepKind.Run, Direction: 1, Ticks: 52),
            new LocalMotionStep(LocalMotionStepKind.Run, Direction: 1, Ticks: 34),
            new LocalMotionStep(LocalMotionStepKind.Run, Direction: 1, Ticks: 8),
            new LocalMotionStep(LocalMotionStepKind.Run, Direction: -1, Ticks: 11),
        },
        DurationTicks: 150,
        NoProgressTicks: 48,
        CooldownTicks: 60,
        AllowInertFastForward: true,
        GoalWindow: new LocalMotionGoalWindow(WaterwayReturnProgramGoalX, WaterwayReturnProgramGoalBottom, 40f, 40f, "waterway_return_probe_goal"));

    private static readonly LocalMotionProgram WaterwayBlueHeavyReturnProgram = new(
        "waterway_heavy_return_probe",
        new[]
        {
            new LocalMotionStep(LocalMotionStepKind.Jump, Direction: 1, Ticks: 17),
            new LocalMotionStep(LocalMotionStepKind.Run, Direction: 1, Ticks: 14),
            new LocalMotionStep(LocalMotionStepKind.Drop, Direction: 1, Ticks: 12),
            new LocalMotionStep(LocalMotionStepKind.Jump, Direction: 1, Ticks: 17),
            new LocalMotionStep(LocalMotionStepKind.Run, Direction: 1, Ticks: 34),
            new LocalMotionStep(LocalMotionStepKind.Run, Direction: 1, Ticks: 34),
            new LocalMotionStep(LocalMotionStepKind.Run, Direction: 1, Ticks: 52),
            new LocalMotionStep(LocalMotionStepKind.Jump, Direction: 0, Ticks: 24),
        },
        DurationTicks: 230,
        NoProgressTicks: 56,
        CooldownTicks: 60,
        AllowInertFastForward: true,
        GoalWindow: new LocalMotionGoalWindow(WaterwayReturnProgramGoalX, WaterwayReturnProgramGoalBottom, 48f, 48f, "waterway_heavy_return_probe_goal"));

    private static readonly LocalMotionProgram WaterwayBlueScoutCapProgram = new(
        "waterway_cap_probe",
        new[]
        {
            new LocalMotionStep(LocalMotionStepKind.Jump, Direction: 1, Ticks: 20),
            new LocalMotionStep(LocalMotionStepKind.Jump, Direction: -1, Ticks: 17),
            new LocalMotionStep(LocalMotionStepKind.Run, Direction: -1, Ticks: 8),
        },
        DurationTicks: 64,
        NoProgressTicks: 28,
        CooldownTicks: 60,
        AllowInertFastForward: true,
        GoalWindow: new LocalMotionGoalWindow(WaterwayCapProgramGoalX, WaterwayCapProgramGoalBottom, 24f, 24f, "waterway_cap_probe_goal"));

    private static readonly LocalMotionProgram WaterwayBlueHeavyCapProgram = new(
        "waterway_heavy_cap_probe",
        new[]
        {
            new LocalMotionStep(LocalMotionStepKind.Run, Direction: 1, Ticks: 14),
            new LocalMotionStep(LocalMotionStepKind.Jump, Direction: -1, Ticks: 17),
            new LocalMotionStep(LocalMotionStepKind.Run, Direction: -1, Ticks: 12),
        },
        DurationTicks: 72,
        NoProgressTicks: 28,
        CooldownTicks: 60,
        AllowInertFastForward: true,
        GoalWindow: new LocalMotionGoalWindow(WaterwayCapProgramGoalX, WaterwayCapProgramGoalBottom, 24f, 24f, "waterway_heavy_cap_probe_goal"));

    private static readonly LocalMotionProgram WaterwayBluePyroCapProgram = new(
        "waterway_pyro_cap_probe",
        new[]
        {
            new LocalMotionStep(LocalMotionStepKind.Run, Direction: 1, Ticks: 14),
            new LocalMotionStep(LocalMotionStepKind.Jump, Direction: -1, Ticks: 20),
            new LocalMotionStep(LocalMotionStepKind.Run, Direction: -1, Ticks: 8),
        },
        DurationTicks: 72,
        NoProgressTicks: 28,
        CooldownTicks: 60,
        AllowInertFastForward: true,
        GoalWindow: new LocalMotionGoalWindow(WaterwayCapProgramGoalX, WaterwayCapProgramGoalBottom, 24f, 24f, "waterway_pyro_cap_probe_goal"));

    private static void ResetLocalMotionFrameState(BotMemory memory)
    {
        memory.LocalMotionRequestedDown = false;
    }

    private static void BeginLocalMotionProgram(BotMemory memory, LocalMotionProgram program)
    {
        memory.ActiveLocalMotionProgram = program;
        memory.LocalMotionProgramStepIndex = 0;
        memory.LocalMotionProgramStepTick = 0;
        memory.LocalMotionProgramTicksRemaining = Math.Max(1, program.DurationTicks);
        memory.LocalMotionProgramNoProgressTicks = 0;
        memory.LocalMotionProgramNoMovementTicks = 0;
        memory.LocalMotionProgramBestDistance = float.PositiveInfinity;
        memory.LocalMotionProgramLastX = float.NaN;
        memory.LocalMotionProgramLastBottom = float.NaN;
        memory.LocalMotionRequestedDown = false;
    }

    private static void ClearLocalMotionProgram(BotMemory memory)
    {
        memory.ActiveLocalMotionProgram = null;
        memory.LocalMotionProgramStepIndex = 0;
        memory.LocalMotionProgramStepTick = 0;
        memory.LocalMotionProgramTicksRemaining = 0;
        memory.LocalMotionProgramNoProgressTicks = 0;
        memory.LocalMotionProgramNoMovementTicks = 0;
        memory.LocalMotionProgramBestDistance = float.PositiveInfinity;
        memory.LocalMotionProgramLastX = float.NaN;
        memory.LocalMotionProgramLastBottom = float.NaN;
        memory.LocalMotionRequestedDown = false;
    }

    private static bool IsLocalMotionProgramActive(BotMemory memory)
    {
        return memory.ActiveLocalMotionProgram is { Steps.Count: > 0 }
            && memory.LocalMotionProgramTicksRemaining > 0
            && memory.LocalMotionProgramStepIndex >= 0
            && memory.LocalMotionProgramStepIndex < memory.ActiveLocalMotionProgram.Steps.Count;
    }

    private static bool TryGetCurrentLocalMotionStep(BotMemory memory, out LocalMotionProgram program, out LocalMotionStep step)
    {
        if (!IsLocalMotionProgramActive(memory))
        {
            program = null!;
            step = null!;
            return false;
        }

        program = memory.ActiveLocalMotionProgram!;
        step = program.Steps[memory.LocalMotionProgramStepIndex];
        return true;
    }

    private static int GetLocalMotionFastForwardTicks(SimulationWorld world)
    {
        return Math.Max(1, (int)MathF.Ceiling(world.Config.TicksPerSecond * LocalMotionInertFastForwardSeconds));
    }

    private static bool IsInertLocalMotionStep(LocalMotionStep step)
    {
        return step.Kind == LocalMotionStepKind.Idle
            || (step.Direction == 0
                && step.Kind is not LocalMotionStepKind.Jump
                && step.Kind is not LocalMotionStepKind.Drop);
    }

    private static bool IsLocalMotionGoalReached(PlayerEntity player, LocalMotionProgram program)
    {
        if (program.GoalWindow is null)
        {
            return false;
        }

        var goal = program.GoalWindow;
        return MathF.Abs(player.X - goal.X) <= goal.RadiusX
            && MathF.Abs(player.Bottom - goal.Y) <= goal.RadiusY;
    }

    private static float GetLocalMotionGoalDistance(PlayerEntity player, LocalMotionProgram program)
    {
        if (program.GoalWindow is null)
        {
            return float.PositiveInfinity;
        }

        var goal = program.GoalWindow;
        return DistanceBetween(player.X, player.Bottom, goal.X, goal.Y);
    }

    private static float GetTraverseStepDistance(PlayerEntity player, LocalMotionStep step)
    {
        return DistanceBetween(player.X, player.Bottom, step.TargetX, step.TargetY);
    }

    private static void TrackLocalMotionMovement(PlayerEntity player, BotMemory memory)
    {
        if (float.IsNaN(memory.LocalMotionProgramLastX) || float.IsNaN(memory.LocalMotionProgramLastBottom))
        {
            memory.LocalMotionProgramLastX = player.X;
            memory.LocalMotionProgramLastBottom = player.Bottom;
            memory.LocalMotionProgramNoMovementTicks = 0;
            return;
        }

        var moved = DistanceBetween(player.X, player.Bottom, memory.LocalMotionProgramLastX, memory.LocalMotionProgramLastBottom);
        memory.LocalMotionProgramNoMovementTicks = moved >= LocalMotionNoMovementDistance
            ? 0
            : memory.LocalMotionProgramNoMovementTicks + 1;
        memory.LocalMotionProgramLastX = player.X;
        memory.LocalMotionProgramLastBottom = player.Bottom;
    }

    private static void TrackLocalMotionDistance(float distance, BotMemory memory)
    {
        if (!float.IsFinite(distance))
        {
            memory.LocalMotionProgramNoProgressTicks += 1;
            return;
        }

        if (distance + LocalMotionNoProgressImprovementDistance < memory.LocalMotionProgramBestDistance)
        {
            memory.LocalMotionProgramBestDistance = distance;
            memory.LocalMotionProgramNoProgressTicks = 0;
            return;
        }

        if (MathF.Abs(distance - memory.LocalMotionProgramBestDistance) <= LocalMotionGoalDistanceEpsilon)
        {
            memory.LocalMotionProgramNoProgressTicks += 1;
            return;
        }

        memory.LocalMotionProgramNoProgressTicks += 1;
    }

    private static bool AdvanceLocalMotionProgramStep(BotMemory memory, PlayerEntity player)
    {
        if (!TryGetCurrentLocalMotionStep(memory, out var program, out _))
        {
            return false;
        }

        memory.LocalMotionProgramStepIndex += 1;
        memory.LocalMotionProgramStepTick = 0;
        memory.LocalMotionProgramNoMovementTicks = 0;
        memory.LocalMotionProgramLastX = player.X;
        memory.LocalMotionProgramLastBottom = player.Bottom;
        if (memory.LocalMotionProgramStepIndex >= program.Steps.Count)
        {
            ClearLocalMotionProgram(memory);
            return false;
        }

        return true;
    }

    private static bool TryFastForwardLocalMotionProgram(SimulationWorld world, PlayerEntity player, BotMemory memory)
    {
        if (!TryGetCurrentLocalMotionStep(memory, out var program, out var step)
            || !program.AllowInertFastForward
            || !IsInertLocalMotionStep(step)
            || memory.LocalMotionProgramNoMovementTicks < GetLocalMotionFastForwardTicks(world))
        {
            return false;
        }

        while (IsLocalMotionProgramActive(memory)
            && TryGetCurrentLocalMotionStep(memory, out _, out step)
            && IsInertLocalMotionStep(step))
        {
            if (!AdvanceLocalMotionProgramStep(memory, player))
            {
                return false;
            }
        }

        memory.LocalMotionProgramNoProgressTicks = 0;
        memory.LocalMotionProgramNoMovementTicks = 0;
        return IsLocalMotionProgramActive(memory);
    }

    private static bool TryStartMapScopedLocalMotionProgram(
        SimulationWorld world,
        PlayerEntity player,
        ModernPathSelection objectiveSelection,
        BotMemory memory)
    {
        _ = objectiveSelection;

        if (IsLocalMotionProgramActive(memory)
            || memory.LocalMotionProgramCooldownTicks > 0
            || !string.Equals(world.Level.Name, "Waterway", StringComparison.OrdinalIgnoreCase)
            || (player.ClassId != PlayerClass.Scout
                && player.ClassId != PlayerClass.Pyro
                && player.ClassId != PlayerClass.Heavy))
        {
            return false;
        }

        if (player.ClassId == PlayerClass.Scout
            && player.IsCarryingIntel)
        {
            if (MathF.Abs(player.X - WaterwayCapProgramStartX) <= WaterwayCapProgramStartToleranceX
                && MathF.Abs(player.Bottom - WaterwayCapProgramStartBottom) <= WaterwayCapProgramStartToleranceY)
            {
                BeginLocalMotionProgram(memory, WaterwayBlueScoutCapProgram);
                memory.ModernMoveDebug = "waterway_cap_probe:start";
                memory.ModernJumpDebug = "waterway_cap_probe:start";
                return true;
            }

            if (MathF.Abs(player.X - WaterwayReturnProgramStartX) > WaterwayReturnProgramStartToleranceX
                || MathF.Abs(player.Bottom - WaterwayReturnProgramStartBottom) > WaterwayReturnProgramStartToleranceY)
            {
                return false;
            }

            BeginLocalMotionProgram(memory, WaterwayBlueScoutReturnProgram);
            memory.ModernMoveDebug = "waterway_return_probe:start";
            memory.ModernJumpDebug = "waterway_return_probe:start";
            return true;
        }

        if (player.ClassId == PlayerClass.Heavy
            && player.IsCarryingIntel)
        {
            if (MathF.Abs(player.X - WaterwayHeavyCapProgramStartX) <= WaterwayHeavyCapProgramStartToleranceX
                && MathF.Abs(player.Bottom - WaterwayHeavyCapProgramStartBottom) <= WaterwayHeavyCapProgramStartToleranceY)
            {
                BeginLocalMotionProgram(memory, WaterwayBlueHeavyCapProgram);
                memory.ModernMoveDebug = "waterway_heavy_cap_probe:start";
                memory.ModernJumpDebug = "waterway_heavy_cap_probe:start";
                return true;
            }

            if (MathF.Abs(player.X - WaterwayReturnProgramStartX) > WaterwayReturnProgramStartToleranceX
                || MathF.Abs(player.Bottom - WaterwayReturnProgramStartBottom) > WaterwayReturnProgramStartToleranceY)
            {
                return false;
            }

            BeginLocalMotionProgram(memory, WaterwayBlueHeavyReturnProgram);
            memory.ModernMoveDebug = "waterway_heavy_return_probe:start";
            memory.ModernJumpDebug = "waterway_heavy_return_probe:start";
            return true;
        }

        if (player.ClassId == PlayerClass.Pyro
            && player.IsCarryingIntel)
        {
            if (MathF.Abs(player.X - WaterwayPyroCapProgramStartX) > WaterwayPyroCapProgramStartToleranceX
                || MathF.Abs(player.Bottom - WaterwayPyroCapProgramStartBottom) > WaterwayPyroCapProgramStartToleranceY)
            {
                return false;
            }

            BeginLocalMotionProgram(memory, WaterwayBluePyroCapProgram);
            memory.ModernMoveDebug = "waterway_pyro_cap_probe:start";
            memory.ModernJumpDebug = "waterway_pyro_cap_probe:start";
            return true;
        }

        if (player.ClassId == PlayerClass.Pyro
            || player.ClassId == PlayerClass.Heavy)
        {
            if (player.IsCarryingIntel
                || MathF.Abs(player.X - WaterwayAttackProgramStartX) > WaterwayAttackProgramStartToleranceX
                || MathF.Abs(player.Bottom - WaterwayAttackProgramStartBottom) > WaterwayAttackProgramStartToleranceY)
            {
                return false;
            }

            BeginLocalMotionProgram(
                memory,
                player.ClassId == PlayerClass.Pyro
                    ? WaterwayBluePyroAttackProgram
                    : WaterwayBlueHeavyAttackProgram);
            memory.ModernMoveDebug = player.ClassId == PlayerClass.Pyro
                ? "waterway_pyro_attack_probe:start"
                : "waterway_heavy_attack_probe:start";
            memory.ModernJumpDebug = memory.ModernMoveDebug;
            return true;
        }

        if (MathF.Abs(player.X - WaterwayTerminalProgramStartX) > WaterwayTerminalProgramStartToleranceX
            || MathF.Abs(player.Bottom - WaterwayTerminalProgramStartBottom) > WaterwayTerminalProgramStartToleranceY)
        {
            return false;
        }

        BeginLocalMotionProgram(memory, WaterwayBlueScoutTerminalProgram);
        memory.ModernMoveDebug = "waterway_terminal_probe:start";
        memory.ModernJumpDebug = "waterway_terminal_probe:start";
        return true;
    }

    private static bool TryResolveActiveLocalMotionMovement(
        SimulationWorld world,
        PlayerEntity player,
        ClientBotNavPoints navPoints,
        BotMemory memory,
        out int horizontal)
    {
        horizontal = 0;
        if (!TryGetCurrentLocalMotionStep(memory, out var program, out var step))
        {
            return false;
        }

        if (step.Kind == LocalMotionStepKind.TraverseToNode)
        {
            return TryResolveTraverseNodeLocalMotionMovement(player, navPoints, memory, step, out horizontal);
        }

        if (IsLocalMotionGoalReached(player, program))
        {
            memory.ModernMoveDebug = $"{program.Label}:goal";
            ClearLocalMotionProgram(memory);
            return false;
        }

        TrackLocalMotionMovement(player, memory);
        TrackLocalMotionDistance(GetLocalMotionGoalDistance(player, program), memory);
        if (TryFastForwardLocalMotionProgram(world, player, memory)
            && TryGetCurrentLocalMotionStep(memory, out program, out step))
        {
            if (step.Kind == LocalMotionStepKind.TraverseToNode)
            {
                return TryResolveTraverseNodeLocalMotionMovement(player, navPoints, memory, step, out horizontal);
            }
        }

        memory.LocalMotionProgramTicksRemaining -= 1;
        if (memory.LocalMotionProgramTicksRemaining <= 0
            || memory.LocalMotionProgramNoProgressTicks >= program.NoProgressTicks)
        {
            var failLabel = $"{program.Label}:fail";
            ClearLocalMotionProgram(memory);
            memory.LocalMotionProgramCooldownTicks = Math.Max(memory.LocalMotionProgramCooldownTicks, program.CooldownTicks);
            memory.NavigationIssueLabel = failLabel;
            memory.ModernMoveDebug = failLabel;
            return false;
        }

        horizontal = step.Direction < 0 ? -1 : step.Direction > 0 ? 1 : 0;
        memory.LocalMotionRequestedDown = step.Kind == LocalMotionStepKind.Drop;
        memory.DoublebackActive = false;
        memory.ModernMoveDebug =
            $"{program.Label}:step{memory.LocalMotionProgramStepIndex}:{step.Kind}:dir{step.Direction}:tick{memory.LocalMotionProgramStepTick}/{Math.Max(1, step.Ticks)}";
        return true;
    }

    private static bool TryResolveTraverseNodeLocalMotionMovement(
        PlayerEntity player,
        ClientBotNavPoints navPoints,
        BotMemory memory,
        LocalMotionStep step,
        out int horizontal)
    {
        horizontal = 0;
        if (memory.CurrentPointId != memory.ModernChainExecutorCurrentPointId
            || memory.NextPointId != memory.ModernChainExecutorTargetPointId
            || step.TargetPointId < 0
            || !navPoints.TryGetPoint(step.TargetPointId, out var targetNode)
            || memory.NextPointId != targetNode.Id)
        {
            var staleLabel =
                $"chain_stale_move:cp{memory.CurrentPointId}:np{memory.NextPointId}:exec{memory.ModernChainExecutorCurrentPointId}->{memory.ModernChainExecutorTargetPointId}";
            memory.ModernMoveDebug = staleLabel;
            memory.NavigationIssueLabel = staleLabel;
            ClearModernChainExecutor(memory);
            return false;
        }

        var targetFeetY = step.TargetY;
        var dx = player.X - targetNode.X;
        var dy = player.Bottom - targetFeetY;
        if (MathF.Abs(dx) <= ModernChainExecutorArrivalDistanceX
            && MathF.Abs(dy) <= ModernChainExecutorArrivalDistanceY)
        {
            CompleteModernChainExecutorSegment(navPoints, memory, targetNode);
            AdvanceLocalMotionProgramStep(memory, player);
            memory.DoublebackActive = false;
            memory.ModernMoveDebug = $"chain_success:np{targetNode.Id}";
            return true;
        }

        var distance = GetTraverseStepDistance(player, step);
        TrackLocalMotionDistance(distance, memory);
        memory.LocalMotionProgramTicksRemaining -= 1;
        if (memory.LocalMotionProgramTicksRemaining <= 0
            || memory.LocalMotionProgramNoProgressTicks >= ModernChainExecutorNoProgressTicks)
        {
            var failedCurrentPointId = memory.CurrentPointId;
            var failedTargetPointId = memory.NextPointId;
            ClearModernChainExecutor(memory);
            memory.ModernChainExecutorCooldownTicks = ModernChainExecutorCooldownTicks;
            memory.CurrentPointId = -1;
            memory.NextPointId = -1;
            memory.NextPoint2Id = -1;
            memory.NextPoint3Id = -1;
            memory.DoublebackActive = false;
            memory.NavigationIssueLabel = $"chain_fail:cp{failedCurrentPointId}:np{failedTargetPointId}:d{distance:0}";
            memory.ModernMoveDebug = memory.NavigationIssueLabel;
            return true;
        }

        var targetDirection = MathF.Sign(targetNode.X - player.X);
        if (MathF.Abs(targetNode.X - player.X) <= ModernChainExecutorArrivalDistanceX
            && TryGetModernSecondNextNode(navPoints, memory, out var secondNextNode))
        {
            targetDirection = MathF.Sign(secondNextNode.X - targetNode.X);
        }

        horizontal = targetDirection < 0f ? -1 : targetDirection > 0f ? 1 : 0;
        memory.DoublebackActive = false;
        memory.ModernMoveDebug =
            $"chain_exec:cp{memory.CurrentPointId}:np{targetNode.Id}:h{horizontal}:dx{-dx:0}:feet{dy:0}:t{memory.LocalMotionProgramTicksRemaining}";
        return true;
    }

    private static bool TryResolveActiveLocalMotionJump(
        SimulationWorld world,
        PlayerEntity player,
        ClientBotNavPoints navPoints,
        BotMemory memory,
        BotTimingProfile timing,
        bool hasGroundContact,
        bool canStartGroundJump,
        out bool jump)
    {
        jump = false;
        if (!TryGetCurrentLocalMotionStep(memory, out var program, out var step))
        {
            return false;
        }

        if (step.Kind == LocalMotionStepKind.TraverseToNode)
        {
            return TryResolveTraverseNodeLocalMotionJump(
                world,
                player,
                navPoints,
                memory,
                timing,
                hasGroundContact,
                canStartGroundJump,
                out jump);
        }

        jump = step.Kind == LocalMotionStepKind.Jump && memory.LocalMotionProgramStepTick == 0;
        memory.ModernJumpDebug =
            $"{program.Label}:step{memory.LocalMotionProgramStepIndex}:{step.Kind}:jump{(jump ? 1 : 0)}:tick{memory.LocalMotionProgramStepTick}/{Math.Max(1, step.Ticks)}";
        memory.LocalMotionProgramStepTick += 1;
        if (memory.LocalMotionProgramStepTick >= Math.Max(1, step.Ticks))
        {
            AdvanceLocalMotionProgramStep(memory, player);
        }

        return true;
    }

    private static bool TryResolveTraverseNodeLocalMotionJump(
        SimulationWorld world,
        PlayerEntity player,
        ClientBotNavPoints navPoints,
        BotMemory memory,
        BotTimingProfile timing,
        bool hasGroundContact,
        bool canStartGroundJump,
        out bool jump)
    {
        jump = false;
        if (!TryGetCurrentLocalMotionStep(memory, out _, out var step)
            || step.TargetPointId < 0
            || !navPoints.TryGetPoint(step.TargetPointId, out var targetNode)
            || memory.NextPointId != memory.ModernChainExecutorTargetPointId)
        {
            memory.ModernJumpDebug = "chain_stale";
            return true;
        }

        var targetFeetY = step.TargetY;
        var riseNeeded = player.Bottom - targetFeetY;
        if (!hasGroundContact)
        {
            memory.ModernJumpDebug = $"chain_air:rise{riseNeeded:0}";
            return true;
        }

        GetModernClassJumpProfile(player.ClassId, out _, out var jumpHeight, out var jumpHeightTotal);
        var graphRiseNeeded = riseNeeded;
        if (TryGetCurrentModernNode(navPoints, memory, out var currentNode))
        {
            graphRiseNeeded = GetModernNodeFeetY(navPoints, currentNode) - targetFeetY;
        }

        if (graphRiseNeeded > 8f
            && graphRiseNeeded <= jumpHeightTotal + ModernTerrainAssistedLiveRiseTolerance
            && HasModernJumpHeadClear(world, player, jumpHeight))
        {
            memory.ModernJumpDebug = $"chain_jump:rise{riseNeeded:0}:gr{graphRiseNeeded:0}";
            jump = TriggerModernGroundBotJump(memory, timing, canStartGroundJump);
            return true;
        }

        memory.ModernJumpDebug = $"chain_wait:rise{riseNeeded:0}:gr{graphRiseNeeded:0}";
        return true;
    }
}
