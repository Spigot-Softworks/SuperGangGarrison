using System;

namespace OpenGarrison.Core;

public sealed partial class PlayerEntity
{
    private void SyncExperimentalDemoknightChargeTurnVelocity(float horizontalDirection)
    {
        if (!IsExperimentalDemoknightCharging
            || !ExperimentalDemoknightChargeFullControlEnabled
            || horizontalDirection == 0f)
        {
            return;
        }

        var desiredDirection = MathF.Sign(horizontalDirection);
        if (desiredDirection == 0f)
        {
            return;
        }

        var currentDirection = MathF.Sign(HorizontalSpeed);
        if (currentDirection == 0f || currentDirection == desiredDirection)
        {
            return;
        }

        var minimumChargeSpeed = ExperimentalDemoknightGroundChargeDrivePerTick * LegacyMovementModel.SourceTicksPerSecond;
        HorizontalSpeed = desiredDirection * MathF.Max(MathF.Abs(HorizontalSpeed), minimumChargeSpeed);
    }

    private void ApplyExperimentalDemoknightChargeDrive(float deltaSeconds)
    {
        if (!IsExperimentalDemoknightCharging || !IsExperimentalDemoknightChargeDashActive || deltaSeconds <= 0f)
        {
            return;
        }

        var sourceTicks = LegacyMovementModel.SourceTicksPerSecond * deltaSeconds;
        var drivePerTick = ExperimentalDemoknightGroundChargeDrivePerTick;
        var isFullControl = ExperimentalDemoknightChargeFullControlEnabled;
        if (IsExperimentalDemoknightChargeFlightActive)
        {
            drivePerTick = ExperimentalDemoknightFlightChargeDrivePerTick
                + (ExperimentalDemoknightChargeAcceleration * ExperimentalDemoknightFlightChargeAccelerationDrivePerTick);
            MovementState = LegacyMovementState.FriendlyJuggle;
        }

        // Calculate the base velocity addition in the facing direction
        var rawVelocityAddition = FacingDirectionX * drivePerTick * LegacyMovementModel.SourceTicksPerSecond * sourceTicks;

        // Re-orient momentum toward the current facing direction for smooth steering
        // Calculate how much the current velocity opposes the facing direction
        var currentSpeed = HorizontalSpeed;
        var speedMagnitude = MathF.Abs(currentSpeed);
        var currentDirectionMatchesFacing = MathF.Sign(currentSpeed) == FacingDirectionX;

        // Blend factor determines how quickly the charge can curve:
        // - Vanilla: gentler steering so direction changes preserve more momentum
        // - Full Control: aggressive steering with vertical control (60% blend per second)
        var blendFactor = isFullControl ? 0.6f : 0.18f;
        var blendRate = blendFactor * (float)deltaSeconds;
        var blendFactorClamped = Math.Clamp(blendRate, 0f, 1f);

        if (currentDirectionMatchesFacing || speedMagnitude < 1f)
        {
            // Same direction or stationary - just add velocity
            HorizontalSpeed += rawVelocityAddition;
        }
        else
        {
            // Opposing direction - blend velocity toward facing direction
            // This prevents immediate direction change while allowing curve
            var targetSpeed = FacingDirectionX * speedMagnitude;
            HorizontalSpeed = currentSpeed + ((targetSpeed - currentSpeed) * blendFactorClamped);
            // Still add some forward drive
            HorizontalSpeed += rawVelocityAddition * (1f - blendFactorClamped);
        }

        // With Full Control, also apply vertical steering based on aim direction Y component.
        // This steers toward cursor direction both upward and downward during charge flight.
        if (isFullControl && IsExperimentalDemoknightChargeFlightActive)
        {
            // Calculate facing direction Y from aim angle
            var aimRadians = AimDirectionDegrees * (MathF.PI / 180f);
            var facingDirectionY = MathF.Sin(aimRadians);
            if (MathF.Abs(facingDirectionY) > 0.2f)
            {
                var verticalSteer = -facingDirectionY * drivePerTick * LegacyMovementModel.SourceTicksPerSecond * sourceTicks * 0.4f;
                VerticalSpeed += verticalSteer;
            }
        }
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
        // Jump only initiates flight when Full Control is enabled (vanilla charge has no jump control)
        var jumpCanInitiateFlight = ExperimentalDemoknightChargeFullControlEnabled;
        var wantsLift = jumpCanInitiateFlight && ExperimentalDemoknightChargeWantsLift;

        return wantsLift
            && ExperimentalDemoknightChargeAcceleration > ExperimentalDemoknightChargeFlightActivationAcceleration
            && IsExperimentalDemoknightChargeAirborne(level, allowDropdownFallThrough);
    }

    private bool IsExperimentalDemoknightChargeAirborne(SimpleLevel level, bool allowDropdownFallThrough = false)
    {
        return CanOccupy(level, Team, X, Y + 1f)
            && !IsStandingOnDropdownPlatform(level, allowDropdownFallThrough);
    }

    private void ApplyExperimentalGhostDashMovement(SimpleLevel level, PlayerTeam team, float deltaSeconds, bool allowDropdownFallThrough)
    {
        if (!IsExperimentalGhostDashing
            || ExperimentalGhostDashDistanceRemaining <= 0f
            || deltaSeconds <= 0f)
        {
            return;
        }

        if (ExperimentalGhostDashUsesMomentum)
        {
            ApplyExperimentalMomentumGhostDashMovement(level, team, allowDropdownFallThrough);
            return;
        }

        if (ExperimentalGhostDashSpeedPerSecondValue <= 0f)
        {
            return;
        }

        var moveDistance = MathF.Min(
            ExperimentalGhostDashDistanceRemaining,
            ExperimentalGhostDashSpeedPerSecondValue * deltaSeconds);
        if (moveDistance <= 0f)
        {
            return;
        }

        MoveWithCollisions(level, team, FacingDirectionX * moveDistance, 0f, allowDropdownFallThrough);
        ExperimentalGhostDashDistanceRemaining = MathF.Max(0f, ExperimentalGhostDashDistanceRemaining - moveDistance);
    }

    private void ApplyExperimentalMomentumGhostDashMovement(SimpleLevel level, PlayerTeam team, bool allowDropdownFallThrough)
    {
        if (ExperimentalGhostDashInitialTicks <= 0 || ExperimentalGhostDashInitialDistance <= 0f)
        {
            return;
        }

        var elapsedTicks = Math.Clamp(
            ExperimentalGhostDashInitialTicks - ExperimentalGhostDashTicksRemaining,
            0,
            ExperimentalGhostDashInitialTicks - 1);
        var nextProgress = (elapsedTicks + 1) / (float)ExperimentalGhostDashInitialTicks;
        var targetDistance = ExperimentalGhostDashInitialDistance * SmoothStepProgress(nextProgress);
        var moveDistance = MathF.Max(0f, targetDistance - ExperimentalGhostDashDistanceTraveled);
        moveDistance = MathF.Min(moveDistance, ExperimentalGhostDashDistanceRemaining);
        if (moveDistance <= 0f)
        {
            return;
        }

        var previousX = X;
        MoveWithCollisions(level, team, ExperimentalGhostDashMomentumDirectionX * moveDistance, 0f, allowDropdownFallThrough);
        var movedDistance = MathF.Abs(X - previousX);
        ExperimentalGhostDashDistanceTraveled += moveDistance;
        ExperimentalGhostDashDistanceRemaining = MathF.Max(0f, ExperimentalGhostDashInitialDistance - ExperimentalGhostDashDistanceTraveled);

        if (movedDistance <= 0.001f)
        {
            ExperimentalGhostDashDistanceRemaining = 0f;
            return;
        }

        if (nextProgress >= 1f)
        {
            const float residualMomentumPerTick = 3.25f;
            var residualSpeed = residualMomentumPerTick * LegacyMovementModel.SourceTicksPerSecond;
            var residualVelocity = ExperimentalGhostDashMomentumDirectionX * residualSpeed;
            if (MathF.Sign(HorizontalSpeed) != MathF.Sign(residualVelocity)
                || MathF.Abs(HorizontalSpeed) < residualSpeed)
            {
                HorizontalSpeed = residualVelocity;
            }
        }
    }

    private static float SmoothStepProgress(float progress)
    {
        progress = float.Clamp(progress, 0f, 1f);
        return progress * progress * (3f - (2f * progress));
    }
}
