using OpenGarrison.GameplayModding;
using SimulationWorld = OpenGarrison.Core.ProjectileSystem;

namespace OpenGarrison.Core;

public sealed partial class ProjectileSystem
{
    public bool DebugHasLastRocketCollision { get; private set; }
    public float DebugLastRocketCollisionX { get; private set; }
    public float DebugLastRocketCollisionY { get; private set; }
    public string DebugLastRocketCollisionObjectName { get; private set; } = string.Empty;
    public string DebugLastRocketCollisionReason { get; private set; } = string.Empty;

    public bool DebugHasProjectileSpawnBlocked { get; private set; }
    public float DebugProjectileSpawnBlockedX { get; private set; }
    public float DebugProjectileSpawnBlockedY { get; private set; }
    public float DebugProjectileSpawnBlockedWidth { get; private set; }
    public float DebugProjectileSpawnBlockedHeight { get; private set; }
    public string DebugProjectileSpawnBlockedObjectName { get; private set; } = string.Empty;

    internal void SetLastRocketCollisionDebug(float x, float y, string objectName, string reason)
    {
        DebugHasLastRocketCollision = true;
        DebugLastRocketCollisionX = x;
        DebugLastRocketCollisionY = y;
        DebugLastRocketCollisionObjectName = objectName;
        DebugLastRocketCollisionReason = reason;
    }

    internal void SetProjectileSpawnBlockedDebug(float x, float y, float width, float height, string objectName)
    {
        DebugHasProjectileSpawnBlocked = true;
        DebugProjectileSpawnBlockedX = x;
        DebugProjectileSpawnBlockedY = y;
        DebugProjectileSpawnBlockedWidth = width;
        DebugProjectileSpawnBlockedHeight = height;
        DebugProjectileSpawnBlockedObjectName = objectName;
    }

    private string ResolveRocketEnvironmentCollisionName(float hitX, float hitY)
    {
        const float epsilon = 0.25f;
        for (var index = 0; index < Level.RoomObjects.Count; index += 1)
        {
            ref readonly var roomObject = ref Level.GetRoomObject(index);
            var blocksProjectiles = roomObject.Type switch
            {
                RoomObjectType.TeamGate => true,
                RoomObjectType.ControlPointSetupGate => Level.ControlPointSetupGatesActive,
                RoomObjectType.BulletWall => true,
                _ => false,
            };
            if (!blocksProjectiles)
            {
                continue;
            }

            if (hitX >= roomObject.Left - epsilon
                && hitX <= roomObject.Right + epsilon
                && hitY >= roomObject.Top - epsilon
                && hitY <= roomObject.Bottom + epsilon)
            {
                return $"RoomObject:{roomObject.Type}";
            }
        }

        for (var index = 0; index < Level.Solids.Count; index += 1)
        {
            var solid = Level.Solids[index];
            if (hitX >= solid.Left - epsilon
                && hitX <= solid.Right + epsilon
                && hitY >= solid.Top - epsilon
                && hitY <= solid.Bottom + epsilon)
            {
                return "LevelSolid";
            }
        }

        return "Environment";
    }

    public void AdvanceRockets()
    {
        RocketProjectileSystem.Advance(this);
    }

    internal void RemoveRocketAt(int rocketIndex)
    {
        var rocket = _rockets[rocketIndex];
        EntityStore.Remove(rocket.Id);
        MarkProjectileTerminated(rocket.Id);
        _rockets.RemoveAt(rocketIndex);
    }

    private static class RocketProjectileSystem
    {
        private static readonly Lazy<GameMakerAssetManifest> _gameMakerAssets = new(GameMakerRuntimeAssetManifestLoader.LoadPackagedOrProjectAssets);

        public static void Advance(ProjectileSystem projectiles)
        {
            var deltaSeconds = (float)projectiles.Config.FixedDeltaSeconds;
            for (var rocketIndex = projectiles._rockets.Count - 1; rocketIndex >= 0; rocketIndex -= 1)
            {
                AdvanceRocket(projectiles, rocketIndex, deltaSeconds);
            }
        }

        public static void AdvancePendingForOwner(ProjectileSystem projectiles, int ownerId)
        {
            var deltaSeconds = (float)projectiles.Config.FixedDeltaSeconds;
            for (var pendingIndex = projectiles._pendingNewRocketIds.Count - 1; pendingIndex >= 0; pendingIndex -= 1)
            {
                var rocketId = projectiles._pendingNewRocketIds[pendingIndex];
                var rocketIndex = FindRocketIndex(projectiles, rocketId);
                if (rocketIndex < 0)
                {
                    projectiles._pendingNewRocketIds.RemoveAt(pendingIndex);
                    continue;
                }

                if (projectiles._rockets[rocketIndex].OwnerId != ownerId)
                {
                    continue;
                }

                projectiles._pendingNewRocketIds.RemoveAt(pendingIndex);
                AdvanceRocket(projectiles, rocketIndex, deltaSeconds);

                // Update the pending spawn event to the post-advance position so that clients
                // reconstructing the rocket from the spawn event (e.g. when the main Rockets
                // list is budget-dropped) start it at the in-flight position rather than the
                // raw spawn origin, preventing a one-frame flash at the spawn point.
                var advancedRocketIndex = FindRocketIndex(projectiles, rocketId);
                if (advancedRocketIndex >= 0)
                {
                    var advancedRocket = projectiles._rockets[advancedRocketIndex];
                    for (var spawnEventIndex = projectiles._pendingRocketSpawnEvents.Count - 1; spawnEventIndex >= 0; spawnEventIndex -= 1)
                    {
                        if (projectiles._pendingRocketSpawnEvents[spawnEventIndex].Id != rocketId)
                        {
                            continue;
                        }

                        var spawnEvent = projectiles._pendingRocketSpawnEvents[spawnEventIndex];
                        projectiles._pendingRocketSpawnEvents[spawnEventIndex] = spawnEvent with
                        {
                            X = advancedRocket.X,
                            Y = advancedRocket.Y,
                            PreviousX = advancedRocket.PreviousX,
                            PreviousY = advancedRocket.PreviousY,
                            PassedFriendlyPlayerIds = advancedRocket.PassedFriendlyPlayerIds.Count == 0
                                ? []
                                : [.. advancedRocket.PassedFriendlyPlayerIds],
                        };
                        break;
                    }
                }
            }
        }

        private static void AdvanceRocket(ProjectileSystem projectiles, int rocketIndex, float deltaSeconds)
        {
            if (rocketIndex < 0 || rocketIndex >= projectiles._rockets.Count)
            {
                return;
            }

            var rocket = projectiles._rockets[rocketIndex];
            if (!projectiles.ShouldAdvanceProjectileForClientPrediction(rocket.OwnerId))
            {
                return;
            }

            if (projectiles.FindPlayerById(rocket.RangeAnchorOwnerId) is { } rangeAnchorPlayer)
            {
                rocket.RefreshRangeOrigin(rangeAnchorPlayer.X, rangeAnchorPlayer.Y);
                if (rocket.EnableExperimentalStingerTracking
                    && projectiles.GetLastToDieGameplaySettings(rangeAnchorPlayer).EnableSoldierStingerRockets
                    && rangeAnchorPlayer.ClassId == PlayerClass.Soldier
                    && projectiles.IsExperimentalPracticePowerOwner(rangeAnchorPlayer))
                {
                    rocket.TrackExperimentalStingerTarget(
                        rangeAnchorPlayer.AimDirectionDegrees * (MathF.PI / 180f),
                        projectiles.GetExperimentalSoldierStingerTurnRateRadians());
                }
                else if (rocket.EnableExperimentalCaveatTracking
                    && projectiles.TryResolveExperimentalEngineerRocketTrackingDirection(rocket, rangeAnchorPlayer, out var engineerTrackingDirection))
                {
                    rocket.TrackExperimentalStingerTarget(
                        engineerTrackingDirection,
                        projectiles.GetExperimentalEngineerCaveatTurnRateRadians());
                }
            }

            if (rocket.IsFading)
            {
                rocket.AdvanceFade(deltaSeconds);
                if (rocket.IsExpired)
                {
                    projectiles.RemoveRocketAt(rocketIndex);
                    return;
                }
            }
            else
            {
                rocket.TryBeginFadeFromSourceRange();
            }

            if (rocket.ExplodeImmediately)
            {
                var explodeReason = string.IsNullOrWhiteSpace(rocket.DelayedExplosionReason)
                    ? "Unknown"
                    : rocket.DelayedExplosionReason;
                projectiles.SetLastRocketCollisionDebug(rocket.X, rocket.Y, "ExplodeImmediately", explodeReason);
                rocket.ClearDelayedExplosion();
                if (rocket.IsFading)
                {
                    projectiles.RemoveRocketAt(rocketIndex);
                }
                else
                {
                    projectiles.ExplodeRocket(rocket, null, null, null);
                }

                return;
            }

            rocket.AdvanceOneTick(deltaSeconds, projectiles._configuredGravityScale);
            var movementX = rocket.X - rocket.PreviousX;
            var movementY = rocket.Y - rocket.PreviousY;
            var movementDistance = MathF.Sqrt((movementX * movementX) + (movementY * movementY));
            if (movementDistance <= 0.0001f)
            {
                if (rocket.IsExpired)
                {
                    projectiles.RemoveRocketAt(rocketIndex);
                }

                return;
            }

            var directionX = movementX / movementDistance;
            var directionY = movementY / movementDistance;
            var universalProjectileScale = projectiles.FindPlayerById(rocket.OwnerId)?.LastToDieUniversalModifiers.ProjectileScale ?? 1f;
            var projectileGeometryScale = MathF.Max(
                0.1f,
                universalProjectileScale * (rocket.IsBallistic ? 1.3f : 1f));
            var hit = ResolveRocketCollisionAlongPath(
                projectiles,
                rocket,
                directionX,
                directionY,
                movementDistance,
                projectileGeometryScale);
            if (projectiles.TryInterceptWithCivilDefenseTurret(rocket.Team, rocket.PreviousX, rocket.PreviousY,
                    directionX, directionY, MathF.Min(movementDistance, hit?.Distance ?? movementDistance)))
            {
                projectiles.RemoveRocketAt(rocketIndex);
                return;
            }
            if (hit.HasValue)
            {
                var hitResult = hit.Value;
                var hitX = hitResult.HitX;
                var hitY = hitResult.HitY;
                var collisionObjectName = "Environment";
                if (hitResult.HitPlayer is not null)
                {
                    collisionObjectName = $"Player:{hitResult.HitPlayer.Id}";
                }
                else if (hitResult.HitSentry is not null)
                {
                    collisionObjectName = $"Sentry:{hitResult.HitSentry.Id}";
                }
                else if (hitResult.HitGenerator is not null)
                {
                    collisionObjectName = $"Generator:{hitResult.HitGenerator.Team}";
                }
                else if (hitResult.HitDamageableZoneRoomObjectIndex >= 0)
                {
                    collisionObjectName = $"DamageableZone:{hitResult.HitDamageableZoneRoomObjectIndex}";
                }

                if (hitResult.HitPlayer is null
                    && hitResult.HitSentry is null
                    && hitResult.HitGenerator is null
                    && hitResult.HitJumpPad is null
                    && hitResult.HitDamageableZoneRoomObjectIndex < 0)
                {
                    // The legacy GameMaker rocket uses a collision mask, so it explodes a few pixels
                    // before the projectile origin would mathematically touch the wall.
                    var backoffDistance = MathF.Min(hitResult.Distance, RocketProjectileEntity.EnvironmentCollisionBackoffDistance);
                    hitX -= directionX * backoffDistance;
                    hitY -= directionY * backoffDistance;
                    collisionObjectName = projectiles.ResolveRocketEnvironmentCollisionName(hitResult.HitX, hitResult.HitY);
                }

                rocket.MoveTo(hitX, hitY);
                projectiles.SetLastRocketCollisionDebug(hitX, hitY, collisionObjectName, "CollisionHit");
                projectiles.RegisterCombatTrace(rocket.PreviousX, rocket.PreviousY, directionX, directionY, hitResult.Distance, hitResult.HitPlayer is not null);

                if (rocket.IsFading
                    && hitResult.HitPlayer is null
                    && hitResult.HitSentry is null
                    && hitResult.HitGenerator is null
                    && hitResult.HitJumpPad is null
                    && hitResult.HitDamageableZoneRoomObjectIndex < 0)
                {
                    projectiles.RemoveRocketAt(rocketIndex);
                }
                else
                {
                    projectiles.ExplodeRocket(
                        rocket,
                        hitResult.HitPlayer,
                        hitResult.HitSentry,
                        hitResult.HitGenerator,
                        hitResult.HitDamageableZoneRoomObjectIndex);
                }
            }
            else
            {
                if (rocket.IsExpired)
                {
                    projectiles.RemoveRocketAt(rocketIndex);
                }
            }
        }

        private static int FindRocketIndex(ProjectileSystem projectiles, int rocketId)
        {
            for (var rocketIndex = projectiles._rockets.Count - 1; rocketIndex >= 0; rocketIndex -= 1)
            {
                if (projectiles._rockets[rocketIndex].Id == rocketId)
                {
                    return rocketIndex;
                }
            }

            return -1;
        }

        private static RocketHitResult? ResolveRocketCollisionAlongPath(
            ProjectileSystem projectiles,
            RocketProjectileEntity rocket,
            float directionX,
            float directionY,
            float movementDistance,
            float projectileGeometryScale)
        {
            const float maxCollisionStepDistance = 1f;
            if (movementDistance <= 0.0001f)
            {
                return null;
            }

            var stepCount = Math.Max(1, (int)MathF.Ceiling(movementDistance / maxCollisionStepDistance));
            var stepDistance = movementDistance / stepCount;
            var sampledDistance = 0f;
            for (var stepIndex = 0; stepIndex < stepCount; stepIndex += 1)
            {
                sampledDistance = MathF.Min(movementDistance, sampledDistance + stepDistance);
                var sampleX = rocket.PreviousX + (directionX * sampledDistance);
                var sampleY = rocket.PreviousY + (directionY * sampledDistance);
                var hit = ResolveRocketCollisionAtSamplePosition(
                    projectiles,
                    rocket,
                    directionX,
                    directionY,
                    sampledDistance,
                    sampleX,
                    sampleY,
                    projectileGeometryScale);
                if (hit.HasValue)
                {
                    return hit;
                }
            }

            return null;
        }

        private static RocketHitResult? ResolveRocketCollisionAtSamplePosition(
            ProjectileSystem projectiles,
            RocketProjectileEntity rocket,
            float directionX,
            float directionY,
            float movementDistance,
            float rocketX,
            float rocketY,
            float projectileGeometryScale)
        {
            if (movementDistance <= 0.0001f)
            {
                return null;
            }

            PlayerEntity? bestDirectHitPlayer = null;
            var bestDirectHitDistance = float.PositiveInfinity;
            foreach (var player in projectiles.EnumerateSimulatedPlayers())
            {
                if (!player.IsAlive || player.Id == rocket.OwnerId)
                {
                    continue;
                }

                GetRocketPlayerCollisionBounds(projectiles, player, out var left, out var top, out var right, out var bottom);
                if (!IntersectsRocketMaskRectangle(rocketX, rocketY, directionX, directionY, left, top, right, bottom, projectileGeometryScale))
                {
                    continue;
                }

                if (player.Team == rocket.Team || !projectiles.CanTeamDamagePlayer(rocket.Team, rocket.OwnerId, player))
                {
                    if (!rocket.IsFading)
                    {
                        rocket.TryRegisterFriendlyPassThrough(player.Id);
                    }

                    continue;
                }

                var directHitDistance = FindRocketDirectHitDistance(
                    rocket,
                    directionX,
                    directionY,
                    movementDistance,
                    left,
                    top,
                    right,
                    bottom,
                    projectileGeometryScale);
                if (bestDirectHitPlayer is null || directHitDistance < bestDirectHitDistance)
                {
                    bestDirectHitPlayer = player;
                    bestDirectHitDistance = directHitDistance;
                }
            }

            if (bestDirectHitPlayer is not null)
            {
                return new RocketHitResult(
                    bestDirectHitDistance,
                    rocket.PreviousX + (directionX * bestDirectHitDistance),
                    rocket.PreviousY + (directionY * bestDirectHitDistance),
                    bestDirectHitPlayer,
                    null,
                    null);
            }

            foreach (var sentry in projectiles._sentries)
            {
                if (sentry.Team == rocket.Team)
                {
                    continue;
                }

                if (IntersectsRocketMaskRectangle(
                    rocketX,
                    rocketY,
                    directionX,
                    directionY,
                    sentry.X - (SentryEntity.Width / 2f),
                    sentry.Y - (SentryEntity.Height / 2f),
                    sentry.X + (SentryEntity.Width / 2f),
                    sentry.Y + (SentryEntity.Height / 2f),
                    projectileGeometryScale))
                {
                    return new RocketHitResult(movementDistance, rocketX, rocketY, null, sentry, null);
                }
            }

            for (var index = 0; index < projectiles._generators.Count; index += 1)
            {
                var generator = projectiles._generators[index];
                if (generator.Team == rocket.Team || generator.IsDestroyed)
                {
                    continue;
                }

                if (IntersectsRocketMaskRectangle(
                    rocketX,
                    rocketY,
                    directionX,
                    directionY,
                    generator.Marker.Left,
                    generator.Marker.Top,
                    generator.Marker.Right,
                    generator.Marker.Bottom,
                    projectileGeometryScale))
                {
                    return new RocketHitResult(movementDistance, rocketX, rocketY, null, null, generator);
                }
            }

            var rocketBounds = GetRocketMaskBounds(rocketX, rocketY, directionX, directionY, projectileGeometryScale);
            foreach (var solid in projectiles.Level.Solids)
            {
                if (!RectanglesOverlap(rocketBounds.Left, rocketBounds.Top, rocketBounds.Right, rocketBounds.Bottom, solid.Left, solid.Top, solid.Right, solid.Bottom))
                {
                    continue;
                }

                if (IntersectsRocketMaskRectangle(rocketX, rocketY, directionX, directionY, solid.Left, solid.Top, solid.Right, solid.Bottom, projectileGeometryScale))
                {
                    return new RocketHitResult(movementDistance, rocketX, rocketY, null, null, null);
                }
            }

            foreach (var roomObjectIndex in projectiles.Level.ProjectileObstacleIndices)
            {
                if (!projectiles.Level.IsRoomObjectActive(roomObjectIndex))
                {
                    continue;
                }

                ref readonly var roomObject = ref projectiles.Level.GetRoomObject(roomObjectIndex);
                if (roomObject.Type == RoomObjectType.Barrier)
                {
                    if (BarrierCollision.BlocksProjectile(roomObject.Barrier, rocket.Team)
                        && BarrierProjectileRaycast.TryRaycastMarker(
                            roomObject.Barrier,
                            rocket.Team,
                            roomObject,
                            rocket.PreviousX,
                            rocket.PreviousY,
                            directionX,
                            directionY,
                            movementDistance,
                            out var barrierDistance))
                    {
                        return new RocketHitResult(
                            barrierDistance,
                            rocket.PreviousX + (directionX * barrierDistance),
                            rocket.PreviousY + (directionY * barrierDistance),
                            null,
                            null,
                            null);
                    }

                    continue;
                }

                if (roomObject.Type == RoomObjectType.DamageableZone)
                {
                    if (!projectiles.BlocksProjectileDamageableZone(roomObjectIndex)
                        || !RectanglesOverlap(
                            rocketBounds.Left,
                            rocketBounds.Top,
                            rocketBounds.Right,
                            rocketBounds.Bottom,
                            roomObject.Left,
                            roomObject.Top,
                            roomObject.Right,
                            roomObject.Bottom))
                    {
                        continue;
                    }

                    if (IntersectsRocketMaskRectangle(
                            rocketX,
                            rocketY,
                            directionX,
                            directionY,
                            roomObject.Left,
                            roomObject.Top,
                            roomObject.Right,
                            roomObject.Bottom))
                    {
                        return new RocketHitResult(movementDistance, rocketX, rocketY, null, null, null)
                        {
                            HitDamageableZoneRoomObjectIndex = roomObjectIndex,
                        };
                    }

                    continue;
                }

                if (!IsRocketBlockingRoomObject(projectiles, rocket, roomObject)
                    || !RectanglesOverlap(rocketBounds.Left, rocketBounds.Top, rocketBounds.Right, rocketBounds.Bottom, roomObject.Left, roomObject.Top, roomObject.Right, roomObject.Bottom))
                {
                    continue;
                }

                if (IntersectsRocketMaskRectangle(rocketX, rocketY, directionX, directionY, roomObject.Left, roomObject.Top, roomObject.Right, roomObject.Bottom, projectileGeometryScale))
                {
                    return new RocketHitResult(movementDistance, rocketX, rocketY, null, null, null);
                }
            }

            return null;
        }

        private static void GetRocketPlayerCollisionBounds(
            ProjectileSystem projectiles,
            PlayerEntity player,
            out float left,
            out float top,
            out float right,
            out float bottom)
        {
            projectiles.GetCachedPlayerPresentationHitBounds(player, out left, out top, out right, out bottom);
        }

        private static string? GetStandingSpriteName(ProjectileSystem projectiles, PlayerEntity player)
        {
            var leanDirection = GetPlayerLeanDirection(projectiles, player);
            if (leanDirection == 0)
            {
                return GetPresentationSpriteName(player.ClassId, player.Team, static presentation => presentation.StandSuffix ?? presentation.BaseSuffix, "StandS");
            }

            var facingLeft = player.IsSourceFacingLeft;
            return leanDirection < 0
                ? GetPresentationFacingSpriteName(
                    player.ClassId,
                    player.Team,
                    static presentation => presentation.LeanRightSuffix ?? presentation.BaseSuffix,
                    static presentation => presentation.LeanLeftSuffix ?? presentation.BaseSuffix,
                    facingLeft,
                    "LeanRS",
                    "LeanLS")
                : GetPresentationFacingSpriteName(
                    player.ClassId,
                    player.Team,
                    static presentation => presentation.LeanLeftSuffix ?? presentation.BaseSuffix,
                    static presentation => presentation.LeanRightSuffix ?? presentation.BaseSuffix,
                    facingLeft,
                    "LeanLS",
                    "LeanRS");
        }

        private static int GetPlayerLeanDirection(ProjectileSystem projectiles, PlayerEntity player)
        {
            var playerScale = player.PlayerScale;
            var bottom = player.Bottom + (2f * playerScale);
            var openRight = !IsPointBlockedForRocketPresentation(projectiles, player, player.X + (6f * playerScale), bottom)
                && !IsPointBlockedForRocketPresentation(projectiles, player, player.X + (2f * playerScale), bottom);
            var openLeft = !IsPointBlockedForRocketPresentation(projectiles, player, player.X - (7f * playerScale), bottom)
                && !IsPointBlockedForRocketPresentation(projectiles, player, player.X - (3f * playerScale), bottom);
            var leanDirection = 0;
            if (openRight)
            {
                leanDirection = 1;
            }

            if (openLeft)
            {
                leanDirection = -1;
            }

            if (openRight && openLeft)
            {
                openRight = !IsPointBlockedForRocketPresentation(projectiles, player, player.Right - playerScale, bottom);
                openLeft = !IsPointBlockedForRocketPresentation(projectiles, player, player.Left, bottom);
                leanDirection = 0;
                if (openRight)
                {
                    leanDirection = 1;
                }

                if (openLeft)
                {
                    leanDirection = -1;
                }
            }

            return leanDirection;
        }

        private static bool HasGroundSupportForRocketPresentation(ProjectileSystem projectiles, PlayerEntity player)
        {
            if (player.VerticalSpeed < 0f)
            {
                return false;
            }

            var playerScale = player.PlayerScale;
            var probeY = player.Bottom + playerScale;
            var leftProbeX = player.Left + MathF.Max(1f, 2f * playerScale);
            var centerProbeX = player.X;
            var rightProbeX = player.Right - MathF.Max(1f, 2f * playerScale);
            return IsPointBlockedForRocketPresentation(projectiles, player, leftProbeX, probeY)
                || IsPointBlockedForRocketPresentation(projectiles, player, centerProbeX, probeY)
                || IsPointBlockedForRocketPresentation(projectiles, player, rightProbeX, probeY);
        }

        private static bool IsPointBlockedForRocketPresentation(ProjectileSystem projectiles, PlayerEntity player, float x, float y)
        {
            foreach (var solid in projectiles.Level.Solids)
            {
                if (x >= solid.Left && x < solid.Right && y >= solid.Top && y < solid.Bottom)
                {
                    return true;
                }
            }

            foreach (var gate in projectiles.Level.GetBlockingTeamGates(player.Team, player.IsCarryingIntel))
            {
                if (x >= gate.Left && x < gate.Right && y >= gate.Top && y < gate.Bottom)
                {
                    return true;
                }
            }

            foreach (var wall in projectiles.Level.GetRoomObjects(RoomObjectType.PlayerWall))
            {
                if (x >= wall.Left && x < wall.Right && y >= wall.Top && y < wall.Bottom)
                {
                    return true;
                }
            }

            if (SimpleLevelBarrierCollision.BlocksPointForPlayer(projectiles.Level, player.Team, player.IsCarryingIntel, x, y))
            {
                return true;
            }

            return SimpleLevelBarrierCollision.BlocksPointForProjectile(projectiles.Level, player.Team, x, y);
        }

        private static string? GetPlayerSpriteName(PlayerClass classId, PlayerTeam team)
        {
            return GetPresentationSpriteName(classId, team, static presentation => presentation.BaseSuffix, "S");
        }

        private static string? GetPresentationSpriteName(
            PlayerClass classId,
            PlayerTeam team,
            Func<GameplayClassPresentationDefinition, string> suffixSelector,
            string legacySuffix)
        {
            var presentation = CharacterClassCatalog.RuntimeRegistry.GetClassDefinition(classId).Presentation;
            return GetTeamSpriteName(classId, team, presentation is null ? legacySuffix : suffixSelector(presentation));
        }

        private static string? GetPresentationFacingSpriteName(
            PlayerClass classId,
            PlayerTeam team,
            Func<GameplayClassPresentationDefinition, string> facingLeftSuffixSelector,
            Func<GameplayClassPresentationDefinition, string> facingRightSuffixSelector,
            bool facingLeft,
            string legacyFacingLeftSuffix,
            string legacyFacingRightSuffix)
        {
            var presentation = CharacterClassCatalog.RuntimeRegistry.GetClassDefinition(classId).Presentation;
            return GetTeamSpriteName(
                classId,
                team,
                presentation is null
                    ? (facingLeft ? legacyFacingLeftSuffix : legacyFacingRightSuffix)
                    : (facingLeft ? facingLeftSuffixSelector(presentation) : facingRightSuffixSelector(presentation)));
        }

        private static string? GetTeamSpriteName(PlayerClass classId, PlayerTeam team, string suffix)
        {
            var prefix = CharacterClassCatalog.RuntimeRegistry.GetClassDefinition(classId).Presentation?.SpritePrefix ?? GetPlayerSpritePrefix(classId);
            if (prefix is null)
            {
                return null;
            }

            var teamName = team switch
            {
                PlayerTeam.Red => "Red",
                PlayerTeam.Blue => "Blue",
                _ => null,
            };

            return teamName is null ? null : $"{prefix}{teamName}{suffix}";
        }

        private static string? GetPlayerSpritePrefix(PlayerClass classId)
        {
            return classId switch
            {
                PlayerClass.Scout => "Scout",
                PlayerClass.Engineer => "Engineer",
                PlayerClass.Pyro => "Pyro",
                PlayerClass.Soldier => "Soldier",
                PlayerClass.Demoman => "Demoman",
                PlayerClass.Heavy => "Heavy",
                PlayerClass.Sniper => "Sniper",
                PlayerClass.Medic => "Medic",
                PlayerClass.Spy => "Spy",
                PlayerClass.Quote => "Querly",
                _ => null,
            };
        }

        private static float FindRocketDirectHitDistance(
            RocketProjectileEntity rocket,
            float directionX,
            float directionY,
            float movementDistance,
            float targetLeft,
            float targetTop,
            float targetRight,
            float targetBottom,
            float projectileGeometryScale)
        {
            if (IntersectsRocketMaskRectangle(rocket.PreviousX, rocket.PreviousY, directionX, directionY, targetLeft, targetTop, targetRight, targetBottom, projectileGeometryScale))
            {
                return 0f;
            }

            var clearDistance = 0f;
            var overlapDistance = movementDistance;
            for (var iteration = 0; iteration < 12; iteration += 1)
            {
                var candidateDistance = (clearDistance + overlapDistance) * 0.5f;
                var candidateX = rocket.PreviousX + (directionX * candidateDistance);
                var candidateY = rocket.PreviousY + (directionY * candidateDistance);
                if (IntersectsRocketMaskRectangle(candidateX, candidateY, directionX, directionY, targetLeft, targetTop, targetRight, targetBottom, projectileGeometryScale))
                {
                    overlapDistance = candidateDistance;
                }
                else
                {
                    clearDistance = candidateDistance;
                }
            }

            return clearDistance;
        }

        private static bool IsRocketBlockingRoomObject(ProjectileSystem projectiles, RocketProjectileEntity rocket, RoomObjectMarker roomObject)
        {
            return roomObject.Type switch
            {
                RoomObjectType.TeamGate => true,
                RoomObjectType.ControlPointSetupGate => projectiles.Level.ControlPointSetupGatesActive,
                RoomObjectType.BulletWall => true,
                RoomObjectType.Barrier => BarrierCollision.BlocksProjectile(roomObject.Barrier, rocket.Team),
                RoomObjectType.DirectionalWall => roomObject.DirectionalWall.AffectsProjectiles,
                _ => false,
            };
        }

        private static bool IntersectsRocketMaskRectangle(
            float rocketX,
            float rocketY,
            float directionX,
            float directionY,
            float left,
            float top,
            float right,
            float bottom,
            float geometryScale = 1f)
        {
            geometryScale = float.IsFinite(geometryScale) ? MathF.Max(0.1f, geometryScale) : 1f;
            var segmentStartX = rocketX + (directionX * RocketProjectileEntity.MaskRearOffset * geometryScale);
            var segmentStartY = rocketY + (directionY * RocketProjectileEntity.MaskRearOffset * geometryScale);
            var segmentEndX = rocketX + (directionX * RocketProjectileEntity.MaskFrontOffset * geometryScale);
            var segmentEndY = rocketY + (directionY * RocketProjectileEntity.MaskFrontOffset * geometryScale);
            return GetThickLineIntersectionDistanceToRectangle(
                segmentStartX,
                segmentStartY,
                segmentEndX,
                segmentEndY,
                left,
                top,
                right,
                bottom,
                RocketProjectileEntity.MaskHalfThickness * geometryScale).HasValue;
        }

        private static RectangleHitbox GetRocketMaskBounds(
            float rocketX,
            float rocketY,
            float directionX,
            float directionY,
            float geometryScale = 1f)
        {
            geometryScale = float.IsFinite(geometryScale) ? MathF.Max(0.1f, geometryScale) : 1f;
            var segmentStartX = rocketX + (directionX * RocketProjectileEntity.MaskRearOffset * geometryScale);
            var segmentStartY = rocketY + (directionY * RocketProjectileEntity.MaskRearOffset * geometryScale);
            var segmentEndX = rocketX + (directionX * RocketProjectileEntity.MaskFrontOffset * geometryScale);
            var segmentEndY = rocketY + (directionY * RocketProjectileEntity.MaskFrontOffset * geometryScale);
            var halfThickness = RocketProjectileEntity.MaskHalfThickness * geometryScale;
            return new RectangleHitbox(
                MathF.Min(segmentStartX, segmentEndX) - halfThickness,
                MathF.Min(segmentStartY, segmentEndY) - halfThickness,
                MathF.Max(segmentStartX, segmentEndX) + halfThickness,
                MathF.Max(segmentStartY, segmentEndY) + halfThickness);
        }

        private static float? GetThickLineIntersectionDistanceToRectangle(
            float startX,
            float startY,
            float endX,
            float endY,
            float left,
            float top,
            float right,
            float bottom,
            float thicknessRadius)
        {
            var distance = MathF.Sqrt(((endX - startX) * (endX - startX)) + ((endY - startY) * (endY - startY)));
            if (distance <= 0.0001f)
            {
                return null;
            }

            var directionX = (endX - startX) / distance;
            var directionY = (endY - startY) / distance;
            return GetRayIntersectionDistanceWithExpandedRectangle(
                startX,
                startY,
                directionX,
                directionY,
                left - thicknessRadius,
                top - thicknessRadius,
                right + thicknessRadius,
                bottom + thicknessRadius,
                distance);
        }

        private static float? GetRayIntersectionDistanceWithExpandedRectangle(
            float originX,
            float originY,
            float directionX,
            float directionY,
            float left,
            float top,
            float right,
            float bottom,
            float maxDistance)
        {
            const float epsilon = 0.0001f;
            var inverseX = MathF.Abs(directionX) < epsilon ? float.PositiveInfinity : 1f / directionX;
            var inverseY = MathF.Abs(directionY) < epsilon ? float.PositiveInfinity : 1f / directionY;

            var t1 = (left - originX) * inverseX;
            var t2 = (right - originX) * inverseX;
            var t3 = (top - originY) * inverseY;
            var t4 = (bottom - originY) * inverseY;

            var tMin = MathF.Max(MathF.Min(t1, t2), MathF.Min(t3, t4));
            var tMax = MathF.Min(MathF.Max(t1, t2), MathF.Max(t3, t4));
            if (tMax < 0f || tMin > tMax)
            {
                return null;
            }

            var hitDistance = tMin >= 0f ? tMin : tMax;
            if (hitDistance < 0f || hitDistance > maxDistance)
            {
                return null;
            }

            return hitDistance;
        }

        private static bool RectanglesOverlap(float leftA, float topA, float rightA, float bottomA, float leftB, float topB, float rightB, float bottomB)
        {
            return leftA <= rightB
                && rightA >= leftB
                && topA <= bottomB
                && bottomA >= topB;
        }
    }
}
