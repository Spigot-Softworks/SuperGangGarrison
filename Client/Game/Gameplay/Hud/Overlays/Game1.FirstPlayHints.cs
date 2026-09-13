using Microsoft.Xna.Framework;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private FirstPlayHintSequence? _firstPlayHints;

    private bool CanPresentFirstPlayHints() =>
        _gameplaySessionKind is GameplaySessionKind.Online or GameplaySessionKind.Practice or GameplaySessionKind.LastToDie
        && !_mainMenuOpen && !_startupSplashOpen && !_loadingOverlayVisible
        && !_inGameMenuOpen && !_teamSelectOpen && !_classSelectOpen && !_consoleOpen && !_hudEditorOpen
        && !_builderEditorEnabled && !_garrisonBuilderQuickTestActive && !_networkClient.IsReplayConnection
        && !_lastToDieSurvivorMenuOpen && !_lastToDiePerkMenuOpen && !_lastToDieStageClearOverlayOpen && !_lastToDieFailureOverlayOpen
        && !_world.LocalPlayerAwaitingJoin && _world.LocalPlayer.IsAlive && !_world.MatchState.IsEnded
        && !IsLocalSpectatorPresentationActive() && !IsGameplayDeathCamActive() && !IsHostedLastToDieBlockingGameplay()
        && _activeGameplayMessage is null
        && _gameplayMessageFlashWhiteSecondsRemaining <= 0f && _gameplayMessageFadeBlackSecondsRemaining <= 0f;

    private void UpdateFirstPlayHints(GameTime gameTime)
    {
        var canPresent = IsGameplayWindowInputActive() && CanPresentFirstPlayHints();
        if (_firstPlayHints is null)
        {
            if (!canPresent) return;
            _firstPlayHints = new FirstPlayHintSequence(FirstPlayHintsDocument.Load().HasShown);
        }
        if (_firstPlayHints.Update((float)gameTime.ElapsedGameTime.TotalSeconds, canPresent))
        {
            // Save at the first actual presentation, not on shutdown. Disconnects,
            // class changes, another game mode, and browser reloads never replay it.
            new FirstPlayHintsDocument { HasShown = true }.Save();
        }
    }

    private void DrawFirstPlayHints(Vector2 cameraPosition)
    {
        if (_firstPlayHints is not { Visible: true } sequence || !CanPresentFirstPlayHints()) return;
        var marker = sequence.Marker;
        var elapsed = sequence.ElapsedSeconds;
        var viewport = new Rectangle(0, 0, ViewportWidth, ViewportHeight);
        var alpha = ResolveGameplayMessageAlpha(marker, elapsed);
        // For introductory tips the authored end fade applies to the box itself.
        if (marker.OnEndEffects.HasFlag(GameplayMessageOnEndEffects.FadeOut))
            alpha *= 1f - Math.Clamp((elapsed - marker.DurationSeconds) / marker.OnEndSeconds, 0f, 1f);
        var playerBounds = GetPlayerScreenBounds(_world.LocalPlayer, GetRenderPosition(_world.LocalPlayer), cameraPosition);
        var bounds = FirstPlayHintPlacement.AbovePlayer(marker, playerBounds, viewport);
        bounds = ResolveGameplayMessageAnimatedBounds(bounds, marker, elapsed, viewport, out var rotation);
        DrawGameplayMessageStyle(bounds, viewport, marker, marker.Text, alpha, rotation, 1f, elapsed);
    }
}

internal static class FirstPlayHintPlacement
{
    public static Rectangle AbovePlayer(GameplayMessageMarker marker, Rectangle playerBounds, Rectangle viewport)
    {
        var width = (int)MathF.Round(marker.Width);
        var height = (int)MathF.Round(marker.Height);
        return new Rectangle(
            (int)Math.Clamp(playerBounds.Center.X - width / 2f, viewport.Left + 4f, Math.Max(viewport.Left + 4f, viewport.Right - width - 4f)),
            Math.Max(viewport.Top + 4, playerBounds.Top - 15 - height), width, height);
    }
}
