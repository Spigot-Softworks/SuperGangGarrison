namespace OpenGarrison.Core.LastToDie;

/// <summary>Starting bonuses and health drops shared by offline and hosted runs.</summary>
public static class LastToDieSurvivorRules
{
    public const string BuffName = "Survivor";
    public const float DamageTakenMultiplier = 0.8f;
    public const int RegenerationPerSecond = 3;

    public static float GetHealthPackDropChance(int stageNumber)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(stageNumber, 1);
        return stageNumber <= 3 ? 1f : 0.5f;
    }
}
