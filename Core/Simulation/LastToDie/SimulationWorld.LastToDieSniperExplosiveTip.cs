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

        var owner = FindPlayerById(arrow.OwnerId);
        return TryExplodeLastToDieSniperImpact(
            arrow.OwnerId,
            arrow.Team,
            owner,
            explosionX ?? arrow.X,
            explosionY ?? arrow.Y,
            arrow.Id,
            unchecked((ulong)(uint)arrow.Id),
            arrow.IsCritical,
            arrow.CriticalDamageMultiplier,
            "BowKL");
    }

    private bool TryExplodeLastToDieSniperRifleImpact(
        PlayerEntity owner,
        float x,
        float y,
        bool isCritical,
        float criticalDamageMultiplier)
    {
        return TryExplodeLastToDieSniperImpact(
            owner.Id,
            owner.Team,
            owner,
            x,
            y,
            owner.Id,
            unchecked((ulong)(uint)owner.Id),
            isCritical,
            criticalDamageMultiplier,
            "RifleKL");
    }

    private bool TryExplodeLastToDieSniperImpact(
        int ownerId,
        PlayerTeam ownerTeam,
        PlayerEntity? owner,
        float x,
        float y,
        int sourceEntityId,
        ulong attackId,
        bool isCritical,
        float criticalDamageMultiplier,
        string fatalWeaponSpriteName)
    {
        var blastRadius = ResolveExplosiveSplashRadius(
            LastToDieSniperProfile.ExplosiveTipBlastRadius
                * MathF.Max(0.1f, owner?.LastToDieUniversalModifiers.ExplosionScale ?? 1f));
        RegisterWorldSoundEvent("ExplosionSnd", x, y, ownerId);
        RegisterVisualEffect("Explosion", x, y);
        if (ClientPredictionMode)
        {
            return true;
        }

        var players = EnumerateSimulatedPlayers().ToArray();
        var hitPlayerIds = new HashSet<int>();
        foreach (var target in players)
        {
            if (!target.IsAlive || !hitPlayerIds.Add(target.Id))
            {
                continue;
            }

            var isSelf = target.Id == ownerId && target.Team == ownerTeam;
            if (!isSelf && target.Team == ownerTeam)
            {
                continue;
            }

            var distance = GetExplosionDistanceToPlayer(this, target, x, y);
            if (distance > blastRadius
                || !HasObstacleLineOfSight(x, y, target.X, target.Y))
            {
                continue;
            }

            var distanceFraction = Math.Clamp(
                distance / blastRadius,
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
            if (isCritical)
            {
                damage *= criticalDamageMultiplier;
                traits |= PlayerDamageTraits.Critical;
            }

            damage = MathF.Max(ExplosiveSplashMinimumDamage, damage);

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
                        CriticalBoost: isCritical,
                        UseLiveAttackerCriticalBoost: false),
                    SourceEntityId: sourceEntityId,
                    AttackId: attackId,
                    AttackerWasGrounded: owner?.IsGrounded,
                    TargetWasGrounded: target.IsGrounded,
                    GibOnFatal: true,
                    FatalWeaponSpriteName: fatalWeaponSpriteName));
            if (resolution.WasFatal)
            {
                KillPlayer(target, gibbed: true, killer: owner, weaponSpriteName: fatalWeaponSpriteName);
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
