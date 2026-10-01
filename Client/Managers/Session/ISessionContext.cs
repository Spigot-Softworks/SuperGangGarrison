#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OpenGarrison.Client;

public interface ISessionContext
{
    TextEditState PasswordEdit { get; }
    TextEditState ConnectHostEdit { get; }
    TextEditState ConnectPortEdit { get; }
    TeamClassSelectionState TeamClassSelection { get; }
    GameplaySessionTransitionState SessionTransitions { get; }
    string _autoBalanceNoticeText { get; set; }
    int _autoBalanceNoticeTicks { get; set; }
    OpenGarrison.Core.SimulationConfig _config { get; set; }
    bool _consoleOpen { get; set; }
    bool _controlsMenuOpen { get; set; }
    bool _creditsOpen { get; set; }
    bool _editingConnectHost { get; set; }
    bool _editingConnectPort { get; set; }
    bool _editingPlayerName { get; set; }
    bool _inGameMenuOpen { get; set; }
    bool _lastToDieRoomCodeJoinOpen { get; set; }
    OpenGarrison.Client.Game1.LobbyBrowserEntry _lobbyBrowserDetailsEntry { get; set; }
    string _lobbyBrowserDetailsStatus { get; set; }
    List<OpenGarrison.Client.Game1.LobbyBrowserEntry> _lobbyBrowserEntries { get; }
    int _lobbyBrowserHoverIndex { get; set; }
    OpenGarrison.Client.Game1.LobbyBrowserMode _lobbyBrowserMode { get; set; }
    bool _lobbyBrowserOpen { get; set; }
    OpenGarrison.Client.Game1.LobbyBrowserPage _lobbyBrowserPage { get; set; }
    Task<List<OpenGarrison.Client.Game1.LobbyRegistryServerEntry>> _lobbyBrowserRegistryRequestTask { get; set; }
    int _lobbyBrowserScrollOffset { get; set; }
    int _lobbyBrowserSelectedIndex { get; set; }
    OpenGarrison.Client.Game1.LobbyBrowserSource _lobbyBrowserSource { get; set; }
    int _manualConnectControllerIndex { get; set; }
    bool _manualConnectOpen { get; set; }
    string _menuStatusMessage { get; set; }
    OpenGarrison.Client.NetworkGameClient _networkClient { get; }
    bool _optionsMenuOpen { get; set; }
    string _passwordPromptMessage { get; set; }
    bool _passwordPromptOpen { get; set; }
    Nullable<OpenGarrison.Client.Game1.ControllerControlsMenuBinding> _pendingControllerControlsBinding { get; set; }
    Nullable<OpenGarrison.Client.Game1.ControlsMenuBinding> _pendingControlsBinding { get; set; }
    bool _pluginOptionsMenuOpen { get; set; }
    OpenGarrison.Client.Game1.LobbyBrowserEntry AddLobbyBrowserEntry(string displayName, OpenGarrison.Client.NetworkEndpoint endpoint, bool isPrivate, bool isLobbyEntry);
    void BeginFriendCodeJoin(string friendCode);
    void BeginRelayRoomJoin(string roomCode);
    void CancelFriendCodeJoin();
    void CancelLegacyGg2LobbyRequest();
    void ClearLobbyBrowserDetails();
    void CloseLobbyBrowserLobbyClient();
    void EnsureLobbyBrowserClient();
    void InitializeConnectHostCursor();
    void InitializeConnectPortCursor();
    void InitializePasswordEditCursor();
    bool IsWatchOnlySession();
    void OpenLobbyBrowserDetails(OpenGarrison.Client.Game1.LobbyBrowserEntry entry);
    void ResetChatInputState(bool requireOpenKeyRelease = false);
    void ResetSpectatorTracking(bool enableTracking);
    void StartLegacyGg2LobbyRequest();
    void StartLobbyBrowserRegistryRequest();
    bool TryConnectLegacyGg2Server(string host, int port, bool addConsoleFeedback);
    bool TryConnectToServer(OpenGarrison.Client.NetworkEndpoint endpoint, bool addConsoleFeedback, OpenGarrison.Client.Game1.OnlineConnectionIntent intent);
    bool TryConnectToServer(OpenGarrison.Client.NetworkEndpoint endpoint, bool addConsoleFeedback);
    bool TryConnectToServer(string host, int port, bool addConsoleFeedback);
}
