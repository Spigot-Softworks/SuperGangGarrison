using System.Collections.Generic;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public sealed partial class PlayerEntity
{
    public bool IsWhippingCordSwingActive => WhippingCordSwingTicksRemaining > 0;

    public bool IsWhippingCordWindupActive => WhippingCordWindupTicksRemaining > 0;

    public bool IsWhippingCordBackswingActive => WhippingCordWindupTicksRemaining == 0
        && WhippingCordSwingTicksRemaining == 0
        && WhippingCordBackswingTicksRemaining > 0;

    public bool IsWhippingCordAttackActive => IsWhippingCordSwingActive || IsWhippingCordWindupActive || IsWhippingCordBackswingActive;

    public bool IsWhippingCordLatched { get; private set; }

    public float WhippingCordAnchorX { get; private set; }

    public float WhippingCordAnchorY { get; private set; }

    public float WhippingCordRopeLength { get; private set; }

    private int WhippingCordWindupTicksRemaining { get; set; }

    private int WhippingCordSwingTicksRemaining { get; set; }

    private int WhippingCordPendingSwingTicks { get; set; }

    private int WhippingCordBackswingTicksRemaining { get; set; }

    private int WhippingCordBackswingDurationTicks { get; set; }

    private bool WhippingCordBackswingTargetClaimed { get; set; }

    private int WhippingCordPendingBackswingTargetId { get; set; } = -1;

    private bool WhippingCordSwingImpactEmitted { get; set; }

    private bool WhippingCordAttackSoundEmitted { get; set; }

    private readonly HashSet<int> WhippingCordHitPlayerIds = new();

    private readonly HashSet<int> WhippingCordHitSentryIds = new();

    private readonly HashSet<int> WhippingCordHitJumpPadIds = new();

    private readonly HashSet<PlayerTeam> WhippingCordHitGeneratorTeams = new();

    public bool TryFireWhippingCord()
    {
        if (!HasPrimaryBehavior(BuiltInGameplayBehaviorIds.WhippingCord)
            || !IsAlive
            || IsHeavyEating
            || IsTaunting
            || IsSpyCloaked
            || PrimaryCooldownTicks > 0
            || IsWhippingCordAttackActive
            || IsWhippingCordLatched)
        {
            return false;
        }

        PrimaryCooldownTicks = GetPrimaryCooldownAfterShot();
        return true;
    }

    public int ResolveWhippingCordCooldownTicks()
        => Math.Max(1, GetPrimaryCooldownAfterShot());

    public void BeginWhippingCordWindup(int windupTicks, int swingDurationTicks, int backswingTicks = 0)
    {
        ReleaseWhippingCord();
        ClearWhippingCordSwingHits();
        WhippingCordSwingImpactEmitted = false;
        WhippingCordAttackSoundEmitted = false;
        WhippingCordBackswingTargetClaimed = false;
        WhippingCordPendingBackswingTargetId = -1;
        WhippingCordBackswingTicksRemaining = Math.Max(0, backswingTicks);
        WhippingCordBackswingDurationTicks = WhippingCordBackswingTicksRemaining;
        WhippingCordPendingSwingTicks = Math.Max(1, swingDurationTicks);
        if (windupTicks <= 0)
        {
            WhippingCordWindupTicksRemaining = 0;
            WhippingCordSwingTicksRemaining = WhippingCordPendingSwingTicks;
            return;
        }

        WhippingCordWindupTicksRemaining = windupTicks;
        WhippingCordSwingTicksRemaining = 0;
    }

    /// <summary>
    /// Advances wind-up. Returns true when the damage swing is active and should
    /// process hits this tick. Wind-up completing arms the swing for the following tick.
    /// </summary>
    public bool TryEnterWhippingCordDamageWindow()
    {
        if (WhippingCordSwingTicksRemaining > 0)
        {
            return true;
        }

        if (WhippingCordWindupTicksRemaining <= 0)
        {
            return false;
        }

        WhippingCordWindupTicksRemaining -= 1;
        if (WhippingCordWindupTicksRemaining > 0)
        {
            return false;
        }

        // Wind-up just finished: arm the damage window for subsequent ticks.
        WhippingCordSwingTicksRemaining = Math.Max(1, WhippingCordPendingSwingTicks);
        ClearWhippingCordSwingHits();
        WhippingCordSwingImpactEmitted = false;
        return false;
    }

    /// <summary>
    /// Advances the damage window.
    /// </summary>
    public void AdvanceWhippingCordSwingTimer()
    {
        if (WhippingCordSwingTicksRemaining <= 0)
        {
            return;
        }

        WhippingCordSwingTicksRemaining -= 1;
        if (WhippingCordSwingTicksRemaining <= 0)
        {
            ClearWhippingCordSwingHits();
        }
    }

    public float WhippingCordBackswingReachScale => WhippingCordBackswingDurationTicks <= 0
        ? 0f
        : MathF.Max(0.25f, WhippingCordBackswingTicksRemaining / (float)WhippingCordBackswingDurationTicks);

    public float WhippingCordBackswingProgress => WhippingCordBackswingDurationTicks <= 0
        ? 1f
        : 1f - WhippingCordBackswingTicksRemaining / (float)WhippingCordBackswingDurationTicks;

    public void AdvanceWhippingCordBackswingTimer()
    {
        if (WhippingCordBackswingTicksRemaining > 0)
        {
            WhippingCordBackswingTicksRemaining -= 1;
            if (WhippingCordBackswingTicksRemaining == 0)
            {
                WhippingCordPendingBackswingTargetId = -1;
            }
        }
    }

    public bool TryClaimWhippingCordBackswingTarget()
    {
        if (WhippingCordBackswingTargetClaimed || !IsWhippingCordBackswingActive)
        {
            return false;
        }

        WhippingCordBackswingTargetClaimed = true;
        return true;
    }

    public int PendingWhippingCordBackswingTargetId => WhippingCordPendingBackswingTargetId;

    public bool TryQueueWhippingCordBackswingTarget(int targetId)
    {
        if (WhippingCordBackswingTargetClaimed || WhippingCordPendingBackswingTargetId >= 0
            || !IsWhippingCordBackswingActive)
        {
            return false;
        }

        WhippingCordPendingBackswingTargetId = targetId;
        return true;
    }

    public void ClearPendingWhippingCordBackswingTarget()
        => WhippingCordPendingBackswingTargetId = -1;

    public void LatchWhippingCord(float anchorX, float anchorY, float ropeLength)
    {
        if (!float.IsFinite(anchorX) || !float.IsFinite(anchorY) || !float.IsFinite(ropeLength)
            || ropeLength < WhippingCordCatalog.MinimumRopeLength)
        {
            return;
        }

        IsWhippingCordLatched = true;
        WhippingCordAnchorX = anchorX;
        WhippingCordAnchorY = anchorY;
        WhippingCordRopeLength = ropeLength;
        WhippingCordWindupTicksRemaining = 0;
        WhippingCordSwingTicksRemaining = 0;
        WhippingCordBackswingTicksRemaining = 0;
    }

    public void ReleaseWhippingCord()
    {
        IsWhippingCordLatched = false;
        WhippingCordAnchorX = 0f;
        WhippingCordAnchorY = 0f;
        WhippingCordRopeLength = 0f;
    }

    public bool ReleaseWhippingCordWithPull()
    {
        if (!IsWhippingCordLatched)
        {
            return false;
        }

        var pullX = WhippingCordAnchorX - X;
        var pullY = WhippingCordAnchorY - Y;
        var pullDistance = MathF.Sqrt(pullX * pullX + pullY * pullY);
        ReleaseWhippingCord();
        BeginWhippingCordReleaseBackswing();
        if (pullDistance > 0.0001f)
        {
            var speed = WhippingCordCatalog.TerrainReleasePullSpeedPerTick
                * LegacyMovementModel.SourceTicksPerSecond;
            var upwardImpulse = WhippingCordCatalog.TerrainReleaseUpwardImpulsePerTick
                * LegacyMovementModel.SourceTicksPerSecond;
            AddImpulse(pullX / pullDistance * speed, pullY / pullDistance * speed - upwardImpulse);
        }

        return true;
    }

    private void BeginWhippingCordReleaseBackswing()
    {
        WhippingCordWindupTicksRemaining = 0;
        WhippingCordSwingTicksRemaining = 0;
        WhippingCordBackswingDurationTicks = Math.Max(
            1, WhippingCordCatalog.ResolveBackswingTicks(WhippingCordCatalog.ResolveRecoilTicks(this)));
        WhippingCordBackswingTicksRemaining = WhippingCordBackswingDurationTicks;
        WhippingCordBackswingTargetClaimed = false;
        WhippingCordPendingBackswingTargetId = -1;
    }

    public void HydrateWhippingCordLatch(bool isLatched, float anchorX, float anchorY, float ropeLength)
    {
        if (isLatched && IsAlive && HasEquippedBehavior(BuiltInGameplayBehaviorIds.WhippingCord))
        {
            LatchWhippingCord(anchorX, anchorY, ropeLength);
        }
        else
        {
            var wasLatched = IsWhippingCordLatched;
            ReleaseWhippingCord();
            if (wasLatched && IsAlive && HasEquippedBehavior(BuiltInGameplayBehaviorIds.WhippingCord))
            {
                BeginWhippingCordReleaseBackswing();
            }
        }
    }

    private void ConstrainWhippingCordMovement(SimpleLevel level, PlayerTeam team)
    {
        if (!IsWhippingCordLatched)
        {
            return;
        }

        var offsetX = X - WhippingCordAnchorX;
        var offsetY = Y - WhippingCordAnchorY;
        var distance = MathF.Sqrt(offsetX * offsetX + offsetY * offsetY);
        if (distance <= 0.0001f || distance < WhippingCordRopeLength - 0.001f)
        {
            return;
        }

        var radialX = offsetX / distance;
        var radialY = offsetY / distance;
        if (distance > WhippingCordRopeLength)
        {
            var correctedX = WhippingCordAnchorX + radialX * WhippingCordRopeLength;
            var correctedY = WhippingCordAnchorY + radialY * WhippingCordRopeLength;
            if (!CanOccupy(level, team, correctedX, correctedY))
            {
                // A wall blocks the rope's circular path. Keep the anchor and
                // stop at contact; never drag the player inside the wall.
                HorizontalSpeed = 0f;
                VerticalSpeed = 0f;
                return;
            }

            X = correctedX;
            Y = correctedY;
        }

        var outwardSpeed = HorizontalSpeed * radialX + VerticalSpeed * radialY;
        if (outwardSpeed > 0f)
        {
            HorizontalSpeed -= outwardSpeed * radialX;
            VerticalSpeed -= outwardSpeed * radialY;
        }
    }

    public void ClearWhippingCordSwing()
    {
        WhippingCordWindupTicksRemaining = 0;
        WhippingCordSwingTicksRemaining = 0;
        WhippingCordPendingSwingTicks = 0;
        WhippingCordBackswingTicksRemaining = 0;
        WhippingCordBackswingDurationTicks = 0;
        WhippingCordBackswingTargetClaimed = false;
        WhippingCordPendingBackswingTargetId = -1;
        WhippingCordSwingImpactEmitted = false;
        WhippingCordAttackSoundEmitted = false;
        ReleaseWhippingCord();
        ClearWhippingCordSwingHits();
    }

    private void ClearWhippingCordSwingHits()
    {
        WhippingCordHitPlayerIds.Clear();
        WhippingCordHitSentryIds.Clear();
        WhippingCordHitJumpPadIds.Clear();
        WhippingCordHitGeneratorTeams.Clear();
    }

    public bool HasWhippingCordHitPlayer(int playerId) => WhippingCordHitPlayerIds.Contains(playerId);

    public bool HasWhippingCordHitSentry(int sentryId) => WhippingCordHitSentryIds.Contains(sentryId);

    public bool HasWhippingCordHitJumpPad(int jumpPadId) => WhippingCordHitJumpPadIds.Contains(jumpPadId);

    public bool HasWhippingCordHitGenerator(PlayerTeam team) => WhippingCordHitGeneratorTeams.Contains(team);

    public bool TryMarkWhippingCordHitPlayer(int playerId) => WhippingCordHitPlayerIds.Add(playerId);

    public bool TryMarkWhippingCordHitSentry(int sentryId) => WhippingCordHitSentryIds.Add(sentryId);

    public bool TryMarkWhippingCordHitJumpPad(int jumpPadId) => WhippingCordHitJumpPadIds.Add(jumpPadId);

    public bool TryMarkWhippingCordHitGenerator(PlayerTeam team) => WhippingCordHitGeneratorTeams.Add(team);

    public bool TryMarkWhippingCordSwingImpact()
    {
        if (WhippingCordSwingImpactEmitted)
        {
            return false;
        }

        WhippingCordSwingImpactEmitted = true;
        return true;
    }

    public bool TryMarkWhippingCordAttackSound()
    {
        if (WhippingCordAttackSoundEmitted)
        {
            return false;
        }

        WhippingCordAttackSoundEmitted = true;
        return true;
    }

    public int GetWhippingCordDamage()
    {
        if (!HasPrimaryBehavior(BuiltInGameplayBehaviorIds.WhippingCord))
        {
            return 0;
        }

        var baseDamage = PrimaryWeapon.DirectHitDamage ?? WhippingCordCatalog.BaseDamage;
        return Math.Max(1, (int)MathF.Round((float)baseDamage));
    }
}
