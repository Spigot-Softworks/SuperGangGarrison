#nullable enable

using Microsoft.Xna.Framework.Audio;
using System;
using System.IO;
using System.Linq;
using OpenGarrison.Core;

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
            TryLoadLoopedMusic(Path.Combine("Music", chosen), out _game._menuMusic, out _game._menuMusicInstance, 0.8f);
        }

        public void LoadFaucetMusic()
        {
            if (_game._audioAvailable)
            {
                TryLoadLoopedMusic(Path.Combine("Music", "faucetmusic.wav"), out _game._faucetMusic, out _game._faucetMusicInstance, 0.8f);
            }
        }

        public void LoadIngameMusic()
        {
            if (_game._audioAvailable)
            {
                TryLoadLoopedMusic(Path.Combine("Music", "ingamemusic.wav"), out _game._ingameMusic, out _game._ingameMusicInstance, 0.8f);
            }
        }

        public void LoadLastToDieMenuMusic()
        {
            if (_game._audioAvailable)
            {
                TryLoadLoopedMusic(Path.Combine("Music", "menu-l2d.fixed.wav"), out _game._lastToDieMenuMusic, out _game._lastToDieMenuMusicInstance, 0.82f, disableAudioOnFailure: false);
            }
        }

        public void LoadLastToDieIngameMusic()
        {
            if (_game._audioAvailable)
            {
                TryLoadLoopedMusic(Path.Combine("Music", "ingame_l2d.wav"), out _game._lastToDieIngameMusic, out _game._lastToDieIngameMusicInstance, 0.82f, disableAudioOnFailure: false);
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
                var soundPath = FindLoopedMusicPath(Path.Combine("Music", "ltdgameover.fixed.wav"));
                if (string.IsNullOrWhiteSpace(soundPath) || !File.Exists(soundPath))
                {
                    return;
                }

                try
                {
                    using var stream = File.OpenRead(soundPath);
                    _game._lastToDieGameOverSound = SoundEffect.FromStream(stream);
                    _game._lastToDieGameOverSoundInstance = _game._lastToDieGameOverSound.CreateInstance();
                    _game._lastToDieGameOverSoundInstance.IsLooped = false;
                    _game._lastToDieGameOverSoundInstance.Volume = 0.85f;
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

            return null;
        }

        private void TryLoadLoopedMusic(string relativePath, out SoundEffect? music, out SoundEffectInstance? musicInstance, float volume = 1f, bool disableAudioOnFailure = true)
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
                try { musicInstance?.Dispose(); } catch { }
                try { music?.Dispose(); } catch { }
                musicInstance = null;
                music = null;

                if (LooksLikeUnsupportedLoopedMusicFormat(musicPath))
                {
                    _game.AddConsoleLine($"skipping looped music {Path.GetFileName(musicPath)}: MonoGame SoundEffect streaming expects RIFF/WAV data");
                    return;
                }

                if (disableAudioOnFailure)
                {
                    _game.DisableAudio($"initializing {Path.GetFileName(relativePath)}", ex);
                    return;
                }

                _game.AddConsoleLine($"optional music unavailable: {Path.GetFileName(relativePath)} ({ex.GetType().Name}: {ex.Message})");
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

            yield return ComposePath(directory, $"{baseName}.wav");
            if (!string.Equals(extension, ".wav", StringComparison.OrdinalIgnoreCase))
            {
                yield return ComposePath(directory, $"{baseName}.ogg");
            }

            if (!string.Equals(extension, ".ogg", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(extension, ".wav", StringComparison.OrdinalIgnoreCase))
            {
                yield return relativePath;
            }
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

        private static bool LooksLikeUnsupportedLoopedMusicFormat(string path)
        {
            return string.Equals(Path.GetExtension(path), ".ogg", StringComparison.OrdinalIgnoreCase);
        }
    }
}
