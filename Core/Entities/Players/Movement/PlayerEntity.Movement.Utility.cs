namespace OpenGarrison.Core;

public sealed partial class PlayerEntity
{
    private float GetMovementScale(PlayerInputSnapshot input)
    {
        if (IsHeavyEating || (IsTaunting && !IsRaging))
        {
            return 0f;
        }

        if (ClassId == PlayerClass.Spy && SpyBackstabVisualTicksRemaining > 0)
        {
            return 0f;
        }

        if (HasScopedSniperWeaponEquipped && IsSniperScoped)
        {
            return SniperScopedMoveScale;
        }

        if (ClassId == PlayerClass.Heavy && input.FirePrimary)
        {
            return HeavyPrimaryMoveScale;
        }

        return 1f;
    }

    private float GetJumpScale()
    {
        if (IsExperimentalDemoknightCharging)
        {
            return 0f;
        }

        if (HasScopedSniperWeaponEquipped && IsSniperScoped)
        {
            return SniperScopedJumpScale;
        }

        if (ClassId == PlayerClass.Spy && SpyBackstabVisualTicksRemaining > 0)
        {
            return 0f;
        }

        return 1f;
    }

    private void UpdateAimDirection(PlayerInputSnapshot input)
    {
        var aimDeltaX = input.AimWorldX - X;
        var aimDeltaY = input.AimWorldY - Y;
        if (MathF.Abs(aimDeltaX) <= 0.0001f && MathF.Abs(aimDeltaY) <= 0.0001f)
        {
            AimDirectionDegrees = FacingDirectionX < 0f ? 180f : 0f;
            return;
        }

        AimDirectionDegrees = NormalizeDegrees(MathF.Atan2(aimDeltaY, aimDeltaX) * (180f / MathF.PI));
    }

    private static float NormalizeDegrees(float degrees)
    {
        while (degrees < 0f)
        {
            degrees += 360f;
        }

        while (degrees >= 360f)
        {
            degrees -= 360f;
        }

        return degrees;
    }

    private static float GetSourceFacingDirectionX(float aimDirectionDegrees)
    {
        return NormalizeDegrees(aimDirectionDegrees + 270f) > 180f ? 1f : -1f;
    }

    private bool ShouldCancelGravityForSourceSpinjump(SimpleLevel level, PlayerTeam team, float gravityPerTick)
    {
        if (gravityPerTick <= 0f)
        {
            return false;
        }

        var horizontalDirection = MathF.Sign(HorizontalSpeed);
        if (horizontalDirection == 0f)
        {
            return false;
        }

        if (!DidSourceFacingSpinForHorizontalDirection(horizontalDirection))
        {
            return false;
        }

        if (!CanOccupy(level, team, X, Y))
        {
            return false;
        }

        if (CanOccupy(level, team, X + horizontalDirection, Y))
        {
            return false;
        }

        if (!CanOccupy(level, team, X, Y - gravityPerTick))
        {
            return false;
        }

        return CanOccupy(level, team, X, Y + 1f) || VerticalSpeed < 0f;
    }

    private bool DidSourceFacingSpinForHorizontalDirection(float horizontalDirection)
    {
        var currentSourceFacingDirectionX = GetSourceFacingDirectionX(AimDirectionDegrees);
        if (horizontalDirection > 0f)
        {
            return SourceFacingDirectionX > currentSourceFacingDirectionX;
        }

        return SourceFacingDirectionX < currentSourceFacingDirectionX;
    }

    private void AdvanceSourceFacingDirectionForNextStep()
    {
        PreviousSourceFacingDirectionX = SourceFacingDirectionX;
        SourceFacingDirectionX = GetSourceFacingDirectionX(AimDirectionDegrees);
    }

    private bool TryApplySourceStepDown(SimpleLevel level, PlayerTeam team)
    {
        if (VerticalSpeed != 0f || !CanOccupy(level, team, X, Y))
        {
            return false;
        }

        if (CanOccupy(level, team, X, Y + 6f) && !CanOccupy(level, team, X, Y + 7f))
        {
            Y += 6f;
            return true;
        }

        if (GetSourceMovementSpeedPerTick() > 6f
            && CanOccupy(level, team, X, Y + 12f)
            && !CanOccupy(level, team, X, Y + 13f))
        {
            Y += 12f;
            return true;
        }

        return false;
    }

    private float GetSourceMovementSpeedPerTick()
    {
        return MathF.Sqrt((HorizontalSpeed * HorizontalSpeed) + (VerticalSpeed * VerticalSpeed))
            / LegacyMovementModel.SourceTicksPerSecond;
    }

    private void ClampMovementSpeedsToMovementMaximum()
    {
        const float demoknightHorizontalClampMultiplier = 2f;
        const float demoknightVerticalClampMultiplier = 4f / 3f;

        var maxHorizontalSpeedPerTick = IsExperimentalDemoknightCharging
            ? GetServerHorizontalSpeedClampPerTick() * demoknightHorizontalClampMultiplier
            : GetServerHorizontalSpeedClampPerTick();
        var maxVerticalSpeedPerTick = IsExperimentalDemoknightCharging
            ? GetServerVerticalSpeedClampPerTick() * demoknightVerticalClampMultiplier
            : GetServerVerticalSpeedClampPerTick();
        HorizontalSpeed = float.Clamp(HorizontalSpeed, -maxHorizontalSpeedPerTick * LegacyMovementModel.SourceTicksPerSecond, maxHorizontalSpeedPerTick * LegacyMovementModel.SourceTicksPerSecond);
        VerticalSpeed = float.Clamp(VerticalSpeed, -maxVerticalSpeedPerTick * LegacyMovementModel.SourceTicksPerSecond, maxVerticalSpeedPerTick * LegacyMovementModel.SourceTicksPerSecond);
    }
}
