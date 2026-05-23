namespace OpenGarrison.Core;

public readonly record struct WorldDecisionResult(
    bool IsCancelled,
    string Reason = "")
{
    public static WorldDecisionResult Continue { get; } = new(false);

    public static WorldDecisionResult Cancel(string reason = "") => new(true, reason);
}

public readonly record struct WorldSpawnDecisionRequest(
    long Frame,
    byte Slot,
    int PlayerId,
    string PlayerName,
    PlayerTeam Team,
    PlayerClass PlayerClass,
    float WorldX,
    float WorldY,
    bool IsRespawn);

public readonly record struct WorldDamageDecisionRequest(
    long Frame,
    DamageTargetKind TargetKind,
    int TargetEntityId,
    int TargetPlayerId,
    PlayerTeam? TargetTeam,
    int AttackerPlayerId,
    PlayerTeam? AttackerTeam,
    int Amount,
    bool WouldBeFatal,
    float WorldX,
    float WorldY);

public readonly record struct WorldDeathDecisionRequest(
    long Frame,
    byte Slot,
    int VictimPlayerId,
    string VictimName,
    PlayerTeam VictimTeam,
    PlayerClass VictimClass,
    int KillerPlayerId,
    string KillerName,
    PlayerTeam? KillerTeam,
    string WeaponSpriteName,
    bool Gibbed);

public enum WorldPickupKind : byte
{
    HealthPack = 0,
    DroppedWeapon = 1,
    Intelligence = 2,
}

public readonly record struct WorldPickupDecisionRequest(
    long Frame,
    WorldPickupKind Kind,
    byte Slot,
    int PlayerId,
    string PlayerName,
    PlayerTeam Team,
    int PickupEntityId,
    string PickupValue,
    float WorldX,
    float WorldY);

public readonly record struct WorldScoreDecisionRequest(
    long Frame,
    PlayerTeam Team,
    int Delta,
    int RedCaps,
    int BlueCaps,
    int ActorPlayerId,
    string Reason);

public readonly record struct WorldRoundEndDecisionRequest(
    long Frame,
    GameModeKind GameMode,
    PlayerTeam? WinnerTeam,
    int RedCaps,
    int BlueCaps,
    string Reason);

public sealed partial class SimulationWorld
{
    public Func<WorldSpawnDecisionRequest, WorldDecisionResult>? SpawnDecisionInterceptor { get; set; }

    public Func<WorldDamageDecisionRequest, WorldDecisionResult>? DamageDecisionInterceptor { get; set; }

    public Func<WorldDeathDecisionRequest, WorldDecisionResult>? DeathDecisionInterceptor { get; set; }

    public Func<WorldPickupDecisionRequest, WorldDecisionResult>? PickupDecisionInterceptor { get; set; }

    public Func<WorldScoreDecisionRequest, WorldDecisionResult>? ScoreDecisionInterceptor { get; set; }

    public Func<WorldRoundEndDecisionRequest, WorldDecisionResult>? RoundEndDecisionInterceptor { get; set; }

    private bool ShouldCancelSpawn(PlayerEntity player, PlayerTeam team, float x, float y)
    {
        var interceptor = SpawnDecisionInterceptor;
        if (interceptor is null)
        {
            return false;
        }

        _ = TryGetPlayerNetworkSlot(player, out var slot);
        return interceptor(new WorldSpawnDecisionRequest(
            Frame,
            slot,
            player.Id,
            player.DisplayName,
            team,
            player.ClassId,
            x,
            y,
            !player.IsAlive)).IsCancelled;
    }

    private bool ShouldCancelDamage(
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
            Frame,
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

    private bool ShouldCancelDeath(
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

        _ = TryGetPlayerNetworkSlot(player, out var slot);
        return interceptor(new WorldDeathDecisionRequest(
            Frame,
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

    private bool ShouldCancelPickup(
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

        _ = TryGetPlayerNetworkSlot(player, out var slot);
        return interceptor(new WorldPickupDecisionRequest(
            Frame,
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

    private bool TryAwardTeamScore(PlayerTeam team, int delta, string reason, int actorPlayerId = -1)
    {
        if (delta <= 0)
        {
            return false;
        }

        var interceptor = ScoreDecisionInterceptor;
        if (interceptor is not null
            && interceptor(new WorldScoreDecisionRequest(
                Frame,
                team,
                delta,
                RedCaps,
                BlueCaps,
                actorPlayerId,
                reason)).IsCancelled)
        {
            return false;
        }

        if (team == PlayerTeam.Red)
        {
            RedCaps += delta;
            return true;
        }

        if (team == PlayerTeam.Blue)
        {
            BlueCaps += delta;
            return true;
        }

        return false;
    }

    private bool TryEndRound(PlayerTeam? winnerTeam, string reason)
    {
        if (MatchState.IsEnded)
        {
            return false;
        }

        var interceptor = RoundEndDecisionInterceptor;
        if (interceptor is not null
            && interceptor(new WorldRoundEndDecisionRequest(
                Frame,
                MatchRules.Mode,
                winnerTeam,
                RedCaps,
                BlueCaps,
                reason)).IsCancelled)
        {
            return false;
        }

        MatchState = MatchState with { Phase = MatchPhase.Ended, WinnerTeam = winnerTeam };
        QueuePendingMapChange();
        return true;
    }
}
