#nullable enable

using OpenGarrison.ClientShared;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public sealed class GameplayRuntimeSettings
{
    private int _bloodRenderMode;
    private bool _dynamicRagdollEnabled = true;
    private bool _burnCharredCorpsesEnabled = true;
    private int _corpseFadeMode = OpenGarrisonPreferencesDocument.DefaultCorpseFadeMode;
    private int _corpseDurationMode;

    /// <summary>
    /// The original GG2 look, as when playing on a GG2 server: stock sprites and animations
    /// (no player skins), classic blood, static dead-body sprites (no ragdolls, no burnt
    /// corpses, plain fade) and corpses that time out. While on, the gore settings below
    /// read as the classic values; the player's own choices are kept (Stored*) and come back
    /// when it is turned off.
    /// </summary>
    public bool ClassicVisualsEnabled
    {
        get => _classicVisualsEnabled || IsClassicVisualsForcedByEdition;
        set => _classicVisualsEnabled = value;
    }

    private bool _classicVisualsEnabled;

    /// <summary>The player's own Classic GG2 Visuals choice (saved), whatever the edition forces.</summary>
    public bool StoredClassicVisualsEnabled => _classicVisualsEnabled;

    /// <summary>
    /// The limited editions (the practice / Last-to-Die browser build and the GG2-only build)
    /// always use the original GG2 sprites, effects and gore.
    /// </summary>
    public static bool IsClassicVisualsForcedByEdition => ClientDistribution.IsRestricted || ClientDistribution.IsGg2Only;

    public bool MapLightingEnabled { get; set; } = true;

    /// <summary>Every map light pulses with the jukebox music (when the player can hear it).</summary>
    public bool LightsFollowJukeboxEnabled { get; set; }

    /// <summary>Brightmapped parts of characters keep some of their colour in the dark (experimental).</summary>
    public bool CharacterBrightmapsEnabled { get; set; } = true;

    public bool WeatherEnabled { get; set; } = true;

    public int LightingIntensityPercent { get; set; } = OpenGarrisonPreferencesDocument.DefaultLightingIntensityPercent;

    /// <summary>Classic blood style (the alternative, 0, is Squib).</summary>
    public const int ClassicBloodRenderMode = 1;

    /// <summary>Regular alpha fade (the alternative, 1, is the dissolve).</summary>
    public const int ClassicCorpseFadeMode = 0;

    public int BloodRenderMode
    {
        get => ClassicVisualsEnabled ? ClassicBloodRenderMode : _bloodRenderMode;
        set => _bloodRenderMode = value;
    }

    public bool DynamicRagdollEnabled
    {
        get => !ClassicVisualsEnabled && _dynamicRagdollEnabled;
        set => _dynamicRagdollEnabled = value;
    }

    public bool BurnCharredCorpsesEnabled
    {
        get => !ClassicVisualsEnabled && _burnCharredCorpsesEnabled;
        set => _burnCharredCorpsesEnabled = value;
    }

    public int CorpseFadeMode
    {
        get => ClassicVisualsEnabled ? ClassicCorpseFadeMode : _corpseFadeMode;
        set => _corpseFadeMode = value;
    }

    public int CorpseDurationMode
    {
        get => ClassicVisualsEnabled ? ClientSettings.CorpseDurationDefault : _corpseDurationMode;
        set => _corpseDurationMode = value;
    }

    /// <summary>The player's own choices, whatever Classic visuals forces (for saving and the menu toggles).</summary>
    public int StoredBloodRenderMode => _bloodRenderMode;

    public bool StoredDynamicRagdollEnabled => _dynamicRagdollEnabled;

    public bool StoredBurnCharredCorpsesEnabled => _burnCharredCorpsesEnabled;

    public int StoredCorpseFadeMode => _corpseFadeMode;

    public int StoredCorpseDurationMode => _corpseDurationMode;

    public bool KillCamEnabled { get; set; } = true;

    public int ParticleMode { get; set; }

    public int FlameRenderMode { get; set; }


    public int BloodPersistenceSeconds { get; set; } = OpenGarrisonPreferencesDocument.DefaultBloodPersistenceSeconds;


    public MenuBackgroundMode MenuBackgroundMode { get; set; } = MenuBackgroundMode.DefaultMaps;

    public int GibLevel { get; set; } = 3;

    public int BloodAmountLevel { get; set; } = 5;


    public bool PositionSmoothingEnabled { get; set; } = false;

    public bool EnablePrediction { get; set; } = true;

    /// <summary>On GG2 servers, run the local player ahead with its own input (default on).</summary>
    public bool Gg2PredictionEnabled { get; set; } = true;

    public bool CameraPanningEnabled { get; set; } = OpenGarrisonPreferencesDocument.DefaultCameraPanningEnabled;

    public float SmoothCameraMultiplier { get; set; } = ClientSettings.DefaultSmoothCameraMultiplier;

    public bool SpriteDropShadowEnabled { get; set; }

    public bool StuckArrowsEnabled { get; set; } = true;

    public WeaponBobMode WeaponBobMode { get; set; } = WeaponBobMode.Enabled;

    public bool PixelPerfectWeaponRotation { get; set; } = true;

    public bool UseLocalWeaponRotation { get; set; } = true;

    public bool ShowUberOutlinesEnabled { get; set; } = true;

    public bool ProjectileTeamTintEnabled { get; set; } = true;
}
