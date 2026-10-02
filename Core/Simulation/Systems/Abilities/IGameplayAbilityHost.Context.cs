using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

internal partial interface IGameplayAbilityHost
{
    // The mod-facing GameplayAbilityContext exposes the world itself, so the world builds it on the system's behalf.
    GameplayAbilityResult ExecuteAbilityExecutor(
        IGameplayAbilityExecutor executor,
        PlayerEntity player,
        GameplayItemDefinition item,
        GameplayAbilityDefinition ability,
        GameplayAbilityInputPhase phase,
        PlayerInputSnapshot input,
        PlayerInputSnapshot previousInput,
        float sourceX,
        float sourceY);
}