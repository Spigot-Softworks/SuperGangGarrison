namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private const float CivilDefenseTurretBuildProximityRadius = 50f;

    private void AdvanceCivilDefenseTurrets()
    {
        if (ClientPredictionMode) return;
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

            if (turret.IsDead || turret.IsExpired)
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

            FireCivilDefenseTurret(turret, targetX, targetY);
        }
    }

    private void FireCivilDefenseTurret(CivilDefenseTurretEntity turret, float targetX, float targetY)
    {
        turret.FireAt(targetX, targetY);
        RegisterWorldSoundEvent("ShotgunSnd", turret.X, turret.Y);
        var distance = MathF.Max(1f, DistanceBetween(turret.X, turret.Y, targetX, targetY));
        RegisterCombatTrace(turret.X, turret.Y, (targetX - turret.X) / distance,
            (targetY - turret.Y) / distance, distance, hitCharacter: false, turret.Team);
    }

    // Limit the segment to the first physical hit before calling this. This
    // prevents a projectile from damaging a player and then being intercepted.
    private bool TryInterceptWithCivilDefenseTurret(PlayerTeam projectileTeam, float x, float y,
        float directionX, float directionY, float maxDistance)
    {
        if (ClientPredictionMode || _civilDefenseTurrets.Count == 0) return false;
        CivilDefenseTurretEntity? selected = null;
        var nearest = maxDistance;
        foreach (var turret in _civilDefenseTurrets)
        {
            if (turret.Team == projectileTeam || !turret.CanFire()) continue;
            var dx = x - turret.X;
            var dy = y - turret.Y;
            var c = dx * dx + dy * dy - CivilDefenseTurretEntity.TargetRange * CivilDefenseTurretEntity.TargetRange;
            var distance = 0f;
            if (c > 0f)
            {
                var projection = dx * directionX + dy * directionY;
                var discriminant = projection * projection - c;
                if (discriminant < 0f) continue;
                distance = -projection - MathF.Sqrt(discriminant);
                if (distance < 0f) continue;
            }
            if (distance > nearest) continue;
            if (!HasDirectLineOfSight(turret.X, turret.Y, x + directionX * distance, y + directionY * distance, projectileTeam)) continue;
            if (selected is not null && distance == nearest && turret.Id > selected.Id) continue;
            selected = turret;
            nearest = distance;
        }
        if (selected is null) return false;
        var hitX = x + directionX * nearest;
        var hitY = y + directionY * nearest;
        FireCivilDefenseTurret(selected, hitX, hitY);
        RegisterImpactEffect(hitX, hitY, 0f);
        return true;
    }

    private bool CanDeployCivilDefenseTurret(PlayerEntity player)
    {
        if (ClientPredictionMode
            || !player.IsAlive
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

        return true;
    }

    private bool TryDeployCivilDefenseTurret(PlayerEntity player)
    {
        if (!CanDeployCivilDefenseTurret(player)) return false;
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

    public bool TryDeployLastToDieDefenseBattery(byte ownerSlot)
    {
        if (ClientPredictionMode
            || !TryGetNetworkPlayer(ownerSlot, out var owner)
            || !owner.IsAlive)
        {
            return false;
        }

        var facing = owner.FacingDirectionX >= 0f ? 1f : -1f;
        var turret = new CivilDefenseTurretEntity(
            AllocateEntityId(),
            owner.Id,
            owner.Team,
            owner.X + (facing * 34f),
            owner.Y - 10f,
            facing,
            lifetimeTicks: Math.Max(1, Config.TicksPerSecond * 30));
        _civilDefenseTurrets.Add(turret);
        _entities.Add(turret.Id, turret);
        RegisterWorldSoundEvent("SentryBuildSnd", turret.X, turret.Y);
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
