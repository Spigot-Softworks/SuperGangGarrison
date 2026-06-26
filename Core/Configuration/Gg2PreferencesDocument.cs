using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace OpenGarrison.Core;

public sealed class OpenGarrisonPreferencesDocument
{
    public const string DefaultFileName = "OpenGarrison.ini";
    public const string DefaultApiBaseUrl = "https://api.superganggarrison.com";
    public const string DefaultLobbyHost = DefaultApiBaseUrl + "/api/servers";

    // The backend moved off this host. Saved configs that still point at it are transparently
    // rewritten to the canonical Super Gang Garrison backend so the old domain can be retired.
    public const string LegacyApiHost = "api.unkind-dev.com";
    public const string CanonicalApiHost = "api.superganggarrison.com";

    public static string MigrateLegacyApiHost(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value ?? string.Empty;
        }

        return value.Contains(LegacyApiHost, StringComparison.OrdinalIgnoreCase)
            ? value.Replace(LegacyApiHost, CanonicalApiHost, StringComparison.OrdinalIgnoreCase)
            : value;
    }
    public const int DefaultLobbyPort = 443;
    public const DisplayModeKind DefaultDisplayMode = DisplayModeKind.Windowed;
    public const IngameResolutionKind DefaultIngameResolution = IngameResolutionKind.Aspect16x9;
    public const WindowSizeKind DefaultWindowSize = WindowSizeKind.Scale100;
    public const DisplayScaleModeKind DefaultDisplayScaleMode = DisplayScaleModeKind.Fill;
    public const bool DefaultOverheadChatEnabled = true;
    public const BubbleWheelBehavior DefaultBubbleWheelBehavior = BubbleWheelBehavior.PressAndClick;
    public const int DefaultDamageVignetteIntensityPercent = 100;
    public const int DefaultCombatMusicVolumePercent = 120;
    public const bool DefaultPostGameMvpArtEnabled = true;
    public const int MaxCombatMusicVolumePercent = 300;
    public const float DefaultControllerAimAssistStrength = 0.6f;
    public const float DefaultControllerAimDeadzone = 0.22f;
    public const float DefaultControllerScopedPrecisionSpeed = 180f;
    public const float DefaultControllerAimDistanceTier1 = 96f;
    public const float DefaultControllerAimDistanceTier2 = 160f;
    public const float DefaultControllerAimDistanceTier3 = 240f;
    public const bool DefaultControllerFlickToChangeDirections = true;
    public const ControllerButtonBinding DefaultControllerJumpButton = ControllerButtonBinding.A;
    public const ControllerButtonBinding DefaultControllerPrimaryFireButton = ControllerButtonBinding.RightTrigger;
    public const ControllerButtonBinding DefaultControllerSecondaryFireButton = ControllerButtonBinding.LeftTrigger;
    public const ControllerButtonBinding DefaultControllerUseAbilityButton = ControllerButtonBinding.RightShoulder;
    public const ControllerButtonBinding DefaultControllerInteractButton = ControllerButtonBinding.X;
    public const ControllerButtonBinding DefaultControllerSwapWeaponButton = ControllerButtonBinding.Y;
    public const ControllerButtonBinding DefaultControllerScoreboardButton = ControllerButtonBinding.Back;
    public const ControllerButtonBinding DefaultControllerPauseButton = ControllerButtonBinding.Start;
    public const ControllerButtonBinding DefaultControllerAimDistanceButton = ControllerButtonBinding.RightStick;
    public const ControllerButtonBinding DefaultControllerChangeTeamButton = ControllerButtonBinding.None;
    public const ControllerButtonBinding DefaultControllerChangeClassButton = ControllerButtonBinding.None;
    private const string SettingsSection = "Settings";
    private const string ServerSection = "Server";
    private const string ConnectionSection = "Connection";
    private const string ServerAdvancedSection = "Server.Advanced";

    public string PlayerName { get; set; } = "Player";

    public string Rewards { get; set; } = string.Empty;

    private DisplayModeKind _displayMode = DefaultDisplayMode;

    public bool Fullscreen
    {
        get => DisplayMode == DisplayModeKind.Fullscreen;
        set => DisplayMode = value ? DisplayModeKind.Fullscreen : DisplayModeKind.Windowed;
    }

    public DisplayModeKind DisplayMode
    {
        get => _displayMode;
        set => _displayMode = NormalizeDisplayMode(value);
    }

    public bool VSync { get; set; }

    public int FrameRateLimit { get; set; }

    public IngameResolutionKind IngameResolution { get; set; } = DefaultIngameResolution;

    public WindowSizeKind WindowSize { get; set; } = DefaultWindowSize;

    public DisplayScaleModeKind DisplayScaleMode { get; set; } = DefaultDisplayScaleMode;

    public MusicMode MusicMode { get; set; } = MusicMode.MenuAndInGame;

    public OfflineBotControllerMode BotMode { get; set; } = OfflineBotControllerMode.BotBrain;

    public bool IngameMusicEnabled
    {
        get => MusicMode is MusicMode.MenuAndInGame or MusicMode.InGameOnly;
        set => MusicMode = value ? MusicMode.MenuAndInGame : MusicMode.MenuOnly;
    }

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

    public bool ShowShieldBarEnabled { get; set; } = true;

    public bool HudShowOnlyActiveWeapon { get; set; }

    public bool OverheadChatEnabled { get; set; } = DefaultOverheadChatEnabled;

    public BubbleWheelBehavior BubbleWheelBehavior { get; set; } = DefaultBubbleWheelBehavior;

    public bool PortraitRumbleEnabled { get; set; } = true;

    public bool PostGameMvpArtEnabled { get; set; } = DefaultPostGameMvpArtEnabled;

    public bool DamageVignetteEnabled { get; set; } = true;

    public int DamageVignetteIntensityPercent { get; set; } = DefaultDamageVignetteIntensityPercent;

    public LowHealthColorMode LowHealthColorMode { get; set; } = LowHealthColorMode.Red;

    public bool ShowUberOutlinesEnabled { get; set; } = true;

    public bool ProjectileTeamTintEnabled { get; set; } = true;

    public bool AudioMuted { get; set; }

    public int MasterVolumePercent { get; set; } = 100;

    public int MenuMusicVolumePercent { get; set; } = 70;

    public int IngameMusicVolumePercent { get; set; } = 70;

    public bool DynamicMusicEnabled { get; set; } = true;

    public int CombatMusicVolumePercent { get; set; } = DefaultCombatMusicVolumePercent;

    public int SoundEffectsVolumePercent { get; set; } = 70;

    public bool ShowPersistentSelfNameEnabled { get; set; }

    public bool PositionSmoothingEnabled { get; set; } = true;

    public float SmoothCameraMultiplier { get; set; } = 0.35f;

    public bool SpriteDropShadowEnabled { get; set; }

    public bool PixelPerfectWeaponRotation { get; set; } = true;

    public bool UseLocalWeaponRotation { get; set; } = false;

    public bool DisableLegacyGameplaySpriteFallback { get; set; }

    public int PlayerCardSizeMode { get; set; }

    public ControllerInputMode ControllerInputMode { get; set; } = ControllerInputMode.Auto;

    public ControllerReticleMode ControllerReticleMode { get; set; } = ControllerReticleMode.Cursor;

    public bool ControllerAimAssistEnabled { get; set; } = true;

    public bool ControllerFlickToChangeDirections { get; set; } = DefaultControllerFlickToChangeDirections;

    public float ControllerAimAssistStrength { get; set; } = DefaultControllerAimAssistStrength;

    public float ControllerAimDeadzone { get; set; } = DefaultControllerAimDeadzone;

    public float ControllerScopedPrecisionSpeed { get; set; } = DefaultControllerScopedPrecisionSpeed;

    public float ControllerAimDistanceTier1 { get; set; } = DefaultControllerAimDistanceTier1;

    public float ControllerAimDistanceTier2 { get; set; } = DefaultControllerAimDistanceTier2;

    public float ControllerAimDistanceTier3 { get; set; } = DefaultControllerAimDistanceTier3;

    public ControllerButtonBinding ControllerJumpButton { get; set; } = DefaultControllerJumpButton;

    public ControllerButtonBinding ControllerPrimaryFireButton { get; set; } = DefaultControllerPrimaryFireButton;

    public ControllerButtonBinding ControllerSecondaryFireButton { get; set; } = DefaultControllerSecondaryFireButton;

    public ControllerButtonBinding ControllerUseAbilityButton { get; set; } = DefaultControllerUseAbilityButton;

    public ControllerButtonBinding ControllerInteractButton { get; set; } = DefaultControllerInteractButton;

    public ControllerButtonBinding ControllerSwapWeaponButton { get; set; } = DefaultControllerSwapWeaponButton;

    public ControllerButtonBinding ControllerScoreboardButton { get; set; } = DefaultControllerScoreboardButton;

    public ControllerButtonBinding ControllerPauseButton { get; set; } = DefaultControllerPauseButton;

    public ControllerButtonBinding ControllerAimDistanceButton { get; set; } = DefaultControllerAimDistanceButton;

    public ControllerButtonBinding ControllerChangeTeamButton { get; set; } = DefaultControllerChangeTeamButton;

    public ControllerButtonBinding ControllerChangeClassButton { get; set; } = DefaultControllerChangeClassButton;

    public string RecentConnectionHost { get; set; } = "127.0.0.1";

    public int RecentConnectionPort { get; set; } = 8190;

    public OpenGarrisonHostSettings HostSettings { get; set; } = new();

    public string LobbyHost { get; set; } = DefaultLobbyHost;

    public int LobbyPort { get; set; } = DefaultLobbyPort;

    public string DiscordApplicationId { get; set; } = string.Empty;

    public int MaxPlayableClients { get; set; } = SimulationWorld.MaxPlayableNetworkPlayers;

    public int MaxTotalClients { get; set; } = SimulationWorld.MaxPlayableNetworkPlayers;

    public int MaxSpectatorClients { get; set; } = SimulationWorld.MaxPlayableNetworkPlayers;

    public bool PersistentGameplayOwnershipEnabled { get; set; }

    public PersistentGameplayOwnershipIdentityMode PersistentGameplayOwnershipIdentityMode { get; set; } = PersistentGameplayOwnershipIdentityMode.Disabled;

    public string PersistentGameplayOwnershipFile { get; set; } = "gameplay-ownership.json";

    public bool SnapshotCompressionEnabled { get; set; } = true;

    public string SnapshotBudgetMode { get; set; } = "GameplayCriticalUntrimmed";

    public static OpenGarrisonPreferencesDocument Load(string? path = null)
    {
        var resolvedPath = path ?? RuntimePaths.GetConfigPath(DefaultFileName);
        var ini = IniConfigurationFile.Load(resolvedPath);
        var legacySelectedMap = ini.GetString(ServerSection, "SelectedMap", string.Empty);

        return new OpenGarrisonPreferencesDocument
        {
            PlayerName = ini.GetString(SettingsSection, "PlayerName", "Player"),
            Rewards = ini.GetString(SettingsSection, "Rewards", string.Empty),
            DisplayMode = ReadDisplayMode(ini),
            VSync = ini.GetBool(SettingsSection, "Monitor Sync", false),
            FrameRateLimit = ini.GetInt(SettingsSection, "Frame Rate Limit", 0),
            IngameResolution = ReadIngameResolution(ini),
            WindowSize = NormalizeWindowSize((WindowSizeKind)ini.GetInt(SettingsSection, "Window Size", (int)DefaultWindowSize)),
            DisplayScaleMode = NormalizeDisplayScaleMode((DisplayScaleModeKind)ini.GetInt(SettingsSection, "Display Scale", (int)DefaultDisplayScaleMode)),
            MusicMode = LoadMusicMode(ini),
            BotMode = ParseBotMode(ini.GetString(SettingsSection, "Bot Mode", OfflineBotControllerMode.BotBrain.ToString())),
            KillCamEnabled = ini.GetBool(SettingsSection, "Kill Cam", true),
            ParticleMode = ini.GetInt(SettingsSection, "Particles", 0),
            FlameRenderMode = ini.GetInt(SettingsSection, "Flame Render Mode", 0),
            MenuBackgroundMode = (MenuBackgroundMode)ini.GetInt(SettingsSection, "Menu Background Mode", (int)MenuBackgroundMode.DefaultMaps),
            GibLevel = ini.GetInt(SettingsSection, "Gib Level", 3),
            CorpseDurationMode = ini.GetInt(SettingsSection, "Corpse Duration", 0),
            HealerRadarEnabled = ini.GetBool(SettingsSection, "Healer Radar", true),
            ShowHealerEnabled = ini.GetBool(SettingsSection, "Show Healer", true),
            ShowHealingEnabled = ini.GetBool(SettingsSection, "Show Healing", true),
            ShowHealthBarEnabled = ini.GetBool(SettingsSection, "Show Healthbar", false),
            ShowShieldBarEnabled = ini.GetBool(SettingsSection, "Show Shield Bar", true),
            HudShowOnlyActiveWeapon = ini.GetBool(SettingsSection, "HUD Show Only Active Weapon", false),
            OverheadChatEnabled = ini.GetBool(SettingsSection, "Overhead Chat", DefaultOverheadChatEnabled),
            BubbleWheelBehavior = ParseBubbleWheelBehavior(ini.GetString(SettingsSection, "Bubble Wheel Behavior", DefaultBubbleWheelBehavior.ToString())),
            PortraitRumbleEnabled = ini.GetBool(SettingsSection, "Portrait Rumble", true),
            PostGameMvpArtEnabled = ini.GetBool(SettingsSection, "MVP Art", DefaultPostGameMvpArtEnabled),
            DamageVignetteEnabled = ini.GetBool(SettingsSection, "Damage Vignette", true),
            DamageVignetteIntensityPercent = NormalizeDamageVignetteIntensityPercent(ini.GetInt(SettingsSection, "Damage Vignette Intensity", DefaultDamageVignetteIntensityPercent)),
            LowHealthColorMode = ParseLowHealthColorMode(ini.GetString(SettingsSection, "Low Health Color", LowHealthColorMode.Red.ToString())),
            ShowUberOutlinesEnabled = ini.GetBool(SettingsSection, "Show Uber Outlines", true),
            ProjectileTeamTintEnabled = ini.GetBool(SettingsSection, "Projectile Team Tint", true),
            AudioMuted = ini.GetBool(SettingsSection, "Audio Muted", false),
            MasterVolumePercent = Math.Clamp(ini.GetInt(SettingsSection, "Master Volume", 100), 0, 100),
            MenuMusicVolumePercent = Math.Clamp(ini.GetInt(SettingsSection, "Menu Music Volume", 100), 0, 100),
            IngameMusicVolumePercent = Math.Clamp(ini.GetInt(SettingsSection, "Ingame Music Volume", 100), 0, 100),
            DynamicMusicEnabled = ini.GetBool(SettingsSection, "Dynamic Music", true),
            CombatMusicVolumePercent = Math.Clamp(ini.GetInt(SettingsSection, "Combat Music Volume", DefaultCombatMusicVolumePercent), 0, MaxCombatMusicVolumePercent),
            SoundEffectsVolumePercent = Math.Clamp(ini.GetInt(SettingsSection, "Sound Effects Volume", 100), 0, 100),
            ShowPersistentSelfNameEnabled = ini.GetBool(SettingsSection, "Show Self Name", false),
            PositionSmoothingEnabled = ini.GetBool(SettingsSection, "Position Smoothing", true),
            SmoothCameraMultiplier = Math.Clamp(ini.GetFloat(SettingsSection, "Smooth Camera Multiplier", 0.35f), 0f, 1f),
            SpriteDropShadowEnabled = ini.GetBool(SettingsSection, "Sprite Drop Shadow", true),
            PixelPerfectWeaponRotation = ini.GetBool(SettingsSection, "Pixel Perfect Weapon Rotation", true),
            UseLocalWeaponRotation = ini.GetBool(SettingsSection, "Use Local Weapon Rotation", false),
            DisableLegacyGameplaySpriteFallback = ini.GetBool(SettingsSection, "Disable Legacy Gameplay Sprite Fallback", false),
            PlayerCardSizeMode = Math.Clamp(ini.GetInt(SettingsSection, "Playercard Size", 0), 0, 2),
            ControllerInputMode = ParseControllerInputMode(ini.GetString(SettingsSection, "Controller Input Mode", ControllerInputMode.Auto.ToString())),
            ControllerReticleMode = ParseControllerReticleMode(ini.GetString(SettingsSection, "Controller Reticle Mode", ControllerReticleMode.Cursor.ToString())),
            ControllerAimAssistEnabled = ini.GetBool(SettingsSection, "Controller Aim Assist", true),
            ControllerFlickToChangeDirections = ini.GetBool(SettingsSection, "Controller Flick To Change Directions", DefaultControllerFlickToChangeDirections),
            ControllerAimAssistStrength = NormalizeControllerAimAssistStrength(ini.GetFloat(SettingsSection, "Controller Aim Assist Strength", DefaultControllerAimAssistStrength)),
            ControllerAimDeadzone = NormalizeControllerAimDeadzone(ini.GetFloat(SettingsSection, "Controller Aim Deadzone", DefaultControllerAimDeadzone)),
            ControllerScopedPrecisionSpeed = NormalizeControllerScopedPrecisionSpeed(ini.GetFloat(SettingsSection, "Controller Scoped Precision Speed", DefaultControllerScopedPrecisionSpeed)),
            ControllerAimDistanceTier1 = NormalizeControllerAimDistance(ini.GetFloat(SettingsSection, "Controller Aim Distance 1", DefaultControllerAimDistanceTier1), DefaultControllerAimDistanceTier1),
            ControllerAimDistanceTier2 = NormalizeControllerAimDistance(ini.GetFloat(SettingsSection, "Controller Aim Distance 2", DefaultControllerAimDistanceTier2), DefaultControllerAimDistanceTier2),
            ControllerAimDistanceTier3 = NormalizeControllerAimDistance(ini.GetFloat(SettingsSection, "Controller Aim Distance 3", DefaultControllerAimDistanceTier3), DefaultControllerAimDistanceTier3),
            ControllerJumpButton = ReadControllerButtonBinding(ini, "Controller Jump Button", DefaultControllerJumpButton),
            ControllerPrimaryFireButton = ReadControllerButtonBinding(ini, "Controller Primary Fire Button", DefaultControllerPrimaryFireButton),
            ControllerSecondaryFireButton = ReadControllerButtonBinding(ini, "Controller Secondary Fire Button", DefaultControllerSecondaryFireButton),
            ControllerUseAbilityButton = ReadControllerButtonBinding(ini, "Controller Utility Ability Button", DefaultControllerUseAbilityButton),
            ControllerInteractButton = ReadControllerButtonBinding(ini, "Controller Interact Button", DefaultControllerInteractButton),
            ControllerSwapWeaponButton = ReadControllerButtonBinding(ini, "Controller Swap Weapon Button", DefaultControllerSwapWeaponButton),
            ControllerScoreboardButton = ReadControllerButtonBinding(ini, "Controller Scoreboard Button", DefaultControllerScoreboardButton),
            ControllerPauseButton = ReadControllerButtonBinding(ini, "Controller Pause Button", DefaultControllerPauseButton),
            ControllerAimDistanceButton = ReadControllerButtonBinding(ini, "Controller Aim Distance Button", DefaultControllerAimDistanceButton),
            ControllerChangeTeamButton = ReadControllerButtonBinding(ini, "Controller Change Team Button", DefaultControllerChangeTeamButton),
            ControllerChangeClassButton = ReadControllerButtonBinding(ini, "Controller Change Class Button", DefaultControllerChangeClassButton),
            RecentConnectionHost = ini.GetString(ConnectionSection, "Host", "127.0.0.1"),
            RecentConnectionPort = ini.GetInt(ConnectionSection, "Port", 8190),
            HostSettings = OpenGarrisonHostSettings.LoadFrom(ini, legacySelectedMap),
            LobbyHost = MigrateLegacyApiHost(ini.GetString(ServerAdvancedSection, "LobbyHost", DefaultLobbyHost)),
            LobbyPort = ini.GetInt(ServerAdvancedSection, "LobbyPort", DefaultLobbyPort),
            DiscordApplicationId = ini.GetString(ServerAdvancedSection, "DiscordApplicationId", string.Empty),
            MaxPlayableClients = ini.GetInt(ServerAdvancedSection, "MaxPlayableClients", SimulationWorld.MaxPlayableNetworkPlayers),
            MaxTotalClients = ini.GetInt(ServerAdvancedSection, "MaxTotalClients", SimulationWorld.MaxPlayableNetworkPlayers),
            MaxSpectatorClients = ini.GetInt(ServerAdvancedSection, "MaxSpectatorClients", SimulationWorld.MaxPlayableNetworkPlayers),
            PersistentGameplayOwnershipEnabled = ini.GetBool(ServerAdvancedSection, "PersistentGameplayOwnershipEnabled", false),
            PersistentGameplayOwnershipIdentityMode = ParsePersistentGameplayOwnershipIdentityMode(
                ini.GetString(ServerAdvancedSection, "PersistentGameplayOwnershipIdentityMode", PersistentGameplayOwnershipIdentityMode.Disabled.ToString())),
            PersistentGameplayOwnershipFile = ini.GetString(ServerAdvancedSection, "PersistentGameplayOwnershipFile", "gameplay-ownership.json"),
            SnapshotCompressionEnabled = ini.GetBool(ServerAdvancedSection, "SnapshotCompressionEnabled", true),
            SnapshotBudgetMode = ini.GetString(ServerAdvancedSection, "SnapshotBudgetMode", "GameplayCriticalUntrimmed"),
        };
    }

    public void Save(string? path = null)
    {
        var resolvedPath = path ?? RuntimePaths.GetConfigPath(DefaultFileName);
        var ini = new IniConfigurationFile();

        ini.SetString(SettingsSection, "PlayerName", PlayerName);
        ini.SetString(SettingsSection, "Rewards", Rewards);
        var displayMode = NormalizeDisplayMode(DisplayMode);
        ini.SetInt(SettingsSection, "Display Mode", (int)displayMode);
        ini.SetBool(SettingsSection, "Fullscreen", displayMode == DisplayModeKind.Fullscreen);
        ini.SetBool(SettingsSection, "UseLobby", HostSettings.LobbyAnnounceEnabled);
        ini.SetInt(SettingsSection, "HostingPort", HostSettings.Port);
        ini.SetInt(SettingsSection, "Resolution", (int)NormalizeIngameResolution(IngameResolution));
        ini.SetInt(SettingsSection, "Window Size", (int)NormalizeWindowSize(WindowSize));
        ini.SetInt(SettingsSection, "Display Scale", (int)NormalizeDisplayScaleMode(DisplayScaleMode));
        ini.SetInt(SettingsSection, "Music", (int)NormalizeMusicMode(MusicMode));
        ini.SetString(SettingsSection, "Bot Mode", NormalizeBotMode(BotMode).ToString());
        ini.SetInt(SettingsSection, "PlayerLimit", HostSettings.Slots);
        ini.SetInt(SettingsSection, "Particles", ParticleMode);
        ini.SetInt(SettingsSection, "Flame Render Mode", FlameRenderMode);
        ini.SetInt(SettingsSection, "Menu Background Mode", (int)MenuBackgroundMode);
        ini.SetInt(SettingsSection, "Gib Level", GibLevel);
        ini.SetInt(SettingsSection, "Corpse Duration", CorpseDurationMode);
        ini.SetBool(SettingsSection, "Kill Cam", KillCamEnabled);
        ini.SetBool(SettingsSection, "Monitor Sync", VSync);
        ini.SetInt(SettingsSection, "Frame Rate Limit", FrameRateLimit);
        ini.SetBool(SettingsSection, "Healer Radar", HealerRadarEnabled);
        ini.SetBool(SettingsSection, "Show Healer", ShowHealerEnabled);
        ini.SetBool(SettingsSection, "Show Healing", ShowHealingEnabled);
        ini.SetBool(SettingsSection, "Show Healthbar", ShowHealthBarEnabled);
        ini.SetBool(SettingsSection, "Show Shield Bar", ShowShieldBarEnabled);
        ini.SetBool(SettingsSection, "HUD Show Only Active Weapon", HudShowOnlyActiveWeapon);
        ini.SetBool(SettingsSection, "Overhead Chat", OverheadChatEnabled);
        ini.SetString(SettingsSection, "Bubble Wheel Behavior", NormalizeBubbleWheelBehavior(BubbleWheelBehavior).ToString());
        ini.SetBool(SettingsSection, "Portrait Rumble", PortraitRumbleEnabled);
        ini.SetBool(SettingsSection, "MVP Art", PostGameMvpArtEnabled);
        ini.SetBool(SettingsSection, "Damage Vignette", DamageVignetteEnabled);
        ini.SetInt(SettingsSection, "Damage Vignette Intensity", NormalizeDamageVignetteIntensityPercent(DamageVignetteIntensityPercent));
        ini.SetString(SettingsSection, "Low Health Color", NormalizeLowHealthColorMode(LowHealthColorMode).ToString());
        ini.SetBool(SettingsSection, "Show Uber Outlines", ShowUberOutlinesEnabled);
        ini.SetBool(SettingsSection, "Projectile Team Tint", ProjectileTeamTintEnabled);
        ini.SetBool(SettingsSection, "Audio Muted", AudioMuted);
        ini.SetInt(SettingsSection, "Master Volume", Math.Clamp(MasterVolumePercent, 0, 100));
        ini.SetInt(SettingsSection, "Menu Music Volume", MenuMusicVolumePercent);
        ini.SetInt(SettingsSection, "Ingame Music Volume", IngameMusicVolumePercent);
        ini.SetBool(SettingsSection, "Dynamic Music", DynamicMusicEnabled);
        ini.SetInt(SettingsSection, "Combat Music Volume", Math.Clamp(CombatMusicVolumePercent, 0, MaxCombatMusicVolumePercent));
        ini.SetInt(SettingsSection, "Sound Effects Volume", SoundEffectsVolumePercent);
        ini.SetBool(SettingsSection, "Show Self Name", ShowPersistentSelfNameEnabled);
        ini.SetBool(SettingsSection, "Position Smoothing", PositionSmoothingEnabled);
        ini.SetFloat(SettingsSection, "Smooth Camera Multiplier", Math.Clamp(SmoothCameraMultiplier, 0f, 1f));
        ini.SetBool(SettingsSection, "Sprite Drop Shadow", SpriteDropShadowEnabled);
        ini.SetBool(SettingsSection, "Pixel Perfect Weapon Rotation", PixelPerfectWeaponRotation);
        ini.SetBool(SettingsSection, "Use Local Weapon Rotation", UseLocalWeaponRotation);
        ini.SetBool(SettingsSection, "Disable Legacy Gameplay Sprite Fallback", DisableLegacyGameplaySpriteFallback);
        ini.SetInt(SettingsSection, "Playercard Size", Math.Clamp(PlayerCardSizeMode, 0, 2));
        ini.SetString(SettingsSection, "Controller Input Mode", NormalizeControllerInputMode(ControllerInputMode).ToString());
        ini.SetString(SettingsSection, "Controller Reticle Mode", NormalizeControllerReticleMode(ControllerReticleMode).ToString());
        ini.SetBool(SettingsSection, "Controller Aim Assist", ControllerAimAssistEnabled);
        ini.SetBool(SettingsSection, "Controller Flick To Change Directions", ControllerFlickToChangeDirections);
        ini.SetFloat(SettingsSection, "Controller Aim Assist Strength", NormalizeControllerAimAssistStrength(ControllerAimAssistStrength));
        ini.SetFloat(SettingsSection, "Controller Aim Deadzone", NormalizeControllerAimDeadzone(ControllerAimDeadzone));
        ini.SetFloat(SettingsSection, "Controller Scoped Precision Speed", NormalizeControllerScopedPrecisionSpeed(ControllerScopedPrecisionSpeed));
        ini.SetFloat(SettingsSection, "Controller Aim Distance 1", NormalizeControllerAimDistance(ControllerAimDistanceTier1, DefaultControllerAimDistanceTier1));
        ini.SetFloat(SettingsSection, "Controller Aim Distance 2", NormalizeControllerAimDistance(ControllerAimDistanceTier2, DefaultControllerAimDistanceTier2));
        ini.SetFloat(SettingsSection, "Controller Aim Distance 3", NormalizeControllerAimDistance(ControllerAimDistanceTier3, DefaultControllerAimDistanceTier3));
        ini.SetString(SettingsSection, "Controller Jump Button", NormalizeControllerButtonBinding(ControllerJumpButton).ToString());
        ini.SetString(SettingsSection, "Controller Primary Fire Button", NormalizeControllerButtonBinding(ControllerPrimaryFireButton).ToString());
        ini.SetString(SettingsSection, "Controller Secondary Fire Button", NormalizeControllerButtonBinding(ControllerSecondaryFireButton).ToString());
        ini.SetString(SettingsSection, "Controller Utility Ability Button", NormalizeControllerButtonBinding(ControllerUseAbilityButton).ToString());
        ini.SetString(SettingsSection, "Controller Interact Button", NormalizeControllerButtonBinding(ControllerInteractButton).ToString());
        ini.SetString(SettingsSection, "Controller Swap Weapon Button", NormalizeControllerButtonBinding(ControllerSwapWeaponButton).ToString());
        ini.SetString(SettingsSection, "Controller Scoreboard Button", NormalizeControllerButtonBinding(ControllerScoreboardButton).ToString());
        ini.SetString(SettingsSection, "Controller Pause Button", NormalizeControllerButtonBinding(ControllerPauseButton).ToString());
        ini.SetString(SettingsSection, "Controller Aim Distance Button", NormalizeControllerButtonBinding(ControllerAimDistanceButton).ToString());
        ini.SetString(SettingsSection, "Controller Change Team Button", NormalizeControllerButtonBinding(ControllerChangeTeamButton).ToString());
        ini.SetString(SettingsSection, "Controller Change Class Button", NormalizeControllerButtonBinding(ControllerChangeClassButton).ToString());

        ini.SetString(ServerSection, "MapRotation", HostSettings.MapRotationFile);
        ini.SetBool(ServerSection, "MapRotationShuffle", HostSettings.MapRotationShuffleEnabled);
        ini.SetString(ServerSection, "MapRotationAdvanceMode", OpenGarrisonHostSettings.NormalizeMapRotationAdvanceMode(HostSettings.MapRotationAdvanceMode).ToString());
        ini.SetInt(ServerSection, "MapRotationRounds", OpenGarrisonHostSettings.NormalizeMapRotationRounds(HostSettings.MapRotationRounds));
        ini.SetInt(ServerSection, "MapRotationMinutes", OpenGarrisonHostSettings.NormalizeMapRotationMinutes(HostSettings.MapRotationMinutes));
        ini.SetBool(ServerSection, "UsePlaylistFile", HostSettings.UsePlaylistFile);
        ini.SetBool(ServerSection, "Dedicated", HostSettings.DedicatedModeEnabled);
        ini.SetString(ServerSection, "ServerName", HostSettings.ServerName);
        ini.SetInt(ServerSection, "CapLimit", HostSettings.CapLimit);
        ini.SetBool(ServerSection, "AutoBalance", HostSettings.AutoBalanceEnabled);
        ini.SetInt(ServerSection, "Respawn Time", HostSettings.RespawnSeconds);
        ini.SetInt(ServerSection, "Time Limit", HostSettings.TimeLimitMinutes);
        ini.SetString(ServerSection, "Password", HostSettings.Password);
        ini.SetBool(ServerSection, "SpecialAbilities", HostSettings.SecondaryAbilitiesEnabled);
        ini.SetBool(ServerSection, "SecondaryAbilities", HostSettings.SecondaryAbilitiesEnabled);

        OpenGarrisonStockMapCatalog.SaveTo(ini, HostSettings.StockMapRotation);

        ini.SetString(ConnectionSection, "Host", RecentConnectionHost);
        ini.SetInt(ConnectionSection, "Port", RecentConnectionPort);

        ini.SetString(ServerAdvancedSection, "LobbyHost", LobbyHost);
        ini.SetInt(ServerAdvancedSection, "LobbyPort", LobbyPort > 0 ? LobbyPort : DefaultLobbyPort);
        ini.SetString(ServerAdvancedSection, "DiscordApplicationId", DiscordApplicationId);
        ini.SetInt(ServerAdvancedSection, "TickRate", SimulationConfig.NormalizeTicksPerSecond(HostSettings.TickRate));
        ini.SetString(ServerAdvancedSection, "RconPassword", HostSettings.RconPassword);
        ini.SetBool(ServerAdvancedSection, "BotAutofillEnabled", HostSettings.BotAutofillEnabled);
        ini.SetInt(ServerAdvancedSection, "BotAutofillMinPlayers", HostSettings.BotAutofillMinPlayers);
        ini.SetInt(ServerAdvancedSection, "BotAutofillPerTeam", HostSettings.BotAutofillPerTeam);
        ini.SetBool(ServerAdvancedSection, "CompetitiveReadyUpEnabled", HostSettings.CompetitiveReadyUpEnabled);
        ini.SetInt(ServerAdvancedSection, "CompetitiveSetupSeconds", HostSettings.CompetitiveSetupSeconds);
        ini.SetBool(ServerAdvancedSection, "RandomSpreadEnabled", HostSettings.RandomSpreadEnabled);
        ini.SetBool(ServerAdvancedSection, "SniperAimIndicatorEnabled", HostSettings.SniperAimIndicatorEnabled);
        ini.SetBool(ServerAdvancedSection, "LocalPredictionEnabled", HostSettings.LocalPredictionEnabled);
        ini.SetBool(ServerAdvancedSection, "RoundEndFriendlyFireEnabled", HostSettings.RoundEndFriendlyFireEnabled);
        ini.SetBool(ServerAdvancedSection, "SwitchTeamsAfterRoundEnd", HostSettings.SwitchTeamsAfterRoundEnd);
        ini.SetInt(ServerAdvancedSection, "TeamShuffleAfterWins", OpenGarrisonHostSettings.NormalizeTeamShuffleAfterWins(HostSettings.TeamShuffleAfterWins));
        ini.SetFloat(ServerAdvancedSection, "PlayerScale", HostSettings.PlayerScale);
        ini.SetFloat(ServerAdvancedSection, "MapScale", HostSettings.MapScale);
        ini.SetFloat(ServerAdvancedSection, "MovementSpeedScale", HostSettings.MovementSpeedScale);
        ini.SetFloat(ServerAdvancedSection, "ProjectileSpeedScale", HostSettings.ProjectileSpeedScale);
        ini.SetFloat(ServerAdvancedSection, "DamageScale", HostSettings.DamageScale);
        ini.SetFloat(ServerAdvancedSection, "GravityScale", HostSettings.GravityScale);
        ini.SetFloat(ServerAdvancedSection, "HorizontalSpeedClampPerTick", HostSettings.HorizontalSpeedClampPerTick);
        ini.SetFloat(ServerAdvancedSection, "VerticalSpeedClampPerTick", HostSettings.VerticalSpeedClampPerTick);
        ini.SetFloat(ServerAdvancedSection, "CaptureSpeedMultiplierPerPlayer", HostSettings.CaptureSpeedMultiplierPerPlayer);
        ini.SetBool(ServerAdvancedSection, "VipAllowDuplicateClasses", HostSettings.VipAllowDuplicateClasses);
        ini.SetInt(ServerAdvancedSection, "ClassLimitScout", HostSettings.ClassLimitScout);
        ini.SetInt(ServerAdvancedSection, "ClassLimitEngineer", HostSettings.ClassLimitEngineer);
        ini.SetInt(ServerAdvancedSection, "ClassLimitPyro", HostSettings.ClassLimitPyro);
        ini.SetInt(ServerAdvancedSection, "ClassLimitSoldier", HostSettings.ClassLimitSoldier);
        ini.SetInt(ServerAdvancedSection, "ClassLimitDemoman", HostSettings.ClassLimitDemoman);
        ini.SetInt(ServerAdvancedSection, "ClassLimitHeavy", HostSettings.ClassLimitHeavy);
        ini.SetInt(ServerAdvancedSection, "ClassLimitSniper", HostSettings.ClassLimitSniper);
        ini.SetInt(ServerAdvancedSection, "ClassLimitMedic", HostSettings.ClassLimitMedic);
        ini.SetInt(ServerAdvancedSection, "ClassLimitSpy", HostSettings.ClassLimitSpy);
        ini.SetInt(ServerAdvancedSection, "ClassLimitCivilian", HostSettings.ClassLimitCivilian);
        ini.SetInt(ServerAdvancedSection, "MaxPlayableClients", MaxPlayableClients);
        ini.SetInt(ServerAdvancedSection, "MaxTotalClients", MaxTotalClients);
        ini.SetInt(ServerAdvancedSection, "MaxSpectatorClients", MaxSpectatorClients);
        ini.SetBool(ServerAdvancedSection, "PersistentGameplayOwnershipEnabled", PersistentGameplayOwnershipEnabled);
        ini.SetString(ServerAdvancedSection, "PersistentGameplayOwnershipIdentityMode", PersistentGameplayOwnershipIdentityMode.ToString());
        ini.SetString(ServerAdvancedSection, "PersistentGameplayOwnershipFile", PersistentGameplayOwnershipFile);
        ini.SetBool(ServerAdvancedSection, "SnapshotCompressionEnabled", SnapshotCompressionEnabled);

        ini.Save(resolvedPath);
    }

    private static MusicMode LoadMusicMode(IniConfigurationFile ini)
    {
        if (ini.ContainsKey(SettingsSection, "Music"))
        {
            return NormalizeMusicMode((MusicMode)ini.GetInt(SettingsSection, "Music", (int)MusicMode.MenuAndInGame));
        }

        return ini.GetBool(SettingsSection, "IngameMusic", true)
            ? MusicMode.MenuAndInGame
            : MusicMode.MenuOnly;
    }

    private static MusicMode NormalizeMusicMode(MusicMode musicMode)
    {
        return musicMode switch
        {
            MusicMode.MenuOnly => MusicMode.MenuOnly,
            MusicMode.MenuAndInGame => MusicMode.MenuAndInGame,
            MusicMode.InGameOnly => MusicMode.InGameOnly,
            MusicMode.None => MusicMode.None,
            _ => MusicMode.MenuAndInGame,
        };
    }

    private static OfflineBotControllerMode ParseBotMode(string value)
    {
        return BotBrain.PracticeBotControllerFactory.ParseMode(value, OfflineBotControllerMode.BotBrain);
    }

    private static OfflineBotControllerMode NormalizeBotMode(OfflineBotControllerMode mode)
    {
        return BotBrain.PracticeBotControllerFactory.NormalizeMode(mode);
    }

    private static ControllerInputMode ParseControllerInputMode(string value)
    {
        return Enum.TryParse<ControllerInputMode>(value, ignoreCase: true, out var mode)
            ? NormalizeControllerInputMode(mode)
            : ControllerInputMode.Auto;
    }

    public static ControllerInputMode NormalizeControllerInputMode(ControllerInputMode mode)
    {
        return mode switch
        {
            ControllerInputMode.Off => ControllerInputMode.Off,
            ControllerInputMode.Auto => ControllerInputMode.Auto,
            ControllerInputMode.On => ControllerInputMode.On,
            _ => ControllerInputMode.Auto,
        };
    }

    private static ControllerReticleMode ParseControllerReticleMode(string value)
    {
        return Enum.TryParse<ControllerReticleMode>(value, ignoreCase: true, out var mode)
            ? NormalizeControllerReticleMode(mode)
            : ControllerReticleMode.Cursor;
    }

    public static ControllerReticleMode NormalizeControllerReticleMode(ControllerReticleMode mode)
    {
        return mode switch
        {
            ControllerReticleMode.Cursor => ControllerReticleMode.Cursor,
            ControllerReticleMode.AimLine => ControllerReticleMode.AimLine,
            _ => ControllerReticleMode.Cursor,
        };
    }

    private static BubbleWheelBehavior ParseBubbleWheelBehavior(string value)
    {
        var normalizedValue = value
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal);
        return Enum.TryParse<BubbleWheelBehavior>(normalizedValue, ignoreCase: true, out var behavior)
            ? NormalizeBubbleWheelBehavior(behavior)
            : DefaultBubbleWheelBehavior;
    }

    public static BubbleWheelBehavior NormalizeBubbleWheelBehavior(BubbleWheelBehavior behavior)
    {
        return behavior switch
        {
            BubbleWheelBehavior.HoldAndHover => BubbleWheelBehavior.HoldAndHover,
            BubbleWheelBehavior.PressAndClick => BubbleWheelBehavior.PressAndClick,
            _ => DefaultBubbleWheelBehavior,
        };
    }

    private static ControllerButtonBinding ReadControllerButtonBinding(
        IniConfigurationFile ini,
        string key,
        ControllerButtonBinding fallback)
    {
        var value = ini.GetString(SettingsSection, key, fallback.ToString());
        return ParseControllerButtonBinding(value, fallback);
    }

    public static ControllerButtonBinding ParseControllerButtonBinding(string value, ControllerButtonBinding fallback)
    {
        return Enum.TryParse<ControllerButtonBinding>(value, ignoreCase: true, out var binding)
            ? NormalizeControllerButtonBinding(binding)
            : NormalizeControllerButtonBinding(fallback);
    }

    public static ControllerButtonBinding NormalizeControllerButtonBinding(ControllerButtonBinding binding)
    {
        return binding switch
        {
            ControllerButtonBinding.None => ControllerButtonBinding.None,
            ControllerButtonBinding.A => ControllerButtonBinding.A,
            ControllerButtonBinding.B => ControllerButtonBinding.B,
            ControllerButtonBinding.X => ControllerButtonBinding.X,
            ControllerButtonBinding.Y => ControllerButtonBinding.Y,
            ControllerButtonBinding.LeftShoulder => ControllerButtonBinding.LeftShoulder,
            ControllerButtonBinding.RightShoulder => ControllerButtonBinding.RightShoulder,
            ControllerButtonBinding.LeftTrigger => ControllerButtonBinding.LeftTrigger,
            ControllerButtonBinding.RightTrigger => ControllerButtonBinding.RightTrigger,
            ControllerButtonBinding.Back => ControllerButtonBinding.Back,
            ControllerButtonBinding.Start => ControllerButtonBinding.Start,
            ControllerButtonBinding.LeftStick => ControllerButtonBinding.LeftStick,
            ControllerButtonBinding.RightStick => ControllerButtonBinding.RightStick,
            ControllerButtonBinding.DPadUp => ControllerButtonBinding.DPadUp,
            ControllerButtonBinding.DPadDown => ControllerButtonBinding.DPadDown,
            ControllerButtonBinding.DPadLeft => ControllerButtonBinding.DPadLeft,
            ControllerButtonBinding.DPadRight => ControllerButtonBinding.DPadRight,
            _ => ControllerButtonBinding.None,
        };
    }

    public static float NormalizeControllerAimAssistStrength(float strength)
    {
        return Math.Clamp(strength, 0f, 1f);
    }

    public static float NormalizeControllerAimDeadzone(float deadzone)
    {
        return Math.Clamp(deadzone, 0.01f, 0.95f);
    }

    public static float NormalizeControllerScopedPrecisionSpeed(float speed)
    {
        return Math.Clamp(speed, 10f, 1200f);
    }

    public static float NormalizeControllerAimDistance(float distance, float fallback)
    {
        return float.IsFinite(distance)
            ? Math.Clamp(distance, 16f, 960f)
            : fallback;
    }

    public static WindowSizeKind NormalizeWindowSize(WindowSizeKind windowSize)
    {
        return windowSize switch
        {
            WindowSizeKind.Scale100 => WindowSizeKind.Scale100,
            WindowSizeKind.Scale150 => WindowSizeKind.Scale150,
            WindowSizeKind.Scale200 => WindowSizeKind.Scale200,
            _ => DefaultWindowSize,
        };
    }

    public static DisplayModeKind NormalizeDisplayMode(DisplayModeKind displayMode)
    {
        return displayMode switch
        {
            DisplayModeKind.Borderless => DisplayModeKind.Borderless,
            DisplayModeKind.BorderlessWindow => DisplayModeKind.BorderlessWindow,
            DisplayModeKind.Fullscreen => DisplayModeKind.Fullscreen,
            _ => DefaultDisplayMode,
        };
    }

    public static DisplayScaleModeKind NormalizeDisplayScaleMode(DisplayScaleModeKind displayScaleMode)
    {
        return displayScaleMode switch
        {
            DisplayScaleModeKind.PixelPerfect => DisplayScaleModeKind.PixelPerfect,
            _ => DefaultDisplayScaleMode,
        };
    }

    private static DisplayModeKind ReadDisplayMode(IniConfigurationFile ini)
    {
        if (ini.ContainsKey(SettingsSection, "Display Mode"))
        {
            return NormalizeDisplayMode((DisplayModeKind)ini.GetInt(SettingsSection, "Display Mode", (int)DefaultDisplayMode));
        }

        return ini.GetBool(SettingsSection, "Fullscreen", false)
            ? DisplayModeKind.Fullscreen
            : DefaultDisplayMode;
    }

    private static IngameResolutionKind ReadIngameResolution(IniConfigurationFile ini)
    {
        var resolution = (IngameResolutionKind)ini.GetInt(SettingsSection, "Resolution", (int)DefaultIngameResolution);
        return ShouldUpgradeLegacyDefaultResolution(ini, resolution)
            ? DefaultIngameResolution
            : NormalizeIngameResolution(resolution);
    }

    private static bool ShouldUpgradeLegacyDefaultResolution(IniConfigurationFile ini, IngameResolutionKind resolution)
    {
        // Older generated preferences wrote 4:3 as the default before Window Size existed.
        return resolution == IngameResolutionKind.Aspect4x3
            && !ini.ContainsKey(SettingsSection, "Window Size");
    }

    private static IngameResolutionKind NormalizeIngameResolution(IngameResolutionKind ingameResolution)
    {
        return ingameResolution switch
        {
            IngameResolutionKind.Aspect5x4 => IngameResolutionKind.Aspect5x4,
            IngameResolutionKind.Aspect4x3 => IngameResolutionKind.Aspect4x3,
            IngameResolutionKind.Aspect16x9 => IngameResolutionKind.Aspect16x9,
            IngameResolutionKind.Aspect16x10 => IngameResolutionKind.Aspect16x9,
            IngameResolutionKind.Aspect2x1 => IngameResolutionKind.Aspect16x9,
            _ => DefaultIngameResolution,
        };
    }

    private static PersistentGameplayOwnershipIdentityMode ParsePersistentGameplayOwnershipIdentityMode(string value)
    {
        return Enum.TryParse<PersistentGameplayOwnershipIdentityMode>(value, ignoreCase: true, out var mode)
            ? mode
            : PersistentGameplayOwnershipIdentityMode.Disabled;
    }

    private static LowHealthColorMode ParseLowHealthColorMode(string value)
    {
        return Enum.TryParse<LowHealthColorMode>(value, ignoreCase: true, out var mode)
            ? NormalizeLowHealthColorMode(mode)
            : LowHealthColorMode.Red;
    }

    private static LowHealthColorMode NormalizeLowHealthColorMode(LowHealthColorMode mode)
    {
        return mode switch
        {
            LowHealthColorMode.None => LowHealthColorMode.None,
            LowHealthColorMode.Red => LowHealthColorMode.Red,
            _ => LowHealthColorMode.Red,
        };
    }

    private static int NormalizeDamageVignetteIntensityPercent(int percent)
    {
        return Math.Clamp(percent, 0, 100);
    }

}

public sealed class OpenGarrisonHostSettings
{
    public string ServerName { get; set; } = "My Server";

    public int Port { get; set; } = 8190;

    public int Slots { get; set; } = 10;

    public string Password { get; set; } = string.Empty;

    public int TimeLimitMinutes { get; set; } = 15;

    public int CapLimit { get; set; } = 5;

    public int RespawnSeconds { get; set; } = 5;

    public int TickRate { get; set; } = SimulationConfig.DefaultTicksPerSecond;

    public string RconPassword { get; set; } = string.Empty;

    public bool BotAutofillEnabled { get; set; }

    public int BotAutofillMinPlayers { get; set; }

    public int BotAutofillPerTeam { get; set; }

    public bool LobbyAnnounceEnabled { get; set; } = true;

    public bool AutoBalanceEnabled { get; set; } = true;

    public bool SecondaryAbilitiesEnabled { get; set; } = true;

    public bool RandomSpreadEnabled { get; set; } = true;

    public bool SniperAimIndicatorEnabled { get; set; } = true;

    public bool LocalPredictionEnabled { get; set; }

    public bool RoundEndFriendlyFireEnabled { get; set; }

    public bool SwitchTeamsAfterRoundEnd { get; set; }

    public int TeamShuffleAfterWins { get; set; }

    public bool CompetitiveReadyUpEnabled { get; set; }

    public int CompetitiveSetupSeconds { get; set; } = 10;

    public float PlayerScale { get; set; } = 1f;

    public float MapScale { get; set; } = 1f;

    public float MovementSpeedScale { get; set; } = 1f;

    public float ProjectileSpeedScale { get; set; } = 1f;

    public float DamageScale { get; set; } = 1f;

    public float GravityScale { get; set; } = 1f;

    public float HorizontalSpeedClampPerTick { get; set; } = LegacyMovementModel.MaxStepSpeedPerTick;

    public float VerticalSpeedClampPerTick { get; set; } = LegacyMovementModel.MaxStepSpeedPerTick;

    public float CaptureSpeedMultiplierPerPlayer { get; set; } = 2f;

    public bool VipAllowDuplicateClasses { get; set; }

    public int ClassLimitScout { get; set; }

    public int ClassLimitEngineer { get; set; }

    public int ClassLimitPyro { get; set; }

    public int ClassLimitSoldier { get; set; }

    public int ClassLimitDemoman { get; set; }

    public int ClassLimitHeavy { get; set; }

    public int ClassLimitSniper { get; set; }

    public int ClassLimitMedic { get; set; }

    public int ClassLimitSpy { get; set; }

    public int ClassLimitCivilian { get; set; }

    public bool DedicatedModeEnabled { get; set; }

    public string MapRotationFile { get; set; } = string.Empty;

    public bool MapRotationShuffleEnabled { get; set; }

    public MapRotationAdvanceMode MapRotationAdvanceMode { get; set; } = MapRotationAdvanceMode.RoundEnd;

    public int MapRotationRounds { get; set; } = 1;

    public int MapRotationMinutes { get; set; } = 15;

    public bool UsePlaylistFile { get; set; }

    public List<OpenGarrisonMapRotationEntry> StockMapRotation { get; set; } = OpenGarrisonStockMapCatalog.CreateDefaultEntries();

    public string GetFirstIncludedMapLevelName()
    {
        var includedMaps = OpenGarrisonStockMapCatalog.GetOrderedIncludedMapLevelNames(StockMapRotation);
        return includedMaps.Count > 0
            ? includedMaps[0]
            : OpenGarrisonStockMapCatalog.Definitions[0].LevelName;
    }

    public void SetPreferredMap(string levelName)
    {
        var includedEntries = OpenGarrisonStockMapCatalog.GetOrderedEntries(StockMapRotation)
            .Where(entry => entry.Order > 0)
            .ToList();
        var selectedEntry = includedEntries.FirstOrDefault(entry =>
            string.Equals(entry.LevelName, levelName, StringComparison.OrdinalIgnoreCase));
        if (selectedEntry is null)
        {
            selectedEntry = StockMapRotation.FirstOrDefault(entry =>
                string.Equals(entry.LevelName, levelName, StringComparison.OrdinalIgnoreCase));
        }

        if (selectedEntry is null)
        {
            return;
        }

        includedEntries.RemoveAll(entry => string.Equals(entry.LevelName, selectedEntry.LevelName, StringComparison.OrdinalIgnoreCase));
        includedEntries.Insert(0, selectedEntry);
        for (var index = 0; index < includedEntries.Count; index += 1)
        {
            includedEntries[index].Order = index + 1;
        }

        foreach (var entry in StockMapRotation)
        {
            if (includedEntries.Any(candidate => string.Equals(candidate.LevelName, entry.LevelName, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (entry.Order > 0)
            {
                entry.Order = includedEntries.Count + 1;
                includedEntries.Add(entry);
            }
        }
    }

    public OpenGarrisonHostSettings Clone()
    {
        return new OpenGarrisonHostSettings
        {
            ServerName = ServerName,
            Port = Port,
            Slots = Slots,
            Password = Password,
            TimeLimitMinutes = TimeLimitMinutes,
            CapLimit = CapLimit,
            RespawnSeconds = RespawnSeconds,
            TickRate = TickRate,
            RconPassword = RconPassword,
            BotAutofillEnabled = BotAutofillEnabled,
            BotAutofillMinPlayers = BotAutofillMinPlayers,
            BotAutofillPerTeam = BotAutofillPerTeam,
            LobbyAnnounceEnabled = LobbyAnnounceEnabled,
            AutoBalanceEnabled = AutoBalanceEnabled,
            SecondaryAbilitiesEnabled = SecondaryAbilitiesEnabled,
            RandomSpreadEnabled = RandomSpreadEnabled,
            SniperAimIndicatorEnabled = SniperAimIndicatorEnabled,
            LocalPredictionEnabled = LocalPredictionEnabled,
            RoundEndFriendlyFireEnabled = RoundEndFriendlyFireEnabled,
            SwitchTeamsAfterRoundEnd = SwitchTeamsAfterRoundEnd,
            TeamShuffleAfterWins = NormalizeTeamShuffleAfterWins(TeamShuffleAfterWins),
            CompetitiveReadyUpEnabled = CompetitiveReadyUpEnabled,
            CompetitiveSetupSeconds = CompetitiveSetupSeconds,
            PlayerScale = PlayerScale,
            MapScale = MapScale,
            MovementSpeedScale = MovementSpeedScale,
            ProjectileSpeedScale = ProjectileSpeedScale,
            DamageScale = DamageScale,
            GravityScale = GravityScale,
            HorizontalSpeedClampPerTick = HorizontalSpeedClampPerTick,
            VerticalSpeedClampPerTick = VerticalSpeedClampPerTick,
            CaptureSpeedMultiplierPerPlayer = CaptureSpeedMultiplierPerPlayer,
            VipAllowDuplicateClasses = VipAllowDuplicateClasses,
            ClassLimitScout = ClassLimitScout,
            ClassLimitEngineer = ClassLimitEngineer,
            ClassLimitPyro = ClassLimitPyro,
            ClassLimitSoldier = ClassLimitSoldier,
            ClassLimitDemoman = ClassLimitDemoman,
            ClassLimitHeavy = ClassLimitHeavy,
            ClassLimitSniper = ClassLimitSniper,
            ClassLimitMedic = ClassLimitMedic,
            ClassLimitSpy = ClassLimitSpy,
            ClassLimitCivilian = ClassLimitCivilian,
            DedicatedModeEnabled = DedicatedModeEnabled,
            MapRotationFile = MapRotationFile,
            MapRotationShuffleEnabled = MapRotationShuffleEnabled,
            MapRotationAdvanceMode = NormalizeMapRotationAdvanceMode(MapRotationAdvanceMode),
            MapRotationRounds = NormalizeMapRotationRounds(MapRotationRounds),
            MapRotationMinutes = NormalizeMapRotationMinutes(MapRotationMinutes),
            UsePlaylistFile = UsePlaylistFile,
            StockMapRotation = StockMapRotation.Select(entry => entry.Clone()).ToList(),
        };
    }

    internal static OpenGarrisonHostSettings LoadFrom(IniConfigurationFile ini, string legacySelectedMap)
    {
        return new OpenGarrisonHostSettings
        {
            ServerName = ini.GetString("Server", "ServerName", "My Server"),
            Port = ini.GetInt("Settings", "HostingPort", 8190),
            Slots = ini.GetInt("Settings", "PlayerLimit", 10),
            Password = ini.GetString("Server", "Password", string.Empty),
            TimeLimitMinutes = ini.GetInt("Server", "Time Limit", 15),
            CapLimit = ini.GetInt("Server", "CapLimit", 5),
            RespawnSeconds = ini.GetInt("Server", "Respawn Time", 5),
            TickRate = SimulationConfig.NormalizeTicksPerSecond(
                ini.GetInt("Server.Advanced", "TickRate", SimulationConfig.DefaultTicksPerSecond)),
            RconPassword = ini.GetString("Server.Advanced", "RconPassword", string.Empty),
            BotAutofillEnabled = ini.GetBool("Server.Advanced", "BotAutofillEnabled", false),
            BotAutofillMinPlayers = Math.Clamp(
                ini.GetInt("Server.Advanced", "BotAutofillMinPlayers", 0),
                0,
                SimulationWorld.MaxPlayableNetworkPlayers),
            BotAutofillPerTeam = Math.Clamp(
                ini.GetInt("Server.Advanced", "BotAutofillPerTeam", 0),
                0,
                SimulationWorld.MaxPlayableNetworkPlayers / 2),
            LobbyAnnounceEnabled = ini.GetBool("Settings", "UseLobby", true),
            AutoBalanceEnabled = ini.GetBool("Server", "AutoBalance", true),
            SecondaryAbilitiesEnabled = ini.ContainsKey("Server", "SpecialAbilities")
                ? ini.GetBool("Server", "SpecialAbilities", true)
                : ini.GetBool("Server", "SecondaryAbilities", true),
            RandomSpreadEnabled = ini.ContainsKey("Server.Advanced", "RandomSpreadEnabled")
                ? ini.GetBool("Server.Advanced", "RandomSpreadEnabled", true)
                : true,
            SniperAimIndicatorEnabled = ini.GetBool("Server.Advanced", "SniperAimIndicatorEnabled", true),
            LocalPredictionEnabled = ini.GetBool("Server.Advanced", "LocalPredictionEnabled", false),
            RoundEndFriendlyFireEnabled = ini.GetBool("Server.Advanced", "RoundEndFriendlyFireEnabled", false),
            SwitchTeamsAfterRoundEnd = ini.GetBool("Server.Advanced", "SwitchTeamsAfterRoundEnd", false),
            TeamShuffleAfterWins = NormalizeTeamShuffleAfterWins(
                ini.ContainsKey("Server.Advanced", "TeamShuffleAfterWins")
                    ? ini.GetInt("Server.Advanced", "TeamShuffleAfterWins", 0)
                    : ini.GetInt("Server.Advanced", "TeamScrambleAfterWins", 0)),
            CompetitiveReadyUpEnabled = ini.GetBool("Server.Advanced", "CompetitiveReadyUpEnabled", false),
            CompetitiveSetupSeconds = Math.Clamp(ini.GetInt("Server.Advanced", "CompetitiveSetupSeconds", 10), 0, 120),
            PlayerScale = PlayerEntity.ClampPlayerScale(ini.GetFloat("Server.Advanced", "PlayerScale", 1f)),
            MapScale = float.Clamp(ini.GetFloat("Server.Advanced", "MapScale", 1f), 0.25f, 4f),
            MovementSpeedScale = float.Clamp(ini.GetFloat("Server.Advanced", "MovementSpeedScale", 1f), 0.1f, 4f),
            ProjectileSpeedScale = float.Clamp(ini.GetFloat("Server.Advanced", "ProjectileSpeedScale", 1f), 0.1f, 4f),
            DamageScale = float.Clamp(ini.GetFloat("Server.Advanced", "DamageScale", 1f), 0f, 10f),
            GravityScale = float.Clamp(ini.GetFloat("Server.Advanced", "GravityScale", 1f), 0f, 4f),
            HorizontalSpeedClampPerTick = float.Clamp(
                ini.GetFloat("Server.Advanced", "HorizontalSpeedClampPerTick", LegacyMovementModel.MaxStepSpeedPerTick),
                1f,
                60f),
            VerticalSpeedClampPerTick = float.Clamp(
                ini.GetFloat("Server.Advanced", "VerticalSpeedClampPerTick", LegacyMovementModel.MaxStepSpeedPerTick),
                1f,
                60f),
            CaptureSpeedMultiplierPerPlayer = float.Clamp(ini.GetFloat("Server.Advanced", "CaptureSpeedMultiplierPerPlayer", 2f), 0f, 10f),
            VipAllowDuplicateClasses = ini.GetBool("Server.Advanced", "VipAllowDuplicateClasses", false),
            ClassLimitScout = NormalizeClassLimit(ini.GetInt("Server.Advanced", "ClassLimitScout", 0)),
            ClassLimitEngineer = NormalizeClassLimit(ini.GetInt("Server.Advanced", "ClassLimitEngineer", 0)),
            ClassLimitPyro = NormalizeClassLimit(ini.GetInt("Server.Advanced", "ClassLimitPyro", 0)),
            ClassLimitSoldier = NormalizeClassLimit(ini.GetInt("Server.Advanced", "ClassLimitSoldier", 0)),
            ClassLimitDemoman = NormalizeClassLimit(ini.GetInt("Server.Advanced", "ClassLimitDemoman", 0)),
            ClassLimitHeavy = NormalizeClassLimit(ini.GetInt("Server.Advanced", "ClassLimitHeavy", 0)),
            ClassLimitSniper = NormalizeClassLimit(ini.GetInt("Server.Advanced", "ClassLimitSniper", 0)),
            ClassLimitMedic = NormalizeClassLimit(ini.GetInt("Server.Advanced", "ClassLimitMedic", 0)),
            ClassLimitSpy = NormalizeClassLimit(ini.GetInt("Server.Advanced", "ClassLimitSpy", 0)),
            ClassLimitCivilian = NormalizeClassLimit(ini.GetInt("Server.Advanced", "ClassLimitCivilian", 0)),
            DedicatedModeEnabled = ini.GetBool("Server", "Dedicated", false),
            MapRotationFile = ini.GetString("Server", "MapRotation", string.Empty),
            MapRotationShuffleEnabled = ini.GetBool("Server", "MapRotationShuffle", false),
            MapRotationAdvanceMode = ParseMapRotationAdvanceMode(ini.GetString("Server", "MapRotationAdvanceMode", MapRotationAdvanceMode.RoundEnd.ToString())),
            MapRotationRounds = NormalizeMapRotationRounds(ini.GetInt("Server", "MapRotationRounds", 1)),
            MapRotationMinutes = NormalizeMapRotationMinutes(ini.GetInt("Server", "MapRotationMinutes", 15)),
            UsePlaylistFile = ini.GetBool("Server", "UsePlaylistFile", false),
            StockMapRotation = OpenGarrisonStockMapCatalog.LoadFrom(ini, legacySelectedMap),
        };
    }

    public static MapRotationAdvanceMode NormalizeMapRotationAdvanceMode(MapRotationAdvanceMode mode)
    {
        return mode switch
        {
            MapRotationAdvanceMode.RoundEnd => MapRotationAdvanceMode.RoundEnd,
            MapRotationAdvanceMode.RoundCount => MapRotationAdvanceMode.RoundCount,
            MapRotationAdvanceMode.TimeElapsed => MapRotationAdvanceMode.TimeElapsed,
            _ => MapRotationAdvanceMode.RoundEnd,
        };
    }

    public static MapRotationAdvanceMode ParseMapRotationAdvanceMode(string value)
    {
        if (Enum.TryParse<MapRotationAdvanceMode>(value, ignoreCase: true, out var parsed))
        {
            return NormalizeMapRotationAdvanceMode(parsed);
        }

        return MapRotationAdvanceMode.RoundEnd;
    }

    public static int NormalizeMapRotationRounds(int rounds)
    {
        return Math.Clamp(rounds, 1, 255);
    }

    public static int NormalizeMapRotationMinutes(int minutes)
    {
        return Math.Clamp(minutes, 1, 255);
    }

    public static int NormalizeTeamShuffleAfterWins(int wins)
    {
        return Math.Clamp(wins, 0, 255);
    }

    public static int NormalizeClassLimit(int limit)
    {
        return Math.Clamp(limit, 0, SimulationWorld.MaxPlayableNetworkPlayers);
    }
}

public sealed class OpenGarrisonMapRotationEntry
{
    public string IniKey { get; init; } = string.Empty;

    public string LevelName { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public GameModeKind Mode { get; init; }

    public bool IsCustomMap { get; init; }

    public bool IsPlaylistClone { get; set; }

    public int DefaultOrder { get; init; }

    public int Order { get; set; }

    public OpenGarrisonMapRotationEntry Clone()
    {
        return new OpenGarrisonMapRotationEntry
        {
            IniKey = IniKey,
            LevelName = LevelName,
            DisplayName = DisplayName,
            Mode = Mode,
            IsCustomMap = IsCustomMap,
            IsPlaylistClone = IsPlaylistClone,
            DefaultOrder = DefaultOrder,
            Order = Order,
        };
    }
}

public readonly record struct OpenGarrisonStockMapDefinition(
    string IniKey,
    string LevelName,
    string DisplayName,
    GameModeKind Mode,
    int DefaultOrder,
    params string[] Aliases);

public static class OpenGarrisonStockMapCatalog
{
    private const string MapsSection = "Maps";
    private const string CustomMapsSection = "CustomMaps";
    private const string MapRotationPlaylistSection = "MapRotationPlaylist";

    public static IReadOnlyList<OpenGarrisonStockMapDefinition> Definitions { get; } =
    [
        new("ctf_truefort", "Truefort", "Truefort", GameModeKind.CaptureTheFlag, 11, "truefort"),
        new("ctf_2dfort", "TwodFortTwo", "2dFort", GameModeKind.CaptureTheFlag, 0, "2dfort", "twodforttwo", "2dfort2", "2dfortremix"),
        new("ctf_conflict", "Conflict", "Conflict", GameModeKind.CaptureTheFlag, 8, "conflict"),
        new("ctf_waterway", "Waterway", "Waterway", GameModeKind.CaptureTheFlag, 7, "waterway"),
        new("cp_dirtbowl", "Dirtbowl", "Dirtbowl", GameModeKind.ControlPoint, 3, "dirtbowl", "cp_dirtbowl_stage1", "cp_dirtbowl_stage2", "cp_dirtbowl_stage3"),
        new("cp_egypt", "Egypt", "Egypt", GameModeKind.ControlPoint, 4, "egypt"),
        new("arena_montane", "Montane", "Montane", GameModeKind.Arena, 10, "montane"),
        new("arena_lumberyard", "Lumberyard", "Lumberyard", GameModeKind.Arena, 9, "lumberyard"),
        new("koth_valley", "Valley", "Valley", GameModeKind.KingOfTheHill, 5, "valley"),
        new("koth_corinth", "Corinth", "Corinth", GameModeKind.KingOfTheHill, 12, "corinth"),
        new("koth_harvest", "Harvest", "Harvest", GameModeKind.KingOfTheHill, 1, "harvest"),
        new("tdm_mantic", "Mantic", "Mantic", GameModeKind.TeamDeathmatch, 0, "mantic"),
        new("koth_gallery", "Gallery", "Gallery", GameModeKind.KingOfTheHill, 2, "gallery"),
        new("ctf_eiger", "Eiger", "Eiger", GameModeKind.CaptureTheFlag, 6, "eiger"),
    ];

    private static IReadOnlyList<OpenGarrisonStockMapDefinition> HiddenDefinitions { get; } =
    [
        new("ctf_avanti", "Avanti", "Avanti", GameModeKind.CaptureTheFlag, 0, "avanti"),
        new("ctf_classicwell", "ClassicWell", "ClassicWell", GameModeKind.CaptureTheFlag, 0, "classicwell"),
        new("ctf_orange", "Orange", "Orange", GameModeKind.CaptureTheFlag, 0, "orange"),
        new("dkoth_atalia", "Atalia", "Atalia", GameModeKind.DoubleKingOfTheHill, 0, "atalia"),
        new("dkoth_sixties", "Sixties", "Sixties", GameModeKind.DoubleKingOfTheHill, 0, "sixties", "60s", "dkoth_60s"),
        new("gen_destroy", "Destroy", "Destroy", GameModeKind.Generator, 0, "destroy"),
    ];

    public static IReadOnlyList<OpenGarrisonStockMapDefinition> SourceDefinitions { get; } =
        Definitions.Concat(HiddenDefinitions).ToArray();

    private static IEnumerable<OpenGarrisonStockMapDefinition> AllDefinitions => SourceDefinitions;

    public static bool IsCpPrefixedIniKey(string? iniKey)
    {
        return !string.IsNullOrWhiteSpace(iniKey)
            && iniKey.StartsWith("cp_", StringComparison.OrdinalIgnoreCase);
    }

    public static string GetVipIniKeyForRotationEntry(OpenGarrisonMapRotationEntry entry)
    {
        if (TryGetDefinition(entry.IniKey, out var definition) && IsCpPrefixedIniKey(definition.IniKey))
        {
            return GetVipIniKey(definition);
        }

        if (TryGetDefinition(entry.LevelName, out definition) && IsCpPrefixedIniKey(definition.IniKey))
        {
            return GetVipIniKey(definition);
        }

        return GetVipIniKeyFromCpIniKey(entry.IniKey);
    }

    public static bool TryCreateVipMapRotationEntry(
        OpenGarrisonMapRotationEntry baseEntry,
        out OpenGarrisonMapRotationEntry vipEntry)
    {
        vipEntry = null!;
        if (!IsCpPrefixedIniKey(baseEntry.IniKey) || baseEntry.Mode == GameModeKind.Vip)
        {
            return false;
        }

        var vipIniKey = GetVipIniKeyForRotationEntry(baseEntry);
        if (TryGetDefinition(vipIniKey, out var vipDefinition))
        {
            vipEntry = new OpenGarrisonMapRotationEntry
            {
                IniKey = vipDefinition.IniKey,
                LevelName = vipDefinition.LevelName,
                DisplayName = vipDefinition.DisplayName,
                Mode = GameModeKind.Vip,
                IsCustomMap = baseEntry.IsCustomMap,
                DefaultOrder = 0,
                Order = 0,
            };
            return true;
        }

        vipEntry = new OpenGarrisonMapRotationEntry
        {
            IniKey = vipIniKey,
            LevelName = vipIniKey,
            DisplayName = $"{baseEntry.DisplayName} (VIP)",
            Mode = GameModeKind.Vip,
            IsCustomMap = baseEntry.IsCustomMap,
            DefaultOrder = 0,
            Order = 0,
        };
        return true;
    }

    public static void AppendVipMapDuplicates(List<OpenGarrisonMapRotationEntry> mapEntries)
    {
        foreach (var baseEntry in mapEntries
                     .Where(entry => IsCpPrefixedIniKey(entry.IniKey) && entry.Mode != GameModeKind.Vip)
                     .ToList())
        {
            if (!TryCreateVipMapRotationEntry(baseEntry, out var vipEntry))
            {
                continue;
            }

            if (mapEntries.Any(entry =>
                    string.Equals(entry.IniKey, vipEntry.IniKey, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(entry.LevelName, vipEntry.LevelName, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            mapEntries.Add(vipEntry);
        }
    }

    public static List<OpenGarrisonMapRotationEntry> CreateDefaultEntries()
    {
        var entries = Definitions
            .Select(definition => new OpenGarrisonMapRotationEntry
            {
                IniKey = definition.IniKey,
                LevelName = definition.LevelName,
                DisplayName = definition.DisplayName,
                Mode = definition.Mode,
                DefaultOrder = definition.DefaultOrder,
                Order = definition.DefaultOrder,
            })
            .ToList();
        AppendVipMapDuplicates(entries);
        return entries;
    }

    public static List<OpenGarrisonMapRotationEntry> LoadFrom(IniConfigurationFile ini, string legacySelectedMap)
    {
        var entries = CreateDefaultEntries();
        var hasExplicitMaps = false;
        foreach (var entry in entries)
        {
            if (!ini.ContainsKey(MapsSection, entry.IniKey))
            {
                continue;
            }

            hasExplicitMaps = true;
            entry.Order = Math.Max(0, ini.GetInt(MapsSection, entry.IniKey, entry.DefaultOrder));
        }

        if (!hasExplicitMaps && TryGetDefinition(legacySelectedMap, out var legacyDefinition) && legacyDefinition.DefaultOrder > 0)
        {
            var selectedEntry = entries.First(entry => string.Equals(entry.LevelName, legacyDefinition.LevelName, StringComparison.OrdinalIgnoreCase));
            entries.Remove(selectedEntry);
            entries.Insert(0, selectedEntry);
            var order = 1;
            foreach (var entry in entries)
            {
                entry.Order = entry.DefaultOrder > 0 ? order++ : 0;
            }
        }

        foreach (var (levelName, orderText) in ini.GetSectionEntries(CustomMapsSection))
        {
            if (string.IsNullOrWhiteSpace(levelName)
                || TryGetDefinition(levelName, out _))
            {
                continue;
            }

            var resolvedLevel = SimpleLevelFactory.GetAvailableSourceLevels()
                .FirstOrDefault(level => string.Equals(level.Name, levelName, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(resolvedLevel.Name))
            {
                continue;
            }

            entries.Add(new OpenGarrisonMapRotationEntry
            {
                IniKey = resolvedLevel.Name,
                LevelName = resolvedLevel.Name,
                DisplayName = resolvedLevel.Name,
                Mode = resolvedLevel.Mode,
                IsCustomMap = true,
                DefaultOrder = entries.Count + 1,
                Order = int.TryParse(orderText, out var order) ? Math.Max(0, order) : 0,
            });
        }

        ApplySavedPlaylist(ini, entries);
        return entries;
    }

    public static void SaveTo(IniConfigurationFile ini, IEnumerable<OpenGarrisonMapRotationEntry> entries)
    {
        var entryList = entries.ToArray();
        foreach (var entry in Definitions)
        {
            var configuredEntry = entryList.FirstOrDefault(candidate =>
                string.Equals(candidate.IniKey, entry.IniKey, StringComparison.OrdinalIgnoreCase));
            ini.SetInt(MapsSection, entry.IniKey, Math.Max(0, configuredEntry?.Order ?? 0));
        }

        foreach (var entry in entryList.Where(entry => entry.IsCustomMap || !TryGetDefinition(entry.LevelName, out _)))
        {
            if (string.IsNullOrWhiteSpace(entry.LevelName))
            {
                continue;
            }

            ini.SetInt(CustomMapsSection, entry.LevelName, Math.Max(0, entry.Order));
        }

        var playlistEntries = GetOrderedEntries(entryList)
            .Where(entry => entry.Order > 0)
            .ToArray();
        ini.SetInt(MapRotationPlaylistSection, "Count", playlistEntries.Length);
        for (var index = 0; index < playlistEntries.Length; index += 1)
        {
            var entry = playlistEntries[index];
            var token = string.IsNullOrWhiteSpace(entry.IniKey) ? entry.LevelName : entry.IniKey;
            ini.SetString(MapRotationPlaylistSection, (index + 1).ToString(CultureInfo.InvariantCulture), token);
        }
    }

    public static IReadOnlyList<OpenGarrisonMapRotationEntry> GetOrderedEntries(IEnumerable<OpenGarrisonMapRotationEntry> entries)
    {
        return entries
            .OrderBy(entry => entry.Order <= 0 ? int.MaxValue : entry.Order)
            .ThenBy(entry => entry.DefaultOrder)
            .ToArray();
    }

    public static IReadOnlyList<string> GetOrderedIncludedMapLevelNames(IEnumerable<OpenGarrisonMapRotationEntry> entries)
    {
        return GetOrderedEntries(entries)
            .Where(entry => entry.Order > 0)
            .Select(entry => entry.LevelName)
            .ToArray();
    }

    public static IReadOnlyList<string> GetDefaultPlaylistIniKeys()
    {
        return Definitions
            .Where(definition => definition.DefaultOrder > 0)
            .OrderBy(definition => definition.DefaultOrder)
            .Select(definition => definition.IniKey)
            .ToArray();
    }

    public static bool TryGetDefinition(string mapName, out OpenGarrisonStockMapDefinition definition)
    {
        var normalized = NormalizeMapName(mapName);
        if (TryGetVipDefinition(normalized, out definition))
        {
            return true;
        }

        foreach (var candidate in AllDefinitions)
        {
            if (string.Equals(candidate.IniKey, normalized, StringComparison.OrdinalIgnoreCase)
                || string.Equals(candidate.LevelName, normalized, StringComparison.OrdinalIgnoreCase)
                || candidate.Aliases.Any(alias => string.Equals(alias, normalized, StringComparison.OrdinalIgnoreCase)))
            {
                definition = candidate;
                return true;
            }
        }

        definition = default;
        return false;
    }

    private static void ApplySavedPlaylist(IniConfigurationFile ini, List<OpenGarrisonMapRotationEntry> entries)
    {
        var count = ini.GetInt(MapRotationPlaylistSection, "Count", 0);
        if (count <= 0)
        {
            return;
        }

        entries.RemoveAll(entry => entry.IsPlaylistClone);
        foreach (var entry in entries)
        {
            entry.Order = 0;
        }

        var catalog = entries
            .Where(entry => !entry.IsPlaylistClone)
            .ToList();
        for (var index = 1; index <= count; index += 1)
        {
            var token = ini.GetString(MapRotationPlaylistSection, index.ToString(CultureInfo.InvariantCulture));
            if (!TryResolveMapRotationEntry(token, catalog, out var resolved))
            {
                continue;
            }

            var playlistEntry = resolved.Order > 0 ? resolved.Clone() : resolved;
            if (!ReferenceEquals(playlistEntry, resolved))
            {
                playlistEntry.IsPlaylistClone = true;
                entries.Add(playlistEntry);
            }

            playlistEntry.Order = index;
        }
    }

    private static bool TryResolveMapRotationEntry(
        string token,
        IReadOnlyList<OpenGarrisonMapRotationEntry> catalog,
        out OpenGarrisonMapRotationEntry entry)
    {
        entry = null!;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        if (TryGetDefinition(token, out var definition))
        {
            entry = catalog.FirstOrDefault(candidate =>
                string.Equals(candidate.IniKey, token, StringComparison.OrdinalIgnoreCase)
                || string.Equals(candidate.LevelName, token, StringComparison.OrdinalIgnoreCase)
                || string.Equals(candidate.DisplayName, token, StringComparison.OrdinalIgnoreCase)
                || string.Equals(candidate.IniKey, definition.IniKey, StringComparison.OrdinalIgnoreCase)
                || string.Equals(candidate.LevelName, definition.LevelName, StringComparison.OrdinalIgnoreCase));
            return entry is not null;
        }

        entry = catalog.FirstOrDefault(candidate =>
            string.Equals(candidate.LevelName, token, StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate.IniKey, token, StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate.DisplayName, token, StringComparison.OrdinalIgnoreCase));
        return entry is not null;
    }

    public static string GetVipIniKey(OpenGarrisonStockMapDefinition definition)
    {
        if (IsCpPrefixedIniKey(definition.IniKey))
        {
            return GetVipIniKeyFromCpIniKey(definition.IniKey);
        }

        var shortName = definition.Aliases.FirstOrDefault(alias =>
            alias.IndexOf('_') < 0
            && !string.Equals(alias, "dustbowl", StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(shortName))
        {
            shortName = NormalizeMapName(definition.LevelName);
        }

        return $"vip_{shortName.ToLowerInvariant()}";
    }

    private static string GetVipIniKeyFromCpIniKey(string cpIniKey)
    {
        return $"vip_{cpIniKey["cp_".Length..].ToLowerInvariant()}";
    }

    private static bool TryGetVipDefinition(string normalizedMapName, out OpenGarrisonStockMapDefinition definition)
    {
        definition = default;
        if (!normalizedMapName.StartsWith("vip_", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var baseName = normalizedMapName["vip_".Length..];
        foreach (var candidate in AllDefinitions)
        {
            if (!IsCpPrefixedIniKey(candidate.IniKey))
            {
                continue;
            }

            if (!IsVipBaseMapMatch(candidate, baseName))
            {
                continue;
            }

            var vipIniKey = GetVipIniKey(candidate);
            definition = new OpenGarrisonStockMapDefinition(
                vipIniKey,
                vipIniKey,
                $"{candidate.DisplayName} (VIP)",
                GameModeKind.Vip,
                0,
                CreateVipAliases(candidate));
            return true;
        }

        return false;
    }

    private static bool IsVipBaseMapMatch(OpenGarrisonStockMapDefinition definition, string baseName)
    {
        return string.Equals(definition.IniKey, baseName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(definition.LevelName, baseName, StringComparison.OrdinalIgnoreCase)
            || definition.Aliases.Any(alias => string.Equals(alias, baseName, StringComparison.OrdinalIgnoreCase))
            || (IsCpPrefixedIniKey(definition.IniKey)
                && string.Equals(definition.IniKey["cp_".Length..], baseName, StringComparison.OrdinalIgnoreCase))
            || (string.Equals(baseName, "dustbowl", StringComparison.OrdinalIgnoreCase)
                && definition.Aliases.Any(alias => string.Equals(alias, "dirtbowl", StringComparison.OrdinalIgnoreCase)));
    }

    private static string[] CreateVipAliases(OpenGarrisonStockMapDefinition definition)
    {
        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            $"vip_{definition.IniKey}",
            $"vip_{definition.LevelName}",
        };
        foreach (var alias in definition.Aliases)
        {
            aliases.Add($"vip_{alias}");
        }

        if (definition.Aliases.Any(alias => string.Equals(alias, "dirtbowl", StringComparison.OrdinalIgnoreCase)))
        {
            aliases.Add("vip_dustbowl");
        }

        return aliases.ToArray();
    }

    private static string NormalizeMapName(string? mapName)
    {
        if (string.IsNullOrWhiteSpace(mapName))
        {
            return string.Empty;
        }

        var trimmed = mapName.Trim();
        var underscoreIndex = trimmed.IndexOf('_');
        if (underscoreIndex > 0)
        {
            var prefix = trimmed[..underscoreIndex];
            if (prefix.Equals("ctf", StringComparison.OrdinalIgnoreCase)
                || prefix.Equals("cp", StringComparison.OrdinalIgnoreCase)
                || prefix.Equals("arena", StringComparison.OrdinalIgnoreCase)
                || prefix.Equals("gen", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed;
            }
        }

        return trimmed;
    }
}
