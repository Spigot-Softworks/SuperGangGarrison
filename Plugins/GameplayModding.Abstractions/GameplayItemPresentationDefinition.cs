namespace OpenGarrison.GameplayModding;

/// <summary>
/// Visual presentation of a gameplay item (sprites, HUD, and animation).
/// </summary>
/// <param name="WorldSpriteName">The world sprite name, if any.</param>
/// <param name="RecoilSpriteName">The recoil sprite name, if any.</param>
/// <param name="ReloadSpriteName">The reload sprite name, if any.</param>
/// <param name="RecoilCarrierSpriteName">The recoil carrier sprite name, if any.</param>
/// <param name="RecoilOverlaySpriteName">The recoil overlay sprite name, if any.</param>
/// <param name="RecoilOverlayOffsetX">The recoil overlay X offset.</param>
/// <param name="RecoilOverlayOffsetY">The recoil overlay Y offset.</param>
/// <param name="RecoilOverlayRotationDegrees">The recoil overlay rotation in degrees.</param>
/// <param name="ReloadCarrierSpriteName">The reload carrier sprite name, if any.</param>
/// <param name="ReloadOverlaySpriteName">The reload overlay sprite name, if any.</param>
/// <param name="ReloadOverlayOffsetX">The reload overlay X offset.</param>
/// <param name="ReloadOverlayOffsetY">The reload overlay Y offset.</param>
/// <param name="ReloadOverlayRotationDegrees">The reload overlay rotation in degrees.</param>
/// <param name="ReloadSpriteOffsetX">The reload sprite X offset.</param>
/// <param name="ReloadSpriteOffsetY">The reload sprite Y offset.</param>
/// <param name="HudSpriteName">The HUD sprite name, if any.</param>
/// <param name="WeaponOffsetX">The weapon X offset.</param>
/// <param name="WeaponOffsetY">The weapon Y offset.</param>
/// <param name="RecoilDurationSourceTicks">The recoil duration in source ticks.</param>
/// <param name="ReloadDurationSourceTicks">The reload duration in source ticks.</param>
/// <param name="ScopedRecoilDurationSourceTicks">The scoped recoil duration in source ticks.</param>
/// <param name="LoopRecoilWhileActive">Whether the recoil animation loops while active.</param>
/// <param name="LoopReloadAnimation">Whether the reload animation loops.</param>
/// <param name="BlueTeamHudFrameOffset">The blue team HUD frame offset.</param>
/// <param name="UseAmmoCountForHudFrame">Whether the ammo count selects the HUD frame.</param>
/// <param name="BlueTeamAmmoHudFrameOffset">The blue team ammo HUD frame offset.</param>
/// <param name="UseTorsoReplacement">When true, legs-only body drawing is used and a torso layer is drawn on top.</param>
/// <param name="TorsoSpriteName">The idle torso companion strip, if any.</param>
/// <param name="TorsoRecoilSpriteName">The attack torso companion strip, if any.</param>
/// <param name="MeleeHitboxSpriteName">The melee hitbox mask sprite name, if any.</param>
/// <param name="RotateMeleeHitboxWithAim">Whether the melee hitbox rotates with aim angle.</param>
/// <param name="Hud">The HUD presentation definition, if any.</param>
public sealed record GameplayItemPresentationDefinition(
    string? WorldSpriteName = null,
    string? RecoilSpriteName = null,
    string? ReloadSpriteName = null,
    string? RecoilCarrierSpriteName = null,
    string? RecoilOverlaySpriteName = null,
    float RecoilOverlayOffsetX = 0f,
    float RecoilOverlayOffsetY = 0f,
    float RecoilOverlayRotationDegrees = 0f,
    string? ReloadCarrierSpriteName = null,
    string? ReloadOverlaySpriteName = null,
    float ReloadOverlayOffsetX = 0f,
    float ReloadOverlayOffsetY = 0f,
    float ReloadOverlayRotationDegrees = 0f,
    float ReloadSpriteOffsetX = 0f,
    float ReloadSpriteOffsetY = 0f,
    string? HudSpriteName = null,
    float WeaponOffsetX = 0f,
    float WeaponOffsetY = 0f,
    int RecoilDurationSourceTicks = 0,
    int ReloadDurationSourceTicks = 0,
    int ScopedRecoilDurationSourceTicks = 0,
    bool LoopRecoilWhileActive = false,
    bool LoopReloadAnimation = true,
    int BlueTeamHudFrameOffset = 1,
    bool UseAmmoCountForHudFrame = false,
    int BlueTeamAmmoHudFrameOffset = 0,
    /// <summary>
    /// When true, legs-only body drawing is used and a torso layer is drawn on top.
    /// Without <see cref="TorsoSpriteName"/>, <see cref="WorldSpriteName"/> /
    /// <see cref="RecoilSpriteName"/> are the full torso replacement (Eyelander-style).
    /// With <see cref="TorsoSpriteName"/>, those world/recoil sprites remain the aimable
    /// weapon layers and the torso strips animate in sync underneath.
    /// </summary>
    bool UseTorsoReplacement = false,
    /// <summary>
    /// Idle torso companion strip. When set with <see cref="UseTorsoReplacement"/>,
    /// world/recoil sprites are treated as the rotating weapon rather than the torso.
    /// </summary>
    string? TorsoSpriteName = null,
    /// <summary>
    /// Attack torso companion strip. Frame indices should match the weapon recoil strip
    /// when both are present (e.g. idle/idle/atk2 torso for a 3-frame whip).
    /// </summary>
    string? TorsoRecoilSpriteName = null,
    /// <summary>
    /// Optional pack sprite whose opaque pixels define a melee swing area (alpha mask),
    /// anchored at the wielder with the sprite origin — same idea as a stab mask, but
    /// authored as art. Used for hit detection; aim still drives facing and reflect angle.
    /// </summary>
    string? MeleeHitboxSpriteName = null,
    /// <summary>
    /// When true, the melee hitbox rotates with aim angle. When false, the mask only
    /// flips horizontally with facing (Eyelander-style).
    /// </summary>
    bool RotateMeleeHitboxWithAim = false,
    GameplayItemHudPresentationDefinition? Hud = null);

/// <summary>
/// HUD presentation for a gameplay item.
/// </summary>
/// <param name="DisplayKind">The display kind (see <see cref="GameplayItemHudDisplayKinds"/>).</param>
/// <param name="StackGroup">The stack group (see <see cref="GameplayItemHudStackGroups"/>).</param>
/// <param name="Order">The display order.</param>
/// <param name="StateProvider">The state provider (see <see cref="GameplayItemHudStateProviders"/>).</param>
/// <param name="HideWhenUnavailable">Whether to hide the widget when unavailable.</param>
/// <param name="ShowWhenEquippedOnly">Whether to show the widget only when equipped.</param>
/// <param name="UseBackgroundPlaque">Whether to draw a background plaque.</param>
/// <param name="StateOwner">The state owner.</param>
/// <param name="CooldownKey">The cooldown key.</param>
/// <param name="MaxCooldown">The maximum cooldown.</param>
/// <param name="ActiveKey">The active key.</param>
/// <param name="DisabledKey">The disabled key.</param>
/// <param name="WidgetId">The widget id (see <see cref="GameplayItemHudWidgetIds"/>).</param>
/// <param name="WidgetOwner">The widget owner.</param>
/// <param name="WidgetCallback">The widget callback.</param>
/// <param name="Anchor">The widget anchor.</param>
public sealed record GameplayItemHudPresentationDefinition(
    string DisplayKind = "",
    string StackGroup = "",
    int Order = 0,
    string StateProvider = "",
    bool HideWhenUnavailable = false,
    bool ShowWhenEquippedOnly = false,
    bool UseBackgroundPlaque = false,
    string StateOwner = "",
    string CooldownKey = "",
    int MaxCooldown = 0,
    string ActiveKey = "",
    string DisabledKey = "",
    string WidgetId = "",
    string WidgetOwner = "",
    string WidgetCallback = "",
    string Anchor = "");

/// <summary>
/// Well-known HUD display kinds for gameplay items.
/// </summary>
public static class GameplayItemHudDisplayKinds
{
    /// <summary>No display.</summary>
    public const string None = "none";
    /// <summary>An ammo panel.</summary>
    public const string AmmoPanel = "ammoPanel";
    /// <summary>A meter.</summary>
    public const string Meter = "meter";
    /// <summary>A cooldown icon.</summary>
    public const string CooldownIcon = "cooldownIcon";
    /// <summary>A custom display.</summary>
    public const string Custom = "custom";
    /// <summary>A count display.</summary>
    public const string Count = "count";
    /// <summary>A prompt.</summary>
    public const string Prompt = "prompt";
}

/// <summary>
/// Well-known HUD stack groups for gameplay items.
/// </summary>
public static class GameplayItemHudStackGroups
{
    /// <summary>The weapon stack group.</summary>
    public const string Weapon = "weapon";
    /// <summary>The ability stack group.</summary>
    public const string Ability = "ability";
    /// <summary>The status stack group.</summary>
    public const string Status = "status";
}

/// <summary>
/// Well-known HUD state providers for gameplay items.
/// </summary>
public static class GameplayItemHudStateProviders
{
    /// <summary>Primary ammo state.</summary>
    public const string PrimaryAmmo = "primaryAmmo";
    /// <summary>Secondary ammo state.</summary>
    public const string SecondaryAmmo = "secondaryAmmo";
    /// <summary>Utility ammo state.</summary>
    public const string UtilityAmmo = "utilityAmmo";
    /// <summary>Reload progress state.</summary>
    public const string ReloadProgress = "reloadProgress";
    /// <summary>Cooldown state.</summary>
    public const string Cooldown = "cooldown";
    /// <summary>Ability cooldown state.</summary>
    public const string AbilityCooldown = "abilityCooldown";
    /// <summary>Custom state.</summary>
    public const string Custom = "custom";
    /// <summary>Heavy sandvich cooldown state.</summary>
    public const string HeavySandvichCooldown = "heavySandvichCooldown";
    /// <summary>Heavy ghost dash cooldown state.</summary>
    public const string HeavyGhostDashCooldown = "heavyGhostDashCooldown";
    /// <summary>Spy superjump cooldown state.</summary>
    public const string SpySuperjumpCooldown = "spySuperjumpCooldown";
    /// <summary>Sticky count state.</summary>
    public const string StickyCount = "stickyCount";
    /// <summary>Uber state.</summary>
    public const string Uber = "uber";
    /// <summary>Metal state.</summary>
    public const string Metal = "metal";
    /// <summary>Sentry state.</summary>
    public const string Sentry = "sentry";
}

/// <summary>
/// Well-known HUD widget ids for gameplay items.
/// </summary>
public static class GameplayItemHudWidgetIds
{
    /// <summary>The ability cooldown meter widget.</summary>
    public const string AbilityCooldownMeter = "abilityCooldownMeter";
    /// <summary>The weapon ammo panel widget.</summary>
    public const string WeaponAmmoPanel = "weaponAmmoPanel";
}
