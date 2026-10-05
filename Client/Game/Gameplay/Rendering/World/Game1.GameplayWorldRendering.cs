#nullable enable

using OpenGarrison.Core;
using Microsoft.Xna.Framework;
using System;
using System.Diagnostics;

namespace OpenGarrison.Client;

public partial class Game1
{
    private readonly System.Collections.Generic.List<PlayerEntity> _worldRenderPlayers = new();
    private readonly System.Collections.Generic.HashSet<int> _worldRenderUberedPlayerIds = new();
    private readonly System.Collections.Generic.HashSet<int> _worldRenderMedicBeamPlayerIds = new();
    private readonly System.Collections.Generic.HashSet<int> _worldRenderMedicBeamMedicIds = new();
    private readonly System.Collections.Generic.HashSet<int> _worldRenderMedicBeamTargetIds = new();
    private readonly System.Collections.Generic.HashSet<int> _worldRenderSkipBelowUberPlayerIds = new();
    private bool _worldRenderLocalPlayerInActiveMedicBeam;

    public Rectangle GetLocalPlayerRectangle(Vector2 cameraPosition)
    {
        var player = _world.LocalPlayer;
        return GetPlayerScreenBounds(player, GetRenderPosition(player), cameraPosition);
    }

    public void DrawGameplayWorld(
        Vector2 cameraPosition,
        int viewportWidth,
        int viewportHeight,
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
        DrawCustomMapBackdrop(viewportWidth, viewportHeight);
        DrawCustomMapParallaxBackgrounds(cameraPosition, viewportWidth, viewportHeight);
        for (var parallaxLayer = 0; parallaxLayer <= 6; parallaxLayer += 1)
        {
            DrawCustomMapGameplaySprites(cameraPosition, (CustomMapSpriteLayerKind)(parallaxLayer + 1));
            DrawSpritesheets(cameraPosition, (CustomMapSpriteLayerKind)(parallaxLayer + 1));
        }

        var hasLevelBackground = DrawLevelBackground(cameraPosition);
        DrawCustomMapGameplaySprites(cameraPosition, CustomMapSpriteLayerKind.Bg);
        DrawSpritesheets(cameraPosition, CustomMapSpriteLayerKind.Bg);
        DrawFallbackLevelSolids(cameraPosition, hasLevelBackground, viewportWidth, viewportHeight);
        DrawMovingPlatforms(cameraPosition);
        // Settled blood sits on the map layer; gameplay FX / characters draw above it.
        if (AreBloodVisualsEnabled && _gameplayManager.RuntimeSettings.BloodRenderMode == 0)
        {
            _gameplayManager.GoreEffects.DrawBloodSquibPools(cameraPosition);
        }

        DrawGameplayEffectsAndProjectiles(cameraPosition, viewportWidth, viewportHeight);
        DrawGameplayStructures(cameraPosition);
        DrawDispenserBeams(cameraPosition);
        if (!_gameplayHudMinimal)
        {
            DrawDamageableZoneHealthBars(cameraPosition);
        }
        DrawGameplayMapMarkers(cameraPosition, hasLevelBackground, centerLine, centerColumn, worldTopBorder, worldBottomBorder, worldLeftBorder, worldRightBorder, spawnRectangle);
        DrawGameplayRemains(cameraPosition, skippedDeadBodySourcePlayerId);
        PrepareGameplayPlayerLayers();
        var medicBeamTargetIds = _worldRenderMedicBeamTargetIds;
        var medicBeamMedicIds = _worldRenderMedicBeamMedicIds;
        var localPlayerInActiveMedicBeam = _worldRenderLocalPlayerInActiveMedicBeam;
        var uberedPlayerIds = _worldRenderUberedPlayerIds;
        var skipBelowUber = _worldRenderSkipBelowUberPlayerIds;

        if (localPlayerInActiveMedicBeam)
        {
            DrawGameplayPlayers(cameraPosition, skipPlayerIds: skipBelowUber);
            DrawGameplayPlayers(cameraPosition, skipPlayerIds: uberedPlayerIds, onlyPlayerIds: medicBeamMedicIds);
            DrawMedicBeams(cameraPosition);
            DrawGameplayPlayers(cameraPosition, skipPlayerIds: uberedPlayerIds, onlyPlayerIds: medicBeamTargetIds);
            DrawGameplayPlayers(cameraPosition, onlyPlayerIds: uberedPlayerIds);
            DrawWithLocalPlayerSubpixelOffset(() => DrawLocalPlayer(cameraPosition, playerRectangle));
        }
        else
        {
            DrawMedicBeams(cameraPosition);
            DrawGameplayPlayers(cameraPosition, skipPlayerIds: skipBelowUber);
            DrawGameplayPlayers(cameraPosition, skipPlayerIds: uberedPlayerIds, onlyPlayerIds: medicBeamMedicIds);
            DrawGameplayPlayers(cameraPosition, skipPlayerIds: uberedPlayerIds, onlyPlayerIds: medicBeamTargetIds);
            DrawGameplayPlayers(cameraPosition, onlyPlayerIds: uberedPlayerIds);
            DrawWithLocalPlayerSubpixelOffset(() => DrawLocalPlayer(cameraPosition, playerRectangle));
        }
        DrawFrozenSpyVisuals(cameraPosition);
        DrawBackstabVisuals(cameraPosition);
        DrawSpySuperjumpVisuals(cameraPosition);
        DrawSniperBowAimArc(cameraPosition);
        DrawStrongDrinkAimArc(cameraPosition);
        DrawSniperAimIndicators(cameraPosition);
        DrawCustomMapGameplaySprites(cameraPosition, CustomMapSpriteLayerKind.Fg);
        DrawSpritesheets(cameraPosition, CustomMapSpriteLayerKind.Fg);
        DrawForegroundSprites(cameraPosition, ForegroundSpriteLayerKind.Bg);
        DrawCustomMapForegroundAndVoid(cameraPosition, worldRectangle, viewportWidth, viewportHeight);
        DrawForegroundSprites(cameraPosition, ForegroundSpriteLayerKind.Fg);
        DrawMapWeather(cameraPosition, viewportWidth, viewportHeight);
        DrawGameplayLightingOverlay(cameraPosition);
        DrawRocketCollisionDebug(cameraPosition);
        DrawProjectileSpawnBlockedDebug(cameraPosition);
        DrawHitboxDebugOverlay(cameraPosition);
        RecordBrowserWorldDrawDuration(browserWorldDrawStartTimestamp);
        RecordFirstGameplayWorldDrawCompleted();
    }

    private void DrawRocketCollisionDebug(Vector2 cameraPosition)
    {
        if (!_debugRocketCollisionsEnabled || !_world.Projectiles.DebugHasLastRocketCollision)
        {
            return;
        }

        var x = _world.Projectiles.DebugLastRocketCollisionX;
        var y = _world.Projectiles.DebugLastRocketCollisionY;
        var markerColor = new Color(255, 255, 255, 240);
        DrawWorldLine(x - 5f, y, x + 5f, y, cameraPosition, markerColor, 2f);
        DrawWorldLine(x, y - 5f, x, y + 5f, cameraPosition, markerColor, 2f);

        var label = $"Rocket hit: {_world.Projectiles.DebugLastRocketCollisionObjectName}";
        var textPosition = new Vector2(x + 8f - cameraPosition.X, y - 18f - cameraPosition.Y);
        _spriteBatch.DrawString(_consoleFont, label, textPosition, new Color(255, 255, 255, 245));
        var reasonLabel = $"Reason: {_world.Projectiles.DebugLastRocketCollisionReason}";
        _spriteBatch.DrawString(_consoleFont, reasonLabel, textPosition + new Vector2(0f, 12f), new Color(220, 255, 220, 245));
    }

    private void DrawProjectileSpawnBlockedDebug(Vector2 cameraPosition)
    {
        if (!_debugRocketCollisionsEnabled || !_world.Projectiles.DebugHasProjectileSpawnBlocked)
        {
            return;
        }

        var blockingRectangle = new Rectangle(
            (int)(_world.Projectiles.DebugProjectileSpawnBlockedX - cameraPosition.X),
            (int)(_world.Projectiles.DebugProjectileSpawnBlockedY - cameraPosition.Y),
            (int)_world.Projectiles.DebugProjectileSpawnBlockedWidth,
            (int)_world.Projectiles.DebugProjectileSpawnBlockedHeight);

        var redColor = new Color(255, 0, 0, 100);
        _spriteBatch.Draw(_pixel, blockingRectangle, redColor);

        var label = $"Spawn blocked: {_world.Projectiles.DebugProjectileSpawnBlockedObjectName}";
        var textPosition = new Vector2(
            _world.Projectiles.DebugProjectileSpawnBlockedX + 4f - cameraPosition.X,
            _world.Projectiles.DebugProjectileSpawnBlockedY + 4f - cameraPosition.Y);
        _spriteBatch.DrawString(_consoleFont, label, textPosition, new Color(255, 100, 100, 245));
    }

    private void DrawFallbackLevelSolids(
        Vector2 cameraPosition,
        bool hasLevelBackground,
        int viewportWidth,
        int viewportHeight)
    {
        if (hasLevelBackground)
        {
            return;
        }

        var visibleLeft = cameraPosition.X - 1f;
        var visibleTop = cameraPosition.Y - 1f;
        var visibleRight = cameraPosition.X + viewportWidth + 1f;
        var visibleBottom = cameraPosition.Y + viewportHeight + 1f;
        foreach (var solid in _world.Level.Solids)
        {
            if (solid.Right <= visibleLeft
                || solid.Left >= visibleRight
                || solid.Bottom <= visibleTop
                || solid.Top >= visibleBottom)
            {
                continue;
            }

            DrawScreenPixelRectangle(
                GetWorldScreenPosition(solid.X, solid.Y, cameraPosition),
                solid.Width,
                solid.Height,
                new Color(46, 70, 56));
        }
    }

    private void DrawGameplayStructures(Vector2 cameraPosition)
    {
        foreach (var sentry in _world.Sentries)
        {
            if (!TryDrawSentry(sentry, cameraPosition))
            {
                var sentryColor = sentry.Team == PlayerTeam.Blue
                    ? new Color(100, 160, 235)
                    : new Color(220, 110, 90);
                if (!sentry.IsBuilt)
                {
                    sentryColor *= 0.75f;
                }

                DrawScreenPixelRectangle(
                    GetWorldScreenPosition(sentry.X - SentryEntity.Width / 2f, sentry.Y - SentryEntity.Height / 2f, cameraPosition),
                    SentryEntity.Width,
                    SentryEntity.Height,
                    sentryColor);
            }

            if (!_gameplayHudMinimal)
            {
                DrawSentryHealthBar(sentry, cameraPosition);
            }
            DrawSentryShotTrace(sentry, cameraPosition);
        }

        foreach (var gib in _world.SentryGibs)
        {
            if (!TryDrawSentryGib(gib, cameraPosition))
            {
                DrawScreenPixelRectangle(
                    GetWorldScreenPosition(gib.X - 6f, gib.Y - 6f, cameraPosition),
                    12f,
                    12f,
                    new Color(160, 170, 175) * gib.Alpha);
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
                var padColor = jumpPad.IsNeutral
                    ? new Color(175, 175, 175)
                    : jumpPad.Team == PlayerTeam.Blue
                        ? new Color(100, 160, 235)
                        : new Color(220, 110, 90);
                DrawScreenPixelRectangle(
                    GetWorldScreenPosition(jumpPad.X - JumpPadEntity.Width / 2f, jumpPad.Y - JumpPadEntity.Height / 2f, cameraPosition),
                    JumpPadEntity.Width,
                    JumpPadEntity.Height,
                    padColor);
            }
        }

        foreach (var gib in _world.JumpPadGibs)
        {
            if (!TryDrawJumpPadGib(gib, cameraPosition))
            {
                DrawScreenPixelRectangle(
                    GetWorldScreenPosition(gib.X - 6f, gib.Y - 6f, cameraPosition),
                    12f,
                    12f,
                    new Color(160, 170, 175));
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
            DrawIntel(_world.ObjectiveRules.RedIntel, cameraPosition);
            DrawIntel(_world.ObjectiveRules.BlueIntel, cameraPosition);
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

        _remainsDrawOrder.Clear();

        for (var index = 0; index < _retainedDeadBodies.Count; index += 1)
        {
            var deadBody = _retainedDeadBodies[index];
            if (skippedDeadBodySourcePlayerId.HasValue
                && deadBody.SourcePlayerId == skippedDeadBodySourcePlayerId.Value)
            {
                continue;
            }

            NoteRemainsSortCeiling(deadBody.Id);
            _remainsDrawOrder.Add(new RemainsDrawEntry(deadBody.Id, RemainsDrawKind.RetainedDeadBody, index));
        }

        foreach (var entry in _immediateNetworkDeadBodies)
        {
            var deadBody = entry.Value;
            if (skippedDeadBodySourcePlayerId.HasValue
                && deadBody.SourcePlayerId == skippedDeadBodySourcePlayerId.Value)
            {
                continue;
            }

            NoteRemainsSortCeiling(deadBody.RemainsSortKey);
            _remainsDrawOrder.Add(new RemainsDrawEntry(
                deadBody.RemainsSortKey,
                RemainsDrawKind.ImmediateNetworkDeadBody,
                deadBody.SourcePlayerId));
        }

        var playerGibIndex = 0;
        foreach (var playerGib in _world.PlayerGibs)
        {
            var index = playerGibIndex;
            playerGibIndex += 1;
            if (!ShouldDrawPlayerGib(playerGib))
            {
                continue;
            }

            NoteRemainsSortCeiling(playerGib.Id);
            _remainsDrawOrder.Add(new RemainsDrawEntry(playerGib.Id, RemainsDrawKind.PlayerGib, index));
        }

        var worldDeadBodyIndex = 0;
        foreach (var deadBody in _world.DeadBodies)
        {
            var index = worldDeadBodyIndex;
            worldDeadBodyIndex += 1;
            if (skippedDeadBodySourcePlayerId.HasValue
                && deadBody.SourcePlayerId == skippedDeadBodySourcePlayerId.Value)
            {
                continue;
            }

            NoteRemainsSortCeiling(deadBody.Id);
            _remainsDrawOrder.Add(new RemainsDrawEntry(deadBody.Id, RemainsDrawKind.WorldDeadBody, index));
        }

        _remainsDrawOrder.Sort(static (left, right) =>
        {
            var keyComparison = left.SortKey.CompareTo(right.SortKey);
            return keyComparison != 0
                ? keyComparison
                : ((int)left.Kind).CompareTo((int)right.Kind);
        });

        for (var orderIndex = 0; orderIndex < _remainsDrawOrder.Count; orderIndex += 1)
        {
            var entry = _remainsDrawOrder[orderIndex];
            switch (entry.Kind)
            {
                case RemainsDrawKind.RetainedDeadBody:
                {
                    var deadBody = _retainedDeadBodies[entry.Index];
                    DrawDeadBodyVisual(
                        deadBody.Id,
                        deadBody.SourcePlayerId,
                        deadBody.ClassId,
                        deadBody.Team,
                        deadBody.AnimationKind,
                        deadBody.X,
                        deadBody.Y,
                        deadBody.Width,
                        deadBody.Height,
                        deadBody.FacingLeft,
                        deadBody.TicksRemaining,
                        cameraPosition,
                        deadBody.GameplayClassId);
                    break;
                }
                case RemainsDrawKind.ImmediateNetworkDeadBody:
                {
                    if (!_immediateNetworkDeadBodies.TryGetValue(entry.Index, out var deadBody))
                    {
                        break;
                    }

                    DrawDeadBodyVisual(
                        id: -Math.Abs(deadBody.SourcePlayerId),
                        deadBody.SourcePlayerId,
                        deadBody.ClassId,
                        deadBody.Team,
                        deadBody.AnimationKind,
                        deadBody.X,
                        deadBody.Y,
                        deadBody.Width,
                        deadBody.Height,
                        deadBody.FacingLeft,
                        deadBody.TicksRemaining,
                        cameraPosition,
                        deadBody.GameplayClassId);
                    break;
                }
                case RemainsDrawKind.PlayerGib:
                    DrawPlayerGib(_world.PlayerGibs[entry.Index], cameraPosition);
                    break;
                case RemainsDrawKind.WorldDeadBody:
                    DrawDeadBody(_world.DeadBodies[entry.Index], cameraPosition);
                    break;
            }
        }

        foreach (var bloodDrop in _world.BloodDrops)
        {
            if (!AreBloodVisualsEnabled)
            {
                continue;
            }

            DrawBloodDrop(bloodDrop, cameraPosition);
        }
    }

    private void NoteRemainsSortCeiling(int sortKey)
    {
        if (sortKey > _remainsSortCeiling)
        {
            _remainsSortCeiling = sortKey;
        }
    }

    private int AllocateRemainsSortKey()
    {
        _remainsSortCeiling += 1;
        return _remainsSortCeiling;
    }

    private void PrepareGameplayPlayerLayers()
    {
        _worldRenderPlayers.Clear();
        _worldRenderUberedPlayerIds.Clear();
        _worldRenderMedicBeamPlayerIds.Clear();
        _worldRenderMedicBeamMedicIds.Clear();
        _worldRenderMedicBeamTargetIds.Clear();
        _worldRenderSkipBelowUberPlayerIds.Clear();

        var localId = _world.LocalPlayer.Id;
        _worldRenderLocalPlayerInActiveMedicBeam = _world.LocalPlayer.IsMedicHealing
            && _world.LocalPlayer.MedicHealTargetId.HasValue;

        foreach (var player in EnumerateRenderablePlayers())
        {
            _worldRenderPlayers.Add(player);
            if (player.Id != localId && (player.IsUbered || player.IsKritzCritBoosted))
            {
                _worldRenderUberedPlayerIds.Add(player.Id);
            }

            if (!player.IsMedicHealing || !player.MedicHealTargetId.HasValue)
            {
                continue;
            }

            var targetId = player.MedicHealTargetId.Value;
            _worldRenderMedicBeamPlayerIds.Add(player.Id);
            _worldRenderMedicBeamMedicIds.Add(player.Id);
            if (targetId == localId)
            {
                _worldRenderLocalPlayerInActiveMedicBeam = true;
            }

            var target = FindPlayerById(targetId);
            if (target is not null && target.IsAlive)
            {
                _worldRenderMedicBeamPlayerIds.Add(target.Id);
                _worldRenderMedicBeamTargetIds.Add(target.Id);
            }
        }

        _worldRenderSkipBelowUberPlayerIds.UnionWith(_worldRenderMedicBeamPlayerIds);
        _worldRenderSkipBelowUberPlayerIds.UnionWith(_worldRenderUberedPlayerIds);
    }

    private void DrawGameplayPlayers(
        Vector2 cameraPosition,
        System.Collections.Generic.HashSet<int>? skipPlayerIds = null,
        System.Collections.Generic.HashSet<int>? onlyPlayerIds = null)
    {
        foreach (var renderPlayer in _worldRenderPlayers)
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
        if (!_world.LocalPlayer.IsAlive || !HasFreshPlayerRenderHistory(_world.LocalPlayer))
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

        var shouldDrawWeapon = !GetPlayerIsHeavyEating(_world.LocalPlayer)
            && !_world.LocalPlayer.IsTaunting
            && !_world.LocalPlayer.IsCivviePogoActive
            && !_world.IsPlayerHumiliated(_world.LocalPlayer);
        var drawWeaponBehindBody = shouldDrawWeapon && GetWeaponRenderDefinition(_world.LocalPlayer).DrawBehindBody;
        if (drawWeaponBehindBody)
        {
            TryDrawWeaponSprite(_world.LocalPlayer, cameraPosition, playerSpriteTint, visibilityAlpha, bodySelection);
        }

        if (!TryDrawPlayerSprite(_world.LocalPlayer, cameraPosition, playerSpriteTint, bodySelection))
        {
            _spriteBatch.Draw(_pixel, playerRectangle, playerFallbackColor * visibilityAlpha);
        }

        DrawExperimentalStickyGibBloodOverlay(_world.LocalPlayer, cameraPosition, visibilityAlpha);

        if (shouldDrawWeapon && !drawWeaponBehindBody)
        {
            TryDrawWeaponSprite(_world.LocalPlayer, cameraPosition, playerSpriteTint, visibilityAlpha, bodySelection);
        }

        DrawHealingCrossParticles(_world.LocalPlayer, renderPosition, cameraPosition, visibilityAlpha);
        _gameplayWeaponRenderController.DrawCivvieUmbrellaShieldBlockVisuals(_world.LocalPlayer, cameraPosition, visibilityAlpha, bodySelection);
        DrawExperimentalCryoOverlays(_world.LocalPlayer, renderPosition, cameraPosition, visibilityAlpha, bodySelection);
        DrawAfterburnOverlay(_world.LocalPlayer, renderPosition, cameraPosition, visibilityAlpha);
        if (_gameplayHudMinimal)
        {
            TryDrawAdditionalHealthBar(_world.LocalPlayer, cameraPosition, visibilityAlpha);
        }
        else
        {
            DrawChatBubble(_world.LocalPlayer, cameraPosition);
            DrawWriteBubble(_world.LocalPlayer, cameraPosition);
            DrawOverheadChatMessage(_world.LocalPlayer, cameraPosition);
            DrawEvasionMissPopup(_world.LocalPlayer, cameraPosition);
            DrawHeavyDashDodgePopup(_world.LocalPlayer, cameraPosition);
            TryDrawAdditionalHealthBar(_world.LocalPlayer, cameraPosition, visibilityAlpha);
            TryDrawCivvieUmbrellaShieldBar(_world.LocalPlayer, cameraPosition, visibilityAlpha);
        }
    }

}
