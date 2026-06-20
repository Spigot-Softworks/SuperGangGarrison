#nullable enable

using Microsoft.Xna.Framework.Audio;
using System;
using System.Collections.Generic;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;
using OpenGarrison.Protocol;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class GameplayRapidFireAudioController
    {
        private const string ChaingunSoundName = "ChaingunSnd";
        private const string FlamethrowerSoundName = "FlamethrowerSnd";
        private const string MedigunSoundName = "MedigunSnd";
        private const float ManagedRapidFireSoundSuppressionDistanceSquared = 576f;
        private const float WorldSoundFullVolumeDistance = 300f;
        private const float WorldSoundSilenceDistance = 1500f;
        private const float WorldSoundPanDistance = 400f;

        private readonly Game1 _game;
        private readonly Dictionary<RemoteRapidFireSoundKey, SoundEffectInstance> _remoteRapidFireSoundInstances = new();
        private readonly HashSet<RemoteRapidFireSoundKey> _activeRemoteRapidFireSoundKeys = new();
        private readonly List<RemoteRapidFireSoundKey> _staleRemoteRapidFireSoundKeys = new();

        public GameplayRapidFireAudioController(Game1 game)
        {
            _game = game;
        }

        public void UpdateLocalRapidFireWeaponAudio()
        {
            if (!_game._audioAvailable)
            {
                StopRapidFireWeaponAudio();
                return;
            }

            UpdateLocalRapidFireWeaponAudio(
                PrimaryWeaponKind.Minigun,
                ChaingunSoundName,
                ref _game._localChaingunSoundInstance);
            UpdateLocalRapidFireWeaponAudio(
                PrimaryWeaponKind.FlameThrower,
                FlamethrowerSoundName,
                ref _game._localFlamethrowerSoundInstance);
            UpdateLocalRapidFireWeaponAudio(
                PrimaryWeaponKind.Medigun,
                MedigunSoundName,
                ref _game._localMedigunSoundInstance);
            if (_game._networkClient.IsReplayConnection)
            {
                StopAndDisposeRemoteRapidFireWeaponAudio();
            }
            else
            {
                UpdateRemoteRapidFireWeaponAudio();
            }
            UpdateLocalUberIdleAudio();
        }

        public bool IsLocalRapidFireWeaponSoundActive(PrimaryWeaponKind weaponKind)
        {
            if (_game._mainMenuOpen)
            {
                return false;
            }

            var player = _game._world.LocalPlayer;
            return !_game._world.LocalPlayerAwaitingJoin
                && IsRapidFireWeaponSoundActive(player, weaponKind);
        }

        public bool ShouldSuppressManagedRapidFireSound(WorldSoundEvent soundEvent)
        {
            if (_game._networkClient.IsReplayConnection)
            {
                return false;
            }

            if (!TryGetManagedRapidFireSoundKind(soundEvent.SoundName, out var weaponKind))
            {
                return false;
            }

            var listenerPosition = _game.GetWorldSoundListenerPosition();
            if (IsLocalRapidFireWeaponSoundActive(weaponKind)
                && AudioDistanceSquared(soundEvent.X, soundEvent.Y, listenerPosition.X, listenerPosition.Y) <= ManagedRapidFireSoundSuppressionDistanceSquared)
            {
                return true;
            }

            if (soundEvent.SourcePlayerId >= 0
                && TryGetManagedRapidFireSoundName(weaponKind, out var soundName)
                && _remoteRapidFireSoundInstances.ContainsKey(new RemoteRapidFireSoundKey(soundEvent.SourcePlayerId, soundName)))
            {
                return true;
            }

            for (var index = 0; index < _game._world.RemoteSnapshotPlayers.Count; index += 1)
            {
                var player = _game._world.RemoteSnapshotPlayers[index];
                if (!IsRapidFireWeaponSoundActive(player, weaponKind))
                {
                    continue;
                }

                if (AudioDistanceSquared(soundEvent.X, soundEvent.Y, player.X, player.Y) <= ManagedRapidFireSoundSuppressionDistanceSquared)
                {
                    return true;
                }
            }

            return false;
        }

        public (float Volume, float Pan) GetWorldSoundMix(float worldX, float worldY)
        {
            var listenerPosition = _game.GetWorldSoundListenerPosition();
            var dx = worldX - listenerPosition.X;
            var dy = worldY - listenerPosition.Y;
            var distance = MathF.Sqrt(dx * dx + dy * dy);
            var volume = distance <= WorldSoundFullVolumeDistance
                ? 1f
                : Math.Clamp(
                    1f - ((distance - WorldSoundFullVolumeDistance) / (WorldSoundSilenceDistance - WorldSoundFullVolumeDistance)),
                    0f,
                    1f);
            var pan = Math.Clamp(dx / WorldSoundPanDistance, -1f, 1f);
            return (volume, pan);
        }

        public void StopRapidFireWeaponAudio()
        {
            StopLocalRapidFireWeaponSound(ref _game._localChaingunSoundInstance);
            StopLocalRapidFireWeaponSound(ref _game._localFlamethrowerSoundInstance);
            StopLocalRapidFireWeaponSound(ref _game._localMedigunSoundInstance);
            StopLocalRapidFireWeaponSound(ref _game._localUberIdleSoundInstance);
            StopAndDisposeRemoteRapidFireWeaponAudio();
        }

        public void StopAndDisposeRapidFireWeaponAudio()
        {
            StopAndDisposeLocalRapidFireWeaponSound(ref _game._localChaingunSoundInstance);
            StopAndDisposeLocalRapidFireWeaponSound(ref _game._localFlamethrowerSoundInstance);
            StopAndDisposeLocalRapidFireWeaponSound(ref _game._localMedigunSoundInstance);
            StopAndDisposeLocalRapidFireWeaponSound(ref _game._localUberIdleSoundInstance);
            StopAndDisposeRemoteRapidFireWeaponAudio();
        }

        private void UpdateLocalUberIdleAudio()
        {
            var player = _game._world.LocalPlayer;
            if (_game._mainMenuOpen
                || _game._world.LocalPlayerAwaitingJoin
                || !player.IsAlive
                || player.ClassId != PlayerClass.Medic
                || !player.IsMedicUberReady
                || !player.HasUtilityBehavior(BuiltInGameplayBehaviorIds.MedicUber)
                || _game._world.MatchState.IsEnded)
            {
                StopLocalRapidFireWeaponSound(ref _game._localUberIdleSoundInstance);
                return;
            }

            UpdateLoopedWorldSound(
                "UberIdleSnd",
                player.X,
                player.Y,
                ref _game._localUberIdleSoundInstance,
                isLocalSource: true);
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

            UpdateLoopedWorldSound(soundName, _game._world.LocalPlayer.X, _game._world.LocalPlayer.Y, ref instance, isLocalSource: true);
        }

        private void UpdateRemoteRapidFireWeaponAudio()
        {
            _activeRemoteRapidFireSoundKeys.Clear();
            for (var index = 0; index < _game._world.RemoteSnapshotPlayers.Count; index += 1)
            {
                var player = _game._world.RemoteSnapshotPlayers[index];
                if (!TryGetActiveRemoteRapidFireSoundName(player, out var soundName))
                {
                    continue;
                }

                var key = new RemoteRapidFireSoundKey(player.Id, soundName);
                _activeRemoteRapidFireSoundKeys.Add(key);
                _remoteRapidFireSoundInstances.TryGetValue(key, out var instance);
                UpdateLoopedWorldSound(soundName, player.X, player.Y, ref instance, isLocalSource: false);
                if (instance is not null)
                {
                    _remoteRapidFireSoundInstances[key] = instance;
                }
            }

            _staleRemoteRapidFireSoundKeys.Clear();
            foreach (var key in _remoteRapidFireSoundInstances.Keys)
            {
                if (!_activeRemoteRapidFireSoundKeys.Contains(key))
                {
                    _staleRemoteRapidFireSoundKeys.Add(key);
                }
            }

            for (var index = 0; index < _staleRemoteRapidFireSoundKeys.Count; index += 1)
            {
                var key = _staleRemoteRapidFireSoundKeys[index];
                if (_remoteRapidFireSoundInstances.Remove(key, out var instance))
                {
                    StopAndDisposeSoundInstance(instance);
                }
            }
        }

        private bool TryGetActiveRapidFireSoundName(PlayerEntity player, out string soundName)
        {
            if (IsRapidFireWeaponSoundActive(player, PrimaryWeaponKind.Minigun))
            {
                soundName = ChaingunSoundName;
                return true;
            }

            if (IsRapidFireWeaponSoundActive(player, PrimaryWeaponKind.FlameThrower))
            {
                soundName = FlamethrowerSoundName;
                return true;
            }

            if (IsRapidFireWeaponSoundActive(player, PrimaryWeaponKind.Medigun))
            {
                soundName = MedigunSoundName;
                return true;
            }

            soundName = string.Empty;
            return false;
        }

        private bool TryGetActiveRemoteRapidFireSoundName(PlayerEntity player, out string soundName)
        {
            if (player.IsAcquiredWeaponPresented && player.AcquiredWeaponClassId != PlayerClass.Medic)
            {
                soundName = string.Empty;
                return false;
            }

            return TryGetActiveRapidFireSoundName(player, out soundName);
        }

        private bool IsRapidFireWeaponSoundActive(PlayerEntity player, PrimaryWeaponKind weaponKind)
        {
            if (_game._mainMenuOpen
                || !player.IsAlive
                || player.IsTaunting
                || _game._world.MatchState.IsEnded
                || GetActivePresentedWeaponKind(player) != weaponKind)
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

            return GetActiveWeaponCooldownTicks(player) > 0;
        }

        private static PrimaryWeaponKind? GetActivePresentedWeaponKind(PlayerEntity player)
        {
            if (IsMedigunPresentationUser(player))
            {
                return PrimaryWeaponKind.Medigun;
            }

            return player.IsAcquiredWeaponPresented
                ? player.AcquiredWeapon?.Kind
                : player.PrimaryWeapon.Kind;
        }

        private static int GetActiveWeaponCooldownTicks(PlayerEntity player)
        {
            return player.IsAcquiredWeaponPresented
                ? player.AcquiredWeaponCooldownTicks
                : player.PrimaryCooldownTicks;
        }

        private static bool TryGetManagedRapidFireSoundKind(string soundName, out PrimaryWeaponKind weaponKind)
        {
            if (string.Equals(soundName, ChaingunSoundName, StringComparison.OrdinalIgnoreCase))
            {
                weaponKind = PrimaryWeaponKind.Minigun;
                return true;
            }

            if (string.Equals(soundName, FlamethrowerSoundName, StringComparison.OrdinalIgnoreCase))
            {
                weaponKind = PrimaryWeaponKind.FlameThrower;
                return true;
            }

            if (string.Equals(soundName, MedigunSoundName, StringComparison.OrdinalIgnoreCase))
            {
                weaponKind = PrimaryWeaponKind.Medigun;
                return true;
            }

            weaponKind = default;
            return false;
        }

        private static bool TryGetManagedRapidFireSoundName(PrimaryWeaponKind weaponKind, out string soundName)
        {
            switch (weaponKind)
            {
                case PrimaryWeaponKind.Minigun:
                    soundName = ChaingunSoundName;
                    return true;
                case PrimaryWeaponKind.FlameThrower:
                    soundName = FlamethrowerSoundName;
                    return true;
                case PrimaryWeaponKind.Medigun:
                    soundName = MedigunSoundName;
                    return true;
                default:
                    soundName = string.Empty;
                    return false;
            }
        }

        private void UpdateLoopedWorldSound(
            string soundName,
            float worldX,
            float worldY,
            ref SoundEffectInstance? instance,
            bool isLocalSource)
        {
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

            var (volume, pan) = _game.GetLoopedWorldSoundMix(soundName, worldX, worldY, isLocalSource);
            if (volume <= 0f)
            {
                StopLocalRapidFireWeaponSound(ref instance);
                return;
            }

            try
            {
                instance.Volume = volume * _game.GetSoundEffectsVolumeScale();
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
            StopAndDisposeSoundInstance(instance);
            instance = null;
        }

        private void StopAndDisposeRemoteRapidFireWeaponAudio()
        {
            foreach (var instance in _remoteRapidFireSoundInstances.Values)
            {
                StopAndDisposeSoundInstance(instance);
            }

            _remoteRapidFireSoundInstances.Clear();
            _activeRemoteRapidFireSoundKeys.Clear();
            _staleRemoteRapidFireSoundKeys.Clear();
        }

        private static void StopSoundInstance(SoundEffectInstance? instance)
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

        private static void StopAndDisposeSoundInstance(SoundEffectInstance? instance)
        {
            StopSoundInstance(instance);
            try
            {
                instance?.Dispose();
            }
            catch
            {
            }
        }

        private static float AudioDistanceSquared(float x1, float y1, float x2, float y2)
        {
            var deltaX = x2 - x1;
            var deltaY = y2 - y1;
            return (deltaX * deltaX) + (deltaY * deltaY);
        }

        private readonly record struct RemoteRapidFireSoundKey(int SourcePlayerId, string SoundName);
    }
}
