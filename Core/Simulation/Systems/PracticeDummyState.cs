namespace OpenGarrison.Core;

internal enum PracticeCombatDummyMode
{
    None,
    Combat,
    Dps,
}

/// <summary>Owns the scripted enemy dummy (class, team, input, strafe, respawn) and practice combat/DPS dummy tracking.</summary>
internal sealed class PracticeDummyState
{
    public CharacterClassDefinition EnemyClassDefinition { get; set; } = CharacterClassCatalog.Scout;

    public PlayerTeam EnemyTeam { get; set; } = PlayerTeam.Blue;

    public PlayerInputSnapshot EnemyInput { get; set; }

    public PlayerInputSnapshot PreviousEnemyInput { get; set; }

    public bool EnemyInputOverrideActive { get; set; }

    public int EnemyRespawnTicks { get; set; }

    public int EnemyStrafeDirection { get; set; } = -1;

    public int EnemyStrafeTicksRemaining { get; set; }

    public PracticeCombatDummyMode CombatMode { get; set; }

    public CharacterClassDefinition? CombatClassDefinition { get; set; }

    public int CombatTotalDamage { get; set; }

    public long CombatFirstDamageFrame { get; set; } = -1;

    public long CombatLastDamageFrame { get; set; } = -1;

    public float CombatContinuousDamageAccumulator { get; set; }
}
