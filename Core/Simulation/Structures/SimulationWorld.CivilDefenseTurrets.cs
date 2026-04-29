namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private const float CivilDefenseTurretBuildProximityRadius = 50f;

    private void AdvanceCivilDefenseTurrets()
    {
        for (var index = _civilDefenseTurrets.Count - 1; index >= 0; index -= 1)
        {
            var turret = _civilDefenseTurrets[index];
            var owner = FindPlayerById(turret.OwnerPlayerId);
            if (owner is null || owner.Team != turret.Team)
            {
                DestroyCivilDefenseTurret(turret);
                continue;
            }

            var wasLanded = turret.HasLanded;
            turret.Advance(Level, Bounds);
            if (!wasLanded && turret.HasLanded)
            {
                RegisterWorldSoundEvent("SentryFloorSnd", turret.X, turret.Y);
                RegisterWorldSoundEvent("SentryBuildSnd", turret.X, turret.Y);
            }

            if (turret.IsDead)
            {
                DestroyCivilDefenseTurret(turret);
                continue;
            }

            if (!turret.CanFire())
            {
                continue;
            }

            if (!TryDestroyNearestEnemyDefensibleProjectile(
                    turret.Team,
                    turret.X,
                    turret.Y,
                    CivilDefenseTurretEntity.TargetRange,
                    out var targetX,
                    out var targetY))
            {
                continue;
            }

            turret.FireAt(targetX, targetY);
            RegisterWorldSoundEvent("ShotgunSnd", turret.X, turret.Y);
            var distance = MathF.Max(1f, DistanceBetween(turret.X, turret.Y, targetX, targetY));
            RegisterCombatTrace(
                turret.X,
                turret.Y,
                (targetX - turret.X) / distance,
                (targetY - turret.Y) / distance,
                distance,
                hitCharacter: false,
                turret.Team);
        }
    }

    private bool TryDeployCivilDefenseTurret(PlayerEntity player)
    {
        if (!player.IsAlive
            || player.ClassId != PlayerClass.Soldier
            || player.IsInSpawnRoom)
        {
            return false;
        }

        foreach (var turret in _civilDefenseTurrets)
        {
            if (turret.OwnerPlayerId == player.Id)
            {
                return false;
            }

            if (turret.IsNear(player.X, player.Y, CivilDefenseTurretBuildProximityRadius))
            {
                return false;
            }
        }

        var entity = new CivilDefenseTurretEntity(
            AllocateEntityId(),
            player.Id,
            player.Team,
            player.X,
            player.Y,
            player.FacingDirectionX);
        _civilDefenseTurrets.Add(entity);
        _entities.Add(entity.Id, entity);
        RegisterWorldSoundEvent("SentryBuildSnd", entity.X, entity.Y);
        return true;
    }

    private void DestroyCivilDefenseTurret(CivilDefenseTurretEntity turret)
    {
        for (var index = _civilDefenseTurrets.Count - 1; index >= 0; index -= 1)
        {
            if (!ReferenceEquals(_civilDefenseTurrets[index], turret))
            {
                continue;
            }

            _entities.Remove(turret.Id);
            _civilDefenseTurrets.RemoveAt(index);
            RegisterWorldSoundEvent("ExplosionSnd", turret.X, turret.Y);
            RegisterVisualEffect("Explosion", turret.X, turret.Y);
            break;
        }
    }
}
