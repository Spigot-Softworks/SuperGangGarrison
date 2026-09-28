using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private sealed class RuntimeEntityPhaseController
    {
        private static readonly bool SlowPlayerTracingEnabled =
            Environment.GetEnvironmentVariable("OG_CLIENT_PERF_SIM_TRACE") is "1" or "true" or "TRUE";
        private static readonly double SlowPlayerThresholdMilliseconds = ResolveSlowPlayerThresholdMilliseconds();
        private static readonly string? SlowPlayerTracePath = SlowPlayerTracingEnabled
            ? RuntimePaths.GetLogPath($"simulation-player-spikes-{DateTime.Now.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture)}.log")
            : null;
        private static readonly string? SlowPlayerBreakdownTracePath = SlowPlayerTracingEnabled
            ? RuntimePaths.GetLogPath($"simulation-player-breakdowns-{DateTime.Now.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture)}.log")
            : null;
        private static readonly string? SlowPhaseTracePath = SlowPlayerTracingEnabled
            ? RuntimePaths.GetLogPath($"simulation-phase-spikes-{DateTime.Now.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture)}.log")
            : null;
        private static readonly object SlowPlayerTraceSync = new();
        private readonly SimulationWorld _world;

        public RuntimeEntityPhaseController(SimulationWorld world)
        {
            _world = world;
        }

        public void AdvanceProjectileAndTransientEntityPhase()
        {
            if (!HasProjectileOrTransientStateToAdvance())
            {
                return;
            }

            if (SlowPlayerTracingEnabled)
            {
                AdvanceProjectileAndTransientEntityPhaseWithTracing();
                return;
            }

            _world.AdvanceCombatTraces();
            _world.ComputeSniperAimIndicators();
            _world.Projectiles.AdvanceShots();
            _world.Projectiles.AdvanceBubbles();
            _world.Projectiles.AdvanceBlades();
            _world.Projectiles.AdvanceNeedles();
            _world.Projectiles.AdvanceRevolverShots();
            _world.Projectiles.AdvanceStabAnimations();
            _world.Projectiles.AdvanceStabMasks();
            _world.Projectiles.AdvanceFlames();
            _world.Projectiles.AdvanceFlares();
            _world.Projectiles.AdvanceRockets();
            _world.Projectiles.AdvanceMines();
            _world.Projectiles.AdvanceGrenades();
            _world.AdvancePlayerGibs();
            _world.AdvanceBloodDrops();
            _world.AdvanceDeadBodies();
            _world.AdvanceSentryGibs();
            _world.AdvanceJumpPadGibs();
        }

        private bool HasProjectileOrTransientStateToAdvance()
        {
            if (_world.CombatTraces.Count > 0
                || _world.SniperAimIndicators.Count > 0
                || _world.Shots.Count > 0
                || _world.Bubbles.Count > 0
                || _world.Blades.Count > 0
                || _world.Needles.Count > 0
                || _world.RevolverShots.Count > 0
                || _world.StabAnimations.Count > 0
                || _world.StabMasks.Count > 0
                || _world.Flames.Count > 0
                || _world.Flares.Count > 0
                || _world.Rockets.Count > 0
                || _world.Mines.Count > 0
                || _world.Grenades.Count > 0
                || _world.PlayerGibs.Count > 0
                || _world.BloodDrops.Count > 0
                || _world.DeadBodies.Count > 0
                || _world.SentryGibs.Count > 0
                || _world.JumpPadGibs.Count > 0)
            {
                return true;
            }

            // An enabled aim indicator is generated from scoped players rather
            // than from a projectile collection, so retain that phase only for
            // the frames where a scoped rifle actually needs one.
            if (!_world.SniperAimIndicatorEnabled)
            {
                return false;
            }

            if (_world.LocalPlayer.IsAlive
                && _world.LocalPlayer.IsSniperScoped
                && !_world.LocalPlayer.IsSniperBowEquipped)
            {
                return true;
            }

            foreach (var slot in _world._enabledAdditionalNetworkPlayerSlots)
            {
                if (_world.TryGetNetworkPlayer(slot, out var player)
                    && player.IsAlive
                    && player.IsSniperScoped
                    && !player.IsSniperBowEquipped)
                {
                    return true;
                }
            }

            if (_world.EnemyPlayerEnabled
                && _world.EnemyPlayer.IsAlive
                && _world.EnemyPlayer.IsSniperScoped
                && !_world.EnemyPlayer.IsSniperBowEquipped)
            {
                return true;
            }

            return _world.FriendlyDummyEnabled
                && _world.FriendlyDummy.IsAlive
                && _world.FriendlyDummy.IsSniperScoped
                && !_world.FriendlyDummy.IsSniperBowEquipped;
        }

        private void AdvanceProjectileAndTransientEntityPhaseWithTracing()
        {
            AdvanceTimedPhase("combatTraces", _world.AdvanceCombatTraces);
            AdvanceTimedPhase("sniperIndicators", _world.ComputeSniperAimIndicators);
            AdvanceTimedPhase("shots", _world.Projectiles.AdvanceShots);
            AdvanceTimedPhase("bubbles", _world.Projectiles.AdvanceBubbles);
            AdvanceTimedPhase("blades", _world.Projectiles.AdvanceBlades);
            AdvanceTimedPhase("needles", _world.Projectiles.AdvanceNeedles);
            AdvanceTimedPhase("revolverShots", _world.Projectiles.AdvanceRevolverShots);
            AdvanceTimedPhase("stabAnimations", _world.Projectiles.AdvanceStabAnimations);
            AdvanceTimedPhase("stabMasks", _world.Projectiles.AdvanceStabMasks);
            AdvanceTimedPhase("flames", _world.Projectiles.AdvanceFlames);
            AdvanceTimedPhase("flares", _world.Projectiles.AdvanceFlares);
            AdvanceTimedPhase("rockets", _world.Projectiles.AdvanceRockets);
            AdvanceTimedPhase("mines", _world.Projectiles.AdvanceMines);
            AdvanceTimedPhase("grenades", _world.Projectiles.AdvanceGrenades);
            AdvanceTimedPhase("playerGibs", _world.AdvancePlayerGibs);
            AdvanceTimedPhase("bloodDrops", _world.AdvanceBloodDrops);
            AdvanceTimedPhase("deadBodies", _world.AdvanceDeadBodies);
            AdvanceTimedPhase("sentryGibs", _world.AdvanceSentryGibs);
            AdvanceTimedPhase("jumpPadGibs", _world.AdvanceJumpPadGibs);
        }

        private void AdvanceTimedPhase(string name, Action advance)
        {
            var startTimestamp = Stopwatch.GetTimestamp();
            advance();
            var elapsedMilliseconds = ElapsedMilliseconds(startTimestamp);
            if (elapsedMilliseconds < SlowPlayerThresholdMilliseconds
                || string.IsNullOrWhiteSpace(SlowPhaseTracePath))
            {
                return;
            }

            var line = string.Create(
                CultureInfo.InvariantCulture,
                $"{DateTime.Now:O} frame={_world.Frame} phase={name} elapsedMs={elapsedMilliseconds:0.0}{Environment.NewLine}");
            lock (SlowPlayerTraceSync)
            {
                File.AppendAllText(SlowPhaseTracePath, line);
            }
        }


        public void AdvanceRemoteSnapshotPlayerTauntStates()
        {
            _world.AdvanceRemoteSnapshotPlayerTauntStates();
        }

        public void AdvancePlayerSimulationPhase()
        {
            _world.ApplyBuffBannerRegeneration();
            _world.UpdateDispenserAuras();
            _world.UpdateBuffBannerAuras();
            var phaseStartTimestamp = SlowPlayerTracingEnabled ? Stopwatch.GetTimestamp() : 0L;
            var enabledAdditionalSlots = _world._enabledAdditionalNetworkPlayerSlots;
            byte[]? playerTimingSlots = SlowPlayerTracingEnabled ? new byte[1 + enabledAdditionalSlots.Count] : null;
            double[]? playerTimingMilliseconds = SlowPlayerTracingEnabled ? new double[1 + enabledAdditionalSlots.Count] : null;
            var timingIndex = 0;

            AdvancePlayerSlot(SimulationWorld.LocalPlayerSlot);
            foreach (var slot in enabledAdditionalSlots)
            {
                AdvancePlayerSlot(slot);
            }

            TracePlayerPhaseBreakdown(phaseStartTimestamp, playerTimingSlots, playerTimingMilliseconds);
            _world.UpdateBuffBannerAuras();

            // Taunt frames are advanced inside PlayerEntity.AdvanceTickState during full
            // simulation. AdvanceRemoteSnapshotPlayerTauntStates is client-prediction only.
            _world.AdvanceEnemyDummy();

            if (_world.FriendlyDummyEnabled && _world.FriendlyDummy.IsAlive)
            {
                _world.ApplyRoomForces(_world.FriendlyDummy);
                _world.FriendlyDummy.Advance(default, false, _world.Level, _world.FriendlyDummy.Team, _world.Config.FixedDeltaSeconds);
                _world.UpdateSpawnRoomState(_world.FriendlyDummy);
                _world.TryActivatePendingSpyBackstab(_world.FriendlyDummy);
                _world.ApplyHealingCabinets(_world.FriendlyDummy);
                _world.ApplyRoomHazards(_world.FriendlyDummy);
            }

            void AdvancePlayerSlot(byte slot)
            {
                var startTimestamp = SlowPlayerTracingEnabled ? Stopwatch.GetTimestamp() : 0L;
                _world.AdvancePlayableNetworkPlayer(slot);
                if (_world._lastToDieStageNumber > 0 && _world.IsNetworkPlayerActive(slot)
                    && _world.TryGetNetworkPlayer(slot, out var survivor))
                {
                    survivor.AdvanceLastToDieSurvivorRegeneration(_world.Config.TicksPerSecond);
                }
                TraceSlowPlayer(slot, startTimestamp);
                if (playerTimingSlots is not null && playerTimingMilliseconds is not null)
                {
                    playerTimingSlots[timingIndex] = slot;
                    playerTimingMilliseconds[timingIndex] = ElapsedMilliseconds(startTimestamp);
                    timingIndex += 1;
                }
            }
        }

        public void AdvancePostPlayerEntityPhase()
        {
            _world.AdvanceMovingPlatforms();
            _world.AdvanceHealthPacks();
            _world.AdvanceCivvieMoneyPickups();
            _world.AdvanceDroppedWeapons();
            _world.AdvanceAfterburnAlertBubbles();
            _world.AdvanceSentries();
            _world.UpdateDispenserAuras();
            _world.AdvanceJumpPads();
        }

        private static double ResolveSlowPlayerThresholdMilliseconds()
        {
            var configured = Environment.GetEnvironmentVariable("OG_CLIENT_PERF_SIM_PLAYER_TRACE_THRESHOLD_MS");
            return double.TryParse(configured, NumberStyles.Float, CultureInfo.InvariantCulture, out var threshold)
                ? Math.Max(0d, threshold)
                : 5d;
        }

        private static double ElapsedMilliseconds(long startTimestamp)
        {
            return startTimestamp == 0L
                ? 0d
                : (Stopwatch.GetTimestamp() - startTimestamp) * 1000d / Stopwatch.Frequency;
        }

        private void TracePlayerPhaseBreakdown(long startTimestamp, byte[]? slots, double[]? timings)
        {
            if (!SlowPlayerTracingEnabled
                || startTimestamp == 0L
                || slots is null
                || timings is null
                || string.IsNullOrWhiteSpace(SlowPlayerBreakdownTracePath))
            {
                return;
            }

            var totalMilliseconds = ElapsedMilliseconds(startTimestamp);
            if (totalMilliseconds < SlowPlayerThresholdMilliseconds)
            {
                return;
            }

            var builder = new System.Text.StringBuilder();
            builder.Append(DateTime.Now.ToString("O", CultureInfo.InvariantCulture));
            builder.Append(" frame=");
            builder.Append(_world.Frame.ToString(CultureInfo.InvariantCulture));
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
                var className = _world.TryGetNetworkPlayer(slot, out var player)
                    ? player.ClassId.ToString()
                    : "missing";
                builder.Append(slot.ToString(CultureInfo.InvariantCulture));
                builder.Append(':');
                builder.Append(className);
                builder.Append('=');
                builder.Append(timings[index].ToString("0.0", CultureInfo.InvariantCulture));
            }

            builder.AppendLine();
            lock (SlowPlayerTraceSync)
            {
                File.AppendAllText(SlowPlayerBreakdownTracePath, builder.ToString());
            }
        }

        private void TraceSlowPlayer(byte slot, long startTimestamp)
        {
            if (!SlowPlayerTracingEnabled || startTimestamp == 0L || string.IsNullOrWhiteSpace(SlowPlayerTracePath))
            {
                return;
            }

            var elapsedMilliseconds = (Stopwatch.GetTimestamp() - startTimestamp) * 1000d / Stopwatch.Frequency;
            if (elapsedMilliseconds < SlowPlayerThresholdMilliseconds)
            {
                return;
            }

            var className = _world.TryGetNetworkPlayer(slot, out var player)
                ? player.ClassId.ToString()
                : "missing";
            var line = string.Create(
                CultureInfo.InvariantCulture,
                $"{DateTime.Now:O} frame={_world.Frame} slot={slot} class={className} elapsedMs={elapsedMilliseconds:0.0}{Environment.NewLine}");
            lock (SlowPlayerTraceSync)
            {
                File.AppendAllText(SlowPlayerTracePath, line);
            }
        }
    }
}
