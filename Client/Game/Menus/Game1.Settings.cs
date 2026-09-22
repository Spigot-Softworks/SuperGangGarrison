#nullable enable

using System;
using System.Globalization;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private readonly ClientSettings _clientSettings;
    private readonly InputBindingsSettings _inputBindings;
    private const string DefaultBrowserSecureManualConnectHost = "wss://45-61-52-208.sslip.io/opengarrison/ws";
    private const int DefaultBrowserSecureManualConnectPort = 443;

    private void ApplyLoadedSettings()
    {
        InitializeVoiceSettings();
        ApplyGraphicsSettings(persist: false);

        _musicMode = _clientSettings.MusicMode;
        ApplyConfiguredPracticeBotController(respawnActiveBots: false);
        _frameRateLimit = NormalizeFrameRateLimit(_clientSettings.FrameRateLimit);
        _killCamEnabled = _clientSettings.KillCamEnabled;
        _particleMode = Math.Clamp(_clientSettings.ParticleMode, 0, 2);
        _flameRenderMode = Math.Clamp(_clientSettings.FlameRenderMode, 0, 1);
        _bloodRenderMode = Math.Clamp(_clientSettings.BloodRenderMode, 0, 1);
        _menuBackgroundMode = _clientSettings.MenuBackgroundMode;
        _gibLevel = Math.Clamp(_clientSettings.GibLevel, 0, 3);
        _bloodAmountLevel = Math.Clamp(_clientSettings.BloodAmountLevel, 1, 5);
        _gibAmountLevel = Math.Clamp(_clientSettings.GibAmountLevel, 1, 5);
        _corpseDurationMode = Math.Clamp(_clientSettings.CorpseDurationMode, ClientSettings.CorpseDurationDefault, ClientSettings.CorpseDurationInfinite);
        _healerRadarEnabled = _clientSettings.HealerRadarEnabled;
        _showHealerEnabled = _clientSettings.ShowHealerEnabled;
        _showHealingEnabled = _clientSettings.ShowHealingEnabled;
        _showHealthBarEnabled = _clientSettings.ShowHealthBarEnabled;
        _showShieldBarEnabled = _clientSettings.ShowShieldBarEnabled;
        _hudShowOnlyActiveWeapon = _clientSettings.HudShowOnlyActiveWeapon;
        _overheadChatEnabled = _clientSettings.OverheadChatEnabled;
        ApplyLoadedBubbleWheelPluginSettings();
        _portraitRumbleEnabled = _clientSettings.PortraitRumbleEnabled;
        _postGameMvpArtEnabled = _clientSettings.PostGameMvpArtEnabled;
        _damageVignetteEnabled = _clientSettings.DamageVignetteEnabled;
        _damageVignetteIntensityPercent = ClientSettings.NormalizeDamageVignetteIntensityPercent(_clientSettings.DamageVignetteIntensityPercent);
        _lowHealthColorMode = ClientSettings.NormalizeLowHealthColorMode(_clientSettings.LowHealthColorMode);
        _showPersistentSelfNameEnabled = _clientSettings.ShowPersistentSelfNameEnabled;
        _showPlayerNamesEnabled = _clientSettings.ShowPlayerNamesEnabled;
        _positionSmoothingEnabled = _clientSettings.PositionSmoothingEnabled;
        _enablePrediction = _clientSettings.EnablePrediction;
        _cameraPanningEnabled = _clientSettings.CameraPanningEnabled;
        _spriteStyle = OpenGarrisonPreferencesDocument.NormalizeSpriteStyle(_clientSettings.SpriteStyle);
        _smoothCameraMultiplier = NormalizeSmoothCameraMultiplier(_clientSettings.SmoothCameraMultiplier);
        if (_smoothCameraMultiplier <= 0f)
        {
            _hasSmoothCamera = false;
        }

        _spriteDropShadowEnabled = _clientSettings.SpriteDropShadowEnabled;
        _stuckArrowsEnabled = _clientSettings.StuckArrowsEnabled;
        _pixelPerfectWeaponRotation = _clientSettings.PixelPerfectWeaponRotation;
        _useLocalWeaponRotation = _clientSettings.UseLocalWeaponRotation;
        _playerCardSizeMode = ClientSettings.NormalizePlayerCardSizeMode(_clientSettings.PlayerCardSizeMode);
        _cursorSizePercent = ClientSettings.NormalizeCursorSizePercent(_clientSettings.CursorSizePercent);
        _uberOutlineEnabled = _clientSettings.ShowUberOutlinesEnabled;
        _projectileTeamTintEnabled = _clientSettings.ProjectileTeamTintEnabled;
        _audioMuted = _clientSettings.AudioMuted;
        _masterVolumePercent = Math.Clamp(_clientSettings.MasterVolumePercent, 0, 100);
        _menuMusicVolumePercent = Math.Clamp(_clientSettings.MenuMusicVolumePercent, 0, 100);
        _ingameMusicVolumePercent = Math.Clamp(_clientSettings.IngameMusicVolumePercent, 0, 100);
        _dynamicMusicEnabled = _clientSettings.DynamicMusicEnabled;
        _combatMusicVolumePercent = Math.Clamp(_clientSettings.CombatMusicVolumePercent, 0, OpenGarrisonPreferencesDocument.MaxCombatMusicVolumePercent);
        _soundEffectsVolumePercent = Math.Clamp(_clientSettings.SoundEffectsVolumePercent, 0, 100);
        ApplyAudioVolumeState();

        _world.SetLocalPlayerName(_clientSettings.PlayerName);
        _world.SetLocalPlayerBadgeMask(BadgeCatalog.ParseRewardString(_clientSettings.Rewards));
        _playerNameEditBuffer = _world.LocalPlayer.DisplayName;

        _connectHostBuffer = SanitizeHost(_clientSettings.RecentConnection.Host);
        _connectPortBuffer = SanitizePort(_clientSettings.RecentConnection.Port);
        ApplyBrowserPreferredManualConnectDefaults();

        _hostSetupState.LoadFrom(_clientSettings.HostDefaults);
    }

    private void PersistClientSettings()
    {
        _clientSettings.PlayerName = _world.LocalPlayer.DisplayName;
        _clientSettings.DisplayMode = _displayMode;
        if (!OperatingSystem.IsBrowser())
        {
            _clientSettings.VSync = _graphics.SynchronizeWithVerticalRetrace;
        }
        _clientSettings.IngameResolution = _ingameResolution;
        _clientSettings.WindowSize = _windowSize;
        _clientSettings.DisplayScaleMode = _displayScaleMode;
        _clientSettings.MusicMode = _musicMode;
        _clientSettings.KillCamEnabled = _killCamEnabled;
        _clientSettings.ParticleMode = Math.Clamp(_particleMode, 0, 2);
        _clientSettings.FlameRenderMode = Math.Clamp(_flameRenderMode, 0, 1);
        _clientSettings.BloodRenderMode = Math.Clamp(_bloodRenderMode, 0, 1);
        _clientSettings.MenuBackgroundMode = _menuBackgroundMode;
        _clientSettings.GibLevel = Math.Clamp(_gibLevel, 0, 3);
        _clientSettings.BloodAmountLevel = Math.Clamp(_bloodAmountLevel, 1, 5);
        _clientSettings.GibAmountLevel = Math.Clamp(_gibAmountLevel, 1, 5);
        _clientSettings.CorpseDurationMode = Math.Clamp(_corpseDurationMode, ClientSettings.CorpseDurationDefault, ClientSettings.CorpseDurationInfinite);
        _clientSettings.HealerRadarEnabled = _healerRadarEnabled;
        _clientSettings.ShowHealerEnabled = _showHealerEnabled;
        _clientSettings.ShowHealingEnabled = _showHealingEnabled;
        _clientSettings.ShowHealthBarEnabled = _showHealthBarEnabled;
        _clientSettings.ShowShieldBarEnabled = _showShieldBarEnabled;
        _clientSettings.HudShowOnlyActiveWeapon = _hudShowOnlyActiveWeapon;
        _clientSettings.OverheadChatEnabled = _overheadChatEnabled;
        _clientSettings.PortraitRumbleEnabled = _portraitRumbleEnabled;
        _clientSettings.PostGameMvpArtEnabled = _postGameMvpArtEnabled;
        _clientSettings.DamageVignetteEnabled = _damageVignetteEnabled;
        _clientSettings.DamageVignetteIntensityPercent = ClientSettings.NormalizeDamageVignetteIntensityPercent(_damageVignetteIntensityPercent);
        _clientSettings.LowHealthColorMode = ClientSettings.NormalizeLowHealthColorMode(_lowHealthColorMode);
        _clientSettings.ShowPersistentSelfNameEnabled = _showPersistentSelfNameEnabled;
        _clientSettings.ShowPlayerNamesEnabled = _showPlayerNamesEnabled;
        _clientSettings.PositionSmoothingEnabled = _positionSmoothingEnabled;
        _clientSettings.EnablePrediction = _enablePrediction;
        _clientSettings.CameraPanningEnabled = _cameraPanningEnabled;
        _clientSettings.SpriteStyle = _spriteStyle;
        _clientSettings.SmoothCameraMultiplier = NormalizeSmoothCameraMultiplier(_smoothCameraMultiplier);
        _clientSettings.SpriteDropShadowEnabled = _spriteDropShadowEnabled;
        _clientSettings.StuckArrowsEnabled = _stuckArrowsEnabled;
        _clientSettings.PixelPerfectWeaponRotation = _pixelPerfectWeaponRotation;
        _clientSettings.UseLocalWeaponRotation = _useLocalWeaponRotation;
        _clientSettings.PlayerCardSizeMode = ClientSettings.NormalizePlayerCardSizeMode(_playerCardSizeMode);
        _clientSettings.CursorSizePercent = ClientSettings.NormalizeCursorSizePercent(_cursorSizePercent);
        _clientSettings.ShowUberOutlinesEnabled = _uberOutlineEnabled;
        _clientSettings.ProjectileTeamTintEnabled = _projectileTeamTintEnabled;
        _clientSettings.AudioMuted = _audioMuted;
        _clientSettings.MasterVolumePercent = _masterVolumePercent;
        _clientSettings.MenuMusicVolumePercent = _menuMusicVolumePercent;
        _clientSettings.IngameMusicVolumePercent = _ingameMusicVolumePercent;
        _clientSettings.DynamicMusicEnabled = _dynamicMusicEnabled;
        _clientSettings.CombatMusicVolumePercent = Math.Clamp(_combatMusicVolumePercent, 0, OpenGarrisonPreferencesDocument.MaxCombatMusicVolumePercent);
        _clientSettings.SoundEffectsVolumePercent = _soundEffectsVolumePercent;
        _clientSettings.FrameRateLimit = _frameRateLimit;
        _clientSettings.RecentConnection.Host = SanitizeHost(_connectHostBuffer);
        _clientSettings.RecentConnection.Port = ParsePortOrDefault(_connectPortBuffer, 8190);
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

    private void PersistInputBindings()
    {
        _inputBindings.Save();
        _voiceSettings.PushToTalkBinding = InputBindingsSettings.FormatBinding(_inputBindings.PushToTalk);
        SaveVoiceSettings();
    }

    private void SetLocalPlayerNameFromSettings(string playerName)
    {
        _world.SetLocalPlayerName(playerName);
        _playerNameEditBuffer = _world.LocalPlayer.DisplayName;
        _networkClient.UpdatePlayerProfile(_world.LocalPlayer.DisplayName, _world.LocalPlayer.BadgeMask);
        PersistClientSettings();
    }

    private void RecordRecentConnection(string host, int port)
    {
        _recentConnectHost = host;
        _recentConnectPort = port;
        _connectHostBuffer = host;
        _connectPortBuffer = port.ToString(CultureInfo.InvariantCulture);
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

        if (ShouldUseBrowserPreferredManualConnectHost(_connectHostBuffer))
        {
            _connectHostBuffer = DefaultBrowserSecureManualConnectHost;
        }

        if (TryParseExplicitBrowserWebSocketUri(_connectHostBuffer, out _)
            && (!int.TryParse(_connectPortBuffer, out var port) || port is <= 0 or > 65535))
        {
            _connectPortBuffer = DefaultBrowserSecureManualConnectPort.ToString(CultureInfo.InvariantCulture);
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

    // Gore mode: 0 none, 1 blood only, 2 gibs only, 3 blood + gibs.
    internal bool AreBloodVisualsEnabled => _gibLevel is 1 or 3;

    internal bool AreGibVisualsEnabled => _gibLevel is 2 or 3;

    internal float GetBloodAmountScale() => Math.Clamp(_bloodAmountLevel, 1, 5) / 5f;

    internal float GetGibAmountScale() => Math.Clamp(_gibAmountLevel, 1, 5) / 5f;

    internal int ScaleBloodVisualCount(int maximumCount)
    {
        if (!AreBloodVisualsEnabled || maximumCount <= 0)
        {
            return 0;
        }

        // Squib mode uses 2x the amount curve so 100% is twice as dense.
        var amountScale = GetBloodAmountScale() * (_bloodRenderMode == 0 ? 2f : 1f);
        return Math.Max(0, (int)MathF.Round(maximumCount * amountScale));
    }

    internal int ScaleGibVisualCount(int maximumCount)
    {
        if (!AreGibVisualsEnabled || maximumCount <= 0)
        {
            return 0;
        }

        return Math.Max(1, (int)MathF.Round(maximumCount * GetGibAmountScale()));
    }

    internal bool ShouldDrawPlayerGib(int gibId)
    {
        if (!AreGibVisualsEnabled)
        {
            return false;
        }

        var scale = GetGibAmountScale();
        if (scale >= 0.999f)
        {
            return true;
        }

        // Stable per-gib fraction so amount changes don't reshuffle every frame.
        unchecked
        {
            var hash = (uint)gibId * 2654435761u;
            return (hash % 1000u) < (uint)MathF.Round(scale * 1000f);
        }
    }
}
