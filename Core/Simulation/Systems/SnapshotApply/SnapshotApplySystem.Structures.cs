using OpenGarrison.GameplayModding;
using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

internal sealed partial class SnapshotApplySystem
{
    private void ApplySnapshotHealthPacks(
        IReadOnlyList<SnapshotHealthPackState> healthPacks,
        IReadOnlyList<int> removedHealthPackIds)
    {
        _host.ClientSnapshots.SeenEntityIds.Clear();
        for (var index = 0; index < healthPacks.Count; index += 1)
        {
            _host.ClientSnapshots.SeenEntityIds.Add(healthPacks[index].Id);
        }

        for (var index = _host.WorldObjects.HealthPacks.Count - 1; index >= 0; index -= 1)
        {
            var healthPack = _host.WorldObjects.HealthPacks[index];
            var snapshotId = healthPack.NetworkSnapshotId;
            if (ContainsEntityId(removedHealthPackIds, snapshotId)
                || !_host.ClientSnapshots.SeenEntityIds.Contains(snapshotId)
                || SnapshotMarksHealthPackInactive(healthPacks, snapshotId))
            {
                _host.EntityStore.Remove(healthPack.Id);
                _host.WorldObjects.HealthPacks.RemoveAt(index);
            }
        }

        for (var index = 0; index < healthPacks.Count; index += 1)
        {
            var state = healthPacks[index];
            if (!state.Active)
            {
                continue;
            }

            ReserveEntityId(state.Id);
            var healthPack = FindHealthPackBySnapshotId(state.Id);
            if (healthPack is null
                || healthPack.Id != state.Id
                || healthPack.Size != (HealthPackSize)state.Size
                || healthPack.SourceSpawnIndex != state.SourceSpawnIndex)
            {
                if (healthPack is not null)
                {
                    _host.EntityStore.Remove(healthPack.Id);
                    _host.WorldObjects.HealthPacks.Remove(healthPack);
                }

                healthPack = new HealthPackEntity(
                    state.Id,
                    state.X,
                    state.Y,
                    (HealthPackSize)state.Size,
                    state.VelocityX,
                    state.VelocityY,
                    state.SourceSpawnIndex);
                _host.WorldObjects.HealthPacks.Add(healthPack);
                _host.EntityStore.Set(healthPack.Id, healthPack);
            }

            healthPack.ApplyNetworkState(
                state.X,
                state.Y,
                state.VelocityX,
                state.VelocityY,
                state.TicksRemaining);
        }
    }

    private HealthPackEntity? FindHealthPackBySnapshotId(int snapshotId)
    {
        for (var index = 0; index < _host.WorldObjects.HealthPacks.Count; index += 1)
        {
            if (_host.WorldObjects.HealthPacks[index].NetworkSnapshotId == snapshotId)
            {
                return _host.WorldObjects.HealthPacks[index];
            }
        }

        return null;
    }

    private static bool SnapshotMarksHealthPackInactive(
        IReadOnlyList<SnapshotHealthPackState> healthPacks,
        int snapshotId)
    {
        for (var index = 0; index < healthPacks.Count; index += 1)
        {
            if (healthPacks[index].Id == snapshotId)
            {
                return !healthPacks[index].Active;
            }
        }

        return false;
    }

    private void ApplySnapshotCivilDefenseTurrets(IReadOnlyList<SnapshotCivilDefenseTurretState> turrets)
    {
        SyncSnapshotEntities(turrets, _host.WorldObjects.CivilDefenseTurrets, static state => state.Id,
            static (entity, state) => entity.OwnerPlayerId == state.OwnerPlayerId && entity.Team == (PlayerTeam)state.Team,
            state => new CivilDefenseTurretEntity(
                state.Id,
                state.OwnerPlayerId,
                (PlayerTeam)state.Team,
                state.X,
                state.Y,
                state.FacingDirectionX,
                state.LifetimeTicksRemaining),
            static (entity, state) => entity.ApplyNetworkState(state.X, state.Y, state.Health, state.HasLanded, state.IsBuilt,
                state.FacingDirectionX, state.AimDirectionDegrees, state.ReloadTicksRemaining,
                state.ShotTraceTicksRemaining, state.LastShotTargetX, state.LastShotTargetY,
                state.LifetimeTicksRemaining));
    }

    private void ApplySnapshotJumpPads(IReadOnlyList<SnapshotJumpPadState> jumpPads)
    {
        SyncSnapshotEntities(
            jumpPads,
            _host.WorldObjects.JumpPads,
            static state => state.Id,
            static (entity, state) => entity.OwnerPlayerId == state.OwnerPlayerId
                && entity.Team == (PlayerTeam)state.Team,
            state => new JumpPadEntity(
                state.Id,
                state.OwnerPlayerId,
                (PlayerTeam)state.Team,
                state.X,
                state.Y),
            static (entity, state) => entity.ApplyNetworkState(
                state.X,
                state.Y,
                state.Health,
                state.HasLanded,
                state.IsBuilt));
    }

    private void ApplySnapshotSentries(IReadOnlyList<SnapshotSentryState> sentries)
    {
        SyncSnapshotEntities(
            sentries,
            _host.WorldObjects.Sentries,
            static state => state.Id,
            static (entity, state) => entity.OwnerPlayerId == state.OwnerPlayerId
                && entity.Team == (PlayerTeam)state.Team
                && entity.IsDispenser == state.IsDispenser,
            state => new SentryEntity(
                state.Id,
                state.OwnerPlayerId,
                (PlayerTeam)state.Team,
                state.X,
                state.Y,
                state.FacingDirectionX,
                state.IsDispenser ? SentryEntity.DispenserMaxHealth : SentryEntity.DefaultMaxHealth,
                state.IsDispenser),
            static (entity, state) => entity.ApplyNetworkState(
                state.X,
                state.Y,
                state.Health,
                state.IsBuilt,
                state.FacingDirectionX,
                state.AimDirectionDegrees,
                state.ShotTraceTicksRemaining,
                state.HasLanded,
                state.HasActiveTarget,
                state.LastShotTargetX,
                state.LastShotTargetY,
                state.DispenserRampTicks));
    }

    private void ApplySnapshotSentryUpdates(IReadOnlyList<SnapshotSentryUpdateState> updates)
    {
        if (updates.Count == 0)
        {
            return;
        }

        // Apply lightweight updates to existing sentries
        for (var index = 0; index < updates.Count; index += 1)
        {
            var update = updates[index];
            var sentry = _host.WorldObjects.Sentries.FirstOrDefault(s => s.Id == update.Id);
            if (sentry is not null)
            {
                // Apply only the dynamic fields
                sentry.ApplyNetworkState(
                    update.X,
                    update.Y,
                    update.Health,
                    sentry.IsBuilt, // Keep existing static fields
                    update.FacingDirectionX,
                    update.AimDirectionDegrees,
                    update.ShotTraceTicksRemaining,
                    sentry.HasLanded, // Keep existing static fields
                    update.HasActiveTarget,
                    update.LastShotTargetX,
                    update.LastShotTargetY,
                    update.DispenserRampTicks);
            }
        }
    }
}
