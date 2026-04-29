namespace OpenGarrison.MLBot.Contracts;

public readonly record struct MLBotRewardBreakdown(
    float ProgressReward,
    float ObjectiveReward,
    float DeathPenalty,
    float TimeoutPenalty,
    float StuckPenalty)
{
    public float Total => ProgressReward + ObjectiveReward + DeathPenalty + TimeoutPenalty + StuckPenalty;
}
