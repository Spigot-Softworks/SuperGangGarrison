namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private const float JumpPadBuildCost = 50f;
    private const float JumpPadBuildProximityRadius = 50f;
    private const float JumpPadJumpBoostMultiplier = 1.85f;
    private const float JumpPadLaunchEpsilon = 0.01f;

    public bool TryBuildLocalJumpPad()
    {
        return TryBuildJumpPad(LocalPlayer);
    }

    public bool TryDestroyLocalJumpPad()
    {
        for (var index = _jumpPads.Count - 1; index >= 0; index -= 1)
        {
            var pad = _jumpPads[index];
            if (pad.OwnerPlayerId != LocalPlayer.Id)
            {
                continue;
            }

            DestroyJumpPad(pad);
            return true;
        }

        return false;
    }

    internal void AdvanceJumpPads()
    {
        if (ExperimentalGameplaySettings.EnableEngineerAuraEnergizer)
        {
            foreach (var player in EnumerateSimulatedPlayers())
            {
                player.SetExperimentalJumpPadAuraMovementSpeedMultiplier(1f);
            }
        }

        for (var index = _jumpPads.Count - 1; index >= 0; index -= 1)
        {
            var pad = _jumpPads[index];
            var owner = FindPlayerById(pad.OwnerPlayerId);
            if (owner is null || owner.ClassId != PlayerClass.Engineer || owner.Team != pad.Team)
            {
                DestroyJumpPad(pad);
                continue;
            }

            var wasLanded = pad.HasLanded;
            pad.Advance(Level, Bounds);
            if (!wasLanded && pad.HasLanded)
            {
                RegisterWorldSoundEvent("SentryFloorSnd", pad.X, pad.Y);
                RegisterWorldSoundEvent("SentryBuildSnd", pad.X, pad.Y);
            }

            if (pad.HasLanded)
            {
                ApplyExperimentalJumpPadPassiveEffects(pad, owner);
            }

            if (pad.IsDead)
            {
                DestroyJumpPad(pad);
            }
        }
    }

    private bool TryApplyJumpPadJumpBoostFromPlayerJump(PlayerEntity player, bool jumped)
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

        return TryApplyJumpPadActivation(player, pad);
    }

    private bool TryApplyJumpPadActivation(PlayerEntity player, JumpPadEntity pad)
    {
        if (!player.IsAlive)
        {
            return false;
        }

        var owner = FindPlayerById(pad.OwnerPlayerId);
        var applied = false;
        if (owner is not null
            && ExperimentalGameplaySettings.EnableEngineerEntanglementTraverser
            && IsExperimentalEngineerPerkOwner(owner))
        {
            applied = TryApplyJumpPadTeleport(player, owner, pad);
        }

        if (!applied)
        {
            applied = TryApplyJumpPadLaunchImpulse(player, owner);
        }

        if (applied && owner is not null)
        {
            ApplyExperimentalJumpPadTraversalEffects(player, owner);
        }

        return applied;
    }

    private bool TryApplyJumpPadLaunchImpulse(PlayerEntity player, PlayerEntity? owner)
    {
        var boostedVerticalSpeed = -player.JumpSpeed * GetJumpPadJumpBoostMultiplier(owner);
        if (player.VerticalSpeed <= boostedVerticalSpeed + JumpPadLaunchEpsilon)
        {
            return false;
        }

        var extraVerticalImpulse = boostedVerticalSpeed - player.VerticalSpeed;
        player.AddImpulse(0f, extraVerticalImpulse);
        RegisterSoundEvent(player, "CompressionBlastSnd");
        RegisterVisualEffect("AirBlast", player.X, player.Y - 8f, 270f);
        return true;
    }

    private float GetJumpPadJumpBoostMultiplier(PlayerEntity? owner)
    {
        if (!ExperimentalGameplaySettings.EnableEngineerAuraEnergizer
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
        for (var index = 0; index < _jumpPads.Count; index += 1)
        {
            var pad = _jumpPads[index];
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

    private void HandleJumpPadTriggerContactEffects(PlayerEntity player)
    {
        if (!player.IsAlive)
        {
            return;
        }

        for (var index = 0; index < _jumpPads.Count; index += 1)
        {
            var pad = _jumpPads[index];
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

        if (ExperimentalGameplaySettings.EnableEngineerAuraEnergizer)
        {
            ApplyExperimentalJumpPadAuraEnergizer(pad, owner);
        }

        if (ExperimentalGameplaySettings.EnableEngineerGravitonAffixer)
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
        if (player.Team == pad.Team)
        {
            return;
        }

        var owner = FindPlayerById(pad.OwnerPlayerId);
        if (!ExperimentalGameplaySettings.EnableEngineerGravitonAffixer
            || !IsExperimentalEngineerPerkOwner(owner))
        {
            return;
        }

        player.RefreshExperimentalJumpPadSlow(
            GetExperimentalJumpPadGravitonSlowTicks(),
            global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerGravitonAffixerEnemySlowMovementMultiplier);
        player.RefreshExperimentalGravitonEffect(GetExperimentalJumpPadGravitonSlowTicks());
    }

    private bool TryApplyJumpPadTeleport(PlayerEntity player, PlayerEntity owner, JumpPadEntity sourcePad)
    {
        var sentry = FindNearestOwnedBuiltSentry(owner.Id, sourcePad.X, sourcePad.Y);
        if (sentry is null)
        {
            return false;
        }

        player.TeleportTo(sentry.X, sentry.Y - MathF.Max(player.Height, SentryEntity.Height));
        player.ResolveBlockingOverlap(Level, player.Team);
        RegisterSoundEvent(player, "AirblastSnd");
        RegisterVisualEffect("Poof", sourcePad.X, sourcePad.Y);
        RegisterVisualEffect("Poof", player.X, player.Y);
        return true;
    }

    private void ApplyExperimentalJumpPadTraversalEffects(PlayerEntity player, PlayerEntity owner)
    {
        if (!ExperimentalGameplaySettings.EnableEngineerAuraEnergizer
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
        for (var sentryIndex = 0; sentryIndex < _sentries.Count; sentryIndex += 1)
        {
            var sentry = _sentries[sentryIndex];
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

    private void DestroyJumpPad(JumpPadEntity pad)
    {
        for (var index = _jumpPads.Count - 1; index >= 0; index -= 1)
        {
            if (!ReferenceEquals(_jumpPads[index], pad))
            {
                continue;
            }

            _entities.Remove(pad.Id);
            _jumpPads.RemoveAt(index);
            RegisterWorldSoundEvent("ExplosionSnd", pad.X, pad.Y);
            RegisterVisualEffect("Explosion", pad.X, pad.Y);
            SpawnJumpPadGibs(pad.Team, pad.X, pad.Y);
            break;
        }
    }

    private void SpawnJumpPadGibs(PlayerTeam team, float x, float y)
    {
        var gib = new JumpPadGibEntity(AllocateEntityId(), team, x, y);
        _jumpPadGibs.Add(gib);
        _entities.Add(gib.Id, gib);
    }

    private void AdvanceJumpPadGibs()
    {
        for (var gibIndex = _jumpPadGibs.Count - 1; gibIndex >= 0; gibIndex -= 1)
        {
            var gib = _jumpPadGibs[gibIndex];
            gib.AdvanceOneTick();
            var pickedUp = false;
            foreach (var player in EnumerateSimulatedPlayers())
            {
                if (!player.IsAlive || player.ClassId != PlayerClass.Engineer || player.Metal >= player.MaxMetal)
                {
                    continue;
                }

                if (!player.IntersectsMarker(gib.X, gib.Y, JumpPadGibEntity.PickupRadius, JumpPadGibEntity.PickupRadius))
                {
                    continue;
                }

                player.AddMetal(JumpPadGibEntity.MetalValue);
                pickedUp = true;
                break;
            }

            if (!pickedUp && !gib.IsExpired)
            {
                continue;
            }

            _entities.Remove(gib.Id);
            _jumpPadGibs.RemoveAt(gibIndex);
        }
    }

    private bool TryBuildJumpPad(PlayerEntity player)
    {
        if (!player.IsAlive
            || player.ClassId != PlayerClass.Engineer
            || player.IsInSpawnRoom)
        {
            return false;
        }

        foreach (var pad in _jumpPads)
        {
            if (pad.OwnerPlayerId == player.Id)
            {
                return false;
            }

            if (pad.IsNear(player.X, player.Y, JumpPadBuildProximityRadius))
            {
                return false;
            }
        }

        if (!player.SpendMetal(JumpPadBuildCost))
        {
            return false;
        }

        var entity = new JumpPadEntity(AllocateEntityId(), player.Id, player.Team, player.X, player.Y);
        _jumpPads.Add(entity);
        _entities.Add(entity.Id, entity);
        return true;
    }

    private bool TryDestroyJumpPad(PlayerEntity player)
    {
        if (!player.IsAlive)
        {
            return false;
        }

        var hadPad = false;
        for (var index = _jumpPads.Count - 1; index >= 0; index -= 1)
        {
            if (_jumpPads[index].OwnerPlayerId != player.Id)
            {
                continue;
            }

            hadPad = true;
            DestroyJumpPad(_jumpPads[index]);
        }

        return hadPad;
    }
}
