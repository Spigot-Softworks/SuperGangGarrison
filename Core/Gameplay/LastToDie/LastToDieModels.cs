using System.Collections.ObjectModel;

namespace OpenGarrison.Core.LastToDie;

public enum LastToDieDifficulty : byte
{
    Standard = 0,
    Hardcore = 1,
}

public enum LastToDiePhase : byte
{
    Lobby = 0,
    SurvivorChoice = 1,
    RewardChoice = 2,
    LoadingStage = 3,
    Playing = 4,
    Won = 5,
    Lost = 6,
}

public readonly record struct LastToDieSurvivorId(string Value)
{
    public override string ToString() => Value ?? string.Empty;
}

public readonly record struct LastToDiePerkId(string Value)
{
    public override string ToString() => Value ?? string.Empty;
}

public sealed record LastToDieSurvivorDefinition(
    LastToDieSurvivorId Id,
    string GameplayClassId,
    string DisplayName);

public sealed record LastToDiePerkDefinition
{
    public LastToDiePerkDefinition(
        LastToDiePerkId id,
        LastToDieSurvivorId survivorId,
        string displayName,
        string description,
        int rank = 1,
        IReadOnlyList<LastToDiePerkId>? requires = null,
        IReadOnlyList<LastToDiePerkId>? excludes = null,
        IReadOnlyList<string>? tags = null)
    {
        Id = id;
        SurvivorId = survivorId;
        DisplayName = displayName;
        Description = description;
        Rank = rank;
        Requires = Freeze(requires);
        Excludes = Freeze(excludes);
        Tags = Freeze(tags);
    }

    public LastToDiePerkId Id { get; }

    public LastToDieSurvivorId SurvivorId { get; }

    public string DisplayName { get; }

    public string Description { get; }

    public int Rank { get; }

    public IReadOnlyList<LastToDiePerkId> Requires { get; }

    public IReadOnlyList<LastToDiePerkId> Excludes { get; }

    public IReadOnlyList<string> Tags { get; }

    private static IReadOnlyList<T> Freeze<T>(IReadOnlyList<T>? values)
        => values is null or { Count: 0 }
            ? Array.Empty<T>()
            : new ReadOnlyCollection<T>(values.ToArray());
}

public sealed record LastToDieRewardOffer(
    ulong OfferId,
    int DraftOrdinal,
    IReadOnlyList<LastToDiePerkId> Choices);

public sealed record LastToDiePlayerSnapshot(
    Guid PlayerId,
    LastToDieSurvivorId? SurvivorId,
    IReadOnlyList<LastToDiePerkId> OwnedPerks,
    LastToDieRewardOffer? ActiveOffer,
    bool IsReady,
    bool IsAlive,
    int Kills,
    int ConquistadorStacks = 0);

public sealed record LastToDieRunSnapshot(
    Guid RunId,
    ulong StructuralRevision,
    ulong Seed,
    int RulesetVersion,
    LastToDieDifficulty Difficulty,
    LastToDiePhase Phase,
    int StageNumber,
    ulong StageInstanceId,
    string CurrentMap,
    int EnemyCount,
    long StageEndServerTick,
    long RunEndServerTick,
    IReadOnlyList<LastToDiePlayerSnapshot> Players,
    string TerminalReason = "");
