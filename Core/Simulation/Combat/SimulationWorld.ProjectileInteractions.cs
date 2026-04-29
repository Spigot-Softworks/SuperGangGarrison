namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
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
        for (var shotIndex = 0; shotIndex < _shots.Count; shotIndex += 1)
        {
            var shot = _shots[shotIndex];
            if (shot.Team == player.Team
                || !IsWithinProjectileInteractionArea(poofX, poofY, aimRadians, shot.X, shot.Y, PyroAirblastProjectileRadius, radial, radialRadius))
            {
                continue;
            }

            shot.Reflect(player.Id, player.Team, aimRadians);
            reflectedCount += 1;
        }

        for (var needleIndex = 0; needleIndex < _needles.Count; needleIndex += 1)
        {
            var needle = _needles[needleIndex];
            if (needle.Team == player.Team
                || !IsWithinProjectileInteractionArea(poofX, poofY, aimRadians, needle.X, needle.Y, PyroAirblastProjectileRadius, radial, radialRadius))
            {
                continue;
            }

            needle.Reflect(player.Id, player.Team, aimRadians);
            reflectedCount += 1;
        }

        for (var shotIndex = 0; shotIndex < _revolverShots.Count; shotIndex += 1)
        {
            var shot = _revolverShots[shotIndex];
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

    private int ReflectEnemyExplosiveProjectiles(
        PlayerEntity player,
        float aimRadians,
        float poofX,
        float poofY,
        bool radial = false,
        float radialRadius = PyroAirblastDistance)
    {
        var reflectedCount = 0;
        for (var rocketIndex = 0; rocketIndex < _rockets.Count; rocketIndex += 1)
        {
            var rocket = _rockets[rocketIndex];
            if (rocket.Team == player.Team
                || !IsWithinProjectileInteractionArea(poofX, poofY, aimRadians, rocket.X, rocket.Y, PyroAirblastProjectileRadius, radial, radialRadius))
            {
                continue;
            }

            rocket.Reflect(player.Id, player.Team, aimRadians);
            reflectedCount += 1;
        }

        for (var flareIndex = 0; flareIndex < _flares.Count; flareIndex += 1)
        {
            var flare = _flares[flareIndex];
            if (flare.Team == player.Team
                || !IsWithinProjectileInteractionArea(poofX, poofY, aimRadians, flare.X, flare.Y, PyroAirblastProjectileRadius, radial, radialRadius))
            {
                continue;
            }

            flare.Reflect(player.Id, player.Team, aimRadians);
            reflectedCount += 1;
        }

        for (var mineIndex = 0; mineIndex < _mines.Count; mineIndex += 1)
        {
            var mine = _mines[mineIndex];
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

    private int DestroyEnemyDefensibleProjectiles(PlayerTeam team, float x, float y, float radius)
    {
        var destroyedCount = 0;
        var radiusSquared = radius * radius;
        for (var shotIndex = _shots.Count - 1; shotIndex >= 0; shotIndex -= 1)
        {
            var shot = _shots[shotIndex];
            if (shot.Team == team || !IsWithinRadiusSquared(x, y, shot.X, shot.Y, radiusSquared))
            {
                continue;
            }

            RegisterImpactEffect(shot.X, shot.Y, 0f);
            RemoveShotAt(shotIndex);
            destroyedCount += 1;
        }

        for (var needleIndex = _needles.Count - 1; needleIndex >= 0; needleIndex -= 1)
        {
            var needle = _needles[needleIndex];
            if (needle.Team == team || !IsWithinRadiusSquared(x, y, needle.X, needle.Y, radiusSquared))
            {
                continue;
            }

            RegisterImpactEffect(needle.X, needle.Y, 0f);
            RemoveNeedleAt(needleIndex);
            destroyedCount += 1;
        }

        for (var shotIndex = _revolverShots.Count - 1; shotIndex >= 0; shotIndex -= 1)
        {
            var shot = _revolverShots[shotIndex];
            if (shot.Team == team || !IsWithinRadiusSquared(x, y, shot.X, shot.Y, radiusSquared))
            {
                continue;
            }

            RegisterImpactEffect(shot.X, shot.Y, 0f);
            RemoveRevolverShotAt(shotIndex);
            destroyedCount += 1;
        }

        for (var rocketIndex = _rockets.Count - 1; rocketIndex >= 0; rocketIndex -= 1)
        {
            var rocket = _rockets[rocketIndex];
            if (rocket.Team == team || !IsWithinRadiusSquared(x, y, rocket.X, rocket.Y, radiusSquared))
            {
                continue;
            }

            RegisterImpactEffect(rocket.X, rocket.Y, 0f);
            RemoveRocketAt(rocketIndex);
            destroyedCount += 1;
        }

        for (var mineIndex = _mines.Count - 1; mineIndex >= 0; mineIndex -= 1)
        {
            var mine = _mines[mineIndex];
            if (mine.Team == team || !IsWithinRadiusSquared(x, y, mine.X, mine.Y, radiusSquared))
            {
                continue;
            }

            RegisterImpactEffect(mine.X, mine.Y, 0f);
            RemoveMineAt(mineIndex);
            destroyedCount += 1;
        }

        return destroyedCount;
    }

    private bool TryDestroyNearestEnemyDefensibleProjectile(PlayerTeam team, float x, float y, float radius, out float targetX, out float targetY)
    {
        targetX = 0f;
        targetY = 0f;
        var nearestKind = DefensibleProjectileKind.None;
        var nearestIndex = -1;
        var nearestDistanceSquared = radius * radius;
        FindNearestDefensibleProjectile(_shots, DefensibleProjectileKind.Shot, team, x, y, ref nearestDistanceSquared, ref nearestKind, ref nearestIndex, ref targetX, ref targetY);
        FindNearestDefensibleProjectile(_needles, DefensibleProjectileKind.Needle, team, x, y, ref nearestDistanceSquared, ref nearestKind, ref nearestIndex, ref targetX, ref targetY);
        FindNearestDefensibleProjectile(_revolverShots, DefensibleProjectileKind.RevolverShot, team, x, y, ref nearestDistanceSquared, ref nearestKind, ref nearestIndex, ref targetX, ref targetY);
        FindNearestDefensibleProjectile(_rockets, DefensibleProjectileKind.Rocket, team, x, y, ref nearestDistanceSquared, ref nearestKind, ref nearestIndex, ref targetX, ref targetY);
        FindNearestDefensibleProjectile(_mines, DefensibleProjectileKind.Mine, team, x, y, ref nearestDistanceSquared, ref nearestKind, ref nearestIndex, ref targetX, ref targetY);

        if (nearestIndex < 0)
        {
            return false;
        }

        switch (nearestKind)
        {
            case DefensibleProjectileKind.Shot:
                RemoveShotAt(nearestIndex);
                break;
            case DefensibleProjectileKind.Needle:
                RemoveNeedleAt(nearestIndex);
                break;
            case DefensibleProjectileKind.RevolverShot:
                RemoveRevolverShotAt(nearestIndex);
                break;
            case DefensibleProjectileKind.Rocket:
                RemoveRocketAt(nearestIndex);
                break;
            case DefensibleProjectileKind.Mine:
                RemoveMineAt(nearestIndex);
                break;
            default:
                return false;
        }

        RegisterImpactEffect(targetX, targetY, 0f);
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

    private static void FindNearestDefensibleProjectile<TProjectile>(
        List<TProjectile> projectiles,
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
            if (distanceSquared > nearestDistanceSquared)
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
