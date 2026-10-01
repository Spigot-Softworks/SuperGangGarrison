namespace OpenGarrison.Core;

public sealed partial class MovementSystem
{
    private const float JumpPadJumpBoostMultiplier = 1.85f;
    private const float JumpPadLaunchEpsilon = 0.01f;

    public void AdvanceJumpPads()
    {
        // Aura state is rebuilt from each pad owner's profile every tick. Do
        // this unconditionally so one hosted participant's perk cannot leak
        // into another participant's movement state.
        foreach (var player in EnumerateSimulatedPlayers())
        {
            player.SetExperimentalJumpPadAuraMovementSpeedMultiplier(1f);
        }

        for (var index = JumpPads.Count - 1; index >= 0; index -= 1)
        {
            var pad = JumpPads[index];
            var owner = FindPlayerById(pad.OwnerPlayerId);
            if (!pad.IsNeutral
                && (owner is null || owner.ClassId != PlayerClass.Engineer || owner.Team != pad.Team))
            {
                _host.DestroyJumpPad(pad);
                continue;
            }

            var wasLanded = pad.HasLanded;
            pad.Advance(Level, Level.Bounds);
            if (!wasLanded && pad.HasLanded)
            {
                RegisterWorldSoundEvent("SentryFloorSnd", pad.X, pad.Y);
                RegisterWorldSoundEvent("SentryBuildSnd", pad.X, pad.Y);
            }

            if (pad.HasLanded && owner is not null)
            {
                ApplyExperimentalJumpPadPassiveEffects(pad, owner);
            }

            if (pad.IsDead)
            {
                _host.DestroyJumpPad(pad);
            }
        }
    }

    public bool TryApplyJumpPadJumpBoostForPrediction(PlayerEntity player, bool jumped)
    {
        return TryApplyJumpPadJumpBoostFromPlayerJump(player, jumped, emitPresentationEvents: false);
    }

    private bool TryApplyJumpPadJumpBoostFromPlayerJump(PlayerEntity player, bool jumped, bool emitPresentationEvents = true)
    {
        if (!jumped || !player.IsAlive || player.VerticalSpeed >= 0f)
        {
            return false;
        }

        var pad = FindUsableJumpPadTouchingPlayer(player);
        if (pad is null)
        {
            return false;
        }

        return TryApplyJumpPadActivation(player, pad, emitPresentationEvents);
    }

    private bool TryApplyJumpPadActivation(PlayerEntity player, JumpPadEntity pad, bool emitPresentationEvents)
    {
        if (!player.IsAlive)
        {
            return false;
        }

        var owner = FindPlayerById(pad.OwnerPlayerId);
        var applied = false;
        if (owner is not null
            && GetLastToDieGameplaySettings(owner).EnableEngineerEntanglementTraverser
            && IsExperimentalEngineerPerkOwner(owner))
        {
            applied = TryApplyJumpPadTeleport(player, owner, pad, emitPresentationEvents);
        }

        if (!applied)
        {
            applied = TryApplyJumpPadLaunchImpulse(player, owner, emitPresentationEvents);
        }

        if (applied && owner is not null)
        {
            ApplyExperimentalJumpPadTraversalEffects(player, owner);
        }

        return applied;
    }

    private bool TryApplyJumpPadLaunchImpulse(PlayerEntity player, PlayerEntity? owner, bool emitPresentationEvents)
    {
        var boostedVerticalSpeed = -player.JumpSpeed * GetJumpPadJumpBoostMultiplier(owner);
        if (player.VerticalSpeed <= boostedVerticalSpeed + JumpPadLaunchEpsilon)
        {
            return false;
        }

        var extraVerticalImpulse = boostedVerticalSpeed - player.VerticalSpeed;
        player.AddImpulse(0f, extraVerticalImpulse);
        if (emitPresentationEvents)
        {
            RegisterSoundEvent(player, "JumpPadSnd");
            RegisterVisualEffect("AirBlast", player.X, player.Y - 8f, 270f);
        }
        return true;
    }

    private float GetJumpPadJumpBoostMultiplier(PlayerEntity? owner)
    {
        if (owner is null
            || !GetLastToDieGameplaySettings(owner).EnableEngineerAuraEnergizer
            || !IsExperimentalEngineerPerkOwner(owner))
        {
            return JumpPadJumpBoostMultiplier;
        }

        var baseBoost = JumpPadJumpBoostMultiplier - 1f;
        var scaledBoost = baseBoost * (1f - global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerAuraEnergizerJumpBoostHeightReductionFraction);
        return 1f + MathF.Max(0f, scaledBoost);
    }

    private JumpPadEntity? FindUsableJumpPadTouchingPlayer(PlayerEntity player)
    {
        for (var index = 0; index < JumpPads.Count; index += 1)
        {
            var pad = JumpPads[index];
            if (!IsJumpPadTriggerActive(pad)
                || !CanUseJumpPad(player, pad)
                || !IsPlayerInJumpPadTriggerArea(player, pad))
            {
                continue;
            }

            return pad;
        }

        return null;
    }

    public void HandleJumpPadTriggerContactEffects(PlayerEntity player)
    {
        if (Level.IsTopDown || !player.IsAlive)
        {
            return;
        }

        for (var index = 0; index < JumpPads.Count; index += 1)
        {
            var pad = JumpPads[index];
            var inTriggerArea = IsJumpPadTriggerActive(pad)
                && IsPlayerInJumpPadTriggerArea(player, pad);

            if (!inTriggerArea)
            {
                continue;
            }

            TryApplyExperimentalEnemyJumpPadEffects(player, pad);
        }
    }

    private static bool IsJumpPadTriggerActive(JumpPadEntity pad)
    {
        return pad.HasLanded && !pad.IsDead;
    }

    private static bool CanUseJumpPad(PlayerEntity player, JumpPadEntity pad)
    {
        if (pad.IsNeutral)
        {
            return true;
        }

        if (player.Id == pad.OwnerPlayerId)
        {
            return true;
        }

        if (player.Team == pad.Team)
        {
            return true;
        }

        return player.ClassId == PlayerClass.Spy;
    }

    private static bool IsPlayerInJumpPadTriggerArea(PlayerEntity player, JumpPadEntity pad)
    {
        var padHalfWidth = JumpPadEntity.Width * 0.75f;
        var padTop = pad.Y - (JumpPadEntity.Height / 2f);
        var padBottom = pad.Y + (JumpPadEntity.Height / 2f);
        return player.Right > pad.X - padHalfWidth
            && player.Left < pad.X + padHalfWidth
            && player.Bottom >= padTop - 2f
            && player.Top <= padBottom + 6f;
    }

    private void ApplyExperimentalJumpPadPassiveEffects(JumpPadEntity pad, PlayerEntity owner)
    {
        if (!IsExperimentalEngineerPerkOwner(owner))
        {
            return;
        }

        var settings = GetLastToDieGameplaySettings(owner);
        if (settings.EnableEngineerAuraEnergizer)
        {
            ApplyExperimentalJumpPadAuraEnergizer(pad, owner);
        }

        if (settings.EnableEngineerGravitonAffixer)
        {
            ApplyExperimentalJumpPadGravitonPull(pad, owner);
        }
    }

    private void ApplyExperimentalJumpPadAuraEnergizer(JumpPadEntity pad, PlayerEntity owner)
    {
        var auraRadius = global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerAuraEnergizerAuraRadius;
        foreach (var candidate in EnumerateSimulatedPlayers())
        {
            if (!candidate.IsAlive
                || candidate.Team != owner.Team
                || DistanceBetween(candidate.X, candidate.Y, pad.X, pad.Y) > auraRadius)
            {
                continue;
            }

            candidate.SetExperimentalJumpPadAuraMovementSpeedMultiplier(
                global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerAuraEnergizerAuraMovementSpeedMultiplier);
        }
    }

    private void ApplyExperimentalJumpPadGravitonPull(JumpPadEntity pad, PlayerEntity owner)
    {
        if (pad.LifetimeTicks > GetExperimentalJumpPadGravitonPullTicks())
        {
            return;
        }

        var pullRadius = global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerGravitonAffixerPullRadius;
        foreach (var candidate in EnumerateSimulatedPlayers())
        {
            if (!candidate.IsAlive
                || candidate.Team == owner.Team)
            {
                continue;
            }

            var deltaX = pad.X - candidate.X;
            var deltaY = pad.Y - candidate.Y;
            var distance = MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
            if (distance <= 0.001f || distance > pullRadius)
            {
                continue;
            }

            var pullScale = 1f - (distance / pullRadius);
            var impulse = global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerGravitonAffixerPullImpulsePerTick * pullScale;
            candidate.AddImpulse(MathF.Sign(deltaX) * impulse, 0f);
            candidate.RefreshExperimentalGravitonEffect(2);
        }
    }

    private void TryApplyExperimentalEnemyJumpPadEffects(PlayerEntity player, JumpPadEntity pad)
    {
        if (pad.IsNeutral || player.Team == pad.Team)
        {
            return;
        }

        var owner = FindPlayerById(pad.OwnerPlayerId);
        if (owner is null
            || !GetLastToDieGameplaySettings(owner).EnableEngineerGravitonAffixer
            || !IsExperimentalEngineerPerkOwner(owner))
        {
            return;
        }

        player.RefreshExperimentalJumpPadSlow(
            GetExperimentalJumpPadGravitonSlowTicks(),
            global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerGravitonAffixerEnemySlowMovementMultiplier);
        player.RefreshExperimentalGravitonEffect(GetExperimentalJumpPadGravitonSlowTicks());
    }

    private bool TryApplyJumpPadTeleport(PlayerEntity player, PlayerEntity owner, JumpPadEntity sourcePad, bool emitPresentationEvents)
    {
        var sentry = FindNearestOwnedBuiltSentry(owner.Id, sourcePad.X, sourcePad.Y);
        if (sentry is null)
        {
            return false;
        }

        player.TeleportTo(sentry.X, sentry.Y - MathF.Max(player.Height, SentryEntity.Height));
        player.ResolveBlockingOverlap(Level, player.Team);
        if (emitPresentationEvents)
        {
            RegisterSoundEvent(player, "AirblastSnd");
            RegisterVisualEffect("Poof", sourcePad.X, sourcePad.Y);
            RegisterVisualEffect("Poof", player.X, player.Y);
        }
        return true;
    }

    private void ApplyExperimentalJumpPadTraversalEffects(PlayerEntity player, PlayerEntity owner)
    {
        if (!GetLastToDieGameplaySettings(owner).EnableEngineerAuraEnergizer
            || !IsExperimentalEngineerPerkOwner(owner))
        {
            return;
        }

        player.GrantExperimentalJumpPadBurst(
            GetExperimentalJumpPadAuraBurstTicks(),
            global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerAuraEnergizerBurstMovementSpeedMultiplier);
    }

    private SentryEntity? FindNearestOwnedBuiltSentry(int ownerPlayerId, float x, float y)
    {
        SentryEntity? bestSentry = null;
        var bestDistanceSquared = float.MaxValue;
        for (var sentryIndex = 0; sentryIndex < Sentries.Count; sentryIndex += 1)
        {
            var sentry = Sentries[sentryIndex];
            if (sentry.OwnerPlayerId != ownerPlayerId || !sentry.IsBuilt)
            {
                continue;
            }

            var deltaX = sentry.X - x;
            var deltaY = sentry.Y - y;
            var distanceSquared = (deltaX * deltaX) + (deltaY * deltaY);
            if (distanceSquared >= bestDistanceSquared)
            {
                continue;
            }

            bestDistanceSquared = distanceSquared;
            bestSentry = sentry;
        }

        return bestSentry;
    }

    private int GetExperimentalJumpPadAuraBurstTicks()
    {
        return Math.Max(
            1,
            (int)MathF.Round(
                Config.TicksPerSecond * global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerAuraEnergizerBurstDurationSeconds));
    }

    private int GetExperimentalJumpPadGravitonPullTicks()
    {
        return Math.Max(
            1,
            (int)MathF.Round(
                Config.TicksPerSecond * global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerGravitonAffixerPullDurationSeconds));
    }

    private int GetExperimentalJumpPadGravitonSlowTicks()
    {
        return Math.Max(
            1,
            (int)MathF.Round(
                Config.TicksPerSecond * global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerGravitonAffixerEnemySlowDurationSeconds));
    }

    private static float DistanceBetween(float x1, float y1, float x2, float y2)
    {
        var deltaX = x2 - x1;
        var deltaY = y2 - y1;
        return MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
    }
}
