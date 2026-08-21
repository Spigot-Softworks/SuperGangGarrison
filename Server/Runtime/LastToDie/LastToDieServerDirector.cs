using OpenGarrison.Core;
using OpenGarrison.Core.LastToDie;

namespace OpenGarrison.Server.LastToDie;

/// <summary>
/// Server ownership boundary for the transport-agnostic Last to Die director.
/// GameServer integration will feed objective/team state into this adapter and
/// publish its immutable snapshots; the client never constructs this type.
/// </summary>
internal sealed class LastToDieServerDirector
{
    private LastToDieServerDirector(LastToDieDirector director)
    {
        Director = director;
    }

    public GameplayVariantKind Variant => GameplayVariantKind.LastToDie;

    public LastToDieDirector Director { get; }

    public static LastToDieServerDirector CreateFirstSlice(
        IEnumerable<string> stockMapRotation,
        LastToDieDifficulty difficulty,
        ulong seed,
        int ticksPerSecond = 30,
        Guid? runId = null,
        int maximumPlayers = 2)
    {
        ArgumentNullException.ThrowIfNull(stockMapRotation);
        var requestedMaps = stockMapRotation
            .Where(map => !string.IsNullOrWhiteSpace(map))
            .Select(map => map.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (requestedMaps.Length == 0)
        {
            throw new InvalidOperationException("The Last to Die server rotation cannot be empty.");
        }

        var maps = new List<string>(requestedMaps.Length);
        foreach (var map in requestedMaps)
        {
            if (!OpenGarrisonStockMapCatalog.TryGetDefinition(map, out var definition))
            {
                throw new InvalidOperationException(
                    $"First-slice Last to Die hosting is restricted to stock maps; '{map}' is not stock.");
            }

            if (definition.Mode is GameModeKind.KingOfTheHill or GameModeKind.CaptureTheFlag
                && !maps.Contains(definition.LevelName, StringComparer.OrdinalIgnoreCase))
            {
                maps.Add(definition.LevelName);
            }
        }

        if (maps.Count == 0)
        {
            throw new InvalidOperationException(
                "Last to Die requires at least one stock King of the Hill or Capture the Flag map.");
        }

        var survivors = LastToDieSurvivorCatalog.CreateStock();
        var perks = LastToDieExpansionPerkCatalog.Create(survivors);
        var ruleset = LastToDieRuleset.CreateDefault(ticksPerSecond) with
        {
            MaximumPlayers = maximumPlayers,
            StartingEnemyCount = maximumPlayers >= 2
                ? LastToDieRuleset.CoopStartingEnemyCount
                : LastToDieRuleset.SoloStartingEnemyCount,
        };
        return new LastToDieServerDirector(
            new LastToDieDirector(
                ruleset,
                survivors,
                perks,
                maps,
                difficulty,
                seed,
                runId));
    }
}
