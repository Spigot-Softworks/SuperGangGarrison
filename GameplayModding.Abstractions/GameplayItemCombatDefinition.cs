namespace OpenGarrison.GameplayModding;

public sealed record GameplayItemCombatDefinition(
    string? FireSoundName = null,
    GameplayRocketCombatDefinition? Rocket = null);
