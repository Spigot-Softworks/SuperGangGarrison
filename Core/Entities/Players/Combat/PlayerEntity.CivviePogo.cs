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

        IsCivviePogoActive = true;
        CivviePogoCrunchTicksRemaining = 0;
        if (IsGrounded)
        {
            ApplyCivviePogoBounce(
                CivviePogoSuperJumpHeld,
                baseBounceJumpScale,
                superJumpScale,
                crunchDurationTicks);
        }

        return true;
    }

    public void DeactivateCivviePogo()
    {
        IsCivviePogoActive = false;
        CivviePogoCrunchTicksRemaining = 0;
    }

    public void AdvanceCivviePogoState()
    {
        if (CivviePogoCrunchTicksRemaining > 0)
        {
            CivviePogoCrunchTicksRemaining -= 1;
        }
    }

    public bool TryApplyCivviePogoLandingBounce(
        bool wasAirborneBeforeMove,
        float baseBounceJumpScale = CivviePogoBaseBounceJumpScaleDefault,
        float superJumpScale = CivviePogoSuperJumpScaleDefault,
        int crunchDurationTicks = CivviePogoCrunchDurationTicksDefault)
    {
        if (!IsCivviePogoActive || !IsGrounded || !wasAirborneBeforeMove)
        {
            return false;
        }

        ApplyCivviePogoBounce(
            CivviePogoSuperJumpHeld,
            baseBounceJumpScale,
            superJumpScale,
            crunchDurationTicks);
        return CivviePogoSuperJumpSoundPending;
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
}
