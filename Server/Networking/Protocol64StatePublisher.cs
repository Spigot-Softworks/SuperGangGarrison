using OpenGarrison.Core;
using OpenGarrison.Protocol;

namespace OpenGarrison.Server;

/// <summary>
/// Builds protocol-64 authoritative state directly from the simulation world.
/// It is deliberately independent of the legacy snapshot string cache: every
/// player record carries its gameplay class identity inline.
/// </summary>
internal sealed class Protocol64StatePublisher
{
    private readonly SimulationWorld _world;
    private readonly Dictionary<(ushort Slot, ulong PlayerId), PlayerIdentityState> _playerIdentities = [];
    private readonly Dictionary<ulong, ProjectileIdentityState> _projectileIdentities = [];
    private readonly Dictionary<(ushort Slot, ulong PlayerId), Protocol64PlayerIdentity> _lastRoster = [];
    private readonly Dictionary<ulong, Protocol64ProjectileState> _lastProjectiles = [];
    private Protocol64ProjectileLifecycle[] _pendingProjectileLifecycles = [];
    private ulong _stateSequence;

    public Protocol64StatePublisher(SimulationWorld world)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
    }

    public Protocol64PlayerStateBatch BuildPlayerStateBatch(uint stateTick)
    {
        var players = _world.EnumerateReplicatedNetworkPlayers()
            .Select(entry => ToPlayerState(entry.Slot, entry.Player, stateTick))
            .ToArray();
        return new(NextSequence(), stateTick, players);
    }

    public Protocol64RosterState BuildRosterState(uint stateTick)
    {
        var players = _world.EnumerateReplicatedNetworkPlayers()
            .Select(entry => ToPlayerIdentity(entry.Slot, entry.Player))
            .ToArray();
        var current = players.ToDictionary(
            player => (player.Slot, player.PlayerId),
            player => player);
        var removed = _lastRoster
            .Where(pair => !current.ContainsKey(pair.Key))
            .Select(pair => pair.Value)
            .ToArray();
        _lastRoster.Clear();
        foreach (var pair in current)
        {
            _lastRoster[pair.Key] = pair.Value;
        }

        return new(NextSequence(), stateTick, players, removed);
    }

    public IReadOnlyList<Protocol64ProjectileState> BuildProjectileStates(uint stateTick)
        => BuildProjectiles(stateTick);

    public IReadOnlyList<Protocol64ProjectileLifecycle> BuildProjectileLifecycleEvents()
    {
        var events = _pendingProjectileLifecycles;
        _pendingProjectileLifecycles = [];
        return events;
    }

    public Protocol64StateResyncResponse BuildResyncResponse(
        Protocol64StateResyncRequest request,
        uint stateTick)
    {
        var players = _world.EnumerateReplicatedNetworkPlayers()
            .Select(entry => ToPlayerState(entry.Slot, entry.Player, stateTick))
            .ToArray();
        var projectiles = BuildProjectiles(stateTick);
        _pendingProjectileLifecycles = [];
        return new(
            request.RequestId,
            NextSequence(),
            stateTick,
            players,
            Array.Empty<Protocol64PlayerIdentity>(),
            projectiles,
            Array.Empty<Protocol64ProjectileIdentity>());
    }

    private Protocol64ProjectileState[] BuildProjectiles(uint stateTick)
    {
        var projectiles = new List<Protocol64ProjectileState>(
            _world.Shots.Count
            + _world.Bubbles.Count
            + _world.Blades.Count
            + _world.Needles.Count
            + _world.RevolverShots.Count
            + _world.Rockets.Count
            + _world.Flames.Count
            + _world.Flares.Count
            + _world.Mines.Count
            + _world.Grenades.Count);

        projectiles.AddRange(_world.Shots.Select(shot => ToProjectile(
            shot.Id,
            Protocol64ProjectileKind.Bullet,
            shot.OwnerId,
            shot.X,
            shot.Y,
            shot.VelocityX,
            shot.VelocityY,
            0f,
            shot.TicksRemaining,
            active: true,
            damage: 0,
            stateTick)));
        projectiles.AddRange(_world.Bubbles.Select(shot => ToProjectile(
            shot.Id,
            Protocol64ProjectileKind.Custom,
            shot.OwnerId,
            shot.X,
            shot.Y,
            shot.VelocityX,
            shot.VelocityY,
            0f,
            shot.TicksRemaining,
            active: true,
            damage: 0,
            stateTick)));
        projectiles.AddRange(_world.Blades.Select(shot => ToProjectile(
            shot.Id,
            Protocol64ProjectileKind.Blade,
            shot.OwnerId,
            shot.X,
            shot.Y,
            shot.VelocityX,
            shot.VelocityY,
            0f,
            shot.TicksRemaining,
            active: true,
            damage: 0,
            stateTick)));
        projectiles.AddRange(_world.Needles.Select(shot => ToProjectile(
            shot.Id,
            Protocol64ProjectileKind.Needle,
            shot.OwnerId,
            shot.X,
            shot.Y,
            shot.VelocityX,
            shot.VelocityY,
            0f,
            shot.TicksRemaining,
            active: true,
            damage: 0,
            stateTick)));
        projectiles.AddRange(_world.RevolverShots.Select(shot => ToProjectile(
            shot.Id,
            Protocol64ProjectileKind.RevolverShot,
            shot.OwnerId,
            shot.X,
            shot.Y,
            shot.VelocityX,
            shot.VelocityY,
            0f,
            shot.TicksRemaining,
            active: true,
            damage: 0,
            stateTick)));
        projectiles.AddRange(_world.Rockets.Select(rocket => ToProjectile(
            rocket.Id,
            Protocol64ProjectileKind.Rocket,
            rocket.OwnerId,
            rocket.X,
            rocket.Y,
            MathF.Cos(rocket.DirectionRadians) * rocket.Speed,
            MathF.Sin(rocket.DirectionRadians) * rocket.Speed,
            rocket.DirectionRadians,
            rocket.TicksRemaining,
            active: !rocket.IsFading,
            damage: 0,
            stateTick)));
        projectiles.AddRange(_world.Flames.Select(flame => ToProjectile(
            flame.Id,
            Protocol64ProjectileKind.Flame,
            flame.OwnerId,
            flame.X,
            flame.Y,
            flame.VelocityX,
            flame.VelocityY,
            0f,
            flame.TicksRemaining,
            active: true,
            damage: 0,
            stateTick)));
        projectiles.AddRange(_world.Flares.Select(flare => ToProjectile(
            flare.Id,
            Protocol64ProjectileKind.Flare,
            flare.OwnerId,
            flare.X,
            flare.Y,
            flare.VelocityX,
            flare.VelocityY,
            0f,
            flare.TicksRemaining,
            active: true,
            damage: 0,
            stateTick)));
        projectiles.AddRange(_world.Mines.Select(mine => ToProjectile(
            mine.Id,
            Protocol64ProjectileKind.Mine,
            mine.OwnerId,
            mine.X,
            mine.Y,
            mine.VelocityX,
            mine.VelocityY,
            0f,
            0,
            active: !mine.IsDestroyed,
            damage: Math.Max(0, (int)MathF.Round(mine.ExplosionDamage)),
            stateTick)));
        projectiles.AddRange(_world.Grenades.Select(grenade => ToProjectile(
            grenade.Id,
            Protocol64ProjectileKind.Grenade,
            grenade.OwnerId,
            grenade.X,
            grenade.Y,
            grenade.VelocityX,
            grenade.VelocityY,
            0f,
            grenade.FuseTicksLeft,
            active: true,
            damage: 0,
            stateTick)));

        var current = projectiles
            .Select(projectile => projectile with
            {
                Generation = GetProjectileGeneration(
                    projectile.EntityId,
                    projectile.EntityKind,
                    stateTick),
            })
            .ToArray();

        _pendingProjectileLifecycles = _lastProjectiles
            .Where(pair => !current.Any(projectile => projectile.EntityId == pair.Key))
            .Select(pair =>
            {
                var state = pair.Value;
                return new Protocol64ProjectileLifecycle(
                    Protocol64ProjectileLifecycleKind.Despawn,
                    state.EntityId,
                    state.Generation,
                    state.EntityKind,
                    stateTick,
                    state.OwnerSlot,
                    state.OwnerGeneration,
                    state.X,
                    state.Y,
                    state.VelocityX,
                    state.VelocityY,
                    state.Rotation,
                    IsActive: false,
                    state.RemainingLifetimeTicks,
                    state.Damage);
            })
            .ToArray();
        _lastProjectiles.Clear();
        foreach (var projectile in current)
        {
            _lastProjectiles[projectile.EntityId] = projectile;
        }

        return current;
    }

    private Protocol64ProjectileState ToProjectile(
        int entityId,
        Protocol64ProjectileKind kind,
        int ownerId,
        float x,
        float y,
        float velocityX,
        float velocityY,
        float rotation,
        int lifetimeTicks,
        bool active,
        int damage,
        uint stateTick)
    {
        var owner = _world.EnumerateReplicatedNetworkPlayers()
            .FirstOrDefault(entry => entry.Player.Id == ownerId);
        var ownerSlot = owner.Player is null ? (ushort)0 : owner.Slot;
        var ownerGeneration = owner.Player is null ? 0u : GetPlayerGeneration(owner.Slot, owner.Player);
        return new(
            checked((ulong)Math.Max(1, entityId)),
            checked((uint)Math.Max(1, entityId)),
            kind,
            stateTick,
            ownerSlot,
            ownerGeneration,
            x,
            y,
            velocityX,
            velocityY,
            rotation,
            active,
            checked((uint)Math.Max(0, lifetimeTicks)),
            Math.Max(0, damage));
    }

    private Protocol64PlayerState ToPlayerState(byte slot, PlayerEntity player, uint stateTick)
        => new(
            slot,
            checked((ulong)Math.Max(1, player.Id)),
            GetPlayerGeneration(slot, player),
            player.GameplayClassId,
            player.Health,
            player.MaxHealth,
            (byte)player.Team,
            player.IsAlive,
            player.X,
            player.Y,
            player.HorizontalSpeed,
            player.VerticalSpeed,
            (byte)player.SelectedGameplayEquippedSlot,
            checked((uint)Math.Max(0, player.GetReplicatedStateEntries().Count)),
            stateTick);

    private Protocol64PlayerIdentity ToPlayerIdentity(byte slot, PlayerEntity player)
        => new(
            slot,
            checked((ulong)Math.Max(1, player.Id)),
            GetPlayerGeneration(slot, player));

    private uint GetPlayerGeneration(byte slot, PlayerEntity player)
    {
        var playerId = checked((ulong)Math.Max(1, player.Id));
        var key = ((ushort)slot, playerId);
        if (!_playerIdentities.TryGetValue(key, out var existing))
        {
            existing = new PlayerIdentityState(player.GameplayClassId, 1);
        }
        else if (!string.Equals(existing.GameplayClassId, player.GameplayClassId, StringComparison.Ordinal))
        {
            existing = existing with
            {
                GameplayClassId = player.GameplayClassId,
                Generation = checked(existing.Generation + 1),
            };
        }

        _playerIdentities[key] = existing;
        return existing.Generation;
    }

    private uint GetProjectileGeneration(
        ulong entityId,
        Protocol64ProjectileKind kind,
        uint stateTick)
    {
        if (!_projectileIdentities.TryGetValue(entityId, out var existing))
        {
            existing = new ProjectileIdentityState(kind, 1, stateTick);
        }
        else if (existing.Kind != kind || existing.LastSeenTick + 1 < stateTick)
        {
            existing = existing with
            {
                Kind = kind,
                Generation = checked(existing.Generation + 1),
                LastSeenTick = stateTick,
            };
        }
        else
        {
            existing = existing with { LastSeenTick = stateTick };
        }

        _projectileIdentities[entityId] = existing;
        return existing.Generation;
    }

    private ulong NextSequence() => checked(++_stateSequence);

    private sealed record PlayerIdentityState(string GameplayClassId, uint Generation);

    private sealed record ProjectileIdentityState(
        Protocol64ProjectileKind Kind,
        uint Generation,
        uint LastSeenTick);
}
