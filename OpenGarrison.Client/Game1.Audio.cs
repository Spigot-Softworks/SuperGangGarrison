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
    private SoundEffect? _faucetMusic;
    private SoundEffectInstance? _faucetMusicInstance;
    private SoundEffect? _ingameMusic;
    private SoundEffectInstance? _ingameMusicInstance;
    private SoundEffectInstance? _localChaingunSoundInstance;
    private SoundEffectInstance? _localFlamethrowerSoundInstance;
    private bool _audioAvailable = true;
    private bool _audioMuted;
    private MusicMode _musicMode = MusicMode.MenuAndInGame;
    private readonly HashSet<ulong> _processedNetworkSoundEventIds = new();
    private readonly Queue<ulong> _processedNetworkSoundEventOrder = new();
    private readonly HashSet<ulong> _processedKillFeedEventIds = new();
    private readonly Queue<ulong> _processedKillFeedEventOrder = new();

    private void LoadMenuMusic()
    {
        if (!_audioAvailable)
        {
            return;
        }

        var candidates = Enumerable.Range(1, 6)
            .Select(static index => $"menumusic{index}.wav")
            .Where(static fileName => FindLoopedMusicPath(Path.Combine("Music", fileName)) is not null)
            .ToArray();
        if (candidates.Length == 0)
        {
            return;
        }

        var chosen = candidates[Random.Shared.Next(candidates.Length)];
        TryLoadLoopedMusic(Path.Combine("Music", chosen), out _menuMusic, out _menuMusicInstance, 0.8f);
    }

    private void LoadFaucetMusic()
    {
        if (!_audioAvailable)
        {
            return;
        }

        TryLoadLoopedMusic(Path.Combine("Music", "faucetmusic.wav"), out _faucetMusic, out _faucetMusicInstance, 0.8f);
    }

    private void LoadIngameMusic()
    {
        if (!_audioAvailable)
        {
            return;
        }

        TryLoadLoopedMusic(Path.Combine("Music", "ingamemusic.wav"), out _ingameMusic, out _ingameMusicInstance, 0.8f);
    }

    private void TryLoadLoopedMusic(
        string relativePath,
        out SoundEffect? music,
        out SoundEffectInstance? musicInstance,
        float volume = 1f)
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
            DisableAudio("initializing audio", ex);
        }
    }

    private void EnsureMenuMusicPlaying()
    {
        if (IsServerLauncherMode || _menuMusicInstance is null || !_audioAvailable || !AllowsMenuMusic())
        {
            StopMenuMusic();
            return;
        }

        try
        {
            if (_menuMusicInstance.State != SoundState.Playing)
            {
                _menuMusicInstance.Play();
            }
        }
        catch (Exception ex)
        {
            DisableAudio("starting menu music", ex);
        }
    }

    private void EnsureFaucetMusicPlaying()
    {
        if (_faucetMusicInstance is null || !_audioAvailable || !AllowsMenuMusic())
        {
            StopFaucetMusic();
            return;
        }

        try
        {
            if (_faucetMusicInstance.State != SoundState.Playing)
            {
                _faucetMusicInstance.Play();
            }
        }
        catch (Exception ex)
        {
            DisableAudio("starting faucet music", ex);
        }
    }

    private void StopMenuMusic()
    {
        try
        {
            if (_menuMusicInstance?.State == SoundState.Playing)
            {
                _menuMusicInstance.Stop();
            }
        }
        catch
        {
        }
    }

    private void StopFaucetMusic()
    {
        try
        {
            if (_faucetMusicInstance?.State == SoundState.Playing)
            {
                _faucetMusicInstance.Stop();
            }
        }
        catch
        {
        }
    }

    private void EnsureIngameMusicPlaying()
    {
        if (_ingameMusicInstance is null || !_audioAvailable || !AllowsIngameMusic())
        {
            StopIngameMusic();
            return;
        }

        if (_world.MatchState.IsEnded)
        {
            StopIngameMusic();
            return;
        }

        try
        {
            if (_ingameMusicInstance.State != SoundState.Playing)
            {
                _ingameMusicInstance.Play();
            }
        }
        catch (Exception ex)
        {
            DisableAudio("starting in-game music", ex);
        }
    }

    private void StopIngameMusic()
    {
        try
        {
            if (_ingameMusicInstance?.State == SoundState.Playing)
            {
                _ingameMusicInstance.Stop();
            }
        }
        catch
        {
        }
    }

    private void PlayDeathCamSoundIfNeeded()
    {
        if (!_audioAvailable)
        {
            return;
        }

        if (!_killCamEnabled || _world.LocalPlayer.IsAlive || _world.LocalDeathCam is null)
        {
            return;
        }

        var deathCam = _world.LocalDeathCam;
        if (GetDeathCamElapsedTicks(deathCam) < DeathCamFocusDelayTicks || _wasDeathCamActive)
        {
            return;
        }

        var sound = _runtimeAssets.GetSound("DeathCamSnd");
        TryPlaySound(sound, 0.6f, 0f, 0f);
    }

    private void PlayRoundEndSoundIfNeeded()
    {
        if (!_audioAvailable)
        {
            return;
        }

        if (!_world.MatchState.IsEnded || _wasMatchEnded)
        {
            return;
        }

        var soundName = _world.MatchState.WinnerTeam switch
        {
            PlayerTeam.Red when _world.LocalPlayer.Team == PlayerTeam.Red => "VictorySnd",
            PlayerTeam.Blue when _world.LocalPlayer.Team == PlayerTeam.Blue => "VictorySnd",
            null => "FailureSnd",
            _ => "FailureSnd",
        };

        if (_ingameMusicInstance?.State == SoundState.Playing)
        {
            _ingameMusicInstance.Stop();
        }

        var sound = _runtimeAssets.GetSound(soundName);
        TryPlaySound(sound, 0.8f, 0f, 0f);
    }

    private void PlayKillFeedAnnouncementSounds()
    {
        if (!_audioAvailable)
        {
            return;
        }

        for (var index = 0; index < _world.KillFeed.Count; index += 1)
        {
            var entry = _world.KillFeed[index];
            if (entry.EventId == 0
                || entry.SpecialType == KillFeedSpecialType.None
                || !ShouldProcessNetworkEvent(entry.EventId, _processedKillFeedEventIds, _processedKillFeedEventOrder))
            {
                continue;
            }

            var localPlayerId = _world.LocalPlayer.Id;
            if (entry.KillerPlayerId != localPlayerId && entry.VictimPlayerId != localPlayerId)
            {
                continue;
            }

            var soundName = entry.SpecialType == KillFeedSpecialType.Domination
                ? "DominationSnd"
                : "RevengeSnd";
            var sound = _runtimeAssets.GetSound(soundName);
            TryPlaySound(sound, 0.85f, 0f, 0f);
        }
    }

    private void PlayPendingSoundEvents()
    {
        if (!_audioAvailable)
        {
            return;
        }

        foreach (var soundEvent in _world.DrainPendingSoundEvents())
        {
            if (!ShouldProcessNetworkEvent(soundEvent.EventId, _processedNetworkSoundEventIds, _processedNetworkSoundEventOrder))
            {
                continue;
            }

            if (string.Equals(soundEvent.SoundName, "ExplosionSnd", StringComparison.OrdinalIgnoreCase)
                && TryCreateExplosionVisual(soundEvent, out var explosion))
            {
                _explosions.Add(explosion!);
            }

            if (ShouldSuppressManagedLocalRapidFireSound(soundEvent))
            {
                continue;
            }

            var sound = _runtimeAssets.GetSound(soundEvent.SoundName);
            if (sound is null)
            {
                continue;
            }

            var (volume, pan) = GetWorldSoundMix(soundEvent.X, soundEvent.Y);
            if (volume <= 0f)
            {
                continue;
            }

            TryPlaySound(sound, volume, 0f, pan);
        }
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
        StopAndDisposeLocalRapidFireWeaponSound(ref _localChaingunSoundInstance);
        StopAndDisposeLocalRapidFireWeaponSound(ref _localFlamethrowerSoundInstance);
        StopMenuMusic();
        StopFaucetMusic();
        StopIngameMusic();
        _menuMusicInstance?.Dispose();
        _menuMusicInstance = null;
        _menuMusic?.Dispose();
        _menuMusic = null;
        _faucetMusicInstance?.Dispose();
        _faucetMusicInstance = null;
        _faucetMusic?.Dispose();
        _faucetMusic = null;
        _ingameMusicInstance?.Dispose();
        _ingameMusicInstance = null;
        _ingameMusic?.Dispose();
        _ingameMusic = null;
        AddConsoleLine($"audio disabled: {reason} ({ex.GetType().Name}: {ex.Message})");
    }

    private static string? FindLoopedMusicPath(string relativePath)
    {
        var candidatePaths = new[]
        {
            Path.Combine("Content", "Sounds", relativePath),
            Path.Combine("OpenGarrison.Core", "Content", "Sounds", relativePath),
            Path.Combine("Sounds", relativePath),
            relativePath,
        };

        for (var index = 0; index < candidatePaths.Length; index += 1)
        {
            var resolved = ProjectSourceLocator.FindFile(candidatePaths[index]);
            if (!string.IsNullOrWhiteSpace(resolved) && File.Exists(resolved))
            {
                return resolved;
            }
        }

        return null;
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
        if (!_audioAvailable)
        {
            StopLocalRapidFireWeaponAudio();
            return;
        }

        UpdateLocalRapidFireWeaponAudio(
            PrimaryWeaponKind.Minigun,
            "ChaingunSnd",
            ref _localChaingunSoundInstance);
        UpdateLocalRapidFireWeaponAudio(
            PrimaryWeaponKind.FlameThrower,
            "FlamethrowerSnd",
            ref _localFlamethrowerSoundInstance);
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
            var sound = _runtimeAssets.GetSound(soundName);
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
                DisableAudio($"starting {soundName}", ex);
                return;
            }
        }

        var (volume, pan) = GetWorldSoundMix(_world.LocalPlayer.X, _world.LocalPlayer.Y);
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
            DisableAudio($"maintaining {soundName}", ex);
        }
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
        if (_mainMenuOpen)
        {
            return false;
        }

        var player = _world.LocalPlayer;
        if (_world.LocalPlayerAwaitingJoin
            || !player.IsAlive
            || player.IsTaunting
            || _world.MatchState.IsEnded
            || player.PrimaryWeapon.Kind != weaponKind)
        {
            return false;
        }

        if (weaponKind == PrimaryWeaponKind.Minigun && GetPlayerIsHeavyEating(player))
        {
            return false;
        }

        if (weaponKind == PrimaryWeaponKind.FlameThrower)
        {
            return player.PyroFlameLoopTicksRemaining > 0;
        }

        return player.PrimaryCooldownTicks > 0;
    }

    private bool ShouldSuppressManagedLocalRapidFireSound(WorldSoundEvent soundEvent)
    {
        var soundName = soundEvent.SoundName;
        if (string.Equals(soundName, "ChaingunSnd", StringComparison.OrdinalIgnoreCase))
        {
            return IsLocalRapidFireWeaponSoundActive(PrimaryWeaponKind.Minigun)
                && AudioDistanceSquared(soundEvent.X, soundEvent.Y, _world.LocalPlayer.X, _world.LocalPlayer.Y) <= 576f;
        }

        if (string.Equals(soundName, "FlamethrowerSnd", StringComparison.OrdinalIgnoreCase))
        {
            return IsLocalRapidFireWeaponSoundActive(PrimaryWeaponKind.FlameThrower)
                && AudioDistanceSquared(soundEvent.X, soundEvent.Y, _world.LocalPlayer.X, _world.LocalPlayer.Y) <= 576f;
        }

        return false;
    }

    private (float Volume, float Pan) GetWorldSoundMix(float worldX, float worldY)
    {
        var dx = worldX - _world.LocalPlayer.X;
        var dy = worldY - _world.LocalPlayer.Y;
        var distance = MathF.Sqrt(dx * dx + dy * dy);
        var volume = Math.Clamp(1f - (distance / 1200f), 0f, 1f) * 0.6f;
        var pan = Math.Clamp(dx / 600f, -1f, 1f);
        return (volume, pan);
    }

    private void StopLocalRapidFireWeaponAudio()
    {
        StopLocalRapidFireWeaponSound(ref _localChaingunSoundInstance);
        StopLocalRapidFireWeaponSound(ref _localFlamethrowerSoundInstance);
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
