using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

/// <summary>
/// Stock Whipping Cord (Engineer alternate primary) identifiers and timing.
/// Recoil is a 15%/15%/70% split: wind-up (frame 0, no damage), strike
/// (frame 1, damage), then post-hit follow-through (frame 2+, no damage).
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

    public const int BaseDamage = 45;
    public const int RecoilDurationSourceTicks = 9;
    public const int CooldownSourceTicks = 18;

    /// <summary>Share of recoil spent on wind-up (atk1 / frame 0).</summary>
    public const float WindupProgress = 0.15f;

    /// <summary>Share of recoil spent on the damaging swing (atk2 / frame 1).</summary>
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

    public static int ResolveRecoilFrameIndex(float progress, int frameCount)
    {
        if (frameCount <= 1)
        {
            return 0;
        }

        var clamped = Math.Clamp(progress, 0f, 1f);
        if (frameCount == 2)
        {
            return clamped < FollowThroughProgress ? 0 : 1;
        }

        if (clamped < WindupProgress)
        {
            return 0;
        }

        if (clamped < FollowThroughProgress)
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
}
