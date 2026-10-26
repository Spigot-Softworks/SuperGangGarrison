using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

internal sealed partial class GameplayAbilitySystem
{
    public bool TryApplyGameplayImpulse(int playerId, float velocityX, float velocityY)
        => _host.Movement.TryApplyGameplayImpulse(playerId, velocityX, velocityY);

    public bool TrySetGameplayVelocity(int playerId, float? velocityX, float? velocityY)
        => _host.Movement.TrySetGameplayVelocity(playerId, velocityX, velocityY);

    // Sound event names travel as strings of at most 64 UTF-8 bytes.
    private const int MaxSoundNameBytes = 64;

    public bool TryPlayGameplaySound(int playerId, string soundName)
    {
        var player = _host.FindPlayerById(playerId);
        if (player is null
            || string.IsNullOrWhiteSpace(soundName)
            || System.Text.Encoding.UTF8.GetByteCount(soundName.Trim()) > MaxSoundNameBytes)
        {
            return false;
        }

        _host.WorldEffects.RegisterWorldSoundEvent(soundName.Trim(), player.X, player.Y, player.Id);
        return true;
    }

    // A blood spray on a player, the effect weapon hits make; directionDegrees is where it flies.
    public bool TrySpawnGameplayBlood(int playerId, float directionDegrees, int count)
    {
        var player = _host.FindPlayerById(playerId);
        if (player is null || !float.IsFinite(directionDegrees))
        {
            return false;
        }

        _host.WorldEffects.RegisterBloodEffect(player.X, player.Y, directionDegrees, Math.Clamp(count, 1, 8), player.Team);
        return true;
    }

    // Plays a pack sprite once at a world position on every client (hit sparks and the like),
    // rotated by rotationDegrees and optionally mirrored, one frame every ticksPerFrame.
    public bool TrySpawnGameplaySpriteEffect(string spriteName, float x, float y, float rotationDegrees, bool flipX, int ticksPerFrame)
    {
        if (string.IsNullOrWhiteSpace(spriteName)
            || !float.IsFinite(x)
            || !float.IsFinite(y)
            || !float.IsFinite(rotationDegrees))
        {
            return false;
        }

        var effectName = GameplayVisualEffects.SpritePrefix + spriteName.Trim();
        if (System.Text.Encoding.UTF8.GetByteCount(effectName) > GameplayVisualEffects.MaxEffectNameBytes)
        {
            return false;
        }

        _host.PresentationEvents.AddVisualEvent(new WorldVisualEvent(
            effectName,
            x,
            y,
            SimulationMath.NormalizeAngleDegrees(rotationDegrees),
            GameplayVisualEffects.EncodeSpriteCount(ticksPerFrame, flipX),
            SourceFrame: _host.Frame < 0 ? 0UL : (ulong)_host.Frame));
        return true;
    }

    // Covers every client's view with a known screen effect (time_stop) centred on x, y.
    public bool TryPlayGameplayScreenEffect(string effect, float x, float y, int durationTicks)
    {
        if (string.IsNullOrWhiteSpace(effect)
            || !GameplayVisualEffects.IsKnownScreenEffect(effect.Trim())
            || !float.IsFinite(x)
            || !float.IsFinite(y)
            || durationTicks <= 0)
        {
            return false;
        }

        _host.PresentationEvents.AddVisualEvent(new WorldVisualEvent(
            GameplayVisualEffects.ScreenPrefix + effect.Trim(),
            x,
            y,
            0f,
            Math.Min(durationTicks, GameplayVisualEffects.MaxScreenEffectTicks),
            SourceFrame: _host.Frame < 0 ? 0UL : (ulong)_host.Frame));
        return true;
    }

    // Moves a player straight toward x, y, as far as they fit: walls, gates and player walls stop
    // them short, so a plugin can't put anyone inside the level. False when they couldn't move.
    public bool TryTeleportGameplayPlayer(int playerId, float x, float y)
    {
        var player = _host.FindPlayerById(playerId);
        if (player is null || !player.IsAlive || !float.IsFinite(x) || !float.IsFinite(y))
        {
            return false;
        }

        // Each step may rise up to maxStepUpPixels over the line (slopes and lips in the floor, which
        // would otherwise stop a teleport along the ground before it starts), and keeps that rise.
        const float stepPixels = 4f;
        const int maxStepUpPixels = 8;
        var deltaX = x - player.X;
        var deltaY = y - player.Y;
        var distance = MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
        if (distance < 0.01f)
        {
            return true;
        }

        var steps = (int)MathF.Ceiling(distance / stepPixels);
        var reachedX = player.X;
        var reachedY = player.Y;
        var rise = 0f;
        for (var step = 1; step <= steps; step += 1)
        {
            var fraction = MathF.Min(1f, step * stepPixels / distance);
            var nextX = player.X + (deltaX * fraction);
            var nextY = player.Y + (deltaY * fraction) - rise;
            var fits = false;
            for (var up = 0; up <= maxStepUpPixels; up += 1)
            {
                if (player.CanOccupy(_host.Level, player.Team, nextX, nextY - up))
                {
                    rise += up;
                    nextY -= up;
                    fits = true;
                    break;
                }
            }

            if (!fits)
            {
                break;
            }

            reachedX = nextX;
            reachedY = nextY;
        }

        if (reachedX == player.X && reachedY == player.Y)
        {
            return false;
        }

        player.TeleportTo(reachedX, reachedY);
        return true;
    }

    // Holds every projectile within radius of x, y still for ticks (a time stop).
    // travelPixels: blades thrown into the zone fly this far before stopping.
    public bool TryFreezeGameplayProjectiles(float x, float y, float radius, int ticks, float travelPixels = 0f)
    {
        if (!float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(radius) || radius <= 0f || ticks <= 0)
        {
            return false;
        }

        _host.Projectiles.AddProjectileFreezeZone(x, y, radius, ticks, travelPixels);
        return true;
    }

    public bool TrySetGameplayPose(int playerId, string? suffix, int ticksPerFrame, bool loop, int durationTicks)
    {
        var player = _host.FindPlayerById(playerId);
        return player is not null && GameplayPlayerPoses.TrySet(player, suffix, ticksPerFrame, loop, durationTicks);
    }

    public bool TrySetGameplayAbilityCooldown(int playerId, string ownerId, string cooldownKey, int ticks)
    {
        var player = _host.FindPlayerById(playerId);
        return player is not null
            && player.SetGameplayAbilityCooldownReplicatedState(ownerId, cooldownKey, ticks);
    }

    // silent heals without the healing event (no particles or numbers): a passive regen.
    public bool TryApplyGameplayHealing(int playerId, float amount, bool silent = false)
    {
        var player = _host.FindPlayerById(playerId);
        if (player is null || !player.IsAlive)
        {
            return false;
        }

        return silent
            ? player.ApplyContinuousHealingAndGetAmount(MathF.Max(0f, amount)) > 0
            : _host.DamageRules.ApplyHealingWithFeedback(player, MathF.Max(0f, amount)) > 0;
    }

    // deathType (GameplayDeathTypes) says how a kill leaves the body: gibs, say.
    public bool TryApplyGameplayDamage(int targetPlayerId, float amount, int? attackerPlayerId, string? weaponSpriteName, string? deathType = null)
    {
        var target = _host.FindPlayerById(targetPlayerId);
        if (target is null || !target.IsAlive || amount <= 0f)
        {
            return false;
        }

        var attacker = attackerPlayerId.HasValue ? _host.FindPlayerById(attackerPlayerId.Value) : null;
        if (attacker is not null && !_host.DamageRules.CanTeamDamagePlayer(attacker.Team, attacker.Id, target))
        {
            return false;
        }

        if (_host.ApplyPlayerContinuousDamage(target, amount, attacker, PlayerEntity.SpyDamageRevealAlpha))
        {
            GameplayDeathTypes.Resolve(deathType, out var gibbed, out var animationKind);
            _host.PlayerDeaths.KillPlayer(
                target,
                gibbed: gibbed,
                killer: attacker,
                weaponSpriteName: string.IsNullOrWhiteSpace(weaponSpriteName) ? null : weaponSpriteName.Trim(),
                deadBodyAnimationKind: animationKind);
        }

        return true;
    }

    public bool TryApplyGameplayStatusEffect(int playerId, string statusEffectId, int ticks, float value = 0f)
    {
        var player = _host.FindPlayerById(playerId);
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
            case "stun":
                // Hitstun: the player's input is ignored for the ticks (never shortens a stun).
                player.RefreshServerStunTicks(clampedTicks);
                return true;
            case "facing_lock":
                // The player keeps facing the way they do now for the ticks (0 ends it).
                player.SetFacingLockTicks(clampedTicks);
                return true;
            case "time_stop":
                // Frozen in a time stop: nothing heals the player for the ticks (0 ends it).
                player.SetTimeStopTicks(clampedTicks);
                return true;
            case "damage_taken_scale":
                // Scales all damage the player takes (below 1 is a resistance) for the ticks.
                player.SetPluginDamageTakenScale(value > 0f ? value : 1f, _host.Frame + clampedTicks);
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

        var owner = _host.FindPlayerById(request.OwnerPlayerId);
        if (owner is null || !owner.IsAlive)
        {
            return false;
        }

        projectileId = _host.EntityStore.NextEntityId;
        var kind = request.Kind.Trim();
        switch (kind)
        {
            case GameplayProjectileKinds.Shot:
                _host.SpawnShot(
                    owner,
                    request.X,
                    request.Y,
                    request.VelocityX,
                    request.VelocityY,
                    request.Damage > 0f ? request.Damage : ShotProjectileEntity.DamagePerHit,
                    killFeedWeaponSpriteNameOverride: request.KillFeedWeaponSpriteName);
                return true;
            case GameplayProjectileKinds.Needle:
                _host.SpawnNeedle(owner, request.X, request.Y, request.VelocityX, request.VelocityY);
                return true;
            case GameplayProjectileKinds.Revolver:
                _host.SpawnRevolverShot(
                    owner,
                    request.X,
                    request.Y,
                    request.VelocityX,
                    request.VelocityY,
                    request.Damage > 0f ? request.Damage : RevolverProjectileEntity.DamagePerHit,
                    request.KillFeedWeaponSpriteName);
                return true;
            case GameplayProjectileKinds.Rocket:
                _host.SpawnRocket(
                    owner,
                    request.X,
                    request.Y,
                    request.Speed,
                    request.DirectionRadians,
                    killFeedWeaponSpriteNameOverride: request.KillFeedWeaponSpriteName);
                return true;
            case GameplayProjectileKinds.Mine:
                _host.Projectiles.SpawnMine(owner, request.X, request.Y, request.VelocityX, request.VelocityY, request.KillFeedWeaponSpriteName);
                return true;
            case GameplayProjectileKinds.Grenade:
                _host.Projectiles.SpawnGrenade(owner, request.X, request.Y, request.VelocityX, request.VelocityY, request.KillFeedWeaponSpriteName);
                return true;
            case GameplayProjectileKinds.Flame:
                _host.SpawnFlame(owner, request.X, request.Y, request.VelocityX, request.VelocityY);
                return true;
            case GameplayProjectileKinds.Flare:
                _host.SpawnFlare(owner, request.X, request.Y, request.VelocityX, request.VelocityY);
                return true;
            case GameplayProjectileKinds.Bubble:
                _host.Projectiles.SpawnBubble(owner, request.X, request.Y, request.VelocityX, request.VelocityY);
                return true;
            case GameplayProjectileKinds.Blade:
                _host.Projectiles.SpawnBlade(
                    owner,
                    request.X,
                    request.Y,
                    request.VelocityX,
                    request.VelocityY,
                    request.Damage > 0f ? (int)MathF.Round(request.Damage) : 3,
                    request.LifetimeTicks > 0 ? request.LifetimeTicks : PlayerEntity.QuoteBladeLifetimeTicks,
                    float.IsFinite(request.KnockbackScale) ? Math.Max(0f, request.KnockbackScale) : 1f,
                    request.DeathType);
                return true;
            default:
                projectileId = 0;
                return false;
        }
    }
}
