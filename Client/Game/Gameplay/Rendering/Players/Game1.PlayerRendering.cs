#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using OpenGarrison.Client.Plugins;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private readonly record struct PlayerBodySpriteSelection(
        string? SpriteName,
        float AnimationImage,
        float BodyYOffset,
        float EquipmentOffset,
        bool DrawIntelUnderlay,
        bool IsHumiliated);

    private readonly record struct RetainedDeadBodyVisual(
        int Id,
        int SourcePlayerId,
        PlayerClass ClassId,
        PlayerTeam Team,
        DeadBodyAnimationKind AnimationKind,
        float X,
        float Y,
        float Width,
        float Height,
        bool FacingLeft,
        int TicksRemaining);

    private readonly record struct WeaponRenderDefinition(
        string? NormalSpriteName,
        string? RecoilSpriteName,
        string? ReloadSpriteName,
        WeaponAnimationOverlayDefinition RecoilOverlay,
        WeaponAnimationOverlayDefinition ReloadOverlay,
        float XOffset,
        float YOffset,
        float RecoilDurationSeconds,
        float ReloadDurationSeconds,
        float ScopedRecoilDurationSeconds = 0f,
        bool LoopRecoilWhileActive = false);

    private readonly record struct WeaponAnimationOverlayDefinition(
        string? CarrierSpriteName,
        string? OverlaySpriteName,
        float OffsetX = 0f,
        float OffsetY = 0f,
        float RotationDegrees = 0f);

    private enum LeanDirection
    {
        None,
        Left,
        Right,
    }

    private readonly Dictionary<int, RetainedDeadBodyVisual> _trackedDeadBodyVisuals = new();
    private readonly List<RetainedDeadBodyVisual> _retainedDeadBodies = new();
    private readonly List<int> _staleTrackedDeadBodyIds = new();

    private static Rectangle GetPlayerScreenBounds(PlayerEntity player, Vector2 renderPosition, Vector2 cameraPosition)
    {
        player.GetCollisionBoundsAt(renderPosition.X, renderPosition.Y, out var left, out var top, out var right, out var bottom);
        var screenLeft = (int)MathF.Floor(left - cameraPosition.X);
        var screenTop = (int)MathF.Floor(top - cameraPosition.Y);
        var screenRight = (int)MathF.Ceiling(right - cameraPosition.X);
        var screenBottom = (int)MathF.Ceiling(bottom - cameraPosition.Y);
        return new Rectangle(
            screenLeft,
            screenTop,
            Math.Max(1, screenRight - screenLeft),
            Math.Max(1, screenBottom - screenTop));
    }

    private static Vector2 GetRoundedPlayerSpriteOrigin(Vector2 renderPosition)
    {
        return RoundToSourcePixels(renderPosition);
    }

    private void DrawPlayer(PlayerEntity player, Vector2 cameraPosition, Color aliveColor, Color deadColor)
    {
        _gameplayPlayerRenderController.DrawPlayer(player, cameraPosition, aliveColor, deadColor);
    }

    private void TryDrawAdditionalHealthBar(PlayerEntity player, Vector2 cameraPosition, float visibilityAlpha)
    {
        _gameplayPlayerStatusEffectRenderController.TryDrawAdditionalHealthBar(player, cameraPosition, visibilityAlpha);
    }

    private void DrawDeadBody(DeadBodyEntity deadBody, Vector2 cameraPosition)
    {
        _gameplayDeadBodyRenderController.DrawDeadBody(deadBody, cameraPosition);
    }

    private void DrawRetainedDeadBodies(Vector2 cameraPosition, int? skippedDeadBodySourcePlayerId = null)
    {
        _gameplayDeadBodyRenderController.DrawRetainedDeadBodies(cameraPosition, skippedDeadBodySourcePlayerId);
    }

    private void SyncRetainedDeadBodies()
    {
        _gameplayDeadBodyRenderController.SyncRetainedDeadBodies();
    }

    private void ResetRetainedDeadBodies()
    {
        _gameplayDeadBodyRenderController.ResetRetainedDeadBodies();
    }

    private void DrawDeadBodyVisual(
        int id,
        int sourcePlayerId,
        PlayerClass classId,
        PlayerTeam team,
        DeadBodyAnimationKind animationKind,
        float x,
        float y,
        float width,
        float height,
        bool facingLeft,
        int ticksRemaining,
        Vector2 cameraPosition)
    {
        _gameplayDeadBodyRenderController.DrawDeadBodyVisual(id, sourcePlayerId, classId, team, animationKind, x, y, width, height, facingLeft, ticksRemaining, cameraPosition);
    }

    private ClientDeadBodyAnimationKind ResolveClientPluginDeadBodyAnimationKind(int sourcePlayerId, PlayerClass classId, PlayerTeam team, DeadBodyAnimationKind animationKind)
    {
        return _gameplayDeadBodyRenderController.ResolveClientPluginDeadBodyAnimationKind(sourcePlayerId, classId, team, animationKind);
    }

    private bool TryGetForcedLastToDieDeadBodyAnimationKind(
        int sourcePlayerId,
        PlayerClass classId,
        PlayerTeam team,
        DeadBodyAnimationKind deadBodyAnimationKind,
        out ClientDeadBodyAnimationKind forcedAnimationKind)
    {
        return _gameplayDeadBodyRenderController.TryGetForcedLastToDieDeadBodyAnimationKind(sourcePlayerId, classId, team, deadBodyAnimationKind, out forcedAnimationKind);
    }

    private bool TryDrawPlayerSprite(PlayerEntity player, Vector2 cameraPosition, Color tint, PlayerBodySpriteSelection bodySelection)
    {
        return _gameplayPlayerSpriteRenderController.TryDrawPlayerSprite(player, cameraPosition, tint, bodySelection);
    }

    private bool TryDrawPlayerSpriteAtPosition(
        PlayerEntity player,
        Vector2 renderPosition,
        Vector2 cameraPosition,
        Color tint,
        PlayerBodySpriteSelection bodySelection,
        bool drawIntelOverlay)
    {
        return _gameplayPlayerSpriteRenderController.TryDrawPlayerSpriteAtPosition(player, renderPosition, cameraPosition, tint, bodySelection, drawIntelOverlay);
    }

    private void DrawIntelUnderlaySprite(
        PlayerEntity player,
        Vector2 cameraPosition,
        Color tint,
        Vector2 scale,
        PlayerBodySpriteSelection bodySelection,
        Vector2 roundedOrigin)
    {
        _gameplayPlayerSpriteRenderController.DrawIntelUnderlaySprite(player, cameraPosition, tint, scale, bodySelection, roundedOrigin);
    }

    private void DrawCarriedIntelTimerSprite(PlayerEntity player, Vector2 cameraPosition, Vector2 roundedOrigin)
    {
        _gameplayPlayerSpriteRenderController.DrawCarriedIntelTimerSprite(player, cameraPosition, roundedOrigin);
    }

    private static PlayerTeam GetCarriedIntelTeam(PlayerEntity player)
    {
        return GameplayPlayerSpriteRenderController.GetCarriedIntelTeamProxy(player);
    }

    private static int GetPlayerBodySpriteFrameIndex(float animationImage, int frameCount)
    {
        return GameplayPlayerSpriteRenderController.GetPlayerBodySpriteFrameIndexProxy(animationImage, frameCount);
    }

    private int GetHumiliationSpriteFrameIndex(PlayerEntity player, float animationImage, int frameCount)
    {
        return _gameplayPlayerSpriteRenderController.GetHumiliationSpriteFrameIndex(player, animationImage, frameCount);
    }

    private static int GetTauntSpriteFrameIndex(PlayerEntity player, int frameCount)
    {
        return GameplayPlayerSpriteRenderController.GetTauntSpriteFrameIndexProxy(player, frameCount);
    }

    private static int GetHeavyEatSpriteFrameIndex(int heavyEatTicksRemaining, int frameCount, PlayerTeam team)
    {
        return GameplayPlayerSpriteRenderController.GetHeavyEatSpriteFrameIndexProxy(heavyEatTicksRemaining, frameCount, team);
    }

    private bool TryDrawWeaponSprite(PlayerEntity player, Vector2 cameraPosition, Color tint, float visibilityAlpha, PlayerBodySpriteSelection bodySelection)
    {
        return _gameplayWeaponRenderController.TryDrawWeaponSprite(player, cameraPosition, tint, visibilityAlpha, bodySelection);
    }

    private bool TryDrawWeaponSpriteAtPosition(
        PlayerEntity player,
        Vector2 renderPosition,
        Vector2 cameraPosition,
        Color tint,
        float visibilityAlpha,
        PlayerBodySpriteSelection bodySelection)
    {
        return _gameplayWeaponRenderController.TryDrawWeaponSpriteAtPosition(player, renderPosition, cameraPosition, tint, visibilityAlpha, bodySelection);
    }

    private void DrawExperimentalDemoknightChargeBlur(
        PlayerEntity player,
        Vector2 cameraPosition,
        Color spriteTint,
        float visibilityAlpha,
        PlayerBodySpriteSelection bodySelection)
    {
        _gameplayPlayerStatusEffectRenderController.DrawExperimentalDemoknightChargeBlur(player, cameraPosition, spriteTint, visibilityAlpha, bodySelection);
    }

    private void DrawCapturedPointHealingGhosting(
        PlayerEntity player,
        Vector2 renderPosition,
        Vector2 cameraPosition,
        float visibilityAlpha,
        PlayerBodySpriteSelection bodySelection)
    {
        _gameplayPlayerStatusEffectRenderController.DrawCapturedPointHealingGhosting(player, renderPosition, cameraPosition, visibilityAlpha, bodySelection);
    }

    private Vector2 GetExperimentalDemoknightChargeBlurDirection(PlayerEntity player)
    {
        return _gameplayPlayerStatusEffectRenderController.GetExperimentalDemoknightChargeBlurDirection(player);
    }

    private Vector2 GetWeaponAnchorOrigin(WeaponRenderDefinition weaponDefinition, LoadedGameMakerSprite currentSprite)
    {
        return _gameplayWeaponRenderController.GetWeaponAnchorOrigin(weaponDefinition, currentSprite);
    }

    private Vector2 GetWeaponShellSpawnOrigin(PlayerEntity player)
    {
        return _gameplayWeaponRenderController.GetWeaponShellSpawnOrigin(player);
    }

    private PlayerBodySpriteSelection GetPlayerBodySpriteSelection(PlayerEntity player)
    {
        return _gameplayPlayerSpriteRenderController.GetPlayerBodySpriteSelection(player);
    }

    private string? GetStandingSpriteName(PlayerEntity player)
    {
        return _gameplayPlayerSpriteRenderController.GetStandingSpriteName(player);
    }

    private LeanDirection GetPlayerLeanDirection(PlayerEntity player)
    {
        return _gameplayPlayerSpriteRenderController.GetPlayerLeanDirection(player);
    }

    private bool IsPointBlockedForPlayer(PlayerEntity player, float x, float y)
    {
        return _gameplayPlayerSpriteRenderController.IsPointBlockedForPlayer(player, x, y);
    }

    private WeaponAnimationMode GetPlayerWeaponAnimationMode(PlayerEntity player)
    {
        return _gameplayWeaponRenderController.GetPlayerWeaponAnimationMode(player);
    }

    private int GetWeaponSpriteFrameIndex(PlayerEntity player, WeaponAnimationMode weaponAnimationMode, WeaponRenderDefinition weaponDefinition, int frameCount)
    {
        return _gameplayWeaponRenderController.GetWeaponSpriteFrameIndex(player, weaponAnimationMode, weaponDefinition, frameCount);
    }

    private static WeaponRenderDefinition GetWeaponRenderDefinition(PlayerEntity player)
    {
        return GameplayWeaponRenderController.GetWeaponRenderDefinitionProxy(player);
    }

    private static float GetSourceTicksAsSeconds(float ticks)
    {
        return GameplayWeaponRenderController.GetSourceTicksAsSecondsProxy(ticks);
    }

    private static float GetPlayerFacingScale(PlayerEntity player)
    {
        return GameplayPlayerSpriteRenderController.GetPlayerFacingScale(player);
    }

    private static bool IsFacingLeftByAim(PlayerEntity player)
    {
        return GameplayPlayerSpriteRenderController.IsFacingLeftByAim(player);
    }

    private static float GetWeaponRotation(PlayerEntity player)
    {
        return GameplayWeaponRenderController.GetWeaponRotation(player);
    }

    private static string? GetTauntSpriteName(PlayerEntity player)
    {
        return GameplayPlayerSpriteRenderController.GetTauntSpriteName(player);
    }

    private static string? GetHeavyEatSpriteName(PlayerEntity player)
    {
        return GameplayPlayerSpriteRenderController.GetHeavyEatSpriteName(player);
    }

    private static string? GetPlayerSpriteName(PlayerEntity player)
    {
        return GameplayPlayerSpriteRenderController.GetPlayerSpriteName(player);
    }

    private static string? GetPlayerSpriteName(PlayerClass classId, PlayerTeam team)
    {
        return GameplayPlayerSpriteRenderController.GetPlayerSpriteName(classId, team);
    }

    private static string? GetDeadBodySpriteName(PlayerClass classId, PlayerTeam team, DeadBodyAnimationKind animationKind = DeadBodyAnimationKind.Default)
    {
        return GameplayPlayerSpriteRenderController.GetDeadBodySpriteName(classId, team, animationKind);
    }

    private static string? GetTeamSpriteName(PlayerClass classId, PlayerTeam team, string suffix)
    {
        return GameplayPlayerSpriteRenderController.GetTeamSpriteNameProxy(classId, team, suffix);
    }

    private static string? GetPlayerSpritePrefix(PlayerClass classId)
    {
        return GameplayPlayerSpriteRenderController.GetPlayerSpritePrefixProxy(classId);
    }

    private Color GetPlayerColor(PlayerEntity player, Color baseColor)
    {
        return _gameplayPlayerStatusEffectRenderController.GetPlayerColor(player, baseColor);
    }

    private static Color GetUberOverlayColor(PlayerTeam team)
    {
        return GameplayPlayerStatusEffectRenderController.GetUberOverlayColor(team);
    }

    private void DrawAfterburnOverlay(PlayerEntity player, Vector2 renderPosition, Vector2 cameraPosition, float visibilityAlpha)
    {
        _gameplayPlayerStatusEffectRenderController.DrawAfterburnOverlay(player, renderPosition, cameraPosition, visibilityAlpha);
    }

    private void DrawDominationIndicator(PlayerEntity player, Vector2 cameraPosition, float visibilityAlpha)
    {
        _gameplayPlayerStatusEffectRenderController.DrawDominationIndicator(player, cameraPosition, visibilityAlpha);
    }

    private IEnumerable<PlayerEntity> EnumerateRenderablePlayers()
    {
        return _gameplayPlayerRenderController.EnumerateRenderablePlayers();
    }

    private static string GetHudPlayerLabel(PlayerEntity player)
    {
        return GameplayPlayerRenderController.GetHudPlayerLabel(player);
    }
}
