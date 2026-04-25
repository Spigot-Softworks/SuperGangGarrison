#nullable enable

using Microsoft.Xna.Framework.Audio;
using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using OpenGarrison.Core;
using NVorbis;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class GameplayAudioMusicController
    {
        private readonly Game1 _game;

        public GameplayAudioMusicController(Game1 game)
        {
            _game = game;
        }

        public void LoadMenuMusic()
        {
            if (!_game._audioAvailable)
            {
                return;
            }

            var candidates = Enumerable.Range(1, 6)
                .SelectMany(static index => EnumeratePreferredMusicRelativePaths(Path.Combine("Music", $"menumusic{index}.wav")))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(fileName => FindLoopedMusicPath(fileName) is not null)
                .ToArray();
            if (candidates.Length == 0)
            {
                return;
            }

            var chosen = candidates[Random.Shared.Next(candidates.Length)];
            TryLoadLoopedMusic(chosen, out _game._menuMusic, out _game._menuMusicInstance, 0.8f);
            _game.ApplyAudioVolumeState();
        }

        public void LoadFaucetMusic()
        {
            if (_game._audioAvailable)
            {
                TryLoadLoopedMusic(Path.Combine("Music", "faucetmusic.wav"), out _game._faucetMusic, out _game._faucetMusicInstance, 0.8f);
                _game.ApplyAudioVolumeState();
            }
        }

        public void LoadIngameMusic()
        {
            if (_game._audioAvailable)
            {
                TryLoadLoopedMusic(Path.Combine("Music", "ingamemusic.wav"), out _game._ingameMusic, out _game._ingameMusicInstance, 0.8f);
                _game.ApplyAudioVolumeState();
            }
        }

        public void LoadLastToDieMenuMusic()
        {
            if (_game._audioAvailable)
            {
                TryLoadLoopedMusic(Path.Combine("Music", "menu-l2d.fixed.wav"), out _game._lastToDieMenuMusic, out _game._lastToDieMenuMusicInstance, 0.82f, disableAudioOnFailure: false);
                _game.ApplyAudioVolumeState();
            }
        }

        public void LoadLastToDieIngameMusic()
        {
            if (_game._audioAvailable)
            {
                TryLoadLoopedMusic(Path.Combine("Music", "ingame_l2d.wav"), out _game._lastToDieIngameMusic, out _game._lastToDieIngameMusicInstance, 0.82f, disableAudioOnFailure: false);
                _game.ApplyAudioVolumeState();
            }
        }

        public void EnsureMenuMusicPlaying()
        {
            if (_game.IsServerLauncherMode || !_game._audioAvailable || !_game.AllowsMenuMusic())
            {
                StopMenuMusic();
                StopLastToDieMenuMusic();
                return;
            }

            EnsureBrowserMusicLoaded(
                ref _game._menuMusicLoadAttempted,
                _game._menuMusicInstance,
                LoadMenuMusic);
            EnsureBrowserMusicLoaded(
                ref _game._lastToDieMenuMusicLoadAttempted,
                _game._lastToDieMenuMusicInstance,
                LoadLastToDieMenuMusic);

            if (!CanStartAudioPlayback())
            {
                return;
            }

            if (_game.IsLastToDieMenuActive() && _game._lastToDieMenuMusicInstance is not null)
            {
                StopMenuMusic();
                try
                {
                    if (_game._lastToDieMenuMusicInstance.State != SoundState.Playing)
                    {
                        _game._lastToDieMenuMusicInstance.Play();
                    }
                }
                catch (Exception ex)
                {
                    HandleMusicPlaybackFailure("starting Last To Die menu music", ex, ref _game._lastToDieMenuMusic, ref _game._lastToDieMenuMusicInstance);
                }

                return;
            }

            StopLastToDieMenuMusic();
            if (_game._menuMusicInstance is null)
            {
                return;
            }

            try
            {
                if (_game._menuMusicInstance.State != SoundState.Playing)
                {
                    _game._menuMusicInstance.Play();
                }
            }
            catch (Exception ex)
            {
                HandleMusicPlaybackFailure("starting menu music", ex, ref _game._menuMusic, ref _game._menuMusicInstance);
            }
        }

        public void EnsureFaucetMusicPlaying()
        {
            EnsureBrowserMusicLoaded(
                ref _game._faucetMusicLoadAttempted,
                _game._faucetMusicInstance,
                LoadFaucetMusic);

            if (!CanStartAudioPlayback())
            {
                return;
            }

            if (_game._faucetMusicInstance is null || !_game._audioAvailable || !_game.AllowsMenuMusic())
            {
                StopFaucetMusic();
                return;
            }

            try
            {
                if (_game._faucetMusicInstance.State != SoundState.Playing)
                {
                    _game._faucetMusicInstance.Play();
                }
            }
            catch (Exception ex)
            {
                HandleMusicPlaybackFailure("starting faucet music", ex, ref _game._faucetMusic, ref _game._faucetMusicInstance);
            }
        }

        public void EnsureIngameMusicPlaying()
        {
            if (!_game._audioAvailable || !_game.AllowsIngameMusic())
            {
                StopIngameMusic();
                StopLastToDieIngameMusic();
                return;
            }

            EnsureBrowserMusicLoaded(
                ref _game._ingameMusicLoadAttempted,
                _game._ingameMusicInstance,
                LoadIngameMusic);
            EnsureBrowserMusicLoaded(
                ref _game._lastToDieIngameMusicLoadAttempted,
                _game._lastToDieIngameMusicInstance,
                LoadLastToDieIngameMusic);

            if (!CanStartAudioPlayback())
            {
                return;
            }

            if (_game.IsLastToDieDeathFocusPresentationActive() || _game._world.MatchState.IsEnded)
            {
                StopIngameMusic();
                StopLastToDieIngameMusic();
                return;
            }

            if (_game.IsLastToDieSessionActive && _game._lastToDieIngameMusicInstance is not null)
            {
                StopIngameMusic();
                try
                {
                    if (_game._lastToDieIngameMusicInstance.State != SoundState.Playing)
                    {
                        _game._lastToDieIngameMusicInstance.Play();
                    }
                }
                catch (Exception ex)
                {
                    HandleMusicPlaybackFailure("starting Last To Die in-game music", ex, ref _game._lastToDieIngameMusic, ref _game._lastToDieIngameMusicInstance);
                }

                return;
            }

            StopLastToDieIngameMusic();
            if (_game._ingameMusicInstance is null)
            {
                return;
            }

            try
            {
                if (_game._ingameMusicInstance.State != SoundState.Playing)
                {
                    _game._ingameMusicInstance.Play();
                }
            }
            catch (Exception ex)
            {
                HandleMusicPlaybackFailure("starting in-game music", ex, ref _game._ingameMusic, ref _game._ingameMusicInstance);
            }
        }

        public void StopMenuMusic() => StopSoundInstance(_game._menuMusicInstance);
        public void StopLastToDieMenuMusic() => StopSoundInstance(_game._lastToDieMenuMusicInstance);
        public void StopFaucetMusic() => StopSoundInstance(_game._faucetMusicInstance);
        public void StopIngameMusic() => StopSoundInstance(_game._ingameMusicInstance);
        public void StopLastToDieIngameMusic() => StopSoundInstance(_game._lastToDieIngameMusicInstance);

        public void PlayLastToDieGameOverSound()
        {
            if (!_game._audioAvailable)
            {
                return;
            }

            if (_game._lastToDieGameOverSound is null)
            {
                if (OperatingSystem.IsBrowser())
                {
                    if (_game._lastToDieGameOverSoundLoadAttempted)
                    {
                        return;
                    }

                    _game._lastToDieGameOverSoundLoadAttempted = true;
                }

                var soundPath = FindLoopedMusicPath(Path.Combine("Music", "ltdgameover.fixed.wav"));
                if (string.IsNullOrWhiteSpace(soundPath) || !MusicAssetExists(soundPath))
                {
                    return;
                }

                try
                {
                    _game._lastToDieGameOverSound = LoadMusicSoundEffect(soundPath);
                    _game._lastToDieGameOverSoundInstance = _game._lastToDieGameOverSound.CreateInstance();
                    _game._lastToDieGameOverSoundInstance.IsLooped = false;
                    _game._lastToDieGameOverSoundInstance.Volume = 0.85f * _game._ingameMusicVolumePercent / 100f;
                }
                catch (Exception ex)
                {
                    _game.AddConsoleLine($"optional LTD game over sound unavailable: {Path.GetFileName(soundPath)} ({ex.GetType().Name}: {ex.Message})");
                    try { _game._lastToDieGameOverSoundInstance?.Dispose(); } catch { }
                    _game._lastToDieGameOverSoundInstance = null;
                    try { _game._lastToDieGameOverSound?.Dispose(); } catch { }
                    _game._lastToDieGameOverSound = null;
                    return;
                }
            }

            if (_game._lastToDieGameOverSoundInstance is null)
            {
                return;
            }

            try
            {
                _game._lastToDieGameOverSoundInstance.Stop();
                _game._lastToDieGameOverSoundInstance.Play();
            }
            catch (Exception ex)
            {
                _game.DisableAudio("starting LTD game over sound", ex);
            }
        }

        public void StopLastToDieGameOverSound()
        {
            StopSoundInstance(_game._lastToDieGameOverSoundInstance);
        }

        public static string? FindLoopedMusicPath(string relativePath)
        {
            foreach (var preferredRelativePath in EnumeratePreferredMusicRelativePaths(relativePath))
            {
                if (OperatingSystem.IsBrowser())
                {
                    var browserRelativePath = Path.Combine("Content", "Sounds", preferredRelativePath).Replace('\\', '/');
                    if (BrowserContentCatalog.TryGetBinary(browserRelativePath, out _))
                    {
                        return browserRelativePath;
                    }
                }

                var candidatePaths = new[]
                {
                    Path.Combine("Content", "Sounds", preferredRelativePath),
                    Path.Combine("OpenGarrison.Core", "Content", "Sounds", preferredRelativePath),
                    Path.Combine("Sounds", preferredRelativePath),
                    preferredRelativePath,
                };

                for (var index = 0; index < candidatePaths.Length; index += 1)
                {
                    var resolved = ProjectSourceLocator.FindFile(candidatePaths[index]);
                    if (!string.IsNullOrWhiteSpace(resolved) && File.Exists(resolved))
                    {
                        return resolved;
                    }
                }
            }

            // Browser music is optional; never block the WASM main thread for a late fetch.
            return null;
        }

        private void TryLoadLoopedMusic(string relativePath, out SoundEffect? music, out SoundEffectInstance? musicInstance, float volume = 1f, bool disableAudioOnFailure = true)
        {
            music = null;
            musicInstance = null;

            var musicPath = FindLoopedMusicPath(relativePath);
            if (musicPath is null || !MusicAssetExists(musicPath))
            {
                return;
            }

            try
            {
                music = LoadMusicSoundEffect(musicPath);
                musicInstance = music.CreateInstance();
                musicInstance.IsLooped = true;
                musicInstance.Volume = volume;
            }
            catch (Exception ex)
            {
                try { musicInstance?.Dispose(); } catch { }
                try { music?.Dispose(); } catch { }
                musicInstance = null;
                music = null;

                if (disableAudioOnFailure)
                {
                    _game.AddConsoleLine($"music unavailable: {Path.GetFileName(musicPath)} ({ex.GetType().Name}: {ex.Message})");
                    return;
                }

                _game.AddConsoleLine($"optional music unavailable: {Path.GetFileName(musicPath)} ({ex.GetType().Name}: {ex.Message})");
            }
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

        private static void EnsureBrowserMusicLoaded(
            ref bool attempted,
            SoundEffectInstance? instance,
            Action loader)
        {
            if (!OperatingSystem.IsBrowser() || attempted || instance is not null)
            {
                return;
            }

            attempted = true;
            loader();
        }

        private static bool CanStartAudioPlayback()
        {
            return !OperatingSystem.IsBrowser() || BrowserInputBridge.HasUserActivation;
        }

        private static IEnumerable<string> EnumeratePreferredMusicRelativePaths(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                yield break;
            }

            var directory = Path.GetDirectoryName(relativePath);
            var baseName = Path.GetFileNameWithoutExtension(relativePath);
            var extension = Path.GetExtension(relativePath);
            if (string.IsNullOrWhiteSpace(baseName))
            {
                yield return relativePath;
                yield break;
            }

            static string ComposePath(string? directoryPath, string fileName)
            {
                return string.IsNullOrWhiteSpace(directoryPath)
                    ? fileName
                    : Path.Combine(directoryPath, fileName);
            }

            var normalizedExtension = string.IsNullOrWhiteSpace(extension)
                ? string.Empty
                : extension;
            if (string.Equals(normalizedExtension, ".ogg", StringComparison.OrdinalIgnoreCase))
            {
                yield return ComposePath(directory, $"{baseName}.ogg");
                yield return ComposePath(directory, $"{baseName}.wav");
                yield break;
            }

            if (string.Equals(normalizedExtension, ".wav", StringComparison.OrdinalIgnoreCase))
            {
                yield return ComposePath(directory, $"{baseName}.wav");
                yield return ComposePath(directory, $"{baseName}.ogg");
                yield break;
            }

            yield return ComposePath(directory, $"{baseName}.ogg");
            yield return ComposePath(directory, $"{baseName}.wav");
            yield return relativePath;
        }

        private void HandleMusicPlaybackFailure(
            string operation,
            Exception ex,
            ref SoundEffect? music,
            ref SoundEffectInstance? musicInstance)
        {
            try { musicInstance?.Dispose(); } catch { }
            try { music?.Dispose(); } catch { }
            musicInstance = null;
            music = null;
            _game.AddConsoleLine($"music unavailable while {operation} ({ex.GetType().Name}: {ex.Message})");
        }

        private static SoundEffect LoadMusicSoundEffect(string path)
        {
            if (OperatingSystem.IsBrowser())
            {
                var bytes = TryGetBrowserMusicBytes(path);
                if (bytes is null || bytes.Length == 0)
                {
                    throw new FileNotFoundException($"Browser music asset was not available: {path}", path);
                }

                if (string.Equals(Path.GetExtension(path), ".ogg", StringComparison.OrdinalIgnoreCase))
                {
                    return LoadOggSoundEffect(bytes, Path.GetFileName(path));
                }

                using var browserStream = new MemoryStream(bytes, writable: false);
                return SoundEffect.FromStream(browserStream);
            }

            if (string.Equals(Path.GetExtension(path), ".ogg", StringComparison.OrdinalIgnoreCase))
            {
                return LoadOggSoundEffect(path);
            }

            using var stream = File.OpenRead(path);
            return SoundEffect.FromStream(stream);
        }

        private static SoundEffect LoadOggSoundEffect(string path)
        {
            using var vorbis = new VorbisReader(path);
            return LoadVorbisSoundEffect(vorbis, Path.GetFileName(path));
        }

        private static SoundEffect LoadOggSoundEffect(byte[] bytes, string assetName)
        {
            using var stream = new MemoryStream(bytes, writable: false);
            using var vorbis = new VorbisReader(stream, false);
            return LoadVorbisSoundEffect(vorbis, assetName);
        }

        private static SoundEffect LoadVorbisSoundEffect(VorbisReader vorbis, string assetName)
        {
            if (vorbis.Channels is < 1 or > 2)
            {
                throw new NotSupportedException($"Unsupported Ogg channel count {vorbis.Channels} for {assetName}.");
            }

            var channels = vorbis.Channels == 1 ? AudioChannels.Mono : AudioChannels.Stereo;
            var sampleRate = vorbis.SampleRate;
            var sampleBuffer = new float[4096];
            byte[] pcmBytes;

            if (vorbis.TotalSamples > 0)
            {
                var estimatedBytes = checked((int)Math.Min(vorbis.TotalSamples * vorbis.Channels * sizeof(short), int.MaxValue));
                pcmBytes = new byte[estimatedBytes];
                var offset = 0;
                while (true)
                {
                    var samplesRead = vorbis.ReadSamples(sampleBuffer, 0, sampleBuffer.Length);
                    if (samplesRead <= 0)
                    {
                        break;
                    }

                    EnsureCapacity(ref pcmBytes, offset, samplesRead * sizeof(short));
                    WritePcm16(sampleBuffer.AsSpan(0, samplesRead), pcmBytes.AsSpan(offset));
                    offset += samplesRead * sizeof(short);
                }

                if (offset != pcmBytes.Length)
                {
                    Array.Resize(ref pcmBytes, offset);
                }
            }
            else
            {
                pcmBytes = Array.Empty<byte>();
                var offset = 0;
                while (true)
                {
                    var samplesRead = vorbis.ReadSamples(sampleBuffer, 0, sampleBuffer.Length);
                    if (samplesRead <= 0)
                    {
                        break;
                    }

                    EnsureCapacity(ref pcmBytes, offset, samplesRead * sizeof(short));
                    WritePcm16(sampleBuffer.AsSpan(0, samplesRead), pcmBytes.AsSpan(offset));
                    offset += samplesRead * sizeof(short);
                }

                Array.Resize(ref pcmBytes, offset);
            }

            return new SoundEffect(pcmBytes, sampleRate, channels);
        }

        private static bool MusicAssetExists(string path)
        {
            return OperatingSystem.IsBrowser()
                ? TryGetBrowserMusicBytes(path) is { Length: > 0 }
                : File.Exists(path);
        }

        private static byte[]? TryGetBrowserMusicBytes(string path)
        {
            if (!OperatingSystem.IsBrowser() || string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            if (BrowserContentCatalog.TryGetBinary(path, out var directBytes))
            {
                return directBytes;
            }

            if (BrowserContentCatalog.TryGetBinaryForPath(path, out var normalizedBytes))
            {
                return normalizedBytes;
            }

            return null;
        }

        private static void EnsureCapacity(ref byte[] buffer, int offset, int additionalBytes)
        {
            var requiredLength = checked(offset + additionalBytes);
            if (requiredLength <= buffer.Length)
            {
                return;
            }

            var nextLength = buffer.Length == 0 ? 8192 : buffer.Length;
            while (nextLength < requiredLength)
            {
                nextLength = checked(nextLength * 2);
            }

            Array.Resize(ref buffer, nextLength);
        }

        private static void WritePcm16(ReadOnlySpan<float> samples, Span<byte> destination)
        {
            for (var index = 0; index < samples.Length; index += 1)
            {
                var clamped = Math.Clamp(samples[index], -1f, 1f);
                var value = clamped >= 0f
                    ? (short)Math.Round(clamped * short.MaxValue)
                    : (short)Math.Round(clamped * -short.MinValue);
                BinaryPrimitives.WriteInt16LittleEndian(destination.Slice(index * sizeof(short), sizeof(short)), value);
            }
        }
    }
}
