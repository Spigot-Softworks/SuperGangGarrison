namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private sealed partial class WeaponFireHandler
    {
        private void FireRifle(PlayerEntity attacker, float aimWorldX, float aimWorldY)
        {
            FireRifle(attacker, attacker.ClassId, aimWorldX, aimWorldY, "RifleKL");
        }

        private void FireRifle(
            PlayerEntity attacker,
            PlayerClass weaponClassId,
            float aimWorldX,
            float aimWorldY,
            string killFeedWeaponSpriteNameOverride)
        {
            const float rifleDistance = 2000f;

            var weaponOrigin = GetSourceWeaponOrigin(attacker, weaponClassId);
            var aimDeltaX = aimWorldX - weaponOrigin.BaseX;
            var aimDeltaY = aimWorldY - weaponOrigin.BaseY;
            if (aimDeltaX == 0f && aimDeltaY == 0f)
            {
                aimDeltaX = attacker.FacingDirectionX;
            }

            var distance = MathF.Sqrt((aimDeltaX * aimDeltaX) + (aimDeltaY * aimDeltaY));
            if (distance <= 0.0001f)
            {
                return;
            }

            var directionX = aimDeltaX / distance;
            var directionY = aimDeltaY / distance;
            var result = ResolveRifleHit(attacker, weaponOrigin.BaseX, weaponOrigin.BaseY, directionX, directionY, rifleDistance);
            RegisterCombatTrace(weaponOrigin.BaseX, weaponOrigin.BaseY, directionX, directionY, result.Distance, result.HitPlayer is not null, attacker.Team, isSniperTracer: true);
            var damage = attacker.GetSniperRifleDamage();
            if (result.HitPlayer is not null)
            {
                RegisterBloodEffect(result.HitPlayer.X, result.HitPlayer.Y, PointDirectionDegrees(weaponOrigin.BaseX, weaponOrigin.BaseY, result.HitPlayer.X, result.HitPlayer.Y) - 180f);
                if (ApplyPlayerDamage(result.HitPlayer, damage, attacker, PlayerEntity.SpySniperRevealAlpha))
                {
                    var deadBodyAnimationKind = damage > PlayerEntity.SniperBaseDamage
                        ? DeadBodyAnimationKind.Severe
                        : DeadBodyAnimationKind.Rifle;
                    KillPlayer(result.HitPlayer, killer: attacker, weaponSpriteName: killFeedWeaponSpriteNameOverride, deadBodyAnimationKind: deadBodyAnimationKind);
                }
            }
            else if (result.HitSentry is not null && ApplySentryDamage(result.HitSentry, damage, attacker))
            {
                DestroySentry(result.HitSentry, attacker);
            }
            else if (result.HitGenerator is not null)
            {
                TryDamageGenerator(result.HitGenerator.Team, damage, attacker);
            }
            else if (result.Distance < rifleDistance)
            {
                RegisterImpactEffect(
                    weaponOrigin.BaseX + directionX * result.Distance,
                    weaponOrigin.BaseY + directionY * result.Distance,
                    PointDirectionDegrees(0f, 0f, directionX, directionY));
            }
        }
    }
}
