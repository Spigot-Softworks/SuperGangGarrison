using OpenGarrison.Core.LastToDie;
using OpenGarrison.GameplayModding;
using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

// Thin forwarders so remaining world code keeps its call shape; callers should migrate to the system directly over time.
public sealed partial class SimulationWorld
{
    public IReadOnlyList<WorldGameplayAbilityEvent> DrainPendingGameplayAbilityEvents() => Abilities.DrainPendingGameplayAbilityEvents();
    internal GameplayPrimaryWeaponResult ExecuteBoomstickPrimaryWeapon(GameplayPrimaryWeaponContext context) => Abilities.ExecuteBoomstickPrimaryWeapon(context);
    internal static GameplayAbilityResult ExecuteCivviePogoAbility(GameplayAbilityContext context) => GameplayAbilitySystem.ExecuteCivviePogoAbility(context);
    internal static GameplayAbilityResult ExecuteCivvieTauntAbility(GameplayAbilityContext context) => GameplayAbilitySystem.ExecuteCivvieTauntAbility(context);
    internal GameplayAbilityResult ExecuteCivvieUmbrellaAbility(GameplayAbilityContext context) => Abilities.ExecuteCivvieUmbrellaAbility(context);
    internal GameplayAbilityResult ExecuteDemomanDetonateAbility(GameplayAbilityContext context) => Abilities.ExecuteDemomanDetonateAbility(context);
    internal GameplayPrimaryWeaponResult ExecuteDragonRagePrimaryWeapon(GameplayPrimaryWeaponContext context) => Abilities.ExecuteDragonRagePrimaryWeapon(context);
    internal GameplayAbilityResult ExecuteEngineerJumpPadAbility(GameplayAbilityContext context) => Abilities.ExecuteEngineerJumpPadAbility(context);
    internal GameplayAbilityResult ExecuteEngineerPdaAbility(GameplayAbilityContext context) => Abilities.ExecuteEngineerPdaAbility(context);
    internal GameplayAbilityResult ExecuteExperimentalLtdPassiveAbility(GameplayAbilityContext context) => Abilities.ExecuteExperimentalLtdPassiveAbility(context);
    internal GameplayAbilityResult ExecuteExperimentalLtdRageAbility(GameplayAbilityContext context) => Abilities.ExecuteExperimentalLtdRageAbility(context);
    internal GameplayAbilityResult ExecuteExperimentalSoldierSecondaryAbility(GameplayAbilityContext context) => Abilities.ExecuteExperimentalSoldierSecondaryAbility(context);
    internal GameplayPrimaryWeaponResult ExecuteFlaregunPrimaryWeapon(GameplayPrimaryWeaponContext context) => Abilities.ExecuteFlaregunPrimaryWeapon(context);
    internal GameplayAbilityResult ExecuteHeavyGhostDashAbility(GameplayAbilityContext context) => Abilities.ExecuteHeavyGhostDashAbility(context);
    internal GameplayAbilityResult ExecuteHeavySandvichAbility(GameplayAbilityContext context) => Abilities.ExecuteHeavySandvichAbility(context);
    internal GameplayAbilityResult ExecuteMedicKritzBeamAbility(GameplayAbilityContext context) => Abilities.ExecuteMedicKritzBeamAbility(context);
    internal GameplayAbilityResult ExecuteMedicKritzHealNeedlesAbility(GameplayAbilityContext context) => Abilities.ExecuteMedicKritzHealNeedlesAbility(context);
    internal GameplayAbilityResult ExecuteMedicNeedlegunAbility(GameplayAbilityContext context) => Abilities.ExecuteMedicNeedlegunAbility(context);
    internal GameplayAbilityResult ExecuteMedicUberAbility(GameplayAbilityContext context) => Abilities.ExecuteMedicUberAbility(context);
    internal GameplayPrimaryWeaponResult ExecuteNeedlegunPrimaryWeapon(GameplayPrimaryWeaponContext context) => Abilities.ExecuteNeedlegunPrimaryWeapon(context);
    internal GameplayAbilityResult ExecutePyroAirblastAbility(GameplayAbilityContext context) => Abilities.ExecutePyroAirblastAbility(context);
    internal GameplayAbilityResult ExecuteQuoteBladeThrowAbility(GameplayAbilityContext context) => Abilities.ExecuteQuoteBladeThrowAbility(context);
    internal GameplayPrimaryWeaponResult ExecuteScoutNailgunPrimaryWeapon(GameplayPrimaryWeaponContext context) => Abilities.ExecuteScoutNailgunPrimaryWeapon(context);
    internal static GameplayAbilityResult ExecuteScoutNailgunToggleAbility(GameplayAbilityContext context) => GameplayAbilitySystem.ExecuteScoutNailgunToggleAbility(context);
    internal static GameplayAbilityResult ExecuteScoutTauntAbility(GameplayAbilityContext context) => GameplayAbilitySystem.ExecuteScoutTauntAbility(context);
    internal static GameplayAbilityResult ExecuteSniperBinocularsAbility(GameplayAbilityContext context) => GameplayAbilitySystem.ExecuteSniperBinocularsAbility(context);
    internal GameplayPrimaryWeaponResult ExecuteSniperBowPrimaryWeapon(GameplayPrimaryWeaponContext context) => Abilities.ExecuteSniperBowPrimaryWeapon(context);
    internal static GameplayAbilityResult ExecuteSniperBowToggleAbility(GameplayAbilityContext context) => GameplayAbilitySystem.ExecuteSniperBowToggleAbility(context);
    internal static GameplayAbilityResult ExecuteSniperScopeAbility(GameplayAbilityContext context) => GameplayAbilitySystem.ExecuteSniperScopeAbility(context);
    internal GameplayAbilityResult ExecuteSniperStrongDrinkAbility(GameplayAbilityContext context) => Abilities.ExecuteSniperStrongDrinkAbility(context);
    internal GameplayAbilityResult ExecuteSoldierBuffBannerAbility(GameplayAbilityContext context) => Abilities.ExecuteSoldierBuffBannerAbility(context);
    internal static GameplayAbilityResult ExecuteSoldierSecondaryToggleAbility(GameplayAbilityContext context) => GameplayAbilitySystem.ExecuteSoldierSecondaryToggleAbility(context);
    internal static GameplayAbilityResult ExecuteSpyCloakAbility(GameplayAbilityContext context) => GameplayAbilitySystem.ExecuteSpyCloakAbility(context);
    internal GameplayAbilityResult ExecuteSpySuperjumpAbility(GameplayAbilityContext context) => Abilities.ExecuteSpySuperjumpAbility(context);
    public Func<WorldGameplayAbilityEvent, bool>? GameplayAbilityInputInterceptor { get => Abilities.GameplayAbilityInputInterceptor; set => Abilities.GameplayAbilityInputInterceptor = value; }
    internal bool IsGameplayAbilityBlockedBySpecialAbilitiesSetting(GameplayAbilityDefinition ability) => Abilities.IsGameplayAbilityBlockedBySpecialAbilitiesSetting(ability);
    public IReadOnlyList<WorldGameplayAbilityEvent> PendingGameplayAbilityEvents => Abilities.PendingGameplayAbilityEvents;
    private static IEnumerable<GameplayItemDefinition> ResolveGameplayAbilityItems(PlayerEntity player, string channel) => GameplayAbilitySystem.ResolveGameplayAbilityItems(player, channel);
    public bool TryApplyGameplayDamage(int targetPlayerId, float amount, int? attackerPlayerId, string? weaponSpriteName) => Abilities.TryApplyGameplayDamage(targetPlayerId, amount, attackerPlayerId, weaponSpriteName);
    public bool TryApplyGameplayHealing(int playerId, float amount) => Abilities.TryApplyGameplayHealing(playerId, amount);
    public bool TryApplyGameplayImpulse(int playerId, float velocityX, float velocityY) => Abilities.TryApplyGameplayImpulse(playerId, velocityX, velocityY);
    public bool TryApplyGameplayStatusEffect(int playerId, string statusEffectId, int ticks, float value = 0f) => Abilities.TryApplyGameplayStatusEffect(playerId, statusEffectId, ticks, value);
    public bool TrySetGameplayAbilityCooldown(int playerId, string ownerId, string cooldownKey, int ticks) => Abilities.TrySetGameplayAbilityCooldown(playerId, ownerId, cooldownKey, ticks);
    public bool TrySpawnGameplayProjectile(GameplayProjectileSpawnRequest request, out int projectileId) => Abilities.TrySpawnGameplayProjectile(request, out projectileId);
}
