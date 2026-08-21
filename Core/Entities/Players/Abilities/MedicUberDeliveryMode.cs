namespace OpenGarrison.Core;

/// <summary>
/// The effect an active Medic charge delivers. This is separate from
/// <see cref="PlayerEntity.IsUbered"/>, which represents any currently active
/// invulnerability source rather than the Medic's charge runtime.
/// </summary>
public enum MedicUberDeliveryMode : byte
{
    None = 0,
    RegularInvulnerability = 1,
    Kritz = 2,
    RejuvenationRay = 3,
}
