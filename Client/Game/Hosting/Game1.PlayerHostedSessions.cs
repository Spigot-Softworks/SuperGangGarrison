#nullable enable
using System;
using OpenGarrison.Core;
using OpenGarrison.Core.LastToDie;
using OpenGarrison.ClientShared;
using OpenGarrison.SessionRuntime;

namespace OpenGarrison.Client;

public partial class Game1
{
    private EmbeddedSessionHost? _embeddedSessionHost;
    private bool IsEmbeddedSessionOwner => _embeddedSessionHost is not null;

    private void TryStartEmbeddedSolo(OpenGarrison.Core.LastToDie.LastToDieDifficulty difficulty)
    {
        if (!_bootstrapController.CanEnterGameplaySession(out var reason))
        { _menuStatusMessage = reason ?? "Game assets are still loading."; return; }
        try
        {
            LeavePeerRoom();
            LeaveManagedRoom();
            _networkClient.Disconnect();
            StopEmbeddedSession();
            ClearReplayQueue(clearActiveReplayPath: true);
            ClearPendingNetworkMapSync();
            ResetGameplayRuntimeState();
            _world.ConfigureExperimentalGameplaySettings(new ExperimentalGameplaySettings());
            _embeddedSessionHost = new EmbeddedSessionHost(new EmbeddedSessionOptions
            { LastToDie = true, MaximumPlayers = 1, Difficulty = difficulty, Name = "Last To Die Solo",
                PreferClassicMaps = ClientDistribution.IsRestricted });
            var transport = new EmbeddedSessionClientTransport(_embeddedSessionHost.CreatePeer(local: true));
            var identity = Guid.TryParse(_clientIdentity.ClientId, out var parsed) ? parsed : Guid.Empty;
            if (!_networkClient.Connect(transport, _world.LocalPlayer.DisplayName, _world.LocalPlayer.BadgeMask,
                    out var error, _clientIdentity.FriendCode, PlayerCardProfile.Serialize(_clientIdentity.PlayerCard),
                    clientInstanceId: identity))
            { transport.Dispose(); throw new InvalidOperationException(error); }
            _onlineConnectionIntent = OnlineConnectionIntent.Join;
            ClearOnlinePlayerSocialProfiles();
            _lastToDieConnectionPresentationPending = true;
            CloseLastToDieMenu(clearStatus: true);
            SetJoiningServerLoadingLabel("Last to Die");
            ShowJoiningServerLoadingOverlay();
            SetNetworkStatus("Loading Last to Die...");
        }
        catch (Exception exception)
        {
            _networkClient.Disconnect();
            StopEmbeddedSession();
            HideJoiningServerLoadingOverlay();
            OpenLastToDieMenu();
            _menuStatusMessage = "Could not start Last to Die: " + exception.Message;
        }
    }

    private void PumpEmbeddedSession(double elapsedSeconds)
    {
        if (_peerRoomSession is not null) return;
        if (_embeddedSessionHost is null) return;
        if (!_networkClient.IsConnected) { StopEmbeddedSession(); return; }
        try { _embeddedSessionHost.Advance(elapsedSeconds); }
        catch (Exception exception)
        {
            _networkClient.Disconnect();
            StopEmbeddedSession();
            _menuStatusMessage = "The local session stopped: " + exception.Message;
        }
    }

    private void StopEmbeddedSession()
    {
        var host = _embeddedSessionHost;
        _embeddedSessionHost = null;
        host?.Dispose();
    }
}
