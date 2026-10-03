using OpenGarrison.Core.LastToDie;

namespace OpenGarrison.Core;

internal sealed partial class LastToDieRulesSystem
{
    internal bool TryExplodeLastToDieSniperArrow(
        ArrowProjectileEntity arrow,
        float? explosionX = null,
        float? explosionY = null)
    {
        if (!arrow.TryConsumeLastToDieExplosiveTip())
        {
            return false;
        }

        var owner = _host.FindPlayerById(arrow.OwnerId);
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

    internal bool TryExplodeLastToDieSniperRifleImpact(
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
        var blastRadius = CombatSystem.ResolveExplosiveSplashRadius(
            LastToDieSniperProfile.ExplosiveTipBlastRadius
                * MathF.Max(0.1f, owner?.LastToDieUniversalModifiers.ExplosionScale ?? 1f));
        _host.RegisterWorldSoundEvent("ExplosionSnd", x, y, ownerId);
        _host.RegisterVisualEffect("Explosion", x, y, 0f, 1, true);
        if (_host.ClientPredictionMode)
        {
            return true;
        }

        var players = _host.EnumerateSimulatedPlayers().ToArray();
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

            var distance = GetExplosionDistanceToPlayer(target, x, y);
            if (distance > blastRadius
                || !_host.GeometryResolver.HasObstacleLineOfSight(x, y, target.X, target.Y))
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

            damage = MathF.Max(CombatSystem.ExplosiveSplashMinimumDamage, damage);

            var resolution = _host.Combat.ResolvePlayerDamage(
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
                _host.PlayerDeaths.KillPlayer(target, gibbed: true, killer: owner, weaponSpriteName: fatalWeaponSpriteName);
            }
        }

        return true;
    }

    internal bool DetonateOwnedLastToDieSniperArrows(PlayerEntity owner)
    {
        var detonated = false;
        for (var needleIndex = _host.Needles.Count - 1; needleIndex >= 0; needleIndex -= 1)
        {
            if (_host.Needles[needleIndex] is not ArrowProjectileEntity
                {
                    IsLastToDieExplosiveTipArmed: true,
                } arrow
                || arrow.OwnerId != owner.Id)
            {
                continue;
            }

            detonated |= TryExplodeLastToDieSniperArrow(arrow);
            arrow.Destroy();
            _host.RemoveNeedleAt(needleIndex);
        }

        return detonated;
    }

    internal bool HasOwnedLastToDieSniperExplosiveArrow(PlayerEntity owner)
        => CountOwnedLastToDieSniperExplosiveArrows(owner) > 0;

    internal int CountOwnedLastToDieSniperExplosiveArrows(PlayerEntity owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return _host.Needles.Count(needle => needle is ArrowProjectileEntity
        {
            IsLastToDieExplosiveTipArmed: true,
        } arrow && arrow.OwnerId == owner.Id);
    }
}
