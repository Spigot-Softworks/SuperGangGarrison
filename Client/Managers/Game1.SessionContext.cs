#nullable enable

using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Client.Plugins;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Client;

public partial class Game1 : ISessionContext, IDiscordContext
{
    string ISessionContext._autoBalanceNoticeText { get => _autoBalanceNoticeText; set => _autoBalanceNoticeText = value; }

    int ISessionContext._autoBalanceNoticeTicks { get => _autoBalanceNoticeTicks; set => _autoBalanceNoticeTicks = value; }

    bool ISessionContext._classSelectOpen { get => _classSelectOpen; set => _classSelectOpen = value; }

    OpenGarrison.Core.SimulationConfig ISessionContext._config { get => _config; set => _config = value; }

    string ISessionContext._connectHostBuffer { get => _connectHostBuffer; set => _connectHostBuffer = value; }

    string ISessionContext._connectPortBuffer { get => _connectPortBuffer; set => _connectPortBuffer = value; }

    bool ISessionContext._consoleOpen { get => _consoleOpen; set => _consoleOpen = value; }

    bool ISessionContext._controlsMenuOpen { get => _controlsMenuOpen; set => _controlsMenuOpen = value; }

    bool ISessionContext._creditsOpen { get => _creditsOpen; set => _creditsOpen = value; }

    bool ISessionContext._editingConnectHost { get => _editingConnectHost; set => _editingConnectHost = value; }

    bool ISessionContext._editingConnectPort { get => _editingConnectPort; set => _editingConnectPort = value; }

    bool ISessionContext._editingPlayerName { get => _editingPlayerName; set => _editingPlayerName = value; }

    bool ISessionContext._inGameMenuOpen { get => _inGameMenuOpen; set => _inGameMenuOpen = value; }

    bool ISessionContext._lastToDieConnectionPresentationPending { get => _lastToDieConnectionPresentationPending; set => _lastToDieConnectionPresentationPending = value; }

    bool ISessionContext._lastToDieRoomCodeJoinOpen { get => _lastToDieRoomCodeJoinOpen; set => _lastToDieRoomCodeJoinOpen = value; }

    OpenGarrison.Client.Game1.LobbyBrowserEntry ISessionContext._lobbyBrowserDetailsEntry { get => _lobbyBrowserDetailsEntry; set => _lobbyBrowserDetailsEntry = value; }

    string ISessionContext._lobbyBrowserDetailsStatus { get => _lobbyBrowserDetailsStatus; set => _lobbyBrowserDetailsStatus = value; }

    List<OpenGarrison.Client.Game1.LobbyBrowserEntry> ISessionContext._lobbyBrowserEntries { get => _lobbyBrowserEntries; }

    int ISessionContext._lobbyBrowserHoverIndex { get => _lobbyBrowserHoverIndex; set => _lobbyBrowserHoverIndex = value; }

    OpenGarrison.Client.Game1.LobbyBrowserMode ISessionContext._lobbyBrowserMode { get => _lobbyBrowserMode; set => _lobbyBrowserMode = value; }

    bool ISessionContext._lobbyBrowserOpen { get => _lobbyBrowserOpen; set => _lobbyBrowserOpen = value; }

    OpenGarrison.Client.Game1.LobbyBrowserPage ISessionContext._lobbyBrowserPage { get => _lobbyBrowserPage; set => _lobbyBrowserPage = value; }

    Task<List<OpenGarrison.Client.Game1.LobbyRegistryServerEntry>> ISessionContext._lobbyBrowserRegistryRequestTask { get => _lobbyBrowserRegistryRequestTask; set => _lobbyBrowserRegistryRequestTask = value; }

    int ISessionContext._lobbyBrowserScrollOffset { get => _lobbyBrowserScrollOffset; set => _lobbyBrowserScrollOffset = value; }

    int ISessionContext._lobbyBrowserSelectedIndex { get => _lobbyBrowserSelectedIndex; set => _lobbyBrowserSelectedIndex = value; }

    OpenGarrison.Client.Game1.LobbyBrowserSource ISessionContext._lobbyBrowserSource { get => _lobbyBrowserSource; set => _lobbyBrowserSource = value; }

    int ISessionContext._manualConnectControllerIndex { get => _manualConnectControllerIndex; set => _manualConnectControllerIndex = value; }

    bool ISessionContext._manualConnectOpen { get => _manualConnectOpen; set => _manualConnectOpen = value; }

    string ISessionContext._menuStatusMessage { get => _menuStatusMessage; set => _menuStatusMessage = value; }

    OpenGarrison.Client.NetworkGameClient ISessionContext._networkClient { get => _networkClient; }

    bool ISessionContext._optionsMenuOpen { get => _optionsMenuOpen; set => _optionsMenuOpen = value; }

    string ISessionContext._passwordEditBuffer { get => _passwordEditBuffer; set => _passwordEditBuffer = value; }

    string ISessionContext._passwordPromptMessage { get => _passwordPromptMessage; set => _passwordPromptMessage = value; }

    bool ISessionContext._passwordPromptOpen { get => _passwordPromptOpen; set => _passwordPromptOpen = value; }

    Nullable<OpenGarrison.Client.Game1.ControllerControlsMenuBinding> ISessionContext._pendingControllerControlsBinding { get => _pendingControllerControlsBinding; set => _pendingControllerControlsBinding = value; }

    Nullable<OpenGarrison.Client.Game1.ControlsMenuBinding> ISessionContext._pendingControlsBinding { get => _pendingControlsBinding; set => _pendingControlsBinding = value; }

    bool ISessionContext._pluginOptionsMenuOpen { get => _pluginOptionsMenuOpen; set => _pluginOptionsMenuOpen = value; }

    string ISessionContext._recentConnectHost { get => _recentConnectHost; set => _recentConnectHost = value; }

    int ISessionContext._recentConnectPort { get => _recentConnectPort; set => _recentConnectPort = value; }

    bool ISessionContext._teamSelectOpen { get => _teamSelectOpen; set => _teamSelectOpen = value; }

    OpenGarrison.Client.Game1.LobbyBrowserEntry ISessionContext.AddLobbyBrowserEntry(string displayName, OpenGarrison.Client.NetworkEndpoint endpoint, bool isPrivate, bool isLobbyEntry) => AddLobbyBrowserEntry(displayName, endpoint, isPrivate, isLobbyEntry);

    void ISessionContext.BeginFriendCodeJoin(string friendCode) { BeginFriendCodeJoin(friendCode); }

    void ISessionContext.BeginRelayRoomJoin(string roomCode) { BeginRelayRoomJoin(roomCode); }

    void ISessionContext.CancelFriendCodeJoin() { CancelFriendCodeJoin(); }

    void ISessionContext.CancelLegacyGg2LobbyRequest() { CancelLegacyGg2LobbyRequest(); }

    void ISessionContext.ClearLobbyBrowserDetails() { ClearLobbyBrowserDetails(); }

    void ISessionContext.CloseLobbyBrowserLobbyClient() { CloseLobbyBrowserLobbyClient(); }

    void ISessionContext.EnsureLobbyBrowserClient() { EnsureLobbyBrowserClient(); }

    void ISessionContext.InitializeConnectHostCursor() { InitializeConnectHostCursor(); }

    void ISessionContext.InitializeConnectPortCursor() { InitializeConnectPortCursor(); }

    void ISessionContext.InitializePasswordEditCursor() { InitializePasswordEditCursor(); }

    bool ISessionContext.IsWatchOnlySession() => IsWatchOnlySession();

    void ISessionContext.OpenLobbyBrowserDetails(OpenGarrison.Client.Game1.LobbyBrowserEntry entry) { OpenLobbyBrowserDetails(entry); }

    void ISessionContext.ResetChatInputState(bool requireOpenKeyRelease = false) { ResetChatInputState(requireOpenKeyRelease); }

    void ISessionContext.ResetSpectatorTracking(bool enableTracking) { ResetSpectatorTracking(enableTracking); }

    void ISessionContext.StartLegacyGg2LobbyRequest() { StartLegacyGg2LobbyRequest(); }

    void ISessionContext.StartLobbyBrowserRegistryRequest() { StartLobbyBrowserRegistryRequest(); }

    bool ISessionContext.TryConnectLegacyGg2Server(string host, int port, bool addConsoleFeedback) => TryConnectLegacyGg2Server(host, port, addConsoleFeedback);

    bool ISessionContext.TryConnectToServer(OpenGarrison.Client.NetworkEndpoint endpoint, bool addConsoleFeedback, OpenGarrison.Client.Game1.OnlineConnectionIntent intent) => TryConnectToServer(endpoint, addConsoleFeedback, intent);

    bool ISessionContext.TryConnectToServer(OpenGarrison.Client.NetworkEndpoint endpoint, bool addConsoleFeedback) => TryConnectToServer(endpoint, addConsoleFeedback);

    bool ISessionContext.TryConnectToServer(string host, int port, bool addConsoleFeedback) => TryConnectToServer(host, port, addConsoleFeedback);

#if !BROWSER_KNI
    string IDiscordContext.ResolveDiscordApplicationId() => ResolveDiscordApplicationId();
#endif

#if !BROWSER_KNI
    DiscordRPC.RichPresence IDiscordContext.BuildDiscordRichPresencePayload(System.DateTime startTimestampUtc) => BuildDiscordRichPresencePayload(startTimestampUtc);
#endif

#if !BROWSER_KNI
    string IDiscordContext.BuildDiscordRichPresenceState() => BuildDiscordRichPresenceState();
#endif

}
