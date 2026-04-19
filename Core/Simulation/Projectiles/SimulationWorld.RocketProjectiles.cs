using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private void AdvanceRockets()
    {
        RocketProjectileSystem.Advance(this);
    }

    private void RemoveRocketAt(int rocketIndex)
    {
        var rocket = _rockets[rocketIndex];
        _entities.Remove(rocket.Id);
        _rockets.RemoveAt(rocketIndex);
    }

    private static class RocketProjectileSystem
    {
        private static readonly Lazy<GameMakerAssetManifest> s_gameMakerAssets = new(GameMakerAssetManifestImporter.ImportProjectAssets);

        public static void Advance(SimulationWorld world)
        {
            var deltaSeconds = (float)world.Config.FixedDeltaSeconds;
            for (var rocketIndex = world._rockets.Count - 1; rocketIndex >= 0; rocketIndex -= 1)
            {
                AdvanceRocket(world, rocketIndex, deltaSeconds);
            }
        }

        public static void AdvancePendingForOwner(SimulationWorld world, int ownerId)
        {
            var deltaSeconds = (float)world.Config.FixedDeltaSeconds;
            for (var pendingIndex = world._pendingNewRocketIds.Count - 1; pendingIndex >= 0; pendingIndex -= 1)
            {
                var rocketId = world._pendingNewRocketIds[pendingIndex];
                var rocketIndex = FindRocketIndex(world, rocketId);
                if (rocketIndex < 0)
                {
                    world._pendingNewRocketIds.RemoveAt(pendingIndex);
                    continue;
                }

                if (world._rockets[rocketIndex].OwnerId != ownerId)
                {
                    continue;
                }

                world._pendingNewRocketIds.RemoveAt(pendingIndex);
                AdvanceRocket(world, rocketIndex, deltaSeconds);
            }
        }

        private static void AdvanceRocket(SimulationWorld world, int rocketIndex, float deltaSeconds)
        {
            if (rocketIndex < 0 || rocketIndex >= world._rockets.Count)
            {
                return;
            }

            var rocket = world._rockets[rocketIndex];
            if (world.FindPlayerById(rocket.RangeAnchorOwnerId) is { } rangeAnchorPlayer)
            {
                rocket.RefreshRangeOrigin(rangeAnchorPlayer.X, rangeAnchorPlayer.Y);
                if (rocket.EnableExperimentalStingerTracking
                    && world.ExperimentalGameplaySettings.EnableSoldierStingerRockets
                    && rangeAnchorPlayer.ClassId == PlayerClass.Soldier
                    && world.IsExperimentalPracticePowerOwner(rangeAnchorPlayer))
                {
                    rocket.TrackExperimentalStingerTarget(
                        rangeAnchorPlayer.AimDirectionDegrees * (MathF.PI / 180f),
                        SimulationWorld.GetExperimentalSoldierStingerTurnRateRadians());
                }
            }

            if (rocket.IsFading)
            {
                rocket.AdvanceFade(deltaSeconds);
                if (rocket.IsExpired)
                {
                    world.RemoveRocketAt(rocketIndex);
                    return;
                }
            }
            else
            {
                rocket.TryBeginFadeFromSourceRange();
            }

            if (rocket.ExplodeImmediately)
            {
                rocket.ClearDelayedExplosion();
                if (rocket.IsFading)
                {
                    world.RemoveRocketAt(rocketIndex);
                }
                else
                {
                    world.ExplodeRocket(rocket, directHitPlayer: null, directHitSentry: null, directHitGenerator: null);
                }

                return;
            }

            rocket.AdvanceOneTick(deltaSeconds);
            var movementX = rocket.X - rocket.PreviousX;
            var movementY = rocket.Y - rocket.PreviousY;
            var movementDistance = MathF.Sqrt((movementX * movementX) + (movementY * movementY));
            if (movementDistance <= 0.0001f)
            {
                if (rocket.IsExpired)
                {
                    world.RemoveRocketAt(rocketIndex);
                }

                return;
            }

            var directionX = movementX / movementDistance;
            var directionY = movementY / movementDistance;
            var hit = ResolveRocketCollisionAlongPath(world, rocket, directionX, directionY, movementDistance);
            if (hit.HasValue)
            {
                var hitResult = hit.Value;
                rocket.MoveTo(hitResult.HitX, hitResult.HitY);
                world.RegisterCombatTrace(rocket.PreviousX, rocket.PreviousY, directionX, directionY, hitResult.Distance, hitResult.HitPlayer is not null);
                if (rocket.IsFading
                    && hitResult.HitPlayer is null
                    && hitResult.HitSentry is null
                    && hitResult.HitGenerator is null)
                {
                    world.RemoveRocketAt(rocketIndex);
                }
                else
                {
                    world.ExplodeRocket(rocket, hitResult.HitPlayer, hitResult.HitSentry, hitResult.HitGenerator);
                }
            }
            else
            {
                if (rocket.IsExpired)
                {
                    world.RemoveRocketAt(rocketIndex);
                }
            }
        }

        private static int FindRocketIndex(SimulationWorld world, int rocketId)
        {
            for (var rocketIndex = world._rockets.Count - 1; rocketIndex >= 0; rocketIndex -= 1)
            {
                if (world._rockets[rocketIndex].Id == rocketId)
                {
                    return rocketIndex;
                }
            }

            return -1;
        }

        private static RocketHitResult? ResolveRocketCollisionAlongPath(
            SimulationWorld world,
            RocketProjectileEntity rocket,
            float directionX,
            float directionY,
            float movementDistance)
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
                var hit = ResolveRocketCollisionAtSamplePosition(world, rocket, directionX, directionY, sampledDistance, sampleX, sampleY);
                if (hit.HasValue)
                {
                    return hit;
                }
            }

            return null;
        }

        private static RocketHitResult? ResolveRocketCollisionAtSamplePosition(
            SimulationWorld world,
            RocketProjectileEntity rocket,
            float directionX,
            float directionY,
            float movementDistance,
            float rocketX,
            float rocketY)
        {
            if (movementDistance <= 0.0001f)
            {
                return null;
            }

            PlayerEntity? bestDirectHitPlayer = null;
            var bestDirectHitDistance = float.PositiveInfinity;
            foreach (var player in world.EnumerateSimulatedPlayers())
            {
                if (!player.IsAlive || player.Id == rocket.OwnerId)
                {
                    continue;
                }

                GetRocketPlayerCollisionBounds(world, player, out var left, out var top, out var right, out var bottom);
                if (!IntersectsRocketMaskRectangle(rocketX, rocketY, directionX, directionY, left, top, right, bottom))
                {
                    continue;
                }

                if (player.Team == rocket.Team || !world.CanTeamDamagePlayer(rocket.Team, rocket.OwnerId, player))
                {
                    if (!rocket.IsFading)
                    {
                        rocket.TryRegisterFriendlyPassThrough(player.Id);
                    }

                    continue;
                }

                var directHitDistance = FindRocketDirectHitDistance(rocket, directionX, directionY, movementDistance, left, top, right, bottom);
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

            foreach (var sentry in world._sentries)
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
                    sentry.Y + (SentryEntity.Height / 2f)))
                {
                    return new RocketHitResult(movementDistance, rocketX, rocketY, null, sentry, null);
                }
            }

            for (var index = 0; index < world._generators.Count; index += 1)
            {
                var generator = world._generators[index];
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
                    generator.Marker.Bottom))
                {
                    return new RocketHitResult(movementDistance, rocketX, rocketY, null, null, generator);
                }
            }

            var rocketBounds = GetRocketMaskBounds(rocketX, rocketY, directionX, directionY);
            foreach (var solid in world.Level.Solids)
            {
                if (!RectanglesOverlap(rocketBounds.Left, rocketBounds.Top, rocketBounds.Right, rocketBounds.Bottom, solid.Left, solid.Top, solid.Right, solid.Bottom))
                {
                    continue;
                }

                if (IntersectsRocketMaskRectangle(rocketX, rocketY, directionX, directionY, solid.Left, solid.Top, solid.Right, solid.Bottom))
                {
                    return new RocketHitResult(movementDistance, rocketX, rocketY, null, null, null);
                }
            }

            foreach (var roomObject in world.Level.RoomObjects)
            {
                if (!IsRocketBlockingRoomObject(world, roomObject)
                    || !RectanglesOverlap(rocketBounds.Left, rocketBounds.Top, rocketBounds.Right, rocketBounds.Bottom, roomObject.Left, roomObject.Top, roomObject.Right, roomObject.Bottom))
                {
                    continue;
                }

                if (IntersectsRocketMaskRectangle(rocketX, rocketY, directionX, directionY, roomObject.Left, roomObject.Top, roomObject.Right, roomObject.Bottom))
                {
                    return new RocketHitResult(movementDistance, rocketX, rocketY, null, null, null);
                }
            }

            return null;
        }

        private static void GetRocketPlayerCollisionBounds(
            SimulationWorld world,
            PlayerEntity player,
            out float left,
            out float top,
            out float right,
            out float bottom)
        {
            GetPlayerPresentationHitBounds(world, player, out left, out top, out right, out bottom);
        }

        private static bool TryGetRocketPlayerSpriteMaskBounds(
            SimulationWorld world,
            PlayerEntity player,
            out float left,
            out float top,
            out float right,
            out float bottom)
        {
            left = 0f;
            top = 0f;
            right = 0f;
            bottom = 0f;
            var spriteName = GetRocketPlayerBodySpriteName(world, player);
            if (string.IsNullOrWhiteSpace(spriteName))
            {
                return false;
            }

            if (!s_gameMakerAssets.Value.Sprites.TryGetValue(spriteName, out var sprite))
            {
                return false;
            }

            var mask = sprite.Mask;
            if (!mask.Left.HasValue || !mask.Top.HasValue || !mask.Right.HasValue || !mask.Bottom.HasValue)
            {
                return false;
            }

            var playerScale = player.PlayerScale;
            left = player.X + ((mask.Left.Value - sprite.OriginX) * playerScale);
            top = player.Y + ((mask.Top.Value - sprite.OriginY) * playerScale);
            right = player.X + (((mask.Right.Value - sprite.OriginX) + 1f) * playerScale);
            bottom = player.Y + (((mask.Bottom.Value - sprite.OriginY) + 1f) * playerScale);
            return true;
        }

        private static string? GetRocketPlayerBodySpriteName(SimulationWorld world, PlayerEntity player)
        {
            if (world.IsPlayerHumiliated(player))
            {
                return GetPresentationSpriteName(
                    player.ClassId,
                    player.Team,
                    static presentation => presentation.HumiliationSuffix ?? presentation.BaseSuffix,
                    "HS");
            }

            if (player.ClassId == PlayerClass.Quote)
            {
                return GetPlayerSpriteName(player.ClassId, player.Team);
            }

            if (player.IsHeavyEating)
            {
                return player.ClassId == PlayerClass.Heavy
                    ? GetPresentationSpriteName(player.ClassId, player.Team, static presentation => presentation.HeavyEatSuffix ?? presentation.BaseSuffix, "OmnomnomnomS")
                    : GetPlayerSpriteName(player.ClassId, player.Team);
            }

            if (player.IsTaunting)
            {
                return GetPresentationSpriteName(player.ClassId, player.Team, static presentation => presentation.TauntSuffix ?? presentation.BaseSuffix, "TauntS");
            }

            if (player.ClassId == PlayerClass.Sniper && player.IsSniperScoped)
            {
                return GetPresentationSpriteName(player.ClassId, player.Team, static presentation => presentation.ScopedSuffix ?? presentation.BaseSuffix, "CrouchS");
            }

            var horizontalSourceStepSpeed = MathF.Abs(player.HorizontalSpeed) / LegacyMovementModel.SourceTicksPerSecond;
            var appearsAirborne = !player.IsGrounded;
            if (appearsAirborne && HasGroundSupportForRocketPresentation(world, player))
            {
                appearsAirborne = false;
            }

            if (appearsAirborne)
            {
                return GetPresentationSpriteName(player.ClassId, player.Team, static presentation => presentation.JumpSuffix ?? presentation.BaseSuffix, "JumpS");
            }

            if (horizontalSourceStepSpeed < 0.2f)
            {
                return GetStandingSpriteName(world, player);
            }

            if (player.ClassId == PlayerClass.Heavy && horizontalSourceStepSpeed < 3f)
            {
                return GetPresentationSpriteName(player.ClassId, player.Team, static presentation => presentation.WalkSuffix ?? presentation.RunSuffix ?? presentation.BaseSuffix, "WalkS");
            }

            return GetPresentationSpriteName(player.ClassId, player.Team, static presentation => presentation.RunSuffix ?? presentation.BaseSuffix, "RunS");
        }

        private static string? GetStandingSpriteName(SimulationWorld world, PlayerEntity player)
        {
            var leanDirection = GetPlayerLeanDirection(world, player);
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

        private static int GetPlayerLeanDirection(SimulationWorld world, PlayerEntity player)
        {
            var playerScale = player.PlayerScale;
            var bottom = player.Bottom + (2f * playerScale);
            var openRight = !IsPointBlockedForRocketPresentation(world, player, player.X + (6f * playerScale), bottom)
                && !IsPointBlockedForRocketPresentation(world, player, player.X + (2f * playerScale), bottom);
            var openLeft = !IsPointBlockedForRocketPresentation(world, player, player.X - (7f * playerScale), bottom)
                && !IsPointBlockedForRocketPresentation(world, player, player.X - (3f * playerScale), bottom);
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
                openRight = !IsPointBlockedForRocketPresentation(world, player, player.Right - playerScale, bottom);
                openLeft = !IsPointBlockedForRocketPresentation(world, player, player.Left, bottom);
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

        private static bool HasGroundSupportForRocketPresentation(SimulationWorld world, PlayerEntity player)
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
            return IsPointBlockedForRocketPresentation(world, player, leftProbeX, probeY)
                || IsPointBlockedForRocketPresentation(world, player, centerProbeX, probeY)
                || IsPointBlockedForRocketPresentation(world, player, rightProbeX, probeY);
        }

        private static bool IsPointBlockedForRocketPresentation(SimulationWorld world, PlayerEntity player, float x, float y)
        {
            foreach (var solid in world.Level.Solids)
            {
                if (x >= solid.Left && x < solid.Right && y >= solid.Top && y < solid.Bottom)
                {
                    return true;
                }
            }

            foreach (var gate in world.Level.GetBlockingTeamGates(player.Team, player.IsCarryingIntel))
            {
                if (x >= gate.Left && x < gate.Right && y >= gate.Top && y < gate.Bottom)
                {
                    return true;
                }
            }

            foreach (var wall in world.Level.GetRoomObjects(RoomObjectType.PlayerWall))
            {
                if (x >= wall.Left && x < wall.Right && y >= wall.Top && y < wall.Bottom)
                {
                    return true;
                }
            }

            return false;
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
            float targetBottom)
        {
            if (IntersectsRocketMaskRectangle(rocket.PreviousX, rocket.PreviousY, directionX, directionY, targetLeft, targetTop, targetRight, targetBottom))
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
                if (IntersectsRocketMaskRectangle(candidateX, candidateY, directionX, directionY, targetLeft, targetTop, targetRight, targetBottom))
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

        private static bool IsRocketBlockingRoomObject(SimulationWorld world, RoomObjectMarker roomObject)
        {
            return roomObject.Type switch
            {
                RoomObjectType.TeamGate => true,
                RoomObjectType.ControlPointSetupGate => world.Level.ControlPointSetupGatesActive,
                RoomObjectType.BulletWall => true,
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
            float bottom)
        {
            var segmentStartX = rocketX + (directionX * RocketProjectileEntity.MaskRearOffset);
            var segmentStartY = rocketY + (directionY * RocketProjectileEntity.MaskRearOffset);
            var segmentEndX = rocketX + (directionX * RocketProjectileEntity.MaskFrontOffset);
            var segmentEndY = rocketY + (directionY * RocketProjectileEntity.MaskFrontOffset);
            return GetThickLineIntersectionDistanceToRectangle(
                segmentStartX,
                segmentStartY,
                segmentEndX,
                segmentEndY,
                left,
                top,
                right,
                bottom,
                RocketProjectileEntity.MaskHalfThickness).HasValue;
        }

        private static RectangleHitbox GetRocketMaskBounds(float rocketX, float rocketY, float directionX, float directionY)
        {
            var segmentStartX = rocketX + (directionX * RocketProjectileEntity.MaskRearOffset);
            var segmentStartY = rocketY + (directionY * RocketProjectileEntity.MaskRearOffset);
            var segmentEndX = rocketX + (directionX * RocketProjectileEntity.MaskFrontOffset);
            var segmentEndY = rocketY + (directionY * RocketProjectileEntity.MaskFrontOffset);
            return new RectangleHitbox(
                MathF.Min(segmentStartX, segmentEndX) - RocketProjectileEntity.MaskHalfThickness,
                MathF.Min(segmentStartY, segmentEndY) - RocketProjectileEntity.MaskHalfThickness,
                MathF.Max(segmentStartX, segmentEndX) + RocketProjectileEntity.MaskHalfThickness,
                MathF.Max(segmentStartY, segmentEndY) + RocketProjectileEntity.MaskHalfThickness);
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
