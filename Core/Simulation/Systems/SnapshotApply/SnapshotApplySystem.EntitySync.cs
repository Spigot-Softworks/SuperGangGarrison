using System;
using OpenGarrison.Core.LastToDie;
using OpenGarrison.GameplayModding;
using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

internal sealed partial class SnapshotApplySystem
{
    private static bool IsSnapshotEntityCollectionComplete(
        SnapshotMessage snapshot,
        SnapshotEntityCollectionCompletenessFlags flag)
    {
        return (snapshot.EntityCollectionCompletenessFlags & flag) != 0;
    }

    private void SyncSnapshotEntities<TState, TEntity>(
        IReadOnlyList<TState> snapshotStates,
        List<TEntity> target,
        Func<TState, int> idSelector,
        Func<TEntity, TState, bool> canReuse,
        Func<TState, TEntity> factory,
        Action<TEntity, TState> applyState)
        where TEntity : SimulationEntity
    {
        SyncSnapshotEntities(
            snapshotStates,
            Array.Empty<int>(),
            collectionIsComplete: true,
            target,
            idSelector,
            canReuse,
            factory,
            applyState,
            suppressProjectileRespawnOnRemoval: false,
            (entity, state, isNew) => applyState(entity, state));
    }

    private void SyncSnapshotEntities<TState, TEntity>(
        IReadOnlyList<TState> snapshotStates,
        List<TEntity> target,
        Func<TState, int> idSelector,
        Func<TEntity, TState, bool> canReuse,
        Func<TState, TEntity> factory,
        Action<TEntity, TState> applyState,
        Action<TEntity, TState, bool> applyStateForNewEntity)
        where TEntity : SimulationEntity
    {
        SyncSnapshotEntities(
            snapshotStates,
            target,
            idSelector,
            canReuse,
            factory,
            applyState,
            suppressProjectileRespawnOnRemoval: false,
            applyStateForNewEntity);
    }

    private void SyncSnapshotEntities<TState, TEntity>(
        IReadOnlyList<TState> snapshotStates,
        List<TEntity> target,
        Func<TState, int> idSelector,
        Func<TEntity, TState, bool> canReuse,
        Func<TState, TEntity> factory,
        Action<TEntity, TState> applyState,
        bool suppressProjectileRespawnOnRemoval,
        Action<TEntity, TState, bool> applyStateForNewEntity)
        where TEntity : SimulationEntity
    {
        SyncSnapshotEntities(
            snapshotStates,
            Array.Empty<int>(),
            collectionIsComplete: true,
            target,
            idSelector,
            canReuse,
            factory,
            applyState,
            suppressProjectileRespawnOnRemoval,
            applyStateForNewEntity);
    }

    private void SyncSnapshotEntities<TState, TEntity>(
        IReadOnlyList<TState> snapshotStates,
        IReadOnlyList<int> removedEntityIds,
        bool collectionIsComplete,
        List<TEntity> target,
        Func<TState, int> idSelector,
        Func<TEntity, TState, bool> canReuse,
        Func<TState, TEntity> factory,
        Action<TEntity, TState> applyState,
        Action<TEntity, TState, bool> applyStateForNewEntity)
        where TEntity : SimulationEntity
    {
        SyncSnapshotEntities(
            snapshotStates,
            removedEntityIds,
            collectionIsComplete,
            target,
            idSelector,
            canReuse,
            factory,
            applyState,
            suppressProjectileRespawnOnRemoval: false,
            applyStateForNewEntity);
    }

    private void SyncSnapshotEntities<TState, TEntity>(
        IReadOnlyList<TState> snapshotStates,
        IReadOnlyList<int> removedEntityIds,
        bool collectionIsComplete,
        List<TEntity> target,
        Func<TState, int> idSelector,
        Func<TEntity, TState, bool> canReuse,
        Func<TState, TEntity> factory,
        Action<TEntity, TState> applyState,
        bool suppressProjectileRespawnOnRemoval,
        Action<TEntity, TState, bool> applyStateForNewEntity)
        where TEntity : SimulationEntity
    {
        _host.ClientSnapshots.SeenEntityIds.Clear();
        for (var index = 0; index < snapshotStates.Count; index += 1)
        {
            _host.ClientSnapshots.SeenEntityIds.Add(idSelector(snapshotStates[index]));
        }

        _host.ClientSnapshots.StaleEntityIds.Clear();
        List<TEntity>? retainedEntities = null;
        for (var index = 0; index < target.Count; index += 1)
        {
            var entityId = target[index].Id;
            var explicitlyRemoved = ContainsEntityId(removedEntityIds, entityId);
            if (explicitlyRemoved || (collectionIsComplete && !_host.ClientSnapshots.SeenEntityIds.Contains(entityId)))
            {
                _host.ClientSnapshots.StaleEntityIds.Add(entityId);
                continue;
            }

            if (!_host.ClientSnapshots.SeenEntityIds.Contains(entityId))
            {
                retainedEntities ??= new List<TEntity>();
                retainedEntities.Add(target[index]);
            }
        }

        target.Clear();
        if (retainedEntities is not null)
        {
            target.AddRange(retainedEntities);
        }

        for (var index = 0; index < snapshotStates.Count; index += 1)
        {
            var state = snapshotStates[index];
            var entityId = idSelector(state);
            ReserveEntityId(entityId);

            TEntity entity;
            var isNewEntity = false;
            var existingEntity = _host.EntityStore.Get(entityId);
            if (existingEntity is not null
                && existingEntity is TEntity typedEntity
                && canReuse(typedEntity, state))
            {
                entity = typedEntity;
            }
            else
            {
                if (existingEntity is not null)
                {
                    _host.EntityStore.Remove(entityId);
                }

                entity = factory(state);
                isNewEntity = true;
            }

            if (isNewEntity)
            {
                applyStateForNewEntity(entity, state, true);
            }
            else
            {
                applyStateForNewEntity(entity, state, false);
            }

            target.Add(entity);
            _host.EntityStore.Set(entityId, entity);
        }

        for (var index = 0; index < _host.ClientSnapshots.StaleEntityIds.Count; index += 1)
        {
            var staleId = _host.ClientSnapshots.StaleEntityIds[index];
            _host.EntityStore.Remove(staleId);
            if (suppressProjectileRespawnOnRemoval)
            {
                SuppressProjectileRespawn(staleId, SimulationConstants.NetworkProjectileRemovalSuppressionTicks);
                _host.ClientSnapshots.PredictedProjectileIds.Remove(staleId);
            }
            else if (_host.ClientSnapshots.PredictedProjectileIds.Remove(staleId))
            {
                SuppressProjectileRespawn(staleId, SimulationConstants.NetworkProjectileRemovalSuppressionTicks);
            }
        }
    }

    private void SyncSnapshotEntities<TState, TEntity>(
        IReadOnlyList<TState> snapshotStates,
        IReadOnlyList<int> removedEntityIds,
        bool collectionIsComplete,
        IReadOnlyList<TEntity> target,
        Func<TState, int> idSelector,
        Func<TEntity, TState, bool> canReuse,
        Func<TState, TEntity> factory,
        Action<TEntity, TState> applyState,
        bool suppressProjectileRespawnOnRemoval,
        Action<TEntity, TState, bool> applyStateForNewEntity,
        Action clearTarget,
        Action<TEntity> addTarget)
        where TEntity : SimulationEntity
    {
        _host.ClientSnapshots.SeenEntityIds.Clear();
        for (var index = 0; index < snapshotStates.Count; index += 1)
        {
            _host.ClientSnapshots.SeenEntityIds.Add(idSelector(snapshotStates[index]));
        }

        _host.ClientSnapshots.StaleEntityIds.Clear();
        List<TEntity>? retainedEntities = null;
        for (var index = 0; index < target.Count; index += 1)
        {
            var entityId = target[index].Id;
            var explicitlyRemoved = ContainsEntityId(removedEntityIds, entityId);
            if (explicitlyRemoved || (collectionIsComplete && !_host.ClientSnapshots.SeenEntityIds.Contains(entityId)))
            {
                _host.ClientSnapshots.StaleEntityIds.Add(entityId);
                continue;
            }

            if (!_host.ClientSnapshots.SeenEntityIds.Contains(entityId))
            {
                retainedEntities ??= new List<TEntity>();
                retainedEntities.Add(target[index]);
            }
        }

        clearTarget();
        if (retainedEntities is not null)
        {
            for (var index = 0; index < retainedEntities.Count; index += 1)
            {
                addTarget(retainedEntities[index]);
            }
        }

        for (var index = 0; index < snapshotStates.Count; index += 1)
        {
            var state = snapshotStates[index];
            var entityId = idSelector(state);
            ReserveEntityId(entityId);

            TEntity entity;
            var isNewEntity = false;
            var existingEntity = _host.EntityStore.Get(entityId);
            if (existingEntity is not null
                && existingEntity is TEntity typedEntity
                && canReuse(typedEntity, state))
            {
                entity = typedEntity;
            }
            else
            {
                if (existingEntity is not null)
                {
                    _host.EntityStore.Remove(entityId);
                }

                entity = factory(state);
                isNewEntity = true;
            }

            if (isNewEntity)
            {
                applyStateForNewEntity(entity, state, true);
            }
            else
            {
                applyStateForNewEntity(entity, state, false);
            }

            addTarget(entity);
            _host.EntityStore.Set(entityId, entity);
        }

        for (var index = 0; index < _host.ClientSnapshots.StaleEntityIds.Count; index += 1)
        {
            var staleId = _host.ClientSnapshots.StaleEntityIds[index];
            _host.EntityStore.Remove(staleId);
            if (suppressProjectileRespawnOnRemoval)
            {
                SuppressProjectileRespawn(staleId, SimulationConstants.NetworkProjectileRemovalSuppressionTicks);
                _host.ClientSnapshots.PredictedProjectileIds.Remove(staleId);
            }
            else if (_host.ClientSnapshots.PredictedProjectileIds.Remove(staleId))
            {
                SuppressProjectileRespawn(staleId, SimulationConstants.NetworkProjectileRemovalSuppressionTicks);
            }
        }
    }

    private static bool ContainsEntityId(IReadOnlyList<int> ids, int entityId)
    {
        for (var index = 0; index < ids.Count; index += 1)
        {
            if (ids[index] == entityId)
            {
                return true;
            }
        }

        return false;
    }

    private void ReserveEntityId(int entityId)
    {
        _host.EntityStore.ReserveThrough(entityId);
    }
}
