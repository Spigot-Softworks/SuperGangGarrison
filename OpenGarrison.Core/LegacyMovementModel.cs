using System;

namespace OpenGarrison.Core;

public enum LegacyMovementState : byte
{
    None = 0,
    ExplosionRecovery = 1,
    RocketJuggle = 2,
    Airblast = 3,
    FriendlyJuggle = 4,
}

public static class LegacyMovementModel
{
    public const float SourceTicksPerSecond = 30f;
    public const float MaxStepSpeedPerTick = 15f;
    public const float BaseControlFactor = 0.85f;
    public const float BaseFrictionFactor = 1.15f;
    public const float JumpStrengthToJumpSpeed = SourceTicksPerSecond;
    public const float DefaultJumpStrength = 8f + (GravityPerTick / 2f);
    public const float GravityPerTick = 0.6f;
    public const float BlastGravityPerTick = 0.54f;
    public const float MaxFallSpeedPerTick = 10f;
    public const float StopSpeedThresholdPerTick = 0.195f;

    private const float HalfSourceTick = 0.5f;
    private const float SelfBlastControlFactor = 0.65f;
    private const float EnemyBlastControlFactor = 0.45f;
    private const float AirblastControlFactor = 0.35f;
    private const float IntelControlFactor = BaseControlFactor - 0.1f;
    private const float SelfBlastFrictionFactor = 1f;
    private const float EnemyBlastFrictionFactor = 1.05f;
    private const float AirblastFrictionFactor = 1.05f;
    private const float FriendlyBlastFrictionFactor = 1f;

    public static float GetGravityPerSecondSquared()
    {
        return GravityPerTick * SourceTicksPerSecond * SourceTicksPerSecond;
    }

    public static float GetJumpSpeed(float jumpStrength)
    {
        return jumpStrength * JumpStrengthToJumpSpeed;
    }

    public static float GetMaxRunSpeed(float runPower)
    {
        return GetMaxRunSpeedPerSourceTick(runPower) * SourceTicksPerSecond;
    }

    public static float GetContinuousRunDrive(float runPower)
    {
        var divisor = GetDeltaMultiplier(BaseFrictionFactor, HalfSourceTick);
        var perHalfTickDrive = runPower * BaseControlFactor * HalfSourceTick;
        var steadyStatePerSourceTick = GetMaxRunSpeedPerSourceTick(runPower);
        return steadyStatePerSourceTick <= 0f
            ? 0f
            : (perHalfTickDrive / divisor) * SourceTicksPerSecond * 2f;
    }

    public static float AdvanceHorizontalSpeed(
        float currentSpeed,
        float runPower,
        float movementScale,
        float horizontalDirection,
        LegacyMovementState state,
        bool isCarryingIntel,
        float deltaSeconds)
    {
        return AdvanceHorizontalSpeed(
            currentSpeed,
            runPower,
            movementScale,
            horizontalDirection != 0f,
            horizontalDirection,
            state,
            isCarryingIntel,
            deltaSeconds);
    }

    public static float AdvanceHorizontalSpeed(
        float currentSpeed,
        float runPower,
        float movementScale,
        bool hasHorizontalInput,
        float horizontalDirection,
        LegacyMovementState state,
        bool isCarryingIntel,
        float deltaSeconds)
    {
        if (deltaSeconds <= 0f)
        {
            return currentSpeed;
        }

        var controlling = horizontalDirection != 0f && movementScale > 0f;
        var speedPerTick = currentSpeed / SourceTicksPerSecond;
        var baseMaxSpeedPerTick = GetMaxRunSpeedPerSourceTick(runPower);
        var remainingSourceTicks = SourceTicksPerSecond * deltaSeconds;
        while (remainingSourceTicks > 0f)
        {
            var stepSourceTicks = MathF.Min(HalfSourceTick, remainingSourceTicks);
            if (controlling)
            {
                speedPerTick += horizontalDirection
                    * runPower
                    * movementScale
                    * GetControlFactor(state, isCarryingIntel)
                    * stepSourceTicks;
            }

            var frictionFactor = GetAppliedFrictionFactor(speedPerTick, baseMaxSpeedPerTick, hasHorizontalInput, state);
            speedPerTick /= GetDeltaMultiplier(frictionFactor, stepSourceTicks);
            remainingSourceTicks -= stepSourceTicks;
        }

        if (!controlling && MathF.Abs(speedPerTick) < StopSpeedThresholdPerTick)
        {
            return 0f;
        }

        return speedPerTick * SourceTicksPerSecond;
    }

    public static float AdvanceVerticalSpeedHalfStep(float currentSpeed, float gravityPerTick, float deltaSeconds)
    {
        if (deltaSeconds <= 0f || gravityPerTick <= 0f)
        {
            return currentSpeed;
        }

        var nextSpeed = currentSpeed + (gravityPerTick * SourceTicksPerSecond * SourceTicksPerSecond * deltaSeconds * 0.5f);
        return MathF.Min(nextSpeed, MaxFallSpeedPerTick * SourceTicksPerSecond);
    }

    public static float AdvanceVerticalSpeed(
        float currentSpeed,
        bool isAirborne,
        float deltaSeconds,
        ref LegacyMovementState state)
    {
        if (!isAirborne || deltaSeconds <= 0f)
        {
            return currentSpeed;
        }

        var gravityPerTick = GetAirborneGravityPerTick(state);
        var nextSpeed = AdvanceVerticalSpeedHalfStep(currentSpeed, gravityPerTick, deltaSeconds);
        return AdvanceVerticalSpeedHalfStep(nextSpeed, gravityPerTick, deltaSeconds);
    }

    public static float GetAirborneGravityPerTick(LegacyMovementState state)
    {
        return state switch
        {
            LegacyMovementState.ExplosionRecovery => BlastGravityPerTick,
            LegacyMovementState.RocketJuggle => BlastGravityPerTick,
            LegacyMovementState.FriendlyJuggle => BlastGravityPerTick,
            _ => GravityPerTick,
        };
    }

    private static float GetMaxRunSpeedPerSourceTick(float runPower)
    {
        return MathF.Abs(runPower * BaseControlFactor / (BaseFrictionFactor - 1f));
    }

    private static float GetControlFactor(LegacyMovementState state, bool isCarryingIntel)
    {
        return state switch
        {
            LegacyMovementState.ExplosionRecovery => SelfBlastControlFactor,
            LegacyMovementState.RocketJuggle => EnemyBlastControlFactor,
            LegacyMovementState.Airblast => AirblastControlFactor,
            LegacyMovementState.FriendlyJuggle => BaseControlFactor,
            _ => isCarryingIntel ? IntelControlFactor : BaseControlFactor,
        };
    }

    private static float GetAppliedFrictionFactor(
        float speedPerTick,
        float baseMaxSpeedPerTick,
        bool hasHorizontalInput,
        LegacyMovementState state)
    {
        var absoluteSpeed = MathF.Abs(speedPerTick);
        if (absoluteSpeed > baseMaxSpeedPerTick * 2f
            || (hasHorizontalInput && absoluteSpeed < baseMaxSpeedPerTick))
        {
            return BaseFrictionFactor;
        }

        return state switch
        {
            LegacyMovementState.ExplosionRecovery => SelfBlastFrictionFactor,
            LegacyMovementState.RocketJuggle => EnemyBlastFrictionFactor,
            LegacyMovementState.Airblast => AirblastFrictionFactor,
            LegacyMovementState.FriendlyJuggle => FriendlyBlastFrictionFactor,
            _ => BaseFrictionFactor,
        };
    }

    private static float GetDeltaMultiplier(float factor, float sourceTickFraction)
    {
        return (factor * sourceTickFraction) + (1f - sourceTickFraction);
    }
}
