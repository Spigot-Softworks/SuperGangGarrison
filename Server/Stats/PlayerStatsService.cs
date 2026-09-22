#nullable enable

using System.Collections.Concurrent;
using System.Globalization;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using OpenGarrison.Server.Plugins;

namespace OpenGarrison.Server;

internal sealed class PlayerStatsService : IDisposable
{
    private const int LeaderboardPageSize = 5;
    private const int MaximumPendingAwards = 256;
    private const int HealingFlushThreshold = 100;
    private const int DamageFlushThreshold = 250;
    private const int PolicyVersion = 1;

    private sealed record ObservedPlayerStats(
        float Points,
        int Kills,
        int Deaths,
        int Assists,
        int Caps,
        int HealPoints,
        int UnflushedHealing);

    private readonly record struct PendingDamageStats(int Dealt, int Received);

    private readonly SimulationWorld _world;
    private readonly Dictionary<byte, ClientSession> _clientsBySlot;
    private readonly Func<bool> _enabledGetter;
    private readonly bool _modeEligible;
    private readonly Action<ClientSession, IProtocolMessage> _send;
    private readonly Action<byte, string> _sendSystemMessage;
    private readonly Action<string> _log;
    private readonly Action<ClientSession>? _verifiedAccountAttached;
    private readonly Action<ClientSession>? _accountAttachmentCleared;
    private readonly IOpenGarrisonStatsApiClient _api;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly ConcurrentQueue<Action> _mainThreadCompletions = new();
    private readonly Dictionary<string, ObservedPlayerStats> _observedByClient = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PendingDamageStats> _pendingDamageByClient = new(StringComparer.Ordinal);
    private string _matchId = Guid.NewGuid().ToString("N");
    private long _lastObservedFrame = -1;
    private long _eventSequence;
    private int _pendingAwardCount;
    private bool _disposed;

    public PlayerStatsService(
        SimulationWorld world,
        Dictionary<byte, ClientSession> clientsBySlot,
        Func<bool> enabledGetter,
        bool modeEligible,
        Action<ClientSession, IProtocolMessage> send,
        Action<byte, string> sendSystemMessage,
        Action<string> log,
        string? apiBaseUrl = null,
        IOpenGarrisonStatsApiClient? api = null,
        Action<ClientSession>? verifiedAccountAttached = null,
        Action<ClientSession>? accountAttachmentCleared = null)
    {
        _world = world;
        _clientsBySlot = clientsBySlot;
        _enabledGetter = enabledGetter;
        _modeEligible = modeEligible;
        _send = send;
        _sendSystemMessage = sendSystemMessage;
        _log = log;
        _api = api ?? new OpenGarrisonStatsApiClient(apiBaseUrl);
        _verifiedAccountAttached = verifiedAccountAttached;
        _accountAttachmentCleared = accountAttachmentCleared;
    }

    public void HandleAttach(ClientSession client, GameplayAccountAttachRequestMessage request)
    {
        if (_disposed || request.RequestId == 0 || string.IsNullOrWhiteSpace(request.GameplayToken))
        {
            SendAttachFailure(client, request.RequestId, "Account session request is invalid.", clearAttachment: false);
            return;
        }

        if (request.RequestId <= client.LatestGameplayAccountAttachRequestId)
        {
            return;
        }

        client.LatestGameplayAccountAttachRequestId = request.RequestId;
        FlushPendingHealingForClient(client);
        FlushPendingDamageForClient(client);
        _observedByClient.Remove(GetObservationIdentity(client));
        ClearAttachment(client);

        var token = request.GameplayToken.Trim();
        var expectedInstance = client.ClientInstanceId;
        _ = ValidateAndAttachAsync(client, expectedInstance, request.RequestId, token, _shutdown.Token);
    }

    public bool TryHandleChatCommand(ClientSession client, string text)
    {
        if (!TryParsePointsCommand(text, out var command, out var argument))
        {
            return false;
        }

        switch (command)
        {
            case "":
            case "me":
            case "rank":
                QueueOwnPointsLookup(client);
                break;
            case "top":
            case "leaderboard":
            case "leaders":
                var page = 1;
                if (!string.IsNullOrWhiteSpace(argument)
                    && (!int.TryParse(argument, NumberStyles.Integer, CultureInfo.InvariantCulture, out page) || page <= 0))
                {
                    _sendSystemMessage(client.Slot, "Usage: !points top [page]");
                    break;
                }

                QueueLeaderboardLookup(client, Math.Clamp(page, 1, 200_000));
                break;
            case "help":
                _sendSystemMessage(client.Slot, "!points [me|rank] - show your lifetime points and global rank.");
                _sendSystemMessage(client.Slot, "!points top [page] - show the global points leaderboard.");
                break;
            default:
                _sendSystemMessage(client.Slot, "Usage: !points [me|rank|top [page]|leaderboard [page]|help]");
                break;
        }

        return true;
    }

    public void Tick()
    {
        while (_mainThreadCompletions.TryDequeue(out var completion))
        {
            completion();
        }

        foreach (var client in _clientsBySlot.Values)
        {
            if (!client.HasAttachedGameplayAccount
                && (client.ConfiguredAdminPermissions != OpenGarrisonServerAdminPermissions.None
                    || client.ServerTitleText.Length > 0))
            {
                _accountAttachmentCleared?.Invoke(client);
            }
        }

        var frame = (long)_world.Frame;
        if (frame == _lastObservedFrame)
        {
            return;
        }

        _lastObservedFrame = frame;
        ObserveAuthoritativeStats(frame);
    }

    public void HandleMapTransition()
    {
        FlushPendingHealing();
        FlushPendingDamage();
        _matchId = Guid.NewGuid().ToString("N");
        _observedByClient.Clear();
        _pendingDamageByClient.Clear();
        _lastObservedFrame = -1;
    }

    public void HandleClientDisconnecting(ClientSession client)
    {
        if (_disposed)
        {
            return;
        }

        FlushPendingHealingForClient(client);
        FlushPendingDamageForClient(client);
        _observedByClient.Remove(GetObservationIdentity(client));
    }

    public void HandleDamage(OpenGarrisonServerDamageEvent damageEvent)
    {
        if (_disposed || damageEvent.Amount <= 0)
        {
            return;
        }

        var attacker = FindClientByPlayerId(damageEvent.AttackerPlayerId);
        var victim = FindClientByPlayerId(damageEvent.VictimPlayerId);
        var isSelfDamage = attacker is not null && victim is not null && ReferenceEquals(attacker, victim);
        var isTeamDamage = damageEvent.AttackerTeam.HasValue
            && damageEvent.VictimTeam.HasValue
            && damageEvent.AttackerTeam.Value == damageEvent.VictimTeam.Value;
        if (attacker is not null && !isSelfDamage && !isTeamDamage && IsAwardEligible(attacker))
        {
            AccumulateDamage(attacker, damageEvent.Frame, damageEvent.Amount, dealt: true);
        }

        if (victim is not null && !isSelfDamage && !isTeamDamage && IsAwardEligible(victim))
        {
            AccumulateDamage(victim, damageEvent.Frame, damageEvent.Amount, dealt: false);
        }
    }

    public void HandleRoundEnded(RoundEndedEvent roundEndedEvent)
    {
        if (_disposed || !_enabledGetter() || !_modeEligible)
        {
            return;
        }

        foreach (var client in _clientsBySlot.Values)
        {
            if (!IsRoundResultEligible(client))
            {
                continue;
            }

            QueueCounterDelta(client, roundEndedEvent.Frame, "rounds_played", 1);
            var team = _world.GetNetworkPlayerConfiguredTeam(client.Slot);
            QueueCounterDelta(
                client,
                roundEndedEvent.Frame,
                roundEndedEvent.WinnerTeam switch
                {
                    null => "draws",
                    var winner when winner == team => "wins",
                    _ => "losses",
                },
                1);
        }
    }

    public void HandleLastToDieRunEnded(
        Guid runId,
        int roundNumber,
        IReadOnlyDictionary<byte, int> scoreUnitsBySlot,
        IReadOnlyDictionary<byte, string> survivorIdsBySlot,
        OpenGarrison.Core.LastToDie.LastToDieDifficulty difficulty)
    {
        if (_disposed || runId == Guid.Empty || roundNumber < 0)
        {
            return;
        }

        var submissions = _clientsBySlot.Values
            .Where(client => client.HasAttachedGameplayAccount
                && scoreUnitsBySlot.ContainsKey(client.Slot))
            .GroupBy(client => client.AccountId, StringComparer.Ordinal)
            .Select(group => group
                .OrderByDescending(client => scoreUnitsBySlot.GetValueOrDefault(client.Slot))
                .First())
            .ToArray();
        foreach (var client in submissions)
        {
            if (Interlocked.Increment(ref _pendingAwardCount) > MaximumPendingAwards)
            {
                Interlocked.Decrement(ref _pendingAwardCount);
                _log($"[stats] award queue full; dropped Last to Die run for slot={client.Slot}");
                continue;
            }

            var expectedInstance = client.ClientInstanceId;
            var expectedAccountId = client.AccountId;
            var request = new LastToDieRunRequest
            {
                GameplayToken = client.GameplayToken,
                SubmissionId = $"{runId:N}:{client.AccountId}",
                RunId = runId.ToString("N"),
                ScoreUnits = Math.Max(0, scoreUnitsBySlot.GetValueOrDefault(client.Slot)),
                RoundNumber = roundNumber,
                Difficulty = difficulty.ToString().ToLowerInvariant(),
                SurvivorId = survivorIdsBySlot.GetValueOrDefault(client.Slot) ?? string.Empty,
                PolicyVersion = OpenGarrison.Core.LastToDie.LastToDieRuleset.CurrentVersion,
            };
            _ = SubmitLastToDieRunAsync(
                client,
                expectedInstance,
                expectedAccountId,
                request,
                _shutdown.Token);
        }
    }

    private async Task SubmitLastToDieRunAsync(
        ClientSession client,
        Guid expectedInstance,
        string expectedAccountId,
        LastToDieRunRequest request,
        CancellationToken cancellationToken)
    {
        Exception? finalError = null;
        try
        {
            for (var attempt = 0; attempt < 3; attempt += 1)
            {
                try
                {
                    var response = await _api.RecordLastToDieRunAsync(request, cancellationToken).ConfigureAwait(false);
                    _mainThreadCompletions.Enqueue(() =>
                    {
                        if (IsCurrentAccountSession(client, expectedInstance, expectedAccountId))
                        {
                            _log(
                                $"[stats] Last to Die run recorded account={expectedAccountId} " +
                                $"round={request.RoundNumber} scoreUnits={request.ScoreUnits} applied={response.Applied}");
                        }
                    });
                    return;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    finalError = ex;
                    if (attempt < 2)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(150 * (1 << attempt)), cancellationToken).ConfigureAwait(false);
                    }
                }
            }
        }
        finally
        {
            Interlocked.Decrement(ref _pendingAwardCount);
        }

        if (finalError is not null)
        {
            _log($"[stats] dropped Last to Die run submission={request.SubmissionId} after retries: {finalError.Message}");
        }
    }

    private async Task ValidateAndAttachAsync(
        ClientSession client,
        Guid expectedInstance,
        ulong requestId,
        string token,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _api.ValidateGameplaySessionAsync(token, cancellationToken).ConfigureAwait(false);
            _mainThreadCompletions.Enqueue(() =>
            {
                if (!IsCurrentAttachRequest(client, expectedInstance, requestId))
                {
                    return;
                }

                if (!response.Valid
                    || !Guid.TryParse(response.ClientId, out var validatedClientId)
                    || validatedClientId != expectedInstance
                    || string.IsNullOrWhiteSpace(response.Profile.AccountId)
                    || !DateTimeOffset.TryParse(response.ExpiresAtIso, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var expiresAt))
                {
                    SendAttachFailure(client, requestId, "Account session could not be validated.");
                    return;
                }

                client.AccountId = response.Profile.AccountId;
                client.GameplayToken = token;
                client.GameplayTokenExpiresAt = expiresAt.ToUniversalTime();
                ApplyProfile(client, response.Profile);
                _verifiedAccountAttached?.Invoke(client);
                _send(client, new GameplayAccountAttachResultMessage(
                    requestId,
                    Attached: true,
                    Reason: string.Empty,
                    response.Profile.FriendCode,
                    response.Profile.DisplayName,
                    response.Profile.LifetimePoints,
                    response.Profile.WalletBalance,
                    response.Profile.ProfileRevision));
                SendPointsState(client, globalRank: -1);
                _log($"[stats] attached account={client.AccountId} slot={client.Slot}");
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _mainThreadCompletions.Enqueue(() =>
            {
                if (IsCurrentAttachRequest(client, expectedInstance, requestId))
                {
                    SendAttachFailure(client, requestId, "Account service is unavailable; this match will not award persistent points.");
                    _log($"[stats] account attach failed slot={client.Slot}: {ex.Message}");
                }
            });
        }
    }

    private void ObserveAuthoritativeStats(long frame)
    {
        var activeIdentities = new HashSet<string>(StringComparer.Ordinal);
        foreach (var client in _clientsBySlot.Values)
        {
            var identity = GetObservationIdentity(client);
            activeIdentities.Add(identity);
            if (!_world.TryGetNetworkPlayer(client.Slot, out var player))
            {
                _pendingDamageByClient.Remove(identity);
                continue;
            }

            var current = new ObservedPlayerStats(
                player.Points,
                player.Kills,
                player.Deaths,
                player.Assists,
                player.Caps,
                player.HealPoints,
                0);
            if (!_observedByClient.TryGetValue(identity, out var previous))
            {
                _observedByClient[identity] = current;
                continue;
            }

            var healingDelta = Math.Max(0, current.HealPoints - previous.HealPoints);
            var unflushedHealing = previous.UnflushedHealing + healingDelta;
            var eligible = IsAwardEligible(client);
            if (eligible)
            {
                var pointUnits = (int)Math.Round(
                    Math.Max(0f, current.Points - previous.Points) * 100f,
                    MidpointRounding.AwayFromZero);
                if (pointUnits > 0)
                {
                    QueueAward(client, frame, "score", pointUnits, pointUnits, pointUnits);
                }

                QueueCounterDelta(client, frame, "kills", current.Kills - previous.Kills);
                QueueCounterDelta(client, frame, "deaths", current.Deaths - previous.Deaths);
                QueueCounterDelta(client, frame, "assists", current.Assists - previous.Assists);
                QueueCounterDelta(client, frame, "captures", current.Caps - previous.Caps);
                if (unflushedHealing >= HealingFlushThreshold || pointUnits > 0)
                {
                    QueueCounterDelta(client, frame, "healing", unflushedHealing);
                    unflushedHealing = 0;
                }
            }
            else
            {
                unflushedHealing = 0;
                _pendingDamageByClient.Remove(identity);
            }

            _observedByClient[identity] = current with { UnflushedHealing = unflushedHealing };
        }

        foreach (var staleIdentity in _observedByClient.Keys.Where(identity => !activeIdentities.Contains(identity)).ToArray())
        {
            _observedByClient.Remove(staleIdentity);
            _pendingDamageByClient.Remove(staleIdentity);
        }
    }

    private void AccumulateDamage(ClientSession client, long frame, int amount, bool dealt)
    {
        var identity = GetObservationIdentity(client);
        var pending = _pendingDamageByClient.GetValueOrDefault(identity);
        pending = dealt
            ? pending with { Dealt = checked(pending.Dealt + amount) }
            : pending with { Received = checked(pending.Received + amount) };
        _pendingDamageByClient[identity] = pending;

        if (pending.Dealt >= DamageFlushThreshold)
        {
            QueueCounterDelta(client, frame, "damage_dealt", pending.Dealt);
            pending = pending with { Dealt = 0 };
        }

        if (pending.Received >= DamageFlushThreshold)
        {
            QueueCounterDelta(client, frame, "damage_received", pending.Received);
            pending = pending with { Received = 0 };
        }

        _pendingDamageByClient[identity] = pending;
    }

    private bool IsAwardEligible(ClientSession client)
    {
        return _enabledGetter()
            && _modeEligible
            && client.IsAuthorized
            && client.HasAttachedGameplayAccount
            && !ServerHelpers.IsSpectatorSlot(client.Slot)
            && !_world.IsNetworkPlayerAwaitingJoin(client.Slot)
            && !_world.MatchState.IsEnded
            && !_world.CompetitiveObjectivesLocked
            && !_world.VipWarmupActive;
    }

    private bool IsRoundResultEligible(ClientSession client)
    {
        return client.IsAuthorized
            && client.HasAttachedGameplayAccount
            && !ServerHelpers.IsSpectatorSlot(client.Slot)
            && !_world.IsNetworkPlayerAwaitingJoin(client.Slot)
            && !_world.VipWarmupActive;
    }

    private ClientSession? FindClientByPlayerId(int playerId)
    {
        if (playerId <= 0)
        {
            return null;
        }

        foreach (var client in _clientsBySlot.Values)
        {
            if (_world.TryGetNetworkPlayer(client.Slot, out var player) && player.Id == playerId)
            {
                return client;
            }
        }

        return null;
    }

    private void QueueCounterDelta(ClientSession client, long frame, string eventType, int delta)
    {
        if (delta > 0)
        {
            QueueAward(client, frame, eventType, delta, pointsDelta: 0, creditsDelta: 0);
        }
    }

    private void QueueAward(
        ClientSession client,
        long frame,
        string eventType,
        int rawValue,
        int pointsDelta,
        int creditsDelta)
    {
        if (Interlocked.Increment(ref _pendingAwardCount) > MaximumPendingAwards)
        {
            Interlocked.Decrement(ref _pendingAwardCount);
            _log($"[stats] award queue full; dropped {eventType} for slot={client.Slot}");
            return;
        }

        var expectedInstance = client.ClientInstanceId;
        var expectedAccountId = client.AccountId;
        var request = new StatAwardRequest
        {
            GameplayToken = client.GameplayToken,
            EventId = $"{_matchId}:{client.AccountId}:{frame}:{Interlocked.Increment(ref _eventSequence)}",
            MatchId = _matchId,
            SourceFrame = frame,
            EventType = eventType,
            RawValue = rawValue,
            PointsDelta = pointsDelta,
            CreditsDelta = creditsDelta,
            PolicyVersion = PolicyVersion,
        };
        _ = SubmitAwardAsync(client, expectedInstance, expectedAccountId, request, _shutdown.Token);
    }

    private async Task SubmitAwardAsync(
        ClientSession client,
        Guid expectedInstance,
        string expectedAccountId,
        StatAwardRequest request,
        CancellationToken cancellationToken)
    {
        Exception? finalError = null;
        try
        {
            for (var attempt = 0; attempt < 3; attempt += 1)
            {
                try
                {
                    var response = await _api.AwardAsync(request, cancellationToken).ConfigureAwait(false);
                    _mainThreadCompletions.Enqueue(() =>
                    {
                        if (IsCurrentAccountSession(client, expectedInstance, expectedAccountId)
                            && string.Equals(response.Profile.AccountId, expectedAccountId, StringComparison.Ordinal))
                        {
                            ApplyProfile(client, response.Profile);
                            if (request.PointsDelta > 0)
                            {
                                SendPointsState(client, globalRank: -1);
                            }
                        }
                    });
                    return;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    finalError = ex;
                    if (attempt < 2)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(150 * (1 << attempt)), cancellationToken).ConfigureAwait(false);
                    }
                }
            }
        }
        finally
        {
            Interlocked.Decrement(ref _pendingAwardCount);
        }

        if (finalError is not null)
        {
            _log($"[stats] dropped award event={request.EventId} after retries: {finalError.Message}");
        }
    }

    private void QueueOwnPointsLookup(ClientSession client)
    {
        if (!client.HasAttachedGameplayAccount)
        {
            _sendSystemMessage(client.Slot, "Persistent points are unavailable until your account is attached. Reconnect after signing in under Settings > Account.");
            return;
        }

        var expectedInstance = client.ClientInstanceId;
        var token = client.GameplayToken;
        _ = RunOwnPointsLookupAsync(client, expectedInstance, token, _shutdown.Token);
    }

    private async Task RunOwnPointsLookupAsync(
        ClientSession client,
        Guid expectedInstance,
        string token,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _api.GetPlayerPointsAsync(token, cancellationToken).ConfigureAwait(false);
            _mainThreadCompletions.Enqueue(() =>
            {
                if (!IsCurrentSession(client, expectedInstance))
                {
                    return;
                }

                if (!string.Equals(client.AccountId, response.Profile.AccountId, StringComparison.Ordinal))
                {
                    return;
                }

                ApplyProfile(client, response.Profile);
                SendPointsState(client, response.GlobalRank);
                _sendSystemMessage(
                    client.Slot,
                    response.GlobalRank > 0
                        ? $"{response.Profile.DisplayName}: {response.Profile.LifetimePoints:N0} points, rank #{response.GlobalRank:N0}. Wallet: {response.Profile.WalletBalance:N0}."
                        : $"{response.Profile.DisplayName}: {response.Profile.LifetimePoints:N0} points, unranked. Wallet: {response.Profile.WalletBalance:N0}.");
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _mainThreadCompletions.Enqueue(() =>
            {
                if (IsCurrentSession(client, expectedInstance))
                {
                    _sendSystemMessage(client.Slot, "The points service is temporarily unavailable.");
                    _log($"[stats] points lookup failed slot={client.Slot}: {ex.Message}");
                }
            });
        }
    }

    private void QueueLeaderboardLookup(ClientSession client, int page)
    {
        var expectedInstance = client.ClientInstanceId;
        _ = RunLeaderboardLookupAsync(client, expectedInstance, page, _shutdown.Token);
    }

    private async Task RunLeaderboardLookupAsync(
        ClientSession client,
        Guid expectedInstance,
        int page,
        CancellationToken cancellationToken)
    {
        try
        {
            var offset = checked((page - 1) * LeaderboardPageSize);
            var response = await _api.GetLeaderboardAsync(LeaderboardPageSize, offset, cancellationToken).ConfigureAwait(false);
            _mainThreadCompletions.Enqueue(() =>
            {
                if (!IsCurrentSession(client, expectedInstance))
                {
                    return;
                }

                var totalPages = Math.Max(1, (int)Math.Ceiling(response.Total / (double)LeaderboardPageSize));
                _sendSystemMessage(client.Slot, $"Points leaderboard - page {Math.Min(page, totalPages)}/{totalPages}:");
                if (response.Entries.Count == 0)
                {
                    _sendSystemMessage(client.Slot, "No ranked players on this page.");
                    return;
                }

                foreach (var entry in response.Entries)
                {
                    _sendSystemMessage(client.Slot, $"#{entry.Rank:N0} {entry.DisplayName}: {entry.LifetimePoints:N0} points");
                }
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _mainThreadCompletions.Enqueue(() =>
            {
                if (IsCurrentSession(client, expectedInstance))
                {
                    _sendSystemMessage(client.Slot, "The points leaderboard is temporarily unavailable.");
                    _log($"[stats] leaderboard lookup failed slot={client.Slot}: {ex.Message}");
                }
            });
        }
    }

    private void FlushPendingHealing()
    {
        foreach (var client in _clientsBySlot.Values)
        {
            FlushPendingHealingForClient(client);
        }
    }

    private void FlushPendingHealingForClient(ClientSession client)
    {
        var identity = GetObservationIdentity(client);
        if (!_observedByClient.TryGetValue(identity, out var observed)
            || observed.UnflushedHealing <= 0
            || !CanFlushBufferedStats(client))
        {
            return;
        }

        QueueCounterDelta(client, (long)_world.Frame, "healing", observed.UnflushedHealing);
        _observedByClient[identity] = observed with { UnflushedHealing = 0 };
    }

    private void FlushPendingDamage()
    {
        foreach (var client in _clientsBySlot.Values)
        {
            FlushPendingDamageForClient(client);
        }
    }

    private void FlushPendingDamageForClient(ClientSession client)
    {
        var identity = GetObservationIdentity(client);
        if (!_pendingDamageByClient.Remove(identity, out var pending) || !CanFlushBufferedStats(client))
        {
            return;
        }

        QueueCounterDelta(client, (long)_world.Frame, "damage_dealt", pending.Dealt);
        QueueCounterDelta(client, (long)_world.Frame, "damage_received", pending.Received);
    }

    private bool IsCurrentSession(ClientSession client, Guid expectedInstance)
    {
        return _clientsBySlot.Values.Any(candidate =>
            ReferenceEquals(candidate, client)
            && candidate.ClientInstanceId == expectedInstance);
    }

    private bool IsCurrentAccountSession(ClientSession client, Guid expectedInstance, string expectedAccountId)
        => IsCurrentSession(client, expectedInstance)
            && client.HasAttachedGameplayAccount
            && string.Equals(client.AccountId, expectedAccountId, StringComparison.Ordinal);

    private bool CanFlushBufferedStats(ClientSession client)
        => _modeEligible
            && client.IsAuthorized
            && client.HasAttachedGameplayAccount;

    private bool IsCurrentAttachRequest(ClientSession client, Guid expectedInstance, ulong requestId)
        => IsCurrentSession(client, expectedInstance)
            && client.LatestGameplayAccountAttachRequestId == requestId;

    private void SendAttachFailure(
        ClientSession client,
        ulong requestId,
        string reason,
        bool clearAttachment = true)
    {
        if (clearAttachment)
        {
            ClearAttachment(client);
        }
        _send(client, new GameplayAccountAttachResultMessage(
            requestId,
            Attached: false,
            reason,
            client.FriendCode,
            client.Name,
            client.LifetimePoints,
            client.WalletBalance,
            client.AccountProfileRevision));
    }

    private void ClearAttachment(ClientSession client)
    {
        client.AccountId = string.Empty;
        client.VerifiedFriendCode = string.Empty;
        client.GameplayToken = string.Empty;
        client.GameplayTokenExpiresAt = DateTimeOffset.MinValue;
        _accountAttachmentCleared?.Invoke(client);
    }

    private static void ApplyProfile(ClientSession client, StatsAccountProfile profile)
    {
        if (!string.IsNullOrWhiteSpace(profile.FriendCode))
        {
            client.VerifiedFriendCode = profile.FriendCode;
            client.FriendCode = profile.FriendCode;
        }

        if (profile.ProfileRevision < client.AccountProfileRevision)
        {
            return;
        }

        client.LifetimePoints = Math.Max(0, profile.LifetimePoints);
        client.WalletBalance = Math.Max(0, profile.WalletBalance);
        client.AccountProfileRevision = Math.Max(0, profile.ProfileRevision);
    }

    private void SendPointsState(ClientSession client, int globalRank)
    {
        _send(client, new PlayerPointsStateMessage(
            client.LifetimePoints,
            client.WalletBalance,
            globalRank,
            client.AccountProfileRevision));
    }

    private static string GetObservationIdentity(ClientSession client)
        => client.ClientInstanceId != Guid.Empty
            ? client.ClientInstanceId.ToString("D")
            : $"session-{client.UserId}";

    private static bool TryParsePointsCommand(string text, out string command, out string argument)
    {
        command = string.Empty;
        argument = string.Empty;
        if (!text.Equals("!points", StringComparison.OrdinalIgnoreCase)
            && !text.StartsWith("!points ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var remainder = text.Length == "!points".Length ? string.Empty : text["!points".Length..].Trim();
        var separator = remainder.IndexOf(' ');
        command = (separator < 0 ? remainder : remainder[..separator]).Trim().ToLowerInvariant();
        argument = separator < 0 ? string.Empty : remainder[(separator + 1)..].Trim();
        return true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        FlushPendingHealing();
        FlushPendingDamage();
        var drainDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        while (Volatile.Read(ref _pendingAwardCount) > 0 && DateTime.UtcNow < drainDeadline)
        {
            Thread.Sleep(10);
        }

        _shutdown.Cancel();
        _shutdown.Dispose();
    }
}
