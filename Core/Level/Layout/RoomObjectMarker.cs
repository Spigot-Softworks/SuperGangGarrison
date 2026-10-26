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
    DamageableZoneConfiguration DamageableZone = default,
    ForegroundSpriteConfiguration ForegroundSprite = default,
    SpritesheetConfiguration Spritesheet = default,
    CatapultConfiguration Catapult = default)
{
    /// <summary>Stable source-entity identity for links between overlapping map objects.</summary>
    public string MapEntityId { get; init; } = string.Empty;
    public string ObjectName { get; init; } = string.Empty;
    public string ObjectGroups { get; init; } = string.Empty;

    public int DestructibleZoneIndex { get; init; } = -1;
    public DestructiblePropSettings DestructionSettings { get; init; } = new(TransitionSeconds: 0.5f);
    public float Left => X;

    public float Top => Y;

    public float Right => X + Width;

    public float Bottom => Y + Height;

    public float CenterX => X + (Width / 2f);

    public float CenterY => Y + (Height / 2f);

    public (float Multiplier, bool IsCustom) CapTimeMultiplierSettings => (CapTimeMultiplier, IsCapTimeMultiplierCustom);
}
