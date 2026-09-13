using OpenGarrison.Core.LastToDie;

namespace OpenGarrison.Core;

public sealed partial class PlayerEntity
{
    private int _lastToDieSurvivorHealingRemainder;

    public bool HasLastToDieSurvivorBuff { get; private set; }

    internal void SetLastToDieSurvivorBuff(bool enabled)
    {
        if (HasLastToDieSurvivorBuff != enabled)
        {
            _lastToDieSurvivorHealingRemainder = 0;
        }
        HasLastToDieSurvivorBuff = enabled;
    }

    internal void AdvanceLastToDieSurvivorRegeneration(int ticksPerSecond)
    {
        if (!HasLastToDieSurvivorBuff || !IsAlive || Health >= MaxHealth)
        {
            _lastToDieSurvivorHealingRemainder = 0;
            return;
        }

        // Integer accumulation gives exactly 3 HP over a second at every
        // supported tick rate, independently of other regeneration sources.
        var rate = Math.Max(1, ticksPerSecond);
        _lastToDieSurvivorHealingRemainder += LastToDieSurvivorRules.RegenerationPerSecond;
        var healing = _lastToDieSurvivorHealingRemainder / rate;
        _lastToDieSurvivorHealingRemainder %= rate;
        if (healing > 0) ApplyContinuousHealingAndGetAmount(healing);
    }
}
