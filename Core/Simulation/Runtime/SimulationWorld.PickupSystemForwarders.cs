using OpenGarrison.Core.LastToDie;
using OpenGarrison.GameplayModding;
using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

// Thin forwarders so remaining world code keeps its call shape; callers should migrate to the system directly over time.
public sealed partial class SimulationWorld
{
    private void AdvanceDroppedWeapons() => Pickups.AdvanceDroppedWeapons();
    private void AdvanceHealthPacks() => Pickups.AdvanceHealthPacks();
    private void ClearDroppedWeapons() => Pickups.ClearDroppedWeapons();
    private void ClearTemporaryHealthPacks() => Pickups.ClearTemporaryHealthPacks();
    public int GetHealthPackSpawnRespawnTicksRemaining(int spawnIndex) => Pickups.GetHealthPackSpawnRespawnTicksRemaining(spawnIndex);
    private void ResetHealthPackSpawnsForLevel() => Pickups.ResetHealthPackSpawnsForLevel();
    private void TryHandleDroppedWeaponInteraction(PlayerEntity player) => Pickups.TryHandleDroppedWeaponInteraction(player);
    private void TrySpawnExperimentalEnemyDroppedWeapon(PlayerEntity victim, PlayerEntity? killer) => Pickups.TrySpawnExperimentalEnemyDroppedWeapon(victim, killer);
    private void TrySpawnExperimentalEnemyHealthPackDrop(PlayerEntity victim, PlayerEntity? killer) => Pickups.TrySpawnExperimentalEnemyHealthPackDrop(victim, killer);
}
