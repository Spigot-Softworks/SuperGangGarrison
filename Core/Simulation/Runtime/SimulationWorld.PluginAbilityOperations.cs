using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    public bool TryApplyGameplayImpulse(int playerId, float velocityX, float velocityY)
    {
        var player = FindPlayerById(playerId);
        if (player is null || !player.IsAlive)
        {
            return false;
        }

        player.ApplyVelocityImpulse(velocityX, velocityY);
        return true;
    }

    public bool TrySetGameplayAbilityCooldown(int playerId, string ownerId, string cooldownKey, int ticks)
    {
        var player = FindPlayerById(playerId);
        return player is not null
            && player.SetReplicatedStateInt(ownerId, cooldownKey, Math.Max(0, ticks));
    }

    public bool TryApplyGameplayHealing(int playerId, float amount)
    {
        var player = FindPlayerById(playerId);
        return player is not null
            && player.IsAlive
            && ApplyHealingWithFeedback(player, MathF.Max(0f, amount)) > 0;
    }

    public bool TryApplyGameplayDamage(int targetPlayerId, float amount, int? attackerPlayerId, string? weaponSpriteName)
    {
        var target = FindPlayerById(targetPlayerId);
        if (target is null || !target.IsAlive || amount <= 0f)
        {
            return false;
        }

        var attacker = attackerPlayerId.HasValue ? FindPlayerById(attackerPlayerId.Value) : null;
        if (attacker is not null && !CanTeamDamagePlayer(attacker.Team, attacker.Id, target))
        {
            return false;
        }

        if (ApplyPlayerContinuousDamage(target, amount, attacker, PlayerEntity.SpyDamageRevealAlpha))
        {
            KillPlayer(target, killer: attacker, weaponSpriteName: string.IsNullOrWhiteSpace(weaponSpriteName) ? null : weaponSpriteName.Trim());
        }

        return true;
    }

    public bool TryApplyGameplayStatusEffect(int playerId, string statusEffectId, int ticks, float value = 0f)
    {
        var player = FindPlayerById(playerId);
        if (player is null || !player.IsAlive || string.IsNullOrWhiteSpace(statusEffectId))
        {
            return false;
        }

        var normalizedStatusEffectId = statusEffectId.Trim();
        var clampedTicks = Math.Max(0, ticks);
        switch (normalizedStatusEffectId)
        {
            case "uber":
                player.RefreshUber(clampedTicks);
                return true;
            case "kritz":
                player.RefreshKritzCritBoost(clampedTicks);
                return true;
            case "ghost_phase":
                player.StartExperimentalGhostPhase(clampedTicks);
                return true;
            case "afterburn":
                player.IgniteAfterburn(
                    ownerPlayerId: -1,
                    durationIncreaseSourceTicks: clampedTicks,
                    intensityIncrease: value > 0f ? value : 1f,
                    afterburnFalloff: false,
                    burnFalloffAmount: 0f);
                return true;
            case "clear_afterburn":
                player.ExtinguishAfterburn();
                return true;
            case "movement_boost":
                player.GrantExperimentalMovementBoost(clampedTicks, value > 0f ? value : 1f);
                return true;
            case "cryo_slow":
                player.RefreshExperimentalCryoSlow(clampedTicks, value > 0f ? value : 0.5f);
                return true;
            case "damage_taken_multiplier":
                player.RefreshExperimentalDamageTakenDebuff(clampedTicks, value > 0f ? value : 1f);
                return true;
            case "confusion":
                player.RefreshExperimentalConfusion(clampedTicks);
                return true;
            default:
                return false;
        }
    }

    public bool TrySpawnGameplayProjectile(GameplayProjectileSpawnRequest request, out int projectileId)
    {
        projectileId = 0;
        if (request is null || string.IsNullOrWhiteSpace(request.Kind))
        {
            return false;
        }

        var owner = FindPlayerById(request.OwnerPlayerId);
        if (owner is null || !owner.IsAlive)
        {
            return false;
        }

        projectileId = _nextEntityId;
        var kind = request.Kind.Trim();
        switch (kind)
        {
            case GameplayProjectileKinds.Shot:
                SpawnShot(
                    owner,
                    request.X,
                    request.Y,
                    request.VelocityX,
                    request.VelocityY,
                    request.Damage > 0f ? request.Damage : ShotProjectileEntity.DamagePerHit,
                    killFeedWeaponSpriteNameOverride: request.KillFeedWeaponSpriteName);
                return true;
            case GameplayProjectileKinds.Needle:
                SpawnNeedle(owner, request.X, request.Y, request.VelocityX, request.VelocityY);
                return true;
            case GameplayProjectileKinds.Revolver:
                SpawnRevolverShot(
                    owner,
                    request.X,
                    request.Y,
                    request.VelocityX,
                    request.VelocityY,
                    request.Damage > 0f ? request.Damage : RevolverProjectileEntity.DamagePerHit,
                    request.KillFeedWeaponSpriteName);
                return true;
            case GameplayProjectileKinds.Rocket:
                SpawnRocket(
                    owner,
                    request.X,
                    request.Y,
                    request.Speed,
                    request.DirectionRadians,
                    killFeedWeaponSpriteNameOverride: request.KillFeedWeaponSpriteName);
                return true;
            case GameplayProjectileKinds.Mine:
                SpawnMine(owner, request.X, request.Y, request.VelocityX, request.VelocityY, request.KillFeedWeaponSpriteName);
                return true;
            case GameplayProjectileKinds.Grenade:
                SpawnGrenade(owner, request.X, request.Y, request.VelocityX, request.VelocityY, request.KillFeedWeaponSpriteName);
                return true;
            case GameplayProjectileKinds.Flame:
                SpawnFlame(owner, request.X, request.Y, request.VelocityX, request.VelocityY);
                return true;
            case GameplayProjectileKinds.Flare:
                SpawnFlare(owner, request.X, request.Y, request.VelocityX, request.VelocityY);
                return true;
            case GameplayProjectileKinds.Bubble:
                SpawnBubble(owner, request.X, request.Y, request.VelocityX, request.VelocityY);
                return true;
            case GameplayProjectileKinds.Blade:
                SpawnBlade(
                    owner,
                    request.X,
                    request.Y,
                    request.VelocityX,
                    request.VelocityY,
                    request.Damage > 0f ? (int)MathF.Round(request.Damage) : 3);
                return true;
            default:
                projectileId = 0;
                return false;
        }
    }
}
