namespace OpenGarrison.Core;

// Forwarders kept for callers outside the world partials (Client, Server, bots,
// plugins, tests). Callers should move to the system directly over time.
public sealed partial class SimulationWorld
{
    public void AdvanceCompetitiveReadyUp(IReadOnlyCollection<byte> playableSlots)
        => ReadyUp.AdvanceCompetitiveReadyUp(playableSlots);
    public bool CompetitiveObjectivesLocked => ReadyUp.CompetitiveObjectivesLocked;
    public bool CompetitiveReadyUpEnabled => ReadyUp.CompetitiveReadyUpEnabled;
    public CompetitiveReadyUpPhase CompetitiveReadyUpPhase => ReadyUp.CompetitiveReadyUpPhase;
    public int CompetitiveReadyUpTicksRemaining => ReadyUp.CompetitiveReadyUpTicksRemaining;
    public int CompetitiveSetupSeconds => ReadyUp.CompetitiveSetupSeconds;
    public bool IsNetworkPlayerReady(byte slot)
        => ReadyUp.IsNetworkPlayerReady(slot);
    public void SetCompetitiveReadyUpEnabled(bool enabled)
        => ReadyUp.SetCompetitiveReadyUpEnabled(enabled);
    public void SetCompetitiveSetupSeconds(int seconds)
        => ReadyUp.SetCompetitiveSetupSeconds(seconds);
    public bool TrySetNetworkPlayerReady(byte slot, bool ready)
        => ReadyUp.TrySetNetworkPlayerReady(slot, ready);
    public bool TryToggleNetworkPlayerReady(byte slot)
        => ReadyUp.TryToggleNetworkPlayerReady(slot);
    private const int DefaultCompetitiveSetupSeconds = ReadyUpSystem.DefaultCompetitiveSetupSeconds;
}
