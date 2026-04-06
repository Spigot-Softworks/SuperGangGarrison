#nullable enable

using Microsoft.Xna.Framework.Audio;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
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
    private MusicMode _musicMode = MusicMode.MenuAndInGame;
    private readonly HashSet<ulong> _processedNetworkSoundEventIds = new();
    private readonly Queue<ulong> _processedNetworkSoundEventOrder = new();
    private readonly HashSet<ulong> _processedKillFeedEventIds = new();
    private readonly Queue<ulong> _processedKillFeedEventOrder = new();

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

    private void TryPlaySound(SoundEffect? sound, float volume, float pitch, float pan)
    {
        if (!_audioAvailable || sound is null)
        {
            return;
        }

        try
        {
            sound.Play(volume, pitch, pan);
        }
        catch (Exception ex)
        {
            DisableAudio("playing sound", ex);
        }
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
        ApplyAudioMuteState();
        AddConsoleLine(_audioMuted ? "audio muted (F12)" : "audio unmuted (F12)");
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

    private bool IsLocalRapidFireWeaponSoundActive(PrimaryWeaponKind weaponKind)
    {
        return _gameplayRapidFireAudioController.IsLocalRapidFireWeaponSoundActive(weaponKind);
    }

    private bool ShouldSuppressManagedLocalRapidFireSound(WorldSoundEvent soundEvent)
    {
        return _gameplayRapidFireAudioController.ShouldSuppressManagedLocalRapidFireSound(soundEvent);
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
