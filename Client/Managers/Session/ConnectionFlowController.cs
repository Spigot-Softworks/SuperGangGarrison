#nullable enable

using System;
using System.Collections.Generic;
using OpenGarrison.Core;

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class ConnectionFlowController
    {
        private readonly ISessionContext _context;
        private string? _recentConnectHost;
        private int _recentConnectPort;

        public ConnectionFlowController(ISessionContext context)
        {
            _context = context;
        }

        internal void RememberRecentConnection(string? host, int port)
        {
            _recentConnectHost = host;
            _recentConnectPort = port;
        }

        public void OpenLobbyBrowser()
        {
            OpenLobbyBrowser(LobbyBrowserMode.Join);
        }

        public void OpenWatchBrowser()
        {
            if (OpenGarrison.ClientShared.ClientDistribution.IsGg2Only) return;
            OpenLobbyBrowser(LobbyBrowserMode.Watch);
        }

        private void OpenLobbyBrowser(LobbyBrowserMode mode)
        {
            if (IsRestrictedBrowserEdition
                && !(OperatingSystem.IsBrowser() && mode == LobbyBrowserMode.Join
                    && _context._lobbyBrowserSource == LobbyBrowserSource.Gg2)) return;
            _context._lobbyBrowserOpen = true;
            _context._lobbyBrowserMode = mode;
            if (OpenGarrison.ClientShared.ClientDistribution.IsGg2Only)
                _context._lobbyBrowserSource = LobbyBrowserSource.Gg2;
            if (mode == LobbyBrowserMode.Watch) _context._lobbyBrowserSource = LobbyBrowserSource.Sgg;
            _context._lobbyBrowserPage = LobbyBrowserPage.List;
            _context.ClearLobbyBrowserDetails();
            _context._manualConnectOpen = false;
            _context._optionsMenuOpen = false;
            _context._pluginOptionsMenuOpen = false;
            _context._creditsOpen = false;
            _context._controlsMenuOpen = false;
            _context._pendingControlsBinding = null;
            _context._pendingControllerControlsBinding = null;
            _context._editingPlayerName = false;
            DisableManualConnectEditing();
            _context._lobbyBrowserSelectedIndex = -1;
            _context._lobbyBrowserHoverIndex = -1;
            _context._lobbyBrowserScrollOffset = 0;
            RefreshLobbyBrowser();
        }

        public void CloseLobbyBrowser(bool clearStatus)
        {
            _context._lobbyBrowserOpen = false;
            _context._lobbyBrowserPage = LobbyBrowserPage.List;
            _context._lobbyBrowserHoverIndex = -1;
            _context._lobbyBrowserRegistryRequestTask = null;
            _context.CancelLegacyGg2LobbyRequest();
            _context.CloseLobbyBrowserLobbyClient();
            _context.ClearLobbyBrowserDetails();
            if (clearStatus)
            {
                _context._menuStatusMessage = string.Empty;
            }
        }

        public void RefreshLobbyBrowser()
        {
            if (IsRestrictedBrowserEdition
                && !(OperatingSystem.IsBrowser() && _context._lobbyBrowserSource == LobbyBrowserSource.Gg2)) return;
            if (OpenGarrison.ClientShared.ClientDistribution.IsGg2Only)
                _context._lobbyBrowserSource = LobbyBrowserSource.Gg2;
            _context._lobbyBrowserRegistryRequestTask = null;
            _context.CancelLegacyGg2LobbyRequest();
            _context.CloseLobbyBrowserLobbyClient();
            _context.ClearLobbyBrowserDetails();
            _context._lobbyBrowserPage = LobbyBrowserPage.List;
            _context._lobbyBrowserEntries.Clear();
            _context._lobbyBrowserSelectedIndex = -1;
            _context._lobbyBrowserScrollOffset = 0;
            if (_context._lobbyBrowserMode == LobbyBrowserMode.Join
                && _context._lobbyBrowserSource == LobbyBrowserSource.Gg2)
            {
                _context.StartLegacyGg2LobbyRequest();
                _context._menuStatusMessage = "Contacting GG2 lobby...";
                return;
            }

            if (!OperatingSystem.IsBrowser())
            {
                _context.EnsureLobbyBrowserClient();
            }

            _context.StartLobbyBrowserRegistryRequest();

            foreach (var target in BuildLobbyBrowserTargets())
            {
                _context.AddLobbyBrowserEntry(target.DisplayName, target.Endpoint, isPrivate: false, isLobbyEntry: false);
            }

            _context._lobbyBrowserSelectedIndex = _context._lobbyBrowserEntries.Count > 0 ? 0 : -1;
            _context._menuStatusMessage = _context._lobbyBrowserEntries.Count > 0
                ? "Refreshing server list..."
                : "Contacting server registry...";
        }

        public void OpenManualConnectMenuFromLobbyBrowser()
        {
            if (IsRestrictedBrowserEdition || OpenGarrison.ClientShared.ClientDistribution.IsGg2Only) return;
            _context._lastToDieRoomCodeJoinOpen = false;
            _context.SessionTransitions.LastToDieConnectionPresentationPending = false;
            CloseLobbyBrowser(clearStatus: false);
            _context.CancelFriendCodeJoin();
            _context._manualConnectOpen = true;
            _context._manualConnectControllerIndex = 0;
            SetManualConnectEditingField(editHost: true);
            _context._menuStatusMessage = string.Empty;
        }

        public void CloseManualConnectMenu(bool clearStatus)
        {
            _context._lastToDieRoomCodeJoinOpen = false;
            _context.SessionTransitions.LastToDieConnectionPresentationPending = false;
            _context._manualConnectOpen = false;
            _context._manualConnectControllerIndex = 0;
            _context.CancelFriendCodeJoin();
            DisableManualConnectEditing();
            if (clearStatus)
            {
                _context._menuStatusMessage = string.Empty;
            }
        }

        public void ToggleManualConnectEditingField()
        {
            SetManualConnectEditingField(!_context._editingConnectHost);
        }

        public void SetManualConnectEditingField(bool editHost)
        {
            _context._editingConnectHost = editHost;
            _context._editingConnectPort = !editHost;
            if (editHost)
            {
                _context.InitializeConnectHostCursor();
            }
            else
            {
                _context.InitializeConnectPortCursor();
            }
        }

        public void DisableManualConnectEditing()
        {
            _context._editingConnectHost = false;
            _context._editingConnectPort = false;
        }

        public void TryConnectFromMenu()
        {
            if (_context._lastToDieRoomCodeJoinOpen)
            {
                if (OpenGarrison.ClientShared.RelayRoomCode.TryNormalize(
                        _context.ConnectHostEdit.Text,
                        out var roomCode))
                {
                    _context.ConnectHostEdit.Text = roomCode;
                    _context.InitializeConnectHostCursor();
                    _context.BeginRelayRoomJoin(roomCode);
                    return;
                }

                if (Game1.TryExtractFriendCodeFromText(_context.ConnectHostEdit.Text, out var lastToDieFriendCode))
                {
                    _context.ConnectHostEdit.Text = lastToDieFriendCode;
                    _context.InitializeConnectHostCursor();
                    _context.BeginFriendCodeJoin(lastToDieFriendCode);
                    return;
                }

                _context._menuStatusMessage = "Enter a four-character room code or OG2 friend code.";
                return;
            }

            if (OpenGarrison.ClientShared.ClientIdentityDocument.TryNormalizeFriendCode(
                    _context.ConnectHostEdit.Text,
                    out var friendCode))
            {
                _context.BeginFriendCodeJoin(friendCode);
                return;
            }

            if (!TryParseManualConnectTarget(out var endpoint))
            {
                return;
            }

            _context.TryConnectToServer(endpoint, addConsoleFeedback: false);
        }

        public bool TryParseManualConnectTarget(out NetworkEndpoint endpoint)
        {
            endpoint = default;
            if (TryParseManualConnectUriEndpoint(out endpoint))
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
            host = _context.ConnectHostEdit.Text.Trim();
            port = 0;

            if (string.IsNullOrWhiteSpace(host))
            {
                _context._menuStatusMessage = "Host is required.";
                return false;
            }

            if (OperatingSystem.IsBrowser()
                && TryParseExplicitNetworkUri(host, out var explicitWebSocketUri)
                && (explicitWebSocketUri.Scheme == "ws" || explicitWebSocketUri.Scheme == "wss"))
            {
                host = explicitWebSocketUri.ToString();
                return true;
            }

            if (!int.TryParse(_context.ConnectPortEdit.Text.Trim(), out port) || port is <= 0 or > 65535)
            {
                _context._menuStatusMessage = "Port must be 1-65535.";
                return false;
            }

            return true;
        }

        public void JoinSelectedLobbyEntry()
        {
            if (!CanJoinSelectedLobbyEntry())
            {
                _context._menuStatusMessage = _context._lobbyBrowserSource == LobbyBrowserSource.Gg2
                    ? "Select a compatible public GG2 server."
                    : "Select an online server first.";
                return;
            }

            var entry = _context._lobbyBrowserEntries[_context._lobbyBrowserSelectedIndex];
            if (OpenGarrison.ClientShared.ClientDistribution.IsGg2Only && !entry.IsLegacyGg2)
            {
                _context._menuStatusMessage = "Select a GG2 server.";
                return;
            }
            if (entry.IsLegacyGg2)
            {
                _context.TryConnectLegacyGg2Server(entry.Host, entry.Port, addConsoleFeedback: false);
            }
            else
            {
                _context.TryConnectToServer(entry.Endpoint, addConsoleFeedback: false);
            }
        }

        public void OpenSelectedLobbyEntryDetails()
        {
            if (!CanJoinSelectedLobbyEntry())
            {
                _context._menuStatusMessage = "Select an online server first.";
                return;
            }

            var entry = _context._lobbyBrowserEntries[_context._lobbyBrowserSelectedIndex];
            _context.OpenLobbyBrowserDetails(entry);
        }

        public void WatchSelectedLobbyEntry()
        {
            var entry = _context._lobbyBrowserDetailsEntry;
            if (entry is null
                && _context._lobbyBrowserSelectedIndex >= 0
                && _context._lobbyBrowserSelectedIndex < _context._lobbyBrowserEntries.Count)
            {
                entry = _context._lobbyBrowserEntries[_context._lobbyBrowserSelectedIndex];
            }

            if (entry is null || !(entry.HasResponse || entry.CanJoinDirectly))
            {
                _context._lobbyBrowserDetailsStatus = "Select an online server first.";
                return;
            }

            _context.TryConnectToServer(entry.Endpoint, addConsoleFeedback: false, OnlineConnectionIntent.Watch);
        }

        public bool CanJoinSelectedLobbyEntry()
        {
            return _context._lobbyBrowserSelectedIndex >= 0
                && _context._lobbyBrowserSelectedIndex < _context._lobbyBrowserEntries.Count
                && (_context._lobbyBrowserEntries[_context._lobbyBrowserSelectedIndex].IsLegacyGg2
                    ? _context._lobbyBrowserEntries[_context._lobbyBrowserSelectedIndex].CanJoinDirectly
                    : _context._lobbyBrowserEntries[_context._lobbyBrowserSelectedIndex].HasResponse
                        || _context._lobbyBrowserEntries[_context._lobbyBrowserSelectedIndex].CanJoinDirectly);
        }

        public IEnumerable<LobbyBrowserTarget> BuildLobbyBrowserTargets()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var target in BuildDefaultLobbyTargets())
            {
                if (string.IsNullOrWhiteSpace(target.Endpoint.Host)
                    || (!target.Endpoint.HasUdpEndpoint
                        && !target.Endpoint.HasWebSocketEndpoint))
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
            _context._passwordPromptOpen = true;
            _context.PasswordEdit.Text = string.Empty;
            _context.InitializePasswordEditCursor();
            _context._passwordPromptMessage = message;
            _context._consoleOpen = false;
            _context._inGameMenuOpen = false;
            _context._optionsMenuOpen = false;
            _context._pluginOptionsMenuOpen = false;
            _context._controlsMenuOpen = false;
            _context._pendingControlsBinding = null;
            _context._pendingControllerControlsBinding = null;
            _context.TeamClassSelection.TeamSelectOpen = false;
            _context.TeamClassSelection.ClassSelectOpen = false;
        }

        public void CloseNetworkPasswordPrompt()
        {
            _context._passwordPromptOpen = false;
            _context.PasswordEdit.Text = string.Empty;
            _context._passwordPromptMessage = string.Empty;
        }

        public void EnterOnlineSpectatorState(string statusMessage)
        {
            _context.ResetSpectatorTracking(enableTracking: true);
            _context.TeamClassSelection.TeamSelectOpen = false;
            _context.TeamClassSelection.ClassSelectOpen = false;
            _context._menuStatusMessage = statusMessage;
        }

        public void EnterOnlineClassSelectionState(string statusMessage)
        {
            _context.ResetSpectatorTracking(enableTracking: false);
            _context.TeamClassSelection.TeamSelectOpen = false;
            _context.TeamClassSelection.ClassSelectOpen = true;
            _context._menuStatusMessage = statusMessage;
        }

        public void OpenOnlineTeamSelection(bool clearPendingSelections, string statusMessage)
        {
            if (_context.IsWatchOnlySession())
            {
                _context.TeamClassSelection.TeamSelectOpen = false;
                _context.TeamClassSelection.ClassSelectOpen = false;
                _context._menuStatusMessage = string.IsNullOrWhiteSpace(statusMessage)
                    ? "Watch mode cannot join teams."
                    : statusMessage;
                return;
            }

            if (clearPendingSelections)
            {
                _context._networkClient.ClearPendingTeamSelection();
                _context._networkClient.ClearPendingClassSelection();
            }

            _context.TeamClassSelection.TeamSelectOpen = true;
            _context.TeamClassSelection.ClassSelectOpen = false;
            _context.ResetChatInputState();
            _context._consoleOpen = false;
            _context._menuStatusMessage = statusMessage;
        }

        public void ShowAutoBalanceNotice(string text, int seconds)
        {
            _context._autoBalanceNoticeText = text;
            _context._autoBalanceNoticeTicks = Math.Max(1, seconds * _context._config.TicksPerSecond);
        }

        private static int TryParseBrowserPort(string text)
        {
            return int.TryParse(text.Trim(), out var port) && port is > 0 and <= 65535 ? port : 0;
        }

        private IEnumerable<LobbyBrowserTarget> BuildDefaultLobbyTargets()
        {
            if (TryCreateManualConnectEndpoint("127.0.0.1", OpenGarrisonPreferencesDocument.DefaultServerPort, out var localhostEndpoint))
            {
                yield return new LobbyBrowserTarget("Localhost", localhostEndpoint);
            }

            if (TryCreateManualConnectEndpoint(_context.ConnectHostEdit.Text, TryParseBrowserPort(_context.ConnectPortEdit.Text), out var manualEndpoint))
            {
                yield return new LobbyBrowserTarget("Manual target", manualEndpoint);
            }

            if (TryCreateManualConnectEndpoint(_recentConnectHost ?? string.Empty, _recentConnectPort, out var recentEndpoint))
            {
                yield return new LobbyBrowserTarget("Recent", recentEndpoint);
            }
        }

        private bool TryParseManualConnectUriEndpoint(out NetworkEndpoint endpoint)
        {
            return TryCreateManualConnectEndpoint(_context.ConnectHostEdit.Text, TryParseBrowserPort(_context.ConnectPortEdit.Text), out endpoint);
        }

        private static bool TryCreateManualConnectEndpoint(string? hostText, int port, out NetworkEndpoint endpoint)
        {
            endpoint = default;
            var host = hostText?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(host))
            {
                return false;
            }

            if (TryParseExplicitNetworkUri(host, out var explicitUri))
            {
                if (OperatingSystem.IsBrowser())
                {
                    endpoint = new NetworkEndpoint(explicitUri.Host, 0, 0, explicitUri.ToString());
                    return true;
                }

                endpoint = new NetworkEndpoint(explicitUri.Host, 0, 0, explicitUri.ToString());
                return true;
            }

            if (port is <= 0 or > 65535)
            {
                return false;
            }

            endpoint = NetworkEndpoint.ForCurrentRuntimeSinglePort(host, port);
            return true;
        }

        private static bool TryParseExplicitNetworkUri(string value, out Uri uri)
        {
            if (Uri.TryCreate(value, UriKind.Absolute, out uri!)
                && (uri.Scheme == "ws"
                    || uri.Scheme == "wss"
                    || uri.Scheme == "ws64"
                    || uri.Scheme == "wss64")
                && !string.IsNullOrWhiteSpace(uri.Host))
            {
                return true;
            }

            uri = null!;
            return false;
        }
}
