namespace OpenGarrison.Core;

// Forwarders kept for callers outside the world partials (Client, Server, bots,
// plugins, tests). Callers should move to the system directly over time.
public sealed partial class SimulationWorld
{
    public bool IsExperimentalEngineerFloatingSentry(SentryEntity sentry)
        => ExperimentalRules.IsExperimentalEngineerFloatingSentry(sentry);
    public bool IsPlayerInsideExperimentalEngineerMisdirectionFieldForVisuals(PlayerEntity? player)
        => ExperimentalRules.IsPlayerInsideExperimentalEngineerMisdirectionFieldForVisuals(player);
    public static bool ShouldTreatPlayerAsExperimentalFriendlyFireTarget(PlayerEntity observer, PlayerEntity candidate)
        => ExperimentalRulesSystem.ShouldTreatPlayerAsExperimentalFriendlyFireTarget(observer, candidate);
    public bool IsPlayerInsideCapturedPointHealingAuraForVisuals(PlayerEntity? player)
        => ExperimentalRules.IsPlayerInsideCapturedPointHealingAuraForVisuals(player);
}
