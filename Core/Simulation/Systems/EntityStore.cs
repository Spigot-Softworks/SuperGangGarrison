namespace OpenGarrison.Core;

/// <summary>Stores the live simulation entities by their stable entity IDs.</summary>
public sealed class EntityStore
{
    private readonly Dictionary<int, SimulationEntity> _entities = new();

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
