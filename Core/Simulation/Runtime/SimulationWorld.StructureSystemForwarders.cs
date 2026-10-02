using OpenGarrison.Core.LastToDie;
using OpenGarrison.GameplayModding;
using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

// Thin forwarders so remaining world code keeps its call shape; callers should migrate to the system directly over time.
public sealed partial class SimulationWorld
{
    private void AdvanceCivilDefenseTurrets() => Structures.AdvanceCivilDefenseTurrets();
    private void AdvanceJumpPadGibs() => Structures.AdvanceJumpPadGibs();
    private void AdvanceSentries() => Structures.AdvanceSentries();
    private void AdvanceSentryGibs() => Structures.AdvanceSentryGibs();
    private bool CanDeployCivilDefenseTurret(PlayerEntity player) => Structures.CanDeployCivilDefenseTurret(player);
    public void ClearLastToDieDroneSentries() => Structures.ClearLastToDieDroneSentries();
    private void DestroyJumpPad(JumpPadEntity pad) => Structures.DestroyJumpPad(pad);
    private void DestroySentry(SentryEntity sentry, PlayerEntity? attacker = null) => Structures.DestroySentry(sentry, attacker);
    public bool IsLastToDieDroneSentry(SentryEntity sentry) => Structures.IsLastToDieDroneSentry(sentry);
    public bool IsNearPrimaryWeaponSwapStation(PlayerEntity player) => Structures.IsNearPrimaryWeaponSwapStation(player);
    public int LastToDieDroneSentryCount => Structures.LastToDieDroneSentryCount;
    private void ResetJumpPadSpawnsForLevel() => Structures.ResetJumpPadSpawnsForLevel();
    public SentryEntity SpawnLastToDieDroneSentry(PlayerTeam team, float x, float y, float startDirectionX, int maxHealth = SentryEntity.DefaultMaxHealth) => Structures.SpawnLastToDieDroneSentry(team, x, y, startDirectionX, maxHealth);
    private bool TryBuildDispenser(PlayerEntity player) => Structures.TryBuildDispenser(player);
    private bool TryBuildJumpPad(PlayerEntity player, bool ignoreMetalCost = false) => Structures.TryBuildJumpPad(player, ignoreMetalCost);
    public bool TryBuildLocalDispenser() => Structures.TryBuildLocalDispenser();
    public bool TryBuildLocalJumpPad() => Structures.TryBuildLocalJumpPad();
    public bool TryBuildLocalSentry() => Structures.TryBuildLocalSentry();
    private bool TryBuildSentry(PlayerEntity player) => Structures.TryBuildSentry(player);
    private bool TryDeployCivilDefenseTurret(PlayerEntity player) => Structures.TryDeployCivilDefenseTurret(player);
    public bool TryDeployLastToDieDefenseBattery(byte ownerSlot) => Structures.TryDeployLastToDieDefenseBattery(ownerSlot);
    private bool TryDestroyDispenser(PlayerEntity player) => Structures.TryDestroyDispenser(player);
    private bool TryDestroyJumpPad(PlayerEntity player) => Structures.TryDestroyJumpPad(player);
    public bool TryDestroyLocalSentry() => Structures.TryDestroyLocalSentry();
    private bool TryDestroySentry(PlayerEntity player) => Structures.TryDestroySentry(player);
    private OwnedSentryDestroyResult TryDestroySentryForOwnerCommand(PlayerEntity player) => Structures.TryDestroySentryForOwnerCommand(player);
    private bool TryInterceptWithCivilDefenseTurret(PlayerTeam projectileTeam, float x, float y, float directionX, float directionY, float maxDistance) => Structures.TryInterceptWithCivilDefenseTurret(projectileTeam, x, y, directionX, directionY, maxDistance);
    private void UpdateDispenserAuras() => Structures.UpdateDispenserAuras();
}
