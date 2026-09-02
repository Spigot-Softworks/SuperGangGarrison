#nullable enable

using Microsoft.Xna.Framework.Audio;
using System;
using System.Collections.Generic;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;
using OpenGarrison.Protocol;

namespace OpenGarrison.Client;

internal sealed class BuffBannerReadyCueTracker
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

public partial class Game1
{
    private sealed class GameplayAudioEventController
    {
        private const float LocalBuffBannerReadyCueVolume = 0.9f;
        private const float LocalBuffBannerReadyCueEchoSuppressionSeconds = 2f;
        private readonly Game1 _game;

        public GameplayAudioEventController(Game1 game)
        {
            _game = game;
        }

        public void PlayDeathCamSoundIfNeeded()
        {
            if (!_game._audioAvailable)
            {
                return;
            }

            if (_game.IsLastToDieDeathFocusPresentationActive())
            {
                return;
            }

            if (!_game._killCamEnabled || _game._world.LocalPlayer.IsAlive || _game._world.LocalDeathCam is null)
            {
                return;
            }

            var deathCam = _game._world.LocalDeathCam;
            if (Game1.GetDeathCamElapsedTicks(deathCam) < DeathCamFocusDelayTicks || _game._wasDeathCamActive)
            {
                return;
            }

            var sound = _game._runtimeAssets.GetSound("DeathCamSnd");
            _game.TryPlaySound(sound, 0.6f, 0f, 0f);
        }

        public void PlayDemoknightChargeReadySoundIfNeeded()
        {
            var player = _game._world.LocalPlayer;
            var currentChargeTicks = player.IsExperimentalDemoknightEnabled && player.IsAlive
                ? player.ExperimentalDemoknightChargeTicksRemaining
                : PlayerEntity.ExperimentalDemoknightChargeMaxTicks;
            var reachedReadyThisTick = player.IsExperimentalDemoknightEnabled
                && player.IsAlive
                && !player.IsExperimentalDemoknightCharging
                && _game._previousLocalDemoknightChargeTicks < PlayerEntity.ExperimentalDemoknightChargeMaxTicks
                && currentChargeTicks >= PlayerEntity.ExperimentalDemoknightChargeMaxTicks;

            _game._previousLocalDemoknightChargeTicks = currentChargeTicks;
            if (!reachedReadyThisTick || !_game._audioAvailable)
            {
                return;
            }

            var sound = _game._runtimeAssets.GetSound(ExperimentalDemoknightCatalog.ChargeReadySoundName);
            _game.TryPlaySound(sound, 0.8f, 0f, 0f);
        }

        public void PlayBuffBannerReadySoundIfNeeded()
        {
            _game._localBuffBannerReadyCueEchoSuppressionSeconds = Math.Max(
                0f,
                _game._localBuffBannerReadyCueEchoSuppressionSeconds
                    - _game._gameplayPresentationDeltaSeconds);
            var player = _game._world.LocalPlayer;
            var eligible = player.IsAlive
                && player.ClassId == PlayerClass.Soldier
                && player.HasGameplayAbilityBehavior(
                    GameplayAbilityConstants.UtilityChannel,
                    BuiltInGameplayBehaviorIds.SoldierBuffBanner);
            if (!_game._localBuffBannerReadyCueTracker.Observe(
                    eligible,
                    _game.GetPlayerBuffBannerChargeDamage(player),
                    _game.GetPlayerBuffBannerMaxChargeDamage(player))
                || !_game._audioAvailable)
            {
                return;
            }

            var sound = _game._runtimeAssets?.GetSound(PlayerEntity.BuffBannerReadySoundName);
            if (sound is not null
                && _game.TryPlaySound(sound, LocalBuffBannerReadyCueVolume, 0f, 0f))
            {
                _game._localBuffBannerReadyCueEchoSuppressionSeconds =
                    LocalBuffBannerReadyCueEchoSuppressionSeconds;
            }
        }

        public void ResetBuffBannerReadySoundObservation()
        {
            _game._localBuffBannerReadyCueTracker.Reset();
            _game._localBuffBannerReadyCueEchoSuppressionSeconds = 0f;
        }

        public void PlayRoundEndSoundIfNeeded()
        {
            if (!_game._audioAvailable)
            {
                return;
            }

            if (!_game._world.MatchState.IsEnded || _game._wasMatchEnded)
            {
                return;
            }

            var soundName = _game._world.MatchState.WinnerTeam switch
            {
                PlayerTeam.Red when _game._world.LocalPlayer.Team == PlayerTeam.Red => "VictorySnd",
                PlayerTeam.Blue when _game._world.LocalPlayer.Team == PlayerTeam.Blue => "VictorySnd",
                null => "FailureSnd",
                _ => "FailureSnd",
            };

            _game.StopIngameMusic();
            _game.StopLastToDieIngameMusic();

            var sound = _game._runtimeAssets.GetSound(soundName);
            _game.TryPlaySound(sound, 0.8f, 0f, 0f);
        }

        public void PlayKillFeedAnnouncementSounds()
        {
            if (!_game._audioAvailable || _game._mainMenuOpen || _game.IsLastToDieFailurePresentationActive())
            {
                return;
            }

            for (var index = 0; index < _game._world.KillFeed.Count; index += 1)
            {
                var entry = _game._world.KillFeed[index];
                if (entry.EventId == 0
                    || entry.SpecialType == OpenGarrison.Core.KillFeedSpecialType.None
                    || !Game1.ShouldProcessNetworkEvent(entry.EventId, _game._processedKillFeedEventIds, _game._processedKillFeedEventOrder))
                {
                    continue;
                }

                var localPlayerId = _game.GetResolvedLocalPlayerId();
                if (entry.KillerPlayerId != localPlayerId && entry.VictimPlayerId != localPlayerId)
                {
                    continue;
                }

                var soundName = entry.SpecialType == OpenGarrison.Core.KillFeedSpecialType.Domination
                    ? "DominationSnd"
                    : "RevengeSnd";
                var sound = _game._runtimeAssets.GetSound(soundName);
                _game.TryPlaySound(sound, 0.85f, 0f, 0f);
            }
        }

        public void PlayPendingSoundEvents()
        {
            _game.BeginExplosionSoundDeduplicationFrame();
            ReplayPendingBrowserSoundEvents();
            _game.AdvanceRecentGibSoundEvents();
            _game.AdvanceRecentProjectileSoundEvents();
            _game.AdvanceLowPriorityWorldSoundThrottle();
            _game.AdvanceLocalWeaponSoundFocus();

            if (_game._pendingNetworkSoundEvents.Count > 1)
            {
                _game._pendingNetworkSoundEvents.Sort((left, right) => GetSoundEventPlaybackPriority(left).CompareTo(GetSoundEventPlaybackPriority(right)));
            }

            var retainedNetworkSoundCount = 0;
            for (var index = 0; index < _game._pendingNetworkSoundEvents.Count; index += 1)
            {
                var soundEvent = _game._pendingNetworkSoundEvents[index];
                if (ProcessPendingSoundEvent(soundEvent))
                {
                    continue;
                }

                _game._pendingNetworkSoundEvents[retainedNetworkSoundCount++] = soundEvent;
            }

            if (retainedNetworkSoundCount == 0)
            {
                _game._pendingNetworkSoundEvents.Clear();
            }
            else if (retainedNetworkSoundCount < _game._pendingNetworkSoundEvents.Count)
            {
                _game._pendingNetworkSoundEvents.RemoveRange(
                    retainedNetworkSoundCount,
                    _game._pendingNetworkSoundEvents.Count - retainedNetworkSoundCount);
            }

            var worldSoundEvents = _game._world.DrainPendingSoundEvents();
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
                for (var index = 0; index < _game._pendingNetworkSoundEvents.Count; index += 1)
                {
                    if (_game._pendingNetworkSoundEvents[index].EventId == soundEvent.EventId)
                    {
                        return;
                    }
                }
            }

            _game._pendingNetworkSoundEvents.Add(soundEvent);
        }

        private int GetSoundEventPlaybackPriority(WorldSoundEvent soundEvent)
        {
            return _game.IsLocalPlayerSoundSource(soundEvent.SourcePlayerId)
                && Game1.IsWeaponFireSoundName(soundEvent.SoundName)
                    ? 0
                    : 1;
        }

        private bool ProcessPendingSoundEvent(WorldSoundEvent soundEvent)
        {
            if (Game1.HasProcessedNetworkEvent(soundEvent.EventId, _game._processedNetworkSoundEventIds))
            {
                return true;
            }

            if (ShouldSuppressLocalBuffBannerReadySoundEcho(soundEvent))
            {
                _game._localBuffBannerReadyCueEchoSuppressionSeconds = 0f;
                return CompleteSoundEvent(soundEvent);
            }

            if (string.Equals(soundEvent.SoundName, "ExplosionSnd", StringComparison.OrdinalIgnoreCase)
                && !_game.HasPresentedExplosionVisualThisFrame(soundEvent.X, soundEvent.Y)
                && !_game.HasPresentedExplosionVisualForSoundEvent(soundEvent)
                && _game.TryCreateExplosionVisual(soundEvent, out var explosion))
            {
                _game._explosions.Add(explosion!);
                _game.RememberPresentedExplosionVisualForSoundEvent(soundEvent);
            }

            if (!_game._audioAvailable)
            {
                return CompleteSoundEvent(soundEvent);
            }

            if (_game._runtimeAssets is null)
            {
                return false;
            }

            if (_game.ShouldSuppressManagedRapidFireSound(soundEvent))
            {
                return CompleteSoundEvent(soundEvent);
            }

            if (_game.ShouldSuppressPredictedGibSoundEcho(soundEvent))
            {
                return CompleteSoundEvent(soundEvent);
            }

            var resolvedSoundName = string.Equals(soundEvent.SoundName, "HealExplosionSnd", StringComparison.OrdinalIgnoreCase)
                ? "ExplosionSnd"
                : soundEvent.SoundName;
            if (_game._runtimeAssets is null)
            {
                return false;
            }

            var isExplosionSound = string.Equals(resolvedSoundName, "ExplosionSnd", StringComparison.OrdinalIgnoreCase);
            if (isExplosionSound && _game.HasPlayedExplosionSoundThisFrame(soundEvent.X, soundEvent.Y))
            {
                return CompleteSoundEvent(soundEvent);
            }

            if (_game.ShouldSuppressPredictedProjectileSoundEcho(resolvedSoundName, soundEvent))
            {
                return CompleteSoundEvent(soundEvent);
            }

            if (_game.ShouldThrottleLowPriorityWorldSound(resolvedSoundName, soundEvent))
            {
                return CompleteSoundEvent(soundEvent);
            }

            if (!TryPlayResolvedWorldSound(resolvedSoundName, soundEvent, allowBrowserDefer: OperatingSystem.IsBrowser()))
            {
                return false;
            }

            _game.NotifyClientPluginsWorldSound(soundEvent);
            Game1.MarkProcessedNetworkEvent(soundEvent.EventId, _game._processedNetworkSoundEventIds, _game._processedNetworkSoundEventOrder);
            _game.ForgetPresentedExplosionVisualForSoundEvent(soundEvent);
            _game.RememberPlayedLowPriorityWorldSound(resolvedSoundName, soundEvent);
            _game.TriggerLocalConfirmedWeaponFireFeedback(resolvedSoundName, soundEvent);
            _game.RememberPlayedProjectileSound(resolvedSoundName, soundEvent);
            if (isExplosionSound)
            {
                _game.RecordPlayedExplosionSoundThisFrame(soundEvent.X, soundEvent.Y);
                return true;
            }

            _game.RememberPlayedGibSound(soundEvent);
            return true;
        }

        private bool CompleteSoundEvent(WorldSoundEvent soundEvent)
        {
            _game.NotifyClientPluginsWorldSound(soundEvent);
            Game1.MarkProcessedNetworkEvent(soundEvent.EventId, _game._processedNetworkSoundEventIds, _game._processedNetworkSoundEventOrder);
            _game.ForgetPresentedExplosionVisualForSoundEvent(soundEvent);
            return true;
        }

        private void ReplayPendingBrowserSoundEvents()
        {
            if (!OperatingSystem.IsBrowser() || !_game._audioAvailable || _game._pendingBrowserSoundEvents.Count == 0)
            {
                return;
            }

            for (var index = _game._pendingBrowserSoundEvents.Count - 1; index >= 0; index -= 1)
            {
                var pendingSound = _game._pendingBrowserSoundEvents[index];
                if (TryPlayResolvedWorldSound(pendingSound.SoundName, pendingSound.X, pendingSound.Y, allowBrowserDefer: false))
                {
                    _game._pendingBrowserSoundEvents.RemoveAt(index);
                    continue;
                }

                pendingSound.TicksRemaining -= 1;
                if (pendingSound.TicksRemaining <= 0)
                {
                    _game._pendingBrowserSoundEvents.RemoveAt(index);
                }
            }
        }

        private bool TryPlayResolvedWorldSound(string resolvedSoundName, float worldX, float worldY, bool allowBrowserDefer)
        {
            var sound = _game._runtimeAssets?.GetSound(resolvedSoundName);
            if (sound is null)
            {
                if (allowBrowserDefer)
                {
                    _game.EnqueuePendingBrowserSoundEvent(resolvedSoundName, worldX, worldY);
                    return true;
                }

                return false;
            }

            var (volume, pan) = _game.GetWorldSoundMix(worldX, worldY);
            if (volume <= 0f)
            {
                return true;
            }

            return _game.TryPlaySound(sound, volume, 0f, pan);
        }

        private bool ShouldSuppressLocalBuffBannerReadySoundEcho(WorldSoundEvent soundEvent)
        {
            var player = _game._world.LocalPlayer;
            return _game._localBuffBannerReadyCueEchoSuppressionSeconds > 0f
                && string.Equals(
                    soundEvent.SoundName,
                    PlayerEntity.BuffBannerReadySoundName,
                    StringComparison.OrdinalIgnoreCase)
                && _game.IsLocalPlayerSoundSource(soundEvent.SourcePlayerId)
                && player.ClassId == PlayerClass.Soldier
                && player.HasGameplayAbilityBehavior(
                    GameplayAbilityConstants.UtilityChannel,
                    BuiltInGameplayBehaviorIds.SoldierBuffBanner);
        }

        private bool TryPlayResolvedWorldSound(string resolvedSoundName, WorldSoundEvent soundEvent, bool allowBrowserDefer)
        {
            var sound = _game._runtimeAssets?.GetSound(resolvedSoundName);
            if (sound is null)
            {
                if (allowBrowserDefer)
                {
                    _game.EnqueuePendingBrowserSoundEvent(resolvedSoundName, soundEvent.X, soundEvent.Y);
                    return true;
                }

                return false;
            }

            var (volume, pan) = _game.GetWorldSoundMix(soundEvent);
            if (volume <= 0f)
            {
                return true;
            }

            return _game.TryPlaySound(sound, volume, 0f, pan);
        }
    }
}
