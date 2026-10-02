namespace OpenGarrison.Core;

/// <summary>
/// What <see cref="CombatFeedbackSystem"/> needs from the world coordinator.
/// </summary>
internal interface ICombatFeedbackHost : ISimulationWorldState, ISimulationPlayerDirectory
{
    CombatRuntimeState CombatRuntime { get; }
    PlayerEntity LocalPlayer { get; }
    SimulationRandomStreams Randoms { get; }

    ExperimentalGameplaySettings GetLastToDieGameplaySettings(PlayerEntity? player);
    bool IsNetworkPlayerActive(byte slot);
    void RecordKillFeedAnnouncement(
        PlayerEntity player,
        string prefix,
        string highlightedText,
        string suffix,
        string weaponSpriteName = "");
    void RecordKillFeedEntry(
        PlayerEntity victim,
        PlayerEntity? killer,
        string weaponSpriteName,
        string? messageText = null,
        int messageHighlightStart = 0,
        int messageHighlightLength = 0,
        KillFeedSpecialType specialType = KillFeedSpecialType.None,
        PlayerEntity? assistingPlayer = null);
    void SpawnFlame(
        PlayerEntity owner,
        float x,
        float y,
        float velocityX,
        float velocityY,
        float directHitDamage = FlameProjectileEntity.DirectHitDamage,
        float burnDamagePerTick = FlameProjectileEntity.BurnDamagePerTick);
    bool TryGetPlayerNetworkSlot(PlayerEntity player, out byte slot);
}
