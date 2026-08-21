namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private sealed record PendingLastToDieSpyAfterlifeDeath(
        bool Gibbed,
        int KillerPlayerId,
        string? WeaponSpriteName,
        DeadBodyAnimationKind DeadBodyAnimationKind,
        string? DeathCamMessage,
        SentryEntity? DeathCamSentry,
        string? KillFeedMessage,
        bool CreateDeathCam,
        bool SpawnRemains,
        bool ForceCorpseRemains,
        bool RecordKillFeed,
        int AssistingPlayerId);

    private readonly Dictionary<int, PendingLastToDieSpyAfterlifeDeath>
        _pendingLastToDieSpyAfterlifeDeathsByPlayerId = [];
    private readonly HashSet<byte> _lastToDieSpyAfterlifeDisconnectFailureSlots = [];

    public bool IsLastToDieSpyAfterlifeWindowActive(byte slot) =>
        TryGetNetworkPlayer(slot, out var player)
        && player.IsLastToDieSpyAfterlifeActive
        && _pendingLastToDieSpyAfterlifeDeathsByPlayerId.ContainsKey(player.Id);

    public bool ConsumeLastToDieSpyAfterlifeDisconnectFailure(byte slot) =>
        _lastToDieSpyAfterlifeDisconnectFailureSlots.Remove(slot);

    private bool TryStartLastToDieSpyAfterlife(
        PlayerEntity player,
        bool gibbed,
        PlayerEntity? killer,
        string? weaponSpriteName,
        DeadBodyAnimationKind deadBodyAnimationKind,
        string? deathCamMessage,
        SentryEntity? deathCamSentry,
        string? killFeedMessage,
        bool createDeathCam,
        bool spawnRemains,
        bool forceCorpseRemains,
        bool recordKillFeed,
        int assistingPlayerId)
    {
        if (!TryGetNetworkPlayerSlot(player, out _)
            || _pendingLastToDieSpyAfterlifeDeathsByPlayerId.ContainsKey(player.Id)
            || !player.TryStartLastToDieSpyAfterlife(Config.TicksPerSecond))
        {
            return false;
        }

        _pendingLastToDieSpyAfterlifeDeathsByPlayerId[player.Id] =
            new PendingLastToDieSpyAfterlifeDeath(
                gibbed,
                killer?.Id ?? -1,
                weaponSpriteName,
                deadBodyAnimationKind,
                deathCamMessage,
                deathCamSentry,
                killFeedMessage,
                createDeathCam,
                spawnRemains,
                forceCorpseRemains,
                recordKillFeed,
                assistingPlayerId);
        TryDropCarriedIntel(player);
        MarkPendingFatalPlayerDamageEventPrevented(player.Id);
        return true;
    }

    private bool TryCompleteExpiredLastToDieSpyAfterlife(PlayerEntity player)
    {
        if (!player.IsLastToDieSpyAfterlifeExpiryPending)
        {
            return false;
        }

        return CompleteLastToDieSpyAfterlifeFailure(player);
    }

    private bool CompleteLastToDieSpyAfterlifeFailure(PlayerEntity player)
    {
        if (!_pendingLastToDieSpyAfterlifeDeathsByPlayerId.Remove(
                player.Id,
                out var pendingDeath))
        {
            player.ResetLastToDieSpyAfterlifeDynamicState(preserveCooldown: true);
            return false;
        }

        player.PrepareLastToDieSpyAfterlifeFailure();
        var killer = pendingDeath.KillerPlayerId > 0
            ? FindPlayerById(pendingDeath.KillerPlayerId)
            : null;
        KillPlayer(
            player,
            pendingDeath.Gibbed,
            killer,
            pendingDeath.WeaponSpriteName,
            pendingDeath.DeadBodyAnimationKind,
            pendingDeath.DeathCamMessage,
            pendingDeath.DeathCamSentry,
            pendingDeath.KillFeedMessage,
            pendingDeath.CreateDeathCam,
            pendingDeath.SpawnRemains,
            pendingDeath.ForceCorpseRemains,
            pendingDeath.RecordKillFeed,
            pendingDeath.AssistingPlayerId,
            completingLastToDieSpyAfterlifeDeath: true);
        return true;
    }

    private void TryCompleteLastToDieSpyAfterlifeSuccess(
        PlayerEntity killer,
        PlayerEntity victim)
    {
        if (!killer.IsLastToDieSpyAfterlifeActive
            || ReferenceEquals(killer, victim)
            || killer.Team == victim.Team
            || !_pendingLastToDieSpyAfterlifeDeathsByPlayerId.Remove(killer.Id))
        {
            return;
        }

        killer.CompleteLastToDieSpyAfterlifeSuccess();
    }

    private bool TryFailLastToDieSpyAfterlifeOnDisconnect(byte slot, PlayerEntity player)
    {
        if (!player.IsLastToDieSpyAfterlifeActive
            || !_pendingLastToDieSpyAfterlifeDeathsByPlayerId.ContainsKey(player.Id))
        {
            return false;
        }

        var completed = CompleteLastToDieSpyAfterlifeFailure(player);
        if (completed)
        {
            _lastToDieSpyAfterlifeDisconnectFailureSlots.Add(slot);
        }

        return completed;
    }

    private void ResetLastToDieSpyAfterlifeRuntime(byte slot, PlayerEntity player)
    {
        _pendingLastToDieSpyAfterlifeDeathsByPlayerId.Remove(player.Id);
        _lastToDieSpyAfterlifeDisconnectFailureSlots.Remove(slot);
        player.ResetLastToDieSpyAfterlifeDynamicState();
    }
}
