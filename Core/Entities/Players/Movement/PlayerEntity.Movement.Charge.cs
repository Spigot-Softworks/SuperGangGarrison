using System;

namespace OpenGarrison.Core;

public sealed partial class PlayerEntity
{
    private void ApplyExperimentalDemoknightChargeDrive(float deltaSeconds)
    {
        if (!IsExperimentalDemoknightCharging || !IsExperimentalDemoknightChargeDashActive || deltaSeconds <= 0f)
        {
            return;
        }

        var sourceTicks = LegacyMovementModel.SourceTicksPerSecond * deltaSeconds;
        var drivePerTick = ExperimentalDemoknightGroundChargeDrivePerTick;
        if (IsExperimentalDemoknightChargeFlightActive)
        {
            drivePerTick = ExperimentalDemoknightFlightChargeDrivePerTick
                + (ExperimentalDemoknightChargeAcceleration * ExperimentalDemoknightFlightChargeAccelerationDrivePerTick);
            MovementState = LegacyMovementState.FriendlyJuggle;
        }

        HorizontalSpeed += FacingDirectionX * drivePerTick * LegacyMovementModel.SourceTicksPerSecond * sourceTicks;
    }

    private bool TryHandleExperimentalDemoknightChargeHorizontalCollision(
        SimpleLevel level,
        PlayerTeam team,
        float horizontalDirection,
        ref float remainingX)
    {
        if (!IsExperimentalDemoknightCharging || horizontalDirection == 0f)
        {
            return false;
        }

        if (TryStepUpForObstacle(level, team, horizontalDirection))
        {
            ExperimentalDemoknightChargeAcceleration = MathF.Min(
                8f,
                ExperimentalDemoknightChargeAcceleration + ExperimentalDemoknightChargeTrimpAccelerationGainPerTick);

            if (ExperimentalDemoknightChargeWantsLift)
            {
                VerticalSpeed -= 7.5f * ExperimentalDemoknightChargeAcceleration * LegacyMovementModel.SourceTicksPerSecond;
            }

            if (IsExperimentalDemoknightChargeWantsFlight(level))
            {
                IsExperimentalDemoknightChargeFlightActive = true;
                MovementState = LegacyMovementState.FriendlyJuggle;
            }
            else
            {
                MovementState = LegacyMovementState.None;
            }

            return true;
        }

        if (IsExperimentalDemoknightChargeFlightActive
            && IsExperimentalDemoknightChargeAirborne(level)
            && ExperimentalDemoknightChargeAcceleration >= ExperimentalDemoknightChargeBounceAccelerationThreshold)
        {
            var bounceMultiplier = 1.2f + (ExperimentalDemoknightChargeAcceleration * 0.15f);
            HorizontalSpeed = -HorizontalSpeed * bounceMultiplier;
            VerticalSpeed -= 4f * ExperimentalDemoknightChargeAcceleration * LegacyMovementModel.SourceTicksPerSecond;
            IsExperimentalDemoknightChargeDashActive = false;
            remainingX = 0f;
            MovementState = LegacyMovementState.FriendlyJuggle;
            return true;
        }

        return false;
    }

    private void RefreshExperimentalDemoknightChargeFlightState(SimpleLevel level, bool allowDropdownFallThrough)
    {
        if (!IsExperimentalDemoknightCharging)
        {
            IsExperimentalDemoknightChargeFlightActive = false;
            ExperimentalDemoknightChargeWantsLift = false;
            return;
        }

        if (!IsExperimentalDemoknightChargeDashActive && IsGrounded)
        {
            ExperimentalDemoknightChargeAcceleration = 0f;
        }

        if (IsExperimentalDemoknightChargeWantsFlight(level, allowDropdownFallThrough))
        {
            IsExperimentalDemoknightChargeFlightActive = true;
            MovementState = LegacyMovementState.FriendlyJuggle;
        }
        else if (!IsExperimentalDemoknightChargeAirborne(level, allowDropdownFallThrough))
        {
            IsExperimentalDemoknightChargeFlightActive = false;
            if (MovementState == LegacyMovementState.FriendlyJuggle)
            {
                MovementState = LegacyMovementState.None;
            }
        }
    }

    private bool IsExperimentalDemoknightChargeWantsFlight(SimpleLevel level, bool allowDropdownFallThrough = false)
    {
        return ExperimentalDemoknightChargeWantsLift
            && ExperimentalDemoknightChargeAcceleration > ExperimentalDemoknightChargeFlightActivationAcceleration
            && IsExperimentalDemoknightChargeAirborne(level, allowDropdownFallThrough);
    }

    private bool IsExperimentalDemoknightChargeAirborne(SimpleLevel level, bool allowDropdownFallThrough = false)
    {
        return CanOccupy(level, Team, X, Y + 1f)
            && !IsStandingOnDropdownPlatform(level, allowDropdownFallThrough);
    }
}
