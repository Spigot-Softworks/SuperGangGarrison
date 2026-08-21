using OpenGarrison.Core.LastToDie;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private bool TryExplodeLastToDieSniperArrow(
        ArrowProjectileEntity arrow,
        float? explosionX = null,
        float? explosionY = null)
    {
        if (!arrow.TryConsumeLastToDieExplosiveTip())
        {
            return false;
        }

        var x = explosionX ?? arrow.X;
        var y = explosionY ?? arrow.Y;
        RegisterWorldSoundEvent("ExplosionSnd", x, y, arrow.OwnerId);
        RegisterVisualEffect("Explosion", x, y);
        if (ClientPredictionMode)
        {
            return true;
        }

        var owner = FindPlayerById(arrow.OwnerId);
        var players = EnumerateSimulatedPlayers().ToArray();
        var hitPlayerIds = new HashSet<int>();
        foreach (var target in players)
        {
            if (!target.IsAlive || !hitPlayerIds.Add(target.Id))
            {
                continue;
            }

            var isSelf = target.Id == arrow.OwnerId && target.Team == arrow.Team;
            if (!isSelf && target.Team == arrow.Team)
            {
                continue;
            }

            var distance = GetExplosionDistanceToPlayer(this, target, x, y);
            if (distance > LastToDieSniperProfile.ExplosiveTipBlastRadius
                || !HasObstacleLineOfSight(x, y, target.X, target.Y))
            {
                continue;
            }

            var distanceFraction = Math.Clamp(
                distance / LastToDieSniperProfile.ExplosiveTipBlastRadius,
                0f,
                1f);
            var damage = LastToDieSniperProfile.ExplosiveTipCenterDamage
                + ((LastToDieSniperProfile.ExplosiveTipEdgeDamage
                    - LastToDieSniperProfile.ExplosiveTipCenterDamage)
                    * distanceFraction);
            if (isSelf)
            {
                damage *= LastToDieSniperProfile.ExplosiveTipSelfDamageMultiplier;
            }

            var traits = PlayerDamageTraits.CanEvade
                | PlayerDamageTraits.CanApplyOnHitEffects
                | PlayerDamageTraits.CanReflect
                | PlayerDamageTraits.Explosive
                | PlayerDamageTraits.EstablishLastToDieSpotted
                | PlayerDamageTraits.BenefitFromLastToDieSpotted
                | PlayerDamageTraits.LastToDieOverkillerEligible;
            if (arrow.IsCritical)
            {
                damage *= arrow.CriticalDamageMultiplier;
                traits |= PlayerDamageTraits.Critical;
            }

            var resolution = ResolvePlayerDamage(
                target,
                new PlayerDamageRequest(
                    PlayerDamageApplicationKind.Instant,
                    Math.Max(1f, MathF.Round(damage)),
                    owner,
                    PlayerEntity.SpyDamageRevealAlpha,
                    DamageEventFlags.None,
                    traits,
                    AllowOsmosisHealOwnedSentries: true,
                    new PlayerDamageUmbrellaOptions(
                        AllowBlock: true,
                        ThreatSourceX: x,
                        ThreatSourceY: y,
                        CriticalBoost: arrow.IsCritical,
                        UseLiveAttackerCriticalBoost: false),
                    SourceEntityId: arrow.Id,
                    AttackId: unchecked((ulong)(uint)arrow.Id),
                    AttackerWasGrounded: owner?.IsGrounded,
                    TargetWasGrounded: target.IsGrounded,
                    GibOnFatal: true,
                    FatalWeaponSpriteName: "BowKL"));
            if (resolution.WasFatal)
            {
                KillPlayer(target, gibbed: true, killer: owner, weaponSpriteName: "BowKL");
            }
        }

        return true;
    }

    private bool DetonateOwnedLastToDieSniperArrows(PlayerEntity owner)
    {
        var detonated = false;
        for (var needleIndex = _needles.Count - 1; needleIndex >= 0; needleIndex -= 1)
        {
            if (_needles[needleIndex] is not ArrowProjectileEntity
                {
                    IsLastToDieExplosiveTipArmed: true,
                } arrow
                || arrow.OwnerId != owner.Id)
            {
                continue;
            }

            detonated |= TryExplodeLastToDieSniperArrow(arrow);
            arrow.Destroy();
            RemoveNeedleAt(needleIndex);
        }

        return detonated;
    }

    private bool HasOwnedLastToDieSniperExplosiveArrow(PlayerEntity owner)
        => CountOwnedLastToDieSniperExplosiveArrows(owner) > 0;

    public int CountOwnedLastToDieSniperExplosiveArrows(PlayerEntity owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return _needles.Count(needle => needle is ArrowProjectileEntity
        {
            IsLastToDieExplosiveTipArmed: true,
        } arrow && arrow.OwnerId == owner.Id);
    }
}
