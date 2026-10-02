namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : ICombatFeedbackHost
{
    CombatRuntimeState ICombatFeedbackHost.CombatRuntime => CombatRuntime;
    ExperimentalGameplaySettings ICombatFeedbackHost.GetLastToDieGameplaySettings(PlayerEntity? player)
        => LastToDieRules.GetLastToDieGameplaySettings(player);
    bool ICombatFeedbackHost.IsNetworkPlayerActive(byte slot)
        => IsNetworkPlayerActive(slot);
    PlayerEntity ICombatFeedbackHost.LocalPlayer => LocalPlayer;
    SimulationRandomStreams ICombatFeedbackHost.Randoms => Randoms;
    void ICombatFeedbackHost.RecordKillFeedAnnouncement(PlayerEntity player, string prefix, string highlightedText, string suffix, string weaponSpriteName)
        => KillFeedRules.RecordKillFeedAnnouncement(player, prefix, highlightedText, suffix, weaponSpriteName);
    void ICombatFeedbackHost.RecordKillFeedEntry(PlayerEntity victim, PlayerEntity? killer, string weaponSpriteName, string? messageText, int messageHighlightStart, int messageHighlightLength, KillFeedSpecialType specialType, PlayerEntity? assistingPlayer)
        => KillFeedRules.RecordKillFeedEntry(victim, killer, weaponSpriteName, messageText, messageHighlightStart, messageHighlightLength, specialType, assistingPlayer);
    void ICombatFeedbackHost.SpawnFlame(PlayerEntity owner, float x, float y, float velocityX, float velocityY, float directHitDamage, float burnDamagePerTick)
        => SpawnFlame(owner, x, y, velocityX, velocityY, directHitDamage, burnDamagePerTick);
    bool ICombatFeedbackHost.TryGetPlayerNetworkSlot(PlayerEntity player, out byte slot)
        => NetworkPlayerRules.TryGetPlayerNetworkSlot(player, out slot);
}
