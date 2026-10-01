#nullable enable

using OpenGarrison.ClientShared;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public sealed class GameplayRuntimeSettings
{
    public bool KillCamEnabled { get; set; } = true;

    public int ParticleMode { get; set; }

    public int FlameRenderMode { get; set; }

    public int BloodRenderMode { get; set; }

    public bool DynamicRagdollEnabled { get; set; } = true;

    public bool BurnCharredCorpsesEnabled { get; set; } = true;

    public int BloodPersistenceSeconds { get; set; } = OpenGarrisonPreferencesDocument.DefaultBloodPersistenceSeconds;

    public int CorpseFadeMode { get; set; } = OpenGarrisonPreferencesDocument.DefaultCorpseFadeMode;

    public MenuBackgroundMode MenuBackgroundMode { get; set; } = MenuBackgroundMode.DefaultMaps;

    public int GibLevel { get; set; } = 3;

    public int BloodAmountLevel { get; set; } = 5;

    public int CorpseDurationMode { get; set; }

    public bool PositionSmoothingEnabled { get; set; } = false;

    public bool EnablePrediction { get; set; } = true;

    public bool CameraPanningEnabled { get; set; } = OpenGarrisonPreferencesDocument.DefaultCameraPanningEnabled;

    public float SmoothCameraMultiplier { get; set; } = ClientSettings.DefaultSmoothCameraMultiplier;

    public bool SpriteDropShadowEnabled { get; set; }

    public bool StuckArrowsEnabled { get; set; } = true;

    public WeaponBobMode WeaponBobMode { get; set; } = WeaponBobMode.Enabled;

    public bool PixelPerfectWeaponRotation { get; set; } = true;

    public bool UseLocalWeaponRotation { get; set; } = false;

    public bool ShowUberOutlinesEnabled { get; set; } = true;

    public bool ProjectileTeamTintEnabled { get; set; } = true;
}
