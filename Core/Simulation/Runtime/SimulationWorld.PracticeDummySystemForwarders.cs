namespace OpenGarrison.Core;

// Forwarders kept for callers outside the world partials (Client, Server, bots,
// plugins, tests). Callers should move to the system directly over time.
public sealed partial class SimulationWorld
{
    public void DespawnEnemyDummy()
        => PracticeDummies.DespawnEnemyDummy();
    public void DespawnFriendlyDummy()
        => PracticeDummies.DespawnFriendlyDummy();
    public void DespawnPracticeCombatDummy()
        => PracticeDummies.DespawnPracticeCombatDummy();
    public void DespawnPracticeDpsDummy()
        => PracticeDummies.DespawnPracticeDpsDummy();
    public bool IsPracticeCombatDummy(PlayerEntity player)
        => PracticeDummies.IsPracticeCombatDummy(player);
    public bool IsPracticeDpsDummy(PlayerEntity player)
        => PracticeDummies.IsPracticeDpsDummy(player);
    public float PracticeCombatDummyDamageIntensity => PracticeDummies.PracticeCombatDummyDamageIntensity;
    public double PracticeCombatDummyDps => PracticeDummies.PracticeCombatDummyDps;
    public bool PracticeCombatDummyDpsVisible => PracticeDummies.PracticeCombatDummyDpsVisible;
    public void SetEnemyPlayerName(string displayName)
        => PracticeDummies.SetEnemyPlayerName(displayName);
    public void SetEnemyPlayerTeam(PlayerTeam team)
        => PracticeDummies.SetEnemyPlayerTeam(team);
    public void SetFriendlyDummyHealth(int health)
        => PracticeDummies.SetFriendlyDummyHealth(health);
    public void SetFriendlyDummyName(string displayName)
        => PracticeDummies.SetFriendlyDummyName(displayName);
    public void SpawnEnemyDummy()
        => PracticeDummies.SpawnEnemyDummy();
    public void SpawnFriendlyDummy()
        => PracticeDummies.SpawnFriendlyDummy();
    public void SpawnPracticeCombatDummy()
        => PracticeDummies.SpawnPracticeCombatDummy();
    public void SpawnPracticeCombatDummy(PlayerClass playerClass)
        => PracticeDummies.SpawnPracticeCombatDummy(playerClass);
    public void SpawnPracticeDpsDummy()
        => PracticeDummies.SpawnPracticeDpsDummy();
}
