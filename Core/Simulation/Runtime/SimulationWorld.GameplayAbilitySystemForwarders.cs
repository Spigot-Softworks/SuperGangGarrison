using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

// Thin forwarders so remaining world code keeps its call shape; callers should migrate to the system directly over time.
public sealed partial class SimulationWorld
{
    public IReadOnlyList<WorldGameplayAbilityEvent> DrainPendingGameplayAbilityEvents() => Abilities.DrainPendingGameplayAbilityEvents();
    public Func<WorldGameplayAbilityEvent, bool>? GameplayAbilityInputInterceptor { get => Abilities.GameplayAbilityInputInterceptor; set => Abilities.GameplayAbilityInputInterceptor = value; }
    public IReadOnlyList<WorldGameplayAbilityEvent> PendingGameplayAbilityEvents => Abilities.PendingGameplayAbilityEvents;
    public bool TryApplyGameplayDamage(int targetPlayerId, float amount, int? attackerPlayerId, string? weaponSpriteName) => Abilities.TryApplyGameplayDamage(targetPlayerId, amount, attackerPlayerId, weaponSpriteName);
    public bool TryApplyGameplayHealing(int playerId, float amount) => Abilities.TryApplyGameplayHealing(playerId, amount);
    public bool TryApplyGameplayImpulse(int playerId, float velocityX, float velocityY) => Abilities.TryApplyGameplayImpulse(playerId, velocityX, velocityY);
    public bool TryApplyGameplayStatusEffect(int playerId, string statusEffectId, int ticks, float value = 0f) => Abilities.TryApplyGameplayStatusEffect(playerId, statusEffectId, ticks, value);
    public bool TrySetGameplayAbilityCooldown(int playerId, string ownerId, string cooldownKey, int ticks) => Abilities.TrySetGameplayAbilityCooldown(playerId, ownerId, cooldownKey, ticks);
    public bool TrySpawnGameplayProjectile(GameplayProjectileSpawnRequest request, out int projectileId) => Abilities.TrySpawnGameplayProjectile(request, out projectileId);
}
