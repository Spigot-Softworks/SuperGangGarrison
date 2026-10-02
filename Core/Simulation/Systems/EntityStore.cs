namespace OpenGarrison.Core;

/// <summary>Stores the live simulation entities by their stable entity IDs.</summary>
public sealed class EntityStore
{
    private readonly Dictionary<int, SimulationEntity> _entities = new();
    private long _nextLocalEffectId = int.MinValue;

    /// <summary>Gets the next entity ID that <see cref="AllocateId"/> will hand out.</summary>
    internal int NextEntityId { get; private set; } = 1;

    /// <summary>Reserves and returns the next unused entity ID.</summary>
    internal int AllocateId()
    {
        return NextEntityId++;
    }

    /// <summary>Reserves a negative ID for a client-local visual effect.</summary>
    internal int AllocateLocalEffectId()
    {
        while (_nextLocalEffectId < -1)
        {
            var id = (int)_nextLocalEffectId;
            _nextLocalEffectId += 1;
            if (!_entities.ContainsKey(id))
            {
                return id;
            }
        }

        throw new InvalidOperationException("The client-local entity ID range is exhausted.");
    }

    /// <summary>Ensures future allocations never reuse the specified, already-known entity ID.</summary>
    internal void ReserveThrough(int entityId)
    {
        if (entityId >= NextEntityId)
        {
            NextEntityId = entityId + 1;
        }
    }

    /// <summary>Gets the entity with the specified ID, or <see langword="null"/> when absent.</summary>
    public SimulationEntity? Get(int id)
    {
        return _entities.TryGetValue(id, out var entity) ? entity : null;
    }

    /// <summary>Adds an entity and throws when another entity has the same ID.</summary>
    public void Add(SimulationEntity entity)
    {
        _entities.Add(entity.Id, entity);
    }

    /// <summary>Sets the entity for an ID, adding it when the ID is not already present.</summary>
    public void Set(int id, SimulationEntity entity)
    {
        _entities[id] = entity;
    }

    /// <summary>Removes the entity with the specified ID and returns whether it was present.</summary>
    public bool Remove(int id)
    {
        return _entities.Remove(id);
    }

    /// <summary>Removes the ID only when it still maps to the specified entity instance.</summary>
    internal bool RemoveIfSame(SimulationEntity entity)
    {
        return _entities.TryGetValue(entity.Id, out var current)
            && ReferenceEquals(current, entity)
            && _entities.Remove(entity.Id);
    }

    /// <summary>Returns whether an entity with the specified ID is present.</summary>
    public bool Contains(int id)
    {
        return _entities.ContainsKey(id);
    }

    /// <summary>Enumerates all live entities in dictionary enumeration order.</summary>
    public IEnumerable<SimulationEntity> All()
    {
        foreach (var entity in _entities.Values)
        {
            yield return entity;
        }
    }

    /// <summary>Exposes the live entity dictionary through a read-only interface.</summary>
    public IReadOnlyDictionary<int, SimulationEntity> AsReadOnly()
    {
        return _entities;
    }
}
