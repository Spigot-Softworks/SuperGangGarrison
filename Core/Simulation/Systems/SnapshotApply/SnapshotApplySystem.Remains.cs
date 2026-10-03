using OpenGarrison.GameplayModding;
using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

internal sealed partial class SnapshotApplySystem
{
    private void ApplySnapshotDeadBodies(IReadOnlyList<SnapshotDeadBodyState> deadBodies)
    {
        SyncSnapshotEntities(
            deadBodies,
            _host.WorldObjects.DeadBodies,
            static state => state.Id,
            static (entity, state) =>
                entity.SourcePlayerId == state.SourcePlayerId
                && entity.ClassId == (PlayerClass)state.ClassId
                && entity.Team == (PlayerTeam)state.Team
                && entity.AnimationKind == (DeadBodyAnimationKind)state.AnimationKind
                && entity.Width == state.Width
                && entity.Height == state.Height
                && entity.FacingLeft == state.FacingLeft
                && entity.DiedToFire == state.DiedToFire
                && string.Equals(entity.GameplayClassId, state.GameplayClassId, StringComparison.Ordinal),
            state => new DeadBodyEntity(
                state.Id,
                state.SourcePlayerId,
                (PlayerClass)state.ClassId,
                (PlayerTeam)state.Team,
                (DeadBodyAnimationKind)state.AnimationKind,
                state.X,
                state.Y,
                state.Width,
                state.Height,
                state.HorizontalSpeed,
                state.VerticalSpeed,
                state.FacingLeft,
                state.GameplayClassId,
                state.DiedToFire),
            static (entity, state) => entity.ApplyNetworkState(
                state.X,
                state.Y,
                state.HorizontalSpeed,
                state.VerticalSpeed,
                state.TicksRemaining));
    }

    private void ApplySnapshotSentryGibs(IReadOnlyList<SnapshotSentryGibState> sentryGibs)
    {
        SyncSnapshotEntities(
            sentryGibs,
            _host.WorldObjects.SentryGibs,
            static state => state.Id,
            static (entity, state) =>
                entity.Team == (PlayerTeam)state.Team
                    && entity.IsDispenser == state.IsDispenser,
            state => new SentryGibEntity(
                state.Id,
                (PlayerTeam)state.Team,
                state.X,
                state.Y,
                state.IsDispenser),
            static (entity, state) => entity.ApplyNetworkState(
                state.X,
                state.Y,
                state.TicksRemaining),
            static (entity, state, isNewEntity) =>
            {
                if (isNewEntity)
                {
                    entity.ApplyNetworkState(
                        state.X,
                        state.Y,
                        state.TicksRemaining);
                }
            });
    }

    private void ApplySnapshotJumpPadGibs(IReadOnlyList<SnapshotJumpPadGibState> jumpPadGibs)
    {
        SyncSnapshotEntities(
            jumpPadGibs,
            _host.WorldObjects.JumpPadGibs,
            static state => state.Id,
            static (entity, state) =>
                entity.Team == (PlayerTeam)state.Team,
            state => new JumpPadGibEntity(
                state.Id,
                (PlayerTeam)state.Team,
                state.X,
                state.Y),
            static (entity, state) => entity.ApplyNetworkState(
                state.X,
                state.Y,
                state.TicksRemaining),
            static (entity, state, isNewEntity) =>
            {
                if (isNewEntity)
                {
                    entity.ApplyNetworkState(
                        state.X,
                        state.Y,
                        state.TicksRemaining);
                }
            });
    }

    private void ApplySnapshotGibSpawnEvents(IReadOnlyList<SnapshotGibSpawnEvent> gibSpawnEvents)
    {
        for (var index = 0; index < gibSpawnEvents.Count; index += 1)
        {
            var e = gibSpawnEvents[index];
            if (e.EventId != 0 && !_host.ClientSnapshots.ProcessedGibSpawnEventIds.Add(e.EventId))
            {
                continue;
            }

            var gib = new PlayerGibEntity(
                _host.EntityStore.AllocateLocalEffectId(),
                e.SpriteName,
                e.FrameIndex,
                e.X,
                e.Y,
                e.VelocityX,
                e.VelocityY,
                e.RotationSpeedDegrees,
                e.HorizontalFriction,
                e.RotationFriction,
                e.LifetimeTicks,
                e.BloodChance);
            _host.WorldObjects.PlayerGibs.Add(gib);
            _host.EntityStore.Add(gib);
        }
    }
}
