namespace OpenGarrison.GameplayModding;

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
    string? HudSpriteName = null,
    float WeaponOffsetX = 0f,
    float WeaponOffsetY = 0f,
    int RecoilDurationSourceTicks = 0,
    int ReloadDurationSourceTicks = 0,
    int ScopedRecoilDurationSourceTicks = 0,
    bool LoopRecoilWhileActive = false,
    int BlueTeamHudFrameOffset = 1,
    bool UseAmmoCountForHudFrame = false,
    int BlueTeamAmmoHudFrameOffset = 0,
    GameplayItemHudPresentationDefinition? Hud = null);

public sealed record GameplayItemHudPresentationDefinition(
    string DisplayKind = "",
    string StackGroup = "",
    int Order = 0,
    string StateProvider = "",
    bool HideWhenUnavailable = false,
    bool ShowWhenEquippedOnly = false,
    string StateOwner = "",
    string CooldownKey = "",
    int MaxCooldown = 0,
    string ActiveKey = "",
    string DisabledKey = "",
    string WidgetId = "",
    string WidgetOwner = "",
    string WidgetCallback = "",
    string Anchor = "");

public static class GameplayItemHudDisplayKinds
{
    public const string None = "none";
    public const string AmmoPanel = "ammoPanel";
    public const string Meter = "meter";
    public const string CooldownIcon = "cooldownIcon";
    public const string Custom = "custom";
    public const string Count = "count";
    public const string Prompt = "prompt";
}

public static class GameplayItemHudStackGroups
{
    public const string Weapon = "weapon";
    public const string Ability = "ability";
    public const string Status = "status";
}

public static class GameplayItemHudStateProviders
{
    public const string PrimaryAmmo = "primaryAmmo";
    public const string SecondaryAmmo = "secondaryAmmo";
    public const string UtilityAmmo = "utilityAmmo";
    public const string ReloadProgress = "reloadProgress";
    public const string Cooldown = "cooldown";
    public const string AbilityCooldown = "abilityCooldown";
    public const string Custom = "custom";
    public const string HeavySandvichCooldown = "heavySandvichCooldown";
    public const string HeavyGhostDashCooldown = "heavyGhostDashCooldown";
    public const string SpySuperjumpCooldown = "spySuperjumpCooldown";
    public const string StickyCount = "stickyCount";
    public const string Uber = "uber";
    public const string Metal = "metal";
    public const string Sentry = "sentry";
}

public static class GameplayItemHudWidgetIds
{
    public const string AbilityCooldownMeter = "abilityCooldownMeter";
    public const string WeaponAmmoPanel = "weaponAmmoPanel";
}
