using System;

namespace OpenGarrison.Core;

public static class CivvieMoneyTrailRules
{
    public const int PickupLifetimeTicks = 90;
    public const int MaxPickupsPerOwner = 10;
    public const int MaxPickupsTotal = 80;
    public const float PickupWidth = 34f;
    public const float PickupHeight = 32f;
    public const float PickupSpacingSquared = 9f * 9f;
    public const float TrailMoveSpeedThreshold = 0.04f * LegacyMovementModel.SourceTicksPerSecond;
    public const float TrailSpawnChance = 0.35f;
    public const float TrailVerticalBaseOffset = 11f;
    public const float TrailVerticalJitterSpan = 9f;
    public const float TrailHorizontalOffset = 8f;

    public static bool ShouldEmitDeterministicSourceTickChance(
        ulong frame,
        int playerId,
        int ticksPerSecond,
        float sourceTickChance)
    {
        if (sourceTickChance <= 0f)
        {
            return false;
        }

        var sourceTicksPerSimulationTick = LegacyMovementModel.SourceTicksPerSecond / (float)ticksPerSecond;
        if (sourceTicksPerSimulationTick <= 0f)
        {
            return false;
        }

        var wholeSourceTicks = (int)MathF.Floor(sourceTicksPerSimulationTick);
        for (var tick = 0; tick < wholeSourceTicks; tick += 1)
        {
            if (GetDeterministicUnitFloat(frame, playerId, tick) < sourceTickChance)
            {
                return true;
            }
        }

        var fractionalSourceTick = sourceTicksPerSimulationTick - wholeSourceTicks;
        if (fractionalSourceTick <= 0f)
        {
            return false;
        }

        var fractionalChance = 1f - MathF.Pow(1f - sourceTickChance, fractionalSourceTick);
        return GetDeterministicUnitFloat(frame, playerId, wholeSourceTicks + 1000) < fractionalChance;
    }

    public static float GetDeterministicVerticalOffset(ulong frame, int playerId)
    {
        return GetDeterministicUnitFloat(frame, playerId, salt: 0x4D4F4E45) * TrailVerticalJitterSpan;
    }

    public static float GetDeterministicUnitFloat(ulong frame, int playerId, int salt)
    {
        var hash = HashCode.Combine((int)(frame & 0x7FFFFFFF), playerId, salt);
        return PositiveModulo(hash, 10_000) / 10_000f;
    }

    public static int GetDeterministicSpriteIndex(ulong frame, int playerId, int spriteCount)
    {
        if (spriteCount <= 1)
        {
            return 0;
        }

        return PositiveModulo(
            HashCode.Combine((int)(frame & 0x7FFFFFFF), playerId, 0x53505254),
            spriteCount);
    }

    public static float GetDeterministicSignedOffset(ulong frame, int playerId, int salt, float magnitude)
    {
        return (GetDeterministicUnitFloat(frame, playerId, salt) * 2f - 1f) * magnitude;
    }

    private static int PositiveModulo(int value, int modulus)
    {
        var result = value % modulus;
        return result < 0 ? result + modulus : result;
    }
}
