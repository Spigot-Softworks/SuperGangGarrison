using System.Diagnostics;
using System.Globalization;

namespace OpenGarrison.Core;

internal sealed class EntityTickPhase : IEntityTickPhase
{
    private readonly IEntityPhaseHost _host;

    public EntityTickPhase(IEntityPhaseHost host)
    {
        _host = host;
    }

    public void AdvanceProjectileAndTransientEntityPhase()
    {
        if (!HasProjectileOrTransientStateToAdvance())
        {
            return;
        }

        if (SimulationTrace.Enabled)
        {
            AdvanceProjectileAndTransientEntityPhaseWithTracing();
            return;
        }

        _host.AdvanceCombatTraces();
        _host.ComputeSniperAimIndicators();
        _host.Projectiles.AdvanceShots();
        _host.Projectiles.AdvanceBubbles();
        _host.Projectiles.AdvanceBlades();
        _host.Projectiles.AdvanceNeedles();
        _host.Projectiles.AdvanceRevolverShots();
        _host.Projectiles.AdvanceStabAnimations();
        _host.Projectiles.AdvanceStabMasks();
        _host.Projectiles.AdvanceFlames();
        _host.Projectiles.AdvanceFlares();
        _host.Projectiles.AdvanceRockets();
        _host.Projectiles.AdvanceMines();
        _host.Projectiles.AdvanceGrenades();
        _host.AdvancePlayerGibs();
        _host.AdvanceBloodDrops();
        _host.AdvanceDeadBodies();
        _host.AdvanceSentryGibs();
        _host.AdvanceJumpPadGibs();
    }

    private bool HasProjectileOrTransientStateToAdvance()
    {
        if (_host.PresentationEvents.CombatTraces.Count > 0
            || _host.PresentationEvents.SniperAimIndicators.Count > 0
            || _host.Projectiles.Shots.Count > 0
            || _host.Projectiles.Bubbles.Count > 0
            || _host.Projectiles.Blades.Count > 0
            || _host.Projectiles.Needles.Count > 0
            || _host.Projectiles.RevolverShots.Count > 0
            || _host.Projectiles.StabAnimations.Count > 0
            || _host.Projectiles.StabMasks.Count > 0
            || _host.Projectiles.Flames.Count > 0
            || _host.Projectiles.Flares.Count > 0
            || _host.Projectiles.Rockets.Count > 0
            || _host.Projectiles.Mines.Count > 0
            || _host.Projectiles.Grenades.Count > 0
            || _host.WorldObjects.PlayerGibs.Count > 0
            || _host.WorldObjects.BloodDrops.Count > 0
            || _host.WorldObjects.DeadBodies.Count > 0
            || _host.WorldObjects.SentryGibs.Count > 0
            || _host.WorldObjects.JumpPadGibs.Count > 0)
        {
            return true;
        }

        // An enabled aim indicator is generated from scoped players rather
        // than from a projectile collection, so retain that phase only for
        // the frames where a scoped rifle actually needs one.
        if (!_host.SniperAimIndicatorEnabled)
        {
            return false;
        }

        if (_host.LocalPlayer.IsAlive
            && _host.LocalPlayer.IsSniperScoped
            && !_host.LocalPlayer.IsSniperBowEquipped)
        {
            return true;
        }

        foreach (var slot in _host.PlayerRegistry.EnabledAdditionalSlots)
        {
            if (_host.NetworkPlayers.TryGetNetworkPlayer(slot, out var player)
                && player.IsAlive
                && player.IsSniperScoped
                && !player.IsSniperBowEquipped)
            {
                return true;
            }
        }

        if (_host.EnemyPlayerEnabled
            && _host.EnemyPlayer.IsAlive
            && _host.EnemyPlayer.IsSniperScoped
            && !_host.EnemyPlayer.IsSniperBowEquipped)
        {
            return true;
        }

        return _host.FriendlyDummyEnabled
            && _host.FriendlyDummy.IsAlive
            && _host.FriendlyDummy.IsSniperScoped
            && !_host.FriendlyDummy.IsSniperBowEquipped;
    }

    private void AdvanceProjectileAndTransientEntityPhaseWithTracing()
    {
        AdvanceTimedPhase("combatTraces", _host.AdvanceCombatTraces);
        AdvanceTimedPhase("sniperIndicators", _host.ComputeSniperAimIndicators);
        AdvanceTimedPhase("shots", _host.Projectiles.AdvanceShots);
        AdvanceTimedPhase("bubbles", _host.Projectiles.AdvanceBubbles);
        AdvanceTimedPhase("blades", _host.Projectiles.AdvanceBlades);
        AdvanceTimedPhase("needles", _host.Projectiles.AdvanceNeedles);
        AdvanceTimedPhase("revolverShots", _host.Projectiles.AdvanceRevolverShots);
        AdvanceTimedPhase("stabAnimations", _host.Projectiles.AdvanceStabAnimations);
        AdvanceTimedPhase("stabMasks", _host.Projectiles.AdvanceStabMasks);
        AdvanceTimedPhase("flames", _host.Projectiles.AdvanceFlames);
        AdvanceTimedPhase("flares", _host.Projectiles.AdvanceFlares);
        AdvanceTimedPhase("rockets", _host.Projectiles.AdvanceRockets);
        AdvanceTimedPhase("mines", _host.Projectiles.AdvanceMines);
        AdvanceTimedPhase("grenades", _host.Projectiles.AdvanceGrenades);
        AdvanceTimedPhase("playerGibs", _host.AdvancePlayerGibs);
        AdvanceTimedPhase("bloodDrops", _host.AdvanceBloodDrops);
        AdvanceTimedPhase("deadBodies", _host.AdvanceDeadBodies);
        AdvanceTimedPhase("sentryGibs", _host.AdvanceSentryGibs);
        AdvanceTimedPhase("jumpPadGibs", _host.AdvanceJumpPadGibs);
    }

    private void AdvanceTimedPhase(string name, Action advance)
    {
        var startTimestamp = Stopwatch.GetTimestamp();
        advance();
        var elapsedMilliseconds = SimulationTrace.ElapsedMilliseconds(startTimestamp);
        if (elapsedMilliseconds < SimulationTrace.PlayerThresholdMilliseconds)
        {
            return;
        }

        var line = string.Create(
            CultureInfo.InvariantCulture,
            $"{DateTime.Now:O} frame={_host.Frame} phase={name} elapsedMs={elapsedMilliseconds:0.0}{Environment.NewLine}");
        SimulationTrace.Append(SimulationTraceLog.PhaseSpikes, line);
    }


    public void AdvanceRemoteSnapshotPlayerTauntStates()
    {
        _host.SnapshotApply.AdvanceRemoteSnapshotPlayerTauntStates();
    }

    public void AdvancePlayerSimulationPhase()
    {
        _host.SupportRules.ApplyBuffBannerRegeneration();
        _host.Structures.UpdateDispenserAuras();
        _host.SupportRules.UpdateBuffBannerAuras();
        var phaseStartTimestamp = SimulationTrace.StartTimestamp();
        var enabledAdditionalSlots = _host.PlayerRegistry.EnabledAdditionalSlots;
        byte[]? playerTimingSlots = SimulationTrace.Enabled ? new byte[1 + enabledAdditionalSlots.Count] : null;
        double[]? playerTimingMilliseconds = SimulationTrace.Enabled ? new double[1 + enabledAdditionalSlots.Count] : null;
        var timingIndex = 0;

        AdvancePlayerSlot(_host.LocalPlayerSlot);
        foreach (var slot in enabledAdditionalSlots)
        {
            AdvancePlayerSlot(slot);
        }

        TracePlayerPhaseBreakdown(phaseStartTimestamp, playerTimingSlots, playerTimingMilliseconds);
        _host.SupportRules.UpdateBuffBannerAuras();

        // Taunt frames are advanced inside PlayerEntity.AdvanceTickState during full
        // simulation. AdvanceRemoteSnapshotPlayerTauntStates is client-prediction only.
        _host.PracticeDummies.AdvanceEnemyDummy();

        if (_host.FriendlyDummyEnabled && _host.FriendlyDummy.IsAlive)
        {
            _host.Movement.ApplyRoomForces(_host.FriendlyDummy);
            _host.FriendlyDummy.Advance(default, false, _host.Level, _host.FriendlyDummy.Team, _host.Config.FixedDeltaSeconds);
            _host.RoomEffects.UpdateSpawnRoomState(_host.FriendlyDummy);
            _host.PlayerInput.TryActivatePendingSpyBackstab(_host.FriendlyDummy);
            _host.RoomEffects.ApplyHealingCabinets(_host.FriendlyDummy);
            _host.RoomEffects.ApplyRoomHazards(_host.FriendlyDummy);
        }

        void AdvancePlayerSlot(byte slot)
        {
            var startTimestamp = SimulationTrace.StartTimestamp();
            _host.NetworkPlayers.AdvancePlayableNetworkPlayer(slot);
            if (_host.LastToDieState.StageNumber > 0 && _host.IsNetworkPlayerActive(slot)
                && _host.NetworkPlayers.TryGetNetworkPlayer(slot, out var survivor))
            {
                survivor.AdvanceLastToDieSurvivorRegeneration(_host.Config.TicksPerSecond);
            }
            TraceSlowPlayer(slot, startTimestamp);
            if (playerTimingSlots is not null && playerTimingMilliseconds is not null)
            {
                playerTimingSlots[timingIndex] = slot;
                playerTimingMilliseconds[timingIndex] = SimulationTrace.ElapsedMilliseconds(startTimestamp);
                timingIndex += 1;
            }
        }
    }

    public void AdvancePostPlayerEntityPhase()
    {
        _host.Movement.AdvanceMovingPlatforms();
        _host.Pickups.AdvanceHealthPacks();
        _host.AdvanceCivvieMoneyPickups();
        _host.Pickups.AdvanceDroppedWeapons();
        _host.CombatFeedback.AdvanceAfterburnAlertBubbles();
        _host.Structures.AdvanceSentries();
        _host.Structures.UpdateDispenserAuras();
        _host.Movement.AdvanceJumpPads();
    }

    private void TracePlayerPhaseBreakdown(long startTimestamp, byte[]? slots, double[]? timings)
    {
        if (!SimulationTrace.Enabled
            || startTimestamp == 0L
            || slots is null
            || timings is null)
        {
            return;
        }

        var totalMilliseconds = SimulationTrace.ElapsedMilliseconds(startTimestamp);
        if (totalMilliseconds < SimulationTrace.PlayerThresholdMilliseconds)
        {
            return;
        }

        var builder = new System.Text.StringBuilder();
        builder.Append(DateTime.Now.ToString("O", CultureInfo.InvariantCulture));
        builder.Append(" frame=");
        builder.Append(_host.Frame.ToString(CultureInfo.InvariantCulture));
        builder.Append(" totalMs=");
        builder.Append(totalMilliseconds.ToString("0.0", CultureInfo.InvariantCulture));
        builder.Append(" players=");
        for (var index = 0; index < slots.Length; index += 1)
        {
            if (index > 0)
            {
                builder.Append('|');
            }

            var slot = slots[index];
            var className = _host.NetworkPlayers.TryGetNetworkPlayer(slot, out var player)
                ? player.ClassId.ToString()
                : "missing";
            builder.Append(slot.ToString(CultureInfo.InvariantCulture));
            builder.Append(':');
            builder.Append(className);
            builder.Append('=');
            builder.Append(timings[index].ToString("0.0", CultureInfo.InvariantCulture));
        }

        builder.AppendLine();
        SimulationTrace.Append(SimulationTraceLog.PlayerBreakdowns, builder.ToString());
    }

    private void TraceSlowPlayer(byte slot, long startTimestamp)
    {
        if (!SimulationTrace.Enabled || startTimestamp == 0L)
        {
            return;
        }

        var elapsedMilliseconds = SimulationTrace.ElapsedMilliseconds(startTimestamp);
        if (elapsedMilliseconds < SimulationTrace.PlayerThresholdMilliseconds)
        {
            return;
        }

        var className = _host.NetworkPlayers.TryGetNetworkPlayer(slot, out var player)
            ? player.ClassId.ToString()
            : "missing";
        var line = string.Create(
            CultureInfo.InvariantCulture,
            $"{DateTime.Now:O} frame={_host.Frame} slot={slot} class={className} elapsedMs={elapsedMilliseconds:0.0}{Environment.NewLine}");
        SimulationTrace.Append(SimulationTraceLog.PlayerSpikes, line);
    }
}
