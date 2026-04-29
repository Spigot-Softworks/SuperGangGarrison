#nullable enable

using System;
using System.Collections.Generic;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class ConnectionFlowController
    {
        private readonly Game1 _game;

        public ConnectionFlowController(Game1 game)
        {
            _game = game;
        }

        public void OpenLobbyBrowser()
        {
            _game._lobbyBrowserOpen = true;
            _game._manualConnectOpen = false;
            _game._optionsMenuOpen = false;
            _game._pluginOptionsMenuOpen = false;
            _game._creditsOpen = false;
            _game._editingPlayerName = false;
            DisableManualConnectEditing();
            _game._lobbyBrowserSelectedIndex = -1;
            _game._lobbyBrowserHoverIndex = -1;
            RefreshLobbyBrowser();
        }

        public void CloseLobbyBrowser(bool clearStatus)
        {
            _game._lobbyBrowserOpen = false;
            _game._lobbyBrowserHoverIndex = -1;
            _game._lobbyBrowserRegistryRequestTask = null;
            _game.CloseLobbyBrowserLobbyClient();
            if (clearStatus)
            {
                _game._menuStatusMessage = string.Empty;
            }
        }

        public void RefreshLobbyBrowser()
        {
            _game._lobbyBrowserRegistryRequestTask = null;
            _game.CloseLobbyBrowserLobbyClient();
            if (!OperatingSystem.IsBrowser())
            {
                _game.EnsureLobbyBrowserClient();
            }

            _game._lobbyBrowserEntries.Clear();
            _game.StartLobbyBrowserRegistryRequest();

            foreach (var target in BuildLobbyBrowserTargets())
            {
                _game.AddLobbyBrowserEntry(target.DisplayName, target.Endpoint, isPrivate: false, isLobbyEntry: false);
            }

            _game._lobbyBrowserSelectedIndex = _game._lobbyBrowserEntries.Count > 0 ? 0 : -1;
            _game._menuStatusMessage = _game._lobbyBrowserEntries.Count > 0
                ? "Refreshing server list..."
                : "Contacting server registry...";
        }

        public void OpenManualConnectMenuFromLobbyBrowser()
        {
            CloseLobbyBrowser(clearStatus: false);
            _game._manualConnectOpen = true;
            SetManualConnectEditingField(editHost: true);
            _game._menuStatusMessage = string.Empty;
        }

        public void CloseManualConnectMenu(bool clearStatus)
        {
            _game._manualConnectOpen = false;
            DisableManualConnectEditing();
            if (clearStatus)
            {
                _game._menuStatusMessage = string.Empty;
            }
        }

        public void ToggleManualConnectEditingField()
        {
            SetManualConnectEditingField(!_game._editingConnectHost);
        }

        public void SetManualConnectEditingField(bool editHost)
        {
            _game._editingConnectHost = editHost;
            _game._editingConnectPort = !editHost;
        }

        public void DisableManualConnectEditing()
        {
            _game._editingConnectHost = false;
            _game._editingConnectPort = false;
        }

        public void TryConnectFromMenu()
        {
            if (!TryParseManualConnectTarget(out var endpoint))
            {
                return;
            }

            _game.TryConnectToServer(endpoint, addConsoleFeedback: false);
        }

        public bool TryParseManualConnectTarget(out NetworkEndpoint endpoint)
        {
            endpoint = default;
            if (TryParseManualConnectWebSocketEndpoint(out endpoint))
            {
                return true;
            }

            if (!TryParseManualConnectTarget(out var host, out var port))
            {
                return false;
            }

            endpoint = NetworkEndpoint.ForCurrentRuntimeSinglePort(host, port);
            return true;
        }

        public bool TryParseManualConnectTarget(out string host, out int port)
        {
            host = _game._connectHostBuffer.Trim();
            port = 0;

            if (string.IsNullOrWhiteSpace(host))
            {
                _game._menuStatusMessage = "Host is required.";
                return false;
            }

            if (OperatingSystem.IsBrowser()
                && TryParseExplicitWebSocketUri(host, out var explicitWebSocketUri))
            {
                host = explicitWebSocketUri.ToString();
                return true;
            }

            if (!int.TryParse(_game._connectPortBuffer.Trim(), out port) || port is <= 0 or > 65535)
            {
                _game._menuStatusMessage = "Port must be 1-65535.";
                return false;
            }

            return true;
        }

        public void JoinSelectedLobbyEntry()
        {
            if (!CanJoinSelectedLobbyEntry())
            {
                _game._menuStatusMessage = "Select an online server first.";
                return;
            }

            var entry = _game._lobbyBrowserEntries[_game._lobbyBrowserSelectedIndex];
            _game.TryConnectToServer(entry.Endpoint, addConsoleFeedback: false);
        }

        public bool CanJoinSelectedLobbyEntry()
        {
            return _game._lobbyBrowserSelectedIndex >= 0
                && _game._lobbyBrowserSelectedIndex < _game._lobbyBrowserEntries.Count
                && (_game._lobbyBrowserEntries[_game._lobbyBrowserSelectedIndex].HasResponse
                    || _game._lobbyBrowserEntries[_game._lobbyBrowserSelectedIndex].CanJoinDirectly);
        }

        public IEnumerable<LobbyBrowserTarget> BuildLobbyBrowserTargets()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var target in BuildDefaultLobbyTargets())
            {
                if (string.IsNullOrWhiteSpace(target.Endpoint.Host)
                    || (!target.Endpoint.HasUdpEndpoint && !target.Endpoint.HasWebSocketEndpoint))
                {
                    continue;
                }

                var key = $"{target.Endpoint.Host}:{target.Endpoint.UdpPort}:{target.Endpoint.WebSocketPort}:{target.Endpoint.WebSocketUrl}";
                if (seen.Add(key))
                {
                    yield return target;
                }
            }
        }

        public void OpenNetworkPasswordPrompt(string message)
        {
            _game._passwordPromptOpen = true;
            _game._passwordEditBuffer = string.Empty;
            _game._passwordPromptMessage = message;
            _game._consoleOpen = false;
            _game._inGameMenuOpen = false;
            _game._optionsMenuOpen = false;
            _game._pluginOptionsMenuOpen = false;
            _game._controlsMenuOpen = false;
            _game._teamSelectOpen = false;
            _game._classSelectOpen = false;
        }

        public void CloseNetworkPasswordPrompt()
        {
            _game._passwordPromptOpen = false;
            _game._passwordEditBuffer = string.Empty;
            _game._passwordPromptMessage = string.Empty;
        }

        public void EnterOnlineSpectatorState(string statusMessage)
        {
            _game.ResetSpectatorTracking(enableTracking: true);
            _game._teamSelectOpen = false;
            _game._classSelectOpen = false;
            _game._menuStatusMessage = statusMessage;
        }

        public void EnterOnlineClassSelectionState(string statusMessage)
        {
            _game.ResetSpectatorTracking(enableTracking: false);
            _game._teamSelectOpen = false;
            _game._classSelectOpen = true;
            _game._menuStatusMessage = statusMessage;
        }

        public void OpenOnlineTeamSelection(bool clearPendingSelections, string statusMessage)
        {
            if (clearPendingSelections)
            {
                _game._networkClient.ClearPendingTeamSelection();
                _game._networkClient.ClearPendingClassSelection();
            }

            _game._teamSelectOpen = true;
            _game._classSelectOpen = false;
            _game.ResetChatInputState();
            _game._consoleOpen = false;
            _game._menuStatusMessage = statusMessage;
        }

        public void ShowAutoBalanceNotice(string text, int seconds)
        {
            _game._autoBalanceNoticeText = text;
            _game._autoBalanceNoticeTicks = Math.Max(1, seconds * _game._config.TicksPerSecond);
        }

        private static int TryParseBrowserPort(string text)
        {
            return int.TryParse(text.Trim(), out var port) && port is > 0 and <= 65535 ? port : 0;
        }

        private IEnumerable<LobbyBrowserTarget> BuildDefaultLobbyTargets()
        {
            if (TryCreateManualConnectEndpoint("127.0.0.1", 8190, out var localhostEndpoint))
            {
                yield return new LobbyBrowserTarget("Localhost", localhostEndpoint);
            }

            if (TryCreateManualConnectEndpoint(_game._connectHostBuffer, TryParseBrowserPort(_game._connectPortBuffer), out var manualEndpoint))
            {
                yield return new LobbyBrowserTarget("Manual target", manualEndpoint);
            }

            if (TryCreateManualConnectEndpoint(_game._recentConnectHost ?? string.Empty, _game._recentConnectPort, out var recentEndpoint))
            {
                yield return new LobbyBrowserTarget("Recent", recentEndpoint);
            }
        }

        private bool TryParseManualConnectWebSocketEndpoint(out NetworkEndpoint endpoint)
        {
            return TryCreateManualConnectEndpoint(_game._connectHostBuffer, TryParseBrowserPort(_game._connectPortBuffer), out endpoint);
        }

        private static bool TryCreateManualConnectEndpoint(string? hostText, int port, out NetworkEndpoint endpoint)
        {
            endpoint = default;
            var host = hostText?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(host))
            {
                return false;
            }

            if (OperatingSystem.IsBrowser()
                && TryParseExplicitWebSocketUri(host, out var explicitWebSocketUri))
            {
                endpoint = new NetworkEndpoint(explicitWebSocketUri.Host, 0, 0, explicitWebSocketUri.ToString());
                return true;
            }

            if (port is <= 0 or > 65535)
            {
                return false;
            }

            endpoint = NetworkEndpoint.ForCurrentRuntimeSinglePort(host, port);
            return true;
        }

        private static bool TryParseExplicitWebSocketUri(string value, out Uri uri)
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
    }
}
