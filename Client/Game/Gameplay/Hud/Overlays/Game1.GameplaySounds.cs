#nullable enable

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private SimpleLevel? _gameplaySoundLevel;
    private bool[] _gameplaySoundPreviousOutputs = [];
    private SoundEffect? _gameplaySoundMusicOverride;
    private SoundEffectInstance? _gameplaySoundMusicOverrideInstance;
    private string _gameplaySoundMusicOverrideName = string.Empty;
    private float _gameplaySoundMusicOverrideFade;
    private float _gameplaySoundMusicOverrideFadeSeconds;
    private bool _gameplaySoundMusicOverrideLoop = true;
    private float _gameplaySoundMusicOverrideLifetimeSeconds;
    private float _gameplaySoundMusicOverrideFadeAfterSeconds;
    private float _gameplaySoundMusicOverrideFadeOutSeconds;
    private bool _gameplaySoundMusicOverrideFadingOut;

    private readonly record struct GameplayMusicOverrideRequest(
        string SoundName,
        bool Crossfade,
        float CrossfadeSeconds,
        bool Loop,
        float FadeAfterSeconds);

    private bool IsGameplaySoundMusicOverrideActive => _gameplaySoundMusicOverrideInstance is not null;

    private void AdvanceGameplaySounds()
    {
        EnsureGameplaySoundState();
        var sounds = _world.Level.GameplaySounds;
        if (sounds.Count == 0)
        {
            return;
        }

        var graph = _world.Level.LogicGraph;
        for (var index = 0; index < sounds.Count; index += 1)
        {
            var marker = sounds[index];
            var current = marker.UsesTrigger
                && marker.TriggerNodeIndex >= 0
                && marker.TriggerNodeIndex < graph.Nodes.Count
                && graph.GetOutput(marker.TriggerNodeIndex);
            if (current && !_gameplaySoundPreviousOutputs[index])
            {
                TriggerGameplaySound(marker);
            }

            _gameplaySoundPreviousOutputs[index] = current;
        }
    }

    private void EnsureGameplaySoundState()
    {
        var sounds = _world.Level.GameplaySounds;
        if (!ReferenceEquals(_gameplaySoundLevel, _world.Level)
            || _gameplaySoundPreviousOutputs.Length != sounds.Count)
        {
            _gameplaySoundLevel = _world.Level;
            _gameplaySoundPreviousOutputs = new bool[sounds.Count];
            StopGameplaySoundMusicOverride();
        }
    }

    private void TriggerGameplaySound(GameplaySoundMarker marker)
    {
        if (string.IsNullOrWhiteSpace(marker.SoundName))
        {
            return;
        }

        if (!TryGetGameplayMessageCustomSound(marker.SoundName, out var sound))
        {
            return;
        }

        if (marker.Mode == GameplaySoundMode.Music)
        {
            StartGameplaySoundMusicOverride(
                new GameplayMusicOverrideRequest(
                    marker.SoundName,
                    marker.Crossfade,
                    marker.CrossfadeSeconds,
                    Loop: true,
                    FadeAfterSeconds: 0f),
                sound);
            return;
        }

        TryPlaySound(sound, 0.88f, 0f, 0f);
    }

    private void StartGameplaySoundMusicOverride(GameplayMusicOverrideRequest request, SoundEffect sound)
    {
        var soundName = request.SoundName.Trim();
        if (soundName.Length == 0)
        {
            return;
        }

        if (_gameplaySoundMusicOverrideName.Equals(soundName, StringComparison.OrdinalIgnoreCase)
            && _gameplaySoundMusicOverrideInstance is not null)
        {
            _gameplaySoundMusicOverrideFadeSeconds = request.Crossfade ? MathF.Max(0f, request.CrossfadeSeconds) : 0f;
            _gameplaySoundMusicOverrideLoop = request.Loop;
            _gameplaySoundMusicOverrideFadeAfterSeconds = MathF.Max(0f, request.FadeAfterSeconds);
            _gameplaySoundMusicOverrideFadeOutSeconds = request.Crossfade ? MathF.Max(0f, request.CrossfadeSeconds) : 0f;
            _gameplaySoundMusicOverrideLifetimeSeconds = 0f;
            _gameplaySoundMusicOverrideFadingOut = false;
            _gameplaySoundMusicOverrideInstance.IsLooped = request.Loop;
            return;
        }

        StopGameplaySoundMusicOverride();
        try
        {
            _gameplaySoundMusicOverride = sound;
            _gameplaySoundMusicOverrideInstance = sound.CreateInstance();
            _gameplaySoundMusicOverrideInstance.IsLooped = request.Loop;
            _gameplaySoundMusicOverrideInstance.Volume = request.Crossfade ? 0f : GetGameplaySoundMusicOverrideVolume();
            _gameplaySoundMusicOverrideFade = request.Crossfade ? 0f : 1f;
            _gameplaySoundMusicOverrideFadeSeconds = request.Crossfade ? MathF.Max(0f, request.CrossfadeSeconds) : 0f;
            _gameplaySoundMusicOverrideLoop = request.Loop;
            _gameplaySoundMusicOverrideLifetimeSeconds = 0f;
            _gameplaySoundMusicOverrideFadeAfterSeconds = MathF.Max(0f, request.FadeAfterSeconds);
            _gameplaySoundMusicOverrideFadeOutSeconds = request.Crossfade ? MathF.Max(0f, request.CrossfadeSeconds) : 0f;
            _gameplaySoundMusicOverrideFadingOut = false;
            _gameplaySoundMusicOverrideName = soundName;
            _gameplaySoundMusicOverrideInstance.Play();
            ApplyAudioVolumeState();
        }
        catch (Exception ex)
        {
            AddConsoleLine($"gameplay sound music unavailable: {soundName} ({ex.GetType().Name}: {ex.Message})");
            StopGameplaySoundMusicOverride();
        }
    }

    private void UpdateGameplaySounds(GameTime gameTime)
    {
        if (_gameplaySoundMusicOverrideInstance is null)
        {
            return;
        }

        var elapsedSeconds = (float)Math.Clamp(gameTime.ElapsedGameTime.TotalSeconds, 0d, 0.1d);
        if (!_gameplaySoundMusicOverrideFadingOut
            && _gameplaySoundMusicOverrideFadeAfterSeconds > 0f)
        {
            _gameplaySoundMusicOverrideLifetimeSeconds += elapsedSeconds;
            if (_gameplaySoundMusicOverrideLifetimeSeconds >= _gameplaySoundMusicOverrideFadeAfterSeconds)
            {
                _gameplaySoundMusicOverrideFadingOut = true;
            }
        }

        if (_gameplaySoundMusicOverrideFadingOut)
        {
            if (_gameplaySoundMusicOverrideFadeOutSeconds <= 0.0001f)
            {
                StopGameplaySoundMusicOverride();
                ApplyAudioVolumeState();
                return;
            }

            _gameplaySoundMusicOverrideFade = Math.Clamp(
                _gameplaySoundMusicOverrideFade - (elapsedSeconds / _gameplaySoundMusicOverrideFadeOutSeconds),
                0f,
                1f);
            if (_gameplaySoundMusicOverrideFade <= 0f)
            {
                StopGameplaySoundMusicOverride();
                ApplyAudioVolumeState();
                return;
            }
        }
        else if (_gameplaySoundMusicOverrideFade < 1f)
        {
            var fadeSeconds = MathF.Max(0.01f, _gameplaySoundMusicOverrideFadeSeconds);
            _gameplaySoundMusicOverrideFade = Math.Clamp(_gameplaySoundMusicOverrideFade + (elapsedSeconds / fadeSeconds), 0f, 1f);
        }

        if (_gameplaySoundMusicOverrideInstance.State != SoundState.Playing)
        {
            if (!_gameplaySoundMusicOverrideLoop)
            {
                StopGameplaySoundMusicOverride();
                ApplyAudioVolumeState();
                return;
            }

            if (!_gameplayAudioMusicController.CanStartMusicPlayback())
            {
                ApplyAudioVolumeState();
                return;
            }

            try
            {
                _gameplaySoundMusicOverrideInstance.Play();
            }
            catch (Exception ex)
            {
                AddConsoleLine($"gameplay sound music playback failed: {ex.GetType().Name}: {ex.Message}");
                StopGameplaySoundMusicOverride();
                return;
            }
        }

        ApplyAudioVolumeState();
    }

    private void StopGameplaySoundMusicOverride()
    {
        try { _gameplaySoundMusicOverrideInstance?.Stop(); } catch { }
        try { _gameplaySoundMusicOverrideInstance?.Dispose(); } catch { }
        _gameplaySoundMusicOverrideInstance = null;
        _gameplaySoundMusicOverride = null;
        _gameplaySoundMusicOverrideName = string.Empty;
        _gameplaySoundMusicOverrideFade = 0f;
        _gameplaySoundMusicOverrideFadeSeconds = 0f;
        _gameplaySoundMusicOverrideLoop = true;
        _gameplaySoundMusicOverrideLifetimeSeconds = 0f;
        _gameplaySoundMusicOverrideFadeAfterSeconds = 0f;
        _gameplaySoundMusicOverrideFadeOutSeconds = 0f;
        _gameplaySoundMusicOverrideFadingOut = false;
    }

    private float GetGameplaySoundUnderlyingMusicVolumeScale() =>
        IsGameplaySoundMusicOverrideActive ? 1f - _gameplaySoundMusicOverrideFade : 1f;

    private float GetGameplaySoundMusicOverrideVolume() =>
        GetNonLinearVolumeScale(_ingameMusicVolumePercent) * 0.8f * _gameplaySoundMusicOverrideFade;
}
