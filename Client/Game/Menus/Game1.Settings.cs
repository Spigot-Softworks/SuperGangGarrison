#nullable enable

using System;
using System.Globalization;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private readonly ClientSettings _clientSettings;
    private readonly InputBindingsSettings _inputBindings;

    private void ApplyLoadedSettings()
    {
        ApplyIngameResolution(_clientSettings.IngameResolution);
        _graphics.IsFullScreen = _clientSettings.Fullscreen;
        _graphics.SynchronizeWithVerticalRetrace = !OperatingSystem.IsBrowser() && _clientSettings.VSync;
        ApplyPreferredBackBufferSize(_graphics.IsFullScreen, _ingameResolution);
        _graphics.ApplyChanges();
        _musicMode = _clientSettings.MusicMode;
        _frameRateLimit = NormalizeFrameRateLimit(_clientSettings.FrameRateLimit);
        _killCamEnabled = _clientSettings.KillCamEnabled;
        _particleMode = Math.Clamp(_clientSettings.ParticleMode, 0, 2);
        _flameRenderMode = Math.Clamp(_clientSettings.FlameRenderMode, 0, 1);
        _gibLevel = Math.Clamp(_clientSettings.GibLevel, 0, 3);
        _corpseDurationMode = Math.Clamp(_clientSettings.CorpseDurationMode, ClientSettings.CorpseDurationDefault, ClientSettings.CorpseDurationInfinite);
        _healerRadarEnabled = _clientSettings.HealerRadarEnabled;
        _showHealerEnabled = _clientSettings.ShowHealerEnabled;
        _showHealingEnabled = _clientSettings.ShowHealingEnabled;
        _showHealthBarEnabled = _clientSettings.ShowHealthBarEnabled;
        _showPersistentSelfNameEnabled = _clientSettings.ShowPersistentSelfNameEnabled;
        _positionSmoothingEnabled = _clientSettings.PositionSmoothingEnabled;
        _spriteDropShadowEnabled = _clientSettings.SpriteDropShadowEnabled;
        _uberOutlineEnabled = _clientSettings.ShowUberOutlinesEnabled;
        _projectileTeamTintEnabled = _clientSettings.ProjectileTeamTintEnabled;
        _audioMuted = _clientSettings.AudioMuted;
        _menuMusicVolumePercent = Math.Clamp(_clientSettings.MenuMusicVolumePercent, 0, 100);
        _ingameMusicVolumePercent = Math.Clamp(_clientSettings.IngameMusicVolumePercent, 0, 100);
        _soundEffectsVolumePercent = Math.Clamp(_clientSettings.SoundEffectsVolumePercent, 0, 100);
        ApplyAudioVolumeState();

        _world.SetLocalPlayerName(_clientSettings.PlayerName);
        _world.SetLocalPlayerBadgeMask(BadgeCatalog.ParseRewardString(_clientSettings.Rewards));
        _playerNameEditBuffer = _world.LocalPlayer.DisplayName;

        _connectHostBuffer = SanitizeHost(_clientSettings.RecentConnection.Host);
        _connectPortBuffer = SanitizePort(_clientSettings.RecentConnection.Port);

        _hostSetupState.LoadFrom(_clientSettings.HostDefaults);
    }

    private void PersistClientSettings()
    {
        _clientSettings.PlayerName = _world.LocalPlayer.DisplayName;
        _clientSettings.Fullscreen = _graphics.IsFullScreen;
        if (!OperatingSystem.IsBrowser())
        {
            _clientSettings.VSync = _graphics.SynchronizeWithVerticalRetrace;
        }
        _clientSettings.IngameResolution = _ingameResolution;
        _clientSettings.MusicMode = _musicMode;
        _clientSettings.KillCamEnabled = _killCamEnabled;
        _clientSettings.ParticleMode = Math.Clamp(_particleMode, 0, 2);
        _clientSettings.FlameRenderMode = Math.Clamp(_flameRenderMode, 0, 1);
        _clientSettings.GibLevel = Math.Clamp(_gibLevel, 0, 3);
        _clientSettings.CorpseDurationMode = Math.Clamp(_corpseDurationMode, ClientSettings.CorpseDurationDefault, ClientSettings.CorpseDurationInfinite);
        _clientSettings.HealerRadarEnabled = _healerRadarEnabled;
        _clientSettings.ShowHealerEnabled = _showHealerEnabled;
        _clientSettings.ShowHealingEnabled = _showHealingEnabled;
        _clientSettings.ShowHealthBarEnabled = _showHealthBarEnabled;
        _clientSettings.ShowPersistentSelfNameEnabled = _showPersistentSelfNameEnabled;
        _clientSettings.PositionSmoothingEnabled = _positionSmoothingEnabled;
        _clientSettings.SpriteDropShadowEnabled = _spriteDropShadowEnabled;
        _clientSettings.ShowUberOutlinesEnabled = _uberOutlineEnabled;
        _clientSettings.ProjectileTeamTintEnabled = _projectileTeamTintEnabled;
        _clientSettings.AudioMuted = _audioMuted;
        _clientSettings.MenuMusicVolumePercent = _menuMusicVolumePercent;
        _clientSettings.IngameMusicVolumePercent = _ingameMusicVolumePercent;
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

    private static string SanitizeHost(string? host)
    {
        return string.IsNullOrWhiteSpace(host) ? "127.0.0.1" : host.Trim();
    }

    private static string SanitizeServerName(string? serverName)
    {
        return string.IsNullOrWhiteSpace(serverName) ? "My Server" : serverName.Trim();
    }

    private static string SanitizePort(int port)
    {
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
}
