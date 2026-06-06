#nullable enable

using System.IO;
using OpenGarrison.Core;

namespace OpenGarrison.ClientShared;

public sealed class ClientSettings
{
    public const string DefaultFileName = OpenGarrisonPreferencesDocument.DefaultFileName;
    private const string LegacyFileName = "client.settings.json";
    public const int CorpseDurationDefault = 0;
    public const int CorpseDurationInfinite = 1;
    public const float DefaultSmoothCameraMultiplier = 0.35f;
    public const int DefaultDamageVignetteIntensityPercent = OpenGarrisonPreferencesDocument.DefaultDamageVignetteIntensityPercent;
    public const int PlayerCardSizeSmall = 0;
    public const int PlayerCardSizeMedium = 1;
    public const int PlayerCardSizeLarge = 2;

    public string PlayerName { get; set; } = "Player";

    public string Rewards { get; set; } = string.Empty;

    public bool Fullscreen { get; set; }

    public bool VSync { get; set; }

    public int FrameRateLimit { get; set; }

    public IngameResolutionKind IngameResolution { get; set; } = OpenGarrisonPreferencesDocument.DefaultIngameResolution;

    public WindowSizeKind WindowSize { get; set; } = OpenGarrisonPreferencesDocument.DefaultWindowSize;

    public MusicMode MusicMode { get; set; } = MusicMode.MenuAndInGame;

    public OfflineBotControllerMode BotMode { get; set; } = OfflineBotControllerMode.BotBrain;

    public bool IngameMusicEnabled
    {
        get => MusicMode is MusicMode.MenuAndInGame or MusicMode.InGameOnly;
        set => MusicMode = value ? MusicMode.MenuAndInGame : MusicMode.MenuOnly;
    }

    public int MenuMusicVolumePercent { get; set; } = 70;

    public int IngameMusicVolumePercent { get; set; } = 70;

    public bool DynamicMusicEnabled { get; set; } = true;

    public int CombatMusicVolumePercent { get; set; } = OpenGarrisonPreferencesDocument.DefaultCombatMusicVolumePercent;

    public int SoundEffectsVolumePercent { get; set; } = 70;

    public bool KillCamEnabled { get; set; } = true;

    public int ParticleMode { get; set; }

    public int FlameRenderMode { get; set; }

    public MenuBackgroundMode MenuBackgroundMode { get; set; } = MenuBackgroundMode.DefaultMaps;

    public int GibLevel { get; set; } = 3;

    public int CorpseDurationMode { get; set; }

    public bool HealerRadarEnabled { get; set; } = true;

    public bool ShowHealerEnabled { get; set; } = true;

    public bool ShowHealingEnabled { get; set; } = true;

    public bool ShowHealthBarEnabled { get; set; }

    public bool HudShowOnlyActiveWeapon { get; set; }

    public bool OverheadChatEnabled { get; set; } = OpenGarrisonPreferencesDocument.DefaultOverheadChatEnabled;

    public bool PortraitRumbleEnabled { get; set; } = true;

    public bool PostGameMvpArtEnabled { get; set; } = OpenGarrisonPreferencesDocument.DefaultPostGameMvpArtEnabled;

    public bool DamageVignetteEnabled { get; set; } = true;

    public int DamageVignetteIntensityPercent { get; set; } = DefaultDamageVignetteIntensityPercent;

    public LowHealthColorMode LowHealthColorMode { get; set; } = LowHealthColorMode.Red;

    public bool ShowUberOutlinesEnabled { get; set; } = true;

    public bool ProjectileTeamTintEnabled { get; set; } = true;

    public bool AudioMuted { get; set; }

    public bool ShowPersistentSelfNameEnabled { get; set; }

    public bool PositionSmoothingEnabled { get; set; } = true;

    public float SmoothCameraMultiplier { get; set; } = DefaultSmoothCameraMultiplier;

    public bool SpriteDropShadowEnabled { get; set; }

    public bool PixelPerfectWeaponRotation { get; set; } = true;

    public bool UseLocalWeaponRotation { get; set; } = false;

    public bool DisableLegacyGameplaySpriteFallback { get; set; }

    public int PlayerCardSizeMode { get; set; } = PlayerCardSizeSmall;

    public ControllerInputMode ControllerInputMode { get; set; } = ControllerInputMode.Auto;

    public ControllerReticleMode ControllerReticleMode { get; set; } = ControllerReticleMode.Cursor;

    public bool ControllerAimAssistEnabled { get; set; } = true;

    public bool ControllerFlickToChangeDirections { get; set; } = OpenGarrisonPreferencesDocument.DefaultControllerFlickToChangeDirections;

    public float ControllerAimAssistStrength { get; set; } = OpenGarrisonPreferencesDocument.DefaultControllerAimAssistStrength;

    public float ControllerAimDeadzone { get; set; } = OpenGarrisonPreferencesDocument.DefaultControllerAimDeadzone;

    public float ControllerScopedPrecisionSpeed { get; set; } = OpenGarrisonPreferencesDocument.DefaultControllerScopedPrecisionSpeed;

    public float ControllerAimDistanceTier1 { get; set; } = OpenGarrisonPreferencesDocument.DefaultControllerAimDistanceTier1;

    public float ControllerAimDistanceTier2 { get; set; } = OpenGarrisonPreferencesDocument.DefaultControllerAimDistanceTier2;

    public float ControllerAimDistanceTier3 { get; set; } = OpenGarrisonPreferencesDocument.DefaultControllerAimDistanceTier3;

    public ControllerButtonBinding ControllerJumpButton { get; set; } = OpenGarrisonPreferencesDocument.DefaultControllerJumpButton;

    public ControllerButtonBinding ControllerPrimaryFireButton { get; set; } = OpenGarrisonPreferencesDocument.DefaultControllerPrimaryFireButton;

    public ControllerButtonBinding ControllerSecondaryFireButton { get; set; } = OpenGarrisonPreferencesDocument.DefaultControllerSecondaryFireButton;

    public ControllerButtonBinding ControllerUseAbilityButton { get; set; } = OpenGarrisonPreferencesDocument.DefaultControllerUseAbilityButton;

    public ControllerButtonBinding ControllerInteractButton { get; set; } = OpenGarrisonPreferencesDocument.DefaultControllerInteractButton;

    public ControllerButtonBinding ControllerSwapWeaponButton { get; set; } = OpenGarrisonPreferencesDocument.DefaultControllerSwapWeaponButton;

    public ControllerButtonBinding ControllerScoreboardButton { get; set; } = OpenGarrisonPreferencesDocument.DefaultControllerScoreboardButton;

    public ControllerButtonBinding ControllerPauseButton { get; set; } = OpenGarrisonPreferencesDocument.DefaultControllerPauseButton;

    public ControllerButtonBinding ControllerAimDistanceButton { get; set; } = OpenGarrisonPreferencesDocument.DefaultControllerAimDistanceButton;

    public ControllerButtonBinding ControllerChangeTeamButton { get; set; } = OpenGarrisonPreferencesDocument.DefaultControllerChangeTeamButton;

    public ControllerButtonBinding ControllerChangeClassButton { get; set; } = OpenGarrisonPreferencesDocument.DefaultControllerChangeClassButton;

    public ClientRecentConnectionSettings RecentConnection { get; set; } = new();

    public OpenGarrisonHostSettings HostDefaults { get; set; } = new();

    public string LobbyHost { get; set; } = OpenGarrisonPreferencesDocument.DefaultLobbyHost;

    public int LobbyPort { get; set; } = OpenGarrisonPreferencesDocument.DefaultLobbyPort;

    public string DiscordApplicationId { get; set; } = string.Empty;

    public static ClientSettings Load(string? path = null)
    {
        if (OperatingSystem.IsBrowser())
        {
            return new ClientSettings
            {
                VSync = false,
                ParticleMode = 2,
            };
        }

        var resolvedPath = path ?? RuntimePaths.GetConfigPath(DefaultFileName);
        if (File.Exists(resolvedPath))
        {
            return LoadFromIni(resolvedPath);
        }

        if (OpenGarrisonLegacyPreferencesMigration.TryMigrate(resolvedPath))
        {
            return LoadFromIni(resolvedPath);
        }

        var legacyPath = RuntimePaths.GetConfigPath(LegacyFileName);
        if (File.Exists(legacyPath))
        {
            var migrated = JsonConfigurationFile.LoadOrCreate<ClientSettings>(legacyPath);
            migrated.Save(resolvedPath);
            return migrated;
        }

        var created = new ClientSettings();
        created.Save(resolvedPath);
        return created;
    }

    public void Save(string? path = null)
    {
        if (OperatingSystem.IsBrowser())
        {
            return;
        }

        var resolvedPath = path ?? RuntimePaths.GetConfigPath(DefaultFileName);
        var preferences = File.Exists(resolvedPath)
            ? OpenGarrisonPreferencesDocument.Load(resolvedPath)
            : new OpenGarrisonPreferencesDocument();
        ApplyTo(preferences);
        preferences.Save(resolvedPath);
    }

    private static ClientSettings LoadFromIni(string path)
    {
        var document = OpenGarrisonPreferencesDocument.Load(path);
        return new ClientSettings
        {
            PlayerName = document.PlayerName,
            Rewards = document.Rewards,
            Fullscreen = document.Fullscreen,
            VSync = document.VSync,
            FrameRateLimit = document.FrameRateLimit,
            MusicMode = document.MusicMode,
            BotMode = document.BotMode,
            IngameResolution = document.IngameResolution,
            WindowSize = OpenGarrisonPreferencesDocument.NormalizeWindowSize(document.WindowSize),
            ParticleMode = document.ParticleMode,
            FlameRenderMode = document.FlameRenderMode,
            MenuBackgroundMode = document.MenuBackgroundMode,
            GibLevel = document.GibLevel,
            CorpseDurationMode = document.CorpseDurationMode,
            KillCamEnabled = document.KillCamEnabled,
            HealerRadarEnabled = document.HealerRadarEnabled,
            ShowHealerEnabled = document.ShowHealerEnabled,
            ShowHealingEnabled = document.ShowHealingEnabled,
            ShowHealthBarEnabled = document.ShowHealthBarEnabled,
            HudShowOnlyActiveWeapon = document.HudShowOnlyActiveWeapon,
            OverheadChatEnabled = document.OverheadChatEnabled,
            PortraitRumbleEnabled = document.PortraitRumbleEnabled,
            PostGameMvpArtEnabled = document.PostGameMvpArtEnabled,
            DamageVignetteEnabled = document.DamageVignetteEnabled,
            DamageVignetteIntensityPercent = NormalizeDamageVignetteIntensityPercent(document.DamageVignetteIntensityPercent),
            LowHealthColorMode = NormalizeLowHealthColorMode(document.LowHealthColorMode),
            ShowUberOutlinesEnabled = document.ShowUberOutlinesEnabled,
            ProjectileTeamTintEnabled = document.ProjectileTeamTintEnabled,
            AudioMuted = document.AudioMuted,
            MenuMusicVolumePercent = Math.Clamp(document.MenuMusicVolumePercent, 0, 100),
            IngameMusicVolumePercent = Math.Clamp(document.IngameMusicVolumePercent, 0, 100),
            DynamicMusicEnabled = document.DynamicMusicEnabled,
            CombatMusicVolumePercent = Math.Clamp(document.CombatMusicVolumePercent, 0, OpenGarrisonPreferencesDocument.MaxCombatMusicVolumePercent),
            SoundEffectsVolumePercent = Math.Clamp(document.SoundEffectsVolumePercent, 0, 100),
            ShowPersistentSelfNameEnabled = document.ShowPersistentSelfNameEnabled,
            PositionSmoothingEnabled = document.PositionSmoothingEnabled,
            SmoothCameraMultiplier = Math.Clamp(document.SmoothCameraMultiplier, 0f, 1f),
            SpriteDropShadowEnabled = document.SpriteDropShadowEnabled,
            PixelPerfectWeaponRotation = document.PixelPerfectWeaponRotation,
            UseLocalWeaponRotation = document.UseLocalWeaponRotation,
            DisableLegacyGameplaySpriteFallback = document.DisableLegacyGameplaySpriteFallback,
            PlayerCardSizeMode = NormalizePlayerCardSizeMode(document.PlayerCardSizeMode),
            ControllerInputMode = OpenGarrisonPreferencesDocument.NormalizeControllerInputMode(document.ControllerInputMode),
            ControllerReticleMode = OpenGarrisonPreferencesDocument.NormalizeControllerReticleMode(document.ControllerReticleMode),
            ControllerAimAssistEnabled = document.ControllerAimAssistEnabled,
            ControllerFlickToChangeDirections = document.ControllerFlickToChangeDirections,
            ControllerAimAssistStrength = OpenGarrisonPreferencesDocument.NormalizeControllerAimAssistStrength(document.ControllerAimAssistStrength),
            ControllerAimDeadzone = OpenGarrisonPreferencesDocument.NormalizeControllerAimDeadzone(document.ControllerAimDeadzone),
            ControllerScopedPrecisionSpeed = OpenGarrisonPreferencesDocument.NormalizeControllerScopedPrecisionSpeed(document.ControllerScopedPrecisionSpeed),
            ControllerAimDistanceTier1 = OpenGarrisonPreferencesDocument.NormalizeControllerAimDistance(document.ControllerAimDistanceTier1, OpenGarrisonPreferencesDocument.DefaultControllerAimDistanceTier1),
            ControllerAimDistanceTier2 = OpenGarrisonPreferencesDocument.NormalizeControllerAimDistance(document.ControllerAimDistanceTier2, OpenGarrisonPreferencesDocument.DefaultControllerAimDistanceTier2),
            ControllerAimDistanceTier3 = OpenGarrisonPreferencesDocument.NormalizeControllerAimDistance(document.ControllerAimDistanceTier3, OpenGarrisonPreferencesDocument.DefaultControllerAimDistanceTier3),
            ControllerJumpButton = OpenGarrisonPreferencesDocument.NormalizeControllerButtonBinding(document.ControllerJumpButton),
            ControllerPrimaryFireButton = OpenGarrisonPreferencesDocument.NormalizeControllerButtonBinding(document.ControllerPrimaryFireButton),
            ControllerSecondaryFireButton = OpenGarrisonPreferencesDocument.NormalizeControllerButtonBinding(document.ControllerSecondaryFireButton),
            ControllerUseAbilityButton = OpenGarrisonPreferencesDocument.NormalizeControllerButtonBinding(document.ControllerUseAbilityButton),
            ControllerInteractButton = OpenGarrisonPreferencesDocument.NormalizeControllerButtonBinding(document.ControllerInteractButton),
            ControllerSwapWeaponButton = OpenGarrisonPreferencesDocument.NormalizeControllerButtonBinding(document.ControllerSwapWeaponButton),
            ControllerScoreboardButton = OpenGarrisonPreferencesDocument.NormalizeControllerButtonBinding(document.ControllerScoreboardButton),
            ControllerPauseButton = OpenGarrisonPreferencesDocument.NormalizeControllerButtonBinding(document.ControllerPauseButton),
            ControllerAimDistanceButton = OpenGarrisonPreferencesDocument.NormalizeControllerButtonBinding(document.ControllerAimDistanceButton),
            ControllerChangeTeamButton = OpenGarrisonPreferencesDocument.NormalizeControllerButtonBinding(document.ControllerChangeTeamButton),
            ControllerChangeClassButton = OpenGarrisonPreferencesDocument.NormalizeControllerButtonBinding(document.ControllerChangeClassButton),
            RecentConnection = new ClientRecentConnectionSettings
            {
                Host = document.RecentConnectionHost,
                Port = document.RecentConnectionPort,
            },
            HostDefaults = document.HostSettings.Clone(),
            LobbyHost = document.LobbyHost,
            LobbyPort = document.LobbyPort,
            DiscordApplicationId = document.DiscordApplicationId,
        };
    }

    private void ApplyTo(OpenGarrisonPreferencesDocument preferences)
    {
        preferences.PlayerName = PlayerName;
        preferences.Rewards = Rewards;
        preferences.Fullscreen = Fullscreen;
        preferences.VSync = VSync;
        preferences.IngameResolution = IngameResolution;
        preferences.WindowSize = OpenGarrisonPreferencesDocument.NormalizeWindowSize(WindowSize);
        preferences.MusicMode = MusicMode;
        preferences.BotMode = BotMode;
        preferences.KillCamEnabled = KillCamEnabled;
        preferences.ParticleMode = ParticleMode;
        preferences.FlameRenderMode = FlameRenderMode;
        preferences.MenuBackgroundMode = MenuBackgroundMode;
        preferences.GibLevel = GibLevel;
        preferences.CorpseDurationMode = CorpseDurationMode;
        preferences.HealerRadarEnabled = HealerRadarEnabled;
        preferences.ShowHealerEnabled = ShowHealerEnabled;
        preferences.ShowHealingEnabled = ShowHealingEnabled;
        preferences.ShowHealthBarEnabled = ShowHealthBarEnabled;
        preferences.HudShowOnlyActiveWeapon = HudShowOnlyActiveWeapon;
        preferences.OverheadChatEnabled = OverheadChatEnabled;
        preferences.PortraitRumbleEnabled = PortraitRumbleEnabled;
        preferences.PostGameMvpArtEnabled = PostGameMvpArtEnabled;
        preferences.DamageVignetteEnabled = DamageVignetteEnabled;
        preferences.DamageVignetteIntensityPercent = NormalizeDamageVignetteIntensityPercent(DamageVignetteIntensityPercent);
        preferences.LowHealthColorMode = NormalizeLowHealthColorMode(LowHealthColorMode);
        preferences.ShowUberOutlinesEnabled = ShowUberOutlinesEnabled;
        preferences.ProjectileTeamTintEnabled = ProjectileTeamTintEnabled;
        preferences.AudioMuted = AudioMuted;
        preferences.MenuMusicVolumePercent = MenuMusicVolumePercent;
        preferences.IngameMusicVolumePercent = IngameMusicVolumePercent;
        preferences.DynamicMusicEnabled = DynamicMusicEnabled;
        preferences.CombatMusicVolumePercent = Math.Clamp(CombatMusicVolumePercent, 0, OpenGarrisonPreferencesDocument.MaxCombatMusicVolumePercent);
        preferences.SoundEffectsVolumePercent = SoundEffectsVolumePercent;
        preferences.ShowPersistentSelfNameEnabled = ShowPersistentSelfNameEnabled;
        preferences.PositionSmoothingEnabled = PositionSmoothingEnabled;
        preferences.SmoothCameraMultiplier = Math.Clamp(SmoothCameraMultiplier, 0f, 1f);
        preferences.SpriteDropShadowEnabled = SpriteDropShadowEnabled;
        preferences.PixelPerfectWeaponRotation = PixelPerfectWeaponRotation;
        preferences.UseLocalWeaponRotation = UseLocalWeaponRotation;
        preferences.FrameRateLimit = FrameRateLimit;
        preferences.DisableLegacyGameplaySpriteFallback = DisableLegacyGameplaySpriteFallback;
        preferences.PlayerCardSizeMode = NormalizePlayerCardSizeMode(PlayerCardSizeMode);
        preferences.ControllerInputMode = OpenGarrisonPreferencesDocument.NormalizeControllerInputMode(ControllerInputMode);
        preferences.ControllerReticleMode = OpenGarrisonPreferencesDocument.NormalizeControllerReticleMode(ControllerReticleMode);
        preferences.ControllerAimAssistEnabled = ControllerAimAssistEnabled;
        preferences.ControllerFlickToChangeDirections = ControllerFlickToChangeDirections;
        preferences.ControllerAimAssistStrength = OpenGarrisonPreferencesDocument.NormalizeControllerAimAssistStrength(ControllerAimAssistStrength);
        preferences.ControllerAimDeadzone = OpenGarrisonPreferencesDocument.NormalizeControllerAimDeadzone(ControllerAimDeadzone);
        preferences.ControllerScopedPrecisionSpeed = OpenGarrisonPreferencesDocument.NormalizeControllerScopedPrecisionSpeed(ControllerScopedPrecisionSpeed);
        preferences.ControllerAimDistanceTier1 = OpenGarrisonPreferencesDocument.NormalizeControllerAimDistance(ControllerAimDistanceTier1, OpenGarrisonPreferencesDocument.DefaultControllerAimDistanceTier1);
        preferences.ControllerAimDistanceTier2 = OpenGarrisonPreferencesDocument.NormalizeControllerAimDistance(ControllerAimDistanceTier2, OpenGarrisonPreferencesDocument.DefaultControllerAimDistanceTier2);
        preferences.ControllerAimDistanceTier3 = OpenGarrisonPreferencesDocument.NormalizeControllerAimDistance(ControllerAimDistanceTier3, OpenGarrisonPreferencesDocument.DefaultControllerAimDistanceTier3);
        preferences.ControllerJumpButton = OpenGarrisonPreferencesDocument.NormalizeControllerButtonBinding(ControllerJumpButton);
        preferences.ControllerPrimaryFireButton = OpenGarrisonPreferencesDocument.NormalizeControllerButtonBinding(ControllerPrimaryFireButton);
        preferences.ControllerSecondaryFireButton = OpenGarrisonPreferencesDocument.NormalizeControllerButtonBinding(ControllerSecondaryFireButton);
        preferences.ControllerUseAbilityButton = OpenGarrisonPreferencesDocument.NormalizeControllerButtonBinding(ControllerUseAbilityButton);
        preferences.ControllerInteractButton = OpenGarrisonPreferencesDocument.NormalizeControllerButtonBinding(ControllerInteractButton);
        preferences.ControllerSwapWeaponButton = OpenGarrisonPreferencesDocument.NormalizeControllerButtonBinding(ControllerSwapWeaponButton);
        preferences.ControllerScoreboardButton = OpenGarrisonPreferencesDocument.NormalizeControllerButtonBinding(ControllerScoreboardButton);
        preferences.ControllerPauseButton = OpenGarrisonPreferencesDocument.NormalizeControllerButtonBinding(ControllerPauseButton);
        preferences.ControllerAimDistanceButton = OpenGarrisonPreferencesDocument.NormalizeControllerButtonBinding(ControllerAimDistanceButton);
        preferences.ControllerChangeTeamButton = OpenGarrisonPreferencesDocument.NormalizeControllerButtonBinding(ControllerChangeTeamButton);
        preferences.ControllerChangeClassButton = OpenGarrisonPreferencesDocument.NormalizeControllerButtonBinding(ControllerChangeClassButton);
        preferences.RecentConnectionHost = RecentConnection.Host;
        preferences.RecentConnectionPort = RecentConnection.Port;
        preferences.HostSettings = HostDefaults.Clone();
        preferences.LobbyHost = LobbyHost;
        preferences.LobbyPort = LobbyPort;
        preferences.DiscordApplicationId = DiscordApplicationId;
    }

    public static int NormalizePlayerCardSizeMode(int sizeMode)
    {
        return Math.Clamp(sizeMode, PlayerCardSizeSmall, PlayerCardSizeLarge);
    }

    public static LowHealthColorMode NormalizeLowHealthColorMode(LowHealthColorMode mode)
    {
        return mode switch
        {
            LowHealthColorMode.None => LowHealthColorMode.None,
            LowHealthColorMode.Red => LowHealthColorMode.Red,
            _ => LowHealthColorMode.Red,
        };
    }

    public static int NormalizeDamageVignetteIntensityPercent(int percent)
    {
        return Math.Clamp(percent, 0, 100);
    }
}

public sealed class ClientRecentConnectionSettings
{
    public string Host { get; set; } = "127.0.0.1";

    public int Port { get; set; } = 8190;
}
