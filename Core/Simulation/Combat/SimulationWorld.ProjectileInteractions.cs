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
        for (var shotIndex = 0; shotIndex < Shots.Count; shotIndex += 1)
        {
            var shot = Shots[shotIndex];
            if (shot.Team == player.Team
                || !IsWithinProjectileInteractionArea(poofX, poofY, aimRadians, shot.X, shot.Y, PyroAirblastProjectileRadius, radial, radialRadius))
            {
                continue;
            }

            shot.Reflect(player.Id, player.Team, aimRadians);
            reflectedCount += 1;
        }

        for (var needleIndex = 0; needleIndex < Needles.Count; needleIndex += 1)
        {
            var needle = Needles[needleIndex];
            if (needle.Team == player.Team
                || !IsWithinProjectileInteractionArea(poofX, poofY, aimRadians, needle.X, needle.Y, PyroAirblastProjectileRadius, radial, radialRadius))
            {
                continue;
            }

            needle.Reflect(player.Id, player.Team, aimRadians);
            reflectedCount += 1;
        }

        for (var shotIndex = 0; shotIndex < RevolverShots.Count; shotIndex += 1)
        {
            var shot = RevolverShots[shotIndex];
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
        for (var rocketIndex = 0; rocketIndex < Rockets.Count; rocketIndex += 1)
        {
            var rocket = Rockets[rocketIndex];
            if (rocket.Team == player.Team
                || !IsWithinProjectileInteractionArea(poofX, poofY, aimRadians, rocket.X, rocket.Y, PyroAirblastProjectileRadius, radial, radialRadius))
            {
                continue;
            }

            rocket.Reflect(player.Id, player.Team, aimRadians);
            reflectedCount += 1;
        }

        for (var flareIndex = 0; flareIndex < Flares.Count; flareIndex += 1)
        {
            var flare = Flares[flareIndex];
            if (flare.Team == player.Team
                || !IsWithinProjectileInteractionArea(poofX, poofY, aimRadians, flare.X, flare.Y, PyroAirblastProjectileRadius, radial, radialRadius))
            {
                continue;
            }

            ResolveDragonRageProjectileOutcome(flare, hitTarget: false);
            flare.Reflect(player.Id, player.Team, aimRadians);
            reflectedCount += 1;
        }

        for (var mineIndex = 0; mineIndex < Mines.Count; mineIndex += 1)
        {
            var mine = Mines[mineIndex];
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


    private bool TryDestroyNearestEnemyDefensibleProjectile(PlayerTeam team, float x, float y, float radius, out float targetX, out float targetY)
    {
        targetX = 0f;
        targetY = 0f;
        var nearestKind = DefensibleProjectileKind.None;
        var nearestIndex = -1;
        var nearestDistanceSquared = radius * radius;
        FindNearestDefensibleProjectile(Shots, DefensibleProjectileKind.Shot, team, x, y, ref nearestDistanceSquared, ref nearestKind, ref nearestIndex, ref targetX, ref targetY);
        FindNearestDefensibleProjectile(Needles, DefensibleProjectileKind.Needle, team, x, y, ref nearestDistanceSquared, ref nearestKind, ref nearestIndex, ref targetX, ref targetY);
        FindNearestDefensibleProjectile(RevolverShots, DefensibleProjectileKind.RevolverShot, team, x, y, ref nearestDistanceSquared, ref nearestKind, ref nearestIndex, ref targetX, ref targetY);
        FindNearestDefensibleProjectile(Rockets, DefensibleProjectileKind.Rocket, team, x, y, ref nearestDistanceSquared, ref nearestKind, ref nearestIndex, ref targetX, ref targetY);
        FindNearestDefensibleProjectile(Mines, DefensibleProjectileKind.Mine, team, x, y, ref nearestDistanceSquared, ref nearestKind, ref nearestIndex, ref targetX, ref targetY);

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
                || !HasDirectLineOfSight(x, y, projectileX, projectileY, projectileTeam))
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
