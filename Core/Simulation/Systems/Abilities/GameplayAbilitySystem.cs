namespace OpenGarrison.Core;

internal sealed partial class GameplayAbilitySystem
{
    private readonly IGameplayAbilityHost _host;

    public GameplayAbilitySystem(IGameplayAbilityHost host)
    {
        _host = host;
    }
}
