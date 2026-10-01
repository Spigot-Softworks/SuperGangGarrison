#nullable enable

using System;
using System.Globalization;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    public ClientSettings _clientSettings => _services.Get<ClientSettings>();
    public InputBindingsSettings _inputBindings => _services.Get<InputBindingsSettings>();
    public const string DefaultBrowserSecureManualConnectHost = "wss://45-61-52-208.sslip.io/opengarrison/ws";
    public const int DefaultBrowserSecureManualConnectPort = 443;

    public void ApplyLoadedSettings()
    {
        InitializeVoiceSettings();
        ApplyGraphicsSettings(persist: false);

        _audioManager.RuntimeSettings.MusicMode = _clientSettings.MusicMode;
        ApplyConfiguredPracticeBotController(respawnActiveBots: false);
        _menuManager.DisplaySettings.FrameRateLimit = NormalizeFrameRateLimit(_clientSettings.FrameRateLimit);
        _gameplayManager.RuntimeSettings.KillCamEnabled = _clientSettings.KillCamEnabled;
        _gameplayManager.RuntimeSettings.ParticleMode = Math.Clamp(_clientSettings.ParticleMode, 0, 2);
        _gameplayManager.RuntimeSettings.FlameRenderMode = Math.Clamp(_clientSettings.FlameRenderMode, 0, 1);
        _gameplayManager.RuntimeSettings.BloodRenderMode = Math.Clamp(_clientSettings.BloodRenderMode, 0, 1);
        _gameplayManager.RuntimeSettings.DynamicRagdollEnabled = _clientSettings.DynamicRagdollEnabled;
        _gameplayManager.RuntimeSettings.BurnCharredCorpsesEnabled = _clientSettings.BurnCharredCorpsesEnabled;
        _gameplayManager.RuntimeSettings.BloodPersistenceSeconds = Math.Clamp(
            _clientSettings.BloodPersistenceSeconds <= 0
                ? OpenGarrisonPreferencesDocument.DefaultBloodPersistenceSeconds
                : _clientSettings.BloodPersistenceSeconds,
            1,
            120);
        _gameplayManager.RuntimeSettings.CorpseFadeMode = OpenGarrisonPreferencesDocument.NormalizeCorpseFadeMode(_clientSettings.CorpseFadeMode);
        _gameplayManager.RuntimeSettings.MenuBackgroundMode = _clientSettings.MenuBackgroundMode;
        _gameplayManager.RuntimeSettings.GibLevel = Math.Clamp(_clientSettings.GibLevel, 0, 3);
        _gameplayManager.RuntimeSettings.BloodAmountLevel = Math.Clamp(_clientSettings.BloodAmountLevel, 1, 5);
        _gameplayManager.RuntimeSettings.CorpseDurationMode = Math.Clamp(_clientSettings.CorpseDurationMode, ClientSettings.CorpseDurationDefault, ClientSettings.CorpseDurationInfinite);
        _hudManager.RuntimeSettings.HealerRadarEnabled = _clientSettings.HealerRadarEnabled;
        _hudManager.RuntimeSettings.ShowHealerEnabled = _clientSettings.ShowHealerEnabled;
        _hudManager.RuntimeSettings.ShowHealingEnabled = _clientSettings.ShowHealingEnabled;
        _hudManager.RuntimeSettings.ShowHealthBarEnabled = _clientSettings.ShowHealthBarEnabled;
        _hudManager.RuntimeSettings.ShowShieldBarEnabled = _clientSettings.ShowShieldBarEnabled;
        _hudManager.RuntimeSettings.HudShowOnlyActiveWeapon = _clientSettings.HudShowOnlyActiveWeapon;
        _hudManager.RuntimeSettings.OverheadChatEnabled = _clientSettings.OverheadChatEnabled;
        ApplyLoadedBubbleWheelPluginSettings();
        _hudManager.RuntimeSettings.PortraitRumbleEnabled = _clientSettings.PortraitRumbleEnabled;
        _hudManager.RuntimeSettings.PostGameMvpArtEnabled = _clientSettings.PostGameMvpArtEnabled;
        _hudManager.RuntimeSettings.DamageVignetteEnabled = _clientSettings.DamageVignetteEnabled;
        _hudManager.RuntimeSettings.DamageVignetteIntensityPercent = ClientSettings.NormalizeDamageVignetteIntensityPercent(_clientSettings.DamageVignetteIntensityPercent);
        _hudManager.RuntimeSettings.LowHealthColorMode = ClientSettings.NormalizeLowHealthColorMode(_clientSettings.LowHealthColorMode);
        _hudManager.RuntimeSettings.ShowPersistentSelfNameEnabled = _clientSettings.ShowPersistentSelfNameEnabled;
        _hudManager.RuntimeSettings.ShowPlayerNamesEnabled = _clientSettings.ShowPlayerNamesEnabled;
        _gameplayManager.RuntimeSettings.PositionSmoothingEnabled = _clientSettings.PositionSmoothingEnabled;
        _gameplayManager.RuntimeSettings.EnablePrediction = _clientSettings.EnablePrediction;
        _gameplayManager.RuntimeSettings.CameraPanningEnabled = _clientSettings.CameraPanningEnabled;
        _gameplayManager.RuntimeSettings.SmoothCameraMultiplier = NormalizeSmoothCameraMultiplier(_clientSettings.SmoothCameraMultiplier);
        if (_gameplayManager.RuntimeSettings.SmoothCameraMultiplier <= 0f)
        {
            _hasSmoothCamera = false;
        }

        _gameplayManager.RuntimeSettings.SpriteDropShadowEnabled = _clientSettings.SpriteDropShadowEnabled;
        _gameplayManager.RuntimeSettings.StuckArrowsEnabled = _clientSettings.StuckArrowsEnabled;
        _gameplayManager.RuntimeSettings.WeaponBobMode = OpenGarrisonPreferencesDocument.NormalizeWeaponBobMode(_clientSettings.WeaponBobMode);
        _gameplayManager.RuntimeSettings.PixelPerfectWeaponRotation = _clientSettings.PixelPerfectWeaponRotation;
        _gameplayManager.RuntimeSettings.UseLocalWeaponRotation = _clientSettings.UseLocalWeaponRotation;
        _hudManager.RuntimeSettings.PlayerCardSizeMode = ClientSettings.NormalizePlayerCardSizeMode(_clientSettings.PlayerCardSizeMode);
        _hudManager.RuntimeSettings.CursorSizePercent = ClientSettings.NormalizeCursorSizePercent(_clientSettings.CursorSizePercent);
        _gameplayManager.RuntimeSettings.ShowUberOutlinesEnabled = _clientSettings.ShowUberOutlinesEnabled;
        _gameplayManager.RuntimeSettings.ProjectileTeamTintEnabled = _clientSettings.ProjectileTeamTintEnabled;
        _audioManager.RuntimeSettings.AudioMuted = _clientSettings.AudioMuted;
        _audioManager.RuntimeSettings.MasterVolumePercent = Math.Clamp(_clientSettings.MasterVolumePercent, 0, 100);
        _audioManager.RuntimeSettings.MenuMusicVolumePercent = Math.Clamp(_clientSettings.MenuMusicVolumePercent, 0, 100);
        _audioManager.RuntimeSettings.IngameMusicVolumePercent = Math.Clamp(_clientSettings.IngameMusicVolumePercent, 0, 100);
        _audioManager.RuntimeSettings.DynamicMusicEnabled = _clientSettings.DynamicMusicEnabled;
        _audioManager.RuntimeSettings.CombatMusicVolumePercent = Math.Clamp(_clientSettings.CombatMusicVolumePercent, 0, OpenGarrisonPreferencesDocument.MaxCombatMusicVolumePercent);
        _audioManager.RuntimeSettings.SoundEffectsVolumePercent = Math.Clamp(_clientSettings.SoundEffectsVolumePercent, 0, 100);
        ApplyAudioVolumeState();

        _world.SetLocalPlayerName(_clientSettings.PlayerName);
        _world.SetLocalPlayerBadgeMask(BadgeCatalog.ParseRewardString(_clientSettings.Rewards));
        _playerNameEditBuffer = _world.LocalPlayer.DisplayName;

        _inputManager.MenuTextInput.ConnectHostEdit.Text = SanitizeHost(_clientSettings.RecentConnection.Host);
        _inputManager.MenuTextInput.ConnectPortEdit.Text = SanitizePort(_clientSettings.RecentConnection.Port);
        ApplyBrowserPreferredManualConnectDefaults();

        _hostSetupState.LoadFrom(_clientSettings.HostDefaults);
        ApplyBloodPresentationSettingsToWorld();
    }

    public void PersistClientSettings()
    {
        _clientSettings.PlayerName = _world.LocalPlayer.DisplayName;
        _clientSettings.DisplayMode = _menuManager.DisplaySettings.DisplayMode;
        if (!OperatingSystem.IsBrowser())
        {
            _clientSettings.VSync = _graphics.SynchronizeWithVerticalRetrace;
        }
        _clientSettings.IngameResolution = _menuManager.DisplaySettings.IngameResolution;
        _clientSettings.WindowSize = _menuManager.DisplaySettings.WindowSize;
        _clientSettings.DisplayScaleMode = _menuManager.DisplaySettings.DisplayScaleMode;
        _clientSettings.MusicMode = _audioManager.RuntimeSettings.MusicMode;
        _clientSettings.KillCamEnabled = _gameplayManager.RuntimeSettings.KillCamEnabled;
        _clientSettings.ParticleMode = Math.Clamp(_gameplayManager.RuntimeSettings.ParticleMode, 0, 2);
        _clientSettings.FlameRenderMode = Math.Clamp(_gameplayManager.RuntimeSettings.FlameRenderMode, 0, 1);
        _clientSettings.BloodRenderMode = Math.Clamp(_gameplayManager.RuntimeSettings.BloodRenderMode, 0, 1);
        _clientSettings.DynamicRagdollEnabled = _gameplayManager.RuntimeSettings.DynamicRagdollEnabled;
        _clientSettings.BurnCharredCorpsesEnabled = _gameplayManager.RuntimeSettings.BurnCharredCorpsesEnabled;
        _clientSettings.BloodPersistenceSeconds = Math.Clamp(_gameplayManager.RuntimeSettings.BloodPersistenceSeconds, 1, 120);
        _clientSettings.CorpseFadeMode = OpenGarrisonPreferencesDocument.NormalizeCorpseFadeMode(_gameplayManager.RuntimeSettings.CorpseFadeMode);
        _clientSettings.MenuBackgroundMode = _gameplayManager.RuntimeSettings.MenuBackgroundMode;
        _clientSettings.GibLevel = Math.Clamp(_gameplayManager.RuntimeSettings.GibLevel, 0, 3);
        _clientSettings.BloodAmountLevel = Math.Clamp(_gameplayManager.RuntimeSettings.BloodAmountLevel, 1, 5);
        _clientSettings.CorpseDurationMode = Math.Clamp(_gameplayManager.RuntimeSettings.CorpseDurationMode, ClientSettings.CorpseDurationDefault, ClientSettings.CorpseDurationInfinite);
        _clientSettings.HealerRadarEnabled = _hudManager.RuntimeSettings.HealerRadarEnabled;
        _clientSettings.ShowHealerEnabled = _hudManager.RuntimeSettings.ShowHealerEnabled;
        _clientSettings.ShowHealingEnabled = _hudManager.RuntimeSettings.ShowHealingEnabled;
        _clientSettings.ShowHealthBarEnabled = _hudManager.RuntimeSettings.ShowHealthBarEnabled;
        _clientSettings.ShowShieldBarEnabled = _hudManager.RuntimeSettings.ShowShieldBarEnabled;
        _clientSettings.HudShowOnlyActiveWeapon = _hudManager.RuntimeSettings.HudShowOnlyActiveWeapon;
        _clientSettings.OverheadChatEnabled = _hudManager.RuntimeSettings.OverheadChatEnabled;
        _clientSettings.PortraitRumbleEnabled = _hudManager.RuntimeSettings.PortraitRumbleEnabled;
        _clientSettings.PostGameMvpArtEnabled = _hudManager.RuntimeSettings.PostGameMvpArtEnabled;
        _clientSettings.DamageVignetteEnabled = _hudManager.RuntimeSettings.DamageVignetteEnabled;
        _clientSettings.DamageVignetteIntensityPercent = ClientSettings.NormalizeDamageVignetteIntensityPercent(_hudManager.RuntimeSettings.DamageVignetteIntensityPercent);
        _clientSettings.LowHealthColorMode = ClientSettings.NormalizeLowHealthColorMode(_hudManager.RuntimeSettings.LowHealthColorMode);
        _clientSettings.ShowPersistentSelfNameEnabled = _hudManager.RuntimeSettings.ShowPersistentSelfNameEnabled;
        _clientSettings.ShowPlayerNamesEnabled = _hudManager.RuntimeSettings.ShowPlayerNamesEnabled;
        _clientSettings.PositionSmoothingEnabled = _gameplayManager.RuntimeSettings.PositionSmoothingEnabled;
        _clientSettings.EnablePrediction = _gameplayManager.RuntimeSettings.EnablePrediction;
        _clientSettings.CameraPanningEnabled = _gameplayManager.RuntimeSettings.CameraPanningEnabled;
        _clientSettings.SmoothCameraMultiplier = NormalizeSmoothCameraMultiplier(_gameplayManager.RuntimeSettings.SmoothCameraMultiplier);
        _clientSettings.SpriteDropShadowEnabled = _gameplayManager.RuntimeSettings.SpriteDropShadowEnabled;
        _clientSettings.StuckArrowsEnabled = _gameplayManager.RuntimeSettings.StuckArrowsEnabled;
        _clientSettings.WeaponBobMode = OpenGarrisonPreferencesDocument.NormalizeWeaponBobMode(_gameplayManager.RuntimeSettings.WeaponBobMode);
        _clientSettings.PixelPerfectWeaponRotation = _gameplayManager.RuntimeSettings.PixelPerfectWeaponRotation;
        _clientSettings.UseLocalWeaponRotation = _gameplayManager.RuntimeSettings.UseLocalWeaponRotation;
        _clientSettings.PlayerCardSizeMode = ClientSettings.NormalizePlayerCardSizeMode(_hudManager.RuntimeSettings.PlayerCardSizeMode);
        _clientSettings.CursorSizePercent = ClientSettings.NormalizeCursorSizePercent(_hudManager.RuntimeSettings.CursorSizePercent);
        _clientSettings.ShowUberOutlinesEnabled = _gameplayManager.RuntimeSettings.ShowUberOutlinesEnabled;
        _clientSettings.ProjectileTeamTintEnabled = _gameplayManager.RuntimeSettings.ProjectileTeamTintEnabled;
        _clientSettings.AudioMuted = _audioManager.RuntimeSettings.AudioMuted;
        _clientSettings.MasterVolumePercent = _audioManager.RuntimeSettings.MasterVolumePercent;
        _clientSettings.MenuMusicVolumePercent = _audioManager.RuntimeSettings.MenuMusicVolumePercent;
        _clientSettings.IngameMusicVolumePercent = _audioManager.RuntimeSettings.IngameMusicVolumePercent;
        _clientSettings.DynamicMusicEnabled = _audioManager.RuntimeSettings.DynamicMusicEnabled;
        _clientSettings.CombatMusicVolumePercent = Math.Clamp(_audioManager.RuntimeSettings.CombatMusicVolumePercent, 0, OpenGarrisonPreferencesDocument.MaxCombatMusicVolumePercent);
        _clientSettings.SoundEffectsVolumePercent = _audioManager.RuntimeSettings.SoundEffectsVolumePercent;
        _clientSettings.FrameRateLimit = _menuManager.DisplaySettings.FrameRateLimit;
        _clientSettings.RecentConnection.Host = SanitizeHost(_inputManager.MenuTextInput.ConnectHostEdit.Text);
        _clientSettings.RecentConnection.Port = ParsePortOrDefault(_inputManager.MenuTextInput.ConnectPortEdit.Text, OpenGarrisonPreferencesDocument.DefaultServerPort);
        _hostSetupState.ApplyTo(_clientSettings);

        _clientSettings.Save();
    }

    private static int NormalizeFrameRateLimit(int frameRateLimit)
    {
        return frameRateLimit switch
        {
            0 => 0,
            30 => 30,
            60 => 60,
            75 => 75,
            120 => 120,
            _ => 0,
        };
    }

    public void PersistInputBindings()
    {
        _inputBindings.Save();
        _voiceSettings.PushToTalkBinding = InputBindingsSettings.FormatBinding(_inputBindings.PushToTalk);
        SaveVoiceSettings();
    }

    public void SetLocalPlayerNameFromSettings(string playerName)
    {
        _world.SetLocalPlayerName(playerName);
        _playerNameEditBuffer = _world.LocalPlayer.DisplayName;
        _networkClient.UpdatePlayerProfile(_world.LocalPlayer.DisplayName, _world.LocalPlayer.BadgeMask);
        PersistClientSettings();
    }

    public void RecordRecentConnection(string host, int port)
    {
        _sessionManager.Connection.RememberRecentConnection(host, port);
        _inputManager.MenuTextInput.ConnectHostEdit.Text = host;
        _inputManager.MenuTextInput.ConnectPortEdit.Text = port.ToString(CultureInfo.InvariantCulture);
        PersistClientSettings();
    }

    private void ApplyBrowserPreferredManualConnectDefaults()
    {
        if (!OperatingSystem.IsBrowser())
        {
            return;
        }

        var browserBaseAddress = ClientShared.ClientRuntimeBootstrap.GetBrowserBaseAddress();
        if (browserBaseAddress is null
            || !string.Equals(browserBaseAddress.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (ShouldUseBrowserPreferredManualConnectHost(_inputManager.MenuTextInput.ConnectHostEdit.Text))
        {
            _inputManager.MenuTextInput.ConnectHostEdit.Text = DefaultBrowserSecureManualConnectHost;
        }

        if (TryParseExplicitBrowserWebSocketUri(_inputManager.MenuTextInput.ConnectHostEdit.Text, out _)
            && (!int.TryParse(_inputManager.MenuTextInput.ConnectPortEdit.Text, out var port) || port is <= 0 or > 65535))
        {
            _inputManager.MenuTextInput.ConnectPortEdit.Text = DefaultBrowserSecureManualConnectPort.ToString(CultureInfo.InvariantCulture);
        }
    }

    private static bool ShouldUseBrowserPreferredManualConnectHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return true;
        }

        var trimmed = host.Trim();
        if (string.Equals(trimmed, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static bool TryParseExplicitBrowserWebSocketUri(string value, out Uri uri)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out uri!)
            && (uri.Scheme == "ws" || uri.Scheme == "wss")
            && !string.IsNullOrWhiteSpace(uri.Host))
        {
            return true;
        }

        uri = null!;
        return false;
    }

    private static string SanitizeHost(string? host)
    {
        if (!string.IsNullOrWhiteSpace(host))
        {
            return host.Trim();
        }

        return OperatingSystem.IsBrowser()
            ? DefaultBrowserSecureManualConnectHost
            : "127.0.0.1";
    }

    private static string SanitizeServerName(string? serverName)
    {
        return string.IsNullOrWhiteSpace(serverName) ? "My Server" : serverName.Trim();
    }

    private static string SanitizePort(int port)
    {
        if (OperatingSystem.IsBrowser() && port <= 0)
        {
            return DefaultBrowserSecureManualConnectPort.ToString(CultureInfo.InvariantCulture);
        }

        return Math.Clamp(port, 1, 65535).ToString(CultureInfo.InvariantCulture);
    }

    private static int ParsePortOrDefault(string? portText, int fallback)
    {
        return int.TryParse(portText, out var port) && port is > 0 and <= 65535
            ? port
            : fallback;
    }

    private static int ParseClampedInt(string? valueText, int fallback, int min, int max)
    {
        return int.TryParse(valueText, out var parsed)
            ? Math.Clamp(parsed, min, max)
            : fallback;
    }

    private const int RegularCorpseFadeTicks = 20;
    /// <summary>Slower top-down melt so the front reads as eating across the corpse, not a quick wipe.</summary>
    private const int AcidCorpseFadeTicks = 120;
    private const int CorpseFadeModeAcid = 1;

    public bool AreBloodVisualsEnabled => _gameplayManager.RuntimeSettings.GibLevel > 0;

    public float GetBloodAmountScale() => Math.Clamp(_gameplayManager.RuntimeSettings.BloodAmountLevel, 1, 5) / 5f;

    internal void ApplyBloodPresentationSettingsToWorld()
    {
        _world.LocalBloodLifetimeSeconds = Math.Clamp(_gameplayManager.RuntimeSettings.BloodPersistenceSeconds, 1, 120);
    }

    internal int GetCorpseFadeTicks()
        => _gameplayManager.RuntimeSettings.CorpseFadeMode == CorpseFadeModeAcid ? AcidCorpseFadeTicks : RegularCorpseFadeTicks;

    /// <summary>
    /// Infinite corpses never fade. Default corpses use the selected Regular/Dissolve corpse fade.
    /// </summary>
    internal bool IsCorpseFading(int ticksRemaining)
    {
        if (_gameplayManager.RuntimeSettings.CorpseDurationMode == ClientSettings.CorpseDurationInfinite || ticksRemaining <= 0)
        {
            return false;
        }

        return ticksRemaining < GetCorpseFadeTicks();
    }

    internal bool IsCorpseAcidFading(int ticksRemaining)
        => _gameplayManager.RuntimeSettings.CorpseFadeMode == CorpseFadeModeAcid && IsCorpseFading(ticksRemaining);

    internal float GetCorpseFadeAlpha(int ticksRemaining)
    {
        if (_gameplayManager.RuntimeSettings.CorpseDurationMode == ClientSettings.CorpseDurationInfinite || ticksRemaining <= 0)
        {
            return _gameplayManager.RuntimeSettings.CorpseDurationMode == ClientSettings.CorpseDurationInfinite ? 1f : 0f;
        }

        if (_gameplayManager.RuntimeSettings.CorpseFadeMode == CorpseFadeModeAcid)
        {
            // Acid dissolve owns disappearance — keep full opacity while melting.
            return 1f;
        }

        var fadeTicks = GetCorpseFadeTicks();
        return ticksRemaining >= fadeTicks
            ? 1f
            : MathF.Max(0f, ticksRemaining / (float)fadeTicks);
    }

    internal float GetCorpseFadeProgress(int ticksRemaining)
    {
        if (_gameplayManager.RuntimeSettings.CorpseDurationMode == ClientSettings.CorpseDurationInfinite)
        {
            return 0f;
        }

        var fadeTicks = GetCorpseFadeTicks();
        if (ticksRemaining <= 0)
        {
            return 1f;
        }

        if (ticksRemaining >= fadeTicks)
        {
            return 0f;
        }

        return 1f - (ticksRemaining / (float)fadeTicks);
    }

    internal float GetBloodPersistenceScale()
        => Math.Clamp(_gameplayManager.RuntimeSettings.BloodPersistenceSeconds, 1, 120)
            / (float)OpenGarrisonPreferencesDocument.DefaultBloodPersistenceSeconds;

    internal int ScaleBloodVisualCount(int maximumCount)
    {
        if (!AreBloodVisualsEnabled || maximumCount <= 0)
        {
            return 0;
        }

        // Squib mode uses 2x the amount curve so 100% is twice as dense.
        var amountScale = GetBloodAmountScale() * (_gameplayManager.RuntimeSettings.BloodRenderMode == 0 ? 2f : 1f);
        return Math.Max(0, (int)MathF.Round(maximumCount * amountScale));
    }

}
