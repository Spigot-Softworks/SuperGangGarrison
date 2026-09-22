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

public enum LastToDiePerkTier : byte
{
    Standard = 0,
    Rare = 1,
    Ultra = 2,
}

public enum LastToDiePerkScope : byte
{
    Survivor = 0,
    AllClass = 1,
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
        LastToDieSurvivorId? survivorId,
        string displayName,
        string description,
        int rank = 1,
        IReadOnlyList<LastToDiePerkId>? requires = null,
        IReadOnlyList<LastToDiePerkId>? excludes = null,
        IReadOnlyList<string>? tags = null,
        LastToDiePerkTier tier = LastToDiePerkTier.Standard,
        LastToDiePerkScope scope = LastToDiePerkScope.Survivor)
    {
        Id = id;
        SurvivorId = survivorId;
        DisplayName = displayName;
        Description = description;
        Rank = rank;
        Requires = Freeze(requires);
        Excludes = Freeze(excludes);
        Tags = Freeze(tags);
        Tier = tier;
        Scope = scope;
    }

    public LastToDiePerkId Id { get; }

    public LastToDieSurvivorId? SurvivorId { get; }

    public string DisplayName { get; }

    public string Description { get; }

    public int Rank { get; }

    public IReadOnlyList<LastToDiePerkId> Requires { get; }

    public IReadOnlyList<LastToDiePerkId> Excludes { get; }

    public IReadOnlyList<string> Tags { get; }

    public LastToDiePerkTier Tier { get; }

    public LastToDiePerkScope Scope { get; }

    private static IReadOnlyList<T> Freeze<T>(IReadOnlyList<T>? values)
        => values is null or { Count: 0 }
            ? Array.Empty<T>()
            : new ReadOnlyCollection<T>(values.ToArray());
}

public sealed record LastToDieRewardOfferSlot(
    LastToDiePerkId PerkId,
    LastToDiePerkTier Tier,
    byte RerollsRemaining,
    bool HasEligibleReplacement);

public sealed record LastToDieRewardOffer
{
    public LastToDieRewardOffer(
        ulong offerId,
        int draftOrdinal,
        IReadOnlyList<LastToDieRewardOfferSlot> slots,
        int targetStage = 0,
        int selectionNumber = 1,
        int selectionsRequired = 1,
        bool guaranteedTierConsumed = false)
    {
        OfferId = offerId;
        DraftOrdinal = draftOrdinal;
        Slots = Array.AsReadOnly((slots ?? throw new ArgumentNullException(nameof(slots))).ToArray());
        TargetStage = targetStage;
        SelectionNumber = selectionNumber;
        SelectionsRequired = selectionsRequired;
        GuaranteedTierConsumed = guaranteedTierConsumed;
    }

    // Kept for diagnostic tools and existing callers that construct a simple
    // standard-tier offer. Authoritative drafts use structured slot metadata.
    public LastToDieRewardOffer(ulong offerId, int draftOrdinal, IReadOnlyList<LastToDiePerkId> choices)
        : this(
            offerId,
            draftOrdinal,
            choices.Select(static perk => new LastToDieRewardOfferSlot(
                perk,
                LastToDiePerkTier.Standard,
                0,
                false)).ToArray())
    {
    }

    public ulong OfferId { get; }

    public int DraftOrdinal { get; }

    public IReadOnlyList<LastToDieRewardOfferSlot> Slots { get; }

    public IReadOnlyList<LastToDiePerkId> Choices => Slots.Select(static slot => slot.PerkId).ToArray();

    public int TargetStage { get; }

    public int SelectionNumber { get; }

    public int SelectionsRequired { get; }

    public bool GuaranteedTierConsumed { get; }
}

public sealed record LastToDiePlayerSnapshot(
    Guid PlayerId,
    LastToDieSurvivorId? SurvivorId,
    IReadOnlyList<LastToDiePerkId> OwnedPerks,
    LastToDieRewardOffer? ActiveOffer,
    bool IsReady,
    bool IsAlive,
    int Kills,
    int ConquistadorStacks = 0,
    int PendingBonusSelections = 0,
    int LuckyDrawRoundsRemaining = 0,
    int SelectionsRemaining = 0,
    bool SecondChanceConsumed = false);

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
