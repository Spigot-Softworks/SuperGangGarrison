using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public sealed class GameplayPrimaryWeaponContext
{
    public required SimulationWorld World { get; init; }

    public required PlayerEntity Player { get; init; }

    public required PrimaryWeaponDefinition Weapon { get; init; }

    public required string ItemId { get; init; }

    public required string BehaviorId { get; init; }

    public required PlayerClass WeaponClassId { get; init; }

    public required float SourceX { get; init; }

    public required float SourceY { get; init; }

    public required float AimWorldX { get; init; }

    public required float AimWorldY { get; init; }

    public required float DirectionX { get; init; }

    public required float DirectionY { get; init; }

    public required float DirectionRadians { get; init; }

    public required string KillFeedWeaponSpriteName { get; init; }
}

public readonly record struct GameplayPrimaryWeaponResult(bool Handled)
{
    public static GameplayPrimaryWeaponResult Ignored { get; } = new(false);

    public static GameplayPrimaryWeaponResult HandledResult { get; } = new(true);
}

public interface IGameplayPrimaryWeaponExecutor
{
    GameplayPrimaryWeaponResult Handle(GameplayPrimaryWeaponContext context);
}

internal sealed class DelegateGameplayPrimaryWeaponExecutor(Func<GameplayPrimaryWeaponContext, GameplayPrimaryWeaponResult> handle) : IGameplayPrimaryWeaponExecutor
{
    public GameplayPrimaryWeaponResult Handle(GameplayPrimaryWeaponContext context)
    {
        return handle(context);
    }
}
