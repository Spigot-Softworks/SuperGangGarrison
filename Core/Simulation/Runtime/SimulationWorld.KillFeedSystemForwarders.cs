namespace OpenGarrison.Core;

// Forwarders kept for callers outside the world partials (Client, Server, bots,
// plugins, tests). Callers should move to the system directly over time.
public sealed partial class SimulationWorld
{
    private void RecordIntelCapturedObjectiveLog(PlayerEntity player)
        => KillFeedRules.RecordIntelCapturedObjectiveLog(player);
    private void RecordIntelDroppedObjectiveLog(PlayerEntity player)
        => KillFeedRules.RecordIntelDroppedObjectiveLog(player);
    private void RecordIntelPickedUpObjectiveLog(PlayerEntity player)
        => KillFeedRules.RecordIntelPickedUpObjectiveLog(player);
    private void RecordIntelReturnedObjectiveLog(PlayerTeam team)
        => KillFeedRules.RecordIntelReturnedObjectiveLog(team);
    private void RecordObjectiveLogEntry(PlayerTeam team, string name, string messageText, string weaponSpriteName = "", int playerId = -1, IReadOnlyCollection<int>? involvedPlayerIds = null)
        => KillFeedRules.RecordObjectiveLogEntry(team, name, messageText, weaponSpriteName, playerId, involvedPlayerIds);
}
