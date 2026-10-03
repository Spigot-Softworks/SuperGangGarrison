namespace OpenGarrison.Core;

internal sealed partial class StructureSystem
{
    internal const float CivilDefenseTurretBuildProximityRadius = 50f;

    internal void AdvanceCivilDefenseTurrets()
    {
        if (_host.ClientPredictionMode) return;
        for (var index = _host.WorldObjects.CivilDefenseTurrets.Count - 1; index >= 0; index -= 1)
        {
            var turret = _host.WorldObjects.CivilDefenseTurrets[index];
            var owner = _host.FindPlayerById(turret.OwnerPlayerId);
            if (owner is null || owner.Team != turret.Team)
            {
                DestroyCivilDefenseTurret(turret);
                continue;
            }

            var wasLanded = turret.HasLanded;
            turret.Advance(_host.Level, _host.Bounds);
            if (!wasLanded && turret.HasLanded)
            {
                _host.WorldEffects.RegisterWorldSoundEvent("SentryFloorSnd", turret.X, turret.Y);
                _host.WorldEffects.RegisterWorldSoundEvent("SentryBuildSnd", turret.X, turret.Y);
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

            if (!_host.AirblastRules.TryDestroyNearestEnemyDefensibleProjectile(
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

    internal void FireCivilDefenseTurret(CivilDefenseTurretEntity turret, float targetX, float targetY)
    {
        turret.FireAt(targetX, targetY);
        _host.WorldEffects.RegisterWorldSoundEvent("ShotgunSnd", turret.X, turret.Y);
        var distance = MathF.Max(1f, SimulationMath.DistanceBetween(turret.X, turret.Y, targetX, targetY));
        _host.WorldEffects.RegisterCombatTrace(turret.X, turret.Y, (targetX - turret.X) / distance,
            (targetY - turret.Y) / distance, distance, hitCharacter: false, turret.Team);
    }

    // Limit the segment to the first physical hit before calling this. This
    // prevents a projectile from damaging a player and then being intercepted.
    internal bool TryInterceptWithCivilDefenseTurret(PlayerTeam projectileTeam, float x, float y,
        float directionX, float directionY, float maxDistance)
    {
        if (_host.ClientPredictionMode || _host.WorldObjects.CivilDefenseTurrets.Count == 0) return false;
        CivilDefenseTurretEntity? selected = null;
        var nearest = maxDistance;
        foreach (var turret in _host.WorldObjects.CivilDefenseTurrets)
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
            if (!_host.GeometryResolver.HasDirectLineOfSight(turret.X, turret.Y, x + directionX * distance, y + directionY * distance, projectileTeam)) continue;
            if (selected is not null && distance == nearest && turret.Id > selected.Id) continue;
            selected = turret;
            nearest = distance;
        }
        if (selected is null) return false;
        var hitX = x + directionX * nearest;
        var hitY = y + directionY * nearest;
        FireCivilDefenseTurret(selected, hitX, hitY);
        _host.WorldEffects.RegisterImpactEffect(hitX, hitY, 0f);
        return true;
    }

    internal bool CanDeployCivilDefenseTurret(PlayerEntity player)
    {
        if (_host.ClientPredictionMode
            || !player.IsAlive
            || player.ClassId != PlayerClass.Soldier
            || player.IsInSpawnRoom)
        {
            return false;
        }

        foreach (var turret in _host.WorldObjects.CivilDefenseTurrets)
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

    internal bool TryDeployCivilDefenseTurret(PlayerEntity player)
    {
        if (!CanDeployCivilDefenseTurret(player)) return false;
        var entity = new CivilDefenseTurretEntity(
            _host.AllocateEntityId(),
            player.Id,
            player.Team,
            player.X,
            player.Y,
            player.FacingDirectionX);
        _host.WorldObjects.CivilDefenseTurrets.Add(entity);
        _host.EntityStore.Add(entity);
        _host.WorldEffects.RegisterWorldSoundEvent("SentryBuildSnd", entity.X, entity.Y);
        return true;
    }

    public bool TryDeployLastToDieDefenseBattery(byte ownerSlot)
    {
        if (_host.ClientPredictionMode
            || !_host.NetworkPlayerRules.TryGetNetworkPlayer(ownerSlot, out var owner)
            || !owner.IsAlive)
        {
            return false;
        }

        var facing = owner.FacingDirectionX >= 0f ? 1f : -1f;
        var turret = new CivilDefenseTurretEntity(
            _host.AllocateEntityId(),
            owner.Id,
            owner.Team,
            owner.X + (facing * 34f),
            owner.Y - 10f,
            facing,
            lifetimeTicks: Math.Max(1, _host.Config.TicksPerSecond * 30));
        _host.WorldObjects.CivilDefenseTurrets.Add(turret);
        _host.EntityStore.Add(turret);
        _host.WorldEffects.RegisterWorldSoundEvent("SentryBuildSnd", turret.X, turret.Y);
        return true;
    }

    internal void DestroyCivilDefenseTurret(CivilDefenseTurretEntity turret)
    {
        for (var index = _host.WorldObjects.CivilDefenseTurrets.Count - 1; index >= 0; index -= 1)
        {
            if (!ReferenceEquals(_host.WorldObjects.CivilDefenseTurrets[index], turret))
            {
                continue;
            }

            _host.EntityStore.Remove(turret.Id);
            _host.WorldObjects.CivilDefenseTurrets.RemoveAt(index);
            _host.WorldEffects.RegisterWorldSoundEvent("ExplosionSnd", turret.X, turret.Y);
            _host.WorldEffects.RegisterVisualEffect("Explosion", turret.X, turret.Y);
            break;
        }
    }
}
