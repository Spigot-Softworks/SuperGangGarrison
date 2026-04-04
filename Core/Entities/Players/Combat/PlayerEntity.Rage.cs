using System;

namespace OpenGarrison.Core;

public sealed partial class PlayerEntity
{
    public void AddRageCharge(float amount, float maxCharge)
    {
        if (!IsAlive || IsRaging || amount <= 0f || maxCharge <= 0f)
        {
            return;
        }

        RageCharge = float.Min(maxCharge, RageCharge + amount);
        if (RageCharge >= maxCharge)
        {
            RageCharge = maxCharge;
            IsRageReady = true;
        }
    }

    public bool TryStartRage(int ticks)
    {
        if (!IsAlive
            || IsTaunting
            || IsHeavyEating
            || IsSpyCloaked
            || IsSpyBackstabAnimating
            || !IsRageReady
            || ticks <= 0)
        {
            return false;
        }

        IsTaunting = true;
        TauntFrameIndex = 0f;
        RageCharge = 0f;
        IsRageReady = false;
        RageTicksRemaining = ticks;
        return true;
    }

    public void AdvanceRageState()
    {
        if (RageTicksRemaining > 0)
        {
            RageTicksRemaining -= 1;
            if (RageTicksRemaining <= 0)
            {
                RageTicksRemaining = 0;
            }
        }
    }

    public void ClearRageState()
    {
        ResetRageState();
    }

    private void ResetRageState()
    {
        RageCharge = 0f;
        IsRageReady = false;
        RageTicksRemaining = 0;
    }
}
