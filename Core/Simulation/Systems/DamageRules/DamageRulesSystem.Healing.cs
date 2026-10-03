namespace OpenGarrison.Core;

internal sealed partial class DamageRulesSystem
{
    internal int ApplyHealingWithFeedback(PlayerEntity target, float healing, string? soundName = null, float soundX = 0f, float soundY = 0f)
    {
        var appliedHealing = target.ApplyContinuousHealingAndGetAmount(healing);
        if (appliedHealing <= 0)
        {
            return 0;
        }

        RegisterHealingEvent(target, appliedHealing);
        if (!string.IsNullOrWhiteSpace(soundName))
        {
            _host.WorldEffects.RegisterWorldSoundEvent(soundName, soundX, soundY);
        }

        return appliedHealing;
    }

    internal void RegisterHealingFeedbackOnly(PlayerEntity target, int amount)
    {
        RegisterHealingEvent(target, amount);
    }

    internal void RegisterHealingEvent(PlayerEntity target, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        _host.PresentationEvents.AddHealingEvent(new WorldHealingEvent(
            target.Id,
            amount,
            SourceFrame: (ulong)_host.Frame));
    }
}
