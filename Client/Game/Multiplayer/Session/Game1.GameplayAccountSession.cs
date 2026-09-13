#nullable enable

using OpenGarrison.ClientShared;
using OpenGarrison.Protocol;

namespace OpenGarrison.Client;

public partial class Game1
{
    private static readonly TimeSpan GameplayAccountRefreshLeadTime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan GameplayAccountRetryDelay = TimeSpan.FromMinutes(1);
    private Task<GameplaySessionCreateResponse>? _gameplayAccountSessionTask;
    private ulong _pendingGameplayAccountAttachRequestId;
    private DateTimeOffset _gameplayAccountTokenExpiresAt = DateTimeOffset.MinValue;
    private DateTimeOffset _nextGameplayAccountAttachAttemptAt = DateTimeOffset.MinValue;
    private string _accountId = string.Empty;
    private long _accountLifetimePoints;
    private long _accountWalletBalance;
    private long _accountProfileRevision;
    private int _accountGlobalRank;
    private bool _accountGlobalRankKnown;
    private bool _accountIsProtected;
    private string _accountRecoveryKey = string.Empty;
    private string _accountStatusMessage = string.Empty;

    private void BeginGameplayAccountAttach()
    {
        if (IsRestrictedBrowserEdition) return;
        _gameplayAccountSessionTask = null;
        _pendingGameplayAccountAttachRequestId = 0;
        if (_networkClient.IsReplayConnection)
        {
            return;
        }

        _accountStatusMessage = "Connecting account...";
        _nextGameplayAccountAttachAttemptAt = DateTimeOffset.UtcNow + GameplayAccountRetryDelay;
        _gameplayAccountSessionTask = _presenceClient.CreateGameplaySessionAsync(_clientIdentity);
    }

    private void UpdateGameplayAccountAttach()
    {
        if (IsRestrictedBrowserEdition) return;
        var task = _gameplayAccountSessionTask;
        if (task is null)
        {
            var now = DateTimeOffset.UtcNow;
            if (_pendingGameplayAccountAttachRequestId == 0
                && _networkClient.IsConnected
                && !_networkClient.IsAwaitingWelcome
                && !_networkClient.IsReplayConnection
                && now >= _nextGameplayAccountAttachAttemptAt
                && _gameplayAccountTokenExpiresAt <= now + GameplayAccountRefreshLeadTime)
            {
                BeginGameplayAccountAttach();
            }
            return;
        }

        if (!task.IsCompleted)
        {
            return;
        }

        _gameplayAccountSessionTask = null;
        if (task.IsCanceled)
        {
            _gameplayAccountTokenExpiresAt = DateTimeOffset.MinValue;
            _accountStatusMessage = "Account connection was canceled.";
            return;
        }

        if (task.IsFaulted)
        {
            _gameplayAccountTokenExpiresAt = DateTimeOffset.MinValue;
            _accountStatusMessage = "Account service unavailable; persistent points are disabled for this match.";
            AddNetworkConsoleLine($"account session unavailable: {task.Exception?.GetBaseException().Message ?? "request failed"}");
            return;
        }

        var response = task.Result;
        if (string.IsNullOrWhiteSpace(response.GameplayToken))
        {
            _gameplayAccountTokenExpiresAt = DateTimeOffset.MinValue;
            _accountStatusMessage = "Account service returned an invalid gameplay session.";
            return;
        }

        if (!DateTimeOffset.TryParse(response.ExpiresAtIso, out var expiresAt))
        {
            _gameplayAccountTokenExpiresAt = DateTimeOffset.MinValue;
            _accountStatusMessage = "Account service returned an invalid gameplay-session expiry.";
            return;
        }

        _gameplayAccountTokenExpiresAt = expiresAt.ToUniversalTime();

        ApplyAccountProfile(response.Profile);
        if (!_networkClient.IsConnected || _networkClient.IsAwaitingWelcome || _networkClient.IsReplayConnection)
        {
            _gameplayAccountTokenExpiresAt = DateTimeOffset.MinValue;
            _accountStatusMessage = "Account session expired before it could be attached.";
            return;
        }

        _pendingGameplayAccountAttachRequestId = _networkClient.AttachGameplayAccount(response.GameplayToken);
        if (_pendingGameplayAccountAttachRequestId == 0)
        {
            _gameplayAccountTokenExpiresAt = DateTimeOffset.MinValue;
            _accountStatusMessage = "Account session expired before it could be attached.";
            return;
        }

        _accountStatusMessage = "Attaching account to server...";
    }

    private void HandleGameplayAccountAttachResult(GameplayAccountAttachResultMessage result)
    {
        if (result.RequestId == 0 || result.RequestId != _pendingGameplayAccountAttachRequestId)
        {
            return;
        }

        _pendingGameplayAccountAttachRequestId = 0;
        if (!result.Attached)
        {
            _gameplayAccountTokenExpiresAt = DateTimeOffset.MinValue;
            _nextGameplayAccountAttachAttemptAt = DateTimeOffset.UtcNow + GameplayAccountRetryDelay;
            _accountStatusMessage = string.IsNullOrWhiteSpace(result.Reason)
                ? "Persistent points are unavailable on this server."
                : result.Reason.Trim();
            AddNetworkConsoleLine($"account attach failed: {_accountStatusMessage}");
            return;
        }

        if (ClientIdentityDocument.TryNormalizeFriendCode(result.FriendCode, out var normalizedCode))
        {
            _clientIdentity.FriendCode = normalizedCode;
        }

        if (!string.IsNullOrWhiteSpace(result.DisplayName))
        {
            _clientIdentity.DisplayName = result.DisplayName.Trim();
        }

        _clientIdentity.Save();
        _accountLifetimePoints = Math.Max(0, result.LifetimePoints);
        _accountWalletBalance = Math.Max(0, result.WalletBalance);
        _accountProfileRevision = Math.Max(0, result.ProfileRevision);
        _accountStatusMessage = "Account connected. Persistent points are enabled.";
        AddNetworkConsoleLine($"account attached: {_clientIdentity.FriendCode}");
    }

    private void HandlePlayerPointsState(PlayerPointsStateMessage state)
    {
        if (state.ProfileRevision < _accountProfileRevision)
        {
            return;
        }

        _accountLifetimePoints = Math.Max(0, state.LifetimePoints);
        _accountWalletBalance = Math.Max(0, state.WalletBalance);
        _accountProfileRevision = Math.Max(0, state.ProfileRevision);
        if (state.GlobalRank >= 0)
        {
            _accountGlobalRank = state.GlobalRank;
            _accountGlobalRankKnown = true;
        }
    }

    private void ApplyAccountProfile(AccountProfileResponse profile)
    {
        var accountChanged = !string.IsNullOrWhiteSpace(_accountId)
            && !string.Equals(_accountId, profile.AccountId, StringComparison.Ordinal);
        if (accountChanged)
        {
            _accountGlobalRank = 0;
            _accountGlobalRankKnown = false;
            _accountRecoveryKey = string.Empty;
        }

        _accountId = profile.AccountId?.Trim() ?? string.Empty;
        _clientIdentity.ApplyAccountProfile(profile);
        _accountLifetimePoints = Math.Max(0, profile.LifetimePoints);
        _accountWalletBalance = Math.Max(0, profile.WalletBalance);
        _accountProfileRevision = Math.Max(0, profile.ProfileRevision);
        _accountIsProtected = profile.IsProtected;
        if (!string.IsNullOrWhiteSpace(profile.RecoveryKey))
        {
            _accountRecoveryKey = profile.RecoveryKey.Trim();
        }
        else if (!profile.IsProtected)
        {
            _accountRecoveryKey = string.Empty;
        }
    }
}
