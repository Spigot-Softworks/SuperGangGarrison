namespace OpenGarrison.Core;

public sealed partial class PlayerEntity
{
    public const float BurnLegIntensity = 7f;
    public const float BurnMaxIntensity = 13f;
    public const float BurnDefaultMaxDurationSourceTicks = 210f;
    public const float BurnPyroMaxDurationSourceTicks = 10f;
    public const float BurnDecayDelaySourceTicks = 90f;
    public const float BurnDecayDurationSourceTicks = 90f;
    public const float BurnDurationDecayPerSourceTick = 1f;
    public const int BurnVisualShuffleSourceTicks = 30;

    public readonly record struct AfterburnTickResult(bool IsFatal, int? BurnedByPlayerId);

    public float BurnIntensity { get; private set; }

    public float BurnDurationSourceTicks { get; private set; }

    public float BurnDecayDelaySourceTicksRemaining { get; private set; }

    public float BurnIntensityDecayPerSourceTick { get; private set; }

    public int? BurnedByPlayerId { get; private set; }

    public bool IsBurning => BurnIntensity > 0f || BurnDurationSourceTicks > 0f;

    public int BurnVisualCount
    {
        get
        {
            var baseCount = BurnVisualBaseCount;
            if (baseCount <= 0 || BurnDurationSourceTicks <= 0f)
            {
                return 0;
            }

            var scaledCount = baseCount * (BurnDurationSourceTicks / GetBurnMaxDurationSourceTicks());
            var count = (int)MathF.Floor(scaledCount);
            if ((scaledCount - count) > 0.0001f)
            {
                count += 1;
            }

            return int.Clamp(count, 0, baseCount);
        }
    }

    public int BurnVisualBaseCount => GetBurnVisualBaseCount(ClassId);

    public float BurnVisualAlpha => BurnIntensity <= 0f
        ? 0f
        : (BurnIntensity / BurnMaxIntensity) * 0.5f + 0.25f;

    public void GetBurnVisualOffset(int flameIndex, int sourceFrame, out float offsetX, out float offsetY)
    {
        if (flameIndex < 0 || flameIndex >= BurnVisualBaseCount)
        {
            offsetX = 0f;
            offsetY = 0f;
            return;
        }

        var shufflePhase = GetBurnVisualShufflePhase(sourceFrame);
        offsetX = GetBurnVisualOffsetComponent(Id, flameIndex, shufflePhase, axis: 0) * (Width * 0.5f);
        offsetY = GetBurnVisualOffsetComponent(Id, flameIndex, shufflePhase, axis: 1) * (Height * 0.5f);
    }

    public int GetBurnVisualFrameIndex(int flameIndex, int sourceFrame, int frameCount)
    {
        if (frameCount <= 0)
        {
            return 0;
        }

        var jitter = GetBurnVisualFrameJitter(Id, flameIndex, sourceFrame);
        var sourceFrameIndex = GetBurnVisualShuffleTicksRemaining(sourceFrame) + flameIndex + jitter;
        return PositiveModulo(sourceFrameIndex, frameCount);
    }

    public void IgniteAfterburn(
        int ownerPlayerId,
        float durationIncreaseSourceTicks,
        float intensityIncrease,
        bool afterburnFalloff,
        float burnFalloffAmount)
    {
        if (!IsAlive || IsUbered || durationIncreaseSourceTicks <= 0f || intensityIncrease <= 0f)
        {
            return;
        }

        BurnDurationSourceTicks += durationIncreaseSourceTicks;

        var falloffFactor = 1f;
        if (afterburnFalloff)
        {
            var clampedFalloffAmount = float.Clamp(burnFalloffAmount, 0f, 1f);
            falloffFactor = clampedFalloffAmount * 0.65f + 0.35f;
            if (BurnIntensity > BurnLegIntensity)
            {
                falloffFactor *= 0.5f;
            }
        }

        BurnIntensity += intensityIncrease * falloffFactor;
        BurnDurationSourceTicks = float.Min(BurnDurationSourceTicks, GetBurnMaxDurationSourceTicks());
        BurnIntensity = float.Min(BurnIntensity, BurnMaxIntensity);
        BurnedByPlayerId = ownerPlayerId > 0 ? ownerPlayerId : null;
        BurnDecayDelaySourceTicksRemaining = BurnDecayDelaySourceTicks;
        BurnIntensityDecayPerSourceTick = 0f;
    }

    public AfterburnTickResult AdvanceAfterburn(float deltaSeconds)
    {
        if (!IsAlive)
        {
            return default;
        }

        if (IsUbered)
        {
            ExtinguishAfterburn();
            return default;
        }

        var sourceDelta = MathF.Max(0f, deltaSeconds) * LegacyMovementModel.SourceTicksPerSecond;
        if (sourceDelta <= 0f || !IsBurning)
        {
            return default;
        }

        if (BurnDurationSourceTicks > 0f
            && ApplyContinuousDamage((BurnIntensity / LegacyMovementModel.SourceTicksPerSecond) * sourceDelta))
        {
            return new AfterburnTickResult(true, BurnedByPlayerId);
        }

        if (BurnDurationSourceTicks > 0f)
        {
            BurnDurationSourceTicks -= BurnDurationDecayPerSourceTick * sourceDelta;
        }

        if (BurnDecayDelaySourceTicksRemaining > 0f)
        {
            BurnDecayDelaySourceTicksRemaining -= sourceDelta;
            if (BurnDecayDelaySourceTicksRemaining <= 0f)
            {
                BurnDecayDelaySourceTicksRemaining = 0f;
                BurnIntensityDecayPerSourceTick = BurnIntensity / BurnDecayDurationSourceTicks;
            }
        }
        else if (BurnIntensity > 0f)
        {
            BurnIntensity -= BurnIntensityDecayPerSourceTick * sourceDelta;
        }

        if (BurnDurationSourceTicks <= 0f || BurnIntensity <= 0f)
        {
            ExtinguishAfterburn();
            return default;
        }

        BurnDurationSourceTicks = float.Min(BurnDurationSourceTicks, GetBurnMaxDurationSourceTicks());
        BurnIntensity = float.Min(BurnIntensity, BurnMaxIntensity);
        return default;
    }

    public void ReduceBurnDuration(float sourceTicks)
    {
        if (sourceTicks <= 0f || BurnDurationSourceTicks <= 0f)
        {
            return;
        }

        BurnDurationSourceTicks = MathF.Max(0f, BurnDurationSourceTicks - sourceTicks);
        if (BurnDurationSourceTicks <= 0f)
        {
            ExtinguishAfterburn();
        }
    }

    public void ExtinguishAfterburn()
    {
        BurnIntensity = 0f;
        BurnDurationSourceTicks = 0f;
        BurnDecayDelaySourceTicksRemaining = 0f;
        BurnIntensityDecayPerSourceTick = 0f;
        BurnedByPlayerId = null;
    }

    private float GetBurnMaxDurationSourceTicks()
    {
        return ClassId == PlayerClass.Pyro
            ? BurnPyroMaxDurationSourceTicks
            : BurnDefaultMaxDurationSourceTicks;
    }

    private static int GetBurnVisualBaseCount(PlayerClass classId)
    {
        return classId switch
        {
            PlayerClass.Heavy => 5,
            PlayerClass.Soldier => 4,
            PlayerClass.Sniper => 4,
            PlayerClass.Medic => 4,
            PlayerClass.Spy => 4,
            _ => 3,
        };
    }

    private static int GetBurnVisualShufflePhase(int sourceFrame)
    {
        return sourceFrame <= 0
            ? 0
            : sourceFrame / BurnVisualShuffleSourceTicks;
    }

    private static int GetBurnVisualShuffleTicksRemaining(int sourceFrame)
    {
        var phaseTicks = PositiveModulo(sourceFrame, BurnVisualShuffleSourceTicks);
        return phaseTicks == 0
            ? BurnVisualShuffleSourceTicks
            : BurnVisualShuffleSourceTicks - phaseTicks;
    }

    private static float GetBurnVisualOffsetComponent(int playerId, int flameIndex, int shufflePhase, int axis)
    {
        var hash = ComputeBurnVisualHash(playerId, flameIndex, shufflePhase, axis);
        var normalized = (uint)hash / (float)uint.MaxValue;
        return normalized * 2f - 1f;
    }

    private static int GetBurnVisualFrameJitter(int playerId, int flameIndex, int sourceFrame)
    {
        return PositiveModulo(ComputeBurnVisualHash(playerId, flameIndex, sourceFrame, axis: 2), 3);
    }

    private static int ComputeBurnVisualHash(int playerId, int flameIndex, int frameOrPhase, int axis)
    {
        var hash = playerId;
        hash = unchecked((hash * 397) ^ flameIndex);
        hash = unchecked((hash * 397) ^ frameOrPhase);
        hash = unchecked((hash * 397) ^ axis);
        return hash;
    }

    private static int PositiveModulo(int value, int modulus)
    {
        if (modulus <= 0)
        {
            return 0;
        }

        var result = value % modulus;
        return result < 0 ? result + modulus : result;
    }
}
