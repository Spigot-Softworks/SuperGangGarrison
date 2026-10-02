namespace OpenGarrison.Core;

// Forwarders kept for callers outside the world partials (Client, Server, bots,
// plugins, tests). Callers should move to the system directly over time.
public sealed partial class SimulationWorld
{
    private void FlushExperimentalEngineerEssenceExtractorHealing(PlayerEntity engineer)
        => SupportRules.FlushExperimentalEngineerEssenceExtractorHealing(engineer);
    public string GetMedicSummary()
        => SupportRules.GetMedicSummary();
    public bool TryFillLocalMedicUber()
        => SupportRules.TryFillLocalMedicUber();
}
