#nullable enable

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const int ReplaySeekStepMilliseconds = 5000;

    private readonly record struct ReplayPlaybackControlLayout(
        Rectangle Panel,
        Rectangle Status,
        Rectangle Backward,
        Rectangle Forward);

    private LoadedSpriteFrame? _replayBackwardArrow;
    private LoadedSpriteFrame? _replayForwardArrow;
    private bool _replayPlaybackControlAssetsLoaded;
    private bool _replaySeekCatchUpActive;
    private int _replaySeekTargetMilliseconds;

    private void UpdateReplayPlaybackControls(KeyboardState keyboard, MouseState mouse)
    {
        if (!TryGetInteractiveReplayPlaybackState(out var state))
        {
            return;
        }

        var layout = GetReplayPlaybackControlLayout(ViewportWidth, ViewportHeight);
        var mousePressed = mouse.LeftButton == ButtonState.Pressed
            && _previousMouse.LeftButton == ButtonState.Released;
        var seekBackward = IsKeyPressed(keyboard, Keys.Left)
            || (mousePressed && layout.Backward.Contains(mouse.X, mouse.Y));
        var seekForward = IsKeyPressed(keyboard, Keys.Right)
            || (mousePressed && layout.Forward.Contains(mouse.X, mouse.Y));

        if (seekBackward && state.PositionMilliseconds > 0)
        {
            SeekOpenGarrisonDemo(-ReplaySeekStepMilliseconds);
        }
        else if (seekForward && state.PositionMilliseconds < Math.Max(0, state.DurationMilliseconds - 1))
        {
            SeekOpenGarrisonDemo(ReplaySeekStepMilliseconds);
        }
    }

    private void SeekOpenGarrisonDemo(int deltaMilliseconds)
    {
        if (TrySeekOpenGarrisonDemo(deltaMilliseconds, out var targetMilliseconds, out var error))
        {
            AddNetworkConsoleLine($"replay seek: {FormatReplayPlaybackTime(targetMilliseconds)}");
        }
        else if (!string.IsNullOrWhiteSpace(error)
            && !error.Contains("already at", StringComparison.OrdinalIgnoreCase))
        {
            AddNetworkConsoleLine($"replay seek failed: {error}");
        }
    }

    private void DrawReplayPlaybackControls(MouseState mouse)
    {
        if (!TryGetInteractiveReplayPlaybackState(out var state))
        {
            return;
        }

        EnsureReplayPlaybackControlAssets();
        var layout = GetReplayPlaybackControlLayout(ViewportWidth, ViewportHeight);
        _spriteBatch.Draw(_pixel, layout.Panel, new Color(7, 12, 18, 210));
        DrawReplayControlBorder(layout.Panel, new Color(220, 226, 232, 210));

        var maximumPosition = Math.Max(0, state.DurationMilliseconds - 1);
        DrawReplaySeekButton(
            layout.Backward,
            _replayBackwardArrow,
            "-5s",
            enabled: !state.IsSeekCatchUpPending && state.PositionMilliseconds > 0,
            hovered: layout.Backward.Contains(mouse.X, mouse.Y));
        DrawReplaySeekButton(
            layout.Forward,
            _replayForwardArrow,
            "+5s",
            enabled: !state.IsSeekCatchUpPending && state.PositionMilliseconds < maximumPosition,
            hovered: layout.Forward.Contains(mouse.X, mouse.Y));

        var playbackLabel = state.IsSeekCatchUpPending
            ? $"SEEKING {FormatReplayPlaybackTime(_replaySeekTargetMilliseconds)}"
            : $"REPLAY  {FormatReplayPlaybackTime(state.PositionMilliseconds)} / {FormatReplayPlaybackTime(state.DurationMilliseconds)}";
        if (state.IsPaused && !state.IsSeekCatchUpPending)
        {
            playbackLabel += "  PAUSED";
        }

        DrawReplayControlText(playbackLabel, layout.Status, Color.White, 0.8f);
    }

    private bool TryGetInteractiveReplayPlaybackState(out NetworkGameClient.ReplayPlaybackState state)
    {
        if (_gameplayHudHidden
            || !_networkClient.IsConnected
            || !_networkClient.IsReplayConnection
            || _mainMenuOpen
            || _loadingOverlayVisible
            || _consoleOpen
            || _chatOpen
            || _passwordPromptOpen
            || _teamSelectOpen
            || _classSelectOpen
            || GetActiveGameplayOverlay() != GameplayOverlayKind.None
            || !_networkClient.TryGetReplayPlaybackState(out state)
            || !state.CanSeek)
        {
            state = default;
            return false;
        }

        return true;
    }

    private static ReplayPlaybackControlLayout GetReplayPlaybackControlLayout(int viewportWidth, int viewportHeight)
    {
        var buttonSize = Math.Clamp((int)MathF.Round(MathF.Min(viewportWidth, viewportHeight) * 0.065f), 34, 52);
        var gap = Math.Max(6, buttonSize / 6);
        var margin = Math.Max(14, buttonSize / 3);
        var statusHeight = Math.Max(20, buttonSize / 2);
        var panelPadding = 7;
        var forward = new Rectangle(
            viewportWidth - margin - buttonSize,
            viewportHeight - margin - buttonSize,
            buttonSize,
            buttonSize);
        var backward = new Rectangle(forward.X - gap - buttonSize, forward.Y, buttonSize, buttonSize);
        var status = new Rectangle(backward.X, backward.Y - statusHeight - 4, (buttonSize * 2) + gap, statusHeight);
        var panel = Rectangle.Union(Rectangle.Union(backward, forward), status);
        panel.Inflate(panelPadding, panelPadding);
        return new ReplayPlaybackControlLayout(panel, status, backward, forward);
    }

    private void DrawReplaySeekButton(
        Rectangle bounds,
        LoadedSpriteFrame? arrow,
        string label,
        bool enabled,
        bool hovered)
    {
        var background = !enabled
            ? new Color(38, 42, 47, 190)
            : hovered
                ? new Color(83, 126, 164, 235)
                : new Color(48, 66, 82, 225);
        var tint = enabled ? Color.White : new Color(125, 130, 136, 210);
        _spriteBatch.Draw(_pixel, bounds, background);
        DrawReplayControlBorder(bounds, enabled && hovered ? Color.White : new Color(165, 175, 184, 220));

        var arrowBounds = bounds;
        arrowBounds.Inflate(-Math.Max(5, bounds.Width / 7), -Math.Max(5, bounds.Height / 7));
        if (arrow is not null)
        {
            DrawLoadedSpriteFrame(arrow, arrowBounds, tint);
        }
        else
        {
            DrawReplayControlText(label[0].ToString(), arrowBounds, tint, 1f);
        }

        var labelBounds = new Rectangle(bounds.X, bounds.Bottom - 15, bounds.Width, 13);
        DrawReplayControlText(label, labelBounds, tint, 0.62f);
    }

    private void DrawReplayControlText(string text, Rectangle bounds, Color color, float maximumScale)
    {
        var measured = _consoleFont.MeasureString(text);
        if (measured.X <= 0f || measured.Y <= 0f)
        {
            return;
        }

        var scale = MathF.Min(maximumScale, MathF.Min(bounds.Width / measured.X, bounds.Height / measured.Y));
        var position = new Vector2(
            bounds.X + ((bounds.Width - (measured.X * scale)) * 0.5f),
            bounds.Y + ((bounds.Height - (measured.Y * scale)) * 0.5f));
        _spriteBatch.DrawString(_consoleFont, text, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    private void DrawReplayControlBorder(Rectangle bounds, Color color)
    {
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), color);
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), color);
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), color);
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), color);
    }

    private void EnsureReplayPlaybackControlAssets()
    {
        if (_replayPlaybackControlAssetsLoaded)
        {
            return;
        }

        _replayPlaybackControlAssetsLoaded = true;
        _replayBackwardArrow = LoadSpriteFrameFromPath(ContentRoot.GetPath(
            "Sprites", "Menu", "LastToDie", "CharacterSelect", "arrowright.png"));
        _replayForwardArrow = LoadSpriteFrameFromPath(ContentRoot.GetPath(
            "Sprites", "Menu", "LastToDie", "CharacterSelect", "arrowleft.png"));
    }

    private void DisposeReplayPlaybackControlAssets()
    {
        _replayBackwardArrow?.Dispose();
        _replayForwardArrow?.Dispose();
        _replayBackwardArrow = null;
        _replayForwardArrow = null;
        _replayPlaybackControlAssetsLoaded = false;
    }

    private void CompleteReplaySeekCatchUpIfReady(bool receivedSnapshot)
    {
        if (_replaySeekCatchUpActive)
        {
            if (_networkClient.TryGetReplayPlaybackState(out var state) && state.IsSeekCatchUpPending)
            {
                return;
            }

            _replaySeekCatchUpActive = false;
            _pendingNetworkVisualEvents.Clear();
            _pendingNetworkSoundEvents.Clear();
            _pendingNetworkDamageEvents.Clear();
            _ = _world.DrainPendingSoundEvents();
            HideLoadingOverlay();
            SetNetworkStatus($"Replay at {FormatReplayPlaybackTime(_replaySeekTargetMilliseconds)}.");
            return;
        }

        if (_networkClient.IsReplayConnection && receivedSnapshot)
        {
            HideLoadingOverlay();
        }
    }

    internal static string FormatReplayPlaybackTime(int milliseconds)
    {
        var totalSeconds = Math.Max(0, milliseconds) / 1000;
        return $"{totalSeconds / 60}:{totalSeconds % 60:00}";
    }
}
