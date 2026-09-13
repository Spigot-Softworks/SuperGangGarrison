#nullable enable
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.ClientShared;
using OpenGarrison.Core;
using OpenGarrison.Protocol;

namespace OpenGarrison.Client;

public partial class Game1
{
    private PlayerHostedRoomSession? _peerRoomSession;
    private Task<PeerRoomConnection>? _peerRoomOperation;
    private Task _peerRoomCleanup = Task.CompletedTask;
    private CancellationTokenSource? _peerRoomCancellation;
    private PeerRoomRequest? _peerRoomRequest;
    private bool _practiceCoOpMenu, _editingPeerPractice, _peerRoomRejoining;
    private int _editingPeerRevision;
    private long _peerReconnectDeadline, _peerReconnectAt, _peerLtdCommandAt;
    private bool IsPeerRoomOwner => _peerRoomSession?.IsOwner == true;
    private static readonly HttpClient PeerRoomHttp = new();

    private PeerPracticeSettings CapturePeerPracticeSettings() => new(
        Map: _practiceSetupState.GetSelectedMapEntry()?.LevelName ?? "Harvest",
        TickRate: _practiceSetupState.TickRate, TimeLimitMinutes: _practiceSetupState.TimeLimitMinutes,
        CaptureLimit: _practiceSetupState.CapLimit, RespawnSeconds: _practiceSetupState.RespawnSeconds,
        RedBots: _practiceSetupState.FriendlyBotCount, BlueBots: _practiceSetupState.EnemyBotCount,
        SpecialAbilities: _practiceSetupState.SpecialAbilitiesEnabled);

    private void OpenPracticeCoOpMenu()
    {
        _practiceSetupOpen = false;
        OpenLastToDieMenu();
        _practiceCoOpMenu = true;
        _lastToDieMenuPage = LastToDieMenuPage.CoOp;
        BeginPeerRoom(true);
    }

    private void OpenPracticeCoOpJoin()
    {
        _practiceSetupOpen = false;
        OpenManagedRoomJoin();
        _practiceCoOpMenu = true;
    }

    private void BeginPeerRoom(bool create, PeerPracticeSettings? settings = null)
    {
        if (_peerRoomOperation is not null) return;
        if (!_bootstrapController.CanEnterGameplaySession(out var reason))
        { _menuStatusMessage = reason ?? "Game assets are still loading."; return; }
        if (!create && !RelayRoomCode.TryNormalize(_managedRoomCodeBuffer, out _))
        { _menuStatusMessage = "Enter the four-character room code."; return; }
        _peerRoomRequest = new(_clientIdentity.ClientId, _clientIdentity.ClientSecret, _clientIdentity.FriendCode,
            _world.LocalPlayer.DisplayName, Guid.NewGuid().ToString("N"), ProtocolVersion.Current,
            ClientDistribution.ContentId, _practiceCoOpMenu ? "Practice" : "LastToDie", _managedRoomCodeBuffer,
            settings ?? (_practiceCoOpMenu ? CapturePeerPracticeSettings() : new PeerPracticeSettings(BlueBots: 0)));
        _peerRoomCancellation = new(TimeSpan.FromSeconds(25));
        _peerRoomOperation = OpenPeerRoomAfterCleanup(_peerRoomRequest, create, _peerRoomCancellation.Token);
        _lastToDieMenuPage = LastToDieMenuPage.RoomLoading;
        _lastToDieMenuHoverIndex = 0;
        _managedRoomReturnPage = create ? LastToDieMenuPage.CoOp : LastToDieMenuPage.RoomJoin;
        _menuStatusMessage = create ? "Creating your room..." : "Joining the room...";
    }

    private async Task<PeerRoomConnection> OpenPeerRoomAfterCleanup(PeerRoomRequest request, bool create, CancellationToken cancellation)
    {
        try { await _peerRoomCleanup; } catch (Exception) { /* The next admission request reports any remaining server problem. */ }
        cancellation.ThrowIfCancellationRequested();
        return await PeerRoomConnection.OpenAsync(ClientRuntimeBootstrap.GetBrowserHttpClient() ?? PeerRoomHttp,
            ClientDistribution.RoomServiceOrigin, request, create, cancellation);
    }

    private void PumpPeerRoom(double elapsed)
    {
        if (_peerRoomOperation is { IsCompleted: true } operation)
        {
            _peerRoomOperation = null;
            _peerRoomCancellation?.Dispose(); _peerRoomCancellation = null;
            if (!operation.IsCompletedSuccessfully)
            {
                var error = operation.IsCanceled ? "Room request cancelled." : operation.Exception?.GetBaseException().Message ?? "The room could not be opened.";
                if (!_peerRoomRejoining || Environment.TickCount64 >= _peerReconnectDeadline)
                { LeavePeerRoom(); ShowManagedRoomFailure(error); }
                else _peerReconnectAt = Environment.TickCount64 + 1500;
            }
            else
            {
                var connection = operation.Result;
                _peerRoomRequest = _peerRoomRequest! with { Code = connection.Grant.Code };
                _hostedLastToDieRoomCode = connection.Grant.Code;
                if (_peerRoomSession is not null) _peerRoomSession.ReplaceConnection(connection);
                else
                {
                    _peerRoomSession = new(connection);
                    _peerRoomSession.ConnectLocal += ConnectPeerRoomGame;
                    _peerRoomSession.ReturnedToLobby += ShowPeerLobby;
                    _peerRoomSession.Failed += FailPeerRoom;
                    _peerRoomSession.Notice += message => _menuStatusMessage = message;
                }
                _peerRoomRejoining = false; _peerReconnectDeadline = 0;
                _menuStatusMessage = string.Empty;
                if (_peerRoomSession.State?.Phase != "Playing") ShowPeerLobby();
            }
        }
        var session = _peerRoomSession;
        if (session is null) return;
        if (!session.Connection.IsConnected && _peerRoomOperation is null && Environment.TickCount64 >= _peerReconnectAt)
        {
            if (_peerReconnectDeadline == 0) _peerReconnectDeadline = Environment.TickCount64 + 30000;
            if (Environment.TickCount64 >= _peerReconnectDeadline) { FailPeerRoom("The room connection could not be restored."); return; }
            _peerRoomRejoining = true;
            _peerRoomCancellation = new(TimeSpan.FromSeconds(8));
            _peerRoomOperation = PeerRoomConnection.OpenAsync(ClientRuntimeBootstrap.GetBrowserHttpClient() ?? PeerRoomHttp,
                ClientDistribution.RoomServiceOrigin, _peerRoomRequest!, false, _peerRoomCancellation.Token);
            _menuStatusMessage = "Reconnecting to your room...";
        }
        session.Update(elapsed);
        if (_peerRoomSession != session) return;
        _embeddedSessionHost = session.Host;
        if (session.State is { Phase: "Playing", Kind: "LastToDie" } room
            && _networkClient.LastToDieState.Snapshot is { Phase: LastToDieWirePhase.Lobby } run
            && Environment.TickCount64 >= _peerLtdCommandAt)
        {
            _peerLtdCommandAt = Environment.TickCount64 + 600;
            var local = run.Players.FirstOrDefault(player => player.Slot == _networkClient.LocalPlayerSlot);
            if (local is not null && !local.IsReady) _networkClient.SendLastToDieCommand(LastToDieCommandKind.Ready);
            if (session.IsOwner && room.Players!.Where(player => player.Connected).All(player =>
                    run.Players.Any(member => member.Slot == player.Slot && member.IsReady && member.IsConnected)))
                _networkClient.SendLastToDieCommand(LastToDieCommandKind.RequestStart);
        }
    }

    private void ConnectPeerRoomGame(INetworkClientMessageTransport transport)
    {
        _networkClient.Disconnect();
        ClearReplayQueue(clearActiveReplayPath: true); ClearPendingNetworkMapSync();
        ResetGameplayRuntimeState();
        _world.ConfigureExperimentalGameplaySettings(new ExperimentalGameplaySettings());
        if (!_networkClient.Connect(transport, _world.LocalPlayer.DisplayName, _world.LocalPlayer.BadgeMask,
                out var error, _clientIdentity.FriendCode, PlayerCardProfile.Serialize(_clientIdentity.PlayerCard),
                clientInstanceId: Guid.Parse(_clientIdentity.ClientId)))
        { transport.Dispose(); FailPeerRoom(error); return; }
        _onlineConnectionIntent = OnlineConnectionIntent.Join;
        ClearOnlinePlayerSocialProfiles();
        _lastToDieConnectionPresentationPending = _peerRoomSession?.State?.Kind == "LastToDie";
        CloseLastToDieMenu(clearStatus: true);
        SetJoiningServerLoadingLabel(_practiceCoOpMenu ? "Practice" : "Last to Die");
        ShowJoiningServerLoadingOverlay();
    }

    private void ShowPeerLobby()
    {
        _networkClient.Disconnect();
        _embeddedSessionHost = null;
        HideJoiningServerLoadingOverlay();
        _lastToDieConnectionPresentationPending = false;
        CloseGameplayOverlayState();
        _mainMenuOpen = true; _lastToDieMenuOpen = true;
        _gameplaySessionKind = GameplaySessionKind.None;
        _lastToDieMenuPage = LastToDieMenuPage.PeerLobby;
        _lastToDieMenuHoverIndex = 0;
        _editingPeerPractice = false; _practiceSetupOpen = false;
        _menuStatusMessage = string.Empty;
    }

    private bool TryReconnectPeerRoom()
    {
        if (_peerRoomSession is null) return false;
        if (_peerRoomSession.IsOwner) { FailPeerRoom("The local room connection ended."); return true; }
        _peerRoomSession.RequestReconnect();
        _menuStatusMessage = "Reconnecting to the host...";
        return true;
    }
    private void FailPeerRoom(string reason)
    {
        ReturnToMainMenu(reason);
        OpenLastToDieMenu(reason);
        _lastToDieMenuPage = LastToDieMenuPage.CoOp;
    }
    private void CancelPeerRoomRequest()
    {
        var pending = _peerRoomOperation;
        _peerRoomOperation = null;
        _peerRoomCancellation?.Cancel(); _peerRoomCancellation?.Dispose(); _peerRoomCancellation = null;
        if (pending is not null)
        {
            _peerRoomCleanup = Task.WhenAll(_peerRoomCleanup, ReleasePendingPeerRoom(pending),
                _peerRoomSession is null && _peerRoomRequest is { } request ? CancelPeerRoomAdmission(request) : Task.CompletedTask);
            ObserveRoomCleanup(_peerRoomCleanup);
        }
    }
    private static async Task CancelPeerRoomAdmission(PeerRoomRequest request)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        using var response = await (ClientRuntimeBootstrap.GetBrowserHttpClient() ?? PeerRoomHttp).PostAsJsonAsync(
            new Uri(ClientDistribution.RoomServiceOrigin, "/api/peer-rooms/cancel"), request,
            PeerRoomJsonContext.Default.PeerRoomRequest, timeout.Token);
    }
    private static async Task ReleasePendingPeerRoom(Task<PeerRoomConnection> pending)
    {
        try { var room = await pending; await room.LeaveAsync(); }
        catch (Exception) { }
    }
    private void LeavePeerRoom()
    {
        CancelPeerRoomRequest();
        var session = _peerRoomSession; _peerRoomSession = null;
        if (session is not null)
        {
            _peerRoomCleanup = Task.WhenAll(_peerRoomCleanup, session.Connection.LeaveAsync());
            ObserveRoomCleanup(_peerRoomCleanup);
        }
        session?.Dispose();
        if (session is not null) _embeddedSessionHost = null;
        _peerRoomRejoining = false; _peerReconnectDeadline = 0;
        _editingPeerPractice = false;
    }

    private void EditPeerPracticeSettings()
    {
        if (!IsPeerRoomOwner || _peerRoomSession?.State is not { Phase: "Lobby", Settings: { } settings } state) return;
        _lastToDieMenuOpen = false;
        OpenPracticeSetupMenu(); ClosePracticeMapBrowser();
        _practiceSetupState.SelectMapEntry(settings.Map);
        _practiceSetupState.TickRate = settings.TickRate;
        _practiceSetupState.TimeLimitMinutes = settings.TimeLimitMinutes;
        _practiceSetupState.CapLimit = settings.CaptureLimit;
        _practiceSetupState.RespawnSeconds = settings.RespawnSeconds;
        _practiceSetupState.FriendlyBotCount = settings.RedBots;
        _practiceSetupState.EnemyBotCount = settings.BlueBots;
        _practiceSetupState.SpecialAbilitiesEnabled = settings.SpecialAbilities;
        _editingPeerPractice = true; _editingPeerRevision = state.Revision;
    }
    private void ApplyPeerPracticeSettings()
    {
        if (!_editingPeerPractice) { TryStartPracticeFromSetup(); return; }
        _peerRoomSession?.Connection.Send(new("settings", Revision: _editingPeerRevision, Settings: CapturePeerPracticeSettings()));
        ShowPeerLobby();
    }
    private void BackFromPracticeSetup()
    {
        ClosePracticeMapBrowser(); _practiceSetupOpen = false;
        if (_editingPeerPractice) ShowPeerLobby();
    }
}
