#nullable enable

using Microsoft.Xna.Framework.Audio;
using System;
using System.Buffers.Binary;
using System.IO;
using OpenGarrison.Core;
using NVorbis;

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public partial class Game1
{
    internal static bool ShouldPlayLastToDieIngameMusic(
        bool anyLastToDieSessionActive,
        bool hasLastToDieIngameMusic,
        bool failurePresentationActive,
        bool matchEnded)
        => anyLastToDieSessionActive
            && hasLastToDieIngameMusic
            && !failurePresentationActive
            && !matchEnded;

}

public sealed class GameplayAudioMusicController
    {
        internal const string LastToDieMenuMusicRelativePath = "Music/menu-l2d.fixed.wav";
        internal const string LastToDieIngameMusicRelativePath = "Music/ingame_l2d.wav";
        internal const string LastToDieGameOverMusicRelativePath = "Music/ltdgameover.fixed.wav";

        private readonly IAudioContext _context;
        private bool _faucetMusicLoadAttempted;
        private bool _ingameMusicLoadAttempted;
        private bool _lastToDieGameOverSoundLoadAttempted;
        private bool _lastToDieIngameMusicLoadAttempted;
        private bool _lastToDieMenuMusicLoadAttempted;
        private bool _menuMusicLoadAttempted;

        public GameplayAudioMusicController(IAudioContext context)
        {
            _context = context;
        }

        public void LoadMenuMusic()
        {
            if (!_context._audioAvailable)
            {
                return;
            }

            TryLoadLoopedMusic(
                Path.Combine("Music", "GG2_theme.ogg"),
                out _context.MusicResources.MenuMusic,
                out _context.MusicResources.MenuMusicInstance,
                0.8f);
            _context.ApplyAudioVolumeState();
        }

        public void LoadFaucetMusic()
        {
            if (_context._audioAvailable)
            {
                TryLoadLoopedMusic(Path.Combine("Music", "faucetmusic.wav"), out _context.MusicResources.FaucetMusic, out _context.MusicResources.FaucetMusicInstance, 0.8f);
                _context.ApplyAudioVolumeState();
            }
        }

        public void LoadIngameMusic()
        {
            if (_context._audioAvailable)
            {
                TryLoadLoopedMusic(
                    Path.Combine("Music", "GG2_ingame.ogg"),
                    out _context.MusicResources.IngameMusic,
                    out _context.MusicResources.IngameMusicInstance,
                    0.8f);
                TryLoadLoopedMusic(
                    Path.Combine("Music", "GG2_combat.ogg"),
                    out _context.MusicResources.IngameCombatMusic,
                    out _context.MusicResources.IngameCombatMusicInstance,
                    0f,
                    disableAudioOnFailure: false);
                _context.ApplyAudioVolumeState();
            }
        }

        public void LoadLastToDieMenuMusic()
        {
            if (_context._audioAvailable)
            {
                TryLoadLoopedMusic(LastToDieMenuMusicRelativePath, out _context.MusicResources.LastToDieMenuMusic, out _context.MusicResources.LastToDieMenuMusicInstance, 0.82f, disableAudioOnFailure: false);
                _context.ApplyAudioVolumeState();
            }
        }

        public void LoadLastToDieIngameMusic()
        {
            if (_context._audioAvailable)
            {
                TryLoadLoopedMusic(LastToDieIngameMusicRelativePath, out _context.MusicResources.LastToDieIngameMusic, out _context.MusicResources.LastToDieIngameMusicInstance, 0.82f, disableAudioOnFailure: false);
                _context.ApplyAudioVolumeState();
            }
        }

        public void EnsureMenuMusicPlaying()
        {
            if (_context.IsServerLauncherMode || !_context._audioAvailable || !_context.AllowsMenuMusic())
            {
                StopMenuMusic();
                StopLastToDieMenuMusic();
                return;
            }

            EnsureBrowserMusicLoaded(
                ref _menuMusicLoadAttempted,
                _context.MusicResources.MenuMusicInstance,
                LoadMenuMusic);
            EnsureBrowserMusicLoaded(
                ref _lastToDieMenuMusicLoadAttempted,
                _context.MusicResources.LastToDieMenuMusicInstance,
                LoadLastToDieMenuMusic);

            if (!CanStartAudioPlayback())
            {
                return;
            }

            if (_context.IsLastToDieMenuActive() && _context.MusicResources.LastToDieMenuMusicInstance is not null)
            {
                StopMenuMusic();
                try
                {
                    if (_context.MusicResources.LastToDieMenuMusicInstance.State != SoundState.Playing)
                    {
                        _context.MusicResources.LastToDieMenuMusicInstance.Play();
                    }
                }
                catch (Exception ex)
                {
                    HandleMusicPlaybackFailure("starting Last To Die menu music", ex, ref _context.MusicResources.LastToDieMenuMusic, ref _context.MusicResources.LastToDieMenuMusicInstance);
                }

                return;
            }

            StopLastToDieMenuMusic();
            if (_context.MusicResources.MenuMusicInstance is null)
            {
                return;
            }

            try
            {
                if (_context.MusicResources.MenuMusicInstance.State != SoundState.Playing)
                {
                    _context.MusicResources.MenuMusicInstance.Play();
                }
            }
            catch (Exception ex)
            {
                HandleMusicPlaybackFailure("starting menu music", ex, ref _context.MusicResources.MenuMusic, ref _context.MusicResources.MenuMusicInstance);
            }
        }

        public void EnsureFaucetMusicPlaying()
        {
            EnsureBrowserMusicLoaded(
                ref _faucetMusicLoadAttempted,
                _context.MusicResources.FaucetMusicInstance,
                LoadFaucetMusic);

            if (!CanStartAudioPlayback())
            {
                return;
            }

            if (_context.MusicResources.FaucetMusicInstance is null || !_context._audioAvailable || !_context.AllowsMenuMusic())
            {
                StopFaucetMusic();
                return;
            }

            try
            {
                if (_context.MusicResources.FaucetMusicInstance.State != SoundState.Playing)
                {
                    _context.MusicResources.FaucetMusicInstance.Play();
                }
            }
            catch (Exception ex)
            {
                HandleMusicPlaybackFailure("starting faucet music", ex, ref _context.MusicResources.FaucetMusic, ref _context.MusicResources.FaucetMusicInstance);
            }
        }

        public void EnsureIngameMusicPlaying()
        {
            if (_context.IsHostedLastToDieMenuMusicPhase())
            {
                EnsureHostedLastToDieMenuMusicPlaying();
                return;
            }

            if (!_context._audioAvailable || !_context.AllowsIngameMusic())
            {
                StopLastToDieMenuMusic();
                StopIngameMusic();
                StopLastToDieIngameMusic();
                return;
            }

            EnsureBrowserMusicLoaded(
                ref _ingameMusicLoadAttempted,
                _context.MusicResources.IngameMusicInstance,
                LoadIngameMusic);
            EnsureBrowserMusicLoaded(
                ref _lastToDieIngameMusicLoadAttempted,
                _context.MusicResources.LastToDieIngameMusicInstance,
                LoadLastToDieIngameMusic);

            if (!CanStartAudioPlayback())
            {
                return;
            }

            if (_context.IsLastToDieFailurePresentationActive() || _context._world.MatchState.IsEnded)
            {
                StopLastToDieMenuMusic();
                StopIngameMusic();
                StopLastToDieIngameMusic();
                return;
            }

            var lastToDieIngameMusicInstance = _context.MusicResources.LastToDieIngameMusicInstance;
            if (Game1.ShouldPlayLastToDieIngameMusic(
                    _context.IsAnyLastToDieSessionActive,
                    lastToDieIngameMusicInstance is not null,
                    _context.IsLastToDieFailurePresentationActive(),
                    _context._world.MatchState.IsEnded))
            {
                StopLastToDieMenuMusic();
                StopIngameMusic();
                try
                {
                    if (lastToDieIngameMusicInstance!.State != SoundState.Playing)
                    {
                        lastToDieIngameMusicInstance.Play();
                    }
                }
                catch (Exception ex)
                {
                    HandleMusicPlaybackFailure("starting Last To Die in-context music", ex, ref _context.MusicResources.LastToDieIngameMusic, ref _context.MusicResources.LastToDieIngameMusicInstance);
                }

                return;
            }

            StopLastToDieMenuMusic();
            StopLastToDieIngameMusic();
            EnsureIngameMusicPairPlaybackStarted();
        }

        private void EnsureHostedLastToDieMenuMusicPlaying()
        {
            // Hosted LTD menus are gameplay screens from the application's
            // perspective, but musically they remain part of the LTD menu flow.
            // Stop both gameplay tracks first so a delayed load/play cannot
            // produce two songs at once.
            StopIngameMusic();
            StopLastToDieIngameMusic();
            StopMenuMusic();

            if (!_context._audioAvailable || !_context.AllowsMenuMusic())
            {
                StopLastToDieMenuMusic();
                return;
            }

            EnsureBrowserMusicLoaded(
                ref _lastToDieMenuMusicLoadAttempted,
                _context.MusicResources.LastToDieMenuMusicInstance,
                LoadLastToDieMenuMusic);
            if (!CanStartAudioPlayback() || _context.MusicResources.LastToDieMenuMusicInstance is null)
            {
                return;
            }

            try
            {
                if (_context.MusicResources.LastToDieMenuMusicInstance.State != SoundState.Playing)
                {
                    _context.MusicResources.LastToDieMenuMusicInstance.Play();
                }
            }
            catch (Exception ex)
            {
                HandleMusicPlaybackFailure(
                    "starting hosted Last To Die menu music",
                    ex,
                    ref _context.MusicResources.LastToDieMenuMusic,
                    ref _context.MusicResources.LastToDieMenuMusicInstance);
            }
        }

        public void StopMenuMusic() => StopSoundInstance(_context.MusicResources.MenuMusicInstance);
        public void StopLastToDieMenuMusic() => StopSoundInstance(_context.MusicResources.LastToDieMenuMusicInstance);
        public void StopFaucetMusic() => StopSoundInstance(_context.MusicResources.FaucetMusicInstance);
        public void StopIngameMusic()
        {
            StopSoundInstance(_context.MusicResources.IngameMusicInstance);
            StopSoundInstance(_context.MusicResources.IngameCombatMusicInstance);
        }
        public void StopLastToDieIngameMusic() => StopSoundInstance(_context.MusicResources.LastToDieIngameMusicInstance);

        public void TryLoadOptionalLoopedMusic(string relativePath, out SoundEffect? music, out SoundEffectInstance? musicInstance, float volume = 0f)
        {
            if (!_context._audioAvailable)
            {
                music = null;
                musicInstance = null;
                return;
            }

            TryLoadLoopedMusic(relativePath, out music, out musicInstance, volume, disableAudioOnFailure: false);
        }

        public void TryLoadOptionalMusicSound(string relativePath, out SoundEffect? music, out SoundEffectInstance? musicInstance, bool isLooped, float volume = 0f)
        {
            if (!_context._audioAvailable)
            {
                music = null;
                musicInstance = null;
                return;
            }

            TryLoadLoopedMusic(relativePath, out music, out musicInstance, volume, disableAudioOnFailure: false, isLooped: isLooped);
        }

        public static bool CanStartMusicPlayback()
        {
            return CanStartAudioPlayback();
        }

        public void EnsureIngameMusicPairPlaybackStarted()
        {
            var backing = _context.MusicResources.IngameMusicInstance;
            if (backing is null)
            {
                return;
            }

            var combat = _context.MusicResources.IngameCombatMusicInstance;
            try
            {
                if (combat is null)
                {
                    if (backing.State != SoundState.Playing)
                    {
                        backing.Play();
                    }

                    return;
                }

                // The files are authored as a phase-locked pair. If either
                // instance is interrupted, restart both from sample zero so
                // the combat layer can never drift relative to the backing.
                if (backing.State == SoundState.Playing && combat.State == SoundState.Playing)
                {
                    return;
                }

                StopSoundInstance(backing);
                StopSoundInstance(combat);
                _context.ApplyAudioVolumeState();
                backing.Play();
                combat.Play();
            }
            catch (Exception ex)
            {
                HandleMusicPlaybackFailure(
                    "starting synchronized in-context music",
                    ex,
                    ref _context.MusicResources.IngameMusic,
                    ref _context.MusicResources.IngameMusicInstance);
                try { _context.MusicResources.IngameCombatMusicInstance?.Dispose(); } catch { }
                _context.MusicResources.IngameCombatMusicInstance = null;
                try { _context.MusicResources.IngameCombatMusic?.Dispose(); } catch { }
                _context.MusicResources.IngameCombatMusic = null;
            }
        }

        public void PlayLastToDieGameOverSound()
        {
            if (!_context._audioAvailable)
            {
                return;
            }

            // The context-over cue is the sole LTD terminal music owner.  Stop
            // every looped track before starting it so a transition that
            // arrives between presentation phases cannot layer the cue over
            // the menu, generic gameplay, or LTD gameplay song.
            _context.StopMenuMusic();
            _context.StopLastToDieMenuMusic();
            _context.StopFaucetMusic();
            _context.StopIngameMusic();
            _context.StopLastToDieIngameMusic();

            if (_context.MusicResources.LastToDieGameOverSound is null)
            {
                if (OperatingSystem.IsBrowser())
                {
                    if (_lastToDieGameOverSoundLoadAttempted)
                    {
                        return;
                    }

                    _lastToDieGameOverSoundLoadAttempted = true;
                }

                var soundPath = FindLoopedMusicPath(LastToDieGameOverMusicRelativePath);
                if (string.IsNullOrWhiteSpace(soundPath) || !MusicAssetExists(soundPath))
                {
                    return;
                }

                try
                {
                    _context.MusicResources.LastToDieGameOverSound = LoadMusicSoundEffect(soundPath);
                    _context.MusicResources.LastToDieGameOverSoundInstance = _context.MusicResources.LastToDieGameOverSound.CreateInstance();
                    _context.MusicResources.LastToDieGameOverSoundInstance.IsLooped = false;
                    _context.MusicResources.LastToDieGameOverSoundInstance.Volume = 0.85f * _context.AudioRuntimeSettings.IngameMusicVolumePercent / 100f;
                }
                catch (Exception ex)
                {
                    _context.AddConsoleLine($"optional LTD context over sound unavailable: {Path.GetFileName(soundPath)} ({ex.GetType().Name}: {ex.Message})");
                    try { _context.MusicResources.LastToDieGameOverSoundInstance?.Dispose(); } catch { }
                    _context.MusicResources.LastToDieGameOverSoundInstance = null;
                    try { _context.MusicResources.LastToDieGameOverSound?.Dispose(); } catch { }
                    _context.MusicResources.LastToDieGameOverSound = null;
                    return;
                }
            }

            if (_context.MusicResources.LastToDieGameOverSoundInstance is null)
            {
                return;
            }

            try
            {
                _context.MusicResources.LastToDieGameOverSoundInstance.Stop();
                _context.MusicResources.LastToDieGameOverSoundInstance.Play();
            }
            catch (Exception ex)
            {
                _context.DisableAudio("starting LTD context over sound", ex);
            }
        }

        public void StopLastToDieGameOverSound()
        {
            StopSoundInstance(_context.MusicResources.LastToDieGameOverSoundInstance);
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

        private void TryLoadLoopedMusic(string relativePath, out SoundEffect? music, out SoundEffectInstance? musicInstance, float volume = 1f, bool disableAudioOnFailure = true, bool isLooped = true)
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
                musicInstance.IsLooped = isLooped;
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
                    _context.AddConsoleLine($"music unavailable: {Path.GetFileName(musicPath)} ({ex.GetType().Name}: {ex.Message})");
                    return;
                }

                _context.AddConsoleLine($"optional music unavailable: {Path.GetFileName(musicPath)} ({ex.GetType().Name}: {ex.Message})");
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
            _context.AddConsoleLine($"music unavailable while {operation} ({ex.GetType().Name}: {ex.Message})");
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
