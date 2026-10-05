#nullable enable

using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;
using OpenGarrison.Protocol;

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class GameplayRapidFireAudioController
    {
        private const string ChaingunSoundName = "ChaingunSnd";
        private const string FlamethrowerSoundName = "FlamethrowerSnd";
        private const string MedigunSoundName = "MedigunSnd";
        private const float ManagedRapidFireSoundSuppressionDistanceSquared = 576f;
        private const float WorldSoundFullVolumeDistance = 300f;
        private const float WorldSoundSilenceDistance = 1500f;
        private const float WorldSoundPanDistance = 400f;

        private readonly IAudioContext _context;
        private Microsoft.Xna.Framework.Audio.SoundEffectInstance? _localChaingunSoundInstance;
        private Microsoft.Xna.Framework.Audio.SoundEffectInstance? _localFlamethrowerSoundInstance;
        private Microsoft.Xna.Framework.Audio.SoundEffectInstance? _localMedigunSoundInstance;
        private Microsoft.Xna.Framework.Audio.SoundEffectInstance? _localUberIdleSoundInstance;
        private readonly Dictionary<RemoteRapidFireSoundKey, SoundEffectInstance> _remoteRapidFireSoundInstances = new();
        private readonly HashSet<RemoteRapidFireSoundKey> _activeRemoteRapidFireSoundKeys = new();
        private readonly List<RemoteRapidFireSoundKey> _staleRemoteRapidFireSoundKeys = new();

        public GameplayRapidFireAudioController(IAudioContext context)
        {
            _context = context;
        }

        public void UpdateLocalRapidFireWeaponAudio()
        {
            if (!_context._audioAvailable)
            {
                StopRapidFireWeaponAudio();
                return;
            }

            UpdateLocalRapidFireWeaponAudio(
                PrimaryWeaponKind.Minigun,
                ChaingunSoundName,
                ref _localChaingunSoundInstance);
            UpdateLocalRapidFireWeaponAudio(
                PrimaryWeaponKind.FlameThrower,
                FlamethrowerSoundName,
                ref _localFlamethrowerSoundInstance);
            UpdateLocalRapidFireWeaponAudio(
                PrimaryWeaponKind.Medigun,
                MedigunSoundName,
                ref _localMedigunSoundInstance);
            if (_context._networkClient.IsReplayConnection)
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
            if (_context._mainMenuOpen)
            {
                return false;
            }

            var player = _context.GetImmediatePrimaryPresentationPlayer();
            return !_context._world.LocalPlayerAwaitingJoin
                && IsRapidFireWeaponSoundActive(player, weaponKind, allowPendingImmediateFire: true);
        }

        public bool ShouldSuppressManagedRapidFireSound(WorldSoundEvent soundEvent)
        {
            if (_context._networkClient.IsReplayConnection)
            {
                return false;
            }

            if (!TryGetManagedRapidFireSoundKind(soundEvent.SoundName, out var weaponKind))
            {
                return false;
            }

            var localPlayerId = _context.GetResolvedLocalPlayerId();
            if (localPlayerId >= 0
                && soundEvent.SourcePlayerId == localPlayerId
                && IsLocalRapidFireWeaponSoundActive(weaponKind))
            {
                return true;
            }

            if (soundEvent.SourcePlayerId < 0
                || !TryGetManagedRapidFireSoundName(weaponKind, out var soundName))
            {
                return false;
            }

            if (_remoteRapidFireSoundInstances.ContainsKey(new RemoteRapidFireSoundKey(soundEvent.SourcePlayerId, soundName)))
            {
                return true;
            }

            for (var index = 0; index < _context._world.RemoteSnapshotPlayers.Count; index += 1)
            {
                var player = _context._world.RemoteSnapshotPlayers[index];
                if (player.Id != soundEvent.SourcePlayerId
                    || !IsRapidFireWeaponSoundActive(player, weaponKind))
                {
                    continue;
                }

                return AudioDistanceSquared(soundEvent.X, soundEvent.Y, player.X, player.Y) <= ManagedRapidFireSoundSuppressionDistanceSquared;
            }

            return false;
        }

        public (float Volume, float Pan) GetWorldSoundMix(float worldX, float worldY)
        {
            return GetWorldSoundMix(worldX, worldY, _context.GetWorldSoundListenerPosition());
        }

        public static (float Volume, float Pan) GetWorldSoundMix(float worldX, float worldY, Vector2 listenerPosition)
        {
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

        public static (float Volume, float Pan) GetBannerSoundMix(float worldX, float worldY, Vector2 listenerPosition)
        {
            var dx = worldX - listenerPosition.X;
            var dy = worldY - listenerPosition.Y;
            var distance = MathF.Sqrt((dx * dx) + (dy * dy));
            var volume = distance <= 96f ? 1f : Math.Clamp(1f - ((distance - 96f) / 416f), 0f, 1f);
            return (volume, Math.Clamp(dx / 400f, -1f, 1f));
        }

        public void StopRapidFireWeaponAudio()
        {
            StopAndDisposeRapidFireWeaponAudio();
        }

        public void StopAndDisposeRapidFireWeaponAudio()
        {
            StopAndDisposeLocalRapidFireWeaponSound(ref _localChaingunSoundInstance);
            StopAndDisposeLocalRapidFireWeaponSound(ref _localFlamethrowerSoundInstance);
            StopAndDisposeLocalRapidFireWeaponSound(ref _localMedigunSoundInstance);
            StopAndDisposeLocalRapidFireWeaponSound(ref _localUberIdleSoundInstance);
            StopAndDisposeRemoteRapidFireWeaponAudio();
        }

        private void UpdateLocalUberIdleAudio()
        {
            var player = _context._world.LocalPlayer;
            if (_context._mainMenuOpen
                || _context._world.LocalPlayerAwaitingJoin
                || !player.IsAlive
                || player.ClassId != PlayerClass.Medic
                || !player.IsMedicUberReady
                || !player.HasGameplayAbilityBehavior(
                    GameplayAbilityConstants.SpecialChannel,
                    BuiltInGameplayBehaviorIds.MedicUber)
                || _context._world.MatchState.IsEnded)
            {
                StopLocalRapidFireWeaponSound(ref _localUberIdleSoundInstance);
                return;
            }

            UpdateLoopedWorldSound(
                "UberIdleSnd",
                player.X,
                player.Y,
                ref _localUberIdleSoundInstance,
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

            UpdateLoopedWorldSound(soundName, _context._world.LocalPlayer.X, _context._world.LocalPlayer.Y, ref instance, isLocalSource: true);
        }

        private void UpdateRemoteRapidFireWeaponAudio()
        {
            _activeRemoteRapidFireSoundKeys.Clear();
            for (var index = 0; index < _context._world.RemoteSnapshotPlayers.Count; index += 1)
            {
                var player = _context._world.RemoteSnapshotPlayers[index];
                if (!TryGetActiveRemoteRapidFireSoundName(player, out var soundName))
                {
                    continue;
                }

                var key = new RemoteRapidFireSoundKey(player.Id, soundName);
                _activeRemoteRapidFireSoundKeys.Add(key);
                _remoteRapidFireSoundInstances.TryGetValue(key, out var instance);
                UpdateLoopedWorldSound(soundName, player.X, player.Y, ref instance, isLocalSource: false, sourcePlayerId: player.Id);
                if (instance is null)
                {
                    _remoteRapidFireSoundInstances.Remove(key);
                }
                else
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

        private bool IsRapidFireWeaponSoundActive(
            PlayerEntity player,
            PrimaryWeaponKind weaponKind,
            bool allowPendingImmediateFire = false)
        {
            if (_context._mainMenuOpen
                || !player.IsAlive
                || player.IsTaunting
                || _context._world.MatchState.IsEnded
                || GetActivePresentedWeaponKind(player) != weaponKind)
            {
                return false;
            }

            if (weaponKind == PrimaryWeaponKind.Minigun && _context.GetPlayerIsHeavyEating(player))
            {
                return false;
            }

            if (allowPendingImmediateFire && _context._pendingImmediateRapidFireWeaponKind == weaponKind)
            {
                return true;
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
            bool isLocalSource,
            int sourcePlayerId = -1)
        {
            var (volume, pan) = _context.GetLoopedWorldSoundMix(soundName, worldX, worldY, isLocalSource, sourcePlayerId);
            if (volume <= 0f)
            {
                StopAndDisposeLocalRapidFireWeaponSound(ref instance);
                return;
            }

            if (instance is null)
            {
                var sound = _context._runtimeAssets.GetSound(soundName);
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
                    _context.AddConsoleLine($"looped sound skipped while starting {soundName} ({ex.GetType().Name}: {ex.Message})");
                    StopAndDisposeLocalRapidFireWeaponSound(ref instance);
                    return;
                }
            }

            try
            {
                instance.Volume = volume * _context.GetSoundEffectsVolumeScale();
                instance.Pan = pan;
                if (instance.State != SoundState.Playing)
                {
                    instance.Play();
                }
            }
            catch (Exception ex)
            {
                _context.AddConsoleLine($"looped sound skipped while maintaining {soundName} ({ex.GetType().Name}: {ex.Message})");
                StopAndDisposeLocalRapidFireWeaponSound(ref instance);
            }
        }

        private static void StopLocalRapidFireWeaponSound(ref SoundEffectInstance? instance)
        {
            StopAndDisposeLocalRapidFireWeaponSound(ref instance);
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
