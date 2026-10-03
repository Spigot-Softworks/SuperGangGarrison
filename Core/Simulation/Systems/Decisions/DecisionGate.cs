namespace OpenGarrison.Core;

/// <summary>
/// The plugin decision gate: interceptors that can cancel spawns, damage, deaths,
/// pickups, score awards and round ends, and the helpers rules call to consult them.
/// The world exposes the interceptor properties as its public plugin surface.
/// </summary>
internal sealed class DecisionGate
{
    private readonly IDecisionGateHost _host;

    public DecisionGate(IDecisionGateHost host)
    {
        _host = host;
    }

    internal Func<WorldSpawnDecisionRequest, WorldDecisionResult>? SpawnDecisionInterceptor { get; set; }

    internal Func<WorldDamageDecisionRequest, WorldDecisionResult>? DamageDecisionInterceptor { get; set; }

    internal Func<WorldDeathDecisionRequest, WorldDecisionResult>? DeathDecisionInterceptor { get; set; }

    internal Func<WorldPickupDecisionRequest, WorldDecisionResult>? PickupDecisionInterceptor { get; set; }

    internal Func<WorldScoreDecisionRequest, WorldDecisionResult>? ScoreDecisionInterceptor { get; set; }

    internal Func<WorldRoundEndDecisionRequest, WorldDecisionResult>? RoundEndDecisionInterceptor { get; set; }

    internal bool ShouldCancelSpawn(PlayerEntity player, PlayerTeam team, float x, float y)
    {
        var interceptor = SpawnDecisionInterceptor;
        if (interceptor is null)
        {
            return false;
        }

        _ = _host.NetworkPlayerRules.TryGetPlayerNetworkSlot(player, out var slot);
        return interceptor(new WorldSpawnDecisionRequest(
            _host.Frame,
            slot,
            player.Id,
            player.DisplayName,
            team,
            player.ClassId,
            x,
            y,
            !player.IsAlive)).IsCancelled;
    }

    internal bool ShouldCancelDamage(
        DamageTargetKind targetKind,
        int targetEntityId,
        int targetPlayerId,
        PlayerTeam? targetTeam,
        PlayerEntity? attacker,
        int amount,
        bool wouldBeFatal,
        float x,
        float y)
    {
        var interceptor = DamageDecisionInterceptor;
        if (interceptor is null)
        {
            return false;
        }

        return interceptor(new WorldDamageDecisionRequest(
            _host.Frame,
            targetKind,
            targetEntityId,
            targetPlayerId,
            targetTeam,
            attacker?.Id ?? -1,
            attacker?.Team,
            amount,
            wouldBeFatal,
            x,
            y)).IsCancelled;
    }

    internal bool ShouldCancelDeath(
        PlayerEntity player,
        bool gibbed,
        PlayerEntity? killer,
        string? weaponSpriteName)
    {
        var interceptor = DeathDecisionInterceptor;
        if (interceptor is null)
        {
            return false;
        }

        _ = _host.NetworkPlayerRules.TryGetPlayerNetworkSlot(player, out var slot);
        return interceptor(new WorldDeathDecisionRequest(
            _host.Frame,
            slot,
            player.Id,
            player.DisplayName,
            player.Team,
            player.ClassId,
            killer?.Id ?? -1,
            killer?.DisplayName ?? string.Empty,
            killer?.Team,
            weaponSpriteName ?? string.Empty,
            gibbed)).IsCancelled;
    }

    internal bool ShouldCancelPickup(
        WorldPickupKind kind,
        PlayerEntity player,
        int pickupEntityId,
        string pickupValue,
        float x,
        float y)
    {
        var interceptor = PickupDecisionInterceptor;
        if (interceptor is null)
        {
            return false;
        }

        _ = _host.NetworkPlayerRules.TryGetPlayerNetworkSlot(player, out var slot);
        return interceptor(new WorldPickupDecisionRequest(
            _host.Frame,
            kind,
            slot,
            player.Id,
            player.DisplayName,
            player.Team,
            pickupEntityId,
            pickupValue,
            x,
            y)).IsCancelled;
    }

    internal bool TryAwardTeamScore(PlayerTeam team, int delta, string reason, int actorPlayerId = -1)
    {
        if (delta <= 0)
        {
            return false;
        }

        var interceptor = ScoreDecisionInterceptor;
        if (interceptor is not null
            && interceptor(new WorldScoreDecisionRequest(
                _host.Frame,
                team,
                delta,
                _host.RedCaps,
                _host.BlueCaps,
                actorPlayerId,
                reason)).IsCancelled)
        {
            return false;
        }

        if (team == PlayerTeam.Red)
        {
            _host.RedCaps += delta;
            return true;
        }

        if (team == PlayerTeam.Blue)
        {
            _host.BlueCaps += delta;
            return true;
        }

        return false;
    }

    internal bool TryEndRound(PlayerTeam? winnerTeam, string reason)
    {
        if (_host.MatchState.IsEnded)
        {
            return false;
        }

        var interceptor = RoundEndDecisionInterceptor;
        if (interceptor is not null
            && interceptor(new WorldRoundEndDecisionRequest(
                _host.Frame,
                _host.MatchRules.Mode,
                winnerTeam,
                _host.RedCaps,
                _host.BlueCaps,
                reason)).IsCancelled)
        {
            return false;
        }

        _host.MatchState = _host.MatchState with { Phase = MatchPhase.Ended, WinnerTeam = winnerTeam };
        _host.QueuePendingMapChange();
        return true;
    }
}
