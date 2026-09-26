using System.Collections.Generic;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public sealed partial class PlayerEntity
{
    public bool IsWhippingCordSwingActive => WhippingCordSwingTicksRemaining > 0;

    public bool IsWhippingCordWindupActive => WhippingCordWindupTicksRemaining > 0;

    public bool IsWhippingCordAttackActive => IsWhippingCordSwingActive || IsWhippingCordWindupActive;

    private int WhippingCordWindupTicksRemaining { get; set; }

    private int WhippingCordSwingTicksRemaining { get; set; }

    private int WhippingCordPendingSwingTicks { get; set; }

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
            || IsWhippingCordAttackActive)
        {
            return false;
        }

        PrimaryCooldownTicks = GetPrimaryCooldownAfterShot();
        return true;
    }

    public int ResolveWhippingCordCooldownTicks()
        => Math.Max(1, GetPrimaryCooldownAfterShot());

    public void BeginWhippingCordWindup(int windupTicks, int swingDurationTicks)
    {
        ClearWhippingCordSwingHits();
        WhippingCordSwingImpactEmitted = false;
        WhippingCordAttackSoundEmitted = false;
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

    public void ClearWhippingCordSwing()
    {
        WhippingCordWindupTicksRemaining = 0;
        WhippingCordSwingTicksRemaining = 0;
        WhippingCordPendingSwingTicks = 0;
        WhippingCordSwingImpactEmitted = false;
        WhippingCordAttackSoundEmitted = false;
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
