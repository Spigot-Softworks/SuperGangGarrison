using OpenGarrison.Core;

namespace OpenGarrison.BotAI;

public readonly record struct ControlledBotSlot(
    byte Slot,
    PlayerTeam Team,
    PlayerClass ClassId,
    BotPathMode PathMode = BotPathMode.ClientBot2020Compat);
