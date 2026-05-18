using OpenGarrison.Core;

namespace OpenGarrison.Core.BotBrain;

public readonly record struct ControlledBotSlot(
    byte Slot,
    PlayerTeam Team,
    PlayerClass ClassId,
    bool PreferEnemyPlayerObjective = false);
