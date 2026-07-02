#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class ActiveGameplayMessage
    {
        public ActiveGameplayMessage(GameplayMessageMarker marker)
        {
            Marker = marker;
        }

        public GameplayMessageMarker Marker { get; }

        public float ElapsedSeconds { get; set; }
    }

    private SimpleLevel? _gameplayMessageLevel;
    private bool[] _gameplayMessagePreviousOutputs = [];
    private bool[] _gameplayMessagePreviousLocalIntersections = [];
    private int[] _gameplayMessageLocalFireCounts = [];
    private ActiveGameplayMessage? _activeGameplayMessage;
    private CustomMapVisualMetadata? _gameplayMessageSoundCacheSource;
    private readonly Dictionary<string, SoundEffect> _gameplayMessageSoundCache = new(StringComparer.OrdinalIgnoreCase);
    private float _gameplayMessageFlashWhiteSecondsRemaining;
    private float _gameplayMessageFlashWhiteDurationSeconds;
    private float _gameplayMessageFadeBlackSecondsRemaining;
    private float _gameplayMessageFadeBlackDurationSeconds;

    private bool IsGameplayMessageSimulationFreezeActive =>
        _activeGameplayMessage is { Marker.FreezeSimulation: true };

    private void AdvanceGameplayMessages()
    {
        EnsureGameplayMessageState();
        var messages = _world.Level.GameplayMessages;
        if (messages.Count == 0)
        {
            return;
        }

        var graph = _world.Level.LogicGraph;
        for (var index = 0; index < messages.Count; index += 1)
        {
            var marker = messages[index];
            var graphOutput = marker.UsesTrigger
                && marker.TriggerNodeIndex >= 0
                && marker.TriggerNodeIndex < graph.Nodes.Count
                && graph.GetOutput(marker.TriggerNodeIndex);
            var localIntersectsTrigger = IsLocalPlayerMatchingGameplayMessageTrigger(marker);
            var directLocalTrigger = IsGameplayMessageLocalPlayerTriggerRaised(
                marker,
                localIntersectsTrigger,
                _gameplayMessagePreviousLocalIntersections[index]);
            if (directLocalTrigger && !IsGameplayMessageLocalPlayerTriggerAllowed(marker, index))
            {
                directLocalTrigger = false;
            }

            var current = graphOutput || directLocalTrigger;
            if (_garrisonBuilderQuickTestActive
                && localIntersectsTrigger != _gameplayMessagePreviousLocalIntersections[index])
            {
                AddConsoleLine(
                    $"builder message diag: local {(localIntersectsTrigger ? "entered" : "left")} gameplayMessage[{index}] trigger zone " +
                    $"ref={marker.TriggerRef} node={marker.TriggerNodeIndex} output={graphOutput} " +
                    $"alive={_world.LocalPlayer.IsAlive} team={_world.LocalPlayer.Team} class={_world.LocalPlayer.ClassId} " +
                    $"awaitingJoin={_world.LocalPlayerAwaitingJoin}");
            }

            if (_garrisonBuilderQuickTestActive && directLocalTrigger && !graphOutput)
            {
                AddConsoleLine(
                    $"builder message diag: gameplayMessage[{index}] direct local trigger raised despite graph output false " +
                    $"ref={marker.TriggerRef} node={marker.TriggerNodeIndex}");
            }

            if (_garrisonBuilderQuickTestActive && current != _gameplayMessagePreviousOutputs[index])
            {
                AddConsoleLine(
                    $"builder message diag: gameplayMessage[{index}] trigger output {(_gameplayMessagePreviousOutputs[index] ? "true" : "false")} -> {(current ? "true" : "false")} " +
                    $"ref={marker.TriggerRef} node={marker.TriggerNodeIndex}");
            }

            if (current && !_gameplayMessagePreviousOutputs[index])
            {
                if (directLocalTrigger && !graphOutput)
                {
                    RecordGameplayMessageLocalPlayerTriggerFire(marker, index);
                }

                if (_garrisonBuilderQuickTestActive)
                {
                    AddConsoleLine(
                        $"builder message diag: gameplayMessage[{index}] popup triggered text=\"{marker.Text}\" " +
                        $"style={marker.Style} animation={marker.Animation} " +
                        $"at=({marker.ScreenX:0.##},{marker.ScreenY:0.##}) size=({marker.Width:0.##},{marker.Height:0.##})");
                }

                TriggerGameplayMessage(marker);
            }

            _gameplayMessagePreviousOutputs[index] = current;
            _gameplayMessagePreviousLocalIntersections[index] = localIntersectsTrigger;
        }
    }

    private void EnsureGameplayMessageState()
    {
        var messages = _world.Level.GameplayMessages;
        if (!ReferenceEquals(_gameplayMessageLevel, _world.Level)
            || _gameplayMessagePreviousOutputs.Length != messages.Count
            || _gameplayMessagePreviousLocalIntersections.Length != messages.Count
            || _gameplayMessageLocalFireCounts.Length != messages.Count)
        {
            _gameplayMessageLevel = _world.Level;
            _gameplayMessagePreviousOutputs = new bool[messages.Count];
            _gameplayMessagePreviousLocalIntersections = new bool[messages.Count];
            _gameplayMessageLocalFireCounts = new int[messages.Count];
            _activeGameplayMessage = null;
        }
    }

    private bool IsGameplayMessageLocalPlayerTriggerAllowed(GameplayMessageMarker marker, int index)
    {
        if (index < 0 || index >= _gameplayMessageLocalFireCounts.Length)
        {
            return false;
        }

        var maxFires = GetMapLogicPlayerTriggerMaxFires(marker.TriggerNodeIndex);
        return maxFires <= 0 || _gameplayMessageLocalFireCounts[index] < maxFires;
    }

    private void RecordGameplayMessageLocalPlayerTriggerFire(GameplayMessageMarker marker, int index)
    {
        if (index < 0 || index >= _gameplayMessageLocalFireCounts.Length)
        {
            return;
        }

        var maxFires = GetMapLogicPlayerTriggerMaxFires(marker.TriggerNodeIndex);
        if (maxFires <= 0)
        {
            return;
        }

        _gameplayMessageLocalFireCounts[index] = Math.Min(maxFires, _gameplayMessageLocalFireCounts[index] + 1);
    }

    private bool IsGameplayMessageLocalPlayerTriggerRaised(
        GameplayMessageMarker marker,
        bool localIntersectsTrigger,
        bool previousLocalIntersectsTrigger)
    {
        if (!marker.UsesTrigger
            || marker.TriggerNodeIndex < 0
            || marker.TriggerNodeIndex >= _world.Level.LogicGraph.Nodes.Count)
        {
            return false;
        }

        var node = _world.Level.LogicGraph.Nodes[marker.TriggerNodeIndex];
        if (node.Kind != MapLogicNodeKind.PlayerTrigger)
        {
            return false;
        }

        return node.SignalMode == MapLogicSignalMode.Latch
            ? localIntersectsTrigger
            : node.PlayerDetectMode == MapLogicPlayerDetectMode.PlayerExit
                ? previousLocalIntersectsTrigger && !localIntersectsTrigger
                : localIntersectsTrigger && !previousLocalIntersectsTrigger;
    }

    private bool IsLocalPlayerMatchingGameplayMessageTrigger(GameplayMessageMarker marker)
    {
        if (!marker.UsesTrigger
            || marker.TriggerNodeIndex < 0
            || marker.TriggerNodeIndex >= _world.Level.LogicGraph.Nodes.Count)
        {
            return false;
        }

        var node = _world.Level.LogicGraph.Nodes[marker.TriggerNodeIndex];
        if (node.Kind != MapLogicNodeKind.PlayerTrigger)
        {
            return false;
        }

        if (!_world.LocalPlayer.IsAlive
            || !PlayerTriggerMetadata.AllowsTeam(node.PlayerTriggerTeamFilter, _world.LocalPlayer.Team)
            || (node.PlayerTriggerIntelCarriersOnly && !_world.LocalPlayer.IsCarryingIntel))
        {
            return false;
        }

        if (node.PlayerTriggerRoomObjectIndex >= 0
            && IsLocalPlayerIntersectingPlayerTriggerZone(node.PlayerTriggerRoomObjectIndex))
        {
            return true;
        }

        for (var index = 0; index < node.PlayerTriggerZoneRoomObjectIndices.Length; index += 1)
        {
            if (IsLocalPlayerIntersectingPlayerTriggerZone(node.PlayerTriggerZoneRoomObjectIndices[index]))
            {
                return true;
            }
        }

        return false;
    }

    private int GetMapLogicPlayerTriggerMaxFires(int triggerNodeIndex)
    {
        if (triggerNodeIndex < 0 || triggerNodeIndex >= _world.Level.LogicGraph.Nodes.Count)
        {
            return 0;
        }

        var node = _world.Level.LogicGraph.Nodes[triggerNodeIndex];
        return node.Kind == MapLogicNodeKind.PlayerTrigger
            ? node.PlayerTriggerMaxFires
            : 0;
    }

    private void TriggerGameplayMessage(GameplayMessageMarker marker)
    {
        _activeGameplayMessage = new ActiveGameplayMessage(marker);
        if (!string.IsNullOrWhiteSpace(marker.SoundName))
        {
            TryPlayGameplayMessageSound(marker.SoundName);
        }

        if (!string.IsNullOrWhiteSpace(marker.MusicName))
        {
            TryStartGameplayMessageMusic(marker);
        }
    }

    private void TryStartGameplayMessageMusic(GameplayMessageMarker marker)
    {
        var musicName = marker.MusicName.Trim();
        if (musicName.Length == 0)
        {
            return;
        }

        if (!TryGetGameplayMessageCustomSound(musicName, out var music))
        {
            return;
        }

        StartGameplaySoundMusicOverride(
            new GameplayMusicOverrideRequest(
                musicName,
                marker.MusicCrossfade,
                marker.MusicCrossfadeSeconds,
                marker.MusicLoop,
                marker.MusicFadeAfterSeconds),
            music);
    }

    private void TryPlayGameplayMessageSound(string soundName)
    {
        var normalized = soundName.Trim();
        if (normalized.Length == 0)
        {
            return;
        }

        if (TryGetGameplayMessageCustomSound(normalized, out var customSound))
        {
            TryPlaySound(customSound, 0.88f, 0f, 0f);
            return;
        }

        if (normalized.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
        {
            normalized = Path.GetFileNameWithoutExtension(normalized);
        }

        var sound = _runtimeAssets.GetSound(normalized);
        if (sound is not null)
        {
            TryPlaySound(sound, 0.88f, 0f, 0f);
        }
    }

    private bool TryGetGameplayMessageCustomSound(string soundName, out SoundEffect sound)
    {
        sound = null!;
        var visuals = _world.Level.CustomMapVisuals;
        if (!ReferenceEquals(_gameplayMessageSoundCacheSource, visuals))
        {
            ClearGameplayMessageSoundCache();
            _gameplayMessageSoundCacheSource = visuals;
        }

        if (visuals.SoundResources.Count == 0)
        {
            return false;
        }

        foreach (var candidate in EnumerateGameplayMessageSoundResourceNames(soundName))
        {
            if (_gameplayMessageSoundCache.TryGetValue(candidate, out var cachedSound))
            {
                sound = cachedSound;
                return true;
            }

            if (!visuals.SoundResources.TryGetValue(candidate, out var resource))
            {
                continue;
            }

            try
            {
                var assetName = candidate.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase)
                    ? candidate
                    : $"{candidate}.ogg";
                sound = SoundDecodeUtility.LoadSoundEffect(resource.Bytes, assetName);
                CacheGameplayMessageSoundAliases(candidate, sound);
                return true;
            }
            catch (Exception)
            {
            }
        }

        sound = null!;
        return false;
    }

    private void CacheGameplayMessageSoundAliases(string resourceName, SoundEffect sound)
    {
        _gameplayMessageSoundCache[resourceName] = sound;
        var withoutExtension = Path.GetFileNameWithoutExtension(resourceName);
        if (!string.IsNullOrWhiteSpace(withoutExtension))
        {
            _gameplayMessageSoundCache[withoutExtension] = sound;
        }

        if (!resourceName.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
        {
            _gameplayMessageSoundCache[$"{resourceName}.ogg"] = sound;
        }
    }

    private void ClearGameplayMessageSoundCache()
    {
        var disposed = new HashSet<SoundEffect>();
        foreach (var cached in _gameplayMessageSoundCache.Values)
        {
            if (disposed.Add(cached))
            {
                cached.Dispose();
            }
        }

        _gameplayMessageSoundCache.Clear();
        _gameplayMessageSoundCacheSource = null;
    }

    private static IEnumerable<string> EnumerateGameplayMessageSoundResourceNames(string soundName)
    {
        if (string.IsNullOrWhiteSpace(soundName))
        {
            yield break;
        }

        var trimmed = soundName.Trim();
        yield return trimmed;

        var withoutExtension = Path.GetFileNameWithoutExtension(trimmed);
        if (!string.IsNullOrWhiteSpace(withoutExtension)
            && !withoutExtension.Equals(trimmed, StringComparison.OrdinalIgnoreCase))
        {
            yield return withoutExtension;
        }
        else
        {
            yield return $"{trimmed}.ogg";
        }
    }

    private void UpdateGameplayMessages(GameTime gameTime, KeyboardState keyboard, MouseState mouse)
    {
        var elapsedFrameSeconds = (float)Math.Clamp(gameTime.ElapsedGameTime.TotalSeconds, 0d, 0.1d);
        UpdateGameplayMessageEndEffects(elapsedFrameSeconds);
        if (_activeGameplayMessage is not { } active)
        {
            return;
        }

        active.ElapsedSeconds += elapsedFrameSeconds;
        var marker = active.Marker;
        var autoExpired = marker.EndMode != GameplayMessageEndMode.Input
            && active.ElapsedSeconds >= marker.DurationSeconds;
        var inputDismissed = marker.EndMode != GameplayMessageEndMode.Auto
            && ShouldDismissGameplayMessage(marker, keyboard, mouse);
        if (autoExpired || inputDismissed)
        {
            FinishGameplayMessage(active);
        }
    }

    private void UpdateGameplayMessageEndEffects(float elapsedFrameSeconds)
    {
        _gameplayMessageFlashWhiteSecondsRemaining = MathF.Max(0f, _gameplayMessageFlashWhiteSecondsRemaining - elapsedFrameSeconds);
        _gameplayMessageFadeBlackSecondsRemaining = MathF.Max(0f, _gameplayMessageFadeBlackSecondsRemaining - elapsedFrameSeconds);
    }

    private void FinishGameplayMessage(ActiveGameplayMessage active)
    {
        _activeGameplayMessage = null;
        ApplyGameplayMessageOnEndAction(active.Marker);
    }

    private void ApplyGameplayMessageOnEndAction(GameplayMessageMarker marker)
    {
        var effects = marker.OnEndEffects;
        if (effects.HasFlag(GameplayMessageOnEndEffects.FlashWhite))
        {
            _gameplayMessageFlashWhiteDurationSeconds = MathF.Max(0.05f, marker.OnEndSeconds);
            _gameplayMessageFlashWhiteSecondsRemaining = _gameplayMessageFlashWhiteDurationSeconds;
        }

        if (effects.HasFlag(GameplayMessageOnEndEffects.PlaySound))
        {
            TryPlayGameplayMessageSound(string.IsNullOrWhiteSpace(marker.OnEndSoundName)
                ? marker.SoundName
                : marker.OnEndSoundName);
        }

        if (effects.HasFlag(GameplayMessageOnEndEffects.FadeOut))
        {
            _gameplayMessageFadeBlackDurationSeconds = MathF.Max(0.05f, marker.OnEndSeconds);
            _gameplayMessageFadeBlackSecondsRemaining = _gameplayMessageFadeBlackDurationSeconds;
        }

        if (effects.HasFlag(GameplayMessageOnEndEffects.MapReset))
        {
            TryApplyGameplayMessageMapReset(marker);
        }

        if (effects.HasFlag(GameplayMessageOnEndEffects.MapTransition))
        {
            TryApplyGameplayMessageMapTransition(marker);
        }

        if (effects.HasFlag(GameplayMessageOnEndEffects.MapTeleport))
        {
            TryApplyGameplayMessageMapTeleport(marker);
        }

        if (effects.HasFlag(GameplayMessageOnEndEffects.Logic) && marker.UsesOnEndTrigger)
        {
            _world.PulseMapLogicNode(marker.OnEndTriggerNodeIndex);
        }
    }

    private void TryApplyGameplayMessageMapReset(GameplayMessageMarker marker)
    {
        var levelName = _world.Level.Name;
        var mapAreaIndex = _world.Level.MapAreaIndex;
        if (!_world.TryLoadLevel(levelName, mapAreaIndex, preservePlayerStats: false))
        {
            LogGameplayMessageOnEndFailure(marker, $"map reset failed: {levelName}");
        }
    }

    private void TryApplyGameplayMessageMapTransition(GameplayMessageMarker marker)
    {
        var levelName = marker.OnEndMapName.Trim();
        if (levelName.Length == 0)
        {
            LogGameplayMessageOnEndFailure(marker, "map transition needs onEndMap");
            return;
        }

        if (!_world.TryLoadLevel(levelName, mapAreaIndex: 1, preservePlayerStats: false))
        {
            LogGameplayMessageOnEndFailure(marker, $"map transition failed: {levelName}");
        }
    }

    private void TryApplyGameplayMessageMapTeleport(GameplayMessageMarker marker)
    {
        if (string.IsNullOrWhiteSpace(marker.OnEndTeleportExitRef)
            && MathF.Abs(marker.OnEndTeleportX) <= 0.0001f
            && MathF.Abs(marker.OnEndTeleportY) <= 0.0001f)
        {
            LogGameplayMessageOnEndFailure(marker, "map teleport needs onEndTeleportExit");
            return;
        }

        if (!string.IsNullOrWhiteSpace(marker.OnEndTeleportExitRef)
            && MathF.Abs(marker.OnEndTeleportX) <= 0.0001f
            && MathF.Abs(marker.OnEndTeleportY) <= 0.0001f)
        {
            LogGameplayMessageOnEndFailure(marker, $"map teleport exit not resolved: {marker.OnEndTeleportExitRef}");
            return;
        }

        _world.TeleportLocalPlayer(marker.OnEndTeleportX, marker.OnEndTeleportY);
    }

    private void LogGameplayMessageOnEndFailure(GameplayMessageMarker marker, string message)
    {
        if (_garrisonBuilderQuickTestActive)
        {
            AddConsoleLine($"builder message diag: {message} text=\"{marker.Text}\"");
        }
    }

    private bool ShouldDismissGameplayMessage(GameplayMessageMarker marker, KeyboardState keyboard, MouseState mouse)
    {
        var input = string.IsNullOrWhiteSpace(marker.InputBinding)
            ? "any"
            : marker.InputBinding.Trim();
        if (input.Equals("jump", StringComparison.OrdinalIgnoreCase))
        {
            return IsBindingPressed(keyboard, mouse, _inputBindings.MoveUp);
        }

        if (input.Equals("attack", StringComparison.OrdinalIgnoreCase)
            || input.Equals("fire", StringComparison.OrdinalIgnoreCase))
        {
            return mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed;
        }

        if (input.Equals("interact", StringComparison.OrdinalIgnoreCase)
            || input.Equals("use", StringComparison.OrdinalIgnoreCase))
        {
            return IsBindingPressed(keyboard, mouse, _inputBindings.InteractWeapon)
                || IsBindingPressed(keyboard, mouse, _inputBindings.UseAbility);
        }

        return HasAnyFreshGameplayMessageInput(keyboard, mouse);
    }

    private bool HasAnyFreshGameplayMessageInput(KeyboardState keyboard, MouseState mouse)
    {
        foreach (var key in keyboard.GetPressedKeys())
        {
            if (!_previousKeyboard.IsKeyDown(key))
            {
                return true;
            }
        }

        return (mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed)
            || (mouse.RightButton == ButtonState.Pressed && _previousMouse.RightButton != ButtonState.Pressed)
            || (mouse.MiddleButton == ButtonState.Pressed && _previousMouse.MiddleButton != ButtonState.Pressed)
            || IsControllerMenuConfirmPressed()
            || IsControllerMenuBackPressed();
    }

    private void DrawGameplayMessageHud(Vector2 cameraPosition)
    {
        if (_activeGameplayMessage is not { } active)
        {
            DrawGameplayMessageEndEffectsHud();
            return;
        }

        var marker = active.Marker;
        var alpha = ResolveGameplayMessageAlpha(marker, active.ElapsedSeconds);
        if (alpha <= 0.01f)
        {
            return;
        }

        var text = marker.Animation == GameplayMessageAnimation.Typing
            ? ResolveGameplayMessageTypingText(marker.Text, marker.TypingDurationSeconds, active.ElapsedSeconds)
            : marker.Text;
        var viewportBounds = new Rectangle(0, 0, ViewportWidth, ViewportHeight);
        var bounds = ResolveGameplayMessageBounds(marker, text, active.ElapsedSeconds, cameraPosition, viewportBounds, out var rotation);
        DrawGameplayMessageStyle(bounds, viewportBounds, marker, text, alpha, rotation, renderScale: 1f, active.ElapsedSeconds);
        DrawGameplayMessageEndEffectsHud();
    }

    private void DrawGameplayMessageEndEffectsHud()
    {
        var viewportBounds = new Rectangle(0, 0, ViewportWidth, ViewportHeight);
        if (_gameplayMessageFadeBlackSecondsRemaining > 0f && _gameplayMessageFadeBlackDurationSeconds > 0f)
        {
            var progress = 1f - Math.Clamp(_gameplayMessageFadeBlackSecondsRemaining / _gameplayMessageFadeBlackDurationSeconds, 0f, 1f);
            var alpha = MathF.Sin(progress * MathHelper.Pi);
            _spriteBatch.Draw(_pixel, viewportBounds, Color.Black * Math.Clamp(alpha, 0f, 1f));
        }

        if (_gameplayMessageFlashWhiteSecondsRemaining > 0f && _gameplayMessageFlashWhiteDurationSeconds > 0f)
        {
            var alpha = Math.Clamp(_gameplayMessageFlashWhiteSecondsRemaining / _gameplayMessageFlashWhiteDurationSeconds, 0f, 1f);
            _spriteBatch.Draw(_pixel, viewportBounds, Color.White * alpha);
        }
    }

    private void DrawGameplayMessagePreview(
        GameplayMessageMarker marker,
        Rectangle bounds,
        Rectangle viewportBounds,
        float alpha,
        float renderScale,
        float previewElapsedSeconds)
    {
        var elapsedSeconds = ResolveGameplayMessagePreviewElapsedSeconds(marker, previewElapsedSeconds);
        var text = marker.Animation == GameplayMessageAnimation.Typing
            ? ResolveGameplayMessageTypingText(marker.Text, marker.TypingDurationSeconds, elapsedSeconds)
            : marker.Text;
        var styledBounds = ResolveGameplayMessageStyleBounds(bounds, viewportBounds, marker, text, renderScale);
        var previewBounds = ResolveGameplayMessageAnimatedBounds(styledBounds, marker, elapsedSeconds, viewportBounds, out var rotation);
        var previewAlpha = alpha * ResolveGameplayMessageAlpha(marker, elapsedSeconds);
        DrawGameplayMessageStyle(previewBounds, viewportBounds, marker, text, previewAlpha, rotation, renderScale, elapsedSeconds);
    }

    private void DrawGameplayMessageStyle(
        Rectangle bounds,
        Rectangle viewportBounds,
        GameplayMessageMarker marker,
        string text,
        float alpha,
        float rotation,
        float renderScale,
        float elapsedSeconds)
    {
        renderScale = MathF.Max(0.1f, renderScale);
        if (marker.Style == GameplayMessageStyle.Dialogue)
        {
            DrawGameplayMessageDialogue(bounds, viewportBounds, marker, text, alpha, rotation, renderScale, elapsedSeconds);
            return;
        }

        if (marker.Style == GameplayMessageStyle.Chat)
        {
            DrawGameplayMessageChat(bounds, marker, text, alpha, rotation, renderScale);
            return;
        }

        if (marker.Style == GameplayMessageStyle.Notification)
        {
            DrawGameplayMessageNotification(bounds, viewportBounds, marker, text, alpha, rotation, renderScale);
            return;
        }

        if (marker.Style == GameplayMessageStyle.Ltd)
        {
            DrawGameplayMessageLtd(bounds, viewportBounds, marker, text, alpha, rotation, renderScale);
            return;
        }

        if (marker.Style == GameplayMessageStyle.Notification2)
        {
            DrawGameplayMessageNotification2(bounds, viewportBounds, marker, text, alpha, rotation, renderScale);
            return;
        }

        DrawGameplayMessageBasic(bounds, marker, text, alpha, rotation, renderScale);
    }

    private Rectangle ResolveGameplayMessageBounds(
        GameplayMessageMarker marker,
        string text,
        float elapsedSeconds,
        Vector2 cameraPosition,
        Rectangle viewportBounds,
        out float rotation)
    {
        rotation = 0f;
        var width = MathF.Max(16f, marker.Width);
        var height = MathF.Max(16f, marker.Height);
        var worldPlaced = IsGameplayMessageWorldPlaced(marker);
        var x = worldPlaced
            ? marker.ScreenX - cameraPosition.X
            : viewportBounds.X + ResolveGameplayMessageCoordinate(marker.ScreenX, viewportBounds.Width, width);
        var y = worldPlaced
            ? marker.ScreenY - cameraPosition.Y
            : viewportBounds.Y + ResolveGameplayMessageCoordinate(marker.ScreenY, viewportBounds.Height, height);
        var styleBounds = ResolveGameplayMessageStyleBounds(
            new Rectangle(
                (int)MathF.Round(x),
                (int)MathF.Round(y),
                (int)MathF.Round(width),
                (int)MathF.Round(height)),
            viewportBounds,
            marker,
            text,
            renderScale: 1f);

        return ResolveGameplayMessageAnimatedBounds(styleBounds, marker, elapsedSeconds, viewportBounds, out rotation);
    }

    private static Rectangle ResolveGameplayMessageAnimatedBounds(
        Rectangle bounds,
        GameplayMessageMarker marker,
        float elapsedSeconds,
        Rectangle viewportBounds,
        out float rotation)
    {
        var transform = ResolveGameplayMessageElementAnimation(marker.Animation, marker, elapsedSeconds, viewportBounds, bounds);
        rotation = transform.Rotation;
        var x = bounds.X + transform.Offset.X;
        var y = bounds.Y + transform.Offset.Y;
        var width = MathF.Max(1f, bounds.Width);
        var height = MathF.Max(1f, bounds.Height);
        return new Rectangle(
            (int)MathF.Round(x),
            (int)MathF.Round(y),
            (int)MathF.Round(width),
            (int)MathF.Round(height));
    }

    private Rectangle ResolveGameplayMessageStyleBounds(
        Rectangle bounds,
        Rectangle viewportBounds,
        GameplayMessageMarker marker,
        string text,
        float renderScale)
    {
        return marker.Style switch
        {
            GameplayMessageStyle.Notification => ResolveGameplayMessageNotificationBounds(bounds, viewportBounds, marker, renderScale),
            GameplayMessageStyle.Notification2 => ResolveGameplayMessageNotification2Bounds(bounds, viewportBounds, renderScale),
            GameplayMessageStyle.Ltd => ResolveGameplayMessageLtdBounds(bounds, marker, text, renderScale),
            _ => bounds,
        };
    }

    private Rectangle ResolveGameplayMessageLtdBounds(
        Rectangle bounds,
        GameplayMessageMarker marker,
        string text,
        float renderScale)
    {
        var scale = ResolveGameplayMessageLtdFontScale(marker, renderScale);
        var renderMarker = marker with { Font = GameplayMessageFont.Default, FontScale = scale };
        var width = MathF.Max(1f, MeasureGameplayMessageTextWidth(renderMarker, text));
        var height = MathF.Max(1f, MeasureGameplayMessageFontHeight(renderMarker));
        var padding = 2f * MathF.Max(0.1f, renderScale);
        var resolvedWidth = (int)MathF.Ceiling(width + (padding * 2f));
        var resolvedHeight = (int)MathF.Ceiling(height + (padding * 2f));
        return new Rectangle(
            (int)MathF.Round(bounds.X + ((bounds.Width - resolvedWidth) * 0.5f)),
            (int)MathF.Round(bounds.Y + ((bounds.Height - resolvedHeight) * 0.5f)),
            resolvedWidth,
            resolvedHeight);
    }

    private static float ResolveGameplayMessageCoordinate(float value, int viewportExtent, float elementExtent)
    {
        if (MathF.Abs(value) <= 1f)
        {
            return (viewportExtent * value) - (elementExtent * 0.5f);
        }

        return value;
    }

    private readonly record struct GameplayMessageElementAnimation(Vector2 Offset, float Alpha, float Rotation);

    private static GameplayMessageElementAnimation ResolveGameplayMessageElementAnimation(
        GameplayMessageAnimation animation,
        GameplayMessageMarker marker,
        float elapsedSeconds,
        Rectangle viewportBounds,
        Rectangle finalBounds)
    {
        if (animation is GameplayMessageAnimation.None or GameplayMessageAnimation.Typing)
        {
            return new GameplayMessageElementAnimation(Vector2.Zero, 1f, 0f);
        }

        var animationSeconds = ResolveGameplayMessageAnimationSeconds(marker, animation);
        var progress = Math.Clamp(elapsedSeconds / animationSeconds, 0f, 1f);
        var ease = 1f - MathF.Pow(1f - progress, 3f);
        var offset = Vector2.Zero;
        var alpha = 1f;
        var rotation = 0f;
        switch (animation)
        {
            case GameplayMessageAnimation.Fade:
                alpha = progress;
                break;
            case GameplayMessageAnimation.Spin:
                alpha = progress;
                rotation = (1f - ease) * -MathHelper.TwoPi;
                break;
            case GameplayMessageAnimation.FromLeft:
                offset.X = -(1f - ease) * (finalBounds.Right - viewportBounds.X + finalBounds.Width);
                break;
            case GameplayMessageAnimation.FromRight:
                offset.X = (1f - ease) * (viewportBounds.Right - finalBounds.X + finalBounds.Width);
                break;
            case GameplayMessageAnimation.FromBottom:
                offset.Y = (1f - ease) * (viewportBounds.Bottom - finalBounds.Y + finalBounds.Height);
                break;
            case GameplayMessageAnimation.FromTop:
                offset.Y = -(1f - ease) * (finalBounds.Bottom - viewportBounds.Y);
                break;
            case GameplayMessageAnimation.Ltd:
                alpha = ResolveGameplayMessageLtdAnnouncementAlpha(progress);
                break;
        }

        return new GameplayMessageElementAnimation(offset, alpha, rotation);
    }

    private static float ResolveGameplayMessageAnimationSeconds(GameplayMessageMarker marker) =>
        ResolveGameplayMessageAnimationSeconds(marker, marker.Animation);

    private static float ResolveGameplayMessageAnimationSeconds(GameplayMessageMarker marker, GameplayMessageAnimation animation)
    {
        if (animation == GameplayMessageAnimation.None
            || animation == GameplayMessageAnimation.Typing)
        {
            return 0.01f;
        }

        if (animation == GameplayMessageAnimation.Ltd)
        {
            return MathF.Max(0.01f, marker.DurationSeconds);
        }

        return MathF.Max(0.01f, MathF.Min(0.75f, marker.DurationSeconds * 0.4f));
    }

    private static float ResolveGameplayMessagePreviewElapsedSeconds(
        GameplayMessageMarker marker,
        float previewElapsedSeconds)
    {
        if (previewElapsedSeconds < 0f)
        {
            return MathF.Max(
                ResolveGameplayMessageStaticPreviewElapsed(marker, marker.Animation),
                ResolveGameplayMessageStaticPreviewElapsed(marker, marker.ImageAnimation));
        }

        if (marker.Animation == GameplayMessageAnimation.Typing)
        {
            var revealSeconds = MathF.Max(0.05f, marker.TypingDurationSeconds);
            return previewElapsedSeconds % (revealSeconds + 0.65f);
        }

        var animationSeconds = ResolveGameplayMessageAnimationSeconds(marker);
        var holdSeconds = marker.Animation == GameplayMessageAnimation.None ? 1.5f : 0.65f;
        var cycleSeconds = MathF.Max(0.75f, animationSeconds + holdSeconds);
        return previewElapsedSeconds % cycleSeconds;
    }

    private static float ResolveGameplayMessageStaticPreviewElapsed(
        GameplayMessageMarker marker,
        GameplayMessageAnimation animation) =>
        animation switch
        {
            GameplayMessageAnimation.Typing => marker.TypingDurationSeconds + 0.01f,
            GameplayMessageAnimation.Fade => 0.2f,
            GameplayMessageAnimation.Ltd => MathF.Max(0.01f, ResolveGameplayMessageAnimationSeconds(marker, animation) * 0.32f),
            GameplayMessageAnimation.None => 0.02f,
            _ => ResolveGameplayMessageAnimationSeconds(marker, animation) + 0.01f,
        };

    private static bool IsGameplayMessageWorldPlaced(GameplayMessageMarker marker) =>
        MathF.Abs(marker.ScreenX - marker.X) <= 0.001f
        && MathF.Abs(marker.ScreenY - marker.Y) <= 0.001f;

    private static float ResolveGameplayMessageAlpha(GameplayMessageMarker marker, float elapsedSeconds)
    {
        var alpha = 1f;
        if (marker.Animation == GameplayMessageAnimation.Fade)
        {
            alpha *= Math.Clamp(elapsedSeconds / 0.18f, 0f, 1f);
        }

        if (marker.Animation == GameplayMessageAnimation.Spin)
        {
            alpha *= Math.Clamp(elapsedSeconds / ResolveGameplayMessageAnimationSeconds(marker), 0f, 1f);
        }

        if (marker.Animation == GameplayMessageAnimation.Ltd)
        {
            var progress = Math.Clamp(elapsedSeconds / ResolveGameplayMessageAnimationSeconds(marker), 0f, 1f);
            alpha *= ResolveGameplayMessageLtdAnnouncementAlpha(progress);
        }

        if (marker.Animation == GameplayMessageAnimation.Fade
            && marker.EndMode != GameplayMessageEndMode.Input)
        {
            var remaining = marker.DurationSeconds - elapsedSeconds;
            if (remaining < 0.25f)
            {
                alpha *= Math.Clamp(remaining / 0.25f, 0f, 1f);
            }
        }

        return alpha;
    }

    private static float ResolveGameplayMessageLtdAnnouncementAlpha(float progress)
    {
        return progress < 0.32f
            ? Math.Clamp(progress / 0.32f, 0f, 1f)
            : Math.Clamp(1f - ((progress - 0.32f) / 0.68f), 0f, 1f);
    }

    private static string ResolveGameplayMessageTypingText(string text, float typingDurationSeconds, float elapsedSeconds)
    {
        if (text.Length == 0)
        {
            return text;
        }

        var revealSeconds = MathF.Max(0.05f, typingDurationSeconds);
        var count = Math.Clamp((int)MathF.Ceiling((elapsedSeconds / revealSeconds) * text.Length), 0, text.Length);
        return text[..count];
    }

    private void DrawGameplayMessageDialogue(
        Rectangle bounds,
        Rectangle viewportBounds,
        GameplayMessageMarker marker,
        string text,
        float alpha,
        float rotation,
        float renderScale,
        float elapsedSeconds)
    {
        var colors = ResolveGameplayMessageDialogueColors(marker.DialogueBoxStyle);
        var radius = ScaleGameplayMessageInteger(8f, renderScale);
        var shadowOffset = ScaleGameplayMessageInteger(5f, renderScale);
        var outlineThickness = ScaleGameplayMessageInteger(2f, renderScale);
        DrawRoundedRectangle(
            new Rectangle(bounds.X + shadowOffset, bounds.Y + shadowOffset, bounds.Width, bounds.Height),
            Color.Black * (0.35f * alpha),
            radius);
        DrawRoundedRectangleOutline(
            bounds,
            colors.Fill * (0.96f * alpha),
            colors.Outline * alpha,
            outlineThickness,
            radius);
        DrawGameplayMessageImage(marker, bounds, viewportBounds, alpha, renderScale, elapsedSeconds);
        DrawGameplayMessageText(marker, text, bounds, colors.Text * alpha, 14f * renderScale, renderScale, rotation);
    }

    private readonly record struct GameplayMessageDialogueColors(Color Fill, Color Outline, Color Text);

    private static GameplayMessageDialogueColors ResolveGameplayMessageDialogueColors(GameplayMessageDialogueBoxStyle style) =>
        style switch
        {
            GameplayMessageDialogueBoxStyle.Blue => new(
                new Color(0xD9, 0xD9, 0xB7),
                new Color(0xEB, 0xE8, 0xC6),
                new Color(0x35, 0x44, 0x4D)),
            GameplayMessageDialogueBoxStyle.Red => new(
                new Color(0xD9, 0xD9, 0xB7),
                new Color(0xEB, 0xE8, 0xC6),
                new Color(0x7E, 0x35, 0x30)),
            _ => new(
                new Color(59, 51, 46),
                new Color(213, 205, 188),
                new Color(245, 238, 220)),
        };

    private void DrawGameplayMessageLtd(
        Rectangle bounds,
        Rectangle viewportBounds,
        GameplayMessageMarker marker,
        string text,
        float alpha,
        float rotation,
        float renderScale)
    {
        var scale = ResolveGameplayMessageLtdFontScale(marker, renderScale);
        var position = new Vector2(
            bounds.X + (bounds.Width * 0.5f),
            bounds.Y + (bounds.Height * 0.5f));
        DrawGameplayMessageFontCentered(
            marker with { Font = GameplayMessageFont.Default, FontScale = scale },
            text,
            position,
            new Color(241, 232, 203) * (alpha * 0.96f),
            rotation);
    }

    private static float ResolveGameplayMessageLtdFontScale(GameplayMessageMarker marker, float renderScale) =>
        2.4f * marker.FontScale * MathF.Max(0.1f, renderScale);

    private void DrawGameplayMessageNotification2(
        Rectangle bounds,
        Rectangle viewportBounds,
        GameplayMessageMarker marker,
        string text,
        float alpha,
        float rotation,
        float renderScale)
    {
        var fillBounds = ResolveGameplayMessageNotification2Bounds(bounds, viewportBounds, renderScale);
        _spriteBatch.Draw(_pixel, fillBounds, Color.Black * (0.42f * alpha));
        DrawGameplayMessageText(marker, text, fillBounds, new Color(255, 226, 120) * alpha, 8f * renderScale, renderScale, rotation);
    }

    private void DrawGameplayMessageChat(
        Rectangle bounds,
        GameplayMessageMarker marker,
        string text,
        float alpha,
        float rotation,
        float renderScale)
    {
        var team = ResolveGameplayMessageChatTeam(marker);
        var fill = team == PlayerTeam.Red
            ? new Color(0xA5, 0x46, 0x40)
            : new Color(0x48, 0x5C, 0x67);
        var outline = team == PlayerTeam.Red
            ? new Color(0x7E, 0x35, 0x30)
            : new Color(0x35, 0x44, 0x4D);
        DrawRoundedRectangleOutline(
            bounds,
            fill * (0.92f * alpha),
            outline * alpha,
            ScaleGameplayMessageInteger(2f, renderScale),
            ScaleGameplayMessageInteger(6f, renderScale));
        DrawGameplayMessageChatText(marker, text, bounds, new Color(0xD9, 0xD9, 0xB7) * alpha, renderScale, rotation);
    }

    private void DrawGameplayMessageChatText(
        GameplayMessageMarker marker,
        string text,
        Rectangle bounds,
        Color color,
        float renderScale,
        float rotation)
    {
        var renderMarker = marker with { FontScale = marker.FontScale * renderScale };
        var horizontalPadding = ResolveGameplayMessageChatHorizontalPadding(renderScale);
        var verticalPadding = ResolveGameplayMessageChatVerticalPadding(renderScale);
        var lines = WrapGameplayMessageText(text, MathF.Max(1f, bounds.Width - (horizontalPadding * 2f)), renderMarker);
        var lineHeight = ResolveGameplayMessageChatLineHeight(renderMarker, renderScale);
        for (var index = 0; index < lines.Count; index += 1)
        {
            var line = lines[index];
            var width = MeasureGameplayMessageTextWidth(renderMarker, line);
            var x = marker.Alignment switch
            {
                GameplayMessageAlignment.Left => bounds.X + horizontalPadding,
                GameplayMessageAlignment.Right => bounds.Right - horizontalPadding - width,
                _ => bounds.X + ((bounds.Width - width) * 0.5f),
            };
            var y = bounds.Y + verticalPadding + (index * lineHeight);
            DrawGameplayMessageFont(renderMarker, line, new Vector2(x, y), color, rotation);
        }
    }

    private static float ResolveGameplayMessageChatHorizontalPadding(float renderScale) =>
        5f * MathF.Max(0.1f, renderScale);

    private static float ResolveGameplayMessageChatVerticalPadding(float renderScale) =>
        3f * MathF.Max(0.1f, renderScale);

    private float ResolveGameplayMessageChatLineHeight(GameplayMessageMarker marker, float renderScale) =>
        MathF.Max(12f * MathF.Max(0.1f, renderScale), MeasureGameplayMessageFontHeight(marker) + (1f * MathF.Max(0.1f, renderScale)));

    private PlayerTeam ResolveGameplayMessageChatTeam(GameplayMessageMarker marker) =>
        marker.ChatTeam switch
        {
            GameplayMessageChatTeam.Red => PlayerTeam.Red,
            GameplayMessageChatTeam.Blue => PlayerTeam.Blue,
            _ => _world.LocalPlayerTeam,
        };

    private void DrawGameplayMessageNotification(
        Rectangle bounds,
        Rectangle viewportBounds,
        GameplayMessageMarker marker,
        string text,
        float alpha,
        float rotation,
        float renderScale)
    {
        var logicalScale = MathF.Max(0.25f, bounds.Height / MathF.Max(1f, GameplayMessageMetadata.DefaultHeight * renderScale));
        var scale = logicalScale * renderScale;
        var noticeBounds = ResolveGameplayMessageNotificationBounds(bounds, viewportBounds, marker, renderScale);
        DrawGameplayNoticeBar(
            text,
            noticeBounds,
            scale,
            Color.White * alpha,
            marker,
            rotation);
    }

    private Rectangle ResolveGameplayMessageNotificationBounds(
        Rectangle bounds,
        Rectangle viewportBounds,
        GameplayMessageMarker marker,
        float renderScale)
    {
        var logicalScale = MathF.Max(0.25f, bounds.Height / MathF.Max(1f, GameplayMessageMetadata.DefaultHeight * renderScale));
        var scale = logicalScale * renderScale;
        var scaledMarker = marker with { FontScale = marker.FontScale * scale };
        var textHeight = MeasureGameplayMessageFontHeight(scaledMarker);
        var barHeight = Math.Max(
            ScaleGameplayMessageInteger(18f, scale),
            (int)MathF.Ceiling(textHeight + (8f * scale)));
        var y = Math.Clamp(bounds.Y, viewportBounds.Y - barHeight, viewportBounds.Bottom);
        return new Rectangle(viewportBounds.X, y, viewportBounds.Width, barHeight);
    }

    private Rectangle ResolveGameplayMessageNotification2Bounds(
        Rectangle bounds,
        Rectangle viewportBounds,
        float renderScale)
    {
        var height = Math.Max(ScaleGameplayMessageInteger(28f, renderScale), bounds.Height);
        var y = Math.Clamp(bounds.Y, viewportBounds.Y, Math.Max(viewportBounds.Y, viewportBounds.Bottom - height));
        return new Rectangle(viewportBounds.X, y, viewportBounds.Width, height);
    }

    private void DrawGameplayNoticeBar(string text, Rectangle barBounds, float scale, Color tint) =>
        DrawGameplayNoticeBar(text, barBounds, scale, tint, null, rotation: 0f);

    private void DrawGameplayNoticeBar(
        string text,
        Rectangle barBounds,
        float scale,
        Color tint,
        GameplayMessageMarker? marker,
        float rotation)
    {
        var clampedScale = MathF.Max(0.1f, scale);
        _spriteBatch.Draw(_pixel, barBounds, Color.Black * (tint.A / 255f));
        var iconPositionX = barBounds.X + (25f * clampedScale);
        if (marker is { } messageMarker)
        {
            var scaledMarker = messageMarker with
            {
                FontScale = messageMarker.FontScale * clampedScale,
            };
            var textHeight = MeasureGameplayMessageFontHeight(scaledMarker);
            var textPosition = new Vector2(
                barBounds.X + (50f * clampedScale),
                barBounds.Y + MathF.Max(0f, (barBounds.Height - textHeight) * 0.5f));
            var spriteY = barBounds.Y + (barBounds.Height * 0.5f);
            TryDrawScreenSprite(
                "GameNoticeS",
                0,
                new Vector2(iconPositionX, spriteY),
                tint,
                new Vector2(2f * clampedScale, 2f * clampedScale));

            if (!string.IsNullOrEmpty(text))
            {
                DrawGameplayMessageFont(scaledMarker, text, textPosition, tint, rotation);
            }

            return;
        }

        var fallbackTextHeight = MeasureBitmapFontHeight(clampedScale);
        var fallbackTextPosition = new Vector2(
            barBounds.X + (50f * clampedScale),
            barBounds.Y + MathF.Max(0f, (barBounds.Height - fallbackTextHeight) * 0.5f));
        var fallbackSpriteY = barBounds.Y + (barBounds.Height * 0.5f);
        TryDrawScreenSprite(
            "GameNoticeS",
            0,
            new Vector2(iconPositionX, fallbackSpriteY),
            tint,
            new Vector2(2f * clampedScale, 2f * clampedScale));
        if (!string.IsNullOrEmpty(text))
        {
            DrawHudTextLeftAligned(text, fallbackTextPosition, tint, clampedScale);
        }
    }

    private void DrawGameplayMessageBasic(
        Rectangle bounds,
        GameplayMessageMarker marker,
        string text,
        float alpha,
        float rotation,
        float renderScale)
    {
        _spriteBatch.Draw(_pixel, bounds, Color.Black * (0.38f * alpha));
        DrawGameplayMessageText(marker, text, bounds, Color.White * alpha, 8f * renderScale, renderScale, rotation);
    }

    private void DrawGameplayMessageImage(
        GameplayMessageMarker marker,
        Rectangle bounds,
        Rectangle viewportBounds,
        float alpha,
        float renderScale,
        float elapsedSeconds)
    {
        if (string.IsNullOrWhiteSpace(marker.ImageResourceName))
        {
            return;
        }

        var visuals = GetRuntimeCustomMapVisuals();
        if (visuals is null
            || !visuals.SpriteResources.TryGetValue(marker.ImageResourceName.Trim(), out var resource)
            || !TryGetCustomMapSpriteTexture(resource, out var texture))
        {
            return;
        }

        var destination = new Rectangle(
            bounds.X + (int)MathF.Round(marker.ImageOffsetX * renderScale),
            bounds.Y + (int)MathF.Round(marker.ImageOffsetY * renderScale),
            (int)MathF.Round(MathF.Max(1f, marker.ImageWidth * renderScale)),
            (int)MathF.Round(MathF.Max(1f, marker.ImageHeight * renderScale)));
        var fitted = FitGameplayMessageImageDestination(texture, destination);
        var transform = ResolveGameplayMessageElementAnimation(
            marker.ImageAnimation,
            marker,
            elapsedSeconds,
            viewportBounds,
            fitted);
        var position = new Vector2(
            fitted.X + (fitted.Width * 0.5f) + transform.Offset.X,
            fitted.Y + (fitted.Height * 0.5f) + transform.Offset.Y);
        var source = new Rectangle(0, 0, texture.Width, texture.Height);
        var scale = new Vector2(
            fitted.Width / MathF.Max(1f, texture.Width),
            fitted.Height / MathF.Max(1f, texture.Height));
        _spriteBatch.Draw(
            texture,
            position,
            source,
            Color.White * (alpha * transform.Alpha),
            transform.Rotation,
            new Vector2(texture.Width * 0.5f, texture.Height * 0.5f),
            scale,
            SpriteEffects.None,
            0f);
    }

    private static Rectangle FitGameplayMessageImageDestination(Texture2D texture, Rectangle bounds)
    {
        var textureWidth = MathF.Max(1f, texture.Width);
        var textureHeight = MathF.Max(1f, texture.Height);
        var scale = MathF.Min(bounds.Width / textureWidth, bounds.Height / textureHeight);
        var width = Math.Max(1, (int)MathF.Round(textureWidth * scale));
        var height = Math.Max(1, (int)MathF.Round(textureHeight * scale));
        return new Rectangle(
            bounds.X + ((bounds.Width - width) / 2),
            bounds.Y + ((bounds.Height - height) / 2),
            width,
            height);
    }

    private void DrawGameplayMessageText(
        GameplayMessageMarker marker,
        string text,
        Rectangle bounds,
        Color color,
        float padding,
        float renderScale,
        float rotation = 0f)
    {
        var renderMarker = marker with { FontScale = marker.FontScale * renderScale };
        var lines = WrapGameplayMessageText(text, MathF.Max(1f, bounds.Width - (padding * 2f)), renderMarker);
        var lineHeight = MeasureGameplayMessageFontHeight(renderMarker) + (2f * renderScale);
        var totalHeight = lines.Count * lineHeight;
        var y = bounds.Y + ((bounds.Height - totalHeight) * 0.5f);
        for (var index = 0; index < lines.Count; index += 1)
        {
            var line = lines[index];
            var width = MeasureGameplayMessageTextWidth(renderMarker, line);
            var x = marker.Alignment switch
            {
                GameplayMessageAlignment.Left => bounds.X + padding,
                GameplayMessageAlignment.Right => bounds.Right - padding - width,
                _ => bounds.X + ((bounds.Width - width) * 0.5f),
            };
            DrawGameplayMessageFont(renderMarker, line, new Vector2(x, y + (index * lineHeight)), color, rotation);
        }
    }

    private static int ScaleGameplayMessageInteger(float value, float scale) =>
        Math.Max(1, (int)MathF.Round(value * MathF.Max(0.1f, scale)));

    private List<string> WrapGameplayMessageText(string text, float maxWidth, GameplayMessageMarker marker)
    {
        var lines = new List<string>();
        if (string.IsNullOrWhiteSpace(text))
        {
            lines.Add(string.Empty);
            return lines;
        }

        var words = text.Replace("\\n", "\n", StringComparison.Ordinal).Split(' ');
        var current = string.Empty;
        foreach (var rawWord in words)
        {
            var word = rawWord;
            if (word.Contains('\n'))
            {
                var parts = word.Split('\n');
                for (var partIndex = 0; partIndex < parts.Length; partIndex += 1)
                {
                    AddGameplayMessageWord(lines, ref current, parts[partIndex], maxWidth, marker);
                    if (partIndex + 1 < parts.Length)
                    {
                        lines.Add(current);
                        current = string.Empty;
                    }
                }

                continue;
            }

            AddGameplayMessageWord(lines, ref current, word, maxWidth, marker);
        }

        if (current.Length > 0 || lines.Count == 0)
        {
            lines.Add(current);
        }

        return lines;
    }

    private void AddGameplayMessageWord(
        List<string> lines,
        ref string current,
        string word,
        float maxWidth,
        GameplayMessageMarker marker)
    {
        if (word.Length == 0)
        {
            return;
        }

        var candidate = current.Length == 0 ? word : $"{current} {word}";
        if (current.Length > 0 && MeasureGameplayMessageTextWidth(marker, candidate) > maxWidth)
        {
            lines.Add(current);
            current = word;
            return;
        }

        current = candidate;
    }

    private float MeasureGameplayMessageTextWidth(GameplayMessageMarker marker, string text)
    {
        return marker.Font switch
        {
            GameplayMessageFont.GG2Build => MeasureMenuBitmapFontWidth(text, marker.FontScale),
            GameplayMessageFont.Count => MeasureSpriteFontWidth(CountFontDefinition, text, marker.FontScale),
            GameplayMessageFont.Timer => MeasureSpriteFontWidth(TimerFontDefinition, text, marker.FontScale),
            _ => MeasureBitmapFontWidth(text, marker.FontScale),
        };
    }

    private float MeasureGameplayMessageFontHeight(GameplayMessageMarker marker)
    {
        return marker.Font switch
        {
            GameplayMessageFont.GG2Build => MeasureMenuBitmapFontHeight(marker.FontScale),
            GameplayMessageFont.Count => MeasureSpriteFontHeight(CountFontDefinition, marker.FontScale),
            GameplayMessageFont.Timer => MeasureSpriteFontHeight(TimerFontDefinition, marker.FontScale),
            _ => MeasureBitmapFontHeight(marker.FontScale),
        };
    }

    private void DrawGameplayMessageFont(GameplayMessageMarker marker, string text, Vector2 position, Color color, float rotation)
    {
        var width = MeasureGameplayMessageTextWidth(marker, text);
        var height = MeasureGameplayMessageFontHeight(marker);
        var rotationCenter = new Vector2(position.X + (width * 0.5f), position.Y + (height * 0.5f));
        switch (marker.Font)
        {
            case GameplayMessageFont.GG2Build:
                DrawMenuBitmapFontText(text, position, color, marker.FontScale, rotation, rotationCenter);
                break;
            case GameplayMessageFont.Count:
                DrawSpriteFontText(CountFontDefinition, text, position, color, marker.FontScale, rotation, rotationCenter);
                break;
            case GameplayMessageFont.Timer:
                DrawSpriteFontText(TimerFontDefinition, text, position, color, marker.FontScale, rotation, rotationCenter);
                break;
            default:
                DrawSpriteFontText(BitmapFontDefinition, text, position, color, marker.FontScale, rotation, rotationCenter);
                break;
        }
    }

    private void DrawGameplayMessageFontCentered(
        GameplayMessageMarker marker,
        string text,
        Vector2 position,
        Color color,
        float rotation)
    {
        var width = MeasureGameplayMessageTextWidth(marker, text);
        var height = MeasureGameplayMessageFontHeight(marker);
        DrawGameplayMessageFont(marker, text, new Vector2(position.X - (width * 0.5f), position.Y - (height * 0.5f)), color, rotation);
    }

    private void DrawMenuBitmapFontText(
        string text,
        Vector2 position,
        Color color,
        float scale,
        float rotation,
        Vector2 rotationCenter)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var drawColor = ApplyCurrentHudElementOpacity(color);
        if (_menuBitmapFontTexture is null || _menuBitmapFontGlyphs.Count == 0)
        {
            _spriteBatch.DrawString(_menuFont, text, rotationCenter, drawColor, rotation, (rotationCenter - position) / scale, scale, SpriteEffects.None, 0f);
            return;
        }

        var cursor = rotation == 0f ? SnapTextPosition(position) : position;
        for (var index = 0; index < text.Length; index += 1)
        {
            var character = text[index];
            if (!_menuBitmapFontGlyphs.TryGetValue(character, out var glyph))
            {
                if (_menuBitmapFontGlyphs.TryGetValue(' ', out var spaceGlyph))
                {
                    cursor.X += (spaceGlyph.Advance + _menuBitmapFontSpacing) * scale;
                }

                continue;
            }

            if (glyph.SourceRect.Width > 0 && glyph.SourceRect.Height > 0)
            {
                if (rotation == 0f)
                {
                    _spriteBatch.Draw(
                        _menuBitmapFontTexture.Texture,
                        new Rectangle(
                            (int)MathF.Round(cursor.X),
                            (int)MathF.Round(cursor.Y),
                            Math.Max(1, (int)MathF.Round(glyph.SourceRect.Width * scale)),
                            Math.Max(1, (int)MathF.Round(glyph.SourceRect.Height * scale))),
                        CombineSourceRectangles(_menuBitmapFontTexture.SourceRectangle, glyph.SourceRect),
                        drawColor);
                }
                else
                {
                    _spriteBatch.Draw(
                        _menuBitmapFontTexture.Texture,
                        rotationCenter,
                        CombineSourceRectangles(_menuBitmapFontTexture.SourceRectangle, glyph.SourceRect),
                        drawColor,
                        rotation,
                        (rotationCenter - cursor) / scale,
                        scale,
                        SpriteEffects.None,
                        0f);
                }
            }

            cursor.X += (glyph.Advance + _menuBitmapFontSpacing) * scale;
        }
    }
}
