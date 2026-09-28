#nullable enable
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using OpenGarrison.ClientShared;
using OpenGarrison.Protocol;

namespace OpenGarrison.Client;

public partial class Game1
{
    public PrivateRoomClient? _privateRoomClient;
    public PrivateRoomRequest? _managedRoomRequest;
    public PrivateRoomResponse? _managedRoom;
    public CancellationTokenSource? _managedRoomCancellation;
    public Task<PrivateRoomResponse>? _managedRoomOperation;
    public Task _managedRoomCleanup = Task.CompletedTask;
    public LastToDieMenuPage _managedRoomReturnPage = LastToDieMenuPage.CoOp;
    public OpenGarrison.Core.LastToDie.LastToDieDifficulty _managedRoomRetryDifficulty;
    private string _managedRoomCodeBuffer = string.Empty;
    private string _managedRoomFailureMessage = string.Empty;
    private bool IsManagedRoomOwner => _managedRoom?.IsOwner == true;
    public bool HasManagedRoom => _managedRoom is not null;
    private ulong _managedPauseCommandId;
    private bool _managedPauseRequested;
    private bool _managedPauseAccepted;
    private bool _managedPauseNeedsSync = true;
    private long _managedPauseRetryAt;
    private Task<bool>? _managedRoomCopyTask;
    private long _managedReconnectDeadline;
    private bool _managedReconnecting;

    private bool TryReconnectManagedRoom()
    {
        if (_managedRoom is null || _managedRoomRequest is null) return false;
        var now = Environment.TickCount64;
        if (_managedReconnectDeadline == 0) _managedReconnectDeadline = now + 24000;
        if (now >= _managedReconnectDeadline) return false;
        if (_managedRoomOperation is not null) return true;
        _managedReconnecting = true;
        _managedPauseCommandId = 0;
        _managedPauseAccepted = false;
        _managedPauseNeedsSync = true;
        _managedRoomCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        _managedRoomOperation = RejoinManagedRoomAsync(_managedRoomRequest, _managedRoomCancellation.Token);
        _mainMenuOpen = true;
        _lastToDieMenuOpen = true;
        _lastToDieMenuPage = LastToDieMenuPage.RoomLoading;
        _menuStatusMessage = "Reconnecting to Last to Die...";
        return true;
    }

    private async Task<PrivateRoomResponse> RejoinManagedRoomAsync(PrivateRoomRequest request, CancellationToken token)
    {
        await Task.Delay(1000, token);
        return await GetPrivateRoomClient().JoinAsync(request, token);
    }

    private void ReconcileManagedSoloPause(LastToDieRunSnapshotMessage? snapshot)
    {
        if (!(IsManagedRoomOwner || IsEmbeddedSessionOwner) || snapshot is not { MaximumPlayers: 1, Phase: LastToDieWirePhase.Playing })
        {
            _managedPauseCommandId = 0;
            _managedPauseAccepted = false;
            return;
        }
        var desired = ShouldPauseHostedLastToDieSoloSimulation(
            true, _networkClient.IsConnected, snapshot.MaximumPlayers, snapshot.Phase,
            HasOpenGameplayOverlay() || !IsWindowInputActive, IsGameplayLoadingForMenuInput());
        var now = Environment.TickCount64;
        if (_managedPauseCommandId != 0
            && _networkClient.LastToDieState.TryGetCommandResult(_managedPauseCommandId, out var result))
        {
            if (result.Result == LastToDieCommandResultKind.Accepted)
            {
                _managedPauseAccepted = _managedPauseRequested;
                _managedPauseNeedsSync = false;
            }
            _managedPauseCommandId = 0;
        }
        if (now < _managedPauseRetryAt || (desired == _managedPauseAccepted && _managedPauseCommandId == 0 && !_managedPauseNeedsSync)) return;
        _managedPauseRequested = desired;
        _managedPauseCommandId = _networkClient.SendLastToDieCommand(desired ? LastToDieCommandKind.PauseSolo : LastToDieCommandKind.ResumeSolo);
        _managedPauseRetryAt = now + 1000;
    }

    private PrivateRoomClient GetPrivateRoomClient() => _privateRoomClient ??= new PrivateRoomClient(
        ClientRuntimeBootstrap.GetBrowserHttpClient() ?? new HttpClient(), ClientDistribution.RoomServiceOrigin);

    private PrivateRoomRequest CreatePrivateRoomRequest() => new(
        _clientIdentity.ClientId, _clientIdentity.ClientSecret, _clientIdentity.FriendCode,
        _world.LocalPlayer.DisplayName, RequestId: Guid.NewGuid().ToString("N"),
        ProtocolVersion: ProtocolVersion.Current, BuildVersion: ClientDistribution.BuildVersion,
        ContentId: ClientDistribution.ContentId);

    private void OpenManagedRoomJoin()
    {
        OpenLastToDieMenu();
        _lastToDieMenuPage = LastToDieMenuPage.RoomJoin;
        _lastToDieMenuHoverIndex = 1;
        _managedRoomCodeBuffer = string.Empty;
        _menuStatusMessage = "Enter the four-character room code.";
    }

    public bool HandleManagedRoomText(char character)
    {
        if (!_lastToDieMenuOpen || _lastToDieMenuPage != LastToDieMenuPage.RoomJoin) return false;
        if (character == '\b' && _managedRoomCodeBuffer.Length > 0)
            _managedRoomCodeBuffer = _managedRoomCodeBuffer[..^1];
        else if (char.IsAsciiLetterOrDigit(character) && _managedRoomCodeBuffer.Length < 24)
            _managedRoomCodeBuffer += char.ToUpperInvariant(character);
        else if (character == '-' && _managedRoomCodeBuffer.Length < 24)
            _managedRoomCodeBuffer += character;
        return true;
    }

    public void HandleBrowserRoomCodePaste(string text)
    {
        if (!OperatingSystem.IsBrowser() || !_lastToDieMenuOpen || _lastToDieMenuPage != LastToDieMenuPage.RoomJoin) return;
        _managedRoomCodeBuffer = string.Empty;
        foreach (var character in text) HandleManagedRoomText(character);
    }

    private async Task<PrivateRoomResponse> CreateManagedRoomAfterCleanupAsync(PrivateRoomRequest request,
        CancellationToken token, IProgress<string> progress)
    {
        try { await _managedRoomCleanup.WaitAsync(token); }
        catch (Exception) when (!token.IsCancellationRequested)
        {
            // The service remains authoritative if the earlier cleanup timed out.
            // Its owned-room response can resume waiting, including after reload.
        }
        token.ThrowIfCancellationRequested();
        return await GetPrivateRoomClient().CreateAndJoinAsync(request, token, progress);
    }


    private void RestoreManagedRoomOrigin()
    {
        _managedRoomFailureMessage = string.Empty;
        _lastToDieMenuPage = _managedRoomReturnPage;
        _lastToDieMenuHoverIndex = _managedRoomReturnPage == LastToDieMenuPage.RoomJoin ? 1
            : _managedRoomReturnPage == LastToDieMenuPage.Difficulty && _managedRoomRetryDifficulty == OpenGarrison.Core.LastToDie.LastToDieDifficulty.Hardcore ? 1 : 0;
        _menuStatusMessage = string.Empty;
    }

    private void ShowManagedRoomFailure(string message)
    {
        HideJoiningServerLoadingOverlay();
        _mainMenuOpen = true;
        _lastToDieMenuOpen = true;
        _lastToDieMenuPage = LastToDieMenuPage.RoomError;
        _lastToDieMenuHoverIndex = 0;
        _managedRoomFailureMessage = string.IsNullOrWhiteSpace(message) ? "The room could not start. Try again." : message;
        _menuStatusMessage = _managedRoomFailureMessage;
    }

    public string GetLastToDieMenuStatusMessage() => _lastToDieMenuPage == LastToDieMenuPage.RoomError
        ? _managedRoomFailureMessage : _menuStatusMessage;

    public void PumpManagedRoomOperation()
    {
        if (_managedRoomCopyTask is { IsCompleted: true } copy)
        {
            _managedRoomCopyTask = null;
            _hostedLastToDieRoomCodeCopyFailed = !copy.IsCompletedSuccessfully || !copy.Result;
            _hostedLastToDieRoomCodeFeedbackSeconds = 2f;
            if (copy.IsFaulted) _ = copy.Exception;
        }
        if (_managedReconnecting && _networkClient.LastToDieState.Snapshot is not null && _networkClient.IsConnected)
        {
            _managedReconnecting = false;
            _managedReconnectDeadline = 0;
        }
        if (_managedRoomOperation is not { IsCompleted: true } operation) return;
        _managedRoomOperation = null;
        _managedRoomCancellation?.Dispose();
        _managedRoomCancellation = null;
        if (!operation.IsCompletedSuccessfully)
        {
            var message = operation.IsCanceled ? "Room request cancelled." : operation.Exception?.GetBaseException().Message ?? "The room could not start.";
            if (_managedReconnecting)
            {
                if (TryReconnectManagedRoom()) return;
                ReturnToLastToDieMenu("The room connection could not be restored.");
                return;
            }
            CancelManagedRoomRequest();
            ShowManagedRoomFailure(message);
            return;
        }
        var room = operation.Result;
        if (!GetPrivateRoomClient().TryValidateGrant(room, ProtocolVersion.Current, ClientDistribution.ContentId, out var endpoint))
        {
            LeaveManagedRoom();
            ShowManagedRoomFailure("The room returned an incompatible session or address.");
            return;
        }
        _managedRoom = room;
        _managedRoomRequest = _managedRoomRequest! with { RoomId = room.RoomId };
        ClientDistribution.AuthorizeRoomEndpoint(endpoint!, DateTimeOffset.Parse(room.ExpiresAtIso));
        _hostedLastToDieRoomCode = room.RoomCode;
        _lastToDieConnectionPresentationPending = true;
        if (!TryConnectToServer(new NetworkEndpoint(endpoint!.Host, 0, 0, endpoint.AbsoluteUri), false))
        {
            if (_managedReconnecting && TryReconnectManagedRoom()) return;
            LeaveManagedRoom();
            ShowManagedRoomFailure("The room connection could not be established. Try again.");
            return;
        }
        _lastToDieMenuOpen = false;
    }

    public void CancelManagedRoomRequest()
    {
        HideJoiningServerLoadingOverlay();
        _managedRoomCancellation?.Cancel();
        _managedRoomCancellation?.Dispose();
        _managedRoomCancellation = null;
        if (_managedRoomOperation is { } pending) ObserveRoomCleanup(pending);
        _managedRoomOperation = null;
        if (_managedRoomRequest is { } request && _managedRoom is null)
            TrackManagedRoomCleanup(GetPrivateRoomClient().CancelAsync(request));
        if (_managedRoom is null) _managedRoomRequest = null;
    }

    public void LeaveManagedRoom()
    {
        CancelManagedRoomRequest();
        if (_managedRoom is not null && _managedRoomRequest is { } request)
            TrackManagedRoomCleanup(GetPrivateRoomClient().LeaveAsync(request));
        _managedRoom = null;
        _managedRoomRequest = null;
        _managedPauseCommandId = 0;
        _managedPauseAccepted = false;
        _managedPauseRetryAt = 0;
        _managedPauseNeedsSync = true;
        _managedReconnecting = false;
        _managedReconnectDeadline = 0;
        ClientDistribution.ClearRoomAccess();
    }

    private void TrackManagedRoomCleanup(Task cleanup)
    {
        // Cancelling a new allocation must not discard an older closing room.
        _managedRoomCleanup = _managedRoomCleanup.IsCompleted ? cleanup : Task.WhenAll(_managedRoomCleanup, cleanup);
        ObserveRoomCleanup(_managedRoomCleanup);
    }

    private static void ObserveRoomCleanup(Task task) => _ = task.ContinueWith(
        static completed => _ = completed.Exception,
        CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
}
