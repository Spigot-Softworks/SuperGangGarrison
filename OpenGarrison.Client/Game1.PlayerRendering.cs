#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private readonly record struct PlayerBodySpriteSelection(
        string? SpriteName,
        float AnimationImage,
        float BodyYOffset,
        float EquipmentOffset,
        bool DrawIntelUnderlay);

    private readonly record struct WeaponRenderDefinition(
        string? NormalSpriteName,
        string? RecoilSpriteName,
        string? ReloadSpriteName,
        float XOffset,
        float YOffset,
        float RecoilDurationSeconds,
        float ReloadDurationSeconds,
        float ScopedRecoilDurationSeconds = 0f,
        bool LoopRecoilWhileActive = false);

    private enum LeanDirection
    {
        None,
        Left,
        Right,
    }

    private Rectangle GetPlayerScreenBounds(PlayerEntity player, Vector2 renderPosition, Vector2 cameraPosition)
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

    private static Vector2 GetRoundedPlayerSpriteOrigin(Vector2 renderPosition, PlayerClass classId)
    {
        var spriteOffset = GetPlayerSpriteOffset(classId);
        return RoundToSourcePixels(renderPosition + spriteOffset);
    }

    private void DrawPlayer(PlayerEntity player, Vector2 cameraPosition, Color aliveColor, Color deadColor)
    {
        if (!player.IsAlive)
        {
            return;
        }

        var visibilityAlpha = GetPlayerVisibilityAlpha(player);
        if (visibilityAlpha <= 0f)
        {
            return;
        }

        var renderPosition = GetRenderPosition(player, allowInterpolation: !ReferenceEquals(player, _world.LocalPlayer));
        var rectangle = GetPlayerScreenBounds(player, renderPosition, cameraPosition);
        var fallbackColor = aliveColor * visibilityAlpha;
        var spriteTint = GetPlayerColor(player, Color.White);
        var bodySelection = GetPlayerBodySpriteSelection(player);
        if (!TryDrawPlayerSprite(player, cameraPosition, spriteTint, bodySelection))
        {
            _spriteBatch.Draw(_pixel, rectangle, fallbackColor);
        }

        if (!GetPlayerIsHeavyEating(player) && !player.IsTaunting)
        {
            TryDrawWeaponSprite(player, cameraPosition, spriteTint, visibilityAlpha, bodySelection);
        }

        DrawAfterburnOverlay(player, renderPosition, cameraPosition, visibilityAlpha);
        DrawDominationIndicator(player, cameraPosition, visibilityAlpha);
        DrawChatBubble(player, cameraPosition);
        if (_showHealthBarEnabled && visibilityAlpha > 0f)
        {
            DrawHealthBar(player, cameraPosition, new Color(120, 220, 120), new Color(36, 64, 36));
        }
    }

    private void DrawDeadBody(DeadBodyEntity deadBody, Vector2 cameraPosition)
    {
        var renderPosition = GetRenderPosition(deadBody.Id, deadBody.X, deadBody.Y);
        var spriteName = GetDeadBodySpriteName(deadBody.ClassId, deadBody.Team);
        if (spriteName is not null)
        {
            var sprite = _runtimeAssets.GetSprite(spriteName);
            if (sprite is not null && sprite.Frames.Count > 0)
            {
                var roundedOrigin = GetRoundedPlayerSpriteOrigin(renderPosition, deadBody.ClassId);
                _spriteBatch.Draw(
                    sprite.Frames[0],
                    new Vector2(
                        roundedOrigin.X - cameraPosition.X,
                        roundedOrigin.Y - cameraPosition.Y),
                    null,
                    Color.White,
                    0f,
                    sprite.Origin.ToVector2(),
                    new Vector2(deadBody.FacingLeft ? -1f : 1f, 1f),
                    SpriteEffects.None,
                    0f);
                return;
            }
        }

        var rectangle = new Rectangle(
            (int)(renderPosition.X - (deadBody.Width / 2f) - cameraPosition.X),
            (int)(renderPosition.Y - (deadBody.Height / 2f) - cameraPosition.Y),
            (int)deadBody.Width,
            (int)deadBody.Height);
        _spriteBatch.Draw(_pixel, rectangle, deadBody.Team == PlayerTeam.Blue ? new Color(24, 45, 80) : new Color(90, 30, 30));
    }

    private bool TryDrawPlayerSprite(PlayerEntity player, Vector2 cameraPosition, Color tint, PlayerBodySpriteSelection bodySelection)
    {
        var isHeavyEating = GetPlayerIsHeavyEating(player);
        var spriteName = isHeavyEating
            ? GetHeavyEatSpriteName(player)
            : player.IsTaunting
                ? GetTauntSpriteName(player)
                : bodySelection.SpriteName;
        if (spriteName is null)
        {
            return false;
        }

        var sprite = _runtimeAssets.GetSprite(spriteName);
        if (sprite is null || sprite.Frames.Count == 0)
        {
            return false;
        }

        var renderPosition = GetRenderPosition(player, allowInterpolation: !ReferenceEquals(player, _world.LocalPlayer));
        var facingScale = GetPlayerFacingScale(player);
        var scale = new Vector2(facingScale, 1f);
        var frameIndex = isHeavyEating
            ? GetHeavyEatSpriteFrameIndex(GetPlayerHeavyEatTicksRemaining(player), sprite.Frames.Count, player.Team)
            : player.IsTaunting
                ? GetTauntSpriteFrameIndex(player, sprite.Frames.Count)
                : GetPlayerBodySpriteFrameIndex(bodySelection.AnimationImage, sprite.Frames.Count);
        var roundedOrigin = GetRoundedPlayerSpriteOrigin(renderPosition, player.ClassId);
        var bodyYOffset = isHeavyEating || player.IsTaunting ? 0f : bodySelection.BodyYOffset;
        var position = new Vector2(
            roundedOrigin.X - cameraPosition.X,
            roundedOrigin.Y + bodyYOffset - cameraPosition.Y);

        if (!isHeavyEating && !player.IsTaunting && bodySelection.DrawIntelUnderlay)
        {
            DrawIntelUnderlaySprite(player, cameraPosition, tint, scale, bodySelection, roundedOrigin);
        }

        _spriteBatch.Draw(
            sprite.Frames[frameIndex],
            position,
            null,
            tint,
            0f,
            sprite.Origin.ToVector2(),
            scale,
            SpriteEffects.None,
            0f);
        if (player.IsUbered)
        {
            _spriteBatch.Draw(
                sprite.Frames[frameIndex],
                position,
                null,
                GetUberOverlayColor(player.Team) * 0.7f,
                0f,
                sprite.Origin.ToVector2(),
                scale,
                SpriteEffects.None,
                0f);
        }

        return true;
    }

    private void DrawIntelUnderlaySprite(
        PlayerEntity player,
        Vector2 cameraPosition,
        Color tint,
        Vector2 scale,
        PlayerBodySpriteSelection bodySelection,
        Vector2 roundedOrigin)
    {
        var spriteName = GetTeamSpriteName(player.ClassId, player.Team, "IntelS");
        if (spriteName is null)
        {
            return;
        }

        var sprite = _runtimeAssets.GetSprite(spriteName);
        if (sprite is null || sprite.Frames.Count == 0)
        {
            return;
        }

        _spriteBatch.Draw(
            sprite.Frames[0],
            new Vector2(
                roundedOrigin.X - cameraPosition.X,
                roundedOrigin.Y + bodySelection.EquipmentOffset - cameraPosition.Y),
            null,
            tint,
            0f,
            sprite.Origin.ToVector2(),
            scale,
            SpriteEffects.None,
            0f);
    }

    private static int GetPlayerBodySpriteFrameIndex(float animationImage, int frameCount)
    {
        if (frameCount <= 0)
        {
            return 0;
        }

        return Math.Clamp((int)MathF.Floor(WrapAnimationImage(animationImage, frameCount)), 0, frameCount - 1);
    }

    private static int GetTauntSpriteFrameIndex(PlayerEntity player, int frameCount)
    {
        if (frameCount <= 0)
        {
            return 0;
        }

        return Math.Clamp((int)MathF.Floor(player.TauntFrameIndex), 0, frameCount - 1);
    }

    private static int GetHeavyEatSpriteFrameIndex(int heavyEatTicksRemaining, int frameCount, PlayerTeam team)
    {
        if (frameCount <= 0)
        {
            return 0;
        }

        var expectedFrames = Math.Max(1, (int)MathF.Ceiling(PlayerEntity.HeavyEatDurationTicks * 0.25f) + 1);
        var hasTeamVariants = frameCount >= expectedFrames * 2;
        var perTeamFrames = hasTeamVariants ? frameCount / 2 : frameCount;
        var elapsedTicks = Math.Clamp(PlayerEntity.HeavyEatDurationTicks - heavyEatTicksRemaining, 0, PlayerEntity.HeavyEatDurationTicks);
        var animationIndex = Math.Clamp((int)MathF.Floor(elapsedTicks * 0.25f), 0, perTeamFrames - 1);
        var teamOffset = team == PlayerTeam.Blue && hasTeamVariants ? perTeamFrames : 0;
        return Math.Clamp(animationIndex + teamOffset, 0, frameCount - 1);
    }

    private bool TryDrawWeaponSprite(PlayerEntity player, Vector2 cameraPosition, Color tint, float visibilityAlpha, PlayerBodySpriteSelection bodySelection)
    {
        if (GetPlayerIsSpyCloaked(player) && visibilityAlpha <= PlayerEntity.SpyCloakToggleThreshold)
        {
            return false;
        }

        var weaponDefinition = GetWeaponRenderDefinition(player);
        if (weaponDefinition.NormalSpriteName is null)
        {
            return false;
        }

        var weaponAnimationMode = GetPlayerWeaponAnimationMode(player);
        var spriteName = weaponAnimationMode switch
        {
            WeaponAnimationMode.ScopedRecoil when weaponDefinition.ReloadSpriteName is not null => weaponDefinition.ReloadSpriteName,
            WeaponAnimationMode.Reload when weaponDefinition.ReloadSpriteName is not null => weaponDefinition.ReloadSpriteName,
            WeaponAnimationMode.Recoil when weaponDefinition.RecoilSpriteName is not null => weaponDefinition.RecoilSpriteName,
            _ => weaponDefinition.NormalSpriteName,
        };
        if (spriteName is null)
        {
            return false;
        }

        var sprite = _runtimeAssets.GetSprite(spriteName);
        if (sprite is null || sprite.Frames.Count == 0)
        {
            return false;
        }

        var facingScale = GetPlayerFacingScale(player);
        var frameIndex = GetWeaponSpriteFrameIndex(player, weaponAnimationMode, weaponDefinition, sprite.Frames.Count);
        var rotation = GetWeaponRotation(player);
        var renderPosition = GetRenderPosition(player, allowInterpolation: !ReferenceEquals(player, _world.LocalPlayer));
        var roundedOrigin = GetRoundedPlayerSpriteOrigin(renderPosition, player.ClassId);
        var anchorOrigin = GetWeaponAnchorOrigin(weaponDefinition, sprite);
        var drawX = roundedOrigin.X + (weaponDefinition.XOffset + anchorOrigin.X) * facingScale;
        var drawY = roundedOrigin.Y + weaponDefinition.YOffset + bodySelection.EquipmentOffset + anchorOrigin.Y;
        var position = new Vector2(drawX - cameraPosition.X, drawY - cameraPosition.Y);
        var scale = new Vector2(facingScale, 1f);
        _spriteBatch.Draw(
            sprite.Frames[frameIndex],
            position,
            null,
            tint,
            rotation,
            sprite.Origin.ToVector2(),
            scale,
            SpriteEffects.None,
            0f);
        if (player.IsUbered)
        {
            _spriteBatch.Draw(
                sprite.Frames[frameIndex],
                position,
                null,
                GetUberOverlayColor(player.Team) * 0.7f,
                rotation,
                sprite.Origin.ToVector2(),
                scale,
                SpriteEffects.None,
                0f);
        }

        return true;
    }

    private Vector2 GetWeaponAnchorOrigin(WeaponRenderDefinition weaponDefinition, LoadedGameMakerSprite currentSprite)
    {
        if (weaponDefinition.NormalSpriteName is not null)
        {
            var normalSprite = _runtimeAssets.GetSprite(weaponDefinition.NormalSpriteName);
            if (normalSprite is not null)
            {
                return normalSprite.Origin.ToVector2();
            }
        }

        return currentSprite.Origin.ToVector2();
    }

    private Vector2 GetWeaponShellSpawnOrigin(PlayerEntity player)
    {
        var renderPosition = GetRenderPosition(player, allowInterpolation: !ReferenceEquals(player, _world.LocalPlayer));
        return GetRoundedPlayerSpriteOrigin(renderPosition, player.ClassId);
    }

    private PlayerBodySpriteSelection GetPlayerBodySpriteSelection(PlayerEntity player)
    {
        var renderState = _playerRenderStates.GetValueOrDefault(GetPlayerStateKey(player));
        var animationImage = WrapAnimationImage(renderState?.BodyAnimationImage ?? 0f, GetPlayerBodyAnimationLength(player));
        var renderHorizontalSpeed = renderState?.RenderHorizontalSpeed ?? player.HorizontalSpeed;
        var horizontalSourceStepSpeed = GetPlayerAnimationSourceStepSpeed(renderHorizontalSpeed);
        var appearsAirborne = renderState?.AppearsAirborne ?? !player.IsGrounded;
        var noNewAnimation = player.ClassId == PlayerClass.Quote;
        if (noNewAnimation)
        {
            return new PlayerBodySpriteSelection(
                GetPlayerSpriteName(player),
                animationImage,
                0f,
                0f,
                false);
        }

        if (player.IsSniperScoped)
        {
            return new PlayerBodySpriteSelection(
                GetTeamSpriteName(player.ClassId, player.Team, "CrouchS"),
                WrapAnimationImage(animationImage, 2f),
                0f,
                0f,
                false);
        }

        string? spriteName;
        var bodyYOffset = 0f;
        var isRunSprite = false;
        var isHeavySlowWalk = false;
        if (appearsAirborne)
        {
            spriteName = GetTeamSpriteName(player.ClassId, player.Team, "JumpS");
        }
        else if (horizontalSourceStepSpeed < 0.2f)
        {
            spriteName = GetStandingSpriteName(player);
            if (spriteName is not null && spriteName.Contains("Lean", StringComparison.Ordinal))
            {
                bodyYOffset = 6f;
            }
        }
        else if (player.ClassId == PlayerClass.Heavy && horizontalSourceStepSpeed < 3f)
        {
            spriteName = GetTeamSpriteName(player.ClassId, player.Team, "WalkS");
            isHeavySlowWalk = true;
        }
        else
        {
            spriteName = GetTeamSpriteName(player.ClassId, player.Team, "RunS");
            isRunSprite = true;
        }

        var equipmentOffset = bodyYOffset;
        if (isRunSprite
            && !appearsAirborne
            && (Math.Abs((int)MathF.Floor(animationImage) % 2) == 0))
        {
            equipmentOffset -= 2f;
        }

        if (isHeavySlowWalk || player.ClassId == PlayerClass.Soldier)
        {
            equipmentOffset = bodyYOffset;
        }
        else if (horizontalSourceStepSpeed < 3f && equipmentOffset < bodyYOffset)
        {
            bodyYOffset += 2f;
            equipmentOffset += 2f;
        }

        return new PlayerBodySpriteSelection(
            spriteName,
            animationImage,
            bodyYOffset,
            equipmentOffset,
            player.IsCarryingIntel);
    }

    private string? GetStandingSpriteName(PlayerEntity player)
    {
        var leanDirection = GetPlayerLeanDirection(player);
        if (leanDirection == LeanDirection.None)
        {
            return GetTeamSpriteName(player.ClassId, player.Team, "StandS");
        }

        var facingLeft = IsFacingLeftByAim(player);
        var suffix = leanDirection switch
        {
            LeanDirection.Left => facingLeft ? "LeanRS" : "LeanLS",
            LeanDirection.Right => facingLeft ? "LeanLS" : "LeanRS",
            _ => "StandS",
        };

        return GetTeamSpriteName(player.ClassId, player.Team, suffix);
    }

    private LeanDirection GetPlayerLeanDirection(PlayerEntity player)
    {
        var bottom = player.Bottom + 2f;
        var openRight = !IsPointBlockedForPlayer(player, player.X + 6f, bottom)
            && !IsPointBlockedForPlayer(player, player.X + 2f, bottom);
        var openLeft = !IsPointBlockedForPlayer(player, player.X - 7f, bottom)
            && !IsPointBlockedForPlayer(player, player.X - 3f, bottom);

        var leanDirection = LeanDirection.None;
        if (openRight)
        {
            leanDirection = LeanDirection.Right;
        }

        if (openLeft)
        {
            leanDirection = LeanDirection.Left;
        }

        if (openRight && openLeft)
        {
            openRight = !IsPointBlockedForPlayer(player, player.Right - 1f, bottom);
            openLeft = !IsPointBlockedForPlayer(player, player.Left, bottom);
            leanDirection = LeanDirection.None;
            if (openRight)
            {
                leanDirection = LeanDirection.Right;
            }

            if (openLeft)
            {
                leanDirection = LeanDirection.Left;
            }
        }

        return leanDirection;
    }

    private bool IsPointBlockedForPlayer(PlayerEntity player, float x, float y)
    {
        foreach (var solid in _world.Level.Solids)
        {
            if (x >= solid.Left && x < solid.Right && y >= solid.Top && y < solid.Bottom)
            {
                return true;
            }
        }

        foreach (var gate in _world.Level.GetBlockingTeamGates(player.Team, player.IsCarryingIntel))
        {
            if (x >= gate.Left && x < gate.Right && y >= gate.Top && y < gate.Bottom)
            {
                return true;
            }
        }

        foreach (var wall in _world.Level.GetRoomObjects(RoomObjectType.PlayerWall))
        {
            if (x >= wall.Left && x < wall.Right && y >= wall.Top && y < wall.Bottom)
            {
                return true;
            }
        }

        return false;
    }

    private WeaponAnimationMode GetPlayerWeaponAnimationMode(PlayerEntity player)
    {
        return _playerRenderStates.TryGetValue(GetPlayerStateKey(player), out var renderState)
            ? renderState.WeaponAnimationMode
            : WeaponAnimationMode.Idle;
    }

    private int GetWeaponSpriteFrameIndex(PlayerEntity player, WeaponAnimationMode weaponAnimationMode, WeaponRenderDefinition weaponDefinition, int frameCount)
    {
        if (frameCount <= 0)
        {
            return 0;
        }

        if (weaponAnimationMode == WeaponAnimationMode.Idle)
        {
            return Math.Clamp(player.Team == PlayerTeam.Blue ? 1 : 0, 0, frameCount - 1);
        }

        if (!_playerRenderStates.TryGetValue(GetPlayerStateKey(player), out var renderState))
        {
            return 0;
        }

        var perTeamFrames = Math.Max(1, frameCount / 2);
        var durationSeconds = MathF.Max(renderState.WeaponAnimationDurationSeconds, 0.0001f);
        var animationPosition = (renderState.WeaponAnimationElapsedSeconds / durationSeconds) * perTeamFrames;
        var animationFrame = weaponAnimationMode == WeaponAnimationMode.Recoil && weaponDefinition.LoopRecoilWhileActive
            ? Math.Clamp((int)MathF.Floor(WrapAnimationImage(animationPosition, perTeamFrames)), 0, perTeamFrames - 1)
            : Math.Clamp((int)MathF.Floor(animationPosition), 0, perTeamFrames - 1);
        var teamOffset = player.Team == PlayerTeam.Blue ? perTeamFrames : 0;
        return Math.Clamp(teamOffset + animationFrame, 0, frameCount - 1);
    }

    private static Vector2 GetPlayerSpriteOffset(PlayerClass classId)
    {
        return classId == PlayerClass.Quote ? new Vector2(0f, 12f) : Vector2.Zero;
    }

    private static WeaponRenderDefinition GetWeaponRenderDefinition(PlayerEntity player)
    {
        return player.ClassId switch
        {
            PlayerClass.Scout => new("ScattergunS", "ScattergunFS", "ScattergunFRS", -5f, -4f, GetSourceTicksAsSeconds(20), GetSourceTicksAsSeconds(15)),
            PlayerClass.Engineer => new("ShotgunS", "ShotgunFS", "ShotgunFRS", -5f, -2f, GetSourceTicksAsSeconds(20), GetSourceTicksAsSeconds(15)),
            PlayerClass.Pyro => new("FlamethrowerS", "FlamethrowerFS", null, -11f, 4f, GetSourceTicksAsSeconds(3), 0f, LoopRecoilWhileActive: true),
            PlayerClass.Soldier => new("RocketlauncherS", "RocketlauncherFS", "RocketlauncherFRS", -15f, -10f, GetSourceTicksAsSeconds(30), GetSourceTicksAsSeconds(22)),
            PlayerClass.Demoman => new("MinegunS", "MinegunFS", "MinegunFRS", -3f, -2f, GetSourceTicksAsSeconds(26), GetSourceTicksAsSeconds(15)),
            PlayerClass.Heavy => new("MinigunS", "MinigunFS", null, -11f, 0f, GetSourceTicksAsSeconds(3), 0f, LoopRecoilWhileActive: true),
            PlayerClass.Sniper => new("RifleS", "RifleFS", "RifleFRS", -5f, -8f, GetSourceTicksAsSeconds(15), 0f, GetSourceTicksAsSeconds(60)),
            PlayerClass.Medic => new("MedigunS", "MedigunFS", "MedigunFRS", -7f, 0f, GetSourceTicksAsSeconds(4), GetSourceTicksAsSeconds(55)),
            PlayerClass.Spy => new("RevolverS", "RevolverFS", "RevolverFRS", -3f, -6f, GetSourceTicksAsSeconds(18), GetSourceTicksAsSeconds(45)),
            PlayerClass.Quote => new("BladeS", null, null, -3f, -6f, 0f, 0f),
            _ => new(null, null, null, 0f, 0f, 0f, 0f),
        };
    }

    private static float GetSourceTicksAsSeconds(float ticks)
    {
        return ticks / (float)LegacyMovementModel.SourceTicksPerSecond;
    }

    private static float GetPlayerFacingScale(PlayerEntity player)
    {
        return IsFacingLeftByAim(player) ? -1f : 1f;
    }

    private static bool IsFacingLeftByAim(PlayerEntity player)
    {
        var radians = MathF.PI * player.AimDirectionDegrees / 180f;
        return MathF.Cos(radians) < 0f;
    }

    private static float GetWeaponRotation(PlayerEntity player)
    {
        var radians = MathF.PI * player.AimDirectionDegrees / 180f;
        return IsFacingLeftByAim(player) ? radians + MathF.PI : radians;
    }

    private static string? GetTauntSpriteName(PlayerEntity player)
    {
        return GetTeamSpriteName(player.ClassId, player.Team, "TauntS");
    }

    private static string? GetHeavyEatSpriteName(PlayerEntity player)
    {
        return player.ClassId == PlayerClass.Heavy
            ? GetTeamSpriteName(player.ClassId, player.Team, "OmnomnomnomS")
            : null;
    }

    private static string? GetPlayerSpriteName(PlayerEntity player)
    {
        return GetPlayerSpriteName(player.ClassId, player.Team);
    }

    private static string? GetPlayerSpriteName(PlayerClass classId, PlayerTeam team)
    {
        return GetTeamSpriteName(classId, team, "S");
    }

    private static string? GetDeadBodySpriteName(PlayerClass classId, PlayerTeam team)
    {
        return GetTeamSpriteName(classId, team, "DeadS");
    }

    private static string? GetTeamSpriteName(PlayerClass classId, PlayerTeam team, string suffix)
    {
        var prefix = GetPlayerSpritePrefix(classId);
        if (prefix is null)
        {
            return null;
        }

        var teamName = team switch
        {
            PlayerTeam.Red => "Red",
            PlayerTeam.Blue => "Blue",
            _ => null,
        };

        return teamName is null
            ? null
            : $"{prefix}{teamName}{suffix}";
    }

    private static string? GetPlayerSpritePrefix(PlayerClass classId)
    {
        return classId switch
        {
            PlayerClass.Scout => "Scout",
            PlayerClass.Engineer => "Engineer",
            PlayerClass.Pyro => "Pyro",
            PlayerClass.Soldier => "Soldier",
            PlayerClass.Demoman => "Demoman",
            PlayerClass.Heavy => "Heavy",
            PlayerClass.Sniper => "Sniper",
            PlayerClass.Medic => "Medic",
            PlayerClass.Spy => "Spy",
            PlayerClass.Quote => "Querly",
            _ => null,
        };
    }

    private Color GetPlayerColor(PlayerEntity player, Color baseColor)
    {
        return baseColor * GetPlayerVisibilityAlpha(player);
    }

    private static Color GetUberOverlayColor(PlayerTeam team)
    {
        return team == PlayerTeam.Blue ? Color.Blue : Color.Red;
    }

    private void DrawAfterburnOverlay(PlayerEntity player, Vector2 renderPosition, Vector2 cameraPosition, float visibilityAlpha)
    {
        if (!player.IsAlive || !player.IsBurning)
        {
            return;
        }

        var alpha = player.BurnVisualAlpha * visibilityAlpha;
        if (alpha <= 0f)
        {
            return;
        }

        var count = player.BurnVisualCount;
        if (count <= 0)
        {
            return;
        }

        var sprite = _runtimeAssets.GetSprite("FlameS");
        var sourceFrame = (int)((_world.Frame * LegacyMovementModel.SourceTicksPerSecond) / _config.TicksPerSecond);
        var flameColor = Color.White * alpha;
        for (var flameIndex = 0; flameIndex < count; flameIndex += 1)
        {
            player.GetBurnVisualOffset(flameIndex, sourceFrame, out var offsetX, out var offsetY);
            if (sprite is not null && sprite.Frames.Count > 0)
            {
                var frameIndex = player.GetBurnVisualFrameIndex(flameIndex, sourceFrame, sprite.Frames.Count);
                _spriteBatch.Draw(
                    sprite.Frames[frameIndex],
                    new Vector2(renderPosition.X + offsetX - cameraPosition.X, renderPosition.Y + offsetY - cameraPosition.Y),
                    null,
                    flameColor,
                    0f,
                    sprite.Origin.ToVector2(),
                    Vector2.One,
                    SpriteEffects.None,
                    0f);
                continue;
            }

            var flameRectangle = new Rectangle(
                (int)(renderPosition.X + offsetX - 2f - cameraPosition.X),
                (int)(renderPosition.Y + offsetY - 2f - cameraPosition.Y),
                4,
                4);
            _spriteBatch.Draw(_pixel, flameRectangle, flameColor);
        }
    }

    private void DrawDominationIndicator(PlayerEntity player, Vector2 cameraPosition, float visibilityAlpha)
    {
        if (!player.IsDominatingLocalViewer || visibilityAlpha <= 0f)
        {
            return;
        }

        var renderPosition = GetRenderPosition(player, allowInterpolation: !ReferenceEquals(player, _world.LocalPlayer));
        var frameIndex = player.Team == PlayerTeam.Blue ? 1 : 0;
        DrawCenteredHudSprite(
            "DominationS",
            frameIndex,
            new Vector2(renderPosition.X - cameraPosition.X, renderPosition.Y - cameraPosition.Y - 35f),
            Color.White * visibilityAlpha,
            Vector2.One);
    }

    private IEnumerable<PlayerEntity> EnumerateRenderablePlayers()
    {
        if (!_networkClient.IsSpectator)
        {
            yield return _world.LocalPlayer;
        }

        foreach (var player in EnumerateRemotePlayersForView())
        {
            yield return player;
        }
    }

    private static string GetHudPlayerLabel(PlayerEntity player)
    {
        return player.DisplayName;
    }
}
