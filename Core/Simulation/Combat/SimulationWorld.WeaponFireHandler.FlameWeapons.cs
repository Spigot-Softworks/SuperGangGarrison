namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private sealed partial class WeaponFireHandler
    {
        private bool FireFlamethrower(PlayerEntity attacker, float aimWorldX, float aimWorldY)
        {
            return FireFlamethrower(attacker, attacker.PrimaryWeapon, attacker.ClassId, aimWorldX, aimWorldY);
        }

        private bool FireFlamethrower(
            PlayerEntity attacker,
            PrimaryWeaponDefinition weaponDefinition,
            PlayerClass weaponClassId,
            float aimWorldX,
            float aimWorldY)
        {
            var weaponOrigin = GetSourceWeaponOrigin(attacker, weaponClassId);
            var sourceX = weaponOrigin.BaseX;
            var sourceY = GetPyroOriginY(weaponOrigin);
            var aimDeltaX = aimWorldX - weaponOrigin.BaseX;
            var aimDeltaY = aimWorldY - sourceY;
            if (aimDeltaX == 0f && aimDeltaY == 0f)
            {
                aimDeltaX = attacker.FacingDirectionX;
            }

            var baseAngle = MathF.Atan2(aimDeltaY, aimDeltaX);
            var directionX = MathF.Cos(baseAngle);
            var directionY = MathF.Sin(baseAngle);
            var spawnX = sourceX + directionX * 25f;
            var spawnY = sourceY + directionY * 25f;
            if (IsFlameSpawnBlocked(sourceX, sourceY, spawnX, spawnY, attacker.Team))
            {
                return false;
            }

            var spreadSign = MathF.Sign((_random.NextSingle() * 2f) - 1f);
            var spreadDegrees = spreadSign * MathF.Pow(_random.NextSingle() * 3f, 1.8f);
            var maxRunSpeed = MathF.Max(0.0001f, attacker.MaxRunSpeed);
            spreadDegrees *= 1f - (attacker.HorizontalSpeed / maxRunSpeed);
            var flameAngle = baseAngle + DegreesToRadians(spreadDegrees);
            var flameSpeed = 6.5f + (_random.NextSingle() * 3.5f);
            var (launchedVelocityX, launchedVelocityY) = _world.ApplyExperimentalProjectileSpeedMultiplier(
                attacker,
                MathF.Cos(flameAngle) * flameSpeed,
                MathF.Sin(flameAngle) * flameSpeed);
            SpawnFlame(
                attacker,
                spawnX,
                spawnY,
                launchedVelocityX + (attacker.HorizontalSpeed / LegacyMovementModel.SourceTicksPerSecond),
                launchedVelocityY + (attacker.VerticalSpeed / LegacyMovementModel.SourceTicksPerSecond));
            return true;
        }
    }
}
