#nullable enable

using System;
using System.Globalization;
using System.Linq;
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
        _healerRadarEnabled = _clientSettings.HealerRadarEnabled;
        _showHealerEnabled = _clientSettings.ShowHealerEnabled;
        _showHealingEnabled = _clientSettings.ShowHealingEnabled;
        _showHealthBarEnabled = _clientSettings.ShowHealthBarEnabled;

        _world.SetLocalPlayerName(_clientSettings.PlayerName);
        _playerNameEditBuffer = _world.LocalPlayer.DisplayName;

        _connectHostBuffer = SanitizeHost(_clientSettings.RecentConnection.Host);
        _connectPortBuffer = SanitizePort(_clientSettings.RecentConnection.Port);

        _hostServerNameBuffer = SanitizeServerName(_clientSettings.HostDefaults.ServerName);
        _hostPortBuffer = SanitizePort(_clientSettings.HostDefaults.Port);
        _hostSlotsBuffer = Math.Clamp(_clientSettings.HostDefaults.Slots, 1, SimulationWorld.MaxPlayableNetworkPlayers).ToString(CultureInfo.InvariantCulture);
        _hostPasswordBuffer = _clientSettings.HostDefaults.Password ?? string.Empty;
        _hostMapRotationFileBuffer = _clientSettings.HostDefaults.MapRotationFile ?? string.Empty;
        _hostTimeLimitBuffer = Math.Clamp(_clientSettings.HostDefaults.TimeLimitMinutes, 1, 255).ToString(CultureInfo.InvariantCulture);
        _hostCapLimitBuffer = Math.Clamp(_clientSettings.HostDefaults.CapLimit, 1, 255).ToString(CultureInfo.InvariantCulture);
        _hostRespawnSecondsBuffer = Math.Clamp(_clientSettings.HostDefaults.RespawnSeconds, 0, 255).ToString(CultureInfo.InvariantCulture);
        _hostLobbyAnnounceEnabled = _clientSettings.HostDefaults.LobbyAnnounceEnabled;
        _hostAutoBalanceEnabled = _clientSettings.HostDefaults.AutoBalanceEnabled;
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
        _clientSettings.HealerRadarEnabled = _healerRadarEnabled;
        _clientSettings.ShowHealerEnabled = _showHealerEnabled;
        _clientSettings.ShowHealingEnabled = _showHealingEnabled;
        _clientSettings.ShowHealthBarEnabled = _showHealthBarEnabled;
        _clientSettings.RecentConnection.Host = SanitizeHost(_connectHostBuffer);
        _clientSettings.RecentConnection.Port = ParsePortOrDefault(_connectPortBuffer, 8190);
        _clientSettings.HostDefaults.ServerName = SanitizeServerName(_hostServerNameBuffer);
        _clientSettings.HostDefaults.Port = ParsePortOrDefault(_hostPortBuffer, 8190);
        _clientSettings.HostDefaults.Slots = ParseClampedInt(_hostSlotsBuffer, 10, 1, SimulationWorld.MaxPlayableNetworkPlayers);
        _clientSettings.HostDefaults.Password = _hostPasswordBuffer.Trim();
        _clientSettings.HostDefaults.MapRotationFile = _hostMapRotationFileBuffer.Trim();
        _clientSettings.HostDefaults.TimeLimitMinutes = ParseClampedInt(_hostTimeLimitBuffer, 15, 1, 255);
        _clientSettings.HostDefaults.CapLimit = ParseClampedInt(_hostCapLimitBuffer, 5, 1, 255);
        _clientSettings.HostDefaults.RespawnSeconds = ParseClampedInt(_hostRespawnSecondsBuffer, 5, 0, 255);
        _clientSettings.HostDefaults.LobbyAnnounceEnabled = _hostLobbyAnnounceEnabled;
        _clientSettings.HostDefaults.AutoBalanceEnabled = _hostAutoBalanceEnabled;
        if (_hostMapEntries.Count > 0)
        {
            _clientSettings.HostDefaults.StockMapRotation = _hostMapEntries
                .Select(entry => entry.Clone())
                .ToList();
        }

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
