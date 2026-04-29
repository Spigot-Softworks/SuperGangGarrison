namespace OpenGarrison.MLBot.Contracts;

public readonly record struct MLBotStepResult(
    MLBotObservation Observation,
    MLBotRewardBreakdown Reward,
    bool IsTerminal,
    bool IsSuccess,
    int Tick,
    string TerminalReason);
