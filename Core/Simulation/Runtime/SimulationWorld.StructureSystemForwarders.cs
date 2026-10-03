using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

// Thin forwarders so remaining world code keeps its call shape; callers should migrate to the system directly over time.
public sealed partial class SimulationWorld
{
    public void ClearLastToDieDroneSentries() => Structures.ClearLastToDieDroneSentries();
    public bool IsLastToDieDroneSentry(SentryEntity sentry) => Structures.IsLastToDieDroneSentry(sentry);
    public bool IsNearPrimaryWeaponSwapStation(PlayerEntity player) => Structures.IsNearPrimaryWeaponSwapStation(player);
    public int LastToDieDroneSentryCount => Structures.LastToDieDroneSentryCount;
    public SentryEntity SpawnLastToDieDroneSentry(PlayerTeam team, float x, float y, float startDirectionX, int maxHealth = SentryEntity.DefaultMaxHealth) => Structures.SpawnLastToDieDroneSentry(team, x, y, startDirectionX, maxHealth);
    public bool TryBuildLocalDispenser() => Structures.TryBuildLocalDispenser();
    public bool TryBuildLocalJumpPad() => Structures.TryBuildLocalJumpPad();
    public bool TryBuildLocalSentry() => Structures.TryBuildLocalSentry();
    public bool TryDeployLastToDieDefenseBattery(byte ownerSlot) => Structures.TryDeployLastToDieDefenseBattery(ownerSlot);
    public bool TryDestroyLocalSentry() => Structures.TryDestroyLocalSentry();
}
