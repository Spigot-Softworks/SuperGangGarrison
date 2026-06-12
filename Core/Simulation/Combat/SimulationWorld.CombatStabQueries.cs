namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private sealed partial class CombatResolver
    {
        public ShotHitResult? GetNearestStabHit(StabMaskEntity mask, float directionX, float directionY)
        {
            mask.GetHitBounds(out var maskLeft, out var maskTop, out var maskRight, out var maskBottom);
            var facingDirectionX = mask.FacingLeft ? -1f : 1f;
            ShotHitResult? nearestHit = GetNearestStabBlockerHit(mask, maskLeft, maskTop, maskRight, maskBottom, facingDirectionX);

            UpdateNearestStabHitFromSentries(ref nearestHit, mask, maskLeft, maskTop, maskRight, maskBottom, facingDirectionX);
            UpdateNearestStabHitFromPlayers(ref nearestHit, mask, maskLeft, maskTop, maskRight, maskBottom, facingDirectionX);
            return nearestHit;
        }

        private ShotHitResult? GetNearestStabBlockerHit(
            StabMaskEntity mask,
            float maskLeft,
            float maskTop,
            float maskRight,
            float maskBottom,
            float facingDirectionX)
        {
            ShotHitResult? nearestHit = null;
            foreach (var solid in Level.Solids)
            {
                UpdateNearestStabBlockerHit(ref nearestHit, mask, maskLeft, maskTop, maskRight, maskBottom, facingDirectionX, solid.Left, solid.Top, solid.Right, solid.Bottom);
            }

            foreach (var roomObject in Level.RoomObjects)
            {
                if (!IsBlockingGate(roomObject)) { continue; }
                UpdateNearestStabBlockerHit(ref nearestHit, mask, maskLeft, maskTop, maskRight, maskBottom, facingDirectionX, roomObject.Left, roomObject.Top, roomObject.Right, roomObject.Bottom);
            }

            return nearestHit;
        }

        private static void UpdateNearestStabBlockerHit(
            ref ShotHitResult? nearestHit,
            StabMaskEntity mask,
            float maskLeft,
            float maskTop,
            float maskRight,
            float maskBottom,
            float facingDirectionX,
            float left,
            float top,
            float right,
            float bottom)
        {
            if (!RectanglesOverlap(maskLeft, maskTop, maskRight, maskBottom, left, top, right, bottom))
            {
                return;
            }

            var distance = GetStabHorizontalBlockerDistance(mask.X, maskLeft, maskRight, facingDirectionX, left, right);
            if (!distance.HasValue)
            {
                return;
            }

            var hitX = mask.X + facingDirectionX * distance.Value;
            var hitY = Math.Clamp(mask.Y, MathF.Max(maskTop, top), MathF.Min(maskBottom, bottom));
            UpdateNearestStabHit(ref nearestHit, distance.Value, hitX, hitY, null);
        }

        private void UpdateNearestStabHitFromPlayers(
            ref ShotHitResult? nearestHit,
            StabMaskEntity mask,
            float maskLeft,
            float maskTop,
            float maskRight,
            float maskBottom,
            float facingDirectionX)
        {
            foreach (var player in EnumerateSimulatedPlayers())
            {
                if (!_world.CanTeamDamagePlayer(mask.Team, mask.OwnerId, player) || player.Id == mask.OwnerId) { continue; }
                GetPlayerPresentationHitBounds(_world, player, out var left, out var top, out var right, out var bottom);
                var distance = GetStabTargetDistance(mask.X, maskLeft, maskTop, maskRight, maskBottom, facingDirectionX, left, top, right, bottom);
                if (distance.HasValue)
                {
                    var hitX = GetStabTargetHitX(mask.X, facingDirectionX, left, right);
                    var hitY = GetStabTargetHitY(mask.Y, maskTop, maskBottom, top, bottom);
                    UpdateNearestStabHit(ref nearestHit, distance.Value, hitX, hitY, player);
                }
            }
        }

        private void UpdateNearestStabHitFromSentries(
            ref ShotHitResult? nearestHit,
            StabMaskEntity mask,
            float maskLeft,
            float maskTop,
            float maskRight,
            float maskBottom,
            float facingDirectionX)
        {
            foreach (var sentry in _sentries)
            {
                if (sentry.Team == mask.Team) { continue; }
                var left = sentry.X - (SentryEntity.Width / 2f);
                var top = sentry.Y - (SentryEntity.Height / 2f);
                var right = sentry.X + (SentryEntity.Width / 2f);
                var bottom = sentry.Y + (SentryEntity.Height / 2f);
                var distance = GetStabTargetDistance(mask.X, maskLeft, maskTop, maskRight, maskBottom, facingDirectionX, left, top, right, bottom);
                if (distance.HasValue)
                {
                    var hitX = GetStabTargetHitX(mask.X, facingDirectionX, left, right);
                    var hitY = GetStabTargetHitY(mask.Y, maskTop, maskBottom, top, bottom);
                    UpdateNearestStabHit(ref nearestHit, distance.Value, hitX, hitY, null, sentry);
                }
            }
        }

        private static float? GetStabTargetDistance(
            float originX,
            float maskLeft,
            float maskTop,
            float maskRight,
            float maskBottom,
            float facingDirectionX,
            float left,
            float top,
            float right,
            float bottom)
        {
            if (!RectanglesOverlap(maskLeft, maskTop, maskRight, maskBottom, left, top, right, bottom))
            {
                return null;
            }

            return facingDirectionX >= 0f
                ? MathF.Max(0f, left - originX)
                : MathF.Max(0f, originX - right);
        }

        private static float? GetStabHorizontalBlockerDistance(
            float originX,
            float maskLeft,
            float maskRight,
            float facingDirectionX,
            float left,
            float right)
        {
            if (facingDirectionX >= 0f)
            {
                if (right <= originX || left >= maskRight)
                {
                    return null;
                }

                return left <= originX ? 0f : left - originX;
            }

            if (left >= originX || right <= maskLeft)
            {
                return null;
            }

            return right >= originX ? 0f : originX - right;
        }

        private static float GetStabTargetHitX(float originX, float facingDirectionX, float left, float right)
        {
            return facingDirectionX >= 0f
                ? Math.Clamp(MathF.Max(originX, left), left, right)
                : Math.Clamp(MathF.Min(originX, right), left, right);
        }

        private static float GetStabTargetHitY(float originY, float maskTop, float maskBottom, float top, float bottom)
        {
            return Math.Clamp(originY, MathF.Max(maskTop, top), MathF.Min(maskBottom, bottom));
        }

        private static bool RectanglesOverlap(
            float left,
            float top,
            float right,
            float bottom,
            float otherLeft,
            float otherTop,
            float otherRight,
            float otherBottom)
        {
            return left < otherRight
                && right > otherLeft
                && top < otherBottom
                && bottom > otherTop;
        }

        private static void UpdateNearestStabHit(
            ref ShotHitResult? nearestHit,
            float distance,
            float hitX,
            float hitY,
            PlayerEntity? player,
            SentryEntity? sentry = null)
        {
            if (nearestHit.HasValue && nearestHit.Value.Distance <= distance) { return; }
            nearestHit = new ShotHitResult(distance, hitX, hitY, player, sentry, null);
        }
    }
}
