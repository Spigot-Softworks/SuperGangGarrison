#nullable enable

using OpenGarrison.Core;
using Microsoft.Xna.Framework;

namespace OpenGarrison.Client;

public partial class Game1
{
    private Rectangle GetLocalPlayerRectangle(Vector2 cameraPosition)
    {
        var player = _world.LocalPlayer;
        return GetPlayerScreenBounds(player, GetRenderPosition(player, allowInterpolation: false), cameraPosition);
    }

    private void DrawGameplayWorld(
        Vector2 cameraPosition,
        Rectangle worldRectangle,
        Rectangle playerRectangle,
        Rectangle centerLine,
        Rectangle centerColumn,
        Rectangle worldTopBorder,
        Rectangle worldBottomBorder,
        Rectangle worldLeftBorder,
        Rectangle worldRightBorder,
        Rectangle spawnRectangle)
    {
        var hasLevelBackground = DrawLevelBackground(worldRectangle);
        DrawFallbackLevelSolids(cameraPosition, hasLevelBackground);
        DrawGameplayEffectsAndProjectiles(cameraPosition);
        DrawGameplayStructures(cameraPosition);
        DrawGameplayMapMarkers(cameraPosition, hasLevelBackground, centerLine, centerColumn, worldTopBorder, worldBottomBorder, worldLeftBorder, worldRightBorder, spawnRectangle);
        DrawGameplayRemains(cameraPosition);
        DrawGameplayPlayers(cameraPosition, playerRectangle);
        DrawBackstabVisuals(cameraPosition);
    }

    private void DrawFallbackLevelSolids(Vector2 cameraPosition, bool hasLevelBackground)
    {
        if (hasLevelBackground)
        {
            return;
        }

        foreach (var solid in _world.Level.Solids)
        {
            var solidRectangle = new Rectangle(
                (int)(solid.X - cameraPosition.X),
                (int)(solid.Y - cameraPosition.Y),
                (int)solid.Width,
                (int)solid.Height);

            _spriteBatch.Draw(_pixel, solidRectangle, new Color(46, 70, 56));
        }
    }

    private void DrawGameplayStructures(Vector2 cameraPosition)
    {
        foreach (var sentry in _world.Sentries)
        {
            if (!TryDrawSentry(sentry, cameraPosition))
            {
                var sentryRectangle = new Rectangle(
                    (int)(sentry.X - SentryEntity.Width / 2f - cameraPosition.X),
                    (int)(sentry.Y - SentryEntity.Height / 2f - cameraPosition.Y),
                    (int)SentryEntity.Width,
                    (int)SentryEntity.Height);
                var sentryColor = sentry.Team == PlayerTeam.Blue
                    ? new Color(100, 160, 235)
                    : new Color(220, 110, 90);
                if (!sentry.IsBuilt)
                {
                    sentryColor *= 0.75f;
                }

                _spriteBatch.Draw(_pixel, sentryRectangle, sentryColor);
            }

            DrawSentryHealthBar(sentry, cameraPosition);
            DrawSentryShotTrace(sentry, cameraPosition);
        }

        foreach (var gib in _world.SentryGibs)
        {
            if (!TryDrawSentryGib(gib, cameraPosition))
            {
                var gibRectangle = new Rectangle(
                    (int)(gib.X - 6f - cameraPosition.X),
                    (int)(gib.Y - 6f - cameraPosition.Y),
                    12,
                    12);
                _spriteBatch.Draw(_pixel, gibRectangle, new Color(160, 170, 175));
            }
        }
    }

    private void DrawGameplayMapMarkers(
        Vector2 cameraPosition,
        bool hasLevelBackground,
        Rectangle centerLine,
        Rectangle centerColumn,
        Rectangle worldTopBorder,
        Rectangle worldBottomBorder,
        Rectangle worldLeftBorder,
        Rectangle worldRightBorder,
        Rectangle spawnRectangle)
    {
        if (!hasLevelBackground)
        {
            _spriteBatch.Draw(_pixel, worldTopBorder, new Color(95, 120, 150));
            _spriteBatch.Draw(_pixel, worldBottomBorder, new Color(95, 120, 150));
            _spriteBatch.Draw(_pixel, worldLeftBorder, new Color(95, 120, 150));
            _spriteBatch.Draw(_pixel, worldRightBorder, new Color(95, 120, 150));
            _spriteBatch.Draw(_pixel, centerLine, new Color(70, 80, 100));
            _spriteBatch.Draw(_pixel, centerColumn, new Color(70, 80, 100));
        }

        foreach (var intelBase in _world.Level.IntelBases)
        {
            if (TryDrawSprite("IntelligenceBaseS", 0, intelBase.X, intelBase.Y, cameraPosition, Color.White))
            {
                continue;
            }

            var markerRectangle = new Rectangle(
                (int)(intelBase.X - 14f - cameraPosition.X),
                (int)(intelBase.Y - 14f - cameraPosition.Y),
                28,
                28);
            var markerColor = intelBase.Team == PlayerTeam.Blue
                ? new Color(80, 150, 240)
                : new Color(210, 90, 90);
            _spriteBatch.Draw(_pixel, markerRectangle, markerColor);
        }

        if (_world.MatchRules.Mode == GameModeKind.CaptureTheFlag)
        {
            DrawIntel(_world.RedIntel, cameraPosition);
            DrawIntel(_world.BlueIntel, cameraPosition);
        }
        else if (_world.MatchRules.Mode == GameModeKind.Arena)
        {
            DrawArenaControlPoint(cameraPosition);
        }
        else if (_world.MatchRules.Mode == GameModeKind.ControlPoint)
        {
            DrawControlPoints(cameraPosition);
        }
        else if (_world.MatchRules.Mode == GameModeKind.Generator)
        {
            DrawGenerators(cameraPosition);
        }

        if (!hasLevelBackground)
        {
            _spriteBatch.Draw(_pixel, spawnRectangle, new Color(110, 200, 130));
        }
    }

    private void DrawGameplayRemains(Vector2 cameraPosition)
    {
        SyncRetainedDeadBodies();
        DrawRetainedDeadBodies(cameraPosition);

        foreach (var playerGib in _world.PlayerGibs)
        {
            if (_gibLevel == 0 || (_gibLevel == 1) || (_gibLevel == 2 && (playerGib.FrameIndex % 2 != 0)))
            {
                continue;
            }

            DrawPlayerGib(playerGib, cameraPosition);
        }

        foreach (var bloodDrop in _world.BloodDrops)
        {
            if (_gibLevel == 0)
            {
                continue;
            }

            DrawBloodDrop(bloodDrop, cameraPosition);
        }

        foreach (var deadBody in _world.DeadBodies)
        {
            DrawDeadBody(deadBody, cameraPosition);
        }
    }

    private void DrawGameplayPlayers(Vector2 cameraPosition, Rectangle playerRectangle)
    {
        foreach (var renderPlayer in EnumerateRenderablePlayers())
        {
            if (ReferenceEquals(renderPlayer, _world.LocalPlayer))
            {
                DrawLocalPlayer(cameraPosition, playerRectangle);
                continue;
            }

            var aliveColor = renderPlayer.Team == PlayerTeam.Blue
                ? new Color(80, 150, 240)
                : new Color(210, 90, 90);
            var deadColor = renderPlayer.Team == PlayerTeam.Blue
                ? new Color(24, 45, 80)
                : new Color(80, 24, 24);
            if (ReferenceEquals(renderPlayer, _world.FriendlyDummy))
            {
                aliveColor = new Color(240, 190, 100);
                deadColor = new Color(70, 50, 24);
            }

            DrawPlayer(renderPlayer, cameraPosition, aliveColor, deadColor);
        }
    }

    private void DrawLocalPlayer(Vector2 cameraPosition, Rectangle playerRectangle)
    {
        if (!_world.LocalPlayer.IsAlive)
        {
            return;
        }

        var visibilityAlpha = GetPlayerVisibilityAlpha(_world.LocalPlayer);
        var playerFallbackColor = _world.LocalPlayer.IsCarryingIntel
            ? new Color(255, 180, 80)
            : Color.OrangeRed;
        var playerSpriteTint = GetPlayerColor(_world.LocalPlayer, Color.White);
        var bodySelection = GetPlayerBodySpriteSelection(_world.LocalPlayer);
        if (!TryDrawPlayerSprite(_world.LocalPlayer, cameraPosition, playerSpriteTint, bodySelection))
        {
            _spriteBatch.Draw(_pixel, playerRectangle, playerFallbackColor * visibilityAlpha);
        }

        if (!GetPlayerIsHeavyEating(_world.LocalPlayer) && !_world.LocalPlayer.IsTaunting && !_world.IsPlayerHumiliated(_world.LocalPlayer))
        {
            TryDrawWeaponSprite(_world.LocalPlayer, cameraPosition, playerSpriteTint, visibilityAlpha, bodySelection);
        }

        DrawAfterburnOverlay(_world.LocalPlayer, GetRenderPosition(_world.LocalPlayer, allowInterpolation: false), cameraPosition, visibilityAlpha);
        DrawChatBubble(_world.LocalPlayer, cameraPosition);
    }

}
