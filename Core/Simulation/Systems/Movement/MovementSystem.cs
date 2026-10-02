using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

/// <summary>
/// Everything <see cref="MovementSystem"/> needs from the world. The system owns
/// movement state and traversal behavior; the host supplies only the
/// level/entity queries and presentation or structure consequences that are
/// outside movement state.
/// </summary>
internal interface IMovementSystemHost :
    ISimulationWorldState,
    ISimulationPlayerDirectory,
    ISimulationStructures,
    ISimulationPresentationEvents,
    ISimulationExperimentalRules
{
    IEnumerable<ArrowProjectileEntity> EnumerateArrowProjectiles();

    bool TryFindWhippingCordTerrainContact(
        PlayerEntity player,
        GameplayItemDefinition item,
        float aimWorldX,
        float aimWorldY,
        out float contactX,
        out float contactY);

    bool IsWhippingCordTerrainLatchPathClear(
        PlayerEntity player,
        float originX,
        float originY,
        float directionX,
        float directionY,
        float distance);
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
    private readonly IMovementSystemHost _host;
    private readonly Dictionary<int, int> _jumpInputBufferTicksByPlayerId = new();
    private readonly Dictionary<(int PlayerId, int RoomObjectIndex), bool> _catapultContacts = new();
    private readonly List<MovingPlatformRuntimeState> _movingPlatforms = new();
    private int[] _teleportZoneIndexByPlayerId = [];
    private SimpleLevel? _catapultContactLevel;

    public MovementSystem()
        : this(new DetachedSimulationHost())
    {
    }

    internal MovementSystem(IMovementSystemHost host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
    }

    public IReadOnlyList<MovingPlatformRuntimeState> MovingPlatforms => _movingPlatforms;

    private SimpleLevel Level => _host.Level;
    private SimulationConfig Config => _host.Config;
    private IReadOnlyList<JumpPadEntity> JumpPads => _host.JumpPads;
    private IReadOnlyList<SentryEntity> Sentries => _host.Sentries;

    private PlayerEntity? FindPlayerById(int playerId) => _host.FindPlayerById(playerId);
    private IEnumerable<PlayerEntity> EnumerateSimulatedPlayers() => _host.EnumerateSimulatedPlayers();

    private void RegisterWorldSoundEvent(string sound, float x, float y, int sourcePlayerId = -1)
        => _host.RegisterWorldSoundEvent(sound, x, y, sourcePlayerId);

    private void RegisterSoundEvent(PlayerEntity player, string sound)
        => _host.RegisterSoundEvent(player, sound);

    private void RegisterVisualEffect(
        string effect,
        float x,
        float y,
        float direction = 0f,
        int count = 1,
        bool normalizeDirection = true)
        => _host.RegisterVisualEffect(effect, x, y, direction, count, normalizeDirection);

    private ExperimentalGameplaySettings GetLastToDieGameplaySettings(PlayerEntity? player)
        => _host.GetLastToDieGameplaySettings(player);

    private bool IsExperimentalEngineerPerkOwner(PlayerEntity? player)
        => _host.IsExperimentalEngineerPerkOwner(player);
}
