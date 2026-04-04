namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private int ApplyHealingWithFeedback(PlayerEntity target, float healing, string? soundName = null, float soundX = 0f, float soundY = 0f)
    {
        var appliedHealing = target.ApplyContinuousHealingAndGetAmount(healing);
        if (appliedHealing <= 0)
        {
            return 0;
        }

        RegisterHealingEvent(target, appliedHealing);
        if (!string.IsNullOrWhiteSpace(soundName))
        {
            RegisterWorldSoundEvent(soundName, soundX, soundY);
        }

        return appliedHealing;
    }

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
