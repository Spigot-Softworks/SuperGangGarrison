using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private MovementSystemDependencies CreateMovementSystemDependencies()
    {
        return new MovementSystemDependencies
        {
            GetLevel = () => Level,
            Config = Config,
            EnumerateSimulatedPlayers = EnumerateSimulatedPlayers,
            FindPlayerById = FindPlayerById,
            JumpPads = _jumpPads,
            Sentries = _sentries,
            EnumerateArrowProjectiles = () => Needles.OfType<ArrowProjectileEntity>(),
            RegisterWorldSoundEvent = RegisterWorldSoundEvent,
            RegisterSoundEvent = RegisterSoundEvent,
            RegisterVisualEffect = RegisterVisualEffect,
            DestroyJumpPad = DestroyJumpPad,
            GetLastToDieGameplaySettings = GetLastToDieGameplaySettings,
            IsExperimentalEngineerPerkOwner = IsExperimentalEngineerPerkOwner,
            TryFindWhippingCordTerrainContact = (PlayerEntity player, GameplayItemDefinition item, float aimWorldX, float aimWorldY, out float contactX, out float contactY) =>
                TryFindWhippingCordTerrainContact(player, item, aimWorldX, aimWorldY, out contactX, out contactY),
            IsWhippingCordTerrainLatchPathClear = (player, originX, originY, directionX, directionY, distance) =>
            {
                var firstHit = GeometryResolver.ResolveRifleHit(
                    player,
                    originX,
                    originY,
                    directionX,
                    directionY,
                    distance);
                return firstHit.HitPlayer is null
                    && firstHit.HitSentry is null
                    && firstHit.HitGenerator is null
                    && firstHit.HitJumpPad is null;
            },
        };
    }
}
