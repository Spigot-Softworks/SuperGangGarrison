namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private const float PyroAirblastDistance = 150f;
    private const float PyroAirblastProjectileRadius = 5f;
    private const float PyroAirblastTargetRadius = 25f;
    private const float PyroAirblastMaskLeft = 8f;
    private const float PyroAirblastMaskRight = 96f;
    private const float PyroAirblastMaskTop = -13f;
    private const float PyroAirblastMaskBottom = 14f;
    private const float PyroAirblastMineSpeedFloor = 28f / 3f;
    private const float PyroAirblastLooseBodyImpulse = 28f;
    private const float PyroAirblastPlayerImpulse = 15f * LegacyMovementModel.SourceTicksPerSecond;
    private const float PyroAirblastPlayerLift = -2f * LegacyMovementModel.SourceTicksPerSecond;

    private void TriggerPyroAirblast(PlayerEntity player, float aimWorldX, float aimWorldY, bool fireFlare)
    {
        var (sourceX, sourceY) = WeaponHandler.GetPyroSecondaryOrigin(player);
        var aimDegrees = PointDirectionDegrees(sourceX, sourceY, aimWorldX, aimWorldY);
        var aimRadians = DegreesToRadians(aimDegrees);
        var poofX = sourceX + MathF.Cos(aimRadians) * 25f;
        var poofY = sourceY + MathF.Sin(aimRadians) * 25f;

        TryFirePyroFlare(player, aimRadians, sourceX, sourceY, fireFlare);
        RegisterSoundEvent(player, "CompressionBlastSnd");
        RegisterVisualEffect("AirBlast", poofX, poofY, aimDegrees);

        ReflectEnemyRockets(player, aimRadians, poofX, poofY);
        ReflectEnemyFlares(player, aimRadians, poofX, poofY);
        PushEnemyMines(player.Team, aimRadians, poofX, poofY);
        ApplyAirblastToPlayers(player, sourceX, sourceY, aimRadians, poofX, poofY);
        PushLooseBodies(sourceX, sourceY, aimRadians, poofX, poofY);
    }

    private void TryFirePyroFlare(PlayerEntity player, float aimRadians, float sourceX, float sourceY, bool fireFlare)
    {
        if (!fireFlare)
        {
            return;
        }

        var spawnX = sourceX + MathF.Cos(aimRadians) * 25f;
        var spawnY = sourceY + MathF.Sin(aimRadians) * 25f;
        if (IsProjectileSpawnBlocked(sourceX, sourceY, spawnX, spawnY) || !player.TryFirePyroFlare())
        {
            return;
        }

        SpawnFlare(
            player,
            spawnX,
            spawnY,
            MathF.Cos(aimRadians) * 15f,
            MathF.Sin(aimRadians) * 15f);
    }

    private void ReflectEnemyRockets(PlayerEntity player, float aimRadians, float poofX, float poofY)
    {
        for (var rocketIndex = 0; rocketIndex < _rockets.Count; rocketIndex += 1)
        {
            var rocket = _rockets[rocketIndex];
            if (rocket.Team == player.Team
                || !IsWithinAirblastMask(poofX, poofY, aimRadians, rocket.X, rocket.Y, PyroAirblastProjectileRadius))
            {
                continue;
            }

            rocket.Reflect(player.Id, player.Team, aimRadians);
        }
    }

    private void ReflectEnemyFlares(PlayerEntity player, float aimRadians, float poofX, float poofY)
    {
        for (var flareIndex = 0; flareIndex < _flares.Count; flareIndex += 1)
        {
            var flare = _flares[flareIndex];
            if (flare.Team == player.Team
                || !IsWithinAirblastMask(poofX, poofY, aimRadians, flare.X, flare.Y, PyroAirblastProjectileRadius))
            {
                continue;
            }

            flare.Reflect(player.Id, player.Team, aimRadians);
        }
    }

    private void PushEnemyMines(PlayerTeam team, float aimRadians, float poofX, float poofY)
    {
        for (var mineIndex = 0; mineIndex < _mines.Count; mineIndex += 1)
        {
            var mine = _mines[mineIndex];
            if (mine.Team == team
                || !IsWithinAirblastMask(poofX, poofY, aimRadians, mine.X, mine.Y, PyroAirblastProjectileRadius))
            {
                continue;
            }

            var currentSpeed = MathF.Sqrt((mine.VelocityX * mine.VelocityX) + (mine.VelocityY * mine.VelocityY));
            var reflectedSpeed = MathF.Max(currentSpeed, PyroAirblastMineSpeedFloor);
            mine.Unstick();
            mine.SetVelocity(MathF.Cos(aimRadians) * reflectedSpeed, MathF.Sin(aimRadians) * reflectedSpeed);
        }
    }

    private void ApplyAirblastToPlayers(PlayerEntity player, float sourceX, float sourceY, float aimRadians, float poofX, float poofY)
    {
        foreach (var target in EnumerateSimulatedPlayers())
        {
            if (!target.IsAlive
                || target.Id == player.Id
                || !IsWithinAirblastMask(poofX, poofY, aimRadians, target.X, target.Y, PyroAirblastTargetRadius))
            {
                continue;
            }

            if (target.Team == player.Team)
            {
                SpawnAirblastExtinguishFlames(player, target, aimRadians);
                target.ExtinguishAfterburn();
                continue;
            }

            var scale = GetAirblastScale(sourceX, sourceY, target.X, target.Y);
            if (scale <= 0f)
            {
                continue;
            }

            target.AddImpulse(
                MathF.Cos(aimRadians) * PyroAirblastPlayerImpulse * scale,
                MathF.Sin(aimRadians) * PyroAirblastPlayerImpulse * scale + PyroAirblastPlayerLift);
            target.SetMovementState(LegacyMovementState.Airblast);
        }
    }

    private void PushLooseBodies(float sourceX, float sourceY, float aimRadians, float poofX, float poofY)
    {
        foreach (var body in _deadBodies)
        {
            if (!IsWithinAirblastMask(poofX, poofY, aimRadians, body.X, body.Y, PyroAirblastTargetRadius))
            {
                continue;
            }

            var scale = GetAirblastScale(sourceX, sourceY, body.X, body.Y);
            if (scale <= 0f)
            {
                continue;
            }

            body.AddImpulse(
                MathF.Cos(aimRadians) * PyroAirblastLooseBodyImpulse * scale,
                MathF.Sin(aimRadians) * PyroAirblastLooseBodyImpulse * scale);
        }

        foreach (var gib in _playerGibs)
        {
            if (!IsWithinAirblastMask(poofX, poofY, aimRadians, gib.X, gib.Y, PyroAirblastTargetRadius))
            {
                continue;
            }

            var scale = GetAirblastScale(sourceX, sourceY, gib.X, gib.Y);
            if (scale <= 0f)
            {
                continue;
            }

            gib.AddImpulse(
                MathF.Cos(aimRadians) * PyroAirblastLooseBodyImpulse * scale,
                MathF.Sin(aimRadians) * PyroAirblastLooseBodyImpulse * scale,
                0f);
        }
    }

    private static float GetAirblastScale(float sourceX, float sourceY, float targetX, float targetY)
    {
        var distance = DistanceBetween(sourceX, sourceY, targetX, targetY);
        return MathF.Max(0f, 1f - (distance / PyroAirblastDistance));
    }

    private static bool IsWithinAirblastMask(float poofX, float poofY, float aimRadians, float targetX, float targetY, float radius)
    {
        var deltaX = targetX - poofX;
        var deltaY = targetY - poofY;
        var cosine = MathF.Cos(aimRadians);
        var sine = MathF.Sin(aimRadians);
        var localX = (deltaX * cosine) + (deltaY * sine);
        var localY = (-deltaX * sine) + (deltaY * cosine);

        return CircleIntersectsRectangle(
            localX,
            localY,
            radius,
            PyroAirblastMaskLeft,
            PyroAirblastMaskTop,
            PyroAirblastMaskRight,
            PyroAirblastMaskBottom);
    }
}
