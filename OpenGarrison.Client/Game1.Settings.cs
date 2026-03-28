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
        _graphics.SynchronizeWithVerticalRetrace = _clientSettings.VSync;
        ApplyPreferredBackBufferSize(_graphics.IsFullScreen, _ingameResolution);
        _graphics.ApplyChanges();
        _musicMode = _clientSettings.MusicMode;
        _killCamEnabled = _clientSettings.KillCamEnabled;
        _particleMode = Math.Clamp(_clientSettings.ParticleMode, 0, 2);
        _gibLevel = Math.Clamp(_clientSettings.GibLevel, 0, 3);
        _corpseDurationMode = Math.Clamp(_clientSettings.CorpseDurationMode, ClientSettings.CorpseDurationDefault, ClientSettings.CorpseDurationInfinite);
        _healerRadarEnabled = _clientSettings.HealerRadarEnabled;
        _showHealerEnabled = _clientSettings.ShowHealerEnabled;
        _showHealingEnabled = _clientSettings.ShowHealingEnabled;
        _showHealthBarEnabled = _clientSettings.ShowHealthBarEnabled;

        _world.SetLocalPlayerName(_clientSettings.PlayerName);
        _playerNameEditBuffer = _world.LocalPlayer.DisplayName;

        _connectHostBuffer = SanitizeHost(_clientSettings.RecentConnection.Host);
        _connectPortBuffer = SanitizePort(_clientSettings.RecentConnection.Port);

        _hostSetupState.LoadFrom(_clientSettings.HostDefaults);
    }

    private void PersistClientSettings()
    {
        _clientSettings.PlayerName = _world.LocalPlayer.DisplayName;
        _clientSettings.Fullscreen = _graphics.IsFullScreen;
        _clientSettings.VSync = _graphics.SynchronizeWithVerticalRetrace;
        _clientSettings.IngameResolution = _ingameResolution;
        _clientSettings.MusicMode = _musicMode;
        _clientSettings.KillCamEnabled = _killCamEnabled;
        _clientSettings.ParticleMode = Math.Clamp(_particleMode, 0, 2);
        _clientSettings.GibLevel = Math.Clamp(_gibLevel, 0, 3);
        _clientSettings.CorpseDurationMode = Math.Clamp(_corpseDurationMode, ClientSettings.CorpseDurationDefault, ClientSettings.CorpseDurationInfinite);
        _clientSettings.HealerRadarEnabled = _healerRadarEnabled;
        _clientSettings.ShowHealerEnabled = _showHealerEnabled;
        _clientSettings.ShowHealingEnabled = _showHealingEnabled;
        _clientSettings.ShowHealthBarEnabled = _showHealthBarEnabled;
        _clientSettings.RecentConnection.Host = SanitizeHost(_connectHostBuffer);
        _clientSettings.RecentConnection.Port = ParsePortOrDefault(_connectPortBuffer, 8190);
        _hostSetupState.ApplyTo(_clientSettings);

        _clientSettings.Save();
    }

    private void PersistInputBindings()
    {
        _inputBindings.Save();
    }

    private void SetLocalPlayerNameFromSettings(string playerName)
    {
        _world.SetLocalPlayerName(playerName);
        _playerNameEditBuffer = _world.LocalPlayer.DisplayName;
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
