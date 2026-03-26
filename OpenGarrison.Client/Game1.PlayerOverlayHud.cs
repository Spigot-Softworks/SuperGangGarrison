#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Globalization;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void DrawLocalHealthHud()
    {
        if (!_world.LocalPlayer.IsAlive)
        {
            return;
        }

        var viewportHeight = ViewportHeight;
        var frameIndex = GetCharacterHudFrameIndex(_world.LocalPlayer);
        DrawScreenHealthBar(new Rectangle(45, viewportHeight - 53, 42, 38), _world.LocalPlayer.Health, _world.LocalPlayer.MaxHealth, false, fillDirection: HudFillDirection.VerticalBottomToTop);
        TryDrawScreenSprite(
            "CharacterHUD",
            frameIndex,
            new Vector2(5f, viewportHeight - 75f),
            Color.White,
            new Vector2(2f, 2f));
        var hpColor = _world.LocalPlayer.Health > (_world.LocalPlayer.MaxHealth / 3.5f) ? Color.White : Color.Red;
        DrawHudTextCentered(Math.Max(_world.LocalPlayer.Health, 0).ToString(CultureInfo.InvariantCulture), new Vector2(69f, viewportHeight - 35f), hpColor, 1f);
    }

    private SentryEntity? GetLocalOwnedSentry()
    {
        foreach (var sentry in _world.Sentries)
        {
            if (sentry.OwnerPlayerId == GetPlayerStateKey(_world.LocalPlayer))
            {
                return sentry;
            }
        }

        return null;
    }

    private static int GetCharacterHudFrameIndex(PlayerEntity player)
    {
        var teamOffset = player.Team == PlayerTeam.Blue ? 10 : 0;
        var classIndex = player.ClassId switch
        {
            PlayerClass.Scout => 0,
            PlayerClass.Soldier => 1,
            PlayerClass.Sniper => 2,
            PlayerClass.Demoman => 3,
            PlayerClass.Medic => 4,
            PlayerClass.Engineer => 5,
            PlayerClass.Heavy => 6,
            PlayerClass.Spy => 7,
            PlayerClass.Pyro => 8,
            PlayerClass.Quote => 9,
            _ => 0,
        };

        return classIndex + teamOffset;
    }

    private static readonly Color AmmoHudBarColor = new(217, 217, 183);
    private static readonly Color AmmoHudTextColor = new(245, 235, 210);
    private static readonly Color LowAmmoHudColor = new(255, 0, 0);
    private static readonly Color DisabledAmmoHudColor = new(128, 128, 128);
    private static readonly Color HeavyCooldownHudColor = new(50, 50, 50);
    private const float SourceHudWidth = 800f;
    private const float SourceHudHeight = 600f;
    private const float SourceAmmoHudBaseY = SourceHudHeight / 1.26f;

    private void DrawAmmoHud()
    {
        if (!_world.LocalPlayer.IsAlive)
        {
            return;
        }

        switch (_world.LocalPlayer.PrimaryWeapon.Kind)
        {
            case PrimaryWeaponKind.FlameThrower:
                DrawPyroAmmoHud();
                return;
            case PrimaryWeaponKind.Minigun:
                DrawHeavyAmmoHud();
                DrawHeavySandwichHud();
                return;
            case PrimaryWeaponKind.Blade:
                DrawQuoteAmmoHud();
                return;
            case PrimaryWeaponKind.Rifle:
                return;
        }

        var hudSpriteName = GetAmmoHudSpriteName();
        if (hudSpriteName is null)
        {
            return;
        }

        if (TryDrawScreenSprite(
            hudSpriteName,
            GetAmmoHudFrameIndex(),
            GetSourceHudPoint(728f, SourceAmmoHudBaseY + 86f),
            Color.White,
            new Vector2(2.4f, 2.4f)))
        {
            if (_world.LocalPlayer.ClassId != PlayerClass.Soldier)
            {
                DrawHudTextLeftAligned(
                    GetPlayerCurrentShells(_world.LocalPlayer).ToString(CultureInfo.InvariantCulture),
                    GetSourceHudPoint(765f, SourceAmmoHudBaseY + 95f),
                    AmmoHudTextColor,
                    1f);
            }

            DrawAmmoReloadBar(GetReloadAmmoHudBarRectangle());
        }
    }

    private void DrawPyroAmmoHud()
    {
        var frameIndex = GetAmmoHudFrameIndex();
        if (!TryDrawSourceAmmoHudSprite("GasAmmoS", frameIndex))
        {
            return;
        }

        var currentShells = GetPlayerCurrentShells(_world.LocalPlayer);
        var barColor = currentShells <= (_world.LocalPlayer.MaxShells * 0.25f)
            ? LowAmmoHudColor
            : AmmoHudBarColor;
        DrawSourceAmmoHudBar(689f, 34f, currentShells, _world.LocalPlayer.MaxShells, barColor);
        DrawPyroFlareHud(frameIndex);
    }

    private void DrawHeavyAmmoHud()
    {
        if (!TryDrawSourceAmmoHudSprite("MinigunAmmoS", GetAmmoHudFrameIndex()))
        {
            return;
        }

        var currentShells = GetPlayerCurrentShells(_world.LocalPlayer);
        var ammoFraction = _world.LocalPlayer.MaxShells <= 0
            ? 0f
            : float.Clamp(currentShells / (float)_world.LocalPlayer.MaxShells, 0f, 1f);
        var barColor = Color.Lerp(AmmoHudBarColor, LowAmmoHudColor, 1f - ammoFraction);
        var cooldownFraction = float.Clamp(Math.Max(GetPlayerPrimaryCooldownTicks(_world.LocalPlayer), GetPlayerReloadTicksUntilNextShell(_world.LocalPlayer)) / 25f, 0f, 1f);
        barColor = Color.Lerp(barColor, HeavyCooldownHudColor, cooldownFraction);
        DrawSourceAmmoHudBar(689f, 34f, currentShells, _world.LocalPlayer.MaxShells, barColor);
    }

    private void DrawHeavySandwichHud()
    {
        if (_world.LocalPlayer.ClassId != PlayerClass.Heavy)
        {
            return;
        }

        if (!TryDrawScreenSprite(
            "SandwichHudS",
            _world.LocalPlayer.Team == PlayerTeam.Blue ? 1 : 0,
            GetSourceHudPoint(730f, 515f),
            Color.White,
            new Vector2(2f, 2f)))
        {
            return;
        }

        var cooldownRemaining = Math.Clamp(
            GetPlayerHeavyEatCooldownTicksRemaining(_world.LocalPlayer),
            0,
            PlayerEntity.HeavySandvichCooldownTicks);
        DrawScreenHealthBar(
            GetSourceHudRectangle(715f, 528f, 35f, 5f),
            PlayerEntity.HeavySandvichCooldownTicks - cooldownRemaining,
            PlayerEntity.HeavySandvichCooldownTicks,
            false,
            AmmoHudBarColor,
            Color.Black);
    }

    private void DrawQuoteAmmoHud()
    {
        if (!TryDrawSourceAmmoHudSprite("BladeAmmoS", GetAmmoHudFrameIndex()))
        {
            return;
        }

        DrawSourceAmmoHudBar(689f, 34f, GetPlayerCurrentShells(_world.LocalPlayer), _world.LocalPlayer.MaxShells, AmmoHudBarColor);
    }

    private void DrawPyroFlareHud(int frameIndex)
    {
        var flareCount = GetPlayerCurrentShells(_world.LocalPlayer) / PlayerEntity.PyroFlareAmmoRequirement;
        if (flareCount <= 0)
        {
            return;
        }

        var flareTint = GetPlayerPyroFlareCooldownTicks(_world.LocalPlayer) <= 0
            ? Color.White
            : DisabledAmmoHudColor;
        for (var flareIndex = 0; flareIndex < flareCount; flareIndex += 1)
        {
            TryDrawScreenSprite(
                "FlareS",
                frameIndex,
                GetSourceHudPoint(760f - (flareIndex * 20f), SourceAmmoHudBaseY + 93f),
                flareTint,
                Vector2.One);
        }
    }

    private bool TryDrawSourceAmmoHudSprite(string spriteName, int frameIndex)
    {
        return TryDrawScreenSprite(
            spriteName,
            frameIndex,
            GetSourceHudPoint(728f, SourceAmmoHudBaseY + 86f),
            Color.White,
            new Vector2(2.4f, 2.4f));
    }

    private void DrawSourceAmmoHudBar(float left, float width, float value, float maxValue, Color fillColor)
    {
        DrawScreenHealthBar(
            GetSourceHudRectangle(left, SourceAmmoHudBaseY + 90f, width, 8f),
            value,
            maxValue,
            false,
            fillColor,
            Color.Black);
    }

    private Rectangle GetReloadAmmoHudBarRectangle()
    {
        return _world.LocalPlayer.ClassId == PlayerClass.Soldier
            ? GetSourceHudRectangle(689f, SourceAmmoHudBaseY + 90f, 34f, 8f)
            : GetSourceHudRectangle(700f, SourceAmmoHudBaseY + 90f, 50f, 8f);
    }

    private Vector2 GetSourceHudPoint(float sourceX, float sourceY)
    {
        return new Vector2(
            ViewportWidth - SourceHudWidth + sourceX,
            ViewportHeight - SourceHudHeight + sourceY);
    }

    private Rectangle GetSourceHudRectangle(float sourceX, float sourceY, float width, float height)
    {
        var position = GetSourceHudPoint(sourceX, sourceY);
        return new Rectangle(
            (int)MathF.Round(position.X),
            (int)MathF.Round(position.Y),
            (int)MathF.Round(width),
            (int)MathF.Round(height));
    }

    private void DrawMedicHud()
    {
        if (_world.LocalPlayer.ClassId != PlayerClass.Medic)
        {
            return;
        }

        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        var hudFrameIndex = _world.LocalPlayer.Team == PlayerTeam.Blue ? 1 : 0;
        var uberRectangle = new Rectangle(viewportWidth - 135, viewportHeight - 100, 120, 32);
        DrawScreenHealthBar(uberRectangle, GetPlayerMedicUberCharge(_world.LocalPlayer), 2000f, false, Color.White, Color.Black);
        TryDrawScreenSprite(
            "UberHudS",
            hudFrameIndex,
            new Vector2(viewportWidth - 80f, viewportHeight - 85f),
            Color.White,
            new Vector2(2f, 2f));
        DrawHudTextCentered("SUPERBURST", new Vector2(viewportWidth - 70f, viewportHeight - 90f), Color.White, 0.9f);
    }

    private void DrawMedicAssistHud()
    {
        if (!_world.LocalPlayer.IsAlive)
        {
            return;
        }

        var healingTarget = _world.LocalPlayer.ClassId == PlayerClass.Medic
            && _world.LocalPlayer.IsMedicHealing
            && _world.LocalPlayer.MedicHealTargetId.HasValue
                ? FindPlayerById(_world.LocalPlayer.MedicHealTargetId.Value)
                : null;
        if (healingTarget is not null && !healingTarget.IsAlive)
        {
            healingTarget = null;
        }

        var healer = FindMedicHealingPlayer(GetPlayerStateKey(_world.LocalPlayer));
        var drewHealingHud = false;
        if (_showHealingEnabled && healingTarget is not null)
        {
            DrawCenterStatusHud(
                $"Healing: {GetHudPlayerLabel(healingTarget)}",
                healingTarget.Health,
                healingTarget.MaxHealth,
                viewportYRatio: 450f / 600f,
                textAlpha: 0.7f);
            drewHealingHud = true;
        }

        if (_showHealerEnabled && healer is not null)
        {
            DrawCenterStatusHud(
                $"Healer: {GetHudPlayerLabel(healer)}",
                healer.MedicUberCharge,
                2000f,
                viewportYRatio: drewHealingHud ? 490f / 600f : 450f / 600f,
                textAlpha: 0.5f);
        }
    }

    private void DrawEngineerHud()
    {
        if (_world.LocalPlayer.ClassId != PlayerClass.Engineer)
        {
            return;
        }

        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        var hudFrameIndex = _world.LocalPlayer.Team == PlayerTeam.Blue ? 1 : 0;
        TryDrawScreenSprite(
            "NutsNBoltsHudS",
            hudFrameIndex,
            new Vector2(viewportWidth - 70f, viewportHeight - 85f),
            Color.White,
            new Vector2(2f, 2f));
        DrawHudTextRightAligned(((int)MathF.Floor(GetPlayerMetal(_world.LocalPlayer))).ToString(CultureInfo.InvariantCulture), new Vector2(viewportWidth - 66f, viewportHeight - 91f), Color.White, 1.5f);

        var localSentry = GetLocalOwnedSentry();
        if (localSentry is null)
        {
            return;
        }

        DrawScreenHealthBar(new Rectangle(45, viewportHeight - 123, 42, 38), localSentry.Health, SentryEntity.MaxHealth, false, fillDirection: HudFillDirection.VerticalBottomToTop);
        TryDrawScreenSprite(
            "SentryHUD",
            hudFrameIndex,
            new Vector2(5f, viewportHeight - 145f),
            Color.White,
            new Vector2(2f, 2f));
        var sentryHpColor = localSentry.Health > (SentryEntity.MaxHealth / 3.5f) ? Color.White : Color.Red;
        DrawHudTextCentered(Math.Max(localSentry.Health, 0).ToString(CultureInfo.InvariantCulture), new Vector2(69f, viewportHeight - 105f), sentryHpColor, 1f);
    }

    private void DrawCenterStatusHud(string label, float value, float maxValue, float viewportYRatio, float textAlpha)
    {
        var sprite = _runtimeAssets.GetSprite("HealedHudS");
        if (sprite is null || sprite.Frames.Count == 0)
        {
            return;
        }

        var frameIndex = _world.LocalPlayer.Team == PlayerTeam.Blue ? 1 : 0;
        var frame = sprite.Frames[Math.Clamp(frameIndex, 0, sprite.Frames.Count - 1)];
        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        var textWidth = MeasureBitmapFontWidth(label, 0.7f);
        var hudWidth = (int)MathF.Ceiling(textWidth) + 20;
        var hudHeight = 40;
        var hudX = (viewportWidth / 2) - (hudWidth / 2);
        var hudY = (int)MathF.Round(viewportHeight * viewportYRatio);
        var destination = new Rectangle(hudX, hudY, hudWidth, hudHeight);

        _spriteBatch.Draw(frame, destination, Color.White * 0.5f);
        DrawHudTextCentered(label, new Vector2(viewportWidth / 2f, hudY + 12f), Color.White * textAlpha, 0.7f);
        DrawScreenHealthBar(
            new Rectangle(hudX + 10, hudY + 20, Math.Max(1, hudWidth - 20), 8),
            value,
            maxValue,
            false,
            Color.White,
            Color.Black);
    }

    private PlayerEntity? FindMedicHealingPlayer(int playerId)
    {
        foreach (var candidate in EnumerateRenderablePlayers())
        {
            if (candidate.ClassId != PlayerClass.Medic
                || !candidate.IsAlive
                || !candidate.IsMedicHealing
                || !candidate.MedicHealTargetId.HasValue
                || candidate.MedicHealTargetId.Value != playerId)
            {
                continue;
            }

            return candidate;
        }

        return null;
    }

    private void DrawHealerRadarHud(Vector2 cameraPosition, MouseState mouse)
    {
        if (!_healerRadarEnabled
            || !_world.LocalPlayer.IsAlive
            || _world.LocalPlayer.ClassId != PlayerClass.Medic
            || _networkClient.IsSpectator)
        {
            return;
        }

        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        var viewBounds = new Rectangle((int)cameraPosition.X, (int)cameraPosition.Y, viewportWidth, viewportHeight);
        var cornerRadians = MathF.Asin(0.6f);
        var localPlayer = _world.LocalPlayer;
        var teamTextColor = localPlayer.Team == PlayerTeam.Blue
            ? new Color(100, 116, 132)
            : new Color(171, 78, 70);

        foreach (var teammate in EnumerateRenderablePlayers())
        {
            if (ReferenceEquals(teammate, localPlayer)
                || teammate.Team != localPlayer.Team
                || !teammate.IsChatBubbleVisible
                || (teammate.ChatBubbleFrameIndex != 45 && teammate.ChatBubbleFrameIndex != 49)
                || viewBounds.Contains((int)teammate.X, (int)teammate.Y))
            {
                continue;
            }

            var bubbleAlpha = MathHelper.Clamp(teammate.ChatBubbleAlpha, 0f, 1f);
            if (bubbleAlpha <= 0f)
            {
                continue;
            }

            var theta = MathF.Atan2(localPlayer.Y - teammate.Y, teammate.X - localPlayer.X);
            if (theta < 0f)
            {
                theta += MathF.PI * 2f;
            }

            var healthRatio = teammate.Health / (float)Math.Max(1, teammate.MaxHealth);
            var arrowFrame = Math.Clamp((int)MathF.Floor(healthRatio * 19f), 0, 19);
            var defaultAlertFrame = teammate.ChatBubbleFrameIndex == 49 ? 1 : 0;
            var detailedAlertFrame = ((int)teammate.Team * 10) + (int)teammate.ClassId + 2;
            var drawX = 0f;
            var drawY = 0f;
            var hovered = false;

            if (theta <= cornerRadians || theta > (MathF.PI * 2f) - cornerRadians)
            {
                var unknown = ((viewportWidth / 2f) - (38f * MathF.Cos(theta))) * MathF.Tan(theta);
                drawX = viewportWidth - (MathF.Cos(theta) * 38f);
                drawY = (viewportHeight / 2f) - unknown;
                hovered = mouse.X > drawX - 15f
                    && mouse.Y > drawY - 15f
                    && mouse.Y < drawY + 15f;

                TryDrawScreenSprite("MedRadarArrow", arrowFrame, new Vector2(drawX, drawY), Color.White * bubbleAlpha, Vector2.One, theta);
                TryDrawScreenSprite("MedAlert", hovered ? detailedAlertFrame : defaultAlertFrame, new Vector2(drawX, drawY), Color.White * bubbleAlpha, Vector2.One);
                if (hovered)
                {
                    var textY = theta < MathF.PI ? drawY + 20f : drawY - 20f;
                    DrawBitmapFontTextRightAligned(GetHudPlayerLabel(teammate), new Vector2(viewportWidth, textY), teamTextColor * bubbleAlpha, 1f);
                }
            }
            else if (theta > cornerRadians && theta <= MathF.PI - cornerRadians)
            {
                var unknown = ((viewportHeight / 2f) - (38f * MathF.Sin(theta))) / MathF.Tan(theta);
                drawX = unknown + (viewportWidth / 2f);
                drawY = 38f * MathF.Sin(theta);
                hovered = mouse.X > drawX - 15f
                    && mouse.X < drawX + 15f
                    && mouse.Y < drawY + 15f;

                TryDrawScreenSprite("MedRadarArrow", arrowFrame, new Vector2(drawX, drawY), Color.White * bubbleAlpha, Vector2.One, theta);
                TryDrawScreenSprite("MedAlert", hovered ? detailedAlertFrame : defaultAlertFrame, new Vector2(drawX, drawY), Color.White * bubbleAlpha, Vector2.One);
                if (hovered)
                {
                    DrawBitmapFontTextCentered(GetHudPlayerLabel(teammate), new Vector2(drawX, drawY + 20f), teamTextColor * bubbleAlpha, 1f);
                }
            }
            else if (theta > MathF.PI - cornerRadians && theta <= MathF.PI + cornerRadians)
            {
                var unknown = ((viewportWidth / 2f) + (38f * MathF.Cos(theta))) * MathF.Tan(theta);
                drawX = -(38f * MathF.Cos(theta));
                drawY = unknown + (viewportHeight / 2f);
                hovered = mouse.X < drawX + 15f
                    && mouse.Y > drawY - 15f
                    && mouse.Y < drawY + 15f;

                TryDrawScreenSprite("MedRadarArrow", arrowFrame, new Vector2(drawX, drawY), Color.White * bubbleAlpha, Vector2.One, theta);
                TryDrawScreenSprite("MedAlert", hovered ? detailedAlertFrame : defaultAlertFrame, new Vector2(drawX, drawY), Color.White * bubbleAlpha, Vector2.One);
                if (hovered)
                {
                    var textY = theta < MathF.PI ? drawY + 20f : drawY - 20f;
                    DrawBitmapFontText(GetHudPlayerLabel(teammate), new Vector2(0f, textY), teamTextColor * bubbleAlpha, 1f);
                }
            }
            else
            {
                var unknown = ((viewportHeight / 2f) + (38f * MathF.Sin(theta))) / MathF.Tan(theta);
                drawX = (viewportWidth / 2f) - unknown;
                drawY = viewportHeight + (38f * MathF.Sin(theta));
                hovered = mouse.X > drawX - 13f
                    && mouse.X < drawX + 13f
                    && mouse.Y > drawY - 13f;

                TryDrawScreenSprite("MedRadarArrow", arrowFrame, new Vector2(drawX, drawY), Color.White * bubbleAlpha, Vector2.One, theta);
                TryDrawScreenSprite("MedAlert", hovered ? detailedAlertFrame : defaultAlertFrame, new Vector2(drawX, drawY), Color.White * bubbleAlpha, Vector2.One);
                if (hovered)
                {
                    DrawBitmapFontTextCentered(GetHudPlayerLabel(teammate), new Vector2(drawX, drawY - 20f), teamTextColor * bubbleAlpha, 1f);
                }
            }
        }
    }

    private void DrawSniperHud(MouseState mouse)
    {
        if (_world.LocalPlayer.ClassId != PlayerClass.Sniper || !GetPlayerIsSniperScoped(_world.LocalPlayer))
        {
            return;
        }

        var damage = GetPlayerSniperRifleDamage(_world.LocalPlayer);
        var chargeScaleX = IsFacingLeftByAim(_world.LocalPlayer) ? 1f : -1f;
        if (damage < 85)
        {
            TryDrawScreenSprite(
                "ChargeS",
                0,
                new Vector2(mouse.X + 15f * chargeScaleX, mouse.Y - 10f),
                Color.White * 0.25f,
                new Vector2(chargeScaleX, 1f));
        }
        else
        {
            TryDrawScreenSprite(
                "FullChargeS",
                0,
                new Vector2(mouse.X + 65f * chargeScaleX, mouse.Y),
                Color.White,
                Vector2.One);
        }

        var chargeWidth = (int)MathF.Ceiling(damage * 40f / 85f);
        if (chargeWidth <= 0)
        {
            return;
        }

        TryDrawScreenSpritePart(
            "ChargeS",
            1,
            new Rectangle(0, 0, chargeWidth, 20),
            new Vector2(mouse.X + 15f * chargeScaleX, mouse.Y - 10f),
            Color.White * 0.8f,
            new Vector2(chargeScaleX, 1f));
    }

    private void DrawHoveredPlayerNameHud(MouseState mouse, Vector2 cameraPosition)
    {
        var hoveredPlayer = GetHoveredPlayerForNameHud(mouse, cameraPosition);
        if (hoveredPlayer is null)
        {
            return;
        }

        var label = GetHudPlayerLabel(hoveredPlayer);
        if (string.IsNullOrWhiteSpace(label))
        {
            return;
        }

        var visibilityAlpha = GetPlayerVisibilityAlpha(hoveredPlayer);
        if (visibilityAlpha <= 0f)
        {
            return;
        }

        var renderPosition = GetRenderPosition(hoveredPlayer, allowInterpolation: !ReferenceEquals(hoveredPlayer, _world.LocalPlayer));
        var screenPosition = new Vector2(
            MathF.Round(renderPosition.X - cameraPosition.X),
            MathF.Round(renderPosition.Y - cameraPosition.Y));
        var textHeight = MeasureBitmapFontHeight(1f);
        var labelPosition = new Vector2(screenPosition.X, screenPosition.Y - 35f - textHeight);
        var teamColor = hoveredPlayer.Team == PlayerTeam.Blue
            ? new Color(80, 150, 240)
            : new Color(210, 90, 90);
        var alpha = Math.Clamp(visibilityAlpha, 0.55f, 1f);

        DrawBitmapFontTextCentered(label, labelPosition + Vector2.One, Color.Black * alpha, 1f);
        DrawBitmapFontTextCentered(label, labelPosition, teamColor * alpha, 1f);
    }

    private PlayerEntity? GetHoveredPlayerForNameHud(MouseState mouse, Vector2 cameraPosition)
    {
        const float hoverRadius = 25f;
        var bestDistanceSquared = hoverRadius * hoverRadius;
        PlayerEntity? hoveredPlayer = null;

        foreach (var player in EnumerateRenderablePlayers())
        {
            if (!player.IsAlive || GetPlayerVisibilityAlpha(player) <= 0f || IsSpyHiddenFromLocalViewer(player))
            {
                continue;
            }

            var renderPosition = GetRenderPosition(player, allowInterpolation: !ReferenceEquals(player, _world.LocalPlayer));
            var deltaX = (renderPosition.X - cameraPosition.X) - mouse.X;
            var deltaY = (renderPosition.Y - cameraPosition.Y) - mouse.Y;
            var distanceSquared = (deltaX * deltaX) + (deltaY * deltaY);
            if (distanceSquared > bestDistanceSquared)
            {
                continue;
            }

            bestDistanceSquared = distanceSquared;
            hoveredPlayer = player;
        }

        return hoveredPlayer;
    }

    private void DrawCrosshair(MouseState mouse)
    {
        var crosshair = _runtimeAssets.GetSprite("CrosshairS");
        if (crosshair is null || crosshair.Frames.Count == 0)
        {
            return;
        }

        _spriteBatch.Draw(
            crosshair.Frames[0],
            new Vector2(mouse.X, mouse.Y),
            null,
            Color.White,
            0f,
            crosshair.Origin.ToVector2(),
            Vector2.One,
            SpriteEffects.None,
            0f);
    }

    private string? GetAmmoHudSpriteName()
    {
        return _world.LocalPlayer.ClassId switch
        {
            PlayerClass.Scout => "ScattergunAmmoS",
            PlayerClass.Engineer => "ShotgunAmmoS",
            PlayerClass.Soldier => "Rocketclip",
            PlayerClass.Demoman => "MinegunAmmoS",
            PlayerClass.Spy => "RevolverAmmoS",
            PlayerClass.Medic => "NeedleAmmoS",
            PlayerClass.Pyro => "GasAmmoS",
            PlayerClass.Heavy => "MinigunAmmoS",
            PlayerClass.Quote => "BladeAmmoS",
            _ => null,
        };
    }

    private int GetAmmoHudFrameIndex()
    {
        return _world.LocalPlayer.ClassId switch
        {
            PlayerClass.Soldier => GetPlayerCurrentShells(_world.LocalPlayer) + (_world.LocalPlayerTeam == PlayerTeam.Blue ? 5 : 0),
            PlayerClass.Scout or PlayerClass.Engineer or PlayerClass.Demoman or PlayerClass.Spy or PlayerClass.Medic or PlayerClass.Pyro or PlayerClass.Heavy or PlayerClass.Quote
                => _world.LocalPlayerTeam == PlayerTeam.Blue ? 1 : 0,
            _ => 0,
        };
    }

    private void DrawAmmoReloadBar(Rectangle barRectangle)
    {
        DrawScreenHealthBar(
            barRectangle,
            GetAmmoReloadBarProgress(_world.LocalPlayer),
            1f,
            false,
            AmmoHudBarColor,
            Color.Black);
    }

    private float GetAmmoReloadBarProgress(PlayerEntity player)
    {
        var currentShells = GetPlayerCurrentShells(player);
        if (currentShells >= player.MaxShells)
        {
            return 1f;
        }

        if (player.ClassId == PlayerClass.Medic)
        {
            var refillTicks = GetPlayerMedicNeedleRefillTicks(player);
            if (refillTicks <= 0)
            {
                return currentShells < player.MaxShells ? 1f : 0f;
            }

            return Math.Clamp(
                1f - (refillTicks / (float)PlayerEntity.MedicNeedleRefillTicksDefault),
                0f,
                1f);
        }

        var reloadTicksUntilNextShell = GetPlayerReloadTicksUntilNextShell(player);
        if (reloadTicksUntilNextShell <= 0)
        {
            return 1f;
        }

        var reloadTicks = Math.Max(1, player.PrimaryWeapon.AmmoReloadTicks);
        return Math.Clamp(
            1f - (reloadTicksUntilNextShell / (float)reloadTicks),
            0f,
            1f);
    }
}
