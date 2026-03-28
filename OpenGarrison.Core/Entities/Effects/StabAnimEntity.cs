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
        float directionDegrees) : base(id)
    {
        OwnerId = ownerId;
        Team = team;
        X = x;
        Y = y;
        DirectionDegrees = directionDegrees;
        TicksRemaining = TotalLifetimeTicks;
        Alpha = InitialAlpha;
    }

    public int OwnerId { get; }

    public PlayerTeam Team { get; }

    public float X { get; private set; }

    public float Y { get; private set; }

    public float DirectionDegrees { get; }

    public int TicksRemaining { get; private set; }

    public bool IsExpired => TicksRemaining <= 0;

    public int FrameIndex { get; private set; }

    public float Alpha { get; private set; }

    public bool FacingLeft => DirectionDegrees >= 95f && DirectionDegrees <= 270f;

    public void AdvanceOneTick(float ownerX, float ownerY)
    {
        X = ownerX;
        Y = ownerY;
        if (TicksRemaining <= 0)
        {
            return;
        }

        TicksRemaining -= 1;
        var elapsedTicks = TotalLifetimeTicks - TicksRemaining;

        if (elapsedTicks > WarmupTicks && FrameIndex < SwingTicks)
        {
            FrameIndex += 1;
        }

        if (FrameIndex >= SwingTicks)
        {
            if (Alpha > InitialAlpha)
            {
                Alpha = MathF.Pow(Alpha, FadeOutExponent);
            }

            if (Alpha <= InitialAlpha)
            {
                Alpha = 0f;
                TicksRemaining = 0;
            }

            return;
        }

        Alpha = Alpha < MaxAlpha
            ? MathF.Pow(Alpha, FadeInExponent)
            : MaxAlpha;
    }
}
