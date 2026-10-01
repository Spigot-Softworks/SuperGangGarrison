namespace OpenGarrison.Core;

// Capabilities the world coordinator exposes to its extracted systems. Each
// system receives one host interface composed from the capabilities it needs;
// SimulationWorld implements them explicitly so a missing member fails the
// build instead of silently running against a placeholder.

/// <summary>Read-only world facts shared by every simulation system.</summary>
internal interface ISimulationWorldState
{
    SimpleLevel Level { get; }
    SimulationConfig Config { get; }
    long Frame { get; }
    int GetSimulationTicksFromSourceTicks(float sourceTicks);
}

/// <summary>Player lookup and team-damage admission.</summary>
internal interface ISimulationPlayerDirectory
{
    IEnumerable<PlayerEntity> EnumerateSimulatedPlayers();
    PlayerEntity? FindPlayerById(int playerId);
    bool CanTeamDamagePlayer(PlayerTeam attackerTeam, int attackerId, PlayerEntity target);
}

/// <summary>The world's shared deterministic random stream.</summary>
internal interface ISimulationRandomSource
{
    int Next(int maximumExclusive);
    float NextSingle();
    double NextDouble();
}

/// <summary>Buildings and map structures owned by the world.</summary>
internal interface ISimulationStructures
{
    IReadOnlyList<SentryEntity> Sentries { get; }
    IReadOnlyList<JumpPadEntity> JumpPads { get; }
    IReadOnlyList<GeneratorState> Generators { get; }
    void DestroyJumpPad(JumpPadEntity jumpPad);
}

/// <summary>Sound, visual-effect, and trace events queued for presentation.</summary>
internal interface ISimulationPresentationEvents
{
    void RegisterWorldSoundEvent(string soundName, float x, float y, int sourcePlayerId);
    void RegisterSoundEvent(PlayerEntity player, string soundName);
    void RegisterVisualEffect(string effectName, float x, float y, float direction, int count, bool normalizeDirection);
    void RegisterImpactEffect(float x, float y, float direction);
    void RegisterBloodEffect(float x, float y, float direction, int count);
    void RegisterCombatTrace(float x, float y, float directionX, float directionY, float distance, bool hitCharacter, PlayerTeam team, bool isSniperTracer, bool isCritical);
    void RegisterStuckArrowEffect(float x, float y, float directionX, float directionY, ArrowProjectileEntity arrow);
    void RegisterExplosionTraces(float x, float y);
}

/// <summary>Experimental and Last-To-Die mode queries.</summary>
internal interface ISimulationExperimentalRules
{
    ExperimentalGameplaySettings GetLastToDieGameplaySettings(PlayerEntity? player);
    bool IsExperimentalPracticePowerOwner(PlayerEntity player);
    bool IsExperimentalEngineerPerkOwner(PlayerEntity? player);
}
