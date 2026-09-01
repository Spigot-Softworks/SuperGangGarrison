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
    private readonly Func<byte, uint> _lastProcessedInputSequenceProvider;
    private readonly Dictionary<(ushort Slot, ulong PlayerId), PlayerIdentityState> _playerIdentities = [];
    private readonly HashSet<(ushort Slot, ulong PlayerId)> _activePlayerIdentities = [];
    private readonly Dictionary<ulong, ProjectileIdentityState> _projectileIdentities = [];
    private readonly Dictionary<(ushort Slot, ulong PlayerId), Protocol64PlayerIdentity> _lastRoster = [];
    private readonly Dictionary<ulong, Protocol64ProjectileState> _lastProjectiles = [];
    private Protocol64ProjectileLifecycle[] _pendingProjectileLifecycles = [];
    private ulong _stateSequence;

    public Protocol64StatePublisher(
        SimulationWorld world,
        Func<byte, uint>? lastProcessedInputSequenceProvider = null)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _lastProcessedInputSequenceProvider = lastProcessedInputSequenceProvider ?? (_ => 0u);
    }

    public Protocol64PlayerStateBatch BuildPlayerStateBatch(uint stateTick, byte? viewerSlot = null)
    {
        var entries = _world.EnumerateReplicatedNetworkPlayers().ToArray();
        PreparePlayerIdentities(entries);
        var viewer = ResolveViewer(viewerSlot);
        var players = entries
            .Where(entry => !SnapshotBroadcaster.ShouldHideSpyFromViewer(entry.Player, viewer))
            .Select(entry => ToPlayerState(entry.Slot, entry.Player, stateTick, viewer))
            .ToArray();
        return new(NextSequence(), stateTick, players);
    }

    public Protocol64RosterState BuildRosterState(uint stateTick)
    {
        var entries = _world.EnumerateReplicatedNetworkPlayers().ToArray();
        PreparePlayerIdentities(entries);
        var players = entries
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
        uint stateTick,
        byte? viewerSlot = null)
    {
        var entries = _world.EnumerateReplicatedNetworkPlayers().ToArray();
        PreparePlayerIdentities(entries);
        var viewer = ResolveViewer(viewerSlot);
        var players = entries
            .Where(entry => !SnapshotBroadcaster.ShouldHideSpyFromViewer(entry.Player, viewer))
            .Select(entry => ToPlayerState(entry.Slot, entry.Player, stateTick, viewer))
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

    private PlayerEntity? ResolveViewer(byte? viewerSlot)
    {
        if (!viewerSlot.HasValue
            || !SimulationWorld.IsPlayableNetworkPlayerSlot(viewerSlot.Value)
            || _world.IsNetworkPlayerAwaitingJoin(viewerSlot.Value)
            || !_world.TryGetNetworkPlayer(viewerSlot.Value, out var viewer))
        {
            return null;
        }

        return viewer;
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
            stateTick,
            isCritical: shot.IsCritical,
            criticalDamageMultiplier: shot.CriticalDamageMultiplier)));
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
            stateTick,
            isCritical: shot.IsCritical,
            criticalDamageMultiplier: shot.CriticalDamageMultiplier)));
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
            stateTick,
            isCritical: shot.IsCritical,
            criticalDamageMultiplier: shot.CriticalDamageMultiplier)));
        projectiles.AddRange(_world.Needles.Select(shot =>
        {
            var arrow = shot as ArrowProjectileEntity;
            var medicKritzM2 = shot as MedicHealNeedleProjectileEntity;
            return ToProjectile(
                shot.Id,
                arrow is null
                    ? Protocol64ProjectileKind.Needle
                    : Protocol64ProjectileKind.Arrow,
                shot.OwnerId,
                shot.X,
                shot.Y,
                shot.VelocityX,
                shot.VelocityY,
                0f,
                shot.TicksRemaining,
                active: true,
                damage: shot.Damage,
                stateTick,
                isCritical: shot.IsCritical,
                arrowFakeSpeedMultiplier: arrow?.FakeSpeedMultiplier ?? 1f,
                isArrowLanded: arrow?.IsLanded ?? false,
                appliesLastToDieGuardian: arrow?.AppliesLastToDieGuardian ?? false,
                piercesPlayers: arrow?.PiercesPlayers ?? false,
                appliesLastToDieTranqDarts: arrow?.AppliesLastToDieTranqDarts ?? false,
                lastToDiePoisonDamagePerSecond: arrow?.LastToDiePoisonDamagePerSecond ?? 0f,
                lastToDieGhostDamageMultiplier: arrow?.LastToDieGhostDamageMultiplier ?? 1f,
                appliesLastToDieDecapitator: arrow?.AppliesLastToDieDecapitator ?? false,
                isLastToDieDecapitatorFullyCharged: arrow?.IsLastToDieDecapitatorFullyCharged ?? false,
                lastToDieAttachedHeadClassId: arrow?.LastToDieAttachedHeadClassId is { } headClass
                    ? (byte)headClass
                    : (byte)0,
                lastToDieAttachedHeadTeam: arrow?.LastToDieAttachedHeadTeam is { } headTeam
                    ? (byte)headTeam
                    : (byte)0,
                appliesLastToDieExplosiveTip: arrow?.AppliesLastToDieExplosiveTip ?? false,
                lastToDieMedicKritzM2Payload: medicKritzM2?.LastToDiePayload.Encode() ?? 0,
                lastToDieMedicJavelinOwnerPlayerId: medicKritzM2?.AppliesLastToDieJavelin == true
                    ? medicKritzM2.OwnerId
                    : 0,
                lastToDieMedicJavelinTeam: medicKritzM2?.AppliesLastToDieJavelin == true
                    ? (byte)medicKritzM2.Team
                    : (byte)0,
                isLastToDieMedicJavelinAnchored:
                    medicKritzM2?.IsLastToDieJavelinAnchored ?? false,
                lastToDieMedicJavelinFuseTicksRemaining: checked((ushort)Math.Clamp(
                    medicKritzM2?.LastToDieJavelinFuseTicksRemaining ?? 0,
                    0,
                    ushort.MaxValue)),
                hasLastToDieMedicJavelinExploded:
                    medicKritzM2?.HasLastToDieJavelinExploded ?? false,
                criticalDamageMultiplier: shot.CriticalDamageMultiplier);
        }));
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
            damage: shot.DamageValue,
            stateTick,
            isCritical: shot.IsCritical,
            lastToDieSpyRevolverProfile: checked((byte)shot.LastToDieProfile.Encode()),
            appliesLastToDieLuckyStrikeStun: shot.AppliesLuckyStrikeStun,
            criticalDamageMultiplier: shot.CriticalDamageMultiplier)));
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
            stateTick,
            isCritical: rocket.IsCritical,
            criticalDamageMultiplier: rocket.CriticalDamageMultiplier)));
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
            stateTick,
            isCritical: flame.IsCritical,
            criticalDamageMultiplier: flame.CriticalDamageMultiplier)));
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
            damage: flare.DamagePerHit,
            stateTick,
            isCritical: flare.IsCritical,
            criticalDamageMultiplier: flare.CriticalDamageMultiplier)));
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
            stateTick,
            isCritical: mine.IsCritical,
            criticalDamageMultiplier: mine.CriticalDamageMultiplier)));
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
            stateTick,
            isCritical: grenade.IsCritical,
            criticalDamageMultiplier: grenade.CriticalDamageMultiplier)));

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
                var wasLastToDieMedicJavelin =
                    (state.LastToDieMedicKritzM2Payload & (1 << 3)) != 0;
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
                    state.Damage,
                    state.IsCritical,
                    state.LastToDieSpyRevolverProfile,
                    state.AppliesLastToDieLuckyStrikeStun,
                    state.ArrowFakeSpeedMultiplier,
                    state.IsArrowLanded,
                    state.AppliesLastToDieGuardian,
                    state.PiercesPlayers,
                    state.AppliesLastToDieTranqDarts,
                    state.LastToDiePoisonDamagePerSecond,
                    state.LastToDieGhostDamageMultiplier,
                    state.AppliesLastToDieDecapitator,
                    state.IsLastToDieDecapitatorFullyCharged,
                    state.LastToDieAttachedHeadClassId,
                    state.LastToDieAttachedHeadTeam,
                    state.AppliesLastToDieExplosiveTip,
                    state.LastToDieMedicKritzM2Payload,
                    state.LastToDieMedicJavelinOwnerPlayerId,
                    state.LastToDieMedicJavelinTeam,
                    state.IsLastToDieMedicJavelinAnchored,
                    wasLastToDieMedicJavelin
                        ? (ushort)0
                        : state.LastToDieMedicJavelinFuseTicksRemaining,
                    wasLastToDieMedicJavelin
                        || state.HasLastToDieMedicJavelinExploded,
                    state.CriticalDamageMultiplier);
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
        float damage,
        uint stateTick,
        bool isCritical = false,
        byte lastToDieSpyRevolverProfile = 0,
        bool appliesLastToDieLuckyStrikeStun = false,
        float arrowFakeSpeedMultiplier = 1f,
        bool isArrowLanded = false,
        bool appliesLastToDieGuardian = false,
        bool piercesPlayers = false,
        bool appliesLastToDieTranqDarts = false,
        float lastToDiePoisonDamagePerSecond = 0f,
        float lastToDieGhostDamageMultiplier = 1f,
        bool appliesLastToDieDecapitator = false,
        bool isLastToDieDecapitatorFullyCharged = false,
        byte lastToDieAttachedHeadClassId = 0,
        byte lastToDieAttachedHeadTeam = 0,
        bool appliesLastToDieExplosiveTip = false,
        byte lastToDieMedicKritzM2Payload = 0,
        int lastToDieMedicJavelinOwnerPlayerId = 0,
        byte lastToDieMedicJavelinTeam = 0,
        bool isLastToDieMedicJavelinAnchored = false,
        ushort lastToDieMedicJavelinFuseTicksRemaining = 0,
        bool hasLastToDieMedicJavelinExploded = false,
        float criticalDamageMultiplier = 1f)
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
            Math.Max(0f, damage),
            isCritical,
            lastToDieSpyRevolverProfile,
            appliesLastToDieLuckyStrikeStun,
            arrowFakeSpeedMultiplier,
            isArrowLanded,
            appliesLastToDieGuardian,
            piercesPlayers,
            appliesLastToDieTranqDarts,
            lastToDiePoisonDamagePerSecond,
            lastToDieGhostDamageMultiplier,
            appliesLastToDieDecapitator,
            isLastToDieDecapitatorFullyCharged,
            lastToDieAttachedHeadClassId,
            lastToDieAttachedHeadTeam,
            appliesLastToDieExplosiveTip,
            lastToDieMedicKritzM2Payload,
            lastToDieMedicJavelinOwnerPlayerId,
            lastToDieMedicJavelinTeam,
            isLastToDieMedicJavelinAnchored,
            lastToDieMedicJavelinFuseTicksRemaining,
            hasLastToDieMedicJavelinExploded,
            isCritical
                ? ExperimentalGameplaySettings.NormalizeCriticalDamageMultiplier(
                    criticalDamageMultiplier)
                : 1f);
    }

    private Protocol64PlayerState ToPlayerState(
        byte slot,
        PlayerEntity player,
        uint stateTick,
        PlayerEntity? viewer = null)
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
            stateTick,
            _lastProcessedInputSequenceProvider(slot),
            player.IsGrounded,
            Math.Max(0, player.RemainingAirJumps),
            Math.Clamp(player.CurrentShells, 0, player.MaxShells),
            Math.Max(0, player.MaxShells),
            Math.Max(0, player.ExperimentalOffhandCurrentShells),
            Math.Max(0, player.ExperimentalOffhandMaxShells),
            Math.Max(0, player.ExperimentalOffhandCooldownTicks),
            Math.Max(0, player.ExperimentalOffhandReloadTicksUntilNextShell),
            checked((ushort)(player.ClassId switch
            {
                PlayerClass.Spy => player.LastToDieSpyRevolverProfile.EncodeReplicatedState(
                    player.LastToDieLuckyStrikeTriggerProgress),
                PlayerClass.Sniper => player.LastToDieSniperProfile.Encode(),
                _ => 0,
            })),
            player.IsSpyCloaked,
            player.SpyCloakAlpha,
            checked((ushort)player.LastToDieSpyCloakMeterUnits),
            checked((byte)player.LastToDieSpyRogueRampStacks),
            checked((ushort)player.LastToDieSpyRogueRampTicks),
            player.IsSpySuperjumping,
            player.SpySuperjumpHorizontalVelocity,
            checked((ushort)Math.Clamp(player.SpySuperjumpCooldownTicksRemaining, 0, ushort.MaxValue)),
            checked((byte)Math.Clamp(player.SpySuperjumpAvailableCharges, 0, byte.MaxValue)),
            checked((byte)Math.Clamp(player.SpySuperjumpMaximumCharges, 1, byte.MaxValue)),
            checked((ushort)Math.Clamp(player.SpySuperjumpChargeTicks, 0, ushort.MaxValue)),
            player.SpySuperjumpChargeDirectionDegrees,
            player.SpySuperjumpChargeStartMovementButtons,
            player.SpySuperjumpChargeStartBlockedUntilAbilityRelease,
            viewer is not null
                && !ReferenceEquals(player, viewer)
                && player.Team != viewer.Team
                ? checked((ushort)(player.LastToDieSniperRuntimeState & ~0x7f))
                : player.LastToDieSniperRuntimeState,
            player.LastToDieMedicLinkState,
            Math.Max(0, player.AcquiredWeaponCurrentShells),
            Math.Max(0, player.AcquiredWeaponMaxShells),
            Math.Max(0, player.AcquiredWeaponCooldownTicks),
            Math.Max(0, player.AcquiredWeaponReloadTicksUntilNextShell),
            Math.Max(0, player.MedicNeedleCooldownTicks),
            Math.Max(0, player.MedicNeedleRefillTicks),
            Math.Max(0, player.PyroPrimaryFuelScaled),
            player.LastToDieSniperExtensionState,
            player.LastToDieSpyInfiltrateState,
            player.LastToDieSpyAfterlifeState,
            ToProtocol64SniperVolleyState(player.LastToDieSniperVolleyState),
            player.MedicUberDeliveryState,
            player.MedicHealTargetId ?? -1,
            player.MedicUberCharge,
            checked((ushort)Math.Clamp(
                player.LastToDieMedicHailMaryTicksRemaining,
                0,
                ushort.MaxValue)),
            checked((ushort)Math.Clamp(
                player.ServerStunTicksRemaining,
                0,
                ushort.MaxValue)),
            Math.Max(0, player.KritzCritBoostTicksRemaining),
            player.IsKritzCritBoosted ? player.KritzCritBoostProviderPlayerId : 0,
            player.IsKritzCritBoosted ? player.KritzCritBoostProviderSlot : int.MaxValue,
            player.ActiveKritzCritDamageMultiplier,
            player.LastToDieProfessionalFireChordState,
            player.IsDispenserBuffed,
            player.IsDispenserBuffed ? player.DispenserAttackReloadSpeedMultiplier : 1f,
            player.RageCharge,
            player.IsRageReady,
            Math.Max(0, player.RageTicksRemaining),
            Math.Max(0, player.PrimaryCooldownTicks),
            Math.Max(0, player.ReloadTicksUntilNextShell),
            Math.Max(0, player.BuffBannerChargeDamage),
            Math.Max(0, player.BuffBannerDeployTicksRemaining),
            Math.Max(0, player.BuffBannerActiveTicksRemaining));

    private static Protocol64LastToDieSniperVolleyState? ToProtocol64SniperVolleyState(
        in LastToDieSniperVolleyState state)
        => !state.IsActive
            ? null
            : new Protocol64LastToDieSniperVolleyState(
                state.QueuedArrowCount,
                state.DueArrowCount,
                state.SourceTicksUntilNextArrow,
                state.VelocityX,
                state.VelocityY,
                state.Damage,
                state.FakeSpeedMultiplier,
                checked((byte)PlayerEntity.EncodeLastToDieSniperArrowPayload(state.Payload)),
                state.Payload.PoisonDamagePerSecond,
                state.Payload.GhostDamageMultiplier,
                state.Payload.CriticalDamageMultiplier);

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

    private void PreparePlayerIdentities(
        IReadOnlyList<(byte Slot, PlayerEntity Player)> entries)
    {
        var current = new HashSet<(ushort Slot, ulong PlayerId)>();
        foreach (var entry in entries)
        {
            var playerId = checked((ulong)Math.Max(1, entry.Player.Id));
            var key = ((ushort)entry.Slot, playerId);
            current.Add(key);
            if (!_playerIdentities.TryGetValue(key, out var existing))
            {
                _playerIdentities[key] = new PlayerIdentityState(entry.Player.GameplayClassId, 1);
                continue;
            }

            if (!_activePlayerIdentities.Contains(key)
                || !string.Equals(existing.GameplayClassId, entry.Player.GameplayClassId, StringComparison.Ordinal))
            {
                _playerIdentities[key] = existing with
                {
                    GameplayClassId = entry.Player.GameplayClassId,
                    Generation = checked(existing.Generation + 1),
                };
            }
        }

        _activePlayerIdentities.Clear();
        foreach (var key in current)
        {
            _activePlayerIdentities.Add(key);
        }
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
