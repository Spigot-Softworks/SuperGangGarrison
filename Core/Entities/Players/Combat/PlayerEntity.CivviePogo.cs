using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public sealed partial class PlayerEntity
{
    public void SyncCivviePogoSuperJumpInput(bool upHeld)
    {
        CivviePogoSuperJumpHeld = upHeld;
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
        return true;
    }

    public void DeactivateCivviePogo()
    {
        IsCivviePogoActive = false;
        CivviePogoCrunchTicksRemaining = 0;
        CivviePogoNeedsGroundBounce = false;
    }

    public void AdvanceCivviePogoState()
    {
        if (CivviePogoCrunchTicksRemaining > 0)
        {
            CivviePogoCrunchTicksRemaining -= 1;
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

    private bool TryApplyCivviePogoBounceImpulse()
    {
        ApplyCivviePogoBounce(
            CivviePogoSuperJumpHeld,
            CivviePogoBaseBounceJumpScale,
            CivviePogoSuperJumpScale,
            CivviePogoCrunchDurationTicks);
        if (VerticalSpeed >= 0f)
        {
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
        CivviePogoCrunchTicksRemaining = Math.Max(1, crunchDurationTicks);
        CivviePogoSuperJumpSoundPending = superJump;
    }

    private float CivviePogoBaseBounceJumpScale { get; set; } = CivviePogoBaseBounceJumpScaleDefault;

    private float CivviePogoSuperJumpScale { get; set; } = CivviePogoSuperJumpScaleDefault;

    private int CivviePogoCrunchDurationTicks { get; set; } = CivviePogoCrunchDurationTicksDefault;

    private bool CivviePogoNeedsGroundBounce { get; set; }
}
