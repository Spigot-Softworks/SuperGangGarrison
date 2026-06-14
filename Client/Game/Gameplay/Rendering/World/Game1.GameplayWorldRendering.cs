#nullable enable

using OpenGarrison.Core;
using Microsoft.Xna.Framework;
using System.Diagnostics;

namespace OpenGarrison.Client;

public partial class Game1
{
    private Rectangle GetLocalPlayerRectangle(Vector2 cameraPosition)
    {
        var player = _world.LocalPlayer;
        return GetPlayerScreenBounds(player, GetRenderPosition(player), cameraPosition);
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
        Rectangle spawnRectangle,
        int? skippedDeadBodySourcePlayerId = null)
    {
        var browserWorldDrawStartTimestamp = ShouldMeasureClientPerformanceDurations() ? Stopwatch.GetTimestamp() : 0L;
        DrawCustomMapBackdrop(worldRectangle);
        DrawCustomMapParallaxBackgrounds(cameraPosition);
        for (var parallaxLayer = 0; parallaxLayer <= 6; parallaxLayer += 1)
        {
            DrawCustomMapGameplaySprites(cameraPosition, (CustomMapSpriteLayerKind)(parallaxLayer + 1));
            DrawSpritesheets(cameraPosition, (CustomMapSpriteLayerKind)(parallaxLayer + 1));
        }

        var hasLevelBackground = DrawLevelBackground(worldRectangle);
        DrawCustomMapGameplaySprites(cameraPosition, CustomMapSpriteLayerKind.Bg);
        DrawSpritesheets(cameraPosition, CustomMapSpriteLayerKind.Bg);
        DrawFallbackLevelSolids(cameraPosition, hasLevelBackground);
        DrawMovingPlatforms(cameraPosition);
        DrawGameplayEffectsAndProjectiles(cameraPosition);
        DrawGameplayStructures(cameraPosition);
        DrawDamageableZoneHealthBars(cameraPosition);
        DrawGameplayMapMarkers(cameraPosition, hasLevelBackground, centerLine, centerColumn, worldTopBorder, worldBottomBorder, worldLeftBorder, worldRightBorder, spawnRectangle);
        DrawGameplayRemains(cameraPosition, skippedDeadBodySourcePlayerId);
        var medicBeamPlayerIds = GetActiveMedicBeamPlayerIds();
        var medicBeamTargetIds = GetActiveMedicBeamTargetIds();
        var medicBeamMedicIds = GetActiveMedicBeamMedicIds();
        var localPlayerInActiveMedicBeam = IsLocalPlayerInActiveMedicBeam();
        var uberedPlayerIds = GetUberedPlayerIds();
        var skipBelowUber = new System.Collections.Generic.HashSet<int>(medicBeamPlayerIds);
        skipBelowUber.UnionWith(uberedPlayerIds);

        if (localPlayerInActiveMedicBeam)
        {
            DrawGameplayPlayers(cameraPosition, playerRectangle, skipPlayerIds: skipBelowUber);
            DrawGameplayPlayers(cameraPosition, playerRectangle, skipPlayerIds: uberedPlayerIds, onlyPlayerIds: medicBeamMedicIds);
            DrawMedicBeams(cameraPosition);
            DrawGameplayPlayers(cameraPosition, playerRectangle, skipPlayerIds: uberedPlayerIds, onlyPlayerIds: medicBeamTargetIds);
            DrawGameplayPlayers(cameraPosition, playerRectangle, onlyPlayerIds: uberedPlayerIds);
            DrawLocalPlayer(cameraPosition, playerRectangle);
        }
        else
        {
            DrawMedicBeams(cameraPosition);
            DrawGameplayPlayers(cameraPosition, playerRectangle, skipPlayerIds: skipBelowUber);
            DrawGameplayPlayers(cameraPosition, playerRectangle, skipPlayerIds: uberedPlayerIds, onlyPlayerIds: medicBeamMedicIds);
            DrawGameplayPlayers(cameraPosition, playerRectangle, skipPlayerIds: uberedPlayerIds, onlyPlayerIds: medicBeamTargetIds);
            DrawGameplayPlayers(cameraPosition, playerRectangle, onlyPlayerIds: uberedPlayerIds);
            DrawLocalPlayer(cameraPosition, playerRectangle);
        }
        DrawFrozenSpyVisuals(cameraPosition);
        DrawBackstabVisuals(cameraPosition);
        DrawSpySuperjumpVisuals(cameraPosition);
        DrawSniperAimIndicators(cameraPosition);
        DrawCustomMapGameplaySprites(cameraPosition, CustomMapSpriteLayerKind.Fg);
        DrawSpritesheets(cameraPosition, CustomMapSpriteLayerKind.Fg);
        DrawForegroundSprites(cameraPosition, ForegroundSpriteLayerKind.Bg);
        DrawCustomMapForegroundAndVoid(cameraPosition, worldRectangle);
        DrawForegroundSprites(cameraPosition, ForegroundSpriteLayerKind.Fg);
        DrawRocketCollisionDebug(cameraPosition);
        DrawProjectileSpawnBlockedDebug(cameraPosition);
        RecordBrowserWorldDrawDuration(browserWorldDrawStartTimestamp);
    }

    private void DrawRocketCollisionDebug(Vector2 cameraPosition)
    {
        if (!_debugRocketCollisionsEnabled || !_world.DebugHasLastRocketCollision)
        {
            return;
        }

        var x = _world.DebugLastRocketCollisionX;
        var y = _world.DebugLastRocketCollisionY;
        var markerColor = new Color(255, 255, 255, 240);
        DrawWorldLine(x - 5f, y, x + 5f, y, cameraPosition, markerColor, 2f);
        DrawWorldLine(x, y - 5f, x, y + 5f, cameraPosition, markerColor, 2f);

        var label = $"Rocket hit: {_world.DebugLastRocketCollisionObjectName}";
        var textPosition = new Vector2(x + 8f - cameraPosition.X, y - 18f - cameraPosition.Y);
        _spriteBatch.DrawString(_consoleFont, label, textPosition, new Color(255, 255, 255, 245));
        var reasonLabel = $"Reason: {_world.DebugLastRocketCollisionReason}";
        _spriteBatch.DrawString(_consoleFont, reasonLabel, textPosition + new Vector2(0f, 12f), new Color(220, 255, 220, 245));
    }

    private void DrawProjectileSpawnBlockedDebug(Vector2 cameraPosition)
    {
        if (!_debugRocketCollisionsEnabled || !_world.DebugHasProjectileSpawnBlocked)
        {
            return;
        }

        var blockingRectangle = new Rectangle(
            (int)(_world.DebugProjectileSpawnBlockedX - cameraPosition.X),
            (int)(_world.DebugProjectileSpawnBlockedY - cameraPosition.Y),
            (int)_world.DebugProjectileSpawnBlockedWidth,
            (int)_world.DebugProjectileSpawnBlockedHeight);

        var redColor = new Color(255, 0, 0, 100);
        _spriteBatch.Draw(_pixel, blockingRectangle, redColor);

        var label = $"Spawn blocked: {_world.DebugProjectileSpawnBlockedObjectName}";
        var textPosition = new Vector2(
            _world.DebugProjectileSpawnBlockedX + 4f - cameraPosition.X,
            _world.DebugProjectileSpawnBlockedY + 4f - cameraPosition.Y);
        _spriteBatch.DrawString(_consoleFont, label, textPosition, new Color(255, 100, 100, 245));
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

        foreach (var turret in _world.CivilDefenseTurrets)
        {
            DrawCivilDefenseTurret(turret, cameraPosition);
        }

        foreach (var healthPack in _world.HealthPacks)
        {
            DrawHealthPack(healthPack, cameraPosition);
        }

        foreach (var jumpPad in _world.JumpPads)
        {
            if (!TryDrawJumpPad(jumpPad, cameraPosition))
            {
                var padRectangle = new Rectangle(
                    (int)(jumpPad.X - JumpPadEntity.Width / 2f - cameraPosition.X),
                    (int)(jumpPad.Y - JumpPadEntity.Height / 2f - cameraPosition.Y),
                    (int)JumpPadEntity.Width,
                    (int)JumpPadEntity.Height);
                var padColor = jumpPad.Team == PlayerTeam.Blue
                    ? new Color(100, 160, 235)
                    : new Color(220, 110, 90);
                _spriteBatch.Draw(_pixel, padRectangle, padColor);
            }
        }

        foreach (var gib in _world.JumpPadGibs)
        {
            if (!TryDrawJumpPadGib(gib, cameraPosition))
            {
                var gibRectangle = new Rectangle(
                    (int)(gib.X - 6f - cameraPosition.X),
                    (int)(gib.Y - 6f - cameraPosition.Y),
                    12,
                    12);
                _spriteBatch.Draw(_pixel, gibRectangle, new Color(160, 170, 175));
            }
        }

        foreach (var droppedWeapon in _world.DroppedWeapons)
        {
            DrawDroppedWeapon(droppedWeapon, cameraPosition);
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
        else if (_world.MatchRules.Mode == GameModeKind.Generator)
        {
            DrawGenerators(cameraPosition);
        }

        if (ShouldDrawControlPointSpritesOnMap())
        {
            DrawControlPoints(cameraPosition);
        }

        if (!hasLevelBackground)
        {
            _spriteBatch.Draw(_pixel, spawnRectangle, new Color(110, 200, 130));
        }
    }

    private void DrawGameplayRemains(Vector2 cameraPosition, int? skippedDeadBodySourcePlayerId = null)
    {
        SyncRetainedDeadBodies();
        SyncImmediateNetworkDeadBodies();
        DrawRetainedDeadBodies(cameraPosition, skippedDeadBodySourcePlayerId);
        DrawImmediateNetworkDeadBodies(cameraPosition, skippedDeadBodySourcePlayerId);

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
            if (skippedDeadBodySourcePlayerId.HasValue
                && deadBody.SourcePlayerId == skippedDeadBodySourcePlayerId.Value)
            {
                continue;
            }

            DrawDeadBody(deadBody, cameraPosition);
        }
    }

    private System.Collections.Generic.HashSet<int> GetUberedPlayerIds()
    {
        var ids = new System.Collections.Generic.HashSet<int>();
        var localId = _world.LocalPlayer.Id;
        foreach (var player in EnumerateRenderablePlayers())
        {
            if (player.Id != localId && (player.IsUbered || player.IsKritzCritBoosted))
            {
                ids.Add(player.Id);
            }
        }

        return ids;
    }

    private System.Collections.Generic.HashSet<int> GetActiveMedicBeamPlayerIds()
    {
        var ids = new System.Collections.Generic.HashSet<int>();
        foreach (var player in EnumerateRenderablePlayers())
        {
            if (player.IsMedicHealing
                && player.MedicHealTargetId.HasValue)
            {
                ids.Add(player.Id);
                var target = FindPlayerById(player.MedicHealTargetId.Value);
                if (target is not null && target.IsAlive)
                    ids.Add(target.Id);
            }
        }
        return ids;
    }

    private bool HasActiveMedicBeamPlayers()
    {
        if (_world.LocalPlayer.IsMedicHealing && _world.LocalPlayer.MedicHealTargetId.HasValue)
        {
            return true;
        }

        foreach (var player in EnumerateRenderablePlayers())
        {
            if (!ReferenceEquals(player, _world.LocalPlayer)
                && player.IsMedicHealing
                && player.MedicHealTargetId.HasValue)
            {
                return true;
            }
        }

        return false;
    }

    private System.Collections.Generic.HashSet<int> GetActiveMedicBeamMedicIds()
    {
        var ids = new System.Collections.Generic.HashSet<int>();
        foreach (var player in EnumerateRenderablePlayers())
        {
            if (player.IsMedicHealing
                && player.MedicHealTargetId.HasValue)
            {
                ids.Add(player.Id);
            }
        }
        return ids;
    }

    private System.Collections.Generic.HashSet<int> GetActiveMedicBeamTargetIds()
    {
        var ids = new System.Collections.Generic.HashSet<int>();
        foreach (var player in EnumerateRenderablePlayers())
        {
            if (player.IsMedicHealing
                && player.MedicHealTargetId.HasValue)
            {
                var target = FindPlayerById(player.MedicHealTargetId.Value);
                if (target is not null && target.IsAlive)
                    ids.Add(target.Id);
            }
        }
        return ids;
    }

    private bool IsLocalPlayerInActiveMedicBeam()
    {
        var localPlayer = _world.LocalPlayer;
        if (localPlayer.IsMedicHealing && localPlayer.MedicHealTargetId.HasValue)
        {
            return true;
        }

        foreach (var player in EnumerateRenderablePlayers())
        {
            if (!player.IsMedicHealing
                || !player.MedicHealTargetId.HasValue)
            {
                continue;
            }

            if (player.MedicHealTargetId.Value == localPlayer.Id)
            {
                return true;
            }
        }

        return false;
    }

    private void DrawGameplayPlayers(
        Vector2 cameraPosition,
        Rectangle playerRectangle,
        System.Collections.Generic.HashSet<int>? skipPlayerIds = null,
        System.Collections.Generic.HashSet<int>? onlyPlayerIds = null)
    {
        foreach (var renderPlayer in EnumerateRenderablePlayers())
        {
            var playerId = renderPlayer.Id;
            if (skipPlayerIds is not null && skipPlayerIds.Contains(playerId)) continue;
            if (onlyPlayerIds is not null && !onlyPlayerIds.Contains(playerId)) continue;
            if (ReferenceEquals(renderPlayer, _world.LocalPlayer)) continue;

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
        var renderPosition = GetRenderPosition(_world.LocalPlayer);
        DrawExperimentalDemoknightChargeBlur(_world.LocalPlayer, cameraPosition, playerSpriteTint, visibilityAlpha, bodySelection);
        DrawCapturedPointHealingGhosting(_world.LocalPlayer, renderPosition, cameraPosition, visibilityAlpha, bodySelection);
        TryDrawWeaponSpriteBackdrop(_world.LocalPlayer, cameraPosition, playerSpriteTint, visibilityAlpha, bodySelection);
        if (!_world.LocalPlayer.IsTaunting)
        {
            DrawHealingCharacterBodyEffects(_world.LocalPlayer, renderPosition, cameraPosition, visibilityAlpha, bodySelection);
        }

        if (!TryDrawPlayerSprite(_world.LocalPlayer, cameraPosition, playerSpriteTint, bodySelection))
        {
            _spriteBatch.Draw(_pixel, playerRectangle, playerFallbackColor * visibilityAlpha);
        }

        if (_world.LocalPlayer.IsTaunting)
        {
            DrawHealingCharacterBodyEffects(_world.LocalPlayer, renderPosition, cameraPosition, visibilityAlpha, bodySelection);
        }

        DrawExperimentalStickyGibBloodOverlay(_world.LocalPlayer, cameraPosition, visibilityAlpha);

        if (!GetPlayerIsHeavyEating(_world.LocalPlayer)
            && !_world.LocalPlayer.IsTaunting
            && !_world.LocalPlayer.IsCivviePogoActive
            && !_world.IsPlayerHumiliated(_world.LocalPlayer))
        {
            DrawHealingCharacterWeaponEffects(_world.LocalPlayer, renderPosition, cameraPosition, visibilityAlpha, bodySelection);
            TryDrawWeaponSprite(_world.LocalPlayer, cameraPosition, playerSpriteTint, visibilityAlpha, bodySelection);
        }

        _gameplayWeaponRenderController.DrawCivvieUmbrellaShieldBlockVisuals(_world.LocalPlayer, cameraPosition, visibilityAlpha, bodySelection);
        DrawExperimentalCryoOverlays(_world.LocalPlayer, renderPosition, cameraPosition, visibilityAlpha, bodySelection);
        DrawAfterburnOverlay(_world.LocalPlayer, renderPosition, cameraPosition, visibilityAlpha);
        DrawChatBubble(_world.LocalPlayer, cameraPosition);
        DrawWriteBubble(_world.LocalPlayer, cameraPosition);
        DrawOverheadChatMessage(_world.LocalPlayer, cameraPosition);
        DrawEvasionMissPopup(_world.LocalPlayer, cameraPosition);
        DrawHeavyDashDodgePopup(_world.LocalPlayer, cameraPosition);
        TryDrawAdditionalHealthBar(_world.LocalPlayer, cameraPosition, visibilityAlpha);
        TryDrawCivvieUmbrellaShieldBar(_world.LocalPlayer, cameraPosition, visibilityAlpha);
    }

}
