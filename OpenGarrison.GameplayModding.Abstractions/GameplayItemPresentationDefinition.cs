namespace OpenGarrison.GameplayModding;

public sealed record GameplayItemPresentationDefinition(
    string? WorldSpriteName = null,
    string? RecoilSpriteName = null,
    string? ReloadSpriteName = null,
    string? HudSpriteName = null,
    float WeaponOffsetX = 0f,
    float WeaponOffsetY = 0f,
    int RecoilDurationSourceTicks = 0,
    int ReloadDurationSourceTicks = 0,
    int ScopedRecoilDurationSourceTicks = 0,
    bool LoopRecoilWhileActive = false,
    int BlueTeamHudFrameOffset = 1,
    bool UseAmmoCountForHudFrame = false,
    int BlueTeamAmmoHudFrameOffset = 0);
