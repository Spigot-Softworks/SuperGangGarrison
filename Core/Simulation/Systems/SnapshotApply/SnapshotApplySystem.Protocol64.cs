using OpenGarrison.Core.LastToDie;
using OpenGarrison.GameplayModding;
using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

internal sealed partial class SnapshotApplySystem
{
    internal void ResetProtocol64ClientLocalPlayer() => ApplySnapshotLocalPlayerState(null);

    /// <summary>
    /// Applies the authoritative semantic Last to Die identity needed by remote
    /// weapon presentation. Remote protocol players use server slots, while the
    /// locally owned player is always simulation slot 1 and is reconciled by the
    /// normal local prediction profile.
    /// </summary>
    internal void ReconcileRemoteLastToDieDemoknightPresentation(
        IReadOnlySet<byte> demoknightServerSlots)
    {
        ArgumentNullException.ThrowIfNull(demoknightServerSlots);
        foreach (var (serverSlot, player) in _host.RemoteSnapshots.PlayersBySlot)
        {
            player.SetExperimentalDemoknightEnabled(
                demoknightServerSlots.Contains(serverSlot));
        }
    }

    /// <summary>
    /// Applies the authoritative player slice from protocol 64 to the actual
    /// gameplay world.  Identity/generation validation happens in the client
    /// protocol applier; this method only resolves the already-validated class
    /// and updates the slot's live entity.
    /// </summary>
    internal bool ApplyProtocol64PlayerState(Protocol64PlayerState state, byte? clientLocalPlayerSlot = null)
    {
        if (state is null
            || state.Slot > byte.MaxValue
            || state.EngineerBuild is { IsValid: false }
            || state.PlayerId == 0
            || (state.Equipment is not null && !PlayerEntity.IsValidProtocol64EquipmentState(state))
            || !NetworkPlayerSystem.IsPlayableNetworkPlayerSlot((byte)state.Slot)
            || !CharacterClassCatalog.RuntimeRegistry.TryGetClassBinding(state.GameplayClassId, out _))
        {
            return false;
        }

        var slot = (byte)state.Slot;
        if (clientLocalPlayerSlot.HasValue)
        {
            _host.Abilities.ApplyNetworkSpecialAbilitiesSetting(state.SpecialAbilitiesEnabled);
        }
        var classDefinition = CharacterClassCatalog.GetDefinition(state.GameplayClassId);
        if (clientLocalPlayerSlot.HasValue)
        {
            if (state.PlayerId > int.MaxValue)
                return false;
            if (slot == clientLocalPlayerSlot.Value)
            {
                if (_host.RemoteSnapshots.PlayersBySlot.Remove(slot, out var formerRemote))
                    _host.RemoteSnapshots.Players.Remove(formerRemote);
                _host.ClientSnapshots.AuthoritativeLocalPlayerId = (int)state.PlayerId;
                _host.LocalPlayer.ApplyProtocol64State(state, classDefinition, _host.Config.TicksPerSecond);
                _host.NetworkPlayers.TrySetNetworkPlayerConfiguredTeam(SimulationConstants.LocalPlayerSlot, (PlayerTeam)state.Team);
                _host.NetworkPlayers.ApplySnapshotNetworkPlayerBot(SimulationConstants.LocalPlayerSlot, state.IsBot);
                if (state.IsAlive)
                    _host.NetworkPlayers.TrySetNetworkPlayerAwaitingJoin(SimulationConstants.LocalPlayerSlot, false);
                return true;
            }

            if (!_host.RemoteSnapshots.PlayersBySlot.TryGetValue(slot, out var remote)
                || remote.Id != (int)state.PlayerId)
            {
                if (remote is not null)
                    _host.RemoteSnapshots.Players.Remove(remote);
                ReserveEntityId((int)state.PlayerId);
                remote = new PlayerEntity((int)state.PlayerId, classDefinition, NetworkPlayerSystem.GetNetworkPlayerDefaultName(slot));
                _host.RemoteSnapshots.PlayersBySlot[slot] = remote;
            }
            remote.ApplyProtocol64State(state, classDefinition, _host.Config.TicksPerSecond);
            _host.NetworkPlayers.ApplySnapshotNetworkPlayerBot(slot, state.IsBot);
            if (!_host.RemoteSnapshots.Players.Contains(remote))
                _host.RemoteSnapshots.Players.Add(remote);
            return true;
        }
        if (slot != SimulationConstants.LocalPlayerSlot)
        {
            _host.NetworkPlayers.EnsureAdditionalNetworkPlayer(slot);
            _host.NetworkPlayers.SetNetworkPlayerEnabled(slot, true);
        }

        if (!_host.NetworkPlayers.TryGetNetworkPlayer(slot, out var player))
        {
            return false;
        }

        player.ApplyProtocol64State(state, classDefinition, _host.Config.TicksPerSecond);
        _host.NetworkPlayers.TrySetNetworkPlayerConfiguredTeam(slot, (PlayerTeam)state.Team);
        _host.NetworkPlayers.ApplySnapshotNetworkPlayerBot(slot, state.IsBot);
        _host.NetworkPlayers.TrySetNetworkPlayerAwaitingJoin(slot, !state.IsAlive);
        return true;
    }

    internal bool RemoveProtocol64Player(Protocol64PlayerIdentity identity, byte? clientLocalPlayerSlot = null)
    {
        if (identity is null || identity.Slot > byte.MaxValue || identity.PlayerId > int.MaxValue
            || !NetworkPlayerSystem.IsPlayableNetworkPlayerSlot((byte)identity.Slot))
        {
            return false;
        }

        if (clientLocalPlayerSlot.HasValue)
        {
            if (identity.Slot == clientLocalPlayerSlot.Value)
            {
                if (_host.ClientSnapshots.AuthoritativeLocalPlayerId != (int)identity.PlayerId)
                    return false;
                ResetProtocol64ClientLocalPlayer();
                return true;
            }
            var slot = (byte)identity.Slot;
            if (!_host.RemoteSnapshots.PlayersBySlot.TryGetValue(slot, out var remote)
                || remote.Id != (int)identity.PlayerId)
                return false;
            _host.RemoteSnapshots.PlayersBySlot.Remove(slot);
            _host.RemoteSnapshots.Players.Remove(remote);
            return true;
        }
        return _host.NetworkPlayers.TryReleaseNetworkPlayerSlot((byte)identity.Slot);
    }

    internal bool ApplyProtocol64ProjectileState(Protocol64ProjectileState state, byte? clientLocalPlayerSlot = null)
    {
        if (state is null || state.EntityId > int.MaxValue)
        {
            return false;
        }

        var id = (int)state.EntityId;
        var existingBlade = FindProtocol64Blade(id);
        RemoveProtocol64Projectile(id);
        PlayerEntity? owner;
        var hasLiveOwner = clientLocalPlayerSlot.HasValue
            ? state.OwnerSlot == clientLocalPlayerSlot.Value
                ? (owner = _host.LocalPlayer) is not null
                : _host.RemoteSnapshots.PlayersBySlot.TryGetValue((byte)state.OwnerSlot, out owner)
            : _host.NetworkPlayers.TryGetNetworkPlayer((byte)state.OwnerSlot, out owner);
        var ownerId = state.LastToDieMedicJavelinOwnerPlayerId > 0
            ? state.LastToDieMedicJavelinOwnerPlayerId
            : hasLiveOwner
                ? clientLocalPlayerSlot.HasValue && state.OwnerSlot == clientLocalPlayerSlot.Value
                    ? _host.ClientSnapshots.AuthoritativeLocalPlayerId ?? owner!.Id
                    : owner!.Id
                : 0;
        var team = state.LastToDieMedicJavelinTeam is >= 1 and <= 2
            ? (PlayerTeam)state.LastToDieMedicJavelinTeam
            : owner?.Team ?? PlayerTeam.Red;
        var lifetime = Math.Clamp((int)state.RemainingLifetimeTicks, 1, 1000000);
        var bladeLifetimeSimulationTicks = state.EntityKind == Protocol64ProjectileKind.Blade
            ? _host.GetSimulationTicksFromSourceTicks(ResolveQuoteBladeLifetimeSourceTicks(ownerId))
            : lifetime;
        var ownerAmmoGeneration = existingBlade is not null && existingBlade.OwnerId == ownerId
            ? existingBlade.OwnerAmmoGeneration
            : IsCurrentQuoteBladeOwner(owner)
                ? owner!.QuoteBladeAmmoGeneration
                : -1;
        var entity = CreateProtocol64Projectile(
            state,
            id,
            team,
            ownerId,
            lifetime,
            bladeLifetimeSimulationTicks,
            ownerAmmoGeneration);
        if (entity is null)
        {
            return false;
        }

        if (entity is BladeProjectileEntity blade)
        {
            blade.HydrateAmmoDrainProgress(
                lifetime,
                LegacyMovementModel.SourceTicksPerSecond / Math.Max(1f, _host.Config.TicksPerSecond));
        }

        HydrateProtocol64ProjectileCritical(
            entity,
            state.IsCritical,
            state.CriticalDamageMultiplier);
        AddProtocol64Projectile(entity);
        if (entity is BladeProjectileEntity addedBlade)
        {
            HydrateProtocol64QuoteBladeCount(_host.FindPlayerById(addedBlade.OwnerId));
        }
        return true;
    }

    internal bool RemoveProtocol64Projectile(ulong entityId)
    {
        return entityId <= int.MaxValue && RemoveProtocol64Projectile((int)entityId);
    }

    internal bool RemoveProtocol64Projectile(int entityId)
    {
        var existingBlade = FindProtocol64Blade(entityId);
        var removed = _host.Projectiles.RemoveProjectileEntity(entityId);
        if (removed && existingBlade is not null)
        {
            HydrateProtocol64QuoteBladeCount(_host.FindPlayerById(existingBlade.OwnerId));
        }

        return removed;
    }

    private BladeProjectileEntity? FindProtocol64Blade(int entityId)
    {
        for (var index = 0; index < _host.Blades.Count; index += 1)
        {
            if (_host.Blades[index].Id == entityId)
            {
                return _host.Blades[index];
            }
        }

        return null;
    }

    private static bool IsCurrentQuoteBladeOwner(PlayerEntity? owner)
    {
        return owner is { IsAlive: true }
            && owner.HasPrimaryBehavior(BuiltInGameplayBehaviorIds.Blade)
            && owner.HasSecondaryBehavior(BuiltInGameplayBehaviorIds.QuoteBladeThrow);
    }

    private void HydrateProtocol64QuoteBladeCount(PlayerEntity? owner)
    {
        if (owner is null)
        {
            return;
        }

        var count = 0;
        for (var index = 0; index < _host.Blades.Count; index += 1)
        {
            var blade = _host.Blades[index];
            if (blade.OwnerId == owner.Id
                && blade.OwnerAmmoGeneration == owner.QuoteBladeAmmoGeneration)
            {
                count += 1;
            }
        }

        owner.HydrateQuoteBladeCount(count);
    }

    private static SimulationEntity? CreateProtocol64Projectile(
        Protocol64ProjectileState state,
        int id,
        PlayerTeam team,
        int ownerId,
        int lifetime,
        int bladeLifetimeSimulationTicks,
        long ownerAmmoGeneration)
    {
        return state.EntityKind switch
        {
            Protocol64ProjectileKind.Bullet => CreateProtocol64BulletProjectile(
                state,
                id,
                team,
                ownerId,
                lifetime),
            Protocol64ProjectileKind.BoomstickPellet => CreateProtocol64BulletProjectile(
                state,
                id,
                team,
                ownerId,
                lifetime,
                isBoomstickPellet: true),
            Protocol64ProjectileKind.Blade => new BladeProjectileEntity(
                id,
                team,
                ownerId,
                state.X,
                state.Y,
                state.VelocityX,
                state.VelocityY,
                Math.Max(0, (int)MathF.Round(state.Damage)),
                ticksRemaining: lifetime,
                lifetimeSimulationTicks: bladeLifetimeSimulationTicks,
                ownerAmmoGeneration: ownerAmmoGeneration),
            Protocol64ProjectileKind.Needle when state.LastToDieMedicKritzM2Payload != 0
                => new MedicHealNeedleProjectileEntity(
                    id,
                    team,
                    ownerId,
                    state.X,
                    state.Y,
                    state.VelocityX,
                    state.VelocityY,
                    enemyDamagePerHit: Math.Max(0, (int)MathF.Round(state.Damage)),
                    lastToDiePayload: LastToDieMedicKritzM2Payload.Decode(
                        state.LastToDieMedicKritzM2Payload),
                    lastToDieJavelinFuseTicksRemaining:
                        state.LastToDieMedicJavelinFuseTicksRemaining,
                    isLastToDieJavelinAnchored:
                        state.IsLastToDieMedicJavelinAnchored,
                    hasLastToDieJavelinExploded:
                        state.HasLastToDieMedicJavelinExploded),
            Protocol64ProjectileKind.Needle => new NeedleProjectileEntity(
                id,
                team,
                ownerId,
                state.X,
                state.Y,
                state.VelocityX,
                state.VelocityY,
                damagePerHit: state.Damage > 0f
                    ? Math.Max(0, (int)MathF.Round(state.Damage))
                    : NeedleProjectileEntity.DamagePerHit),
            Protocol64ProjectileKind.Arrow => CreateProtocol64ArrowProjectile(
                state,
                id,
                team,
                ownerId,
                lifetime),
            Protocol64ProjectileKind.RevolverShot => CreateProtocol64RevolverProjectile(
                state,
                id,
                team,
                ownerId,
                lifetime),
            Protocol64ProjectileKind.Rocket => new RocketProjectileEntity(
                id,
                team,
                ownerId,
                state.X,
                state.Y,
                MathF.Sqrt((state.VelocityX * state.VelocityX) + (state.VelocityY * state.VelocityY)),
                DeterministicMath.Atan2(state.VelocityY, state.VelocityX),
                isBallistic: state.IsBallisticRocket,
                ballisticGravityPerTick: state.BallisticRocketGravityPerTick,
                suppressSmokeTrail: state.SuppressRocketSmokeTrail),
            Protocol64ProjectileKind.Flame => new FlameProjectileEntity(id, team, ownerId, state.X, state.Y, state.VelocityX, state.VelocityY, lifetime),
            Protocol64ProjectileKind.Flare => new FlareProjectileEntity(
                id,
                team,
                ownerId,
                state.X,
                state.Y,
                state.VelocityX,
                state.VelocityY,
                lifetime,
                state.Damage > 0f ? state.Damage : FlareProjectileEntity.DefaultDamagePerHit,
                style: (FlareProjectileStyle)state.FlareStyle),
            Protocol64ProjectileKind.Mine => new MineProjectileEntity(id, team, ownerId, state.X, state.Y, state.VelocityX, state.VelocityY),
            Protocol64ProjectileKind.Grenade => CreateProtocol64GrenadeProjectile(state, id, team, ownerId, lifetime),
            // Bubble is intentionally represented as Custom on the wire so
            // plugins can extend the projectile-kind space without claiming a
            // core enum value.  The stock client still has a concrete entity
            // for it and must not silently drop the state.
            Protocol64ProjectileKind.Custom => new BubbleProjectileEntity(id, team, ownerId, state.X, state.Y, state.VelocityX, state.VelocityY),
            _ => null,
        };
    }

    private static GrenadeProjectileEntity CreateProtocol64GrenadeProjectile(
        Protocol64ProjectileState state,
        int id,
        PlayerTeam team,
        int ownerId,
        int lifetime)
    {
        var grenade = new GrenadeProjectileEntity(id, team, ownerId, state.X, state.Y, state.VelocityX, state.VelocityY);
        if (state.Damage >= GrenadeProjectileEntity.StrongDrinkDirectHitDamage - 0.01f)
        {
            grenade.ConfigureAsStrongDrink(
                Math.Max(1, lifetime),
                GrenadeProjectileEntity.StrongDrinkDefaultSpinSpeed);
        }

        return grenade;
    }

    private static ShotProjectileEntity CreateProtocol64BulletProjectile(
        Protocol64ProjectileState state,
        int id,
        PlayerTeam team,
        int ownerId,
        int lifetime,
        bool isBoomstickPellet = false)
    {
        var damage = Math.Max(0f, state.Damage);
        var shot = new ShotProjectileEntity(
            id,
            team,
            ownerId,
            state.X,
            state.Y,
            state.VelocityX,
            state.VelocityY,
            damagePerHit: damage,
            playerKnockbackImpulse: state.PlayerKnockbackImpulse,
            playerKnockbackAirborneVerticalScale: state.PlayerKnockbackAirborneVerticalScale,
            playerKnockbackGroundedVerticalScale: state.PlayerKnockbackGroundedVerticalScale,
            isBoomstickPellet: isBoomstickPellet);
        shot.ApplyNetworkState(
            state.X,
            state.Y,
            state.VelocityX,
            state.VelocityY,
            lifetime,
            damage,
            state.PlayerKnockbackImpulse,
            state.PlayerKnockbackAirborneVerticalScale,
            state.PlayerKnockbackGroundedVerticalScale);
        return shot;
    }

    private static RevolverProjectileEntity CreateProtocol64RevolverProjectile(
        Protocol64ProjectileState state,
        int id,
        PlayerTeam team,
        int ownerId,
        int lifetime)
    {
        var shot = new RevolverProjectileEntity(
            id,
            team,
            ownerId,
            state.X,
            state.Y,
            state.VelocityX,
            state.VelocityY,
            Math.Max(0f, state.Damage),
            lastToDieProfile: LastToDieSpyRevolverProfile.Decode(
                state.LastToDieSpyRevolverProfile),
            appliesLuckyStrikeStun: state.AppliesLastToDieLuckyStrikeStun,
            playerKnockbackImpulse: state.PlayerKnockbackImpulse,
            playerKnockbackAirborneVerticalScale: state.PlayerKnockbackAirborneVerticalScale,
            playerKnockbackGroundedVerticalScale: state.PlayerKnockbackGroundedVerticalScale);
        shot.ApplyNetworkState(
            state.X,
            state.Y,
            state.VelocityX,
            state.VelocityY,
            lifetime,
            Math.Max(0f, state.Damage),
            state.PlayerKnockbackImpulse,
            state.PlayerKnockbackAirborneVerticalScale,
            state.PlayerKnockbackGroundedVerticalScale);
        return shot;
    }

    private static ArrowProjectileEntity CreateProtocol64ArrowProjectile(
        Protocol64ProjectileState state,
        int id,
        PlayerTeam team,
        int ownerId,
        int lifetime)
    {
        var arrow = new ArrowProjectileEntity(
            id,
            team,
            ownerId,
            state.X,
            state.Y,
            state.VelocityX,
            state.VelocityY,
            Math.Max(0, (int)MathF.Round(state.Damage)),
            fakeSpeedMultiplier: state.ArrowFakeSpeedMultiplier,
            appliesLastToDieGuardian: state.AppliesLastToDieGuardian,
            piercesPlayers: state.PiercesPlayers,
            appliesLastToDieTranqDarts: state.AppliesLastToDieTranqDarts,
            lastToDiePoisonDamagePerSecond: state.LastToDiePoisonDamagePerSecond,
            lastToDieGhostDamageMultiplier: state.LastToDieGhostDamageMultiplier,
            appliesLastToDieDecapitator: state.AppliesLastToDieDecapitator,
            isLastToDieDecapitatorFullyCharged: state.IsLastToDieDecapitatorFullyCharged,
            appliesLastToDieExplosiveTip: state.AppliesLastToDieExplosiveTip,
            lastToDieAttachedHeadClassId: state.LastToDieAttachedHeadClassId > 0
                ? (PlayerClass?)state.LastToDieAttachedHeadClassId
                : null,
            lastToDieAttachedHeadTeam: state.LastToDieAttachedHeadTeam > 0
                ? (PlayerTeam?)state.LastToDieAttachedHeadTeam
                : null);
        arrow.ApplyNetworkState(
            state.X,
            state.Y,
            state.VelocityX,
            state.VelocityY,
            lifetime);
        arrow.SetLanded(state.IsArrowLanded);
        return arrow;
    }

    private void AddProtocol64Projectile(SimulationEntity entity)
    {
        _host.Projectiles.AddProjectileEntity(entity);
        ReserveEntityId(entity.Id);
    }

    private static void HydrateProtocol64ProjectileCritical(
        SimulationEntity entity,
        bool isCritical,
        float criticalDamageMultiplier)
    {
        switch (entity)
        {
            case ShotProjectileEntity value:
                value.HydrateCritical(isCritical, criticalDamageMultiplier);
                break;
            case BubbleProjectileEntity value:
                value.HydrateCritical(isCritical, criticalDamageMultiplier);
                break;
            case BladeProjectileEntity value:
                value.HydrateCritical(isCritical, criticalDamageMultiplier);
                break;
            case NeedleProjectileEntity value:
                value.HydrateCritical(isCritical, criticalDamageMultiplier);
                break;
            case RevolverProjectileEntity value:
                value.HydrateCritical(isCritical, criticalDamageMultiplier);
                break;
            case RocketProjectileEntity value:
                value.HydrateCritical(isCritical, criticalDamageMultiplier);
                break;
            case FlameProjectileEntity value:
                value.HydrateCritical(isCritical, criticalDamageMultiplier);
                break;
            case FlareProjectileEntity value:
                value.HydrateCritical(isCritical, criticalDamageMultiplier);
                break;
            case MineProjectileEntity value:
                value.HydrateCritical(isCritical, criticalDamageMultiplier);
                break;
            case GrenadeProjectileEntity value:
                value.HydrateCritical(isCritical, criticalDamageMultiplier);
                break;
        }
    }
}
