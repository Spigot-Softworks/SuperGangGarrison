#nullable enable

using Microsoft.Xna.Framework.Audio;
using System;
using OpenGarrison.Core;
using OpenGarrison.Protocol;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class GameplayAudioEventController
    {
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

            if (_game._ingameMusicInstance?.State == SoundState.Playing)
            {
                _game._ingameMusicInstance.Stop();
            }
            if (_game._lastToDieIngameMusicInstance?.State == SoundState.Playing)
            {
                _game._lastToDieIngameMusicInstance.Stop();
            }

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
            foreach (var soundEvent in _game._world.DrainPendingSoundEvents())
            {
                if (!Game1.ShouldProcessNetworkEvent(soundEvent.EventId, _game._processedNetworkSoundEventIds, _game._processedNetworkSoundEventOrder))
                {
                    continue;
                }

                if (string.Equals(soundEvent.SoundName, "ExplosionSnd", StringComparison.OrdinalIgnoreCase)
                    && _game.TryCreateExplosionVisual(soundEvent, out var explosion))
                {
                    _game._explosions.Add(explosion!);
                }

                _game.NotifyClientPluginsWorldSound(soundEvent);

                if (!_game._audioAvailable)
                {
                    continue;
                }

                if (_game.ShouldSuppressManagedLocalRapidFireSound(soundEvent))
                {
                    continue;
                }

                var resolvedSoundName = string.Equals(soundEvent.SoundName, "HealExplosionSnd", StringComparison.OrdinalIgnoreCase)
                    ? "ExplosionSnd"
                    : soundEvent.SoundName;
                var sound = _game._runtimeAssets.GetSound(resolvedSoundName);
                if (sound is null)
                {
                    continue;
                }

                var (volume, pan) = _game.GetWorldSoundMix(soundEvent.X, soundEvent.Y);
                if (volume <= 0f)
                {
                    continue;
                }

                _game.TryPlaySound(sound, volume, 0f, pan);
            }
        }
    }
}
