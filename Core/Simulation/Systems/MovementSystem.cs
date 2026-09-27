using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public delegate bool MovementWhippingCordTerrainContactProvider(
    PlayerEntity player,
    GameplayItemDefinition item,
    float aimWorldX,
    float aimWorldY,
    out float contactX,
    out float contactY);

public delegate bool MovementWhippingCordTerrainLatchPathProvider(
    PlayerEntity player,
    float originX,
    float originY,
    float directionX,
    float directionY,
    float distance);

/// <summary>
/// World services used by <see cref="MovementSystem"/>. The system owns
/// movement state and traversal behavior; the coordinator supplies only the
/// level/entity queries and presentation or structure consequences that are
/// outside movement state.
/// </summary>
public sealed record MovementSystemDependencies
{
    public Func<SimpleLevel> GetLevel { get; init; } = static () => SimpleLevelFactory.CreateScoutPrototypeLevel(1f);
    public SimulationConfig Config { get; init; } = new();
    public Func<IEnumerable<PlayerEntity>> EnumerateSimulatedPlayers { get; init; } = static () => [];
    public Func<int, PlayerEntity?> FindPlayerById { get; init; } = static _ => null;
    public IReadOnlyList<JumpPadEntity> JumpPads { get; init; } = [];
    public IReadOnlyList<SentryEntity> Sentries { get; init; } = [];
    public Func<IEnumerable<ArrowProjectileEntity>> EnumerateArrowProjectiles { get; init; } = static () => [];
    public Action<string, float, float, int> RegisterWorldSoundEvent { get; init; } = static (_, _, _, _) => { };
    public Action<PlayerEntity, string> RegisterSoundEvent { get; init; } = static (_, _) => { };
    public Action<string, float, float, float, int, bool> RegisterVisualEffect { get; init; } = static (_, _, _, _, _, _) => { };
    public Action<JumpPadEntity> DestroyJumpPad { get; init; } = static _ => { };
    public Func<PlayerEntity?, ExperimentalGameplaySettings> GetLastToDieGameplaySettings { get; init; } = static _ => new();
    public Func<PlayerEntity?, bool> IsExperimentalEngineerPerkOwner { get; init; } = static _ => false;
    public MovementWhippingCordTerrainContactProvider TryFindWhippingCordTerrainContact { get; init; } =
        static (_, _, _, _, out contactX, out contactY) =>
        {
            contactX = 0f;
            contactY = 0f;
            return false;
        };
    public MovementWhippingCordTerrainLatchPathProvider IsWhippingCordTerrainLatchPathClear { get; init; } =
        static (_, _, _, _, _, _) => true;
}

public readonly record struct MovementPreparationResult(
    PlayerInputSnapshot Input,
    bool JumpPressed,
    bool StartedGrounded,
    bool Jumped,
    bool EmitWallspinDust);

/// <summary>Owns environmental movement, traversal, and movement preparation state.</summary>
public sealed partial class MovementSystem
{
    private readonly MovementSystemDependencies _dependencies;
    private readonly Dictionary<int, int> _jumpInputBufferTicksByPlayerId = new();
    private readonly Dictionary<(int PlayerId, int RoomObjectIndex), bool> _catapultContacts = new();
    private readonly List<MovingPlatformRuntimeState> _movingPlatforms = new();
    private int[] _teleportZoneIndexByPlayerId = [];
    private SimpleLevel? _catapultContactLevel;

    public MovementSystem(MovementSystemDependencies? dependencies = null)
    {
        _dependencies = dependencies ?? new MovementSystemDependencies();
    }

    public IReadOnlyList<MovingPlatformRuntimeState> MovingPlatforms => _movingPlatforms;

    private SimpleLevel Level => _dependencies.GetLevel();
    private SimulationConfig Config => _dependencies.Config;
    private IReadOnlyList<JumpPadEntity> JumpPads => _dependencies.JumpPads;
    private IReadOnlyList<SentryEntity> Sentries => _dependencies.Sentries;

    private PlayerEntity? FindPlayerById(int playerId) => _dependencies.FindPlayerById(playerId);
    private IEnumerable<PlayerEntity> EnumerateSimulatedPlayers() => _dependencies.EnumerateSimulatedPlayers();

    private void RegisterWorldSoundEvent(string sound, float x, float y, int sourcePlayerId = -1)
        => _dependencies.RegisterWorldSoundEvent(sound, x, y, sourcePlayerId);

    private void RegisterSoundEvent(PlayerEntity player, string sound)
        => _dependencies.RegisterSoundEvent(player, sound);

    private void RegisterVisualEffect(
        string effect,
        float x,
        float y,
        float direction = 0f,
        int count = 1,
        bool normalizeDirection = true)
        => _dependencies.RegisterVisualEffect(effect, x, y, direction, count, normalizeDirection);

    private ExperimentalGameplaySettings GetLastToDieGameplaySettings(PlayerEntity? player)
        => _dependencies.GetLastToDieGameplaySettings(player);

    private bool IsExperimentalEngineerPerkOwner(PlayerEntity? player)
        => _dependencies.IsExperimentalEngineerPerkOwner(player);
}
