namespace OpenGarrison.Core;

internal sealed partial class AirblastRulesSystem
{
    private int ReflectEnemyBulletLikeProjectiles(
        PlayerEntity player,
        float aimRadians,
        float poofX,
        float poofY,
        bool radial = false,
        float radialRadius = PyroAirblastDistance)
    {
        var reflectedCount = 0;
        for (var shotIndex = 0; shotIndex < _host.Shots.Count; shotIndex += 1)
        {
            var shot = _host.Shots[shotIndex];
            if (shot.Team == player.Team
                || !IsWithinProjectileInteractionArea(poofX, poofY, aimRadians, shot.X, shot.Y, PyroAirblastProjectileRadius, radial, radialRadius))
            {
                continue;
            }

            shot.Reflect(player.Id, player.Team, aimRadians);
            reflectedCount += 1;
        }

        for (var needleIndex = 0; needleIndex < _host.Needles.Count; needleIndex += 1)
        {
            var needle = _host.Needles[needleIndex];
            if (needle.Team == player.Team
                || !IsWithinProjectileInteractionArea(poofX, poofY, aimRadians, needle.X, needle.Y, PyroAirblastProjectileRadius, radial, radialRadius))
            {
                continue;
            }

            needle.Reflect(player.Id, player.Team, aimRadians);
            reflectedCount += 1;
        }

        for (var shotIndex = 0; shotIndex < _host.RevolverShots.Count; shotIndex += 1)
        {
            var shot = _host.RevolverShots[shotIndex];
            if (shot.Team == player.Team
                || !IsWithinProjectileInteractionArea(poofX, poofY, aimRadians, shot.X, shot.Y, PyroAirblastProjectileRadius, radial, radialRadius))
            {
                continue;
            }

            shot.Reflect(player.Id, player.Team, aimRadians);
            reflectedCount += 1;
        }

        return reflectedCount;
    }

    internal int ReflectEnemyExplosiveProjectiles(
        PlayerEntity player,
        float aimRadians,
        float poofX,
        float poofY,
        bool radial = false,
        float radialRadius = PyroAirblastDistance)
    {
        var reflectedCount = 0;
        for (var rocketIndex = 0; rocketIndex < _host.Rockets.Count; rocketIndex += 1)
        {
            var rocket = _host.Rockets[rocketIndex];
            if (rocket.Team == player.Team
                || !IsWithinProjectileInteractionArea(poofX, poofY, aimRadians, rocket.X, rocket.Y, PyroAirblastProjectileRadius, radial, radialRadius))
            {
                continue;
            }

            rocket.Reflect(player.Id, player.Team, aimRadians);
            reflectedCount += 1;
        }

        for (var flareIndex = 0; flareIndex < _host.Flares.Count; flareIndex += 1)
        {
            var flare = _host.Flares[flareIndex];
            if (flare.Team == player.Team
                || !IsWithinProjectileInteractionArea(poofX, poofY, aimRadians, flare.X, flare.Y, PyroAirblastProjectileRadius, radial, radialRadius))
            {
                continue;
            }

            _host.Projectiles.ResolveDragonRageProjectileOutcome(flare, hitTarget: false);
            flare.Reflect(player.Id, player.Team, aimRadians);
            reflectedCount += 1;
        }

        for (var mineIndex = 0; mineIndex < _host.Mines.Count; mineIndex += 1)
        {
            var mine = _host.Mines[mineIndex];
            if (mine.Team == player.Team
                || !IsWithinProjectileInteractionArea(poofX, poofY, aimRadians, mine.X, mine.Y, PyroAirblastProjectileRadius, radial, radialRadius))
            {
                continue;
            }

            mine.Reflect(player.Id, player.Team, aimRadians, PyroAirblastMineSpeedFloor);
            reflectedCount += 1;
        }

        return reflectedCount;
    }


    internal bool TryDestroyNearestEnemyDefensibleProjectile(PlayerTeam team, float x, float y, float radius, out float targetX, out float targetY)
    {
        targetX = 0f;
        targetY = 0f;
        var nearestKind = DefensibleProjectileKind.None;
        var nearestIndex = -1;
        var nearestDistanceSquared = radius * radius;
        FindNearestDefensibleProjectile(_host.Shots, DefensibleProjectileKind.Shot, team, x, y, ref nearestDistanceSquared, ref nearestKind, ref nearestIndex, ref targetX, ref targetY);
        FindNearestDefensibleProjectile(_host.Needles, DefensibleProjectileKind.Needle, team, x, y, ref nearestDistanceSquared, ref nearestKind, ref nearestIndex, ref targetX, ref targetY);
        FindNearestDefensibleProjectile(_host.RevolverShots, DefensibleProjectileKind.RevolverShot, team, x, y, ref nearestDistanceSquared, ref nearestKind, ref nearestIndex, ref targetX, ref targetY);
        FindNearestDefensibleProjectile(_host.Rockets, DefensibleProjectileKind.Rocket, team, x, y, ref nearestDistanceSquared, ref nearestKind, ref nearestIndex, ref targetX, ref targetY);
        FindNearestDefensibleProjectile(_host.Mines, DefensibleProjectileKind.Mine, team, x, y, ref nearestDistanceSquared, ref nearestKind, ref nearestIndex, ref targetX, ref targetY);

        if (nearestIndex < 0)
        {
            return false;
        }

        switch (nearestKind)
        {
            case DefensibleProjectileKind.Shot:
                _host.RemoveShotAt(nearestIndex);
                break;
            case DefensibleProjectileKind.Needle:
                _host.RemoveNeedleAt(nearestIndex);
                break;
            case DefensibleProjectileKind.RevolverShot:
                _host.RemoveRevolverShotAt(nearestIndex);
                break;
            case DefensibleProjectileKind.Rocket:
                _host.RemoveRocketAt(nearestIndex);
                break;
            case DefensibleProjectileKind.Mine:
                _host.RemoveMineAt(nearestIndex);
                break;
            default:
                return false;
        }

        _host.WorldEffects.RegisterImpactEffect(targetX, targetY, 0f);
        return true;
    }

    private static bool IsWithinRadiusSquared(float originX, float originY, float targetX, float targetY, float radiusSquared)
    {
        var deltaX = targetX - originX;
        var deltaY = targetY - originY;
        return ((deltaX * deltaX) + (deltaY * deltaY)) <= radiusSquared;
    }

    private enum DefensibleProjectileKind
    {
        None,
        Shot,
        Needle,
        RevolverShot,
        Rocket,
        Mine,
    }

    private void FindNearestDefensibleProjectile<TProjectile>(
        IReadOnlyList<TProjectile> projectiles,
        DefensibleProjectileKind kind,
        PlayerTeam team,
        float x,
        float y,
        ref float nearestDistanceSquared,
        ref DefensibleProjectileKind nearestKind,
        ref int nearestIndex,
        ref float targetX,
        ref float targetY)
    {
        for (var index = 0; index < projectiles.Count; index += 1)
        {
            var projectile = projectiles[index];
            var (projectileTeam, projectileX, projectileY) = projectile switch
            {
                ShotProjectileEntity shot => (shot.Team, shot.X, shot.Y),
                NeedleProjectileEntity needle => (needle.Team, needle.X, needle.Y),
                RevolverProjectileEntity shot => (shot.Team, shot.X, shot.Y),
                RocketProjectileEntity rocket => (rocket.Team, rocket.X, rocket.Y),
                MineProjectileEntity mine => (mine.Team, mine.X, mine.Y),
                _ => (team, 0f, 0f),
            };
            if (projectileTeam == team)
            {
                continue;
            }

            var deltaX = projectileX - x;
            var deltaY = projectileY - y;
            var distanceSquared = (deltaX * deltaX) + (deltaY * deltaY);
            if (distanceSquared > nearestDistanceSquared
                || !_host.GeometryResolver.HasDirectLineOfSight(x, y, projectileX, projectileY, projectileTeam))
            {
                continue;
            }

            nearestDistanceSquared = distanceSquared;
            nearestKind = kind;
            nearestIndex = index;
            targetX = projectileX;
            targetY = projectileY;
        }
    }

    private static bool IsWithinProjectileInteractionArea(
        float poofX,
        float poofY,
        float aimRadians,
        float targetX,
        float targetY,
        float projectileRadius,
        bool radial,
        float radialRadius)
    {
        if (radial)
        {
            var combinedRadius = radialRadius + projectileRadius;
            return IsWithinRadiusSquared(poofX, poofY, targetX, targetY, combinedRadius * combinedRadius);
        }

        return IsWithinAirblastMask(poofX, poofY, aimRadians, targetX, targetY, projectileRadius);
    }
}
