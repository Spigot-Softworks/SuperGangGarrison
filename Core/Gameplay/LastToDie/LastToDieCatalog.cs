using System.Collections.ObjectModel;

namespace OpenGarrison.Core.LastToDie;

public sealed class LastToDieSurvivorCatalog
{
    public static readonly LastToDieSurvivorId SoldierId = new("ltd.survivor.soldier");
    public static readonly LastToDieSurvivorId DemoknightId = new("ltd.survivor.demoknight");
    public static readonly LastToDieSurvivorId EngineerId = new("ltd.survivor.engineer");
    public static readonly LastToDieSurvivorId SpyId = new("ltd.survivor.spy");
    public static readonly LastToDieSurvivorId MedicId = new("ltd.survivor.medic");
    public static readonly LastToDieSurvivorId SniperId = new("ltd.survivor.sniper");

    private readonly IReadOnlyDictionary<LastToDieSurvivorId, LastToDieSurvivorDefinition> _definitions;
    private readonly IReadOnlyList<LastToDieSurvivorDefinition> _orderedDefinitions;

    public LastToDieSurvivorCatalog(IEnumerable<LastToDieSurvivorDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var result = new Dictionary<LastToDieSurvivorId, LastToDieSurvivorDefinition>();
        foreach (var definition in definitions)
        {
            ArgumentNullException.ThrowIfNull(definition);
            ValidateCanonicalId(definition.Id.Value, "survivor");
            if (string.IsNullOrWhiteSpace(definition.GameplayClassId))
            {
                throw new InvalidOperationException($"Survivor {definition.Id} has no gameplay class ID.");
            }

            if (string.IsNullOrWhiteSpace(definition.DisplayName))
            {
                throw new InvalidOperationException($"Survivor {definition.Id} has no display name.");
            }

            if (!result.TryAdd(definition.Id, definition))
            {
                throw new InvalidOperationException($"Duplicate Last to Die survivor ID {definition.Id}.");
            }
        }

        if (result.Count == 0)
        {
            throw new InvalidOperationException("Last to Die survivor catalog cannot be empty.");
        }

        _definitions = new ReadOnlyDictionary<LastToDieSurvivorId, LastToDieSurvivorDefinition>(result);
        _orderedDefinitions = Array.AsReadOnly(
            result.Values.OrderBy(definition => definition.Id.Value, StringComparer.Ordinal).ToArray());
    }

    public IReadOnlyList<LastToDieSurvivorDefinition> Definitions => _orderedDefinitions;

    public bool Contains(LastToDieSurvivorId survivorId) => _definitions.ContainsKey(survivorId);

    public LastToDieSurvivorDefinition GetRequired(LastToDieSurvivorId survivorId)
        => _definitions.TryGetValue(survivorId, out var definition)
            ? definition
            : throw new KeyNotFoundException($"Unknown Last to Die survivor ID {survivorId}.");

    public static LastToDieSurvivorCatalog CreateStock()
        => new(
        [
            new(SoldierId, "soldier", "Soldier"),
            new(DemoknightId, "demoman", "Demoknight"),
            new(EngineerId, "engineer", "Engineer"),
            new(SpyId, "spy", "Spy"),
            new(MedicId, "medic", "Medic"),
            new(SniperId, "sniper", "Sniper"),
        ]);

    internal static void ValidateCanonicalId(string? value, string kind)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !value.StartsWith($"ltd.{kind}.", StringComparison.Ordinal)
            || !string.Equals(value, value.ToLowerInvariant(), StringComparison.Ordinal)
            || value.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_')))
        {
            throw new InvalidOperationException($"Last to Die {kind} ID '{value}' is not canonical.");
        }
    }
}

public sealed class LastToDiePerkCatalog
{
    private readonly LastToDieSurvivorCatalog _survivors;
    private readonly IReadOnlyDictionary<LastToDiePerkId, LastToDiePerkDefinition> _definitions;
    private readonly IReadOnlyList<LastToDiePerkDefinition> _orderedDefinitions;

    public LastToDiePerkCatalog(
        LastToDieSurvivorCatalog survivors,
        IEnumerable<LastToDiePerkDefinition> definitions)
    {
        _survivors = survivors ?? throw new ArgumentNullException(nameof(survivors));
        ArgumentNullException.ThrowIfNull(definitions);

        var result = new Dictionary<LastToDiePerkId, LastToDiePerkDefinition>();
        foreach (var definition in definitions)
        {
            ArgumentNullException.ThrowIfNull(definition);
            LastToDieSurvivorCatalog.ValidateCanonicalId(definition.Id.Value, "perk");
            if (!_survivors.Contains(definition.SurvivorId))
            {
                throw new InvalidOperationException($"Perk {definition.Id} references unknown survivor {definition.SurvivorId}.");
            }

            if (string.IsNullOrWhiteSpace(definition.DisplayName) || definition.Rank <= 0)
            {
                throw new InvalidOperationException($"Perk {definition.Id} has invalid presentation or rank metadata.");
            }

            if (!result.TryAdd(definition.Id, definition))
            {
                throw new InvalidOperationException($"Duplicate Last to Die perk ID {definition.Id}.");
            }
        }

        _definitions = new ReadOnlyDictionary<LastToDiePerkId, LastToDiePerkDefinition>(result);
        _orderedDefinitions = Array.AsReadOnly(
            result.Values.OrderBy(definition => definition.Id.Value, StringComparer.Ordinal).ToArray());
        ValidateReferences();
        ValidateAcyclicPrerequisites();
    }

    public IReadOnlyList<LastToDiePerkDefinition> Definitions => _orderedDefinitions;

    public LastToDiePerkDefinition GetRequired(LastToDiePerkId perkId)
        => _definitions.TryGetValue(perkId, out var definition)
            ? definition
            : throw new KeyNotFoundException($"Unknown Last to Die perk ID {perkId}.");

    public IReadOnlyList<LastToDiePerkDefinition> GetEligible(
        LastToDieSurvivorId survivorId,
        IReadOnlySet<LastToDiePerkId> ownedPerks)
    {
        ArgumentNullException.ThrowIfNull(ownedPerks);
        _ = _survivors.GetRequired(survivorId);
        return _definitions.Values
            .Where(definition =>
                definition.SurvivorId == survivorId
                && !ownedPerks.Contains(definition.Id)
                && definition.Requires.All(ownedPerks.Contains)
                && definition.Excludes.All(excluded => !ownedPerks.Contains(excluded)))
            .OrderBy(definition => definition.Id.Value, StringComparer.Ordinal)
            .ToArray();
    }

    private void ValidateReferences()
    {
        foreach (var definition in _definitions.Values)
        {
            foreach (var requiredId in definition.Requires)
            {
                var required = GetReferencedDefinition(definition, requiredId, "requires");
                if (required.SurvivorId != definition.SurvivorId)
                {
                    throw new InvalidOperationException($"Perk {definition.Id} cannot require a perk from another survivor.");
                }
            }

            foreach (var excludedId in definition.Excludes)
            {
                var excluded = GetReferencedDefinition(definition, excludedId, "excludes");
                if (excluded.SurvivorId != definition.SurvivorId)
                {
                    throw new InvalidOperationException($"Perk {definition.Id} cannot exclude a perk from another survivor.");
                }

                if (!excluded.Excludes.Contains(definition.Id))
                {
                    throw new InvalidOperationException($"Perk exclusion {definition.Id} <-> {excluded.Id} must be symmetric.");
                }
            }
        }
    }

    private LastToDiePerkDefinition GetReferencedDefinition(
        LastToDiePerkDefinition owner,
        LastToDiePerkId referencedId,
        string relationship)
    {
        if (referencedId == owner.Id)
        {
            throw new InvalidOperationException($"Perk {owner.Id} cannot {relationship} itself.");
        }

        if (!_definitions.TryGetValue(referencedId, out var referenced))
        {
            throw new InvalidOperationException($"Perk {owner.Id} {relationship} unknown perk {referencedId}.");
        }

        return referenced;
    }

    private void ValidateAcyclicPrerequisites()
    {
        var visitStates = new Dictionary<LastToDiePerkId, byte>();
        foreach (var definition in _definitions.Values)
        {
            Visit(definition, visitStates);
        }
    }

    private void Visit(
        LastToDiePerkDefinition definition,
        IDictionary<LastToDiePerkId, byte> visitStates)
    {
        if (visitStates.TryGetValue(definition.Id, out var state))
        {
            if (state == 1)
            {
                throw new InvalidOperationException($"Last to Die perk prerequisite cycle includes {definition.Id}.");
            }

            if (state == 2)
            {
                return;
            }
        }

        visitStates[definition.Id] = 1;
        foreach (var requiredId in definition.Requires)
        {
            Visit(_definitions[requiredId], visitStates);
        }

        visitStates[definition.Id] = 2;
    }
}
