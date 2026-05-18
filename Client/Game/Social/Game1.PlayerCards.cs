#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.ClientShared;
using OpenGarrison.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace OpenGarrison.Client;

public partial class Game1
{
    private static readonly PlayerClass[] PlayerCardClasses =
    [
        PlayerClass.Scout,
        PlayerClass.Engineer,
        PlayerClass.Pyro,
        PlayerClass.Soldier,
        PlayerClass.Demoman,
        PlayerClass.Heavy,
        PlayerClass.Sniper,
        PlayerClass.Medic,
        PlayerClass.Spy,
    ];

    private static readonly string[] PlayerCardFallbackColors =
    [
        "#263880",
        "#80403A",
        "#4E804D",
        "#80602E",
        "#6A4080",
        "#407280",
    ];

    private List<string>? _playerCardBackgroundPaths;
    private readonly Dictionary<string, LoadedSpriteFrame?> _playerCardBackgroundFrameCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Texture2D, Texture2D> _playerCardLuminosityTextureCache = new();
    private Texture2D? _playerCardColorWheelTexture;
    private bool _playerCardDraggingColorWheel;

    private readonly record struct PlayerCardLayout(
        Rectangle Bounds,
        Rectangle InnerBounds,
        Rectangle PortraitBounds,
        Rectangle PortraitInnerBounds,
        Rectangle EditButtonBounds,
        Rectangle NameBounds);

    private readonly record struct PlayerCardEditorLayout(
        Rectangle Bounds,
        Rectangle BackgroundPrevBounds,
        Rectangle BackgroundNextBounds,
        Rectangle ClassPrevBounds,
        Rectangle ClassNextBounds,
        Rectangle TeamBounds,
        Rectangle FramePrevBounds,
        Rectangle FrameNextBounds,
        Rectangle CardColor1Bounds,
        Rectangle CardColor2Bounds,
        Rectangle CardGradientBounds,
        Rectangle PortraitColor1Bounds,
        Rectangle PortraitColor2Bounds,
        Rectangle PortraitGradientBounds,
        Rectangle ColorWheelBounds,
        Rectangle BrightnessDownBounds,
        Rectangle BrightnessUpBounds,
        Rectangle ZoomOutBounds,
        Rectangle ZoomInBounds);

    private void ClosePlayerCardOverlay()
    {
        _playerCardOwnOpen = false;
        _playerCardEditorOpen = false;
        _playerCardDraggingPortrait = false;
        _playerCardDraggingColorWheel = false;
    }

    private bool TryUpdatePlayerCardOverlay(MouseState mouse, FriendsMenuLayout friendsLayout, bool leftClickPressed)
    {
        if (!_playerCardOwnOpen)
        {
            return false;
        }

        var cardLayout = GetPlayerCardLayout(friendsLayout, hoverRowBounds: null);
        var editorLayout = GetPlayerCardEditorLayout(cardLayout);
        var point = mouse.Position;

        if (_playerCardDraggingPortrait)
        {
            if (mouse.LeftButton != ButtonState.Pressed)
            {
                _playerCardDraggingPortrait = false;
                SaveCurrentPlayerCardProfile();
                return cardLayout.PortraitInnerBounds.Contains(point);
            }

            var profile = _clientIdentity.PlayerCard;
            var deltaX = mouse.X - _previousMouse.X;
            var deltaY = mouse.Y - _previousMouse.Y;
            if (deltaX != 0 || deltaY != 0)
            {
                profile.PortraitOffsetX += deltaX / Math.Max(1f, profile.PortraitZoom);
                profile.PortraitOffsetY += deltaY / Math.Max(1f, profile.PortraitZoom);
                _clientIdentity.PlayerCard = PlayerCardProfile.Sanitize(profile);
            }

            return true;
        }

        if (_playerCardDraggingColorWheel)
        {
            if (mouse.LeftButton != ButtonState.Pressed)
            {
                _playerCardDraggingColorWheel = false;
                SaveCurrentPlayerCardProfile();
                return true;
            }

            TryApplyPlayerCardColorWheelPoint(point, editorLayout);
            return true;
        }

        if (!leftClickPressed)
        {
            return false;
        }

        if (cardLayout.EditButtonBounds.Contains(point))
        {
            _playerCardEditorOpen = !_playerCardEditorOpen;
            _playerCardDraggingPortrait = false;
            _playerCardDraggingColorWheel = false;
            return true;
        }

        if (_playerCardEditorOpen && TryHandlePlayerCardEditorClick(point, cardLayout, editorLayout))
        {
            return true;
        }

        return cardLayout.Bounds.Contains(point) || (_playerCardEditorOpen && editorLayout.Bounds.Contains(point));
    }

    private bool TryHandlePlayerCardEditorClick(Point point, PlayerCardLayout cardLayout, PlayerCardEditorLayout editorLayout)
    {
        if (cardLayout.PortraitInnerBounds.Contains(point))
        {
            _playerCardDraggingPortrait = true;
            return true;
        }

        var profile = _clientIdentity.PlayerCard;
        var changed = false;

        if (editorLayout.BackgroundPrevBounds.Contains(point))
        {
            CyclePlayerCardBackground(profile, -1);
            changed = true;
        }
        else if (editorLayout.BackgroundNextBounds.Contains(point))
        {
            CyclePlayerCardBackground(profile, 1);
            changed = true;
        }
        else if (editorLayout.ClassPrevBounds.Contains(point))
        {
            CyclePlayerCardClass(profile, -1);
            changed = true;
        }
        else if (editorLayout.ClassNextBounds.Contains(point))
        {
            CyclePlayerCardClass(profile, 1);
            changed = true;
        }
        else if (editorLayout.TeamBounds.Contains(point))
        {
            profile.Team = string.Equals(profile.Team, "Red", StringComparison.OrdinalIgnoreCase) ? "Blue" : "Red";
            ClampPlayerCardFrame(profile);
            changed = true;
        }
        else if (editorLayout.FramePrevBounds.Contains(point))
        {
            CyclePlayerCardFrame(profile, -1);
            changed = true;
        }
        else if (editorLayout.FrameNextBounds.Contains(point))
        {
            CyclePlayerCardFrame(profile, 1);
            changed = true;
        }
        else if (editorLayout.CardColor1Bounds.Contains(point))
        {
            _playerCardActiveColorIndex = 0;
            return true;
        }
        else if (editorLayout.CardColor2Bounds.Contains(point))
        {
            _playerCardActiveColorIndex = 1;
            return true;
        }
        else if (editorLayout.CardGradientBounds.Contains(point))
        {
            profile.Gradient = !profile.Gradient;
            changed = true;
        }
        else if (editorLayout.PortraitColor1Bounds.Contains(point))
        {
            _playerCardActiveColorIndex = 2;
            return true;
        }
        else if (editorLayout.PortraitColor2Bounds.Contains(point))
        {
            _playerCardActiveColorIndex = 3;
            return true;
        }
        else if (editorLayout.PortraitGradientBounds.Contains(point))
        {
            profile.PortraitGradient = !profile.PortraitGradient;
            changed = true;
        }
        else if (editorLayout.ZoomOutBounds.Contains(point))
        {
            profile.PortraitZoom -= 0.25f;
            changed = true;
        }
        else if (editorLayout.ZoomInBounds.Contains(point))
        {
            profile.PortraitZoom += 0.25f;
            changed = true;
        }
        else if (editorLayout.BrightnessDownBounds.Contains(point))
        {
            AdjustPlayerCardActiveColorBrightness(profile, -10);
            changed = true;
        }
        else if (editorLayout.BrightnessUpBounds.Contains(point))
        {
            AdjustPlayerCardActiveColorBrightness(profile, 10);
            changed = true;
        }
        else if (editorLayout.ColorWheelBounds.Contains(point))
        {
            _playerCardDraggingColorWheel = true;
            TryApplyPlayerCardColorWheelPoint(point, editorLayout);
            return true;
        }

        if (changed)
        {
            _clientIdentity.PlayerCard = PlayerCardProfile.Sanitize(profile);
            SaveCurrentPlayerCardProfile();
            return true;
        }

        return editorLayout.Bounds.Contains(point);
    }

    private void DrawPlayerCardOverlay(FriendsMenuLayout friendsLayout)
    {
        if (_playerCardOwnOpen)
        {
            var cardLayout = GetPlayerCardLayout(friendsLayout, hoverRowBounds: null);
            DrawPlayerCard(cardLayout, _clientIdentity.PlayerCard, GetSocialPresenceDisplayName(), isOwnCard: true);
            if (_playerCardEditorOpen)
            {
                DrawPlayerCardEditor(cardLayout, GetPlayerCardEditorLayout(cardLayout));
            }

            return;
        }

        if (_friendsContextMenuOpen
            || (_friendsMenuTab != FriendsMenuTab.Friends && _friendsMenuTab != FriendsMenuTab.Messages)
            || _friendsMenuHoverIndex < 0
            || _friendsMenuHoverIndex >= _friendList.Friends.Count
            || _friendsMenuHoverIndex >= friendsLayout.RowBounds.Length)
        {
            return;
        }

        var friend = _friendList.Friends[_friendsMenuHoverIndex];
        _friendPresenceByCode.TryGetValue(friend.FriendCode, out var presence);
        var profile = string.IsNullOrWhiteSpace(presence?.PlayerCardJson)
            ? CreateFallbackFriendPlayerCard(friend.FriendCode)
            : PlayerCardProfile.Deserialize(presence.PlayerCardJson);
        var displayName = GetFriendDisplayName(friend, presence);
        var card = GetPlayerCardLayout(friendsLayout, friendsLayout.RowBounds[_friendsMenuHoverIndex]);
        DrawPlayerCard(card, profile, displayName, isOwnCard: false);
    }

    private void DrawPlayerCard(PlayerCardLayout layout, PlayerCardProfile sourceProfile, string displayName, bool isOwnCard)
    {
        DrawPlayerCard(
            layout,
            sourceProfile,
            displayName,
            isOwnCard ? "Edit" : string.Empty,
            isOwnCard && _playerCardEditorOpen);
    }

    private void DrawPlayerCard(PlayerCardLayout layout, PlayerCardProfile sourceProfile, string displayName, string actionLabel, bool actionActive)
    {
        var profile = PlayerCardProfile.Sanitize(sourceProfile);
        DrawRoundedRectangle(new Rectangle(layout.Bounds.X + 7, layout.Bounds.Y + 7, layout.Bounds.Width, layout.Bounds.Height), Color.Black * 0.45f, 8);
        DrawRoundedRectangleOutline(layout.Bounds, new Color(49, 43, 39), new Color(213, 205, 188), outlineThickness: 2, radius: 8);
        DrawPlayerCardGradient(layout.InnerBounds, PlayerCardColorFromHex(profile.Color1), profile.Gradient ? PlayerCardColorFromHex(profile.Color2) : PlayerCardColorFromHex(profile.Color1));
        DrawPlayerCardBackgroundArt(layout.InnerBounds, profile);

        DrawRoundedRectangle(new Rectangle(layout.PortraitBounds.X + 4, layout.PortraitBounds.Y + 4, layout.PortraitBounds.Width, layout.PortraitBounds.Height), Color.Black * 0.35f, 8);
        DrawRoundedRectangle(layout.PortraitBounds, new Color(42, 39, 36), 16);
        DrawRoundedPlayerCardGradient(
            layout.PortraitInnerBounds,
            PlayerCardColorFromHex(profile.PortraitColor1),
            profile.PortraitGradient ? PlayerCardColorFromHex(profile.PortraitColor2) : PlayerCardColorFromHex(profile.PortraitColor1),
            13);
        DrawRectangleBorder(layout.PortraitBounds, new Color(213, 205, 188), 2);
        DrawPlayerCardPortraitSprite(layout.PortraitInnerBounds, profile);

        if (!string.IsNullOrWhiteSpace(actionLabel))
        {
            DrawMenuButtonScaled(layout.EditButtonBounds, actionLabel, actionActive, 1f);
        }

        var name = string.IsNullOrWhiteSpace(displayName) ? "PLAYER" : displayName.Trim().ToUpperInvariant();
        var scale = GetPlayerCardNameScale(name, layout.NameBounds.Width - 18f);
        var textWidth = MeasureBitmapFontWidth(name, scale);
        var textPosition = new Vector2(
            layout.NameBounds.X + ((layout.NameBounds.Width - textWidth) * 0.5f),
            layout.NameBounds.Y + ((layout.NameBounds.Height - MeasureBitmapFontHeight(scale)) * 0.5f));
        DrawBitmapFontText(name, textPosition + new Vector2(3f, 3f), Color.Black * 0.55f, scale);
        DrawBitmapFontText(name, textPosition, Color.White, scale);
    }

    private void DrawPlayerCardEditor(PlayerCardLayout cardLayout, PlayerCardEditorLayout layout)
    {
        var profile = PlayerCardProfile.Sanitize(_clientIdentity.PlayerCard);
        DrawRoundedRectangle(new Rectangle(layout.Bounds.X + 6, layout.Bounds.Y + 6, layout.Bounds.Width, layout.Bounds.Height), Color.Black * 0.36f, 8);
        DrawRoundedRectangleOutline(layout.Bounds, new Color(59, 51, 46), new Color(213, 205, 188), outlineThickness: 2, radius: 8);

        DrawBitmapFontText("Edit Playercard", new Vector2(layout.Bounds.X + 16f, layout.Bounds.Y + 14f), Color.White, 1f);

        var backgroundName = Path.GetFileName(profile.Background);
        DrawBitmapFontText("Background", new Vector2(layout.Bounds.X + 16f, layout.BackgroundPrevBounds.Y - 17f), Color.White, 1f);
        DrawMenuButtonScaled(layout.BackgroundPrevBounds, "<", false, 1f);
        DrawMenuButtonScaled(layout.BackgroundNextBounds, ">", false, 1f);
        DrawBitmapFontText(TrimBitmapMenuText(backgroundName, layout.BackgroundNextBounds.X - layout.BackgroundPrevBounds.Right - 14f, 0.86f), new Vector2(layout.BackgroundPrevBounds.Right + 8f, layout.BackgroundPrevBounds.Y + 8f), Color.White, 0.86f);

        var playerClass = GetPlayerCardClass(profile.Class);
        var className = CharacterClassCatalog.GetDefinition(playerClass).DisplayName;
        DrawBitmapFontText("Class", new Vector2(layout.Bounds.X + 16f, layout.ClassPrevBounds.Y - 17f), Color.White, 1f);
        DrawMenuButtonScaled(layout.ClassPrevBounds, "<", false, 1f);
        DrawMenuButtonScaled(layout.ClassNextBounds, ">", false, 1f);
        DrawBitmapFontText(className, new Vector2(layout.ClassPrevBounds.Right + 8f, layout.ClassPrevBounds.Y + 8f), Color.White, 0.86f);

        DrawBitmapFontText("Team", new Vector2(layout.TeamBounds.X, layout.TeamBounds.Y - 17f), Color.White, 1f);
        DrawMenuButtonScaled(layout.TeamBounds, string.Equals(profile.Team, "Red", StringComparison.OrdinalIgnoreCase) ? "Red" : "Blu", false, 1f);

        DrawBitmapFontText("Frame", new Vector2(layout.FramePrevBounds.X, layout.FramePrevBounds.Y - 17f), Color.White, 1f);
        DrawMenuButtonScaled(layout.FramePrevBounds, "<", false, 1f);
        DrawMenuButtonScaled(layout.FrameNextBounds, ">", false, 1f);
        var frameCount = Math.Max(1, GetPlayerCardTauntFrameCount(profile));
        DrawBitmapFontText($"Frame {Math.Clamp(profile.Frame + 1, 1, frameCount)}/{frameCount}", new Vector2(layout.FramePrevBounds.Right + 8f, layout.FramePrevBounds.Y + 8f), Color.White, 0.86f);

        DrawBitmapFontText("Card", new Vector2(layout.CardColor1Bounds.X, layout.CardColor1Bounds.Y - 17f), Color.White, 1f);
        DrawPlayerCardColorSwatch(layout.CardColor1Bounds, PlayerCardColorFromHex(profile.Color1), _playerCardActiveColorIndex == 0);
        DrawPlayerCardColorSwatch(layout.CardColor2Bounds, PlayerCardColorFromHex(profile.Color2), _playerCardActiveColorIndex == 1);
        DrawMenuButtonScaled(layout.CardGradientBounds, "Gradient", profile.Gradient, 0.86f);

        DrawBitmapFontText("Portrait", new Vector2(layout.PortraitColor1Bounds.X, layout.PortraitColor1Bounds.Y - 17f), Color.White, 1f);
        DrawPlayerCardColorSwatch(layout.PortraitColor1Bounds, PlayerCardColorFromHex(profile.PortraitColor1), _playerCardActiveColorIndex == 2);
        DrawPlayerCardColorSwatch(layout.PortraitColor2Bounds, PlayerCardColorFromHex(profile.PortraitColor2), _playerCardActiveColorIndex == 3);
        DrawMenuButtonScaled(layout.PortraitGradientBounds, "Gradient", profile.PortraitGradient, 0.86f);

        DrawPlayerCardColorWheel(layout.ColorWheelBounds);
        DrawPlayerCardColorBrightnessControls(layout, profile);

        DrawBitmapFontText("Zoom", new Vector2(layout.ZoomOutBounds.X, layout.ZoomOutBounds.Y - 17f), Color.White, 1f);
        DrawMenuButtonScaled(layout.ZoomOutBounds, "-", false, 1f);
        DrawMenuButtonScaled(layout.ZoomInBounds, "+", false, 1f);
    }

    private PlayerCardLayout GetPlayerCardLayout(FriendsMenuLayout friendsLayout, Rectangle? hoverRowBounds)
    {
        var leftSpace = Math.Max(320, friendsLayout.Panel.X - 28);
        var baseWidth = Math.Clamp(leftSpace, 320, 500);
        var width = Math.Clamp((int)MathF.Round(baseWidth * GetPlayerCardSizeScale()), 210, baseWidth);
        var height = (int)MathF.Round(width * 0.61f);
        var x = Math.Max(12, friendsLayout.Panel.X - width - 18);
        var preferredY = hoverRowBounds.HasValue
            ? hoverRowBounds.Value.Center.Y - (height / 2)
            : friendsLayout.Panel.Y + 10;
        var y = Math.Clamp(preferredY, 12, Math.Max(12, ViewportHeight - height - 16));
        return CreatePlayerCardLayout(new Rectangle(x, y, width, height));
    }

    private PlayerCardEditorLayout GetPlayerCardEditorLayout(PlayerCardLayout cardLayout)
    {
        const int padding = 16;
        var width = Math.Max(320, cardLayout.Bounds.Width);
        var height = 352;
        var y = cardLayout.Bounds.Bottom + 10;
        if (y + height > ViewportHeight - 12)
        {
            y = Math.Max(12, cardLayout.Bounds.Y - height - 10);
        }

        var x = Math.Clamp(cardLayout.Bounds.Right - width, 12, Math.Max(12, ViewportWidth - width - 12));
        var bounds = new Rectangle(x, y, width, height);
        var buttonHeight = 30;
        var smallButton = 38;
        var rightColumnWidth = Math.Clamp(width / 4, 86, 116);
        var rightX = bounds.Right - padding - rightColumnWidth;
        var leftRightLimit = rightX - 12;
        var rowY = bounds.Y + 52;
        var backgroundPrev = new Rectangle(bounds.X + padding, rowY, smallButton, buttonHeight);
        var backgroundNext = new Rectangle(leftRightLimit - smallButton, rowY, smallButton, buttonHeight);
        var team = new Rectangle(rightX, rowY, rightColumnWidth, buttonHeight);

        rowY += 48;
        var classPrev = new Rectangle(bounds.X + padding, rowY, smallButton, buttonHeight);
        var classNext = new Rectangle(Math.Min(bounds.X + 150, leftRightLimit - smallButton), rowY, smallButton, buttonHeight);
        var zoomOut = new Rectangle(rightX, rowY, 36, buttonHeight);
        var zoomIn = new Rectangle(rightX + 44, rowY, 40, buttonHeight);

        rowY += 42;
        var framePrev = new Rectangle(bounds.X + padding, rowY, smallButton, buttonHeight);
        var frameNext = new Rectangle(Math.Min(bounds.X + 150, leftRightLimit - smallButton), rowY, smallButton, buttonHeight);

        var colorWheelSize = Math.Clamp(rightColumnWidth, 82, 96);
        var colorWheel = new Rectangle(bounds.Right - padding - colorWheelSize, rowY + 50, colorWheelSize, colorWheelSize);
        var brightnessY = colorWheel.Bottom + 8;
        var brightnessButtonWidth = 34;
        var brightnessDown = new Rectangle(colorWheel.X, brightnessY, brightnessButtonWidth, 28);
        var brightnessUp = new Rectangle(colorWheel.Right - brightnessButtonWidth, brightnessY, brightnessButtonWidth, 28);

        var cardColorY = rowY + 66;
        var cardColor1 = new Rectangle(bounds.X + padding, cardColorY, 34, 34);
        var cardColor2 = new Rectangle(cardColor1.Right + 8, cardColorY, 34, 34);
        var gradientWidth = Math.Clamp(leftRightLimit - cardColor2.Right - 18, 72, 92);
        var cardGradient = new Rectangle(cardColor2.Right + 10, cardColorY, gradientWidth, 34);
        var portraitColorY = cardColorY + 52;
        var portraitColor1 = new Rectangle(bounds.X + padding, portraitColorY, 34, 34);
        var portraitColor2 = new Rectangle(portraitColor1.Right + 8, portraitColorY, 34, 34);
        var portraitGradient = new Rectangle(portraitColor2.Right + 10, portraitColorY, gradientWidth, 34);

        return new PlayerCardEditorLayout(
            bounds,
            backgroundPrev,
            backgroundNext,
            classPrev,
            classNext,
            team,
            framePrev,
            frameNext,
            cardColor1,
            cardColor2,
            cardGradient,
            portraitColor1,
            portraitColor2,
            portraitGradient,
            colorWheel,
            brightnessDown,
            brightnessUp,
            zoomOut,
            zoomIn);
    }

    private static PlayerCardLayout CreatePlayerCardLayout(Rectangle bounds)
    {
        var inner = new Rectangle(bounds.X + 5, bounds.Y + 5, bounds.Width - 10, bounds.Height - 10);
        var margin = Math.Clamp((int)MathF.Round(bounds.Width * 0.052f), 11, 24);
        var portraitSize = Math.Clamp((int)MathF.Round(bounds.Width * 0.26f), 58, 156);
        var portrait = new Rectangle(bounds.X + margin, bounds.Y + margin, portraitSize, portraitSize);
        var portraitInset = Math.Clamp((int)MathF.Round(bounds.Width * 0.012f), 2, 3);
        var portraitInner = new Rectangle(
            portrait.X + portraitInset,
            portrait.Y + portraitInset,
            portrait.Width - (portraitInset * 2),
            portrait.Height - (portraitInset * 2));
        var actionGap = Math.Clamp((int)MathF.Round(bounds.Width * 0.022f), 5, 8);
        var actionHeight = Math.Clamp((int)MathF.Round(bounds.Width * 0.075f), 22, 32);
        var edit = new Rectangle(portrait.X, portrait.Bottom + actionGap, portrait.Width, actionHeight);
        var nameHeight = Math.Clamp((int)MathF.Round(bounds.Width * 0.108f), 28, 46);
        var nameBottomMargin = Math.Clamp((int)MathF.Round(bounds.Width * 0.036f), 8, 16);
        var name = new Rectangle(bounds.X + margin, bounds.Bottom - nameBottomMargin - nameHeight, bounds.Width - (margin * 2), nameHeight);
        return new PlayerCardLayout(bounds, inner, portrait, portraitInner, edit, name);
    }

    private void DrawPlayerCardBackgroundArt(Rectangle bounds, PlayerCardProfile profile)
    {
        var texture = TryGetPlayerCardBackgroundTexture(profile.Background);
        if (texture is null)
        {
            return;
        }

        var drawTexture = OperatingSystem.IsBrowser() ? texture : GetPlayerCardLuminosityTexture(texture);
        var source = GetTopAlignedCoverSourceRectangle(drawTexture.Width, drawTexture.Height, bounds.Width, bounds.Height);
        _spriteBatch.Draw(drawTexture, bounds, source, Color.White * 0.15f);
    }

    private void DrawPlayerCardPortraitSprite(Rectangle portraitBounds, PlayerCardProfile profile)
    {
        var sprite = GetResolvedSprite(GetPlayerCardTauntSpriteName(profile));
        if (sprite is null || sprite.Frames.Count == 0)
        {
            return;
        }

        var frameIndex = Math.Clamp(profile.Frame, 0, sprite.Frames.Count - 1);
        var frame = sprite.Frames[frameIndex];
        var scale = new Vector2(profile.PortraitZoom, profile.PortraitZoom);
        var origin = new Vector2(frame.Width / 2f, frame.Height / 2f);
        var position = new Vector2(portraitBounds.Center.X + (profile.PortraitOffsetX * profile.PortraitZoom), portraitBounds.Center.Y + (profile.PortraitOffsetY * profile.PortraitZoom));

        DrawPlayerCardClipped(portraitBounds, () =>
        {
            DrawLoadedSpriteFrame(frame, position + new Vector2(3f, 3f), null, Color.Black * 0.42f, 0f, origin, scale, SpriteEffects.None, 0f);
            DrawLoadedSpriteFrame(frame, position, null, Color.White, 0f, origin, scale, SpriteEffects.None, 0f);
        });
    }

    private void DrawPlayerCardClipped(Rectangle clipBounds, Action draw)
    {
        _spriteBatch.End();
        var previousScissor = GraphicsDevice.ScissorRectangle;
        using var scissorRasterizer = new RasterizerState
        {
            CullMode = CullMode.None,
            ScissorTestEnable = true,
        };

        GraphicsDevice.ScissorRectangle = clipBounds;
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp, rasterizerState: scissorRasterizer);
        draw();
        _spriteBatch.End();
        GraphicsDevice.ScissorRectangle = previousScissor;
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp, rasterizerState: RasterizerState.CullNone);
    }

    private void DrawPlayerCardGradient(Rectangle bounds, Color left, Color right)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        for (var column = 0; column < bounds.Width; column += 1)
        {
            var amount = bounds.Width <= 1 ? 0f : column / (float)(bounds.Width - 1);
            var color = Color.Lerp(left, right, amount);
            _spriteBatch.Draw(_pixel, new Rectangle(bounds.X + column, bounds.Y, 1, bounds.Height), color);
        }
    }

    private void DrawRoundedPlayerCardGradient(Rectangle bounds, Color left, Color right, int radius)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        radius = Math.Clamp(radius, 0, Math.Min(bounds.Width, bounds.Height) / 2);
        var radiusSquared = radius * radius;
        for (var x = 0; x < bounds.Width; x += 1)
        {
            float inset;
            if (x < radius)
            {
                var dx = radius - x - 0.5f;
                inset = MathF.Round(radius - MathF.Sqrt(MathF.Max(0f, radiusSquared - (dx * dx))));
            }
            else if (x >= bounds.Width - radius)
            {
                var dx = x - (bounds.Width - radius) + 0.5f;
                inset = MathF.Round(radius - MathF.Sqrt(MathF.Max(0f, radiusSquared - (dx * dx))));
            }
            else
            {
                inset = 0f;
            }

            var drawY = bounds.Y + (int)inset;
            var drawHeight = bounds.Height - ((int)inset * 2);
            if (drawHeight > 0)
            {
                var amount = bounds.Width <= 1 ? 0f : x / (float)(bounds.Width - 1);
                _spriteBatch.Draw(_pixel, new Rectangle(bounds.X + x, drawY, 1, drawHeight), Color.Lerp(left, right, amount));
            }
        }
    }

    private void DrawPlayerCardColorSwatch(Rectangle bounds, Color color, bool selected)
    {
        DrawRoundedRectangleOutline(bounds, color, selected ? new Color(255, 242, 190) : new Color(213, 205, 188), outlineThickness: 2, radius: 6);
    }

    private void DrawPlayerCardColorWheel(Rectangle bounds)
    {
        _playerCardColorWheelTexture ??= CreatePlayerCardColorWheelTexture(96);
        _spriteBatch.Draw(_playerCardColorWheelTexture, bounds, Color.White);
        DrawRectangleBorder(bounds, new Color(213, 205, 188), 1);
    }

    private void DrawPlayerCardColorBrightnessControls(PlayerCardEditorLayout layout, PlayerCardProfile profile)
    {
        DrawMenuButtonScaled(layout.BrightnessDownBounds, "-", false, 1f);
        DrawMenuButtonScaled(layout.BrightnessUpBounds, "+", false, 1f);

        var brightnessText = $"{GetPlayerCardActiveBrightnessPercent(profile)}%";
        var textScale = 0.86f;
        var textWidth = MeasureBitmapFontWidth(brightnessText, textScale);
        var textX = layout.BrightnessDownBounds.Right
            + ((layout.BrightnessUpBounds.X - layout.BrightnessDownBounds.Right - textWidth) * 0.5f);
        var textY = layout.BrightnessDownBounds.Y
            + ((layout.BrightnessDownBounds.Height - MeasureBitmapFontHeight(textScale)) * 0.5f);
        DrawBitmapFontText(brightnessText, new Vector2(textX, textY), Color.White, textScale);
    }

    private Texture2D CreatePlayerCardColorWheelTexture(int size)
    {
        var texture = new Texture2D(GraphicsDevice, size, size);
        var pixels = new Color[size * size];
        var center = (size - 1) * 0.5f;
        var radius = center - 1f;
        for (var y = 0; y < size; y += 1)
        {
            for (var x = 0; x < size; x += 1)
            {
                var dx = x - center;
                var dy = y - center;
                var distance = MathF.Sqrt((dx * dx) + (dy * dy));
                if (distance > radius)
                {
                    pixels[(y * size) + x] = Color.Transparent;
                    continue;
                }

                var hue = (MathF.Atan2(dy, dx) / (MathF.PI * 2f)) + 0.5f;
                var saturation = Math.Clamp(distance / radius, 0f, 1f);
                pixels[(y * size) + x] = ColorFromHsv(hue, saturation, 1f);
            }
        }

        texture.SetData(pixels);
        return texture;
    }

    private bool TryApplyPlayerCardColorWheelPoint(Point point, PlayerCardEditorLayout editorLayout)
    {
        var profile = PlayerCardProfile.Sanitize(_clientIdentity.PlayerCard);
        if (!TryPickPlayerCardColor(point, editorLayout.ColorWheelBounds, GetPlayerCardActiveBrightnessPercent(profile) / 100f, out var pickedColor))
        {
            return false;
        }

        SetPlayerCardActiveColor(profile, pickedColor);
        _clientIdentity.PlayerCard = PlayerCardProfile.Sanitize(profile);
        return true;
    }

    private static bool TryPickPlayerCardColor(Point point, Rectangle bounds, float brightness, out Color color)
    {
        color = default;
        if (!bounds.Contains(point))
        {
            return false;
        }

        var centerX = bounds.X + ((bounds.Width - 1) * 0.5f);
        var centerY = bounds.Y + ((bounds.Height - 1) * 0.5f);
        var dx = point.X - centerX;
        var dy = point.Y - centerY;
        var radius = (Math.Min(bounds.Width, bounds.Height) - 1) * 0.5f;
        var distance = MathF.Sqrt((dx * dx) + (dy * dy));
        if (distance > radius)
        {
            return false;
        }

        var hue = (MathF.Atan2(dy, dx) / (MathF.PI * 2f)) + 0.5f;
        var saturation = Math.Clamp(distance / Math.Max(1f, radius), 0f, 1f);
        color = ColorFromHsv(hue, saturation, Math.Clamp(brightness, 0.1f, 1f));
        return true;
    }

    private void AdjustPlayerCardActiveColorBrightness(PlayerCardProfile profile, int deltaPercent)
    {
        var currentColor = GetPlayerCardActiveColor(profile);
        ColorToHsv(currentColor, out var hue, out var saturation, out _);
        var nextPercent = Math.Clamp(GetPlayerCardBrightnessPercent(currentColor) + deltaPercent, 10, 100);
        SetPlayerCardActiveColor(profile, ColorFromHsv(hue, saturation, nextPercent / 100f));
    }

    private Color GetPlayerCardActiveColor(PlayerCardProfile profile)
    {
        return _playerCardActiveColorIndex switch
        {
            1 => PlayerCardColorFromHex(profile.Color2),
            2 => PlayerCardColorFromHex(profile.PortraitColor1),
            3 => PlayerCardColorFromHex(profile.PortraitColor2),
            _ => PlayerCardColorFromHex(profile.Color1),
        };
    }

    private void SetPlayerCardActiveColor(PlayerCardProfile profile, Color color)
    {
        var hex = PlayerCardColorToHex(color);
        if (_playerCardActiveColorIndex == 1)
        {
            profile.Color2 = hex;
        }
        else if (_playerCardActiveColorIndex == 2)
        {
            profile.PortraitColor1 = hex;
        }
        else if (_playerCardActiveColorIndex == 3)
        {
            profile.PortraitColor2 = hex;
        }
        else
        {
            profile.Color1 = hex;
        }
    }

    private int GetPlayerCardActiveBrightnessPercent(PlayerCardProfile profile)
    {
        return GetPlayerCardBrightnessPercent(GetPlayerCardActiveColor(profile));
    }

    private static int GetPlayerCardBrightnessPercent(Color color)
    {
        var value = Math.Max(color.R, Math.Max(color.G, color.B)) / 255f;
        return Math.Clamp((int)MathF.Round(value * 10f) * 10, 10, 100);
    }

    private Texture2D GetPlayerCardLuminosityTexture(Texture2D source)
    {
        if (_playerCardLuminosityTextureCache.TryGetValue(source, out var cached))
        {
            return cached;
        }

        var pixels = new Color[source.Width * source.Height];
        source.GetData(pixels);
        for (var index = 0; index < pixels.Length; index += 1)
        {
            var pixel = pixels[index];
            var luminance = (byte)Math.Clamp((int)MathF.Round((pixel.R * 0.2126f) + (pixel.G * 0.7152f) + (pixel.B * 0.0722f)), 0, 255);
            pixels[index] = new Color(luminance, luminance, luminance, pixel.A);
        }

        cached = new Texture2D(GraphicsDevice, source.Width, source.Height);
        cached.SetData(pixels);
        _playerCardLuminosityTextureCache[source] = cached;
        return cached;
    }

    private Texture2D? TryGetPlayerCardBackgroundTexture(string backgroundName)
    {
        var path = ResolvePlayerCardBackgroundPath(backgroundName);
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (!_playerCardBackgroundFrameCache.TryGetValue(path, out var frame))
        {
            frame = LoadSpriteFrameFromPath(path);
            _playerCardBackgroundFrameCache[path] = frame;
        }

        return frame?.Texture;
    }

    private string ResolvePlayerCardBackgroundPath(string backgroundName)
    {
        var backgrounds = GetPlayerCardBackgroundPaths();
        if (backgrounds.Count == 0)
        {
            return string.Empty;
        }

        var match = backgrounds.FirstOrDefault(path => string.Equals(Path.GetFileName(path), backgroundName, StringComparison.OrdinalIgnoreCase));
        return match ?? backgrounds[0];
    }

    private List<string> GetPlayerCardBackgroundPaths()
    {
        if (_playerCardBackgroundPaths is not null)
        {
            return _playerCardBackgroundPaths;
        }

        var paths = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in EnumeratePlayerCardProbeRoots())
        {
            AddPlayerCardBackgroundDirectory(Path.Combine(root, "Plugins", "Client", "Lua.RandomBackgrounds", "Resources", "PrOF", "Backgrounds"), paths, seen);
            AddPlayerCardBackgroundDirectory(Path.Combine(root, "Plugins", "Packaged", "Client", "Lua.RandomBackgrounds", "Resources", "PrOF", "Backgrounds"), paths, seen);
            AddPlayerCardBackgroundDirectory(Path.Combine(root, "Plugins", "Client", "OpenGarrison.Client.Plugins.RandomBackgrounds", "Resources", "PrOF", "Backgrounds"), paths, seen);
        }

        _playerCardBackgroundPaths = paths
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .ToList();
        return _playerCardBackgroundPaths;
    }

    private static IEnumerable<string> EnumeratePlayerCardProbeRoots()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var start in new[] { RuntimePaths.ApplicationRoot, AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            if (string.IsNullOrWhiteSpace(start))
            {
                continue;
            }

            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                if (seen.Add(directory.FullName))
                {
                    yield return directory.FullName;
                }

                directory = directory.Parent;
            }
        }
    }

    private static void AddPlayerCardBackgroundDirectory(string directory, List<string> paths, HashSet<string> seen)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(directory, "*.png", SearchOption.TopDirectoryOnly))
        {
            var fileName = Path.GetFileName(path);
            if (!string.IsNullOrWhiteSpace(fileName) && seen.Add(fileName))
            {
                paths.Add(path);
            }
        }
    }

    private PlayerCardProfile CreateFallbackFriendPlayerCard(string friendCode)
    {
        return CreateFallbackPlayerCard(friendCode);
    }

    private PlayerCardProfile CreateFallbackPlayerCard(string seedText)
    {
        var seed = 0;
        foreach (var character in seedText)
        {
            seed = unchecked((seed * 31) + character);
        }

        var positive = seed == int.MinValue ? 0 : Math.Abs(seed);
        var backgrounds = GetPlayerCardBackgroundPaths();
        var profile = PlayerCardProfile.CreateDefault();
        if (backgrounds.Count > 0)
        {
            profile.Background = Path.GetFileName(backgrounds[positive % backgrounds.Count]);
        }

        var playerClass = PlayerCardClasses[positive % PlayerCardClasses.Length];
        profile.Class = playerClass.ToString();
        profile.Team = (positive & 1) == 0 ? "Blue" : "Red";
        profile.Frame = positive % Math.Max(1, GetPlayerCardTauntFrameCount(profile));
        profile.Color1 = PlayerCardFallbackColors[positive % PlayerCardFallbackColors.Length];
        profile.Color2 = PlayerCardFallbackColors[(positive / 7) % PlayerCardFallbackColors.Length];
        profile.Gradient = true;
        profile.PortraitColor1 = PlayerCardFallbackColors[(positive / 11) % PlayerCardFallbackColors.Length];
        profile.PortraitColor2 = PlayerCardFallbackColors[(positive / 17) % PlayerCardFallbackColors.Length];
        profile.PortraitGradient = true;
        return PlayerCardProfile.Sanitize(profile);
    }

    private void CyclePlayerCardBackground(PlayerCardProfile profile, int direction)
    {
        var backgrounds = GetPlayerCardBackgroundPaths();
        if (backgrounds.Count == 0)
        {
            return;
        }

        var currentIndex = -1;
        for (var index = 0; index < backgrounds.Count; index += 1)
        {
            if (string.Equals(Path.GetFileName(backgrounds[index]), profile.Background, StringComparison.OrdinalIgnoreCase))
            {
                currentIndex = index;
                break;
            }
        }
        if (currentIndex < 0)
        {
            currentIndex = 0;
        }

        var nextIndex = PositiveModulo(currentIndex + direction, backgrounds.Count);
        profile.Background = Path.GetFileName(backgrounds[nextIndex]);
    }

    private void CyclePlayerCardClass(PlayerCardProfile profile, int direction)
    {
        var currentClass = GetPlayerCardClass(profile.Class);
        var currentIndex = Array.IndexOf(PlayerCardClasses, currentClass);
        if (currentIndex < 0)
        {
            currentIndex = 0;
        }

        var nextIndex = PositiveModulo(currentIndex + direction, PlayerCardClasses.Length);
        profile.Class = PlayerCardClasses[nextIndex].ToString();
        profile.Frame = 0;
        ClampPlayerCardFrame(profile);
    }

    private void CyclePlayerCardFrame(PlayerCardProfile profile, int direction)
    {
        var frameCount = Math.Max(1, GetPlayerCardTauntFrameCount(profile));
        profile.Frame = PositiveModulo(profile.Frame + direction, frameCount);
    }

    private void ClampPlayerCardFrame(PlayerCardProfile profile)
    {
        profile.Frame = Math.Clamp(profile.Frame, 0, Math.Max(0, GetPlayerCardTauntFrameCount(profile) - 1));
    }

    private int GetPlayerCardTauntFrameCount(PlayerCardProfile profile)
    {
        var sprite = GetResolvedSprite(GetPlayerCardTauntSpriteName(profile));
        return Math.Max(1, sprite?.Frames.Count ?? 1);
    }

    private static string GetPlayerCardTauntSpriteName(PlayerCardProfile profile)
    {
        var playerClass = GetPlayerCardClass(profile.Class);
        var className = playerClass == PlayerClass.Quote ? "Querly" : playerClass.ToString();
        var teamName = string.Equals(profile.Team, "Red", StringComparison.OrdinalIgnoreCase) ? "Red" : "Blue";
        return $"{className}{teamName}TauntS";
    }

    private static PlayerClass GetPlayerCardClass(string className)
    {
        return Enum.TryParse<PlayerClass>(className, ignoreCase: true, out var playerClass) && PlayerCardClasses.Contains(playerClass)
            ? playerClass
            : PlayerClass.Spy;
    }

    private void SaveCurrentPlayerCardProfile()
    {
        _clientIdentity.PlayerCard = PlayerCardProfile.Sanitize(_clientIdentity.PlayerCard);
        _clientIdentity.Save();
        _lastSocialPresenceSignature = string.Empty;
        _socialPresenceSecondsUntilHeartbeat = 0d;
        _networkClient.UpdatePlayerProfile(
            _world.LocalPlayer.DisplayName,
            _world.LocalPlayer.BadgeMask,
            _clientIdentity.FriendCode,
            PlayerCardProfile.Serialize(_clientIdentity.PlayerCard));
    }

    private static Rectangle GetTopAlignedCoverSourceRectangle(int sourceWidth, int sourceHeight, int destinationWidth, int destinationHeight)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0 || destinationWidth <= 0 || destinationHeight <= 0)
        {
            return Rectangle.Empty;
        }

        var sourceAspect = sourceWidth / (float)sourceHeight;
        var destinationAspect = destinationWidth / (float)destinationHeight;
        if (sourceAspect > destinationAspect)
        {
            var width = (int)MathF.Round(sourceHeight * destinationAspect);
            return new Rectangle((sourceWidth - width) / 2, 0, width, sourceHeight);
        }

        var height = (int)MathF.Round(sourceWidth / destinationAspect);
        return new Rectangle(0, 0, sourceWidth, height);
    }

    private static Color ColorFromHsv(float hue, float saturation, float value)
    {
        hue = hue - MathF.Floor(hue);
        saturation = Math.Clamp(saturation, 0f, 1f);
        value = Math.Clamp(value, 0f, 1f);

        var scaled = hue * 6f;
        var sector = (int)MathF.Floor(scaled);
        var fraction = scaled - sector;
        var p = value * (1f - saturation);
        var q = value * (1f - (fraction * saturation));
        var t = value * (1f - ((1f - fraction) * saturation));

        var (r, g, b) = sector switch
        {
            0 => (value, t, p),
            1 => (q, value, p),
            2 => (p, value, t),
            3 => (p, q, value),
            4 => (t, p, value),
            _ => (value, p, q),
        };

        return new Color(r, g, b);
    }

    private static void ColorToHsv(Color color, out float hue, out float saturation, out float value)
    {
        var r = color.R / 255f;
        var g = color.G / 255f;
        var b = color.B / 255f;
        var max = MathF.Max(r, MathF.Max(g, b));
        var min = MathF.Min(r, MathF.Min(g, b));
        var delta = max - min;

        value = max;
        saturation = max <= 0f ? 0f : delta / max;
        if (delta <= 0f)
        {
            hue = 0f;
            return;
        }

        if (Math.Abs(max - r) <= float.Epsilon)
        {
            hue = ((g - b) / delta) % 6f;
        }
        else if (Math.Abs(max - g) <= float.Epsilon)
        {
            hue = ((b - r) / delta) + 2f;
        }
        else
        {
            hue = ((r - g) / delta) + 4f;
        }

        hue /= 6f;
        if (hue < 0f)
        {
            hue += 1f;
        }
    }

    private static Color PlayerCardColorFromHex(string value)
    {
        var text = string.IsNullOrWhiteSpace(value) ? "#263880" : value.Trim();
        if (text.Length == 7 && text[0] == '#'
            && byte.TryParse(text.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r)
            && byte.TryParse(text.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g)
            && byte.TryParse(text.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
        {
            return new Color(r, g, b);
        }

        return new Color(38, 56, 128);
    }

    private static string PlayerCardColorToHex(Color color)
    {
        return FormattableString.Invariant($"#{color.R:X2}{color.G:X2}{color.B:X2}");
    }

    private float GetPlayerCardNameScale(string text, float maxWidth)
    {
        var scale = 2.5f;
        while (scale > 1f && MeasureBitmapFontWidth(text, scale) > maxWidth)
        {
            scale -= 0.1f;
        }

        return scale;
    }

    private static int PositiveModulo(int value, int divisor)
    {
        if (divisor <= 0)
        {
            return 0;
        }

        var result = value % divisor;
        return result < 0 ? result + divisor : result;
    }
}
