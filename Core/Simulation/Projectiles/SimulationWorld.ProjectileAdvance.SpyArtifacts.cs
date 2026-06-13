namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
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
            var hit = GetNearestStabHit(mask, directionX, directionY);
            if (hit.HasValue)
            {
                var hitResult = hit.Value;
                RegisterCombatTrace(mask.X, mask.Y, directionX, directionY, hitResult.Distance, hitResult.HitPlayer is not null);
                if (hitResult.HitPlayer is not null)
                {
                    RegisterBloodEffect(hitResult.HitPlayer.X, hitResult.HitPlayer.Y, mask.DirectionDegrees - 180f, 6);
                    if (ApplyPlayerDamage(
                            hitResult.HitPlayer,
                            StabMaskEntity.DamagePerHit,
                            owner,
                            PlayerEntity.SpyDamageRevealAlpha,
                            allowCivvieUmbrellaShield: false))
                    {
                        KillPlayer(hitResult.HitPlayer, killer: owner, weaponSpriteName: "KnifeKL", deadBodyAnimationKind: DeadBodyAnimationKind.Severe);
                    }
                }
                else if (hitResult.HitSentry is not null && ApplySentryDamage(hitResult.HitSentry, StabMaskEntity.DamagePerHit, owner))
                {
                    DestroySentry(hitResult.HitSentry, owner);
                }
                else if (hitResult.HitDamageableZoneRoomObjectIndex >= 0)
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
