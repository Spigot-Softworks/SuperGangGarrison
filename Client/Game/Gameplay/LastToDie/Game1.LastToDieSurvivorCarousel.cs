#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;
using OpenGarrison.Core.LastToDie;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed record LastToDieSurvivorCard(
        LastToDieSurvivorId Id,
        string Label,
        string AssetName);

    private readonly record struct LastToDieCarouselCardDraw(
        int CardIndex,
        Rectangle Bounds,
        bool Active);

    private static readonly IReadOnlyList<LastToDieSurvivorCard> LastToDieSurvivorCards =
    [
        new(OpenGarrison.Core.LastToDie.LastToDieSurvivorCatalog.SoldierId, "ROCKETMAN", "soldiercard.png"),
        new(OpenGarrison.Core.LastToDie.LastToDieSurvivorCatalog.DemoknightId, "DEMOKNIGHT", "democard.png"),
        new(OpenGarrison.Core.LastToDie.LastToDieSurvivorCatalog.MedicId, "HEALER", "mediccard.png"),
        new(OpenGarrison.Core.LastToDie.LastToDieSurvivorCatalog.EngineerId, "CONSTRUCTOR", "engicard.png"),
        new(OpenGarrison.Core.LastToDie.LastToDieSurvivorCatalog.SniperId, "RIFLEMAN", "snipercard.png"),
        new(OpenGarrison.Core.LastToDie.LastToDieSurvivorCatalog.SpyId, "INFILTRATOR", "spycard.png"),
    ];

    private readonly LoadedSpriteFrame?[] _lastToDieSurvivorCardFrames = new LoadedSpriteFrame?[6];
    private LoadedSpriteFrame? _lastToDieCarouselLeftArrow;
    private LoadedSpriteFrame? _lastToDieCarouselRightArrow;
    private SoundEffect? _lastToDieCarouselHoverSound;
    private SoundEffect? _lastToDieCarouselArrowSound;
    private SoundEffect? _lastToDieCarouselLockInSound;
    private SoundEffect? _lastToDiePlayerDieSound;
    private SoundEffect? _lastToDieLoseSound;
    private bool _lastToDieCarouselAssetsLoaded;
    private int _lastToDieCarouselFirstCard;
    private int _lastToDieCarouselHoveredCard = -1;
    private int _lastToDieCarouselLastSoundCard = -1;
    private float _lastToDieCarouselSlideOffset;

    private void ResetLastToDieSurvivorCarousel()
    {
        _lastToDieCarouselFirstCard = 0;
        _lastToDieCarouselHoveredCard = -1;
        _lastToDieCarouselLastSoundCard = -1;
        _lastToDieCarouselSlideOffset = 0f;
    }

    private void UpdateLastToDieSurvivorCarousel(
        KeyboardState keyboard,
        MouseState mouse,
        string selectedSurvivorId,
        Action<string> selectSurvivor,
        Action unlockSurvivor)
    {
        EnsureLastToDieSurvivorCarouselAssets();
        _lastToDieCarouselSlideOffset = MathHelper.Lerp(
            _lastToDieCarouselSlideOffset,
            0f,
            Math.Clamp(_gameplayPresentationDeltaSeconds * 13f, 0f, 1f));
        if (MathF.Abs(_lastToDieCarouselSlideOffset) < 0.25f)
        {
            _lastToDieCarouselSlideOffset = 0f;
        }

        var selected = !string.IsNullOrWhiteSpace(selectedSurvivorId);
        if (selected && (IsKeyPressed(keyboard, Keys.Escape) || IsControllerMenuBackPressed()))
        {
            unlockSurvivor();
            return;
        }

        var (_, leftArrow, rightArrow, cards) = GetLastToDieCarouselLayout(selectedSurvivorId);
        var clicked = mouse.LeftButton == ButtonState.Pressed
            && _previousMouse.LeftButton != ButtonState.Pressed;
        _ = TryConsumeControllerMenuNavigation(out var controllerHorizontal, out _);
        var leftRequested = IsKeyPressed(keyboard, Keys.Left)
            || controllerHorizontal < 0
            || leftArrow.Contains(mouse.Position) && clicked;
        var rightRequested = IsKeyPressed(keyboard, Keys.Right)
            || controllerHorizontal > 0
            || rightArrow.Contains(mouse.Position) && clicked;
        if (leftRequested)
        {
            ShiftLastToDieCarousel(-1);
        }
        else if (rightRequested)
        {
            ShiftLastToDieCarousel(1);
        }

        if (leftRequested || rightRequested)
        {
            (_, leftArrow, rightArrow, cards) = GetLastToDieCarouselLayout(selectedSurvivorId);
        }

        _lastToDieCarouselHoveredCard = -1;
        if (ShouldUseMouseMenuHover(mouse))
        {
            foreach (var card in cards)
            {
                if (card.Bounds.Contains(mouse.Position))
                {
                    _lastToDieCarouselHoveredCard = card.CardIndex;
                    break;
                }
            }
        }
        else if (IsControllerMenuInputActive())
        {
            _lastToDieCarouselHoveredCard = _lastToDieCarouselFirstCard;
        }

        if (_lastToDieCarouselHoveredCard >= 0
            && _lastToDieCarouselHoveredCard != _lastToDieCarouselLastSoundCard)
        {
            TryPlaySound(_lastToDieCarouselHoverSound, 0.8f, 0f, 0f);
        }
        _lastToDieCarouselLastSoundCard = _lastToDieCarouselHoveredCard;

        var confirmRequested = clicked && _lastToDieCarouselHoveredCard >= 0
            || IsKeyPressed(keyboard, Keys.Enter)
            || IsControllerMenuConfirmPressed();
        if (selected || !confirmRequested)
        {
            return;
        }

        var cardIndex = _lastToDieCarouselHoveredCard >= 0
            ? _lastToDieCarouselHoveredCard
            : _lastToDieCarouselFirstCard;
        TryPlaySound(_lastToDieCarouselLockInSound, 0.9f, 0f, 0f);
        selectSurvivor(LastToDieSurvivorCards[cardIndex].Id.Value);
    }

    private void ShiftLastToDieCarousel(int direction)
    {
        if (direction == 0 || LastToDieSurvivorCards.Count <= 4)
        {
            return;
        }

        var slotSpacing = GetLastToDieCarouselSlotSpacing();
        _lastToDieCarouselFirstCard = Mod(
            _lastToDieCarouselFirstCard + Math.Sign(direction),
            LastToDieSurvivorCards.Count);
        _lastToDieCarouselSlideOffset = direction > 0 ? slotSpacing : -slotSpacing;
        TryPlaySound(_lastToDieCarouselArrowSound, 0.9f, 0f, 0f);
    }

    private void DrawLastToDieSurvivorCarousel(string selectedSurvivorId)
    {
        EnsureLastToDieSurvivorCarouselAssets();
        var (titleBounds, leftArrow, rightArrow, cards) = GetLastToDieCarouselLayout(selectedSurvivorId);

        if (_lastToDieCarouselLeftArrow is not null)
        {
            DrawLoadedSpriteFrame(_lastToDieCarouselLeftArrow, leftArrow, Color.White);
        }
        if (_lastToDieCarouselRightArrow is not null)
        {
            DrawLoadedSpriteFrame(_lastToDieCarouselRightArrow, rightArrow, Color.White);
        }

        _spriteBatch.End();
        _spriteBatch.Begin(
            samplerState: SamplerState.PointClamp,
            effect: _grayscaleEffect,
            rasterizerState: RasterizerState.CullNone);
        foreach (var card in cards)
        {
            if (card.Active || _lastToDieSurvivorCardFrames[card.CardIndex] is not { } frame)
            {
                continue;
            }

            DrawLoadedSpriteFrame(frame, card.Bounds, Color.White);
        }
        _spriteBatch.End();
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp, rasterizerState: RasterizerState.CullNone);

        foreach (var card in cards)
        {
            if (card.Active && _lastToDieSurvivorCardFrames[card.CardIndex] is { } frame)
            {
                DrawLoadedSpriteFrame(frame, card.Bounds, Color.White);
            }

            var labelBounds = new Rectangle(
                card.Bounds.X - 8,
                card.Bounds.Bottom + 6,
                card.Bounds.Width + 16,
                Math.Max(24, (int)MathF.Round(ViewportHeight * 0.055f)));
            DrawOutlinedLastToDieMenuText(
                LastToDieSurvivorCards[card.CardIndex].Label,
                labelBounds,
                Color.White,
                new Color(207, 37, 37),
                maxScale: 1.25f,
                outlinePixels: 2);
        }

        DrawOutlinedLastToDieMenuText(
            string.IsNullOrWhiteSpace(selectedSurvivorId) ? "CHOOSE A SURVIVOR" : "LOCKED IN!",
            titleBounds,
            Color.White,
            Color.Black,
            maxScale: 2.15f,
            outlinePixels: 3);
    }

    private (Rectangle Title, Rectangle LeftArrow, Rectangle RightArrow, LastToDieCarouselCardDraw[] Cards)
        GetLastToDieCarouselLayout(string selectedSurvivorId)
    {
        const int visibleCards = 4;
        var horizontalMargin = MathF.Max(42f, ViewportWidth * 0.045f);
        var gap = MathF.Max(18f, ViewportWidth * 0.025f);
        var maximumCardWidth = (ViewportWidth - (horizontalMargin * 2f) - (gap * (visibleCards - 1))) / visibleCards;
        var maximumCardHeight = ViewportHeight * 0.51f;
        const float sourceWidth = 266f;
        const float sourceHeight = 370f;
        var scale = MathF.Min(maximumCardWidth / sourceWidth, maximumCardHeight / sourceHeight);
        var cardWidth = MathF.Max(80f, sourceWidth * scale);
        var cardHeight = MathF.Max(112f, sourceHeight * scale);
        var slotSpacing = cardWidth + gap;
        var totalWidth = (cardWidth * visibleCards) + (gap * (visibleCards - 1));
        var startX = (ViewportWidth - totalWidth) * 0.5f;
        var cardY = ViewportHeight * 0.205f;
        var draws = new LastToDieCarouselCardDraw[visibleCards];
        for (var slot = 0; slot < visibleCards; slot += 1)
        {
            var cardIndex = Mod(_lastToDieCarouselFirstCard + slot, LastToDieSurvivorCards.Count);
            var active = cardIndex == _lastToDieCarouselHoveredCard
                || string.Equals(selectedSurvivorId, LastToDieSurvivorCards[cardIndex].Id.Value, StringComparison.Ordinal);
            var lift = active ? MathF.Max(6f, ViewportHeight * 0.012f) : 0f;
            draws[slot] = new LastToDieCarouselCardDraw(
                cardIndex,
                new Rectangle(
                    (int)MathF.Round(startX + (slot * slotSpacing) + _lastToDieCarouselSlideOffset),
                    (int)MathF.Round(cardY - lift),
                    Math.Max(1, (int)MathF.Round(cardWidth)),
                    Math.Max(1, (int)MathF.Round(cardHeight))),
                active);
        }

        var arrowWidth = Math.Max(31, (int)MathF.Round(ViewportHeight * 0.068f));
        var arrowHeight = Math.Max(39, (int)MathF.Round(arrowWidth * (78f / 63f)));
        var arrowY = Math.Max(18, (int)MathF.Round(ViewportHeight * 0.07f));
        var arrowInset = Math.Max(20, (int)MathF.Round(ViewportWidth * 0.045f));
        var titleHeight = Math.Max(52, (int)MathF.Round(ViewportHeight * 0.11f));
        return (
            new Rectangle(0, (int)MathF.Round(ViewportHeight * 0.82f), ViewportWidth, titleHeight),
            new Rectangle(arrowInset, arrowY, arrowWidth, arrowHeight),
            new Rectangle(ViewportWidth - arrowInset - arrowWidth, arrowY, arrowWidth, arrowHeight),
            draws);
    }

    private float GetLastToDieCarouselSlotSpacing()
    {
        var horizontalMargin = MathF.Max(42f, ViewportWidth * 0.045f);
        var gap = MathF.Max(18f, ViewportWidth * 0.025f);
        var maximumCardWidth = (ViewportWidth - (horizontalMargin * 2f) - (gap * 3f)) / 4f;
        var cardScale = MathF.Min(maximumCardWidth / 266f, (ViewportHeight * 0.51f) / 370f);
        return MathF.Max(80f, 266f * cardScale) + gap;
    }

    private void DrawOutlinedLastToDieMenuText(
        string text,
        Rectangle bounds,
        Color fill,
        Color outline,
        float maxScale,
        int outlinePixels)
    {
        var scale = GetMenuFontScaleToFit(text, bounds.Width - 12f, bounds.Height - 4f, maxScale);
        var width = MeasureMenuBitmapFontWidth(text, scale);
        var height = MeasureMenuBitmapFontHeight(scale);
        var position = new Vector2(
            bounds.X + ((bounds.Width - width) * 0.5f),
            bounds.Y + ((bounds.Height - height) * 0.5f));
        for (var y = -outlinePixels; y <= outlinePixels; y += outlinePixels)
        {
            for (var x = -outlinePixels; x <= outlinePixels; x += outlinePixels)
            {
                if (x != 0 || y != 0)
                {
                    DrawMenuBitmapFontText(text, position + new Vector2(x, y), outline, scale);
                }
            }
        }
        DrawMenuBitmapFontText(text, position, fill, scale);
    }

    private void EnsureLastToDieSurvivorCarouselAssets()
    {
        if (_lastToDieCarouselAssetsLoaded)
        {
            return;
        }

        _lastToDieCarouselAssetsLoaded = true;
        for (var index = 0; index < LastToDieSurvivorCards.Count; index += 1)
        {
            _lastToDieSurvivorCardFrames[index] = LoadSpriteFrameFromPath(ContentRoot.GetPath(
                "Sprites", "Menu", "LastToDie", "CharacterSelect", LastToDieSurvivorCards[index].AssetName));
        }
        _lastToDieCarouselLeftArrow = LoadSpriteFrameFromPath(ContentRoot.GetPath(
            "Sprites", "Menu", "LastToDie", "CharacterSelect", "arrowright.png"));
        _lastToDieCarouselRightArrow = LoadSpriteFrameFromPath(ContentRoot.GetPath(
            "Sprites", "Menu", "LastToDie", "CharacterSelect", "arrowleft.png"));
        _lastToDieCarouselHoverSound = TryLoadLastToDieUiSound("hover.ogg");
        _lastToDieCarouselArrowSound = TryLoadLastToDieUiSound("arrow.ogg");
        _lastToDieCarouselLockInSound = TryLoadLastToDieUiSound("lockin.ogg");
        _lastToDiePlayerDieSound = TryLoadLastToDieUiSound("playerdie.ogg");
        _lastToDieLoseSound = TryLoadLastToDieUiSound("lose.ogg");
    }

    private SoundEffect? TryLoadLastToDieUiSound(string fileName)
    {
        var relativePath = $"Content/Sounds/LastToDie/{fileName}";
        var path = ContentRoot.GetPath("Sounds", "LastToDie", fileName);
        if (OperatingSystem.IsBrowser())
        {
            if ((_browserBootstrapAssets?.TryGetBinary(relativePath, out var browserBytes) ?? false)
                || BrowserContentCatalog.TryGetBinary(relativePath, out browserBytes))
            {
                try
                {
                    return SoundDecodeUtility.LoadSoundEffect(browserBytes, fileName);
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            return SoundDecodeUtility.LoadSoundEffect(File.ReadAllBytes(path), fileName);
        }
        catch
        {
            return null;
        }
    }

    private void DisposeLastToDieSurvivorCarouselAssets()
    {
        foreach (var frame in _lastToDieSurvivorCardFrames)
        {
            frame?.Dispose();
        }
        _lastToDieCarouselLeftArrow?.Dispose();
        _lastToDieCarouselRightArrow?.Dispose();
        _lastToDieCarouselHoverSound?.Dispose();
        _lastToDieCarouselArrowSound?.Dispose();
        _lastToDieCarouselLockInSound?.Dispose();
        _lastToDiePlayerDieSound?.Dispose();
        _lastToDieLoseSound?.Dispose();
        _lastToDieCarouselAssetsLoaded = false;
    }

    private static int Mod(int value, int modulus)
        => ((value % modulus) + modulus) % modulus;
}
