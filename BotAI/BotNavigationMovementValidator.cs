using OpenGarrison.Core;

namespace OpenGarrison.BotAI;

internal static class BotNavigationMovementValidator
{
    private const double FixedDeltaSeconds = 1d / SimulationConfig.DefaultTicksPerSecond;
    private const float HorizontalAimDistance = 256f;
    private const float LandingToleranceX = 24f;
    private const float LandingToleranceY = 12f;
    private const float HintLandingToleranceX = 36f;
    private const float HintLandingToleranceY = 18f;
    private const float FailureOvershootMargin = 80f;
    private const float FailureFallMargin = 140f;

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
        var horizontalReach = (classDefinition.MaxRunSpeed * (airTime + runUpSeconds)) + 32f;
        var maxDescent = jumpRise + (profile == BotNavigationProfile.Heavy ? 28f : 44f);

        return new JumpSearchEnvelope(
            MaxHorizontalDistance: MathF.Max(96f, horizontalReach),
            MaxRiseDistance: MathF.Max(48f, jumpRise + 12f),
            MaxDescentDistance: MathF.Max(48f, maxDescent));
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
        return TryBuildJumpTapeInternal(
            level,
            classDefinition,
            profile,
            sourceX,
            sourceY,
            targetX,
            targetY,
            RunUpTickOptions,
            GetAirborneTickOptions(profile),
            LandingToleranceX,
            LandingToleranceY,
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
        return TryBuildJumpTapeInternal(
            level,
            classDefinition,
            profile,
            sourceX,
            sourceY,
            targetX,
            targetY,
            HintRunUpTickOptions,
            GetHintAirborneTickOptions(profile),
            HintLandingToleranceX,
            HintLandingToleranceY,
            out tape,
            out cost);
    }

    private static bool TryBuildJumpTapeInternal(
        SimpleLevel level,
        CharacterClassDefinition classDefinition,
        BotNavigationProfile profile,
        float sourceX,
        float sourceY,
        float targetX,
        float targetY,
        IReadOnlyList<int> runUpTickOptions,
        IReadOnlyList<int> airborneTickOptions,
        float landingToleranceX,
        float landingToleranceY,
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
                    runUpTicks,
                    airborneTicks,
                    landingToleranceX,
                    landingToleranceY,
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
        int runUpTicks,
        int airborneTicks,
        float landingToleranceX,
        float landingToleranceY,
        out int usedPostJumpTicks)
    {
        usedPostJumpTicks = 0;

        var player = new PlayerEntity(id: 1, classDefinition, displayName: "nav-validator");
        player.Spawn(PlayerTeam.Red, sourceX, sourceY);
        player.ResolveBlockingOverlap(level, PlayerTeam.Red);
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
            _ = player.Advance(input, jumpThisTick && !previousInput.Up, level, PlayerTeam.Red, FixedDeltaSeconds);
            previousInput = input;

            if (!player.IsAlive)
            {
                return false;
            }

            if (HasReachedLandingWindow(player, targetX, targetY, landingToleranceX, landingToleranceY))
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

    private static BotNavigationInputFrame CreateDirectionalFrame(int direction, bool jump, int ticks)
    {
        return new BotNavigationInputFrame
        {
            Left = direction < 0,
            Right = direction > 0,
            Up = jump,
            Ticks = ticks,
        };
    }

    private static bool HasReachedLandingWindow(PlayerEntity player, float targetX, float targetY, float toleranceX, float toleranceY)
    {
        return player.IsGrounded
            && MathF.Abs(player.X - targetX) <= toleranceX
            && MathF.Abs(player.Y - targetY) <= toleranceY;
    }

    private static IReadOnlyList<int> GetAirborneTickOptions(BotNavigationProfile profile)
    {
        return profile switch
        {
            BotNavigationProfile.Light => LightAirborneTickOptions,
            BotNavigationProfile.Heavy => HeavyAirborneTickOptions,
            _ => StandardAirborneTickOptions,
        };
    }

    private static IReadOnlyList<int> GetHintAirborneTickOptions(BotNavigationProfile profile)
    {
        return profile switch
        {
            BotNavigationProfile.Light => HintLightAirborneTickOptions,
            BotNavigationProfile.Heavy => HintHeavyAirborneTickOptions,
            _ => HintStandardAirborneTickOptions,
        };
    }
}

internal readonly record struct JumpSearchEnvelope(
    float MaxHorizontalDistance,
    float MaxRiseDistance,
    float MaxDescentDistance);
