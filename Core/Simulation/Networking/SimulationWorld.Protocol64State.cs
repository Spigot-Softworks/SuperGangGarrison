using System;
using System.Collections.Generic;
using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    /// <summary>
    /// Applies the authoritative player slice from protocol 64 to the actual
    /// gameplay world.  Identity/generation validation happens in the client
    /// protocol applier; this method only resolves the already-validated class
    /// and updates the slot's live entity.
    /// </summary>
    public bool ApplyProtocol64PlayerState(Protocol64PlayerState state)
    {
        if (state is null
            || !IsPlayableNetworkPlayerSlot((byte)state.Slot)
            || !CharacterClassCatalog.RuntimeRegistry.TryGetClassBinding(state.GameplayClassId, out _))
        {
            return false;
        }

        var slot = (byte)state.Slot;
        if (slot != LocalPlayerSlot)
        {
            EnsureAdditionalNetworkPlayer(slot);
            SetNetworkPlayerEnabled(slot, true);
        }

        if (!TryGetNetworkPlayer(slot, out var player))
        {
            return false;
        }

        var classDefinition = CharacterClassCatalog.GetDefinition(state.GameplayClassId);
        player.ApplyProtocol64State(state, classDefinition);
        TrySetNetworkPlayerConfiguredTeam(slot, (PlayerTeam)state.Team);
        TrySetNetworkPlayerAwaitingJoin(slot, !state.IsAlive);
        return true;
    }

    public bool RemoveProtocol64Player(Protocol64PlayerIdentity identity)
    {
        if (identity is null || !IsPlayableNetworkPlayerSlot((byte)identity.Slot))
        {
            return false;
        }

        return TryReleaseNetworkPlayerSlot((byte)identity.Slot);
    }

    public bool ApplyProtocol64ProjectileState(Protocol64ProjectileState state)
    {
        if (state is null || state.EntityId > int.MaxValue)
        {
            return false;
        }

        var id = (int)state.EntityId;
        RemoveProtocol64Projectile(id);
        var ownerId = TryGetNetworkPlayer((byte)state.OwnerSlot, out var owner) ? owner.Id : 0;
        var team = owner?.Team ?? PlayerTeam.Red;
        var lifetime = Math.Clamp((int)state.RemainingLifetimeTicks, 1, 1000000);
        var entity = CreateProtocol64Projectile(state, id, team, ownerId, lifetime);
        if (entity is null)
        {
            return false;
        }

        AddProtocol64Projectile(entity);
        return true;
    }

    public bool RemoveProtocol64Projectile(ulong entityId)
    {
        return entityId <= int.MaxValue && RemoveProtocol64Projectile((int)entityId);
    }

    private bool RemoveProtocol64Projectile(int entityId)
    {
        var removed = false;
        removed |= RemoveEntity(_shots, entityId);
        removed |= RemoveEntity(_bubbles, entityId);
        removed |= RemoveEntity(_blades, entityId);
        removed |= RemoveEntity(_needles, entityId);
        removed |= RemoveEntity(_revolverShots, entityId);
        removed |= RemoveEntity(_flames, entityId);
        removed |= RemoveEntity(_flares, entityId);
        removed |= RemoveEntity(_rockets, entityId);
        removed |= RemoveEntity(_mines, entityId);
        removed |= RemoveEntity(_grenades, entityId);
        removed |= _entities.Remove(entityId);
        return removed;
    }

    private static bool RemoveEntity<T>(List<T> entities, int entityId)
        where T : SimulationEntity
    {
        for (var index = entities.Count - 1; index >= 0; index -= 1)
        {
            if (entities[index].Id != entityId)
            {
                continue;
            }

            entities.RemoveAt(index);
            return true;
        }

        return false;
    }

    private SimulationEntity? CreateProtocol64Projectile(
        Protocol64ProjectileState state,
        int id,
        PlayerTeam team,
        int ownerId,
        int lifetime)
    {
        return state.EntityKind switch
        {
            Protocol64ProjectileKind.Bullet => new ShotProjectileEntity(id, team, ownerId, state.X, state.Y, state.VelocityX, state.VelocityY),
            Protocol64ProjectileKind.Blade => new BladeProjectileEntity(id, team, ownerId, state.X, state.Y, state.VelocityX, state.VelocityY, Math.Max(0, state.Damage), lifetime),
            Protocol64ProjectileKind.Needle => new NeedleProjectileEntity(id, team, ownerId, state.X, state.Y, state.VelocityX, state.VelocityY),
            Protocol64ProjectileKind.RevolverShot => new RevolverProjectileEntity(id, team, ownerId, state.X, state.Y, state.VelocityX, state.VelocityY, Math.Max(0, state.Damage)),
            Protocol64ProjectileKind.Rocket => new RocketProjectileEntity(
                id,
                team,
                ownerId,
                state.X,
                state.Y,
                MathF.Sqrt((state.VelocityX * state.VelocityX) + (state.VelocityY * state.VelocityY)),
                MathF.Atan2(state.VelocityY, state.VelocityX)),
            Protocol64ProjectileKind.Flame => new FlameProjectileEntity(id, team, ownerId, state.X, state.Y, state.VelocityX, state.VelocityY, lifetime),
            Protocol64ProjectileKind.Flare => new FlareProjectileEntity(id, team, ownerId, state.X, state.Y, state.VelocityX, state.VelocityY, lifetime),
            Protocol64ProjectileKind.Mine => new MineProjectileEntity(id, team, ownerId, state.X, state.Y, state.VelocityX, state.VelocityY),
            Protocol64ProjectileKind.Grenade => new GrenadeProjectileEntity(id, team, ownerId, state.X, state.Y, state.VelocityX, state.VelocityY),
            // Bubble is intentionally represented as Custom on the wire so
            // plugins can extend the projectile-kind space without claiming a
            // core enum value.  The stock client still has a concrete entity
            // for it and must not silently drop the state.
            Protocol64ProjectileKind.Custom => new BubbleProjectileEntity(id, team, ownerId, state.X, state.Y, state.VelocityX, state.VelocityY),
            _ => null,
        };
    }

    private void AddProtocol64Projectile(SimulationEntity entity)
    {
        switch (entity)
        {
            case ShotProjectileEntity value: _shots.Add(value); break;
            case BladeProjectileEntity value: _blades.Add(value); break;
            case NeedleProjectileEntity value: _needles.Add(value); break;
            case RevolverProjectileEntity value: _revolverShots.Add(value); break;
            case RocketProjectileEntity value: _rockets.Add(value); break;
            case FlameProjectileEntity value: _flames.Add(value); break;
            case FlareProjectileEntity value: _flares.Add(value); break;
            case MineProjectileEntity value: _mines.Add(value); break;
            case GrenadeProjectileEntity value: _grenades.Add(value); break;
            default: throw new ArgumentOutOfRangeException(nameof(entity));
        }

        _entities[entity.Id] = entity;
        ReserveEntityId(entity.Id);
    }
}
