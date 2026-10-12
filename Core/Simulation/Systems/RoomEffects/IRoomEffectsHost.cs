namespace OpenGarrison.Core;

/// <summary>
/// What <see cref="RoomEffectsSystem"/> needs from the world coordinator.
/// </summary>
internal interface IRoomEffectsHost : ISimulationWorldState, ISimulationPlayerDirectory
{
    PlayerDeathSystem PlayerDeaths { get; }
    WorldEffectsSystem WorldEffects { get; }

    /// <summary>Damage resolution (laser hazards).</summary>
    CombatSystem Combat { get; }

    /// <summary>True on a predicting client: hazards that deal damage are left to the server.</summary>
    bool ClientPredictionMode { get; }
}
