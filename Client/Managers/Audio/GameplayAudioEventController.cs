#nullable enable

using Microsoft.Xna.Framework.Audio;
using System;
using System.Collections.Generic;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;
using OpenGarrison.Protocol;

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class BuffBannerReadyCueTracker
{
    private bool _hasObservedEligibleState;
    private bool _playedForCurrentChargeCycle;

    public bool Observe(bool eligible, int chargeDamage, int maxChargeDamage)
    {
        if (!eligible)
        {
            Reset();
            return false;
        }

        var normalizedMaximum = Math.Max(1, maxChargeDamage);
        var normalizedCharge = Math.Clamp(chargeDamage, 0, normalizedMaximum);
        var ready = normalizedCharge >= normalizedMaximum;
        if (!_hasObservedEligibleState)
        {
            _hasObservedEligibleState = true;
            _playedForCurrentChargeCycle = ready;
            return false;
        }

        if ((long)normalizedCharge * 2 < normalizedMaximum)
        {
            _playedForCurrentChargeCycle = false;
        }

        if (!ready || _playedForCurrentChargeCycle)
        {
            return false;
        }

        _playedForCurrentChargeCycle = true;
        return true;
    }

    public void Reset()
    {
        _hasObservedEligibleState = false;
        _playedForCurrentChargeCycle = false;
    }
}

public sealed class GameplayAudioEventController
    {
        private const float LocalBuffBannerReadyCueVolume = 0.9f;
        private const float LocalBuffBannerReadyCueEchoSuppressionSeconds = 2f;
        private readonly IAudioContext _context;
        private readonly List<OpenGarrison.Core.WorldSoundEvent> _pendingNetworkSoundEvents = new();
        private readonly Queue<ulong> _processedKillFeedEventOrder = new();
        private readonly HashSet<ulong> _processedKillFeedEventIds = new();
        private readonly Queue<ulong> _processedNetworkSoundEventOrder = new();
        private readonly HashSet<ulong> _processedNetworkSoundEventIds = new();
        private float _localBuffBannerReadyCueEchoSuppressionSeconds;
        private readonly OpenGarrison.Client.BuffBannerReadyCueTracker _localBuffBannerReadyCueTracker = new();
        private int _previousLocalDemoknightChargeTicks = PlayerEntity.ExperimentalDemoknightChargeMaxTicks;

        public GameplayAudioEventController(IAudioContext context)
        {
            _context = context;
        }

        internal void QueuePendingNetworkSoundEvent(WorldSoundEvent soundEvent)
        {
            _pendingNetworkSoundEvents.Add(soundEvent);
        }

        internal void ClearPendingNetworkSoundEvents()
        {
            _pendingNetworkSoundEvents.Clear();
        }

        internal void ResetProcessedNetworkSoundEventHistory()
        {
            _processedNetworkSoundEventIds.Clear();
            _processedNetworkSoundEventOrder.Clear();
        }

        internal void ResetProcessedKillFeedEventHistory()
        {
            _processedKillFeedEventIds.Clear();
            _processedKillFeedEventOrder.Clear();
        }

        internal bool HasProcessedNetworkSoundEvent(ulong eventId)
        {
            return Game1.HasProcessedNetworkEvent(eventId, _processedNetworkSoundEventIds);
        }

        public void PlayDeathCamSoundIfNeeded()
        {
            if (!_context._audioAvailable)
            {
                return;
            }

            if (_context.IsLastToDieDeathFocusPresentationActive())
            {
                return;
            }

            if (!_context.GameplayRuntimeSettings.KillCamEnabled || _context._world.LocalPlayer.IsAlive || _context._world.LocalDeathCam is null)
            {
                return;
            }

            var deathCam = _context._world.LocalDeathCam;
            if (Game1.GetDeathCamElapsedTicks(deathCam) < DeathCamFocusDelayTicks || _context._wasDeathCamActive)
            {
                return;
            }

            var sound = _context._runtimeAssets.GetSound("DeathCamSnd");
            _context.TryPlaySound(sound, 0.6f, 0f, 0f);
        }

        public void PlayDemoknightChargeReadySoundIfNeeded()
        {
            var player = _context._world.LocalPlayer;
            var currentChargeTicks = player.IsExperimentalDemoknightEnabled && player.IsAlive
                ? player.ExperimentalDemoknightChargeTicksRemaining
                : PlayerEntity.ExperimentalDemoknightChargeMaxTicks;
            var reachedReadyThisTick = player.IsExperimentalDemoknightEnabled
                && player.IsAlive
                && !player.IsExperimentalDemoknightCharging
                && _previousLocalDemoknightChargeTicks < PlayerEntity.ExperimentalDemoknightChargeMaxTicks
                && currentChargeTicks >= PlayerEntity.ExperimentalDemoknightChargeMaxTicks;

            _previousLocalDemoknightChargeTicks = currentChargeTicks;
            if (!reachedReadyThisTick || !_context._audioAvailable)
            {
                return;
            }

            var sound = _context._runtimeAssets.GetSound(ExperimentalDemoknightCatalog.ChargeReadySoundName);
            _context.TryPlaySound(sound, 0.8f, 0f, 0f);
        }

        public void PlayBuffBannerReadySoundIfNeeded()
        {
            _localBuffBannerReadyCueEchoSuppressionSeconds = Math.Max(
                0f,
                _localBuffBannerReadyCueEchoSuppressionSeconds
                    - _context._gameplayPresentationDeltaSeconds);
            var player = _context._world.LocalPlayer;
            var eligible = player.IsAlive
                && player.ClassId == PlayerClass.Soldier
                && player.HasGameplayAbilityBehavior(
                    GameplayAbilityConstants.UtilityChannel,
                    BuiltInGameplayBehaviorIds.SoldierBuffBanner);
            if (!_localBuffBannerReadyCueTracker.Observe(
                    eligible,
                    _context.GetPlayerBuffBannerChargeDamage(player),
                    _context.GetPlayerBuffBannerMaxChargeDamage(player))
                || !_context._audioAvailable)
            {
                return;
            }

            var sound = _context._runtimeAssets?.GetSound(PlayerEntity.BuffBannerReadySoundName);
            if (sound is not null
                && _context.TryPlaySound(sound, LocalBuffBannerReadyCueVolume, 0f, 0f))
            {
                _localBuffBannerReadyCueEchoSuppressionSeconds =
                    LocalBuffBannerReadyCueEchoSuppressionSeconds;
            }
        }

        public void ResetBuffBannerReadySoundObservation()
        {
            _localBuffBannerReadyCueTracker.Reset();
            _localBuffBannerReadyCueEchoSuppressionSeconds = 0f;
        }

        public void PlayRoundEndSoundIfNeeded()
        {
            if (!_context._audioAvailable || _context.IsAnyLastToDieSessionActive)
            {
                return;
            }

            if (!_context._world.MatchState.IsEnded || _context._wasMatchEnded)
            {
                return;
            }

            var soundName = _context._world.MatchState.WinnerTeam switch
            {
                PlayerTeam.Red when _context._world.LocalPlayer.Team == PlayerTeam.Red => "VictorySnd",
                PlayerTeam.Blue when _context._world.LocalPlayer.Team == PlayerTeam.Blue => "VictorySnd",
                null => "FailureSnd",
                _ => "FailureSnd",
            };

            _context.StopIngameMusic();
            _context.StopLastToDieIngameMusic();

            var sound = _context._runtimeAssets.GetSound(soundName);
            _context.TryPlaySound(sound, 0.8f, 0f, 0f);
        }

        public void PlayKillFeedAnnouncementSounds()
        {
            if (!_context._audioAvailable || _context._mainMenuOpen || _context.IsLastToDieFailurePresentationActive())
            {
                return;
            }

            for (var index = 0; index < _context._world.KillFeed.Count; index += 1)
            {
                var entry = _context._world.KillFeed[index];
                if (entry.EventId == 0
                    || entry.SpecialType == OpenGarrison.Core.KillFeedSpecialType.None
                    || !Game1.ShouldProcessNetworkEvent(entry.EventId, _processedKillFeedEventIds, _processedKillFeedEventOrder))
                {
                    continue;
                }

                var localPlayerId = _context.GetResolvedLocalPlayerId();
                if (entry.KillerPlayerId != localPlayerId && entry.VictimPlayerId != localPlayerId)
                {
                    continue;
                }

                var soundName = entry.SpecialType == OpenGarrison.Core.KillFeedSpecialType.Domination
                    ? "DominationSnd"
                    : "RevengeSnd";
                var sound = _context._runtimeAssets.GetSound(soundName);
                _context.TryPlaySound(sound, 0.85f, 0f, 0f);
            }
        }

        public void PlayPendingSoundEvents()
        {
            _context.BeginExplosionSoundDeduplicationFrame();
            ReplayPendingBrowserSoundEvents();
            _context.AdvanceRecentGibSoundEvents();
            _context.AdvanceRecentProjectileSoundEvents();
            _context.AdvanceLowPriorityWorldSoundThrottle();
            _context.AdvanceLocalWeaponSoundFocus();

            if (_pendingNetworkSoundEvents.Count > 1)
            {
                _pendingNetworkSoundEvents.Sort((left, right) => GetSoundEventPlaybackPriority(left).CompareTo(GetSoundEventPlaybackPriority(right)));
            }

            var retainedNetworkSoundCount = 0;
            for (var index = 0; index < _pendingNetworkSoundEvents.Count; index += 1)
            {
                var soundEvent = _pendingNetworkSoundEvents[index];
                if (ProcessPendingSoundEvent(soundEvent))
                {
                    continue;
                }

                _pendingNetworkSoundEvents[retainedNetworkSoundCount++] = soundEvent;
            }

            if (retainedNetworkSoundCount == 0)
            {
                _pendingNetworkSoundEvents.Clear();
            }
            else if (retainedNetworkSoundCount < _pendingNetworkSoundEvents.Count)
            {
                _pendingNetworkSoundEvents.RemoveRange(
                    retainedNetworkSoundCount,
                    _pendingNetworkSoundEvents.Count - retainedNetworkSoundCount);
            }

            var worldSoundEvents = _context._world.DrainPendingSoundEvents();
            if (worldSoundEvents.Count > 1)
            {
                var sortedWorldSoundEvents = new List<WorldSoundEvent>(worldSoundEvents);
                sortedWorldSoundEvents.Sort((left, right) => GetSoundEventPlaybackPriority(left).CompareTo(GetSoundEventPlaybackPriority(right)));
                foreach (var soundEvent in sortedWorldSoundEvents)
                {
                    if (!ProcessPendingSoundEvent(soundEvent))
                    {
                        QueueSoundEventForRetry(soundEvent);
                    }
                }

                return;
            }

            foreach (var soundEvent in worldSoundEvents)
            {
                if (!ProcessPendingSoundEvent(soundEvent))
                {
                    QueueSoundEventForRetry(soundEvent);
                }
            }
        }

        private void QueueSoundEventForRetry(WorldSoundEvent soundEvent)
        {
            if (soundEvent.EventId != 0)
            {
                for (var index = 0; index < _pendingNetworkSoundEvents.Count; index += 1)
                {
                    if (_pendingNetworkSoundEvents[index].EventId == soundEvent.EventId)
                    {
                        return;
                    }
                }
            }

            _pendingNetworkSoundEvents.Add(soundEvent);
        }

        private int GetSoundEventPlaybackPriority(WorldSoundEvent soundEvent)
        {
            return _context.IsLocalPlayerSoundSource(soundEvent.SourcePlayerId)
                && Game1.IsWeaponFireSoundName(soundEvent.SoundName)
                    ? 0
                    : 1;
        }

        private bool ProcessPendingSoundEvent(WorldSoundEvent soundEvent)
        {
            if (Game1.HasProcessedNetworkEvent(soundEvent.EventId, _processedNetworkSoundEventIds))
            {
                return true;
            }

            // Impact sounds can supply fallback explosion art. Keep that path
            // on the projectile/body timeline too, before creating the visual.
            if (soundEvent.EventId != 0
                && (string.Equals(soundEvent.SoundName, "ExplosionSnd", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(soundEvent.SoundName, "FlareImpactSnd", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(soundEvent.SoundName, "AirblastSnd", StringComparison.OrdinalIgnoreCase))
                && !NetworkInterpolationPolicy.IsSourceFrameReady(
                    soundEvent.SourceFrame, _context._config.TicksPerSecond, _context.GetProjectileRenderTimeSeconds()))
                return false;

            if (ShouldSuppressLocalBuffBannerReadySoundEcho(soundEvent))
            {
                _localBuffBannerReadyCueEchoSuppressionSeconds = 0f;
                return CompleteSoundEvent(soundEvent);
            }

            if (string.Equals(soundEvent.SoundName, "ExplosionSnd", StringComparison.OrdinalIgnoreCase)
                && !_context.HasPresentedExplosionVisualThisFrame(soundEvent.X, soundEvent.Y)
                && !_context.HasPresentedExplosionVisualForSoundEvent(soundEvent)
                && _context.TryCreateExplosionVisual(soundEvent, out var explosion))
            {
                var shouldPresentAuthoritativeExplosion = _context.ShouldPresentAuthoritativeExplosionSound(soundEvent);
                var isPredictedExplosionEcho = soundEvent.EventId != 0
                    && _context.HasRecentPredictedExplosionVisual(soundEvent.X, soundEvent.Y);
                if (shouldPresentAuthoritativeExplosion && !isPredictedExplosionEcho)
                {
                    _context._explosions.Add(explosion!);
                }

                // Mark the fallback decision even when another channel already
                // presented it. Browser audio may retry this event while its
                // sound asset is loading, and it must not reconsider the art.
                _context.RememberPresentedExplosionVisualForSoundEvent(soundEvent);
            }

            if (!_context._audioAvailable)
            {
                return CompleteSoundEvent(soundEvent);
            }

            if (_context._runtimeAssets is null)
            {
                return false;
            }

            if (_context.ShouldSuppressManagedRapidFireSound(soundEvent))
            {
                return CompleteSoundEvent(soundEvent);
            }

            if (_context.ShouldSuppressPredictedGibSoundEcho(soundEvent))
            {
                return CompleteSoundEvent(soundEvent);
            }

            var resolvedSoundName = string.Equals(soundEvent.SoundName, "HealExplosionSnd", StringComparison.OrdinalIgnoreCase)
                ? "ExplosionSnd"
                : soundEvent.SoundName;
            if (_context._runtimeAssets is null)
            {
                return false;
            }

            var isExplosionSound = string.Equals(resolvedSoundName, "ExplosionSnd", StringComparison.OrdinalIgnoreCase);
            if (isExplosionSound && _context.HasPlayedExplosionSoundThisFrame(soundEvent.X, soundEvent.Y))
            {
                return CompleteSoundEvent(soundEvent);
            }

            if (_context.ShouldSuppressPredictedProjectileSoundEcho(resolvedSoundName, soundEvent))
            {
                return CompleteSoundEvent(soundEvent);
            }

            if (_context.ShouldThrottleLowPriorityWorldSound(resolvedSoundName, soundEvent))
            {
                return CompleteSoundEvent(soundEvent);
            }

            if (!TryPlayResolvedWorldSound(
                    soundEvent with { SoundName = resolvedSoundName },
                    allowBrowserDefer: OperatingSystem.IsBrowser(),
                    out var playbackSucceeded))
            {
                return false;
            }

            _context.NotifyClientPluginsWorldSound(soundEvent);
            Game1.MarkProcessedNetworkEvent(soundEvent.EventId, _processedNetworkSoundEventIds, _processedNetworkSoundEventOrder);
            _context.ForgetPresentedExplosionVisualForSoundEvent(soundEvent);
            _context.RememberPlayedLowPriorityWorldSound(resolvedSoundName, soundEvent);
            _context.TriggerLocalConfirmedWeaponFireFeedback(resolvedSoundName, soundEvent);
            _context.RememberPlayedProjectileSound(resolvedSoundName, soundEvent);
            if (isExplosionSound)
            {
                _context.RecordPlayedExplosionSoundThisFrame(soundEvent.X, soundEvent.Y);
                return true;
            }

            if (playbackSucceeded)
            {
                _context.RememberPlayedGibSound(soundEvent);
            }

            return true;
        }

        private bool CompleteSoundEvent(WorldSoundEvent soundEvent)
        {
            _context.NotifyClientPluginsWorldSound(soundEvent);
            Game1.MarkProcessedNetworkEvent(soundEvent.EventId, _processedNetworkSoundEventIds, _processedNetworkSoundEventOrder);
            _context.ForgetPresentedExplosionVisualForSoundEvent(soundEvent);
            return true;
        }

        private void ReplayPendingBrowserSoundEvents()
        {
            if (!OperatingSystem.IsBrowser() || !_context._audioAvailable || _context._pendingBrowserSoundEvents.Count == 0)
            {
                return;
            }

            for (var index = _context._pendingBrowserSoundEvents.Count - 1; index >= 0; index -= 1)
            {
                var pendingSound = _context._pendingBrowserSoundEvents[index];
                if (string.Equals(pendingSound.SoundName, "Gibbing", StringComparison.OrdinalIgnoreCase)
                    && _context.ShouldSuppressPredictedGibSoundEcho(pendingSound.SoundEvent))
                {
                    _context._pendingBrowserSoundEvents.RemoveAt(index);
                    continue;
                }

                if (TryPlayResolvedWorldSound(
                        pendingSound.SoundEvent,
                        allowBrowserDefer: false,
                        out var playbackSucceeded))
                {
                    _context._pendingBrowserSoundEvents.RemoveAt(index);
                    if (playbackSucceeded
                        && string.Equals(pendingSound.SoundName, "Gibbing", StringComparison.OrdinalIgnoreCase))
                    {
                        _context.RememberPlayedGibSound(pendingSound.SoundEvent);
                    }

                    continue;
                }

                pendingSound.TicksRemaining -= 1;
                if (pendingSound.TicksRemaining <= 0)
                {
                    _context._pendingBrowserSoundEvents.RemoveAt(index);
                }
            }
        }

        private bool ShouldSuppressLocalBuffBannerReadySoundEcho(WorldSoundEvent soundEvent)
        {
            var player = _context._world.LocalPlayer;
            return _localBuffBannerReadyCueEchoSuppressionSeconds > 0f
                && string.Equals(
                    soundEvent.SoundName,
                    PlayerEntity.BuffBannerReadySoundName,
                    StringComparison.OrdinalIgnoreCase)
                && _context.IsLocalPlayerSoundSource(soundEvent.SourcePlayerId)
                && player.ClassId == PlayerClass.Soldier
                && player.HasGameplayAbilityBehavior(
                    GameplayAbilityConstants.UtilityChannel,
                    BuiltInGameplayBehaviorIds.SoldierBuffBanner);
        }

        private bool TryPlayResolvedWorldSound(
            WorldSoundEvent soundEvent,
            bool allowBrowserDefer,
            out bool playbackSucceeded)
        {
            playbackSucceeded = false;
            var sound = _context._runtimeAssets?.GetSound(soundEvent.SoundName);
            if (sound is null && string.Equals(soundEvent.SoundName, "FlareImpactSnd", StringComparison.OrdinalIgnoreCase))
            {
                sound = _context._runtimeAssets?.GetSound("DirecthitSnd");
            }
            if (sound is null)
            {
                if (allowBrowserDefer)
                {
                    _context.EnqueuePendingBrowserSoundEvent(soundEvent);
                    return true;
                }

                return false;
            }

            var (volume, pan) = Game1.UsesGlobalWorldSoundMix(soundEvent.SoundName)
                ? (1f, 0f)
                : _context.GetWorldSoundMix(soundEvent);
            if (volume <= 0f)
            {
                return true;
            }

            playbackSucceeded = _context.TryPlaySound(sound, volume, 0f, pan);
            return playbackSucceeded;
        }
}
