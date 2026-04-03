namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private void RegisterHealingEvent(PlayerEntity target, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        _pendingHealingEvents.Add(new WorldHealingEvent(
            target.Id,
            amount,
            SourceFrame: (ulong)Frame));
    }
}
