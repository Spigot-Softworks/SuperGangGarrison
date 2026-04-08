namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private sealed partial class WeaponFireHandler
    {
        private void FireBladeBubble(PlayerEntity attacker, float aimWorldX, float aimWorldY)
        {
            var weaponOrigin = GetSourceWeaponOrigin(attacker);
            var aimDeltaX = aimWorldX - weaponOrigin.BaseX;
            var aimDeltaY = aimWorldY - weaponOrigin.BaseY;
            if (aimDeltaX == 0f && aimDeltaY == 0f)
            {
                aimDeltaX = attacker.FacingDirectionX;
            }

            var directionRadians = MathF.Atan2(aimDeltaY, aimDeltaX);
            var directionX = MathF.Cos(directionRadians);
            var directionY = MathF.Sin(directionRadians);
            var bubbleSpeed = 10f;
            var (launchedVelocityX, launchedVelocityY) = _world.ApplyExperimentalProjectileSpeedMultiplier(
                attacker,
                directionX * bubbleSpeed,
                directionY * bubbleSpeed);
            SpawnBubble(
                attacker,
                weaponOrigin.BaseX + directionX * 8f,
                weaponOrigin.BaseY + directionY * 8f,
                launchedVelocityX + (attacker.HorizontalSpeed / LegacyMovementModel.SourceTicksPerSecond),
                launchedVelocityY + (attacker.VerticalSpeed / LegacyMovementModel.SourceTicksPerSecond));
        }

        public void FireMedicNeedle(PlayerEntity attacker, float aimWorldX, float aimWorldY)
        {
            RegisterSoundEvent(attacker, "MedichaingunSnd");
            FireMedicNeedle(attacker, GetSourceWeaponOrigin(attacker), aimWorldX, aimWorldY);
        }

        public void FireAcquiredMedicNeedle(PlayerEntity attacker, float aimWorldX, float aimWorldY)
        {
            RegisterSoundEvent(attacker, "MedichaingunSnd");
            FireMedicNeedle(attacker, GetSourceWeaponOrigin(attacker, PlayerClass.Medic), aimWorldX, aimWorldY);
        }

        private void FireMedicNeedle(
            PlayerEntity attacker,
            SourceWeaponOrigin weaponOrigin,
            float aimWorldX,
            float aimWorldY)
        {
            var shotOriginY = weaponOrigin.BaseY + weaponOrigin.WeaponYOffset + 1f;
            var aimDeltaX = aimWorldX - weaponOrigin.BaseX;
            var aimDeltaY = aimWorldY - shotOriginY;
            if (aimDeltaX == 0f && aimDeltaY == 0f)
            {
                aimDeltaX = attacker.FacingDirectionX;
            }

            var directionRadians = MathF.Atan2(aimDeltaY, aimDeltaX);
            var speed = 7f + (_random.NextSingle() * 3f);
            var (launchedVelocityX, launchedVelocityY) = _world.ApplyExperimentalProjectileSpeedMultiplier(
                attacker,
                MathF.Cos(directionRadians) * speed,
                MathF.Sin(directionRadians) * speed);
            SpawnNeedle(
                attacker,
                weaponOrigin.BaseX,
                shotOriginY,
                launchedVelocityX + (attacker.HorizontalSpeed * (float)Config.FixedDeltaSeconds),
                launchedVelocityY);
        }
    }
}
