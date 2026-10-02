namespace OpenGarrison.Core;

public readonly record struct WorldGameplayAbilityEvent(
    long Frame,
    int PlayerId,
    PlayerClass ClassId,
    PlayerTeam Team,
    string ItemId,
    string BehaviorId,
    string AbilityCategory,
    string Activation,
    string ExecutorId,
    GameplayAbilityInputPhase Phase,
    IReadOnlyList<string> Tags,
    bool Handled,
    bool ConsumedInput,
    bool Cancelled);

