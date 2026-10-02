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
    private const float PyroAirblastPlayerMaskLeft = 12f;
    private const float PyroAirblastPlayerMaskRight = 84f;
    private const float PyroAirblastPlayerMaskTop = -10f;
    private const float PyroAirblastPlayerMaskBottom = 11f;
    private const float PyroAirblastMineSpeedFloor = SimulationMath.PyroAirblastMineSpeedFloor;
    private const float PyroAirblastLooseBodyImpulse = 28f;
    private const float PyroAirblastPlayerImpulse = 15f * LegacyMovementModel.SourceTicksPerSecond;
    private const float PyroAirblastPlayerLift = -2f * LegacyMovementModel.SourceTicksPerSecond;
    private const float PyroSelfAirblastHorizontalStrengthScale = 1f / 3f;
    private const float PyroSelfAirblastVerticalStrengthScale = 1f / 3f;
    private const float SoldierThundergunnerDistance = 170f;
    private const float SoldierThundergunnerPlayerImpulse = 24f * LegacyMovementModel.SourceTicksPerSecond;
    private const float SoldierThundergunnerPlayerLift = -4f * LegacyMovementModel.SourceTicksPerSecond;

    private void TriggerPyroSelfAirblast(PlayerEntity player, float aimWorldX, float aimWorldY)
    {
        var (sourceX, sourceY) = WeaponHandler.GetPyroSecondaryOrigin(player);
        var aimDegrees = PointDirectionDegrees(sourceX, sourceY, aimWorldX, aimWorldY);
        var aimRadians = DegreesToRadians(aimDegrees);
        var poofX = sourceX + DeterministicMath.Cos(aimRadians) * 25f;
        var poofY = sourceY + DeterministicMath.Sin(aimRadians) * 25f;

        RegisterSoundEvent(player, "CompressionBlastSnd");
        RegisterVisualEffect("AirBlast", poofX, poofY, aimDegrees);
        ApplyAirblastToSelf(player, sourceX, sourceY, aimRadians);
        var applyTeammateKnockback = this.ExperimentalGameplaySettings.EnableFriendlyAirburstKnockback;
        ApplyAirblastToPlayers(
            player,
            sourceX,
            sourceY,
            aimRadians,
            poofX,
            poofY,
            affectEnemies: false,
            affectTeammates: true,
            applyTeammateKnockback: applyTeammateKnockback,
            carryTeammatesWithPlayerVelocity: applyTeammateKnockback);
        PushLooseBodies(sourceX, sourceY, aimRadians, poofX, poofY);
    }

    private void TriggerPyroAirblast(PlayerEntity player, float aimWorldX, float aimWorldY)
    {
        var (sourceX, sourceY) = WeaponHandler.GetPyroSecondaryOrigin(player);
        var aimDegrees = PointDirectionDegrees(sourceX, sourceY, aimWorldX, aimWorldY);
        var aimRadians = DegreesToRadians(aimDegrees);
        var poofX = sourceX + DeterministicMath.Cos(aimRadians) * 25f;
        var poofY = sourceY + DeterministicMath.Sin(aimRadians) * 25f;

        RegisterSoundEvent(player, "CompressionBlastSnd");
        RegisterVisualEffect("AirBlast", poofX, poofY, aimDegrees);

        ReflectEnemyRockets(player, aimRadians, poofX, poofY);
        ReflectEnemyFlares(player, aimRadians, poofX, poofY);
        ReflectEnemyGrenades(player, aimRadians, poofX, poofY);
        PushEnemyMines(player.Team, aimRadians, poofX, poofY);
        ApplyAirblastToPlayers(
            player,
            sourceX,
            sourceY,
            aimRadians,
            poofX,
            poofY,
            applyTeammateKnockback: this.ExperimentalGameplaySettings.EnableFriendlyAirblastKnockback);
        PushLooseBodies(sourceX, sourceY, aimRadians, poofX, poofY);
    }

    private void TriggerCivvieUmbrellaAirblast(PlayerEntity player, float aimWorldX, float aimWorldY)
    {
        var (sourceX, sourceY, aimRadians) = WeaponHandler.GetCivvieUmbrellaTip(player, aimWorldX, aimWorldY);
        var aimDegrees = aimRadians * (180f / MathF.PI);
        var poofX = sourceX;
        var poofY = sourceY;

        RegisterSoundEvent(player, "CompressionBlastSnd");
        RegisterVisualEffect("AirBlast", poofX, poofY, aimDegrees);
        ApplyAirblastToPlayers(
            player,
            sourceX,
            sourceY,
            aimRadians,
            poofX,
            poofY,
            affectTeammates: false);
    }

    private void TriggerExperimentalSoldierThundergunner(PlayerEntity player, float aimWorldX, float aimWorldY)
    {
        var emptyClip = player.CurrentShells <= 0;
        var forceScale = emptyClip
            ? global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultSoldierThundergunnerEmptyClipForceMultiplier
            : 1f;
        var (sourceX, sourceY, aimRadians) = emptyClip
            ? (player.X, player.Y, PointDirectionRadians(player.X, player.Y, aimWorldX, aimWorldY, player.FacingDirectionX))
            : WeaponHandler.GetSoldierRocketLauncherTip(player, aimWorldX, aimWorldY);
        var aimDegrees = aimRadians * (180f / MathF.PI);
        var poofX = sourceX + DeterministicMath.Cos(aimRadians) * 25f;
        var poofY = sourceY + DeterministicMath.Sin(aimRadians) * 25f;

        RegisterSoundEvent(player, "CompressionBlastSnd");
        RegisterVisualEffect("AirBlast", poofX, poofY, aimDegrees);
        ReflectEnemyExplosiveProjectiles(player, aimRadians, poofX, poofY, emptyClip, SoldierThundergunnerDistance);
        ReflectEnemyBulletLikeProjectiles(player, aimRadians, poofX, poofY, emptyClip, SoldierThundergunnerDistance);
        ApplyThundergunnerToPlayers(player, sourceX, sourceY, aimRadians, poofX, poofY, emptyClip, forceScale);
        PushLooseBodies(sourceX, sourceY, aimRadians, poofX, poofY);
    }

    private static void ApplyAirblastToSelf(PlayerEntity player, float sourceX, float sourceY, float aimRadians)
    {
        var scale = GetAirblastScale(sourceX, sourceY, player.X, player.Y);
        if (scale <= 0f)
        {
            return;
        }

        player.AddImpulse(
            -DeterministicMath.Cos(aimRadians) * PyroAirblastPlayerImpulse * scale * PyroSelfAirblastHorizontalStrengthScale,
            -DeterministicMath.Sin(aimRadians) * PyroAirblastPlayerImpulse * scale * PyroSelfAirblastVerticalStrengthScale + (PyroAirblastPlayerLift * PyroSelfAirblastVerticalStrengthScale));
        player.SetMovementStateIfAirborne(LegacyMovementState.Airblast);
    }

    private void ReflectEnemyRockets(PlayerEntity player, float aimRadians, float poofX, float poofY)
    {
        for (var rocketIndex = 0; rocketIndex < Rockets.Count; rocketIndex += 1)
        {
            var rocket = Rockets[rocketIndex];
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
        for (var flareIndex = 0; flareIndex < Flares.Count; flareIndex += 1)
        {
            var flare = Flares[flareIndex];
            if (flare.Team == player.Team
                || !IsWithinAirblastMask(poofX, poofY, aimRadians, flare.X, flare.Y, PyroAirblastProjectileRadius))
            {
                continue;
            }

            ResolveDragonRageProjectileOutcome(flare, hitTarget: false);
            flare.Reflect(player.Id, player.Team, aimRadians);
        }
    }

    private void ReflectEnemyGrenades(PlayerEntity player, float aimRadians, float poofX, float poofY)
    {
        for (var grenadeIndex = 0; grenadeIndex < Grenades.Count; grenadeIndex += 1)
        {
            var grenade = Grenades[grenadeIndex];
            if (grenade.Team == player.Team
                || !IsWithinAirblastMask(poofX, poofY, aimRadians, grenade.X, grenade.Y, PyroAirblastProjectileRadius))
            {
                continue;
            }

            // Strong Drink: sticky-style shove — redirect velocity, keep original owner/team.
            if (grenade.IsStrongDrink)
            {
                grenade.PushByAirblast(aimRadians, PyroAirblastMineSpeedFloor);
                continue;
            }

            grenade.Reflect(player.Id, player.Team, aimRadians);
        }
    }

    private void PushEnemyMines(PlayerTeam team, float aimRadians, float poofX, float poofY)
    {
        for (var mineIndex = 0; mineIndex < Mines.Count; mineIndex += 1)
        {
            var mine = Mines[mineIndex];
            if (mine.Team == team
                || !IsWithinAirblastMask(poofX, poofY, aimRadians, mine.X, mine.Y, PyroAirblastProjectileRadius))
            {
                continue;
            }

            var currentSpeed = MathF.Sqrt((mine.VelocityX * mine.VelocityX) + (mine.VelocityY * mine.VelocityY));
            var reflectedSpeed = MathF.Max(currentSpeed, PyroAirblastMineSpeedFloor);
            var wasStickied = mine.IsStickied;
            mine.Unstick();
            mine.SetVelocity(DeterministicMath.Cos(aimRadians) * reflectedSpeed, DeterministicMath.Sin(aimRadians) * reflectedSpeed);
            if (!wasStickied)
            {
                continue;
            }

            mine.SetVelocity(mine.VelocityX * 0.65f, mine.VelocityY * 0.65f);
            if (!TryGetAirblastMineSurfaceNormal(mine.X, mine.Y, out var normalX, out var normalY))
            {
                continue;
            }

            var normalSpeed = (normalX * mine.VelocityX) + (normalY * mine.VelocityY);
            if (normalSpeed < 0f)
            {
                mine.SetVelocity(
                    mine.VelocityX - (2f * normalSpeed * normalX),
                    mine.VelocityY - (2f * normalSpeed * normalY));
            }
        }
    }

    private void ApplyAirblastToPlayers(
        PlayerEntity player,
        float sourceX,
        float sourceY,
        float aimRadians,
        float poofX,
        float poofY,
        bool affectEnemies = true,
        bool affectTeammates = true,
        bool applyTeammateKnockback = false,
        bool carryTeammatesWithPlayerVelocity = false)
    {
        foreach (var target in EnumerateSimulatedPlayers())
        {
            if (!target.IsAlive || target.Id == player.Id)
            {
                continue;
            }

            var targetIsTeammate = target.Team == player.Team;
            var useFriendlyAirburstMask = targetIsTeammate && carryTeammatesWithPlayerVelocity;
            if (!IsWithinAirblastPlayerMask(poofX, poofY, aimRadians, target.X, target.Y, PyroAirblastTargetRadius, useFriendlyAirburstMask))
            {
                continue;
            }

            if (targetIsTeammate ? !affectTeammates : !affectEnemies)
            {
                continue;
            }

            if (targetIsTeammate)
            {
                SpawnAirblastExtinguishFlames(player, target, aimRadians);
                target.ExtinguishAfterburn();
                if (!applyTeammateKnockback)
                {
                    continue;
                }
            }

            var scale = GetAirblastScale(sourceX, sourceY, target.X, target.Y);
            if (scale <= 0f)
            {
                continue;
            }

            if (!targetIsTeammate)
            {
            }

            if (targetIsTeammate && carryTeammatesWithPlayerVelocity)
            {
                if (target.IsGrounded)
                {
                    continue;
                }

                target.AddImpulse(
                    player.HorizontalSpeed - target.HorizontalSpeed,
                    player.VerticalSpeed - target.VerticalSpeed);
                target.SetMovementState(LegacyMovementState.FriendlyJuggle);
                continue;
            }

            target.AddImpulse(
                DeterministicMath.Cos(aimRadians) * PyroAirblastPlayerImpulse * scale,
                DeterministicMath.Sin(aimRadians) * PyroAirblastPlayerImpulse * scale + PyroAirblastPlayerLift);
            target.SetMovementStateIfAirborne(LegacyMovementState.Airblast);
        }
    }

    private void PushLooseBodies(float sourceX, float sourceY, float aimRadians, float poofX, float poofY)
    {
        var deadBodiesSnapshot = WorldObjects.DeadBodies.ToArray();
        foreach (var body in deadBodiesSnapshot)
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
                DeterministicMath.Cos(aimRadians) * PyroAirblastLooseBodyImpulse * scale,
                DeterministicMath.Sin(aimRadians) * PyroAirblastLooseBodyImpulse * scale);
        }

        var playerGibsSnapshot = WorldObjects.PlayerGibs.ToArray();
        foreach (var gib in playerGibsSnapshot)
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
                DeterministicMath.Cos(aimRadians) * PyroAirblastLooseBodyImpulse * scale,
                DeterministicMath.Sin(aimRadians) * PyroAirblastLooseBodyImpulse * scale,
                0f);
        }
    }

    private static float GetAirblastScale(float sourceX, float sourceY, float targetX, float targetY)
    {
        var distance = DistanceBetween(sourceX, sourceY, targetX, targetY);
        return MathF.Max(0f, 1f - (distance / PyroAirblastDistance));
    }

    private static float GetThundergunnerScale(float sourceX, float sourceY, float targetX, float targetY)
    {
        var distance = DistanceBetween(sourceX, sourceY, targetX, targetY);
        return MathF.Max(0f, 1f - (distance / SoldierThundergunnerDistance));
    }

    private void ApplyThundergunnerToPlayers(
        PlayerEntity player,
        float sourceX,
        float sourceY,
        float aimRadians,
        float poofX,
        float poofY,
        bool radial,
        float forceScale)
    {
        foreach (var target in EnumerateSimulatedPlayers())
        {
            if (!target.IsAlive || target.Id == player.Id)
            {
                continue;
            }

            var targetDirectionRadians = radial
                ? DeterministicMath.Atan2(target.Y - sourceY, target.X - sourceX)
                : aimRadians;
            if (!radial && !IsWithinAirblastMask(poofX, poofY, aimRadians, target.X, target.Y, PyroAirblastTargetRadius))
            {
                continue;
            }

            if (radial && DistanceBetween(sourceX, sourceY, target.X, target.Y) > SoldierThundergunnerDistance)
            {
                continue;
            }

            if (target.Team == player.Team)
            {
                SpawnAirblastExtinguishFlames(player, target, targetDirectionRadians);
                target.ExtinguishAfterburn();
                continue;
            }

            var scale = GetThundergunnerScale(sourceX, sourceY, target.X, target.Y) * forceScale;
            if (scale <= 0f)
            {
                continue;
            }

            target.AddImpulse(
                DeterministicMath.Cos(targetDirectionRadians) * SoldierThundergunnerPlayerImpulse * scale,
                DeterministicMath.Sin(targetDirectionRadians) * SoldierThundergunnerPlayerImpulse * scale + SoldierThundergunnerPlayerLift * forceScale);
            target.SetMovementState(LegacyMovementState.Airblast);
        }
    }

    private static bool IsWithinAirblastMask(float poofX, float poofY, float aimRadians, float targetX, float targetY, float radius)
    {
        return IsWithinAirblastMask(
            poofX,
            poofY,
            aimRadians,
            targetX,
            targetY,
            radius,
            PyroAirblastMaskLeft,
            PyroAirblastMaskTop,
            PyroAirblastMaskRight,
            PyroAirblastMaskBottom);
    }

    private static bool IsWithinAirblastPlayerMask(
        float poofX,
        float poofY,
        float aimRadians,
        float targetX,
        float targetY,
        float radius,
        bool useFriendlyAirburstMask)
    {
        if (!useFriendlyAirburstMask)
        {
            return IsWithinAirblastMask(poofX, poofY, aimRadians, targetX, targetY, radius);
        }

        return IsWithinAirblastMask(
            poofX,
            poofY,
            aimRadians,
            targetX,
            targetY,
            radius,
            PyroAirblastPlayerMaskLeft,
            PyroAirblastPlayerMaskTop,
            PyroAirblastPlayerMaskRight,
            PyroAirblastPlayerMaskBottom);
    }

    private static bool IsWithinAirblastMask(
        float poofX,
        float poofY,
        float aimRadians,
        float targetX,
        float targetY,
        float radius,
        float maskLeft,
        float maskTop,
        float maskRight,
        float maskBottom)
    {
        var deltaX = targetX - poofX;
        var deltaY = targetY - poofY;
        var cosine = DeterministicMath.Cos(aimRadians);
        var sine = DeterministicMath.Sin(aimRadians);
        var localX = (deltaX * cosine) + (deltaY * sine);
        var localY = (-deltaX * sine) + (deltaY * cosine);

        return CircleIntersectsRectangle(
            localX,
            localY,
            radius,
            maskLeft,
            maskTop,
            maskRight,
            maskBottom);
    }

    private static float PointDirectionRadians(float x1, float y1, float x2, float y2, float fallbackDirectionX)
    {
        return SimulationMath.PointDirectionRadians(x1, y1, x2, y2, fallbackDirectionX);
    }

    private bool TryGetAirblastMineSurfaceNormal(float x, float y, out float normalX, out float normalY)
    {
        normalY = (IsAirblastMineObstacleBlocked(x, y - 3f) ? 1f : 0f)
            - (IsAirblastMineObstacleBlocked(x, y + 3f) ? 1f : 0f);
        normalX = (IsAirblastMineObstacleBlocked(x - 3f, y) ? 1f : 0f)
            - (IsAirblastMineObstacleBlocked(x + 3f, y) ? 1f : 0f);

        var length = MathF.Sqrt((normalX * normalX) + (normalY * normalY));
        if (length <= 0.0001f)
        {
            normalX = 0f;
            normalY = 0f;
            return false;
        }

        normalX /= length;
        normalY /= length;
        return true;
    }

    private bool IsAirblastMineObstacleBlocked(float x, float y)
    {
        foreach (var solid in Level.Solids)
        {
            if (x >= solid.Left && x < solid.Right && y >= solid.Top && y < solid.Bottom)
            {
                return true;
            }
        }

        foreach (var roomObject in Level.RoomObjects)
        {
            if (!IsAirblastMineBlockingRoomObject(roomObject))
            {
                continue;
            }

            if (x >= roomObject.Left && x < roomObject.Right && y >= roomObject.Top && y < roomObject.Bottom)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsAirblastMineBlockingRoomObject(RoomObjectMarker roomObject)
    {
        return roomObject.Type switch
        {
            RoomObjectType.TeamGate => true,
            RoomObjectType.ControlPointSetupGate => Level.ControlPointSetupGatesActive,
            RoomObjectType.BulletWall => true,
            _ => false,
        };
    }
}
