namespace OpenGarrison.Core;

public sealed class StabAnimEntity : SimulationEntity
{
    public const int WarmupTicks = 10;
    public const int SwingTicks = 32;
    public const int FadeOutTicks = 18;
    public const int TotalLifetimeTicks = WarmupTicks + SwingTicks + FadeOutTicks;
    private const float InitialAlpha = 0.01f;
    private const float MaxAlpha = 0.99f;
    private const float FadeInExponent = 0.7f;
    private const float FadeOutExponent = 1f / FadeInExponent;

    public StabAnimEntity(
        int id,
        int ownerId,
        PlayerTeam team,
        float x,
        float y,
        float directionDegrees,
        int speedMultiplier = 1) : base(id)
    {
        OwnerId = ownerId;
        Team = team;
        X = x;
        Y = y;
        DirectionDegrees = directionDegrees;
        SpeedMultiplier = Math.Max(1, speedMultiplier);
        WarmupDurationTicks = ResolvePhaseDurationTicks(WarmupTicks, SpeedMultiplier);
        SwingDurationTicks = ResolvePhaseDurationTicks(SwingTicks, SpeedMultiplier);
        FadeOutDurationTicks = ResolvePhaseDurationTicks(FadeOutTicks, SpeedMultiplier);
        LifetimeTicks = ResolveLifetimeTicks(SpeedMultiplier);
        TicksRemaining = LifetimeTicks;
        Alpha = InitialAlpha;
    }

    public int OwnerId { get; }

    public PlayerTeam Team { get; }

    public float X { get; private set; }

    public float Y { get; private set; }

    public float DirectionDegrees { get; }

    public int TicksRemaining { get; private set; }

    public int LifetimeTicks { get; }

    public int SpeedMultiplier { get; }

    public int WarmupDurationTicks { get; }

    public int SwingDurationTicks { get; }

    public int FadeOutDurationTicks { get; }

    public bool IsExpired => TicksRemaining <= 0;

    public int FrameIndex { get; private set; }

    public float Alpha { get; private set; }

    public bool FacingLeft => DirectionDegrees >= 95f && DirectionDegrees <= 270f;

    public static int ResolveWarmupDurationTicks(int speedMultiplier) =>
        ResolvePhaseDurationTicks(WarmupTicks, speedMultiplier);

    public static int ResolveSwingDurationTicks(int speedMultiplier) =>
        ResolvePhaseDurationTicks(SwingTicks, speedMultiplier);

    public static int ResolveFadeOutDurationTicks(int speedMultiplier) =>
        ResolvePhaseDurationTicks(FadeOutTicks, speedMultiplier);

    public static int ResolveLifetimeTicks(int speedMultiplier) =>
        ResolveWarmupDurationTicks(speedMultiplier)
            + ResolveSwingDurationTicks(speedMultiplier)
            + ResolveFadeOutDurationTicks(speedMultiplier);

    public static float ResolveAlpha(int elapsedTicks, int speedMultiplier)
    {
        var warmupDuration = ResolveWarmupDurationTicks(speedMultiplier);
        var swingDuration = ResolveSwingDurationTicks(speedMultiplier);
        var fadeOutDuration = ResolveFadeOutDurationTicks(speedMultiplier);
        elapsedTicks = Math.Max(0, elapsedTicks);
        if (elapsedTicks <= warmupDuration)
        {
            var progress = elapsedTicks / (float)warmupDuration;
            return Math.Clamp(
                InitialAlpha + ((MaxAlpha - InitialAlpha) * MathF.Pow(progress, FadeInExponent)),
                InitialAlpha,
                MaxAlpha);
        }

        if (elapsedTicks <= warmupDuration + swingDuration)
        {
            return MaxAlpha;
        }

        var fadeElapsed = elapsedTicks - warmupDuration - swingDuration;
        if (fadeElapsed >= fadeOutDuration)
        {
            return 0f;
        }

        var remainingFraction = 1f - (fadeElapsed / (float)fadeOutDuration);
        return Math.Clamp(MaxAlpha * MathF.Pow(remainingFraction, FadeOutExponent), 0f, MaxAlpha);
    }

    public void AdvanceOneTick(float ownerX, float ownerY)
    {
        X = ownerX;
        Y = ownerY;
        if (TicksRemaining <= 0)
        {
            return;
        }

        TicksRemaining -= 1;
        var elapsedTicks = LifetimeTicks - TicksRemaining;
        if (elapsedTicks > WarmupDurationTicks)
        {
            var swingElapsed = Math.Min(
                SwingDurationTicks,
                elapsedTicks - WarmupDurationTicks);
            FrameIndex = Math.Min(
                SwingTicks,
                (int)MathF.Ceiling(swingElapsed * SwingTicks / (float)SwingDurationTicks));
        }

        Alpha = ResolveAlpha(elapsedTicks, SpeedMultiplier);
    }

    private static int ResolvePhaseDurationTicks(int sourceDurationTicks, int speedMultiplier)
    {
        speedMultiplier = Math.Max(1, speedMultiplier);
        return Math.Max(1, (int)MathF.Ceiling(sourceDurationTicks / (float)speedMultiplier));
    }
}
