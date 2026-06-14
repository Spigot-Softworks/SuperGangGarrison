using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public sealed partial class PlayerEntity
{
    public void SyncCivviePogoSuperJumpInput(bool upHeld)
    {
        CivviePogoSuperJumpHeld = upHeld;
    }

    public bool TryStartCivviePogoTrick(
        int trickFrameCount = CivviePogoTrickFrameCountDefault,
        int durationTicks = CivviePogoTrickDurationTicksDefault)
    {
        if (!IsAlive
            || ClassId != PlayerClass.Quote
            || !CanPerformCivviePogoTrick
            || CivviePogoTrickInputReleaseRequired
            || IsCivviePogoTrickActive)
        {
            return false;
        }

        trickFrameCount = Math.Max(1, trickFrameCount);
        durationTicks = Math.Max(1, durationTicks);
        CivviePogoTrickDurationTicks = durationTicks;
        CivviePogoTrickTicksRemaining = durationTicks;
        CivviePogoTrickInputReleaseRequired = true;
        CivviePogoSuperJumpTrickUsed = true;
        return true;
    }

    public void ObserveCivviePogoTrickInput(bool isHeld)
    {
        if (!isHeld)
        {
            CivviePogoTrickInputReleaseRequired = false;
        }
    }

    public bool TryToggleCivviePogo(
        float baseBounceJumpScale = CivviePogoBaseBounceJumpScaleDefault,
        float superJumpScale = CivviePogoSuperJumpScaleDefault,
        int crunchDurationTicks = CivviePogoCrunchDurationTicksDefault)
    {
        if (IsCivviePogoActive)
        {
            DeactivateCivviePogo();
            return true;
        }

        return TryActivateCivviePogo(baseBounceJumpScale, superJumpScale, crunchDurationTicks);
    }

    public bool TryActivateCivviePogo(
        float baseBounceJumpScale = CivviePogoBaseBounceJumpScaleDefault,
        float superJumpScale = CivviePogoSuperJumpScaleDefault,
        int crunchDurationTicks = CivviePogoCrunchDurationTicksDefault)
    {
        if (!IsAlive
            || ClassId != PlayerClass.Quote
            || !HasUtilityBehavior(BuiltInGameplayBehaviorIds.CivviePogo)
            || IsTaunting
            || IsHeavyEating
            || IsSpyBackstabAnimating
            || IsExperimentalCryoFrozen
            || IsCivvieUmbrellaActive)
        {
            return false;
        }

        CivviePogoBaseBounceJumpScale = baseBounceJumpScale;
        CivviePogoSuperJumpScale = superJumpScale;
        CivviePogoCrunchDurationTicks = crunchDurationTicks;
        IsCivviePogoActive = true;
        CivviePogoCrunchTicksRemaining = 0;
        CivviePogoNeedsGroundBounce = true;
        ResetCivviePogoStuckRecoveryState();
        return true;
    }

    public void DeactivateCivviePogo()
    {
        IsCivviePogoActive = false;
        IsCivviePogoSuperJumpAirPhaseActive = false;
        CivviePogoSuperJumpTrickUsed = false;
        CivviePogoCrunchTicksRemaining = 0;
        CivviePogoNeedsGroundBounce = false;
        ClearCivviePogoTrick();
        ResetCivviePogoStuckRecoveryState();
    }

    public void AdvanceCivviePogoState()
    {
        if (CivviePogoCrunchTicksRemaining > 0)
        {
            if (IsCivviePogoTrickActive)
            {
                ClearCivviePogoTrick();
            }

            CivviePogoCrunchTicksRemaining -= 1;
            return;
        }

        if (CivviePogoTrickTicksRemaining > 0)
        {
            CivviePogoTrickTicksRemaining -= 1;
        }
    }

    public bool TryApplyCivviePogoLandingBounce(bool wasAirborneBeforeMove)
    {
        if (!IsCivviePogoActive || !IsGrounded)
        {
            return false;
        }

        if (!wasAirborneBeforeMove && !CivviePogoNeedsGroundBounce)
        {
            return false;
        }

        return TryApplyCivviePogoBounceImpulse();
    }

    public void TryFulfillCivviePogoGroundBounceAfterMovement()
    {
        if (!IsCivviePogoActive || !CivviePogoNeedsGroundBounce || !IsGrounded)
        {
            return;
        }

        _ = TryApplyCivviePogoBounceImpulse();
    }

    public void AdvanceCivviePogoStuckGroundRecovery()
    {
        if (!IsCivviePogoActive)
        {
            return;
        }

        if (CivviePogoStuckRebounceCooldownTicks > 0)
        {
            CivviePogoStuckRebounceCooldownTicks -= 1;
        }

        if (!IsGrounded)
        {
            ResetCivviePogoStuckRecoveryState();
            return;
        }

        CivviePogoStuckWatchTicks += 1;
        if (CivviePogoStuckWatchTicks < CivviePogoStuckSampleIntervalTicks)
        {
            return;
        }

        var verticalMovement = MathF.Abs(Y - CivviePogoStuckReferenceY);
        CivviePogoStuckReferenceY = Y;
        CivviePogoStuckWatchTicks = 0;

        if (verticalMovement >= CivviePogoStuckMinVerticalMovement
            || CivviePogoStuckRebounceCooldownTicks > 0)
        {
            return;
        }

        CivviePogoNeedsGroundBounce = true;
        _ = TryApplyCivviePogoBounceImpulse();
        CivviePogoStuckRebounceCooldownTicks = CivviePogoStuckRebounceCooldownTicksDefault;
    }

    public bool TryConsumeCivviePogoSuperJumpSoundRequest(out float soundX, out float soundY)
    {
        soundX = X;
        soundY = Y;
        if (!CivviePogoSuperJumpSoundPending)
        {
            return false;
        }

        CivviePogoSuperJumpSoundPending = false;
        return true;
    }

    public static int GetCivviePogoSpriteFrameIndex(PlayerEntity player, int frameCount)
    {
        if (frameCount <= 0)
        {
            return 0;
        }

        return player.CivviePogoCrunchTicksRemaining > 0
            ? Math.Clamp(1, 0, frameCount - 1)
            : 0;
    }

    private void ClearCivviePogoTrick()
    {
        CivviePogoTrickTicksRemaining = 0;
        CivviePogoTrickDurationTicks = 0;
    }

    public int GetCivviePogoTrickFrameIndex(int sessionSeed, ulong currentFrame, int frameCount)
    {
        if (!IsCivviePogoTrickActive || frameCount <= 0)
        {
            return 0;
        }

        var durationTicks = CivviePogoTrickDurationTicks > 0
            ? CivviePogoTrickDurationTicks
            : CivviePogoTrickDurationTicksDefault;
        return CivviePogoTrickRules.ResolveTrickFrameIndex(
            sessionSeed,
            Id,
            currentFrame,
            durationTicks,
            CivviePogoTrickTicksRemaining,
            frameCount);
    }

    private bool TryApplyCivviePogoBounceImpulse()
    {
        ApplyCivviePogoBounce(
            CivviePogoSuperJumpHeld,
            CivviePogoBaseBounceJumpScale,
            CivviePogoSuperJumpScale,
            CivviePogoCrunchDurationTicks);
        if (VerticalSpeed >= 0f)
        {
            CivviePogoNeedsGroundBounce = true;
            return false;
        }

        CivviePogoNeedsGroundBounce = false;
        return CivviePogoSuperJumpSoundPending;
    }

    private void ApplyCivviePogoBounce(
        bool superJump,
        float baseBounceJumpScale,
        float superJumpScale,
        int crunchDurationTicks)
    {
        var jumpSpeed = JumpSpeed * GetJumpScale();
        if (jumpSpeed <= 0f)
        {
            return;
        }

        var scale = superJump
            ? MathF.Max(0f, superJumpScale)
            : MathF.Max(0f, baseBounceJumpScale);
        VerticalSpeed = -jumpSpeed * scale;
        IsGrounded = false;
        IsCivviePogoSuperJumpAirPhaseActive = superJump;
        if (superJump)
        {
            CivviePogoSuperJumpTrickUsed = false;
        }

        CivviePogoCrunchTicksRemaining = Math.Max(1, crunchDurationTicks);
        CivviePogoSuperJumpSoundPending = superJump;
    }

    private void ResetCivviePogoStuckRecoveryState()
    {
        CivviePogoStuckReferenceY = Y;
        CivviePogoStuckWatchTicks = 0;
        CivviePogoStuckRebounceCooldownTicks = 0;
    }

    private float CivviePogoBaseBounceJumpScale { get; set; } = CivviePogoBaseBounceJumpScaleDefault;

    private float CivviePogoSuperJumpScale { get; set; } = CivviePogoSuperJumpScaleDefault;

    private int CivviePogoCrunchDurationTicks { get; set; } = CivviePogoCrunchDurationTicksDefault;

    private bool CivviePogoNeedsGroundBounce { get; set; }

    private bool CivviePogoTrickInputReleaseRequired { get; set; }

    private bool CivviePogoSuperJumpTrickUsed { get; set; }

    private int CivviePogoTrickDurationTicks { get; set; }
}
