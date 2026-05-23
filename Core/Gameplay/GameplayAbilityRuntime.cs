using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public enum GameplayAbilityInputPhase
{
    Pressed = 1,
    Held = 2,
    Released = 3,
    PassiveTick = 4,
}

public sealed class GameplayAbilityContext
{
    public required SimulationWorld World { get; init; }

    public required PlayerEntity Player { get; init; }

    public required GameplayItemDefinition Item { get; init; }

    public required GameplayAbilityDefinition Ability { get; init; }

    public required GameplayAbilityInputPhase Phase { get; init; }

    public required PlayerInputSnapshot Input { get; init; }

    public required PlayerInputSnapshot PreviousInput { get; init; }

    public required float SourceX { get; init; }

    public required float SourceY { get; init; }
}

public readonly record struct GameplayAbilityResult(
    bool Handled,
    bool ConsumedInput,
    bool SuppressPrimary = false)
{
    public static GameplayAbilityResult Ignored { get; } = new(false, false);

    public static GameplayAbilityResult HandledAndConsumed { get; } = new(true, true);
}

public interface IGameplayAbilityExecutor
{
    GameplayAbilityResult Handle(GameplayAbilityContext context);
}

internal sealed class DelegateGameplayAbilityExecutor(Func<GameplayAbilityContext, GameplayAbilityResult> handle) : IGameplayAbilityExecutor
{
    public GameplayAbilityResult Handle(GameplayAbilityContext context)
    {
        return handle(context);
    }
}
