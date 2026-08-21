namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private sealed partial class CombatResolver
    {
        public ShotHitResult? GetNearestStabHit(StabMaskEntity mask, float directionX, float directionY)
        {
            ShotHitResult? nearestHit = null;
            GetStabSweep(mask, directionX, out var originX, out var originY, out var reachLength, out var verticalRadius);

            UpdateNearestStabHitFromSolids(ref nearestHit, originX, originY, directionX, directionY, reachLength, verticalRadius);
            UpdateNearestStabHitFromGates(ref nearestHit, originX, originY, directionX, directionY, reachLength, verticalRadius);
            UpdateNearestStabHitFromDamageableZones(ref nearestHit, originX, originY, directionX, directionY, reachLength, verticalRadius);
            UpdateNearestStabHitFromSentries(ref nearestHit, originX, originY, mask, directionX, directionY, reachLength, verticalRadius);
            UpdateNearestStabHitFromPlayers(ref nearestHit, originX, originY, mask, directionX, directionY, reachLength, verticalRadius);
            return nearestHit;
        }

        public ShotHitResult? GetNearestHealstabHit(StabMaskEntity mask, float directionX, float directionY)
        {
            ShotHitResult? nearestHit = null;
            GetStabSweep(mask, directionX, out var originX, out var originY, out var reachLength, out var verticalRadius);

            UpdateNearestStabHitFromSolids(ref nearestHit, originX, originY, directionX, directionY, reachLength, verticalRadius);
            UpdateNearestStabHitFromGates(ref nearestHit, originX, originY, directionX, directionY, reachLength, verticalRadius);
            foreach (var player in EnumerateSimulatedPlayers())
            {
                if (player.Id == mask.OwnerId
                    || !player.IsAlive
                    || player.Team != mask.Team
                    || player.Health >= player.MaxHealth)
                {
                    continue;
                }

                GetStabTargetBounds(player, out var left, out var top, out var right, out var bottom);
                var distance = GetStabSweepIntersectionDistanceWithRectangle(
                    originX,
                    originY,
                    directionX,
                    directionY,
                    left,
                    top,
                    right,
                    bottom,
                    reachLength,
                    verticalRadius);
                if (distance.HasValue)
                {
                    UpdateNearestStabHit(
                        ref nearestHit,
                        originX,
                        originY,
                        directionX,
                        directionY,
                        distance.Value,
                        player);
                }
            }

            return nearestHit;
        }

        private static void GetStabSweep(
            StabMaskEntity mask,
            float directionX,
            out float originX,
            out float originY,
            out float reachLength,
            out float verticalRadius)
        {
            mask.GetHitBounds(out var left, out var top, out var right, out var bottom);
            originX = directionX < 0f ? right : left;
            originY = (top + bottom) * 0.5f;
            reachLength = MathF.Max(0f, right - left);
            verticalRadius = MathF.Max(0f, (bottom - top) * 0.5f);
        }

        private static float? GetStabSweepIntersectionDistanceWithRectangle(
            float originX,
            float originY,
            float directionX,
            float directionY,
            float left,
            float top,
            float right,
            float bottom,
            float reachLength,
            float verticalRadius)
        {
            // A legacy stab is a horizontally swept rectangular mask. Inflate
            // only the target's vertical slab; isotropic ray thickness extends
            // the stab beyond its authored left/right bounds.
            return GetRayIntersectionDistanceWithRectangle(
                originX,
                originY,
                directionX,
                directionY,
                left,
                top - verticalRadius,
                right,
                bottom + verticalRadius,
                reachLength);
        }

        public bool HasStabChainLineOfSight(float originX, float originY, float targetX, float targetY)
        {
            var deltaX = targetX - originX;
            var deltaY = targetY - originY;
            var distance = MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
            if (distance <= 0.0001f)
            {
                return true;
            }

            var directionX = deltaX / distance;
            var directionY = deltaY / distance;
            foreach (var solid in Level.Solids)
            {
                var hitDistance = GetThickRayIntersectionDistanceWithRectangle(
                    originX,
                    originY,
                    directionX,
                    directionY,
                    solid.Left,
                    solid.Top,
                    solid.Right,
                    solid.Bottom,
                    distance,
                    thicknessRadius: 0f);
                if (hitDistance is { } value && value < distance - 0.001f)
                {
                    return false;
                }
            }

            foreach (var roomObject in Level.RoomObjects)
            {
                if (!IsBlockingGate(roomObject))
                {
                    continue;
                }

                var hitDistance = GetThickRayIntersectionDistanceWithRectangle(
                    originX,
                    originY,
                    directionX,
                    directionY,
                    roomObject.Left,
                    roomObject.Top,
                    roomObject.Right,
                    roomObject.Bottom,
                    distance,
                    thicknessRadius: 0f);
                if (hitDistance is { } value && value < distance - 0.001f)
                {
                    return false;
                }
            }

            return true;
        }

        private void UpdateNearestStabHitFromDamageableZones(
            ref ShotHitResult? nearestHit,
            float originX,
            float originY,
            float directionX,
            float directionY,
            float reachLength,
            float verticalRadius)
        {
            for (var roomObjectIndex = 0; roomObjectIndex < Level.RoomObjects.Count; roomObjectIndex += 1)
            {
                if (!_world.Level.IsRoomObjectActive(roomObjectIndex))
                {
                    continue;
                }

                var roomObject = Level.RoomObjects[roomObjectIndex];
                if (roomObject.Type != RoomObjectType.DamageableZone
                    || !DamageableMetadata.IsStabbableTarget(
                        roomObject.DamageableZone,
                        _world.GetDamageableZoneHealth(roomObjectIndex)))
                {
                    continue;
                }

                var distance = GetStabSweepIntersectionDistanceWithRectangle(
                    originX,
                    originY,
                    directionX,
                    directionY,
                    roomObject.Left,
                    roomObject.Top,
                    roomObject.Right,
                    roomObject.Bottom,
                    reachLength,
                    verticalRadius);
                if (distance.HasValue)
                {
                    UpdateNearestStabHit(
                        ref nearestHit,
                        originX,
                        originY,
                        directionX,
                        directionY,
                        distance.Value,
                        null,
                        hitDamageableZoneRoomObjectIndex: roomObjectIndex);
                }
            }
        }

        private void UpdateNearestStabHitFromSolids(
            ref ShotHitResult? nearestHit,
            float originX,
            float originY,
            float directionX,
            float directionY,
            float reachLength,
            float verticalRadius)
        {
            foreach (var solid in Level.Solids)
            {
                var distance = GetStabSweepIntersectionDistanceWithRectangle(originX, originY, directionX, directionY, solid.Left, solid.Top, solid.Right, solid.Bottom, reachLength, verticalRadius);
                if (distance.HasValue) { UpdateNearestStabHit(ref nearestHit, originX, originY, directionX, directionY, distance.Value, null); }
            }
        }

        private void UpdateNearestStabHitFromGates(
            ref ShotHitResult? nearestHit,
            float originX,
            float originY,
            float directionX,
            float directionY,
            float reachLength,
            float verticalRadius)
        {
            foreach (var roomObject in Level.RoomObjects)
            {
                if (!IsBlockingGate(roomObject)) { continue; }
                var distance = GetStabSweepIntersectionDistanceWithRectangle(originX, originY, directionX, directionY, roomObject.Left, roomObject.Top, roomObject.Right, roomObject.Bottom, reachLength, verticalRadius);
                if (distance.HasValue) { UpdateNearestStabHit(ref nearestHit, originX, originY, directionX, directionY, distance.Value, null); }
            }
        }

        private void UpdateNearestStabHitFromPlayers(
            ref ShotHitResult? nearestHit,
            float originX,
            float originY,
            StabMaskEntity mask,
            float directionX,
            float directionY,
            float reachLength,
            float verticalRadius)
        {
            foreach (var player in EnumerateSimulatedPlayers())
            {
                if (player.Team == mask.Team
                    || !_world.CanTeamDamagePlayer(mask.Team, mask.OwnerId, player)
                    || player.Id == mask.OwnerId)
                {
                    continue;
                }
                GetStabTargetBounds(player, out var left, out var top, out var right, out var bottom);
                var distance = GetStabSweepIntersectionDistanceWithRectangle(originX, originY, directionX, directionY, left, top, right, bottom, reachLength, verticalRadius);
                if (distance.HasValue) { UpdateNearestStabHit(ref nearestHit, originX, originY, directionX, directionY, distance.Value, player); }
            }
        }

        private void GetStabTargetBounds(
            PlayerEntity player,
            out float left,
            out float top,
            out float right,
            out float bottom)
        {
            _world.GetCachedPlayerPresentationHitBounds(
                player,
                out var presentationLeft,
                out var presentationTop,
                out var presentationRight,
                out var presentationBottom);
            player.GetCollisionBounds(
                out var collisionLeft,
                out var collisionTop,
                out var collisionRight,
                out var collisionBottom);

            // Stabs historically target the physical body, while several tall
            // attack/taunt frames legitimately extend above that box. The union
            // preserves those visual overlaps without letting a narrow or
            // displaced animation mask make the physical body unstabbable.
            left = MathF.Min(presentationLeft, collisionLeft);
            top = MathF.Min(presentationTop, collisionTop);
            right = MathF.Max(presentationRight, collisionRight);
            bottom = MathF.Max(presentationBottom, collisionBottom);
        }

        private void UpdateNearestStabHitFromSentries(
            ref ShotHitResult? nearestHit,
            float originX,
            float originY,
            StabMaskEntity mask,
            float directionX,
            float directionY,
            float reachLength,
            float verticalRadius)
        {
            foreach (var sentry in _sentries)
            {
                if (sentry.Team == mask.Team) { continue; }
                var distance = GetStabSweepIntersectionDistanceWithRectangle(
                    originX,
                    originY,
                    directionX,
                    directionY,
                    sentry.X - (SentryEntity.Width / 2f),
                    sentry.Y - (SentryEntity.Height / 2f),
                    sentry.X + (SentryEntity.Width / 2f),
                    sentry.Y + (SentryEntity.Height / 2f),
                    reachLength,
                    verticalRadius);
                if (distance.HasValue) { UpdateNearestStabHit(ref nearestHit, originX, originY, directionX, directionY, distance.Value, null, sentry); }
            }
        }

        private static void UpdateNearestStabHit(
            ref ShotHitResult? nearestHit,
            float originX,
            float originY,
            float directionX,
            float directionY,
            float distance,
            PlayerEntity? player,
            SentryEntity? sentry = null,
            int hitDamageableZoneRoomObjectIndex = -1)
        {
            if (nearestHit.HasValue && nearestHit.Value.Distance <= distance) { return; }
            nearestHit = new ShotHitResult(
                distance,
                originX + directionX * distance,
                originY + directionY * distance,
                player,
                sentry,
                null)
            {
                HitDamageableZoneRoomObjectIndex = hitDamageableZoneRoomObjectIndex,
            };
        }
    }
}
