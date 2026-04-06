#nullable enable

using Microsoft.Xna.Framework.Audio;
using System;
using OpenGarrison.Core;
using OpenGarrison.Protocol;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class GameplayRapidFireAudioController
    {
        private readonly Game1 _game;

        public GameplayRapidFireAudioController(Game1 game)
        {
            _game = game;
        }

        public void UpdateLocalRapidFireWeaponAudio()
        {
            if (!_game._audioAvailable)
            {
                StopLocalRapidFireWeaponAudio();
                return;
            }

            UpdateLocalRapidFireWeaponAudio(
                PrimaryWeaponKind.Minigun,
                "ChaingunSnd",
                ref _game._localChaingunSoundInstance);
            UpdateLocalRapidFireWeaponAudio(
                PrimaryWeaponKind.FlameThrower,
                "FlamethrowerSnd",
                ref _game._localFlamethrowerSoundInstance);
            UpdateLocalRapidFireWeaponAudio(
                PrimaryWeaponKind.Medigun,
                "MedigunSnd",
                ref _game._localMedigunSoundInstance);
        }

        public bool IsLocalRapidFireWeaponSoundActive(PrimaryWeaponKind weaponKind)
        {
            if (_game._mainMenuOpen)
            {
                return false;
            }

            var player = _game._world.LocalPlayer;
            var activeWeaponKind = player.IsAcquiredWeaponPresented
                ? player.AcquiredWeapon?.Kind
                : player.PrimaryWeapon.Kind;
            if (_game._world.LocalPlayerAwaitingJoin
                || !player.IsAlive
                || player.IsTaunting
                || _game._world.MatchState.IsEnded
                || activeWeaponKind != weaponKind)
            {
                return false;
            }

            if (weaponKind == PrimaryWeaponKind.Minigun && _game.GetPlayerIsHeavyEating(player))
            {
                return false;
            }

            if (weaponKind == PrimaryWeaponKind.FlameThrower)
            {
                return player.PyroFlameLoopTicksRemaining > 0;
            }

            if (weaponKind == PrimaryWeaponKind.Medigun)
            {
                return player.IsMedicHealing && player.MedicHealTargetId.HasValue;
            }

            return player.PrimaryCooldownTicks > 0;
        }

        public bool ShouldSuppressManagedLocalRapidFireSound(WorldSoundEvent soundEvent)
        {
            var soundName = soundEvent.SoundName;
            if (string.Equals(soundName, "ChaingunSnd", StringComparison.OrdinalIgnoreCase))
            {
                return IsLocalRapidFireWeaponSoundActive(PrimaryWeaponKind.Minigun)
                    && AudioDistanceSquared(soundEvent.X, soundEvent.Y, _game._world.LocalPlayer.X, _game._world.LocalPlayer.Y) <= 576f;
            }

            if (string.Equals(soundName, "FlamethrowerSnd", StringComparison.OrdinalIgnoreCase))
            {
                return IsLocalRapidFireWeaponSoundActive(PrimaryWeaponKind.FlameThrower)
                    && AudioDistanceSquared(soundEvent.X, soundEvent.Y, _game._world.LocalPlayer.X, _game._world.LocalPlayer.Y) <= 576f;
            }

            return false;
        }

        public (float Volume, float Pan) GetWorldSoundMix(float worldX, float worldY)
        {
            var dx = worldX - _game._world.LocalPlayer.X;
            var dy = worldY - _game._world.LocalPlayer.Y;
            var distance = MathF.Sqrt(dx * dx + dy * dy);
            var volume = Math.Clamp(1f - (distance / 1200f), 0f, 1f) * 0.6f;
            var pan = Math.Clamp(dx / 600f, -1f, 1f);
            return (volume, pan);
        }

        public void StopLocalRapidFireWeaponAudio()
        {
            StopLocalRapidFireWeaponSound(ref _game._localChaingunSoundInstance);
            StopLocalRapidFireWeaponSound(ref _game._localFlamethrowerSoundInstance);
            StopLocalRapidFireWeaponSound(ref _game._localMedigunSoundInstance);
        }

        public void StopAndDisposeLocalRapidFireWeaponAudio()
        {
            StopAndDisposeLocalRapidFireWeaponSound(ref _game._localChaingunSoundInstance);
            StopAndDisposeLocalRapidFireWeaponSound(ref _game._localFlamethrowerSoundInstance);
            StopAndDisposeLocalRapidFireWeaponSound(ref _game._localMedigunSoundInstance);
        }

        private void UpdateLocalRapidFireWeaponAudio(
            PrimaryWeaponKind weaponKind,
            string soundName,
            ref SoundEffectInstance? instance)
        {
            if (!IsLocalRapidFireWeaponSoundActive(weaponKind))
            {
                StopLocalRapidFireWeaponSound(ref instance);
                return;
            }

            if (instance is null)
            {
                var sound = _game._runtimeAssets.GetSound(soundName);
                if (sound is null)
                {
                    return;
                }

                try
                {
                    instance = sound.CreateInstance();
                    instance.IsLooped = true;
                }
                catch (Exception ex)
                {
                    _game.DisableAudio($"starting {soundName}", ex);
                    return;
                }
            }

            var (volume, pan) = GetWorldSoundMix(_game._world.LocalPlayer.X, _game._world.LocalPlayer.Y);
            if (volume <= 0f)
            {
                StopLocalRapidFireWeaponSound(ref instance);
                return;
            }

            try
            {
                instance.Volume = volume;
                instance.Pan = pan;
                if (instance.State != SoundState.Playing)
                {
                    instance.Play();
                }
            }
            catch (Exception ex)
            {
                _game.DisableAudio($"maintaining {soundName}", ex);
            }
        }

        private static void StopLocalRapidFireWeaponSound(ref SoundEffectInstance? instance)
        {
            try
            {
                if (instance?.State == SoundState.Playing)
                {
                    instance.Stop();
                }
            }
            catch
            {
            }
        }

        private static void StopAndDisposeLocalRapidFireWeaponSound(ref SoundEffectInstance? instance)
        {
            try
            {
                if (instance?.State == SoundState.Playing)
                {
                    instance.Stop();
                }
            }
            catch
            {
            }

            try
            {
                instance?.Dispose();
            }
            catch
            {
            }

            instance = null;
        }

        private static float AudioDistanceSquared(float x1, float y1, float x2, float y2)
        {
            var deltaX = x2 - x1;
            var deltaY = y2 - y1;
            return (deltaX * deltaX) + (deltaY * deltaY);
        }
    }
}
