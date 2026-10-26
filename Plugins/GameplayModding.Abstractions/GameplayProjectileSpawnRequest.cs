namespace OpenGarrison.GameplayModding;

/// <summary>
/// A request to spawn a projectile in gameplay.
/// </summary>
/// <param name="OwnerPlayerId">The owning player id.</param>
/// <param name="Kind">The projectile kind (see <see cref="GameplayProjectileKinds"/>).</param>
/// <param name="X">The spawn X position.</param>
/// <param name="Y">The spawn Y position.</param>
/// <param name="VelocityX">The X velocity.</param>
/// <param name="VelocityY">The Y velocity.</param>
/// <param name="Speed">The speed.</param>
/// <param name="DirectionRadians">The direction in radians.</param>
/// <param name="Damage">The damage.</param>
/// <param name="KillFeedWeaponSpriteName">The kill feed weapon sprite name, if any.</param>
/// <param name="LifetimeTicks">How long a blade flies before it vanishes, in ticks; 0 keeps the stock lifetime.</param>
/// <param name="KnockbackScale">Scales the push a blade gives the player it hits (1 is the stock push).</param>
/// <param name="DeathType">How a kill by a blade leaves the body: "gib", "decapitated", "bisected", "severe", "rifle", or null for the usual corpse.</param>
public sealed record GameplayProjectileSpawnRequest(
    int OwnerPlayerId,
    string Kind,
    float X,
    float Y,
    float VelocityX = 0f,
    float VelocityY = 0f,
    float Speed = 0f,
    float DirectionRadians = 0f,
    float Damage = 0f,
    string? KillFeedWeaponSpriteName = null,
    int LifetimeTicks = 0,
    float KnockbackScale = 1f,
    string? DeathType = null);

/// <summary>
/// Well-known projectile kinds.
/// </summary>
public static class GameplayProjectileKinds
{
    /// <summary>A hitscan-style shot.</summary>
    public const string Shot = "shot";
    /// <summary>A needle.</summary>
    public const string Needle = "needle";
    /// <summary>A revolver bullet.</summary>
    public const string Revolver = "revolver";
    /// <summary>A rocket.</summary>
    public const string Rocket = "rocket";
    /// <summary>A mine.</summary>
    public const string Mine = "mine";
    /// <summary>A grenade.</summary>
    public const string Grenade = "grenade";
    /// <summary>A flame particle.</summary>
    public const string Flame = "flame";
    /// <summary>A flare.</summary>
    public const string Flare = "flare";
    /// <summary>A bubble.</summary>
    public const string Bubble = "bubble";
    /// <summary>A blade.</summary>
    public const string Blade = "blade";
}
