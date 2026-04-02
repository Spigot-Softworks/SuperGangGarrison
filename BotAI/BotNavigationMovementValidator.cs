using OpenGarrison.Core;
using System.Linq;

namespace OpenGarrison.BotAI;

internal static class BotNavigationMovementValidator
{
    private const double FixedDeltaSeconds = 1d / SimulationConfig.DefaultTicksPerSecond;
    private const float HorizontalAimDistance = 256f;
    private const float HorizontalProbeStep = 8f;
    private const float SearchEnvelopeHorizontalSafetyMargin = 40f;
    private const float SearchEnvelopeRiseSafetyMargin = 48f;
    private const float SearchEnvelopeDescentSafetyMargin = 36f;
    private const float LandingToleranceX = 24f;
    private const float LandingToleranceY = 12f;
    private const float GroundTraverseToleranceX = 24f;
    private const float GroundTraverseToleranceY = 18f;
    private const float HintLandingToleranceX = 36f;
    private const float HintLandingToleranceY = 18f;
    private const float FailureOvershootMargin = 80f;
    private const float FailureFallMargin = 140f;
    private const int GroundTraverseExtraTicks = 24;

    private static readonly int[] RunUpTickOptions = [0, 4, 8, 12, 16, 20];
    private static readonly int[] HintRunUpTickOptions = [0, 4, 8, 12, 16, 20, 24, 28, 32, 36, 40];
    private static readonly int[] LightAirborneTickOptions = [8, 12, 16, 20, 24, 28, 32, 36, 40];
    private static readonly int[] StandardAirborneTickOptions = [8, 12, 16, 20, 24, 28, 32, 36];
    private static readonly int[] HeavyAirborneTickOptions = [8, 12, 16, 20, 24, 28, 32];
    private static readonly int[] HintLightAirborneTickOptions = [8, 12, 16, 20, 24, 28, 32, 36, 40, 44, 48, 52, 56];
    private static readonly int[] HintStandardAirborneTickOptions = [8, 12, 16, 20, 24, 28, 32, 36, 40, 44, 48, 52];
    private static readonly int[] HintHeavyAirborneTickOptions = [8, 12, 16, 20, 24, 28, 32, 36, 40, 44, 48];

    public static JumpSearchEnvelope GetSearchEnvelope(BotNavigationProfile profile, CharacterClassDefinition classDefinition)
    {
        var gravity = MathF.Max(1f, classDefinition.Gravity);
        var jumpRise = (classDefinition.JumpSpeed * classDefinition.JumpSpeed) / (2f * gravity);
        var airTime = (2f * classDefinition.JumpSpeed) / gravity;
        var runUpSeconds = RunUpTickOptions[^1] / SimulationConfig.DefaultTicksPerSecond;
        var horizontalReach = (classDefinition.MaxRunSpeed * (airTime + runUpSeconds)) + SearchEnvelopeHorizontalSafetyMargin;
        var maxDescent = jumpRise + (profile == BotNavigationProfile.Heavy ? 28f : 44f);

        return new JumpSearchEnvelope(
            MaxHorizontalDistance: MathF.Max(96f, horizontalReach),
            // Real jumps in the discrete movement model can beat the simple
            // ballistic estimate thanks to step-up resolution and ledge snap.
            // Pad the envelope so the validator gets a chance to prove them.
            MaxRiseDistance: MathF.Max(72f, jumpRise + SearchEnvelopeRiseSafetyMargin),
            MaxDescentDistance: MathF.Max(72f, maxDescent + SearchEnvelopeDescentSafetyMargin));
    }

    public static bool TryBuildJumpTape(
        SimpleLevel level,
        CharacterClassDefinition classDefinition,
        BotNavigationProfile profile,
        float sourceX,
        float sourceY,
        float targetX,
        float targetY,
        out IReadOnlyList<BotNavigationInputFrame> tape,
        out float cost)
    {
        return TryBuildJumpTape(
            level,
            classDefinition,
            profile,
            sourceX,
            sourceY,
            targetX,
            targetY,
            PlayerTeam.Red,
            out tape,
            out cost);
    }

    public static bool TryBuildJumpTape(
        SimpleLevel level,
        CharacterClassDefinition classDefinition,
        BotNavigationProfile profile,
        float sourceX,
        float sourceY,
        float targetX,
        float targetY,
        PlayerTeam team,
        out IReadOnlyList<BotNavigationInputFrame> tape,
        out float cost)
    {
        return TryBuildJumpTapeInternal(
            level,
            classDefinition,
            profile,
            sourceX,
            sourceY,
            targetX,
            targetY,
            team,
            RunUpTickOptions,
            GetAirborneTickOptions(profile),
            LandingToleranceX,
            LandingToleranceY,
            requireGroundedArrival: true,
            out tape,
            out cost);
    }

    public static bool TryBuildHintJumpTape(
        SimpleLevel level,
        CharacterClassDefinition classDefinition,
        BotNavigationProfile profile,
        float sourceX,
        float sourceY,
        float targetX,
        float targetY,
        out IReadOnlyList<BotNavigationInputFrame> tape,
        out float cost)
    {
        return TryBuildHintJumpTape(
            level,
            classDefinition,
            profile,
            sourceX,
            sourceY,
            targetX,
            targetY,
            PlayerTeam.Red,
            requireGroundedArrival: true,
            out tape,
            out cost);
    }

    public static IReadOnlyList<BotNavigationInputFrame> BuildApproximateHintJumpTape(
        CharacterClassDefinition classDefinition,
        BotNavigationProfile profile,
        float sourceX,
        float sourceY,
        float targetX,
        float targetY)
    {
        var direction = targetX >= sourceX ? 1 : -1;
        var horizontalDistance = MathF.Abs(targetX - sourceX);
        var riseDistance = MathF.Max(0f, sourceY - targetY);
        var dropDistance = MathF.Max(0f, targetY - sourceY);
        var runUpTicks = PickNearestTickOption(
            HintRunUpTickOptions,
            (int)MathF.Round(((horizontalDistance / MathF.Max(1f, classDefinition.MaxRunSpeed)) * SimulationConfig.DefaultTicksPerSecond * 0.45f)
                + (riseDistance / MathF.Max(1f, classDefinition.JumpSpeed / 2f))));
        var airborneTicks = PickNearestTickOption(
            GetHintAirborneTickOptions(profile),
            (int)MathF.Round(
                10f
                + ((horizontalDistance / MathF.Max(1f, classDefinition.MaxRunSpeed)) * SimulationConfig.DefaultTicksPerSecond * 0.9f)
                + (riseDistance / 10f)
                - (dropDistance / 24f)));

        var tape = new List<BotNavigationInputFrame>(3);
        if (runUpTicks > 0)
        {
            tape.Add(CreateDirectionalFrame(direction, jump: false, runUpTicks));
        }

        tape.Add(CreateDirectionalFrame(direction, jump: true, ticks: 1));
        if (airborneTicks > 0)
        {
            tape.Add(CreateDirectionalFrame(direction, jump: false, airborneTicks));
        }

        return tape;
    }

    public static bool TryBuildHintJumpTape(
        SimpleLevel level,
        CharacterClassDefinition classDefinition,
        BotNavigationProfile profile,
        float sourceX,
        float sourceY,
        float targetX,
        float targetY,
        PlayerTeam team,
        out IReadOnlyList<BotNavigationInputFrame> tape,
        out float cost)
    {
        return TryBuildHintJumpTape(
            level,
            classDefinition,
            profile,
            sourceX,
            sourceY,
            targetX,
            targetY,
            team,
            requireGroundedArrival: true,
            out tape,
            out cost);
    }

    public static bool TryBuildHintJumpTape(
        SimpleLevel level,
        CharacterClassDefinition classDefinition,
        BotNavigationProfile profile,
        float sourceX,
        float sourceY,
        float targetX,
        float targetY,
        PlayerTeam team,
        bool requireGroundedArrival,
        out IReadOnlyList<BotNavigationInputFrame> tape,
        out float cost)
    {
        return TryBuildJumpTapeInternal(
            level,
            classDefinition,
            profile,
            sourceX,
            sourceY,
            targetX,
            targetY,
            team,
            HintRunUpTickOptions,
            GetHintAirborneTickOptions(profile),
            HintLandingToleranceX,
            HintLandingToleranceY,
            requireGroundedArrival,
            out tape,
            out cost);
    }

    public static bool TryBuildGroundTape(
        SimpleLevel level,
        CharacterClassDefinition classDefinition,
        float sourceX,
        float sourceY,
        float targetX,
        float targetY,
        out IReadOnlyList<BotNavigationInputFrame> tape,
        out float cost)
    {
        return TryBuildGroundTape(
            level,
            classDefinition,
            sourceX,
            sourceY,
            targetX,
            targetY,
            PlayerTeam.Red,
            out tape,
            out cost);
    }

    public static bool TryBuildGroundTape(
        SimpleLevel level,
        CharacterClassDefinition classDefinition,
        float sourceX,
        float sourceY,
        float targetX,
        float targetY,
        PlayerTeam team,
        out IReadOnlyList<BotNavigationInputFrame> tape,
        out float cost,
        out string failureReason)
    {
        tape = Array.Empty<BotNavigationInputFrame>();
        cost = 0f;
        failureReason = string.Empty;

        var horizontalDistance = MathF.Abs(targetX - sourceX);
        if (horizontalDistance <= 0.5f)
        {
            failureReason = "ground traverse needs distinct source and target anchors";
            return false;
        }

        var direction = targetX >= sourceX ? 1 : -1;
        var player = new PlayerEntity(id: 1, classDefinition, displayName: "nav-ground-validator");
        player.Spawn(team, sourceX, sourceY);
        player.ResolveBlockingOverlap(level, team);
        if (!player.IsAlive
            || MathF.Abs(player.X - sourceX) > 2f
            || MathF.Abs(player.Y - sourceY) > 2f)
        {
            failureReason = "ground traverse source anchor is not a clean standing position";
            return false;
        }

        var estimatedTicks = (int)MathF.Ceiling(horizontalDistance / MathF.Max(1f, classDefinition.MaxRunSpeed));
        var verticalAllowanceTicks = (int)MathF.Ceiling(MathF.Abs(targetY - sourceY) / 6f);
        var totalTicks = Math.Max(12, estimatedTicks + verticalAllowanceTicks + GroundTraverseExtraTicks);
        var maxExpectedHorizontalOffset = horizontalDistance + FailureOvershootMargin;
        var maxExpectedY = MathF.Max(sourceY, targetY) + FailureFallMargin;

        for (var tick = 0; tick < totalTicks; tick += 1)
        {
            var input = CreateDirectionalInput(player, direction, jump: false);
            _ = player.Advance(input, jumpPressed: false, level, team, FixedDeltaSeconds);

            if (!player.IsAlive)
            {
                failureReason = "ground traverse died during simulation";
                return false;
            }

            if (HasReachedGroundTraverseWindow(player, targetX, targetY))
            {
                tape =
                [
                    CreateDirectionalFrame(direction, jump: false, tick + 1),
                ];
                cost = (tick + 1) * 12f;
                return true;
            }

            if (MathF.Abs(player.X - sourceX) > maxExpectedHorizontalOffset && player.IsGrounded)
            {
                failureReason = "ground traverse ran past the target without entering the acceptance window";
                return false;
            }

            if (player.Y > maxExpectedY)
            {
                failureReason = "ground traverse fell below the expected walk path";
                return false;
            }
        }

        failureReason = "ground traverse timed out before reaching the target anchor";
        return false;
    }

    public static bool TryBuildGroundTape(
        SimpleLevel level,
        CharacterClassDefinition classDefinition,
        float sourceX,
        float sourceY,
        float targetX,
        float targetY,
        PlayerTeam team,
        out IReadOnlyList<BotNavigationInputFrame> tape,
        out float cost)
    {
        return TryBuildGroundTape(
            level,
            classDefinition,
            sourceX,
            sourceY,
            targetX,
            targetY,
            team,
            out tape,
            out cost,
            out _);
    }

    public static bool CanWalkDirectly(
        SimpleLevel level,
        CharacterClassDefinition classDefinition,
        float sourceX,
        float sourceY,
        float targetX,
        float targetY)
    {
        if (MathF.Abs(sourceY - targetY) > 2f)
        {
            return false;
        }

        var minX = MathF.Min(sourceX, targetX);
        var maxX = MathF.Max(sourceX, targetX);
        for (var x = minX; x <= maxX; x += HorizontalProbeStep)
        {
            if (!CanOccupy(level, classDefinition, x, sourceY)
                || !HasGroundSupport(level, classDefinition, x, sourceY))
            {
                return false;
            }
        }

        return CanOccupy(level, classDefinition, maxX, sourceY)
            && HasGroundSupport(level, classDefinition, maxX, sourceY);
    }

    public static bool TryValidateRecordedTraversalTape(
        SimpleLevel level,
        CharacterClassDefinition classDefinition,
        float sourceX,
        float sourceY,
        float targetX,
        float targetY,
        PlayerTeam team,
        IReadOnlyList<BotNavigationInputFrame> tape,
        out float cost,
        out BotNavigationTraversalKind traversalKind)
    {
        return TryValidateRecordedTraversalTape(
            level,
            classDefinition,
            sourceX,
            sourceY,
            targetX,
            targetY,
            team,
            tape,
            requireGroundedArrival: true,
            fixedDeltaSeconds: FixedDeltaSeconds,
            out cost,
            out traversalKind,
            out _);
    }

    public static bool TryValidateRecordedTraversalTape(
        SimpleLevel level,
        CharacterClassDefinition classDefinition,
        float sourceX,
        float sourceY,
        float targetX,
        float targetY,
        PlayerTeam team,
        IReadOnlyList<BotNavigationInputFrame> tape,
        bool requireGroundedArrival,
        out float cost,
        out BotNavigationTraversalKind traversalKind)
    {
        return TryValidateRecordedTraversalTape(
            level,
            classDefinition,
            sourceX,
            sourceY,
            targetX,
            targetY,
            team,
            tape,
            requireGroundedArrival,
            fixedDeltaSeconds: FixedDeltaSeconds,
            out cost,
            out traversalKind,
            out _);
    }

    public static bool TryValidateRecordedTraversalTape(
        SimpleLevel level,
        CharacterClassDefinition classDefinition,
        float sourceX,
        float sourceY,
        float targetX,
        float targetY,
        PlayerTeam team,
        IReadOnlyList<BotNavigationInputFrame> tape,
        bool requireGroundedArrival,
        double fixedDeltaSeconds,
        out float cost,
        out BotNavigationTraversalKind traversalKind,
        out string failureMessage)
    {
        cost = 0f;
        failureMessage = string.Empty;
        traversalKind = ResolveRecordedTraversalKind(sourceY, targetY, tape);
        var validatedFixedDeltaSeconds = fixedDeltaSeconds > 0d ? fixedDeltaSeconds : FixedDeltaSeconds;
        if (tape.Count == 0)
        {
            failureMessage = "recorded tape is empty";
            return false;
        }

        var player = new PlayerEntity(id: 1, classDefinition, displayName: "nav-recording-validator");
        player.Spawn(team, sourceX, sourceY);
        player.ResolveBlockingOverlap(level, team);
        if (!player.IsAlive
            || MathF.Abs(player.X - sourceX) > 2f
            || MathF.Abs(player.Y - sourceY) > 2f)
        {
            failureMessage = "source position is not a clean spawn point for this class";
            return false;
        }

        var previousInput = default(PlayerInputSnapshot);
        var elapsedTicks = 0;
        var maxExpectedHorizontalOffset = MathF.Abs(targetX - sourceX) + FailureOvershootMargin;
        var maxExpectedY = MathF.Max(sourceY, targetY) + FailureFallMargin;

        foreach (var frame in tape)
        {
            var frameTicks = GetFrameTickCount(frame, validatedFixedDeltaSeconds);
            if (frameTicks <= 0)
            {
                continue;
            }

            for (var tick = 0; tick < frameTicks; tick += 1)
            {
                var input = CreateRecordedFrameInput(player, frame);
                var jumpPressed = frame.Up && !previousInput.Up;
                _ = player.Advance(input, jumpPressed, level, team, validatedFixedDeltaSeconds);
                previousInput = input;
                elapsedTicks += 1;

                if (!player.IsAlive)
                {
                    failureMessage = "player died during validation";
                    return false;
                }

                if (HasReachedTargetWindow(player, targetX, targetY, HintLandingToleranceX, HintLandingToleranceY, requireGroundedArrival))
                {
                    cost = elapsedTicks * 12f;
                    traversalKind = ResolveRecordedTraversalKind(sourceY, targetY, tape);
                    return true;
                }

                if (MathF.Abs(player.X - sourceX) > maxExpectedHorizontalOffset && player.IsGrounded)
                {
                    failureMessage = "recorded traversal overshot horizontally before reaching the target";
                    return false;
                }

                if (player.Y > maxExpectedY)
                {
                    failureMessage = "recorded traversal fell below the expected path";
                    return false;
                }
            }
        }

        if (HasReachedTargetWindow(player, targetX, targetY, HintLandingToleranceX, HintLandingToleranceY, requireGroundedArrival))
        {
            cost = elapsedTicks * 12f;
            traversalKind = ResolveRecordedTraversalKind(sourceY, targetY, tape);
            return true;
        }

        failureMessage = requireGroundedArrival
            ? "recorded traversal ended outside the grounded landing window"
            : "recorded traversal ended outside the target window";
        return false;
    }

    public static bool TryValidateRecordedTraversalTape(
        SimpleLevel level,
        CharacterClassDefinition classDefinition,
        float sourceX,
        float sourceY,
        float targetX,
        float targetY,
        PlayerTeam team,
        IReadOnlyList<BotNavigationInputFrame> tape,
        bool requireGroundedArrival,
        out float cost,
        out BotNavigationTraversalKind traversalKind,
        out string failureMessage)
    {
        return TryValidateRecordedTraversalTape(
            level,
            classDefinition,
            sourceX,
            sourceY,
            targetX,
            targetY,
            team,
            tape,
            requireGroundedArrival,
            fixedDeltaSeconds: FixedDeltaSeconds,
            out cost,
            out traversalKind,
            out failureMessage);
    }

    private static bool TryBuildJumpTapeInternal(
        SimpleLevel level,
        CharacterClassDefinition classDefinition,
        BotNavigationProfile profile,
        float sourceX,
        float sourceY,
        float targetX,
        float targetY,
        PlayerTeam team,
        IReadOnlyList<int> runUpTickOptions,
        IReadOnlyList<int> airborneTickOptions,
        float landingToleranceX,
        float landingToleranceY,
        bool requireGroundedArrival,
        out IReadOnlyList<BotNavigationInputFrame> tape,
        out float cost)
    {
        tape = Array.Empty<BotNavigationInputFrame>();
        cost = 0f;

        var direction = targetX >= sourceX ? 1 : -1;
        foreach (var runUpTicks in runUpTickOptions)
        {
            foreach (var airborneTicks in airborneTickOptions)
            {
                if (!TrySimulateJumpAttempt(
                    level,
                    classDefinition,
                    direction,
                    sourceX,
                    sourceY,
                    targetX,
                    targetY,
                    team,
                    runUpTicks,
                    airborneTicks,
                    landingToleranceX,
                    landingToleranceY,
                    requireGroundedArrival,
                    out var usedPostJumpTicks))
                {
                    continue;
                }

                var builtTape = new List<BotNavigationInputFrame>(3);
                if (runUpTicks > 0)
                {
                    builtTape.Add(CreateDirectionalFrame(direction, jump: false, runUpTicks));
                }

                builtTape.Add(CreateDirectionalFrame(direction, jump: true, ticks: 1));
                if (usedPostJumpTicks > 0)
                {
                    builtTape.Add(CreateDirectionalFrame(direction, jump: false, usedPostJumpTicks));
                }

                tape = builtTape;
                cost = (runUpTicks + 1 + usedPostJumpTicks) * 12f;
                return true;
            }
        }

        return false;
    }

    private static bool TrySimulateJumpAttempt(
        SimpleLevel level,
        CharacterClassDefinition classDefinition,
        int direction,
        float sourceX,
        float sourceY,
        float targetX,
        float targetY,
        PlayerTeam team,
        int runUpTicks,
        int airborneTicks,
        float landingToleranceX,
        float landingToleranceY,
        bool requireGroundedArrival,
        out int usedPostJumpTicks)
    {
        usedPostJumpTicks = 0;

        var player = new PlayerEntity(id: 1, classDefinition, displayName: "nav-validator");
        player.Spawn(team, sourceX, sourceY);
        player.ResolveBlockingOverlap(level, team);
        if (!player.IsAlive
            || MathF.Abs(player.X - sourceX) > 2f
            || MathF.Abs(player.Y - sourceY) > 2f)
        {
            return false;
        }

        var previousInput = default(PlayerInputSnapshot);
        var totalTicks = runUpTicks + 1 + airborneTicks;
        var maxExpectedHorizontalOffset = MathF.Abs(targetX - sourceX) + FailureOvershootMargin;
        var maxExpectedY = MathF.Max(sourceY, targetY) + FailureFallMargin;

        for (var tick = 0; tick < totalTicks; tick += 1)
        {
            var jumpThisTick = tick == runUpTicks;
            var input = CreateDirectionalInput(player, direction, jumpThisTick);
            _ = player.Advance(input, jumpThisTick && !previousInput.Up, level, team, FixedDeltaSeconds);
            previousInput = input;

            if (!player.IsAlive)
            {
                return false;
            }

            if (HasReachedTargetWindow(player, targetX, targetY, landingToleranceX, landingToleranceY, requireGroundedArrival))
            {
                usedPostJumpTicks = Math.Max(0, tick - runUpTicks);
                return true;
            }

            if (MathF.Abs(player.X - sourceX) > maxExpectedHorizontalOffset && player.IsGrounded)
            {
                return false;
            }

            if (player.Y > maxExpectedY)
            {
                return false;
            }
        }

        return false;
    }

    private static PlayerInputSnapshot CreateDirectionalInput(PlayerEntity player, int direction, bool jump)
    {
        var aimWorldX = player.X + (direction * HorizontalAimDistance);
        return new PlayerInputSnapshot(
            Left: direction < 0,
            Right: direction > 0,
            Up: jump,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: aimWorldX,
            AimWorldY: player.Y,
            DebugKill: false);
    }

    private static PlayerInputSnapshot CreateRecordedFrameInput(PlayerEntity player, BotNavigationInputFrame frame)
    {
        var direction = frame.Right ? 1 : frame.Left ? -1 : 0;
        var aimWorldX = player.X + ((direction == 0 ? 1 : direction) * HorizontalAimDistance);
        return new PlayerInputSnapshot(
            Left: frame.Left,
            Right: frame.Right,
            Up: frame.Up,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: aimWorldX,
            AimWorldY: player.Y,
            DebugKill: false);
    }

    private static BotNavigationInputFrame CreateDirectionalFrame(int direction, bool jump, int ticks)
    {
        return new BotNavigationInputFrame
        {
            Left = direction < 0,
            Right = direction > 0,
            Up = jump,
            DurationSeconds = ticks * FixedDeltaSeconds,
            Ticks = ticks,
        };
    }

    private static int GetFrameTickCount(BotNavigationInputFrame frame, double fixedDeltaSeconds)
    {
        if (frame.Ticks > 0)
        {
            return frame.Ticks;
        }

        if (frame.DurationSeconds > 0d)
        {
            return Math.Max(1, (int)Math.Round(frame.DurationSeconds / fixedDeltaSeconds));
        }

        return 1;
    }

    private static BotNavigationTraversalKind ResolveRecordedTraversalKind(
        float sourceY,
        float targetY,
        IReadOnlyList<BotNavigationInputFrame> tape)
    {
        if (tape.Any(frame => frame.Up))
        {
            return BotNavigationTraversalKind.Jump;
        }

        return targetY > sourceY + 8f
            ? BotNavigationTraversalKind.Drop
            : BotNavigationTraversalKind.Walk;
    }

    private static bool HasReachedTargetWindow(PlayerEntity player, float targetX, float targetY, float toleranceX, float toleranceY, bool requireGroundedArrival)
    {
        return (!requireGroundedArrival || player.IsGrounded)
            && MathF.Abs(player.X - targetX) <= toleranceX
            && MathF.Abs(player.Y - targetY) <= toleranceY;
    }

    private static bool HasReachedGroundTraverseWindow(PlayerEntity player, float targetX, float targetY)
    {
        return HasReachedTargetWindow(player, targetX, targetY, GroundTraverseToleranceX, GroundTraverseToleranceY, requireGroundedArrival: true);
    }

    private static bool CanOccupy(SimpleLevel level, CharacterClassDefinition classDefinition, float x, float y)
    {
        var left = x + classDefinition.CollisionLeft;
        var top = y + classDefinition.CollisionTop;
        var right = x + classDefinition.CollisionRight;
        var bottom = y + classDefinition.CollisionBottom;

        if (left < 0f
            || top < 0f
            || right > level.Bounds.Width
            || bottom > level.Bounds.Height)
        {
            return false;
        }

        foreach (var solid in level.Solids)
        {
            if (left < solid.Right && right > solid.Left && top < solid.Bottom && bottom > solid.Top)
            {
                return false;
            }
        }

        foreach (var wall in level.GetRoomObjects(RoomObjectType.PlayerWall))
        {
            if (left < wall.Right && right > wall.Left && top < wall.Bottom && bottom > wall.Top)
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasGroundSupport(SimpleLevel level, CharacterClassDefinition classDefinition, float x, float y)
    {
        return !CanOccupy(level, classDefinition, x, y + 1f);
    }

    private static int[] GetAirborneTickOptions(BotNavigationProfile profile)
    {
        return profile switch
        {
            BotNavigationProfile.Light => LightAirborneTickOptions,
            BotNavigationProfile.Heavy => HeavyAirborneTickOptions,
            _ => StandardAirborneTickOptions,
        };
    }

    private static int[] GetHintAirborneTickOptions(BotNavigationProfile profile)
    {
        return profile switch
        {
            BotNavigationProfile.Light => HintLightAirborneTickOptions,
            BotNavigationProfile.Heavy => HintHeavyAirborneTickOptions,
            _ => HintStandardAirborneTickOptions,
        };
    }

    private static int PickNearestTickOption(int[] options, int requestedTicks)
    {
        if (options.Length == 0)
        {
            return Math.Max(1, requestedTicks);
        }

        var bestOption = options[0];
        var bestDistance = Math.Abs(bestOption - requestedTicks);
        for (var index = 1; index < options.Length; index += 1)
        {
            var option = options[index];
            var distance = Math.Abs(option - requestedTicks);
            if (distance >= bestDistance)
            {
                continue;
            }

            bestOption = option;
            bestDistance = distance;
        }

        return bestOption;
    }
}

public static class BotNavigationRecordedTraversalValidator
{
    public static bool TryValidate(
        SimpleLevel level,
        CharacterClassDefinition classDefinition,
        float sourceX,
        float sourceY,
        float targetX,
        float targetY,
        PlayerTeam team,
        IReadOnlyList<BotNavigationInputFrame> tape,
        out float cost,
        out BotNavigationTraversalKind traversalKind)
    {
        return BotNavigationMovementValidator.TryValidateRecordedTraversalTape(
            level,
            classDefinition,
            sourceX,
            sourceY,
            targetX,
            targetY,
            team,
            tape,
            out cost,
            out traversalKind);
    }

    public static bool TryValidate(
        SimpleLevel level,
        CharacterClassDefinition classDefinition,
        float sourceX,
        float sourceY,
        float targetX,
        float targetY,
        PlayerTeam team,
        IReadOnlyList<BotNavigationInputFrame> tape,
        bool requireGroundedArrival,
        out float cost,
        out BotNavigationTraversalKind traversalKind,
        out string failureMessage)
    {
        return BotNavigationMovementValidator.TryValidateRecordedTraversalTape(
            level,
            classDefinition,
            sourceX,
            sourceY,
            targetX,
            targetY,
            team,
            tape,
            requireGroundedArrival,
            fixedDeltaSeconds: 1d / SimulationConfig.DefaultTicksPerSecond,
            out cost,
            out traversalKind,
            out failureMessage);
    }

    public static bool TryValidate(
        SimpleLevel level,
        CharacterClassDefinition classDefinition,
        float sourceX,
        float sourceY,
        float targetX,
        float targetY,
        PlayerTeam team,
        IReadOnlyList<BotNavigationInputFrame> tape,
        bool requireGroundedArrival,
        double fixedDeltaSeconds,
        out float cost,
        out BotNavigationTraversalKind traversalKind,
        out string failureMessage)
    {
        return BotNavigationMovementValidator.TryValidateRecordedTraversalTape(
            level,
            classDefinition,
            sourceX,
            sourceY,
            targetX,
            targetY,
            team,
            tape,
            requireGroundedArrival,
            fixedDeltaSeconds,
            out cost,
            out traversalKind,
            out failureMessage);
    }
}

internal readonly record struct JumpSearchEnvelope(
    float MaxHorizontalDistance,
    float MaxRiseDistance,
    float MaxDescentDistance);
