#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Core;
using OpenGarrison.Client.Plugins;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const int LastToDieDeathFocusDurationTicks = 54;
    private const int LastToDieDeathFocusZoomDelayTicks = 6;
    private const int LastToDieDeathFocusZoomTicks = 42;
    private const float LastToDieDeathFocusZoomStart = 1f;
    private const float LastToDieDeathFocusZoomEnd = 1.82f;
    private const float LastToDieDeathFocusCorpsePlaybackRate = 1f;
    private const float LastToDieDeathFocusBaseDimMaxAlpha = 0.2f;
    private const float LastToDieDeathFocusEdgeDimMaxAlpha = 0.84f;
    private const float LastToDieFailureCorpseScale = 3f;

    private sealed class LastToDieDeathFocusState
    {
        public LastToDieDeathFocusState(
            int sourcePlayerId,
            PlayerClass classId,
            PlayerTeam team,
            DeadBodyAnimationKind animationKind,
            Vector2 worldPosition,
            float width,
            float height,
            bool facingLeft)
        {
            SourcePlayerId = sourcePlayerId;
            ClassId = classId;
            Team = team;
            AnimationKind = animationKind;
            WorldPosition = worldPosition;
            Width = width;
            Height = height;
            FacingLeft = facingLeft;
        }

        public int SourcePlayerId { get; }

        public PlayerClass ClassId { get; }

        public PlayerTeam Team { get; }

        public DeadBodyAnimationKind AnimationKind { get; }

        public Vector2 WorldPosition { get; }

        public float Width { get; }

        public float Height { get; }

        public bool FacingLeft { get; }

        public int ElapsedTicks { get; set; }
    }

    private LastToDieDeathFocusState? _lastToDieDeathFocus;
    private RenderTarget2D? _lastToDieDeathFocusTarget;
    private RenderTarget2D? _lastToDieFailureCorpseTarget;
    private bool _lastToDieFailureCorpseTargetHasVisual;

    private bool IsLastToDieDeathFocusPresentationActive()
    {
        return IsLastToDieSessionActive && _lastToDieDeathFocus is not null;
    }

    private bool IsLastToDieFailurePresentationActive()
    {
        return IsLastToDieDeathFocusPresentationActive() || IsLastToDieFailureOverlayActive();
    }

    private void ClearLastToDieDeathFocusPresentation()
    {
        _lastToDieDeathFocus = null;
        _lastToDieFailureCorpseTargetHasVisual = false;
    }

    private void TriggerLastToDieDeathFocusFailure()
    {
        if (IsLastToDieFailurePresentationActive())
        {
            return;
        }

        StopIngameMusic();
        StopLastToDieIngameMusic();
        CloseInGameMenu();

        var localPlayer = _world.LocalPlayer;
        var fallbackClassId = GetLastToDieLocalDeathFocusClassId();
        var fallbackAnimationKind = GetLastToDieLocalDeathFocusAnimationKind(fallbackClassId);
        var focusState = new LastToDieDeathFocusState(
            localPlayer.Id,
            fallbackClassId,
            localPlayer.Team,
            fallbackAnimationKind,
            new Vector2(localPlayer.X, localPlayer.Y),
            localPlayer.Width,
            localPlayer.Height,
            IsFacingLeftByAim(localPlayer));

        if (TryGetLastToDieLocalCorpseVisual(
            out var corpseClassId,
            out var corpseTeam,
            out var corpseAnimationKind,
            out var corpsePosition,
            out var corpseWidth,
            out var corpseHeight,
            out var corpseFacingLeft))
        {
            focusState = new LastToDieDeathFocusState(
                localPlayer.Id,
                corpseClassId,
                corpseTeam,
                corpseAnimationKind,
                corpsePosition,
                corpseWidth,
                corpseHeight,
                corpseFacingLeft);
        }

        _lastToDieDeathFocus = focusState;
    }

    private bool TryGetLastToDieLocalCorpseVisual(
        out PlayerClass classId,
        out PlayerTeam team,
        out DeadBodyAnimationKind animationKind,
        out Vector2 worldPosition,
        out float width,
        out float height,
        out bool facingLeft)
    {
        for (var index = 0; index < _world.DeadBodies.Count; index += 1)
        {
            var deadBody = _world.DeadBodies[index];
            if (deadBody.SourcePlayerId != _world.LocalPlayer.Id)
            {
                continue;
            }

            classId = deadBody.ClassId;
            team = deadBody.Team;
            animationKind = deadBody.AnimationKind;
            worldPosition = new Vector2(deadBody.X, deadBody.Y);
            width = deadBody.Width;
            height = deadBody.Height;
            facingLeft = deadBody.FacingLeft;
            return true;
        }

        classId = default;
        team = default;
        animationKind = DeadBodyAnimationKind.Default;
        worldPosition = default;
        width = 0f;
        height = 0f;
        facingLeft = false;
        return false;
    }

    private void UpdateLastToDieDeathFocusPresentation()
    {
        if (_lastToDieDeathFocus is null)
        {
            return;
        }

        _lastToDieDeathFocus.ElapsedTicks += 1;
        if (!_lastToDieFailureOverlayOpen
            && _lastToDieDeathFocus.ElapsedTicks >= LastToDieDeathFocusDurationTicks)
        {
            OpenLastToDieFailureOverlay();
        }
    }

    private void OpenLastToDieFailureOverlay()
    {
        StopIngameMusic();
        StopLastToDieIngameMusic();
        CloseInGameMenu();
        if (_lastToDieFailureOverlayOpen)
        {
            return;
        }

        _lastToDieFailureOverlayOpen = true;
        _lastToDieFailureOverlayTicks = 0;
        PlayLastToDieGameOverSound();
    }

    private Vector2 GetLastToDieDeathFocusCameraTopLeft(int viewportWidth, int viewportHeight)
    {
        var focusState = _lastToDieDeathFocus!;
        var halfViewportWidth = viewportWidth / 2f;
        var halfViewportHeight = viewportHeight / 2f;
        var x = Math.Clamp(
            focusState.WorldPosition.X - halfViewportWidth,
            0f,
            Math.Max(0f, _world.Bounds.Width - viewportWidth));
        var y = Math.Clamp(
            focusState.WorldPosition.Y - halfViewportHeight,
            0f,
            Math.Max(0f, _world.Bounds.Height - viewportHeight));
        return new Vector2(x, y);
    }

    private static float GetLastToDieDeathFocusProgress(LastToDieDeathFocusState focusState)
    {
        return Math.Clamp(focusState.ElapsedTicks / (float)LastToDieDeathFocusDurationTicks, 0f, 1f);
    }

    private static float GetLastToDieDeathFocusZoom(LastToDieDeathFocusState focusState)
    {
        var zoomProgress = Math.Clamp(
            (focusState.ElapsedTicks - LastToDieDeathFocusZoomDelayTicks) / (float)LastToDieDeathFocusZoomTicks,
            0f,
            1f);
        return MathHelper.SmoothStep(
            LastToDieDeathFocusZoomStart,
            LastToDieDeathFocusZoomEnd,
            zoomProgress);
    }

    private static int GetLastToDieDeathFocusCorpseElapsedTicks(LastToDieDeathFocusState focusState)
    {
        return Math.Clamp(
            (int)MathF.Floor(focusState.ElapsedTicks * LastToDieDeathFocusCorpsePlaybackRate),
            0,
            DeadBodyEntity.LifetimeTicks);
    }

    private void EnsureLastToDieDeathFocusTarget(int viewportWidth, int viewportHeight)
    {
        if (_lastToDieDeathFocusTarget is not null
            && _lastToDieDeathFocusTarget.Width == viewportWidth
            && _lastToDieDeathFocusTarget.Height == viewportHeight)
        {
            return;
        }

        _lastToDieDeathFocusTarget?.Dispose();
        _lastToDieDeathFocusTarget = new RenderTarget2D(
            GraphicsDevice,
            viewportWidth,
            viewportHeight,
            false,
            SurfaceFormat.Color,
            DepthFormat.None,
            0,
            RenderTargetUsage.PreserveContents);
    }

    private void EnsureLastToDieFailureCorpseTarget(int viewportWidth, int viewportHeight)
    {
        if (_lastToDieFailureCorpseTarget is not null
            && _lastToDieFailureCorpseTarget.Width == viewportWidth
            && _lastToDieFailureCorpseTarget.Height == viewportHeight)
        {
            return;
        }

        _lastToDieFailureCorpseTarget?.Dispose();
        _lastToDieFailureCorpseTarget = new RenderTarget2D(
            GraphicsDevice,
            viewportWidth,
            viewportHeight,
            false,
            SurfaceFormat.Color,
            DepthFormat.None,
            0,
            RenderTargetUsage.PreserveContents);
    }

    private void PrepareLastToDieDeathFocusOverlayIfNeeded(int viewportWidth, int viewportHeight)
    {
        if (!IsLastToDieDeathFocusPresentationActive())
        {
            return;
        }

        EnsureLastToDieDeathFocusTarget(viewportWidth, viewportHeight);
        var focusCameraPosition = GetLastToDieDeathFocusCameraTopLeft(viewportWidth, viewportHeight);
        GraphicsDevice.SetRenderTarget(_lastToDieDeathFocusTarget);
        GraphicsDevice.Clear(new Color(24, 32, 48));
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp, rasterizerState: RasterizerState.CullNone);
        DrawGameplayWorldForCamera(
            focusCameraPosition,
            viewportWidth,
            viewportHeight,
            skippedDeadBodySourcePlayerId: _lastToDieDeathFocus!.SourcePlayerId);
        DrawLastToDieDeathFocusCorpse(focusCameraPosition);
        _spriteBatch.End();
        GraphicsDevice.SetRenderTarget(null);

        PrepareLastToDieFailureCorpseOverlayIfNeeded(viewportWidth, viewportHeight);
    }

    private void PrepareLastToDieFailureCorpseOverlayIfNeeded(int viewportWidth, int viewportHeight)
    {
        _lastToDieFailureCorpseTargetHasVisual = false;
        if (_lastToDieDeathFocus is null || !IsLastToDieFailureOverlayActive())
        {
            return;
        }

        EnsureLastToDieFailureCorpseTarget(viewportWidth, viewportHeight);
        if (_lastToDieFailureCorpseTarget is null)
        {
            return;
        }

        var corpsePosition = new Vector2(viewportWidth / 2f, viewportHeight * 0.54f);
        GraphicsDevice.SetRenderTarget(_lastToDieFailureCorpseTarget);
        GraphicsDevice.Clear(Color.Transparent);
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp, rasterizerState: RasterizerState.CullNone);
        var syntheticTicksRemaining = Math.Max(
            0,
            DeadBodyEntity.LifetimeTicks - GetLastToDieDeathFocusCorpseElapsedTicks(_lastToDieDeathFocus));
        var pluginAnimationKind = ResolveClientPluginDeadBodyAnimationKind(
            _lastToDieDeathFocus.SourcePlayerId,
            _lastToDieDeathFocus.ClassId,
            _lastToDieDeathFocus.Team,
            _lastToDieDeathFocus.AnimationKind);
        _lastToDieFailureCorpseTargetHasVisual = TryDrawClientPluginDeadBody(Vector2.Zero, new ClientDeadBodyRenderState(
            -1,
            ToClientPluginClass(_lastToDieDeathFocus.ClassId),
            ToClientPluginTeam(_lastToDieDeathFocus.Team),
            corpsePosition,
            _lastToDieDeathFocus.Width,
            _lastToDieDeathFocus.Height,
            _lastToDieDeathFocus.FacingLeft,
            syntheticTicksRemaining,
            pluginAnimationKind));
        if (!_lastToDieFailureCorpseTargetHasVisual)
        {
            _lastToDieFailureCorpseTargetHasVisual = TryDrawLastToDieFailureCorpseSprite(corpsePosition, _lastToDieDeathFocus);
        }

        _spriteBatch.End();
        GraphicsDevice.SetRenderTarget(null);
    }

    private bool TryDrawLastToDieFailureCorpseSprite(Vector2 corpsePosition, LastToDieDeathFocusState focusState)
    {
        var spriteName = GetDeadBodySpriteName(focusState.ClassId, focusState.Team, focusState.AnimationKind);
        if (spriteName is null)
        {
            return false;
        }

        var sprite = _runtimeAssets.GetSprite(spriteName);
        if (sprite is null || sprite.Frames.Count == 0)
        {
            return false;
        }

        _spriteBatch.Draw(
            sprite.Frames[0],
            corpsePosition,
            null,
            Color.White,
            0f,
            sprite.Origin.ToVector2(),
            new Vector2(focusState.FacingLeft ? -1f : 1f, 1f),
            SpriteEffects.None,
            0f);
        return true;
    }

    private void DrawLastToDieDeathFocusCorpse(Vector2 cameraPosition)
    {
        if (_lastToDieDeathFocus is null)
        {
            return;
        }

        var syntheticTicksRemaining = Math.Max(
            0,
            DeadBodyEntity.LifetimeTicks - GetLastToDieDeathFocusCorpseElapsedTicks(_lastToDieDeathFocus));
        DrawDeadBodyVisual(
            id: -1,
            sourcePlayerId: _lastToDieDeathFocus.SourcePlayerId,
            classId: _lastToDieDeathFocus.ClassId,
            team: _lastToDieDeathFocus.Team,
            animationKind: _lastToDieDeathFocus.AnimationKind,
            x: _lastToDieDeathFocus.WorldPosition.X,
            y: _lastToDieDeathFocus.WorldPosition.Y,
            width: _lastToDieDeathFocus.Width,
            height: _lastToDieDeathFocus.Height,
            facingLeft: _lastToDieDeathFocus.FacingLeft,
            ticksRemaining: syntheticTicksRemaining,
            cameraPosition: cameraPosition);
    }

    private void DrawLastToDieFailureCorpse(int viewportWidth, int viewportHeight, float alpha)
    {
        if (_lastToDieDeathFocus is null || alpha <= 0f)
        {
            return;
        }

        if (_lastToDieFailureCorpseTargetHasVisual && _lastToDieFailureCorpseTarget is not null)
        {
            var corpsePosition = new Vector2(viewportWidth / 2f, viewportHeight * 0.54f);
            _spriteBatch.Draw(
                _lastToDieFailureCorpseTarget,
                corpsePosition,
                null,
                Color.White * alpha,
                0f,
                corpsePosition,
                LastToDieFailureCorpseScale,
                SpriteEffects.None,
                0f);
            return;
        }

        var corpsePositionFallback = new Vector2(viewportWidth / 2f, viewportHeight * 0.54f);
        var fallbackRectangle = new Rectangle(
            (int)MathF.Round(corpsePositionFallback.X - (_lastToDieDeathFocus.Width * LastToDieFailureCorpseScale * 0.5f)),
            (int)MathF.Round(corpsePositionFallback.Y - (_lastToDieDeathFocus.Height * LastToDieFailureCorpseScale * 0.5f)),
            (int)MathF.Round(_lastToDieDeathFocus.Width * LastToDieFailureCorpseScale),
            (int)MathF.Round(_lastToDieDeathFocus.Height * LastToDieFailureCorpseScale));
        var fallbackColor = _lastToDieDeathFocus.Team == PlayerTeam.Blue
            ? new Color(24, 45, 80)
            : new Color(90, 30, 30);
        _spriteBatch.Draw(_pixel, fallbackRectangle, fallbackColor * alpha);
    }

    private PlayerClass GetLastToDieLocalDeathFocusClassId()
    {
        return _lastToDieRun?.SurvivorKind == LastToDieSurvivorKind.Demoknight
            ? PlayerClass.Demoman
            : PlayerClass.Soldier;
    }

    private static DeadBodyAnimationKind GetLastToDieLocalDeathFocusAnimationKind(PlayerClass classId)
    {
        return classId == PlayerClass.Soldier
            ? DeadBodyAnimationKind.Rifle
            : DeadBodyAnimationKind.Default;
    }

    private bool DrawLastToDieDeathFocusOverlay(int viewportWidth, int viewportHeight)
    {
        if (!IsLastToDieDeathFocusPresentationActive() || _lastToDieDeathFocusTarget is null)
        {
            return false;
        }

        var progress = GetLastToDieDeathFocusProgress(_lastToDieDeathFocus!);
        var zoom = GetLastToDieDeathFocusZoom(_lastToDieDeathFocus!);
        var center = new Vector2(viewportWidth / 2f, viewportHeight / 2f);
        var origin = new Vector2(_lastToDieDeathFocusTarget.Width / 2f, _lastToDieDeathFocusTarget.Height / 2f);
        _spriteBatch.Draw(_lastToDieDeathFocusTarget, center, null, Color.White, 0f, origin, zoom, SpriteEffects.None, 0f);

        var baseDimAlpha = MathHelper.Lerp(0.04f, LastToDieDeathFocusBaseDimMaxAlpha, progress);
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * baseDimAlpha);

        var edgeDimAlpha = MathHelper.Lerp(0.18f, LastToDieDeathFocusEdgeDimMaxAlpha, progress);
        var edgeSize = (int)MathF.Round(MathHelper.Lerp(72f, 180f, progress));
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewportWidth, edgeSize), Color.Black * edgeDimAlpha);
        _spriteBatch.Draw(_pixel, new Rectangle(0, viewportHeight - edgeSize, viewportWidth, edgeSize), Color.Black * edgeDimAlpha);
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, edgeSize, viewportHeight), Color.Black * edgeDimAlpha);
        _spriteBatch.Draw(_pixel, new Rectangle(viewportWidth - edgeSize, 0, edgeSize, viewportHeight), Color.Black * edgeDimAlpha);
        return true;
    }
}
