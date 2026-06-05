namespace OpenGarrison.Core;

public readonly record struct RoomObjectMarker(
    RoomObjectType Type,
    float X,
    float Y,
    float Width,
    float Height,
    string SpriteName,
    PlayerTeam? Team = null,
    string SourceName = "",
    float Value = 0f,
    ControlPointInitialOwnership InitialOwnership = ControlPointInitialOwnership.ModeDefault,
    ControlPointLockRules LockRules = default,
    float CapTimeMultiplier = 1f,
    bool IsCapTimeMultiplierCustom = false,
    BarrierConfiguration Barrier = default,
    DirectionalWallConfiguration DirectionalWall = default,
    TeleportZoneConfiguration TeleportZone = default,
    PlayerTriggerZoneConfiguration PlayerTriggerZone = default,
    CustomMapSpriteConfiguration CustomMapSprite = default,
    AreaExtensionConfiguration AreaExtension = default,
    DamageableZoneConfiguration DamageableZone = default)
{
    public float Left => X;

    public float Top => Y;

    public float Right => X + Width;

    public float Bottom => Y + Height;

    public float CenterX => X + (Width / 2f);

    public float CenterY => Y + (Height / 2f);

    public (float Multiplier, bool IsCustom) CapTimeMultiplierSettings => (CapTimeMultiplier, IsCapTimeMultiplierCustom);
}
