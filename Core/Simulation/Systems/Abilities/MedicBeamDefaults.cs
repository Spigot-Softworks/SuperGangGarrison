namespace OpenGarrison.Core;

/// <summary>Shared Medic beam tuning values used by both the world's Medic rules and the ability system.</summary>
internal static class MedicBeamDefaults
{
    public const float UberChargeGainPerTickHealthyTarget = 1.75f;
    public const float HealBeamRange = 300f;
    public const float KritzBeamDefaultRange = HealBeamRange * 0.5f;
    public const float KritzBeamDefaultDamagePerSecond = 1f;
    public const float KritzBeamDefaultChargePerTick = UberChargeGainPerTickHealthyTarget;
}