using OpenGarrison.Core.LastToDie;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private enum SpyStabTargetKind : byte
    {
        None = 0,
        Obstruction = 1,
        HostilePlayer = 2,
        HostileSentry = 3,
        DamageableZone = 4,
        FriendlyPlayer = 5,
    }

    private readonly record struct SpyStabTarget(
        SpyStabTargetKind Kind,
        ShotHitResult Hit);

    private void AdvanceStabAnimations()
    {
        for (var animationIndex = _stabAnimations.Count - 1; animationIndex >= 0; animationIndex -= 1)
        {
            var animation = _stabAnimations[animationIndex];
            var owner = FindPlayerById(animation.OwnerId);
            if (owner is null || !owner.IsAlive || owner.ClassId != PlayerClass.Spy)
            {
                RemoveStabAnimationAt(animationIndex);
                continue;
            }

            animation.AdvanceOneTick(owner.X, owner.Y);
            if (animation.IsExpired)
            {
                RemoveStabAnimationAt(animationIndex);
            }
        }
    }

    private void AdvanceStabMasks()
    {
        for (var maskIndex = _stabMasks.Count - 1; maskIndex >= 0; maskIndex -= 1)
        {
            var mask = _stabMasks[maskIndex];
            var owner = FindPlayerById(mask.OwnerId);
            if (owner is null || !owner.IsAlive || owner.ClassId != PlayerClass.Spy)
            {
                RemoveStabMaskAt(maskIndex);
                continue;
            }

            mask.AdvanceOneTick(owner.X, owner.Y);
            var directionX = mask.FacingLeft ? -1f : 1f;
            const float directionY = 0f;
            var target = ResolveSpyStabTarget(mask, owner, directionX, directionY);
            if (target is { } resolvedTarget)
            {
                var hitResult = resolvedTarget.Hit;
                RegisterCombatTrace(
                    mask.X,
                    mask.Y,
                    directionX,
                    directionY,
                    hitResult.Distance,
                    resolvedTarget.Kind is SpyStabTargetKind.HostilePlayer or SpyStabTargetKind.FriendlyPlayer);
                if (resolvedTarget.Kind == SpyStabTargetKind.HostilePlayer
                    && hitResult.HitPlayer is { } hostilePlayer)
                {
                    var primaryApplied = ApplySpyBackstabDamage(
                        owner,
                        hostilePlayer,
                        mask.DirectionDegrees,
                        owner.LastToDieMultistabEnabled);
                    if (primaryApplied && owner.LastToDieMultistabEnabled)
                    {
                        ApplyLastToDieMultistab(owner, hostilePlayer, mask.DirectionDegrees);
                    }

                    if (primaryApplied && owner.LastToDieSpringLoadedEnabled)
                    {
                        owner.RestoreLastToDieSpyJumpBootCharges();
                    }
                }
                else if (resolvedTarget.Kind == SpyStabTargetKind.FriendlyPlayer
                    && hitResult.HitPlayer is { } friendlyPlayer)
                {
                    ApplyHealingWithFeedback(
                        friendlyPlayer,
                        LastToDieDerivedModifiers.SpyHealstabHealing,
                        "HealSnd",
                        friendlyPlayer.X,
                        friendlyPlayer.Y);
                }
                else if (resolvedTarget.Kind == SpyStabTargetKind.HostileSentry
                    && hitResult.HitSentry is not null
                    && ApplySentryDamage(hitResult.HitSentry, StabMaskEntity.DamagePerHit, owner))
                {
                    DestroySentry(hitResult.HitSentry, owner);
                }
                else if (resolvedTarget.Kind == SpyStabTargetKind.DamageableZone)
                {
                    TryApplyDamageableZoneDamage(hitResult.HitDamageableZoneRoomObjectIndex, StabMaskEntity.DamagePerHit, mask.Team);
                }

                mask.Destroy();
            }

            if (mask.IsExpired)
            {
                RegisterImpactEffect(
                    mask.X + directionX * 15f,
                    mask.Y - 12f,
                    mask.DirectionDegrees);
                RemoveStabMaskAt(maskIndex);
            }
        }
    }

    private SpyStabTarget? ResolveSpyStabTarget(
        StabMaskEntity mask,
        PlayerEntity owner,
        float directionX,
        float directionY)
    {
        var hostileHit = GetNearestStabHit(mask, directionX, directionY);
        if (hostileHit is { } hostile)
        {
            if (hostile.HitPlayer is not null)
            {
                return new SpyStabTarget(SpyStabTargetKind.HostilePlayer, hostile);
            }

            if (hostile.HitSentry is not null)
            {
                return new SpyStabTarget(SpyStabTargetKind.HostileSentry, hostile);
            }

            if (hostile.HitDamageableZoneRoomObjectIndex >= 0)
            {
                return new SpyStabTarget(SpyStabTargetKind.DamageableZone, hostile);
            }
        }

        if (owner.LastToDieHealstabEnabled
            && GetNearestHealstabHit(mask, directionX, directionY) is { } friendlyHit
            && friendlyHit.HitPlayer is not null)
        {
            return new SpyStabTarget(SpyStabTargetKind.FriendlyPlayer, friendlyHit);
        }

        return hostileHit is { } obstruction
            ? new SpyStabTarget(SpyStabTargetKind.Obstruction, obstruction)
            : null;
    }

    private bool ApplySpyBackstabDamage(
        PlayerEntity owner,
        PlayerEntity target,
        float directionDegrees,
        bool removeDamageCap)
    {
        RegisterBloodEffect(target.X, target.Y, directionDegrees - 180f, 6);
        var damage = removeDamageCap
            ? Math.Max(1, target.Health)
            : StabMaskEntity.DamagePerHit;
        var resolution = ResolvePlayerDamage(
            target,
            new PlayerDamageRequest(
                PlayerDamageApplicationKind.Instant,
                damage,
                owner,
                PlayerEntity.SpyDamageRevealAlpha,
                DamageEventFlags.None,
                PlayerDamageTraits.CanEvade
                    | PlayerDamageTraits.CanApplyOnHitEffects
                    | PlayerDamageTraits.CanReflect
                    | PlayerDamageTraits.Melee,
                AllowOsmosisHealOwnedSentries: true,
                new PlayerDamageUmbrellaOptions(AllowBlock: false),
                AttackerWasGrounded: owner.IsGrounded,
                TargetWasGrounded: target.IsGrounded));
        if (resolution.WasFatal)
        {
            KillPlayer(
                target,
                killer: owner,
                weaponSpriteName: "KnifeKL",
                deadBodyAnimationKind: DeadBodyAnimationKind.Severe);
        }

        return resolution.AppliedHealthDamage > 0;
    }

    private void ApplyLastToDieMultistab(
        PlayerEntity owner,
        PlayerEntity primaryTarget,
        float directionDegrees)
    {
        var radiusSquared = LastToDieDerivedModifiers.SpyMultistabRadius
            * LastToDieDerivedModifiers.SpyMultistabRadius;
        foreach (var candidate in EnumerateSimulatedPlayers())
        {
            if (ReferenceEquals(candidate, primaryTarget)
                || candidate.Id == owner.Id
                || !candidate.IsAlive
                || candidate.Team != primaryTarget.Team
                || !CanTeamDamagePlayer(owner.Team, owner.Id, candidate))
            {
                continue;
            }

            var deltaX = candidate.X - primaryTarget.X;
            var deltaY = candidate.Y - primaryTarget.Y;
            if ((deltaX * deltaX) + (deltaY * deltaY) > radiusSquared
                || !HasStabChainLineOfSight(primaryTarget.X, primaryTarget.Y, candidate.X, candidate.Y))
            {
                continue;
            }

            ApplySpyBackstabDamage(owner, candidate, directionDegrees, removeDamageCap: true);
        }
    }

    private void RemoveStabAnimationAt(int animationIndex)
    {
        var animation = _stabAnimations[animationIndex];
        _entities.Remove(animation.Id);
        _stabAnimations.RemoveAt(animationIndex);
    }

    private void RemoveStabMaskAt(int maskIndex)
    {
        var mask = _stabMasks[maskIndex];
        _entities.Remove(mask.Id);
        _stabMasks.RemoveAt(maskIndex);
    }

    private void RemoveOwnedSpyArtifacts(int ownerId)
    {
        for (var animationIndex = _stabAnimations.Count - 1; animationIndex >= 0; animationIndex -= 1)
        {
            if (_stabAnimations[animationIndex].OwnerId == ownerId)
            {
                RemoveStabAnimationAt(animationIndex);
            }
        }

        for (var maskIndex = _stabMasks.Count - 1; maskIndex >= 0; maskIndex -= 1)
        {
            if (_stabMasks[maskIndex].OwnerId == ownerId)
            {
                RemoveStabMaskAt(maskIndex);
            }
        }
    }
}
