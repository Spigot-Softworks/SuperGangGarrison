#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
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
        var rectangle = new Rectangle(
            (int)(renderPosition.X - (player.Width / 2f) - cameraPosition.X),
            (int)(renderPosition.Y - (player.Height / 2f) - cameraPosition.Y),
            (int)player.Width,
            (int)player.Height);
        var fallbackColor = aliveColor * visibilityAlpha;
        var spriteTint = GetPlayerColor(player, Color.White);
        if (!TryDrawPlayerSprite(player, cameraPosition, spriteTint))
        {
            _spriteBatch.Draw(_pixel, rectangle, fallbackColor);
        }

        if (!GetPlayerIsHeavyEating(player) && !player.IsTaunting)
        {
            TryDrawWeaponSprite(player, cameraPosition, spriteTint, visibilityAlpha);
        }

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
                _spriteBatch.Draw(
                    sprite.Frames[0],
                    new Vector2(
                        renderPosition.X - cameraPosition.X + GetPlayerSpriteOffset(deadBody.ClassId).X,
                        renderPosition.Y - cameraPosition.Y + GetPlayerSpriteOffset(deadBody.ClassId).Y),
                    null,
                    Color.White,
                    0f,
                    sprite.Origin.ToVector2(),
                    Vector2.One,
                    deadBody.FacingLeft ? SpriteEffects.FlipHorizontally : SpriteEffects.None,
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

    private bool TryDrawPlayerSprite(PlayerEntity player, Vector2 cameraPosition, Color tint)
    {
        var isHeavyEating = GetPlayerIsHeavyEating(player);
        var spriteName = isHeavyEating
            ? GetHeavyEatSpriteName(player)
            : player.IsTaunting
                ? GetTauntSpriteName(player)
                : GetPlayerSpriteName(player);
        if (spriteName is null)
        {
            return false;
        }

        var sprite = _runtimeAssets.GetSprite(spriteName);
        if (sprite is null || sprite.Frames.Count == 0)
        {
            return false;
        }

        var effects = GetSpriteEffectsFromAim(player);
        var frameIndex = isHeavyEating
            ? GetHeavyEatSpriteFrameIndex(GetPlayerHeavyEatTicksRemaining(player), sprite.Frames.Count, player.Team)
            : player.IsTaunting
                ? GetTauntSpriteFrameIndex(player, sprite.Frames.Count)
                : GetPlayerSpriteFrameIndex(player, sprite.Frames.Count);
        var renderPosition = GetRenderPosition(player, allowInterpolation: !ReferenceEquals(player, _world.LocalPlayer));
        var spriteOffset = GetPlayerSpriteOffset(player.ClassId);
        var position = new Vector2(
            renderPosition.X - cameraPosition.X + spriteOffset.X,
            renderPosition.Y - cameraPosition.Y + spriteOffset.Y);
        _spriteBatch.Draw(
            sprite.Frames[frameIndex],
            position,
            null,
            tint,
            0f,
            sprite.Origin.ToVector2(),
            Vector2.One,
            effects,
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
                Vector2.One,
                effects,
                0f);
        }

        return true;
    }

    private int GetPlayerSpriteFrameIndex(PlayerEntity player, int frameCount)
    {
        const int normalOffset = 0;
        const int intelOffset = 2;
        const int deadFrame = 5;

        if (!player.IsAlive)
        {
            return Math.Clamp(deadFrame, 0, frameCount - 1);
        }

        var animationImage = _playerAnimationImages.GetValueOrDefault(GetPlayerStateKey(player), 0f);
        var animationOffset = player.IsCarryingIntel ? intelOffset : normalOffset;
        return Math.Clamp((int)MathF.Floor(animationImage + animationOffset), 0, frameCount - 1);
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

    private bool TryDrawWeaponSprite(PlayerEntity player, Vector2 cameraPosition, Color tint, float visibilityAlpha)
    {
        if (GetPlayerIsSpyCloaked(player) && visibilityAlpha <= PlayerEntity.SpyCloakToggleThreshold)
        {
            return false;
        }

        var (spriteName, xOffset, yOffset) = GetWeaponSpriteInfo(player);
        if (spriteName is null)
        {
            return false;
        }

        var sprite = _runtimeAssets.GetSprite(spriteName);
        if (sprite is null || sprite.Frames.Count == 0)
        {
            return false;
        }

        var facingScale = IsFacingLeftByAim(player) ? -1f : 1f;
        var frameIndex = GetWeaponSpriteFrameIndex(player, sprite.Frames.Count);
        var rotation = GetWeaponRotation(player);
        var leftFacingNudge = GetLeftFacingWeaponNudge(player, facingScale);
        var renderPosition = GetRenderPosition(player, allowInterpolation: !ReferenceEquals(player, _world.LocalPlayer));
        var spriteOffset = GetPlayerSpriteOffset(player.ClassId);
        var drawX = renderPosition.X + spriteOffset.X + (xOffset + sprite.Origin.X) * facingScale + leftFacingNudge;
        var drawY = renderPosition.Y + spriteOffset.Y + yOffset + sprite.Origin.Y;
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

    private static Vector2 GetPlayerSpriteOffset(PlayerClass classId)
    {
        return classId == PlayerClass.Quote ? new Vector2(0f, 12f) : Vector2.Zero;
    }

    private static (string? SpriteName, float XOffset, float YOffset) GetWeaponSpriteInfo(PlayerEntity player)
    {
        return player.ClassId switch
        {
            PlayerClass.Scout => ("ScattergunS", -5f, -4f),
            PlayerClass.Engineer => ("ShotgunS", -5f, -2f),
            PlayerClass.Pyro => ("FlamethrowerS", -11f, 4f),
            PlayerClass.Soldier => ("RocketlauncherS", -15f, -10f),
            PlayerClass.Demoman => ("MinegunS", -3f, -2f),
            PlayerClass.Heavy => ("MinigunS", -11f, 0f),
            PlayerClass.Sniper => ("RifleS", -5f, -8f),
            PlayerClass.Medic => ("MedigunS", -7f, 0f),
            PlayerClass.Spy => ("Revolver2S", -3f, -6f),
            PlayerClass.Quote => ("BladeS", -3f, -6f),
            _ => (null, 0f, 0f),
        };
    }

    private int GetWeaponSpriteFrameIndex(PlayerEntity player, int frameCount)
    {
        var teamFrame = player.Team == PlayerTeam.Blue ? 1 : 0;
        if (player.ClassId == PlayerClass.Medic && player.IsMedicHealing)
        {
            return Math.Clamp(teamFrame + 2, 0, frameCount - 1);
        }

        var flashOffset = _playerWeaponFlashTimes.GetValueOrDefault(GetPlayerStateKey(player), 0f) > 0f ? 2 : 0;
        return Math.Clamp(teamFrame + flashOffset, 0, frameCount - 1);
    }

    private static SpriteEffects GetSpriteEffectsFromAim(PlayerEntity player)
    {
        return IsFacingLeftByAim(player) ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
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

    private static float GetLeftFacingWeaponNudge(PlayerEntity player, float facingScale)
    {
        if (facingScale >= 0f)
        {
            return 0f;
        }

        return player.ClassId switch
        {
            PlayerClass.Heavy => 25f,
            PlayerClass.Sniper => 5f,
            _ => 1f,
        };
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
