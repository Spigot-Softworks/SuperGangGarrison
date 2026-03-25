namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private sealed partial class CombatResolver
    {
        public ShotHitResult? GetNearestStabHit(StabMaskEntity mask, float directionX, float directionY)
        {
            ShotHitResult? nearestHit = null;
            var originX = GetStabOriginX(mask, directionX);
            var originY = GetStabOriginY(mask, directionY);
            var thicknessRadius = StabMaskEntity.Thickness / 2f;

            UpdateNearestStabHitFromSolids(ref nearestHit, originX, originY, directionX, directionY, thicknessRadius);
            UpdateNearestStabHitFromGates(ref nearestHit, originX, originY, directionX, directionY, thicknessRadius);
            UpdateNearestStabHitFromSentries(ref nearestHit, originX, originY, mask, directionX, directionY, thicknessRadius);
            UpdateNearestStabHitFromPlayers(ref nearestHit, originX, originY, mask, directionX, directionY, thicknessRadius);
            return nearestHit;
        }

        private void UpdateNearestStabHitFromSolids(
            ref ShotHitResult? nearestHit,
            float originX,
            float originY,
            float directionX,
            float directionY,
            float thicknessRadius)
        {
            foreach (var solid in Level.Solids)
            {
                var distance = GetThickRayIntersectionDistanceWithRectangle(originX, originY, directionX, directionY, solid.Left, solid.Top, solid.Right, solid.Bottom, StabMaskEntity.ReachLength, thicknessRadius);
                if (distance.HasValue) { UpdateNearestStabHit(ref nearestHit, originX, originY, directionX, directionY, distance.Value, null); }
            }
        }

        private void UpdateNearestStabHitFromGates(
            ref ShotHitResult? nearestHit,
            float originX,
            float originY,
            float directionX,
            float directionY,
            float thicknessRadius)
        {
            foreach (var roomObject in Level.RoomObjects)
            {
                if (!IsBlockingGate(roomObject)) { continue; }
                var distance = GetThickRayIntersectionDistanceWithRectangle(originX, originY, directionX, directionY, roomObject.Left, roomObject.Top, roomObject.Right, roomObject.Bottom, StabMaskEntity.ReachLength, thicknessRadius);
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
            float thicknessRadius)
        {
            foreach (var player in EnumerateSimulatedPlayers())
            {
                if (!player.IsAlive || player.Team == mask.Team || player.Id == mask.OwnerId) { continue; }
                player.GetCollisionBounds(out var left, out var top, out var right, out var bottom);
                var distance = GetThickRayIntersectionDistanceWithRectangle(originX, originY, directionX, directionY, left, top, right, bottom, StabMaskEntity.ReachLength, thicknessRadius);
                if (distance.HasValue) { UpdateNearestStabHit(ref nearestHit, originX, originY, directionX, directionY, distance.Value, player); }
            }
        }

        private void UpdateNearestStabHitFromSentries(
            ref ShotHitResult? nearestHit,
            float originX,
            float originY,
            StabMaskEntity mask,
            float directionX,
            float directionY,
            float thicknessRadius)
        {
            foreach (var sentry in _sentries)
            {
                if (sentry.Team == mask.Team) { continue; }
                var distance = GetThickRayIntersectionDistanceWithSentry(originX, originY, directionX, directionY, sentry, StabMaskEntity.ReachLength, thicknessRadius);
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
            SentryEntity? sentry = null)
        {
            if (nearestHit.HasValue && nearestHit.Value.Distance <= distance) { return; }
            nearestHit = new ShotHitResult(distance, originX + directionX * distance, originY + directionY * distance, player, sentry, null);
        }
    }
}
