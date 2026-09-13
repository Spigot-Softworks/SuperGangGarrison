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
    private const int PlayerCardNativeWidth = 276;
    private const int PlayerCardNativeHeight = 116;
    internal const float PlayerCardTextScale = PixelPerfectTextLayout.NaturalScale;

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

    private readonly record struct PlayerCardMedalDefinition(string Id, string DisplayName, string FileName);

    private static readonly PlayerCardMedalDefinition[] PlayerCardMedals =
    [
        new("shining-hero", "Shining Hero", "Shining Hero.png"),
        new("bloodsoaked-batallion", "Bloodsoaked Batallion", "Bloodsoaked Batallion.png"),
        new("keyboard-warrior", "Keyboard Warrior", "Keyboard Warrior.png"),
        new("mercenary", "Mercenary", "Mercenary.png"),
        new("the-legendary", "The Legendary", "The Legendary.png"),
        new("time-tested-veteran", "Time-Tested Veteran", "Time-Tested Veteran.png"),
    ];

    private Texture2D? _playerCardColorWheelTexture;
    private bool _playerCardDraggingColorWheel;
    private readonly Dictionary<string, LoadedSpriteFrame?> _playerCardAssetFrameCache = new(StringComparer.OrdinalIgnoreCase);
    private bool _editingPlayerCardBio;
    private string _playerCardBioInputBuffer = string.Empty;
    private int _playerCardBioCursorIndex;
    private int _playerCardBioSelectionStart;

    private readonly record struct PlayerCardLayout(
        Rectangle Bounds,
        Rectangle PortraitInnerBounds,
        Rectangle EditButtonBounds,
        Rectangle NameBounds,
        Rectangle BioBounds,
        Rectangle MedalBounds);

    private readonly record struct PlayerCardEditorLayout(
        Rectangle Bounds,
        PlayerCardLayout PreviewCard,
        Rectangle BioInputBounds,
        Rectangle MedalPrevBounds,
        Rectangle MedalNextBounds,
        Rectangle ClassPrevBounds,
        Rectangle ClassNextBounds,
        Rectangle TeamBounds,
        Rectangle FramePrevBounds,
        Rectangle FrameNextBounds,
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
        CommitPlayerCardBioEdit();
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
        var portraitBounds = _playerCardEditorOpen
            ? editorLayout.PreviewCard.PortraitInnerBounds
            : cardLayout.PortraitInnerBounds;
        var point = mouse.Position;

        if (_playerCardDraggingPortrait)
        {
            if (mouse.LeftButton != ButtonState.Pressed)
            {
                _playerCardDraggingPortrait = false;
                SaveCurrentPlayerCardProfile();
                return portraitBounds.Contains(point);
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

        if (!_playerCardEditorOpen && cardLayout.EditButtonBounds.Contains(point))
        {
            CommitPlayerCardBioEdit();
            _playerCardEditorOpen = !_playerCardEditorOpen;
            _playerCardDraggingPortrait = false;
            _playerCardDraggingColorWheel = false;
            return true;
        }

        if (_playerCardEditorOpen && TryHandlePlayerCardEditorClick(point, editorLayout))
        {
            return true;
        }

        return cardLayout.Bounds.Contains(point) || (_playerCardEditorOpen && editorLayout.Bounds.Contains(point));
    }

    private bool TryHandlePlayerCardEditorClick(Point point, PlayerCardEditorLayout editorLayout)
    {
        if (editorLayout.BioInputBounds.Contains(point))
        {
            BeginPlayerCardBioEdit();
            return true;
        }

        CommitPlayerCardBioEdit();

        if (editorLayout.PreviewCard.PortraitInnerBounds.Contains(point))
        {
            _playerCardDraggingPortrait = true;
            return true;
        }

        var profile = _clientIdentity.PlayerCard;
        var changed = false;

        if (editorLayout.MedalPrevBounds.Contains(point))
        {
            CyclePlayerCardMedal(profile, -1);
            changed = true;
        }
        else if (editorLayout.MedalNextBounds.Contains(point))
        {
            CyclePlayerCardMedal(profile, 1);
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
        else if (editorLayout.PortraitColor1Bounds.Contains(point))
        {
            _playerCardActiveColorIndex = 0;
            return true;
        }
        else if (editorLayout.PortraitColor2Bounds.Contains(point))
        {
            _playerCardActiveColorIndex = 1;
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

    private void BeginPlayerCardBioEdit()
    {
        if (_editingPlayerCardBio)
        {
            return;
        }

        _playerCardBioInputBuffer = PlayerCardProfile.Sanitize(_clientIdentity.PlayerCard).Bio;
        _playerCardBioCursorIndex = _playerCardBioInputBuffer.Length;
        _playerCardBioSelectionStart = _playerCardBioCursorIndex;
        _editingPlayerCardBio = true;
    }

    private void CommitPlayerCardBioEdit()
    {
        if (!_editingPlayerCardBio)
        {
            return;
        }

        _editingPlayerCardBio = false;
        _clientIdentity.PlayerCard.Bio = _playerCardBioInputBuffer;
        SaveCurrentPlayerCardProfile();
    }

    private bool TryHandlePlayerCardBioTextInput(char character)
    {
        if (!_editingPlayerCardBio)
        {
            return false;
        }

        switch (character)
        {
            case '\b':
            {
                var result = DeleteTextSelectionOrBackspace(
                    _playerCardBioInputBuffer,
                    _playerCardBioCursorIndex,
                    _playerCardBioSelectionStart);
                _playerCardBioInputBuffer = result.Text;
                _playerCardBioCursorIndex = result.CursorIndex;
                _playerCardBioSelectionStart = result.SelectionStart;
                break;
            }
            case '\r':
            case '\n':
                CommitPlayerCardBioEdit();
                break;
            default:
                if (!char.IsControl(character) && character != '"')
                {
                    var result = InsertTextCharacterAtCursor(
                        _playerCardBioInputBuffer,
                        character,
                        _playerCardBioCursorIndex,
                        _playerCardBioSelectionStart,
                        PlayerCardProfile.MaximumBioLength);
                    _playerCardBioInputBuffer = result.Text;
                    _playerCardBioCursorIndex = result.CursorIndex;
                    _playerCardBioSelectionStart = result.SelectionStart;
                    _clientIdentity.PlayerCard.Bio = _playerCardBioInputBuffer;
                }
                break;
        }

        return true;
    }

    private void DrawPlayerCardOverlay(FriendsMenuLayout friendsLayout)
    {
        if (_playerCardOwnOpen)
        {
            var cardLayout = GetPlayerCardLayout(friendsLayout, hoverRowBounds: null);
            if (_playerCardEditorOpen)
            {
                DrawPlayerCardEditor(GetPlayerCardEditorLayout(cardLayout));
            }
            else
            {
                DrawPlayerCard(cardLayout, _clientIdentity.PlayerCard, GetSocialPresenceDisplayName(), isOwnCard: true);
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

    private void DrawPlayerCard(
        PlayerCardLayout layout,
        PlayerCardProfile sourceProfile,
        string displayName,
        bool isOwnCard,
        PlayerTeam? teamOverride = null)
    {
        DrawPlayerCard(
            layout,
            sourceProfile,
            displayName,
            isOwnCard ? "Edit Card" : string.Empty,
            isOwnCard && _playerCardEditorOpen,
            teamOverride);
    }

    private void DrawPlayerCard(
        PlayerCardLayout layout,
        PlayerCardProfile sourceProfile,
        string displayName,
        string actionLabel,
        bool actionActive,
        PlayerTeam? teamOverride = null)
    {
        var profile = PlayerCardProfile.Sanitize(sourceProfile);
        var team = teamOverride ?? GetPlayerCardTeam(profile);
        var template = GetPlayerCardTemplateFrame(team);
        if (template is not null)
        {
            DrawLoadedSpriteFrame(template, layout.Bounds, Color.White);
        }
        else
        {
            DrawRoundedRectangleOutline(layout.Bounds, new Color(122, 130, 132), new Color(216, 224, 226), outlineThickness: 2, radius: 7);
        }

        DrawPlayerCardGradient(
            layout.PortraitInnerBounds,
            PlayerCardColorFromHex(profile.PortraitColor1),
            profile.PortraitGradient ? PlayerCardColorFromHex(profile.PortraitColor2) : PlayerCardColorFromHex(profile.PortraitColor1));
        DrawPlayerCardPortraitSprite(layout.PortraitInnerBounds, profile, team);

        var name = string.IsNullOrWhiteSpace(displayName) ? "PLAYER" : displayName.Trim().ToUpperInvariant();
        var nameMaximumWidth = layout.NameBounds.Width;
        name = PixelPerfectTextLayout.TrimToWidth(
            name,
            nameMaximumWidth,
            candidate => MeasureBitmapFontWidth(candidate, PlayerCardTextScale));
        var namePosition = PixelPerfectTextLayout.CenterNaturalText(
            layout.NameBounds,
            MeasureBitmapFontWidth(name, PlayerCardTextScale),
            MeasureBitmapFontHeight(PlayerCardTextScale));
        DrawBitmapFontText(
            name,
            new Vector2(layout.NameBounds.X, namePosition.Y),
            Color.Black,
            PlayerCardTextScale);

        DrawPlayerCardBio(layout, profile);

        var medal = GetPlayerCardMedalFrame(profile.Medal);
        if (medal is not null)
        {
            DrawLoadedSpriteFrame(medal, layout.MedalBounds, Color.White);
        }

        if (!string.IsNullOrWhiteSpace(actionLabel))
        {
            DrawPlayerCardActionButton(layout.EditButtonBounds, actionLabel, actionActive);
        }
    }

    private void DrawPlayerCardBio(PlayerCardLayout layout, PlayerCardProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.Bio))
        {
            return;
        }

        var quotedBio = $"\"{profile.Bio.ToUpperInvariant()}\"";
        var lines = WrapBitmapFontText(
            quotedBio,
            layout.BioBounds.Width,
            layout.BioBounds.Width);
        var lineHeight = MeasureBitmapFontHeight(PlayerCardTextScale);
        var maximumVisibleLines = Math.Min(
            2,
            (int)MathF.Floor(layout.BioBounds.Height / MathF.Max(1f, lineHeight)));
        for (var index = 0; index < Math.Min(maximumVisibleLines, lines.Count); index += 1)
        {
            var line = index == maximumVisibleLines - 1 && lines.Count > maximumVisibleLines
                ? lines[index] + "..."
                : lines[index];
            line = PixelPerfectTextLayout.TrimToWidth(
                line,
                layout.BioBounds.Width,
                candidate => MeasureBitmapFontWidth(candidate, PlayerCardTextScale));
            DrawBitmapFontText(
                line,
                new Vector2(
                    layout.BioBounds.X,
                    MathF.Round(layout.BioBounds.Y + (index * lineHeight))),
                Color.White,
                PlayerCardTextScale);
        }
    }

    private void DrawPlayerCardActionButton(Rectangle bounds, string label, bool highlighted)
    {
        var fillColor = highlighted
            ? new Color(77, 69, 63)
            : new Color(54, 47, 41);
        DrawRoundedRectangleOutline(
            bounds,
            fillColor,
            new Color(213, 205, 188),
            outlineThickness: 2,
            radius: 8);

        const int horizontalPadding = 14;
        var visibleLabel = PixelPerfectTextLayout.TrimToWidth(
            label,
            Math.Max(0f, bounds.Width - (horizontalPadding * 2f)),
            candidate => MeasureBitmapFontWidth(candidate, PlayerCardTextScale));
        var textY = MathF.Round(
            bounds.Y
            + MathF.Max(
                4f,
                ((bounds.Height - MeasureBitmapFontHeight(PlayerCardTextScale)) * 0.5f) - 1f));
        DrawBitmapFontText(
            visibleLabel,
            new Vector2(bounds.X + horizontalPadding, textY),
            Color.White,
            PlayerCardTextScale);
    }

    private void DrawPlayerCardEditor(PlayerCardEditorLayout layout)
    {
        var profile = PlayerCardProfile.Sanitize(_clientIdentity.PlayerCard);
        DrawRoundedRectangle(new Rectangle(layout.Bounds.X + 6, layout.Bounds.Y + 6, layout.Bounds.Width, layout.Bounds.Height), Color.Black * 0.36f, 8);
        DrawRoundedRectangleOutline(layout.Bounds, new Color(59, 51, 46), new Color(213, 205, 188), outlineThickness: 2, radius: 8);

        DrawBitmapFontText("Edit Playercard", new Vector2(layout.Bounds.X + 16f, layout.Bounds.Y + 12f), Color.White, 1f);

        DrawPlayerCard(
            layout.PreviewCard,
            profile,
            GetSocialPresenceDisplayName(),
            string.Empty,
            actionActive: false);
        DrawBitmapFontText(
            "Drag portrait to position",
            new Vector2(layout.PreviewCard.Bounds.X, layout.PreviewCard.Bounds.Bottom + 3f),
            new Color(213, 205, 188),
            0.65f);

        DrawBitmapFontText("Color", new Vector2(layout.ColorWheelBounds.X, layout.ColorWheelBounds.Y - 15f), Color.White, 0.76f);

        DrawBitmapFontText("Bio (26 characters)", new Vector2(layout.BioInputBounds.X, layout.BioInputBounds.Y - 16f), Color.White, 0.86f);
        DrawRoundedRectangleOutline(
            layout.BioInputBounds,
            new Color(31, 28, 26),
            _editingPlayerCardBio ? new Color(255, 242, 190) : new Color(173, 164, 139),
            outlineThickness: 1,
            radius: 4);
        var bioText = _editingPlayerCardBio
            ? GetTextWithCursor(_playerCardBioInputBuffer, _playerCardBioCursorIndex)
            : profile.Bio;
        DrawBitmapFontText(
            TrimBitmapMenuText(bioText, layout.BioInputBounds.Width - 14f, 0.86f),
            new Vector2(layout.BioInputBounds.X + 7f, layout.BioInputBounds.Y + 8f),
            Color.White,
            0.86f);

        var medalDefinition = GetPlayerCardMedalDefinition(profile.Medal);
        DrawBitmapFontText("Medal", new Vector2(layout.MedalPrevBounds.X, layout.MedalPrevBounds.Y - 16f), Color.White, 0.86f);
        DrawMenuButtonScaled(layout.MedalPrevBounds, "<", false, 1f);
        DrawMenuButtonScaled(layout.MedalNextBounds, ">", false, 1f);
        DrawBitmapFontText(
            TrimBitmapMenuText(medalDefinition.DisplayName, layout.MedalNextBounds.X - layout.MedalPrevBounds.Right - 14f, 0.8f),
            new Vector2(layout.MedalPrevBounds.Right + 8f, layout.MedalPrevBounds.Y + 8f),
            Color.White,
            0.8f);

        var playerClass = GetPlayerCardClass(profile.Class);
        var className = CharacterClassCatalog.GetDefinition(playerClass).DisplayName;
        DrawBitmapFontText("Class", new Vector2(layout.ClassPrevBounds.X, layout.ClassPrevBounds.Y - 16f), Color.White, 0.86f);
        DrawMenuButtonScaled(layout.ClassPrevBounds, "<", false, 1f);
        DrawMenuButtonScaled(layout.ClassNextBounds, ">", false, 1f);
        DrawBitmapFontText(className, new Vector2(layout.ClassPrevBounds.Right + 8f, layout.ClassPrevBounds.Y + 8f), Color.White, 0.8f);

        DrawBitmapFontText("Preview team", new Vector2(layout.TeamBounds.X, layout.TeamBounds.Y - 16f), Color.White, 0.86f);
        DrawMenuButtonScaled(layout.TeamBounds, string.Equals(profile.Team, "Red", StringComparison.OrdinalIgnoreCase) ? "Red" : "Blu", false, 0.86f);

        DrawBitmapFontText("Frame", new Vector2(layout.FramePrevBounds.X, layout.FramePrevBounds.Y - 16f), Color.White, 0.86f);
        DrawMenuButtonScaled(layout.FramePrevBounds, "<", false, 1f);
        DrawMenuButtonScaled(layout.FrameNextBounds, ">", false, 1f);
        var frameCount = Math.Max(1, GetPlayerCardTauntFrameCount(profile));
        DrawBitmapFontText($"{Math.Clamp(profile.Frame + 1, 1, frameCount)}/{frameCount}", new Vector2(layout.FramePrevBounds.Right + 8f, layout.FramePrevBounds.Y + 8f), Color.White, 0.8f);

        DrawBitmapFontText("Zoom", new Vector2(layout.ZoomOutBounds.X, layout.ZoomOutBounds.Y - 16f), Color.White, 0.86f);
        DrawMenuButtonScaled(layout.ZoomOutBounds, "-", false, 1f);
        DrawMenuButtonScaled(layout.ZoomInBounds, "+", false, 1f);

        DrawBitmapFontText("Portrait colors", new Vector2(layout.PortraitColor1Bounds.X, layout.PortraitColor1Bounds.Y - 16f), Color.White, 0.86f);
        DrawPlayerCardColorSwatch(layout.PortraitColor1Bounds, PlayerCardColorFromHex(profile.PortraitColor1), _playerCardActiveColorIndex == 0);
        DrawPlayerCardColorSwatch(layout.PortraitColor2Bounds, PlayerCardColorFromHex(profile.PortraitColor2), _playerCardActiveColorIndex == 1);
        DrawMenuButtonScaled(layout.PortraitGradientBounds, "Gradient", profile.PortraitGradient, 0.8f);

        DrawPlayerCardColorWheel(layout.ColorWheelBounds);
        DrawPlayerCardColorBrightnessControls(layout, profile);
    }

    private PlayerCardLayout GetPlayerCardLayout(FriendsMenuLayout friendsLayout, Rectangle? hoverRowBounds)
    {
        var leftSpace = Math.Max(320, friendsLayout.Panel.X - 28);
        var baseWidth = Math.Clamp(leftSpace, 320, 500);
        var width = Math.Clamp((int)MathF.Round(baseWidth * GetPlayerCardSizeScale()), 241, baseWidth);
        var height = GetPlayerCardHeight(width);
        var x = Math.Max(12, friendsLayout.Panel.X - width - 18);
        var preferredY = hoverRowBounds.HasValue
            ? hoverRowBounds.Value.Center.Y - (height / 2)
            : friendsLayout.Panel.Y + 10;
        var bottomReserve = hoverRowBounds.HasValue ? 16 : 54;
        var y = Math.Clamp(preferredY, 12, Math.Max(12, ViewportHeight - height - bottomReserve));
        return CreatePlayerCardLayout(new Rectangle(x, y, width, height));
    }

    private PlayerCardEditorLayout GetPlayerCardEditorLayout(PlayerCardLayout cardLayout)
    {
        const int padding = 16;
        var width = Math.Max(360, cardLayout.Bounds.Width);
        var height = 390;
        var y = cardLayout.EditButtonBounds.Bottom + 10;
        if (y + height > ViewportHeight - 12)
        {
            y = Math.Max(12, cardLayout.Bounds.Y - height - 10);
        }

        var x = Math.Clamp(cardLayout.Bounds.Right - width, 12, Math.Max(12, ViewportWidth - width - 12));
        var bounds = new Rectangle(x, y, width, height);
        var smallButton = 38;
        var rightColumnWidth = Math.Clamp(width / 4, 86, 108);
        var rightX = bounds.Right - padding - rightColumnWidth;
        var leftRightLimit = rightX - 12;
        var previewWidth = Math.Min(
            PlayerCardNativeWidth,
            Math.Max(160, rightX - 10 - (bounds.X + padding)));
        var previewBounds = new Rectangle(
            bounds.X + padding,
            bounds.Y + 34,
            previewWidth,
            GetPlayerCardHeight(previewWidth));
        var previewCard = CreatePlayerCardLayout(previewBounds);

        var colorWheelSize = Math.Clamp(rightColumnWidth - 8, 72, 82);
        var colorWheel = new Rectangle(
            rightX + ((rightColumnWidth - colorWheelSize) / 2),
            previewBounds.Y,
            colorWheelSize,
            colorWheelSize);
        var brightnessY = colorWheel.Bottom + 6;
        var brightnessButtonWidth = 28;
        var brightnessDown = new Rectangle(colorWheel.X, brightnessY, brightnessButtonWidth, 28);
        var brightnessUp = new Rectangle(colorWheel.Right - brightnessButtonWidth, brightnessY, brightnessButtonWidth, 28);

        var rowY = bounds.Y + 160;
        var bioInput = new Rectangle(bounds.X + padding, rowY, bounds.Width - (padding * 2), 28);

        rowY += 42;
        var medalPrev = new Rectangle(bounds.X + padding, rowY, smallButton, 28);
        var medalNext = new Rectangle(leftRightLimit - smallButton, rowY, smallButton, 28);

        rowY += 42;
        var classPrev = new Rectangle(bounds.X + padding, rowY, smallButton, 28);
        var classNext = new Rectangle(Math.Min(bounds.X + 150, leftRightLimit - smallButton), rowY, smallButton, 28);
        var team = new Rectangle(rightX, rowY, rightColumnWidth, 28);

        rowY += 42;
        var framePrev = new Rectangle(bounds.X + padding, rowY, smallButton, 28);
        var frameNext = new Rectangle(Math.Min(bounds.X + 126, leftRightLimit - smallButton), rowY, smallButton, 28);
        var zoomOut = new Rectangle(rightX, rowY, 36, 28);
        var zoomIn = new Rectangle(rightX + 44, rowY, 40, 28);

        var colorRowY = rowY + 48;
        var portraitColor1 = new Rectangle(bounds.X + padding, colorRowY, 34, 34);
        var portraitColor2 = new Rectangle(portraitColor1.Right + 8, colorRowY, 34, 34);
        var gradientWidth = Math.Clamp(leftRightLimit - portraitColor2.Right - 18, 72, 92);
        var portraitGradient = new Rectangle(portraitColor2.Right + 10, colorRowY, gradientWidth, 34);

        return new PlayerCardEditorLayout(
            bounds,
            previewCard,
            bioInput,
            medalPrev,
            medalNext,
            classPrev,
            classNext,
            team,
            framePrev,
            frameNext,
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
        var portraitInner = ScalePlayerCardSourceRectangle(bounds, 8, 8, 64, 64);
        var name = ScalePlayerCardSourceRectangle(bounds, 87, 18, 173, 32);
        var bio = ScalePlayerCardSourceRectangle(bounds, 16, 81, 151, 30);
        var medal = ScalePlayerCardSourceRectangle(bounds, 213, 45, 37, 73);
        var editWidth = Math.Clamp((int)MathF.Round(bounds.Width * 0.36f), 100, 124);
        var editHeight = Math.Clamp((int)MathF.Round(bounds.Width * 0.105f), 26, 34);
        var edit = new Rectangle(bounds.Right - editWidth, bounds.Bottom + 7, editWidth, editHeight);
        return new PlayerCardLayout(bounds, portraitInner, edit, name, bio, medal);
    }

    private static Rectangle ScalePlayerCardSourceRectangle(Rectangle bounds, int x, int y, int width, int height)
    {
        var scaleX = bounds.Width / (float)PlayerCardNativeWidth;
        var scaleY = bounds.Height / (float)PlayerCardNativeHeight;
        return new Rectangle(
            bounds.X + (int)MathF.Round(x * scaleX),
            bounds.Y + (int)MathF.Round(y * scaleY),
            Math.Max(1, (int)MathF.Round(width * scaleX)),
            Math.Max(1, (int)MathF.Round(height * scaleY)));
    }

    private static int GetPlayerCardHeight(int width)
    {
        return Math.Max(1, (int)MathF.Round(width * (PlayerCardNativeHeight / (float)PlayerCardNativeWidth)));
    }

    private LoadedSpriteFrame? GetPlayerCardTemplateFrame(PlayerTeam team)
    {
        return GetPlayerCardAssetFrame(team == PlayerTeam.Red ? "redplayercard.png" : "blueplayercard.png");
    }

    private LoadedSpriteFrame? GetPlayerCardMedalFrame(string medalId)
    {
        var medal = GetPlayerCardMedalDefinition(medalId);
        return GetPlayerCardAssetFrame(Path.Combine("Medals", medal.FileName));
    }

    private LoadedSpriteFrame? GetPlayerCardAssetFrame(string relativePath)
    {
        if (_playerCardAssetFrameCache.TryGetValue(relativePath, out var cached))
        {
            return cached;
        }

        var path = ContentRoot.GetPath("Sprites", "Menu", "PlayerCards", relativePath);
        var frame = string.IsNullOrWhiteSpace(path) || !CanLoadSpriteFrameFromPath(path)
            ? null
            : LoadSpriteFrameFromPath(path);
        _playerCardAssetFrameCache[relativePath] = frame;
        return frame;
    }

    private static PlayerTeam GetPlayerCardTeam(PlayerCardProfile profile)
    {
        return string.Equals(profile.Team, "Red", StringComparison.OrdinalIgnoreCase)
            ? PlayerTeam.Red
            : PlayerTeam.Blue;
    }

    private static PlayerCardMedalDefinition GetPlayerCardMedalDefinition(string medalId)
    {
        for (var index = 0; index < PlayerCardMedals.Length; index += 1)
        {
            if (string.Equals(PlayerCardMedals[index].Id, medalId, StringComparison.OrdinalIgnoreCase))
            {
                return PlayerCardMedals[index];
            }
        }

        return PlayerCardMedals[0];
    }

    private void DrawPlayerCardPortraitSprite(Rectangle portraitBounds, PlayerCardProfile profile, PlayerTeam team)
    {
        var sprite = GetResolvedSprite(GetPlayerCardTauntSpriteName(profile, team));
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
            1 => PlayerCardColorFromHex(profile.PortraitColor2),
            _ => PlayerCardColorFromHex(profile.PortraitColor1),
        };
    }

    private void SetPlayerCardActiveColor(PlayerCardProfile profile, Color color)
    {
        var hex = PlayerCardColorToHex(color);
        if (_playerCardActiveColorIndex == 1)
        {
            profile.PortraitColor2 = hex;
        }
        else
        {
            profile.PortraitColor1 = hex;
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
        var profile = PlayerCardProfile.CreateDefault();
        var playerClass = PlayerCardClasses[positive % PlayerCardClasses.Length];
        profile.Class = playerClass.ToString();
        profile.Team = (positive & 1) == 0 ? "Blue" : "Red";
        profile.Frame = positive % Math.Max(1, GetPlayerCardTauntFrameCount(profile));
        profile.PortraitColor1 = PlayerCardFallbackColors[(positive / 11) % PlayerCardFallbackColors.Length];
        profile.PortraitColor2 = PlayerCardFallbackColors[(positive / 17) % PlayerCardFallbackColors.Length];
        profile.PortraitGradient = true;
        return PlayerCardProfile.Sanitize(profile);
    }

    private static void CyclePlayerCardMedal(PlayerCardProfile profile, int direction)
    {
        var currentIndex = 0;
        for (var index = 0; index < PlayerCardMedals.Length; index += 1)
        {
            if (string.Equals(PlayerCardMedals[index].Id, profile.Medal, StringComparison.OrdinalIgnoreCase))
            {
                currentIndex = index;
                break;
            }
        }

        profile.Medal = PlayerCardMedals[PositiveModulo(currentIndex + direction, PlayerCardMedals.Length)].Id;
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
        return GetPlayerCardTauntSpriteName(profile, GetPlayerCardTeam(profile));
    }

    private static string GetPlayerCardTauntSpriteName(PlayerCardProfile profile, PlayerTeam team)
    {
        var playerClass = GetPlayerCardClass(profile.Class);
        var className = playerClass == PlayerClass.Quote ? "Querly" : playerClass.ToString();
        var teamName = team == PlayerTeam.Red ? "Red" : "Blue";
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
