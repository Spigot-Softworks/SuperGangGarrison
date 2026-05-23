namespace OpenGarrison.GameplayModding;

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
    string? KillFeedWeaponSpriteName = null);

public static class GameplayProjectileKinds
{
    public const string Shot = "shot";
    public const string Needle = "needle";
    public const string Revolver = "revolver";
    public const string Rocket = "rocket";
    public const string Mine = "mine";
    public const string Grenade = "grenade";
    public const string Flame = "flame";
    public const string Flare = "flare";
    public const string Bubble = "bubble";
    public const string Blade = "blade";
}
