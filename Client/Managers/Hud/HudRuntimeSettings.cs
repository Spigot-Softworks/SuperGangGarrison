#nullable enable

using OpenGarrison.ClientShared;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public sealed class HudRuntimeSettings
{
    public bool HealerRadarEnabled { get; set; } = true;

    public bool ShowHealerEnabled { get; set; } = true;

    public bool ShowHealingEnabled { get; set; } = true;

    public bool ShowHealthBarEnabled { get; set; }

    public bool ShowShieldBarEnabled { get; set; } = true;

    public bool HudShowOnlyActiveWeapon { get; set; }

    public bool OverheadChatEnabled { get; set; } = OpenGarrisonPreferencesDocument.DefaultOverheadChatEnabled;

    public bool PortraitRumbleEnabled { get; set; } = true;

    public bool PostGameMvpArtEnabled { get; set; }

    public bool DamageVignetteEnabled { get; set; } = true;

    public int DamageVignetteIntensityPercent { get; set; } = ClientSettings.DefaultDamageVignetteIntensityPercent;

    public LowHealthColorMode LowHealthColorMode { get; set; } = LowHealthColorMode.Red;

    public bool ShowPersistentSelfNameEnabled { get; set; }

    public bool ShowPlayerNamesEnabled { get; set; } = true;

    public int PlayerCardSizeMode { get; set; } = ClientSettings.PlayerCardSizeSmall;

    public int CursorSizePercent { get; set; } = ClientSettings.DefaultCursorSizePercent;
}
