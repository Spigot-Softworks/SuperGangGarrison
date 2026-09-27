using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

/// <summary>
/// Stock Whipping Cord (Engineer alternate primary) identifiers and timing.
/// Frame 0 starts the wind-up, frame 1 is its short motion smear, and frame 2
/// is the fully extended damaging strike and follow-through.
/// </summary>
public static class WhippingCordCatalog
{
    public const string ItemId = "weapon.whipping-cord";
    public const string BehaviorId = BuiltInGameplayBehaviorIds.WhippingCord;

    public const string TorsoSpriteName = "WhippingCordTorsoS";
    public const string TorsoRecoilSpriteName = "WhippingCordTorsoFS";
    public const string WhipSpriteName = "WhippingCordWhipS";
    public const string WhipRecoilSpriteName = "WhippingCordWhipFS";
    public const string MeleeHitboxSpriteName = "WhippingCordHitboxS";
    public const string KillFeedSpriteName = "WhipKL";
    public const string AttackSoundName = "WhippingCordCrackSnd";
    public const string ReplicatedLatchKey = "whipping_cord_latched";
    public const string ReplicatedAnchorXKey = "whipping_cord_anchor_x";
    public const string ReplicatedAnchorYKey = "whipping_cord_anchor_y";
    public const string ReplicatedRopeLengthKey = "whipping_cord_rope_length";

    public const int BaseDamage = 45;
    public const int RecoilDurationSourceTicks = 9;
    public const int CooldownSourceTicks = 18;
    public const float BackswingPullSpeedPerTick = 8f;
    public const float TerrainReleasePullSpeedPerTick = 8f;
    public const float TerrainReleaseUpwardImpulsePerTick = 1.5f;
    public const float MinimumRopeLength = 12f;
    // Frame 1 is the motion smear; frame 2 is the straight extended whip.
    // The tip is the center of its rightmost opaque pixel column, relative to
    // WhippingCordWhipFS's (60, 36) origin.
    public const int ExtendedWhipFrameIndex = 2;
    public const float ExtendedWhipTipOffsetX = 67.5f;
    public const float ExtendedWhipTipOffsetY = -4.5f;
    public const float BackswingPullStartProgress = 0.4f;

    public static (float Rotation, float ScaleX, float ScaleY) ResolveAnchoredWhipPose(
        float handleToAnchorX,
        float handleToAnchorY,
        float playerScale,
        bool facingLeft)
    {
        var distance = MathF.Sqrt(handleToAnchorX * handleToAnchorX + handleToAnchorY * handleToAnchorY);
        var verticalScale = MathF.Min(
            MathF.Max(0.1f, playerScale),
            distance / MathF.Abs(ExtendedWhipTipOffsetY));
        // Scale along the whip while keeping its thickness stable, so the
        // authored tip follows the fixed terrain point during a swing.
        var projectedLength = MathF.Sqrt(MathF.Max(0f,
            distance * distance - ExtendedWhipTipOffsetY * ExtendedWhipTipOffsetY * verticalScale * verticalScale));
        var horizontalScale = projectedLength / ExtendedWhipTipOffsetX * (facingLeft ? -1f : 1f);
        var sourceTipAngle = MathF.Atan2(
            ExtendedWhipTipOffsetY * verticalScale,
            ExtendedWhipTipOffsetX * horizontalScale);
        var rotation = MathF.Atan2(handleToAnchorY, handleToAnchorX) - sourceTipAngle;
        return (rotation, horizontalScale, verticalScale);
    }

    /// <summary>Share of recoil spent on wind-up (frames 0 and 1).</summary>
    public const float WindupProgress = 0.15f;

    /// <summary>Share of recoil spent on the damaging extended strike.</summary>
    public const float StrikeProgress = 0.15f;

    /// <summary>Progress at which follow-through (frame 2+) begins.</summary>
    public const float FollowThroughProgress = WindupProgress + StrikeProgress;

    public const float AutogunOverdriveMetalCost = 30f;
    public const int AutogunOverdriveDurationSourceTicks = 150; // 5 seconds at 30 TPS
    public const float AutogunOverdriveDamageMultiplier = 2f;
    public const float ConstructionSpeedMultiplier = 2f;

    public const int AutogunFullRepairMetalCost = 100;
    public const int DispenserFullRepairMetalCost = 100;
    public const int JumpPadFullRepairMetalCost = 50;

    public static int ResolveRecoilTicks(PlayerEntity player)
    {
        var itemId = player.GameplayLoadoutState.PrimaryItemId;
        if (!string.IsNullOrWhiteSpace(itemId)
            && CharacterClassCatalog.RuntimeRegistry.TryGetItem(itemId, out var item))
        {
            return item.Presentation.RecoilDurationSourceTicks;
        }

        return RecoilDurationSourceTicks;
    }

    public static int ResolveRecoilFrameIndex(float progress, int frameCount)
    {
        if (frameCount <= 1)
        {
            return 0;
        }

        var clamped = Math.Clamp(progress, 0f, 1f);
        if (frameCount == 2)
        {
            return clamped < WindupProgress ? 0 : 1;
        }

        if (clamped < WindupProgress * 0.5f)
        {
            return 0;
        }

        if (clamped < WindupProgress)
        {
            return Math.Min(1, frameCount - 1);
        }

        return Math.Min(frameCount - 1, 2);
    }

    public static bool IsDamageFrame(float progress)
        => progress >= WindupProgress && progress < FollowThroughProgress;

    public static int ResolveSwingTicks(int recoilDurationSourceTicks)
    {
        var recoilTicks = Math.Max(1, recoilDurationSourceTicks > 0 ? recoilDurationSourceTicks : RecoilDurationSourceTicks);
        return Math.Max(1, (int)MathF.Round(recoilTicks * StrikeProgress));
    }

    public static int ResolveWindupTicks(int recoilDurationSourceTicks)
    {
        var recoilTicks = Math.Max(1, recoilDurationSourceTicks > 0 ? recoilDurationSourceTicks : RecoilDurationSourceTicks);
        return Math.Max(0, (int)MathF.Round(recoilTicks * WindupProgress));
    }

    public static int ResolveBackswingTicks(int recoilDurationSourceTicks)
    {
        var recoilTicks = Math.Max(1, recoilDurationSourceTicks > 0 ? recoilDurationSourceTicks : RecoilDurationSourceTicks);
        return Math.Max(0, recoilTicks - ResolveWindupTicks(recoilTicks) - ResolveSwingTicks(recoilTicks));
    }
}
