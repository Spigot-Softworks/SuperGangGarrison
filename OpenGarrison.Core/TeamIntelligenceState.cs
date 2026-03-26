namespace OpenGarrison.Core;

public sealed class TeamIntelligenceState
{
    public TeamIntelligenceState(PlayerTeam team, float homeX, float homeY)
    {
        Team = team;
        HomeX = homeX;
        HomeY = homeY;
        ResetToBase();
    }

    public PlayerTeam Team { get; }

    public float HomeX { get; }

    public float HomeY { get; }

    public float X { get; private set; }

    public float Y { get; private set; }

    public bool IsAtBase { get; private set; }

    public bool IsDropped { get; private set; }

    public bool IsCarried => !IsAtBase && !IsDropped;

    public int ReturnTicksRemaining { get; private set; }

    public void PickUp()
    {
        IsAtBase = false;
        IsDropped = false;
        ReturnTicksRemaining = 0;
    }

    public void Drop(float x, float y, int returnTicks)
    {
        X = x;
        Y = y;
        IsAtBase = false;
        IsDropped = true;
        ReturnTicksRemaining = returnTicks;
    }

    public void ResetToBase()
    {
        X = HomeX;
        Y = HomeY;
        IsAtBase = true;
        IsDropped = false;
        ReturnTicksRemaining = 0;
    }

    public void AdvanceTick()
    {
        if (!IsDropped)
        {
            return;
        }

        if (ReturnTicksRemaining <= 0)
        {
            ResetToBase();
            return;
        }

        ReturnTicksRemaining -= 1;
        if (ReturnTicksRemaining <= 0)
        {
            ResetToBase();
        }
    }

    public void ApplyNetworkState(float x, float y, bool isAtBase, bool isDropped, int returnTicksRemaining)
    {
        X = x;
        Y = y;
        IsAtBase = isAtBase;
        IsDropped = isDropped;
        ReturnTicksRemaining = returnTicksRemaining;
    }
}
