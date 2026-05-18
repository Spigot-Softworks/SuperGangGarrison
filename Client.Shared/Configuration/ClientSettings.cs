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
    public const int PlayerCardSizeSmall = 0;
    public const int PlayerCardSizeMedium = 1;
    public const int PlayerCardSizeLarge = 2;

    public string PlayerName { get; set; } = "Player";

    public string Rewards { get; set; } = string.Empty;

    public bool Fullscreen { get; set; }

    public bool VSync { get; set; }

    public int FrameRateLimit { get; set; }

    public IngameResolutionKind IngameResolution { get; set; } = IngameResolutionKind.Aspect4x3;

    public MusicMode MusicMode { get; set; } = MusicMode.MenuAndInGame;

    public OfflineBotControllerMode BotMode { get; set; } = OfflineBotControllerMode.BotBrain;

    public bool IngameMusicEnabled
    {
        get => MusicMode is MusicMode.MenuAndInGame or MusicMode.InGameOnly;
        set => MusicMode = value ? MusicMode.MenuAndInGame : MusicMode.MenuOnly;
    }

    public int MenuMusicVolumePercent { get; set; } = 70;

    public int IngameMusicVolumePercent { get; set; } = 70;

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

    public bool PortraitRumbleEnabled { get; set; } = true;

    public bool DamageVignetteEnabled { get; set; } = true;

    public LowHealthColorMode LowHealthColorMode { get; set; } = LowHealthColorMode.Red;

    public bool ShowUberOutlinesEnabled { get; set; } = true;

    public bool ProjectileTeamTintEnabled { get; set; } = true;

    public bool AudioMuted { get; set; }

    public bool ShowPersistentSelfNameEnabled { get; set; }

    public bool PositionSmoothingEnabled { get; set; } = true;

    public float SmoothCameraMultiplier { get; set; } = DefaultSmoothCameraMultiplier;

    public bool SpriteDropShadowEnabled { get; set; }

    public bool DisableLegacyGameplaySpriteFallback { get; set; }

    public int PlayerCardSizeMode { get; set; } = PlayerCardSizeSmall;

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
            PortraitRumbleEnabled = document.PortraitRumbleEnabled,
            DamageVignetteEnabled = document.DamageVignetteEnabled,
            LowHealthColorMode = NormalizeLowHealthColorMode(document.LowHealthColorMode),
            ShowUberOutlinesEnabled = document.ShowUberOutlinesEnabled,
            ProjectileTeamTintEnabled = document.ProjectileTeamTintEnabled,
            AudioMuted = document.AudioMuted,
            MenuMusicVolumePercent = Math.Clamp(document.MenuMusicVolumePercent, 0, 100),
            IngameMusicVolumePercent = Math.Clamp(document.IngameMusicVolumePercent, 0, 100),
            SoundEffectsVolumePercent = Math.Clamp(document.SoundEffectsVolumePercent, 0, 100),
            ShowPersistentSelfNameEnabled = document.ShowPersistentSelfNameEnabled,
            PositionSmoothingEnabled = document.PositionSmoothingEnabled,
            SmoothCameraMultiplier = Math.Clamp(document.SmoothCameraMultiplier, 0f, 1f),
            SpriteDropShadowEnabled = document.SpriteDropShadowEnabled,
            DisableLegacyGameplaySpriteFallback = document.DisableLegacyGameplaySpriteFallback,
            PlayerCardSizeMode = NormalizePlayerCardSizeMode(document.PlayerCardSizeMode),
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
        preferences.PortraitRumbleEnabled = PortraitRumbleEnabled;
        preferences.DamageVignetteEnabled = DamageVignetteEnabled;
        preferences.LowHealthColorMode = NormalizeLowHealthColorMode(LowHealthColorMode);
        preferences.ShowUberOutlinesEnabled = ShowUberOutlinesEnabled;
        preferences.ProjectileTeamTintEnabled = ProjectileTeamTintEnabled;
        preferences.AudioMuted = AudioMuted;
        preferences.MenuMusicVolumePercent = MenuMusicVolumePercent;
        preferences.IngameMusicVolumePercent = IngameMusicVolumePercent;
        preferences.SoundEffectsVolumePercent = SoundEffectsVolumePercent;
        preferences.ShowPersistentSelfNameEnabled = ShowPersistentSelfNameEnabled;
        preferences.PositionSmoothingEnabled = PositionSmoothingEnabled;
        preferences.SmoothCameraMultiplier = Math.Clamp(SmoothCameraMultiplier, 0f, 1f);
        preferences.SpriteDropShadowEnabled = SpriteDropShadowEnabled;
        preferences.FrameRateLimit = FrameRateLimit;
        preferences.DisableLegacyGameplaySpriteFallback = DisableLegacyGameplaySpriteFallback;
        preferences.PlayerCardSizeMode = NormalizePlayerCardSizeMode(PlayerCardSizeMode);
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
}

public sealed class ClientRecentConnectionSettings
{
    public string Host { get; set; } = "127.0.0.1";

    public int Port { get; set; } = 8190;
}
