using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    GameplayAbilityResult IGameplayAbilityHost.ExecuteAbilityExecutor(
        IGameplayAbilityExecutor executor,
        PlayerEntity player,
        GameplayItemDefinition item,
        GameplayAbilityDefinition ability,
        GameplayAbilityInputPhase phase,
        PlayerInputSnapshot input,
        PlayerInputSnapshot previousInput,
        float sourceX,
        float sourceY)
    {
        return executor.Handle(new GameplayAbilityContext
        {
            World = this,
            Player = player,
            Item = item,
            Ability = ability,
            Phase = phase,
            Input = input,
            PreviousInput = previousInput,
            SourceX = sourceX,
            SourceY = sourceY,
        });
    }
}