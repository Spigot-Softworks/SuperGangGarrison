#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const int BrowserPendingSoundEventLifetimeTicks = 30;
    private const int BrowserPendingSoundEventLimit = 96;

    private sealed class PendingBrowserSoundEvent
    {
        public PendingBrowserSoundEvent(string soundName, float x, float y, int ticksRemaining)
        {
            SoundName = soundName;
            X = x;
            Y = y;
            TicksRemaining = ticksRemaining;
        }

        public string SoundName { get; }

        public float X { get; }

        public float Y { get; }

        public int TicksRemaining { get; set; }
    }

    private SoundEffect? _menuMusic;
    private SoundEffectInstance? _menuMusicInstance;
    private SoundEffect? _lastToDieMenuMusic;
    private SoundEffectInstance? _lastToDieMenuMusicInstance;
    private SoundEffect? _faucetMusic;
    private SoundEffectInstance? _faucetMusicInstance;
    private SoundEffect? _ingameMusic;
    private SoundEffectInstance? _ingameMusicInstance;
    private SoundEffect? _lastToDieIngameMusic;
    private SoundEffectInstance? _lastToDieIngameMusicInstance;
    private SoundEffect? _lastToDieGameOverSound;
    private SoundEffectInstance? _lastToDieGameOverSoundInstance;
    private SoundEffectInstance? _localChaingunSoundInstance;
    private SoundEffectInstance? _localFlamethrowerSoundInstance;
    private SoundEffectInstance? _localMedigunSoundInstance;
    private bool _audioAvailable = true;
    private bool _audioMuted;
    private int _menuMusicVolumePercent = 70;
    private int _ingameMusicVolumePercent = 70;
    private int _soundEffectsVolumePercent = 70;
    private MusicMode _musicMode = MusicMode.MenuAndInGame;
    private bool _menuMusicLoadAttempted;
    private bool _lastToDieMenuMusicLoadAttempted;
    private bool _faucetMusicLoadAttempted;
    private bool _ingameMusicLoadAttempted;
    private bool _lastToDieIngameMusicLoadAttempted;
    private bool _lastToDieGameOverSoundLoadAttempted;
    private readonly HashSet<ulong> _processedNetworkSoundEventIds = new();
    private readonly Queue<ulong> _processedNetworkSoundEventOrder = new();
    private readonly HashSet<ulong> _processedKillFeedEventIds = new();
    private readonly Queue<ulong> _processedKillFeedEventOrder = new();
    private readonly List<PendingBrowserSoundEvent> _pendingBrowserSoundEvents = new();
    private readonly List<WorldSoundEvent> _pendingNetworkSoundEvents = new();
    private readonly List<PlayedExplosionSoundThisFrame> _playedExplosionSoundsThisFrame = new();

    private readonly record struct PlayedExplosionSoundThisFrame(float X, float Y);

    private void LoadMenuMusic()
    {
        _gameplayAudioMusicController.LoadMenuMusic();
    }

    private void LoadFaucetMusic()
    {
        _gameplayAudioMusicController.LoadFaucetMusic();
    }

    private void LoadIngameMusic()
    {
        _gameplayAudioMusicController.LoadIngameMusic();
    }

    private void LoadLastToDieMenuMusic()
    {
        _gameplayAudioMusicController.LoadLastToDieMenuMusic();
    }

    private void LoadLastToDieIngameMusic()
    {
        _gameplayAudioMusicController.LoadLastToDieIngameMusic();
    }

    private void TryLoadLoopedMusic(
        string relativePath,
        out SoundEffect? music,
        out SoundEffectInstance? musicInstance,
        float volume = 1f,
        bool disableAudioOnFailure = true)
    {
        music = null;
        musicInstance = null;

        var musicPath = FindLoopedMusicPath(relativePath);
        if (musicPath is null || !File.Exists(musicPath))
        {
            return;
        }

        try
        {
            using var stream = File.OpenRead(musicPath);
            music = SoundEffect.FromStream(stream);
            musicInstance = music.CreateInstance();
            musicInstance.IsLooped = true;
            musicInstance.Volume = volume;
        }
        catch (Exception ex)
        {
            try
            {
                musicInstance?.Dispose();
            }
            catch
            {
            }

            try
            {
                music?.Dispose();
            }
            catch
            {
            }

            musicInstance = null;
            music = null;

            if (disableAudioOnFailure)
            {
                DisableAudio($"initializing {Path.GetFileName(relativePath)}", ex);
                return;
            }

            AddConsoleLine($"optional music unavailable: {Path.GetFileName(relativePath)} ({ex.GetType().Name}: {ex.Message})");
        }
    }

    private void EnsureMenuMusicPlaying()
    {
        _gameplayAudioMusicController.EnsureMenuMusicPlaying();
    }

    private void EnsureFaucetMusicPlaying()
    {
        _gameplayAudioMusicController.EnsureFaucetMusicPlaying();
    }

    private void StopMenuMusic()
    {
        _gameplayAudioMusicController.StopMenuMusic();
    }

    private void StopLastToDieMenuMusic()
    {
        _gameplayAudioMusicController.StopLastToDieMenuMusic();
    }

    private void StopFaucetMusic()
    {
        _gameplayAudioMusicController.StopFaucetMusic();
    }

    private void EnsureIngameMusicPlaying()
    {
        _gameplayAudioMusicController.EnsureIngameMusicPlaying();
    }

    private void StopIngameMusic()
    {
        _gameplayAudioMusicController.StopIngameMusic();
    }

    private void StopLastToDieIngameMusic()
    {
        _gameplayAudioMusicController.StopLastToDieIngameMusic();
    }

    private void PlayDeathCamSoundIfNeeded()
    {
        _gameplayAudioEventController.PlayDeathCamSoundIfNeeded();
    }

    private void PlayDemoknightChargeReadySoundIfNeeded()
    {
        _gameplayAudioEventController.PlayDemoknightChargeReadySoundIfNeeded();
    }

    private void PlayRoundEndSoundIfNeeded()
    {
        _gameplayAudioEventController.PlayRoundEndSoundIfNeeded();
    }

    private void PlayKillFeedAnnouncementSounds()
    {
        _gameplayAudioEventController.PlayKillFeedAnnouncementSounds();
    }

    private void PlayLastToDieGameOverSound()
    {
        _gameplayAudioMusicController.PlayLastToDieGameOverSound();
    }

    private void StopLastToDieGameOverSound()
    {
        _gameplayAudioMusicController.StopLastToDieGameOverSound();
    }

    private void PlayPendingSoundEvents()
    {
        _gameplayAudioEventController.PlayPendingSoundEvents();
    }

    private void BeginExplosionSoundDeduplicationFrame()
    {
        _playedExplosionSoundsThisFrame.Clear();
    }

    private bool HasPlayedExplosionSoundThisFrame(float x, float y)
    {
        const float epsilon = 0.01f;
        for (var index = 0; index < _playedExplosionSoundsThisFrame.Count; index += 1)
        {
            var played = _playedExplosionSoundsThisFrame[index];
            if (MathF.Abs(played.X - x) <= epsilon
                && MathF.Abs(played.Y - y) <= epsilon)
            {
                return true;
            }
        }

        return false;
    }

    private void RecordPlayedExplosionSoundThisFrame(float x, float y)
    {
        _playedExplosionSoundsThisFrame.Add(new PlayedExplosionSoundThisFrame(x, y));
    }

    private void EnqueuePendingBrowserSoundEvent(string soundName, float x, float y)
    {
        if (!OperatingSystem.IsBrowser() || string.IsNullOrWhiteSpace(soundName))
        {
            return;
        }

        while (_pendingBrowserSoundEvents.Count >= BrowserPendingSoundEventLimit)
        {
            _pendingBrowserSoundEvents.RemoveAt(0);
        }

        _pendingBrowserSoundEvents.Add(new PendingBrowserSoundEvent(soundName, x, y, BrowserPendingSoundEventLifetimeTicks));
    }

    private void ResetPendingBrowserSoundEvents()
    {
        _pendingBrowserSoundEvents.Clear();
    }

    private void TryPlaySound(SoundEffect? sound, float volume, float pitch, float pan)
    {
        if (!_audioAvailable || sound is null)
        {
            return;
        }

        try
        {
            sound.Play(volume * GetSoundEffectsVolumeScale(), pitch, pan);
        }
        catch (Exception ex)
        {
            DisableAudio("playing sound", ex);
        }
    }

    private void PlayDirectMessageNotificationSound()
    {
        TryPlaySound(_runtimeAssets.GetSound("MessageSnd"), 0.88f, 0f, 0f);
    }

    private void DisableAudio(string reason, Exception ex)
    {
        if (!_audioAvailable)
        {
            return;
        }

        _audioAvailable = false;
        _gameplayRapidFireAudioController.StopAndDisposeLocalRapidFireWeaponAudio();
        StopMenuMusic();
        StopLastToDieMenuMusic();
        StopFaucetMusic();
        StopIngameMusic();
        StopLastToDieIngameMusic();
        StopLastToDieGameOverSound();
        _menuMusicInstance?.Dispose();
        _menuMusicInstance = null;
        _menuMusic?.Dispose();
        _menuMusic = null;
        _lastToDieMenuMusicInstance?.Dispose();
        _lastToDieMenuMusicInstance = null;
        _lastToDieMenuMusic?.Dispose();
        _lastToDieMenuMusic = null;
        _faucetMusicInstance?.Dispose();
        _faucetMusicInstance = null;
        _faucetMusic?.Dispose();
        _faucetMusic = null;
        _ingameMusicInstance?.Dispose();
        _ingameMusicInstance = null;
        _ingameMusic?.Dispose();
        _ingameMusic = null;
        _lastToDieIngameMusicInstance?.Dispose();
        _lastToDieIngameMusicInstance = null;
        _lastToDieIngameMusic?.Dispose();
        _lastToDieIngameMusic = null;
        _lastToDieGameOverSoundInstance?.Dispose();
        _lastToDieGameOverSoundInstance = null;
        _lastToDieGameOverSound?.Dispose();
        _lastToDieGameOverSound = null;
        AddConsoleLine($"audio disabled: {reason} ({ex.GetType().Name}: {ex.Message})");
    }

    private static string? FindLoopedMusicPath(string relativePath)
    {
        return GameplayAudioMusicController.FindLoopedMusicPath(relativePath);
    }

    private bool AllowsMenuMusic()
    {
        return _musicMode is MusicMode.MenuOnly or MusicMode.MenuAndInGame;
    }

    private bool AllowsIngameMusic()
    {
        return _musicMode is MusicMode.InGameOnly or MusicMode.MenuAndInGame;
    }

    private void UpdateLocalRapidFireWeaponAudio()
    {
        _gameplayRapidFireAudioController.UpdateLocalRapidFireWeaponAudio();
    }

    private void ToggleAudioMute()
    {
        _audioMuted = !_audioMuted;
        ApplyAudioVolumeState();
        AddConsoleLine(_audioMuted ? "audio muted (F12)" : "audio unmuted (F12)");
    }

    private void ApplyAudioVolumeState()
    {
        ApplyAudioMuteState();
        UpdateCurrentMusicInstanceVolumes();
    }

    private void ApplyAudioMuteState()
    {
        try
        {
            SoundEffect.MasterVolume = _audioMuted ? 0f : 1f;
        }
        catch (Exception ex)
        {
            DisableAudio("updating audio mute", ex);
        }
    }

    private void UpdateCurrentMusicInstanceVolumes()
    {
        SetSoundEffectInstanceVolume(_menuMusicInstance, GetNonLinearVolumeScale(_menuMusicVolumePercent) * 0.8f);
        SetSoundEffectInstanceVolume(_lastToDieMenuMusicInstance, GetNonLinearVolumeScale(_menuMusicVolumePercent) * 0.82f);
        SetSoundEffectInstanceVolume(_faucetMusicInstance, GetNonLinearVolumeScale(_menuMusicVolumePercent) * 0.8f);
        SetSoundEffectInstanceVolume(_ingameMusicInstance, GetNonLinearVolumeScale(_ingameMusicVolumePercent) * 0.8f);
        SetSoundEffectInstanceVolume(_lastToDieIngameMusicInstance, GetNonLinearVolumeScale(_ingameMusicVolumePercent) * 0.82f);
        SetSoundEffectInstanceVolume(_lastToDieGameOverSoundInstance, GetNonLinearVolumeScale(_ingameMusicVolumePercent) * 0.85f);
    }

    private static float GetNonLinearVolumeScale(int percent)
    {
        return Math.Clamp(MathF.Pow(percent / 100f, 1.5f), 0f, 1f);
    }

    private static void SetSoundEffectInstanceVolume(SoundEffectInstance? instance, float volume)
    {
        if (instance is null)
        {
            return;
        }

        try
        {
            instance.Volume = Math.Clamp(volume, 0f, 1f);
        }
        catch
        {
        }
    }

    private float GetSoundEffectsVolumeScale()
    {
        return _audioMuted ? 0f : GetNonLinearVolumeScale(_soundEffectsVolumePercent);
    }

    private bool IsLocalRapidFireWeaponSoundActive(PrimaryWeaponKind weaponKind)
    {
        return _gameplayRapidFireAudioController.IsLocalRapidFireWeaponSoundActive(weaponKind);
    }

    private bool ShouldSuppressManagedLocalRapidFireSound(WorldSoundEvent soundEvent)
    {
        return _gameplayRapidFireAudioController.ShouldSuppressManagedLocalRapidFireSound(soundEvent);
    }

    private Vector2 GetWorldSoundListenerPosition()
    {
        if (_networkClient.IsReplayConnection || _networkClient.IsSpectator)
        {
            return GetLocalViewPosition();
        }

        return new Vector2(_world.LocalPlayer.X, _world.LocalPlayer.Y);
    }

    private (float Volume, float Pan) GetWorldSoundMix(float worldX, float worldY)
    {
        return _gameplayRapidFireAudioController.GetWorldSoundMix(worldX, worldY);
    }

    private void StopLocalRapidFireWeaponAudio()
    {
        _gameplayRapidFireAudioController.StopLocalRapidFireWeaponAudio();
    }
}
