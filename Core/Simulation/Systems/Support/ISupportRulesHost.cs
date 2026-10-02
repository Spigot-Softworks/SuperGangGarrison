namespace OpenGarrison.Core;

/// <summary>
/// What <see cref="SupportRulesSystem"/> needs from the world coordinator.
/// </summary>
internal interface ISupportRulesHost : ISimulationWorldState, ISimulationPlayerDirectory
{
    bool ControlPointSetupActive { get; }
    PlayerEntity LocalPlayer { get; }

    int ApplyHealingWithFeedback(PlayerEntity target, float healing, string? soundName = null, float soundX = 0f, float soundY = 0f);
    void ApplyLastToDieMedicHomeostasis(PlayerEntity medic, int appliedTargetHealing);
    bool ApplyPlayerContinuousDamage(
        PlayerEntity target,
        float damage,
        PlayerEntity? attacker,
        float spyRevealAlpha = 0f,
        DamageEventFlags damageFlags = DamageEventFlags.None,
        bool allowOsmosisHealOwnedSentries = true,
        bool allowCivvieUmbrellaShield = true,
        float? civvieUmbrellaThreatSourceX = null,
        float? civvieUmbrellaThreatSourceY = null,
        int? civvieUmbrellaDrainTicks = null,
        bool civvieUmbrellaCriticalBoost = false);
    void AwardHealingPoints(PlayerEntity healer, int healedAmount);
    bool CanPlayerDamagePlayer(PlayerEntity attacker, PlayerEntity target);
    void GetCachedPlayerPresentationHitBounds(PlayerEntity player, out float left, out float top, out float right, out float bottom);
    int GetExperimentalEngineerCryoFreezeDurationTicks();
    int GetExperimentalEngineerEssenceExtractorDebuffTicks();
    int GetExperimentalEngineerFreezeRayExposureWindowTicks();
    int GetExperimentalEngineerFreezeRayFreezeThresholdTicks();
    int GetExperimentalEngineerFreezeRaySlowTicks();
    float GetLastToDieMedicHealingMultiplier(PlayerEntity medic, PlayerEntity target);
    float GetLastToDieMedicUberChargeGainMultiplier(PlayerEntity medic);
    float? GetThickLineIntersectionDistanceToPlayer(
        float originX,
        float originY,
        float endX,
        float endY,
        PlayerEntity player,
        float maxDistance,
        float thicknessRadius);
    bool HasObstacleLineOfSight(float originX, float originY, float targetX, float targetY);
    void KillPlayer(
        PlayerEntity player,
        bool gibbed = false,
        PlayerEntity? killer = null,
        string? weaponSpriteName = null,
        DeadBodyAnimationKind deadBodyAnimationKind = DeadBodyAnimationKind.Default,
        string? deathCamMessage = null,
        SentryEntity? deathCamSentry = null,
        string? killFeedMessage = null,
        bool createDeathCam = true,
        bool spawnRemains = true,
        bool forceCorpseRemains = false,
        bool recordKillFeed = true,
        int assistingPlayerIdOverride = -1,
        bool completingLastToDieSpyAfterlifeDeath = false);
    void RegisterBloodEffect(float x, float y, float directionDegrees, int count = 1);
    void RegisterHealingFeedbackOnly(PlayerEntity target, int amount);
    void RegisterVisualEffect(
        string effectName,
        float x,
        float y,
        float directionDegrees = 0f,
        int count = 1,
        bool normalizeDirection = true);
    void RegisterWorldSoundEvent(string soundName, float x, float y, int sourcePlayerId = -1);
    bool TryApplyLastToDieMedicSupportRelay(PlayerEntity medic, PlayerEntity target);
    bool TryGetPlayerNetworkSlot(PlayerEntity player, out byte slot);
}
