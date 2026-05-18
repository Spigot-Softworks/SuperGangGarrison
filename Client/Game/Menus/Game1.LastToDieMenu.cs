#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;
using System;
using System.IO;

namespace OpenGarrison.Client;

public partial class Game1
{
    private enum LastToDieMenuPage
    {
        Root,
        Difficulty,
        Stats,
    }

    private readonly record struct LastToDieMenuLayout(
        Rectangle PlaqueBounds,
        Rectangle ContentBounds,
        Rectangle[] ButtonBounds,
        float Scale);

    private bool _lastToDieMenuOpen;
    private LastToDieMenuPage _lastToDieMenuPage;
    private int _lastToDieMenuHoverIndex = -1;
    private LoadedSpriteFrame? _lastToDieLogoTexture;
    private string? _lastToDieLogoTexturePath;

    private bool IsLastToDieMenuActive()
    {
        return _mainMenuOpen && _lastToDieMenuOpen;
    }

    private void OpenLastToDieMenu(string? statusMessage = null)
    {
        _mainMenuOverlayStateController.OpenLastToDieMenu(statusMessage);
    }

    private void CloseLastToDieMenu(bool clearStatus = false)
    {
        _mainMenuOverlayStateController.CloseLastToDieMenu(clearStatus);
    }

    private void ReturnToLastToDieMenu(string? statusMessage = null)
    {
        StopLastToDieGameOverSound();
        ReturnToMainMenu(statusMessage);
        OpenLastToDieMenu(statusMessage);
    }

    private void UpdateLastToDieMenu(KeyboardState keyboard, MouseState mouse)
    {
        var buttonLabels = GetLastToDieMenuButtonLabels();
        if (buttonLabels.Length == 0)
        {
            return;
        }

        var layout = GetLastToDieMenuLayout(buttonLabels.Length, _lastToDieMenuPage == LastToDieMenuPage.Stats);
        if (IsKeyPressed(keyboard, Keys.Escape))
        {
            if (_lastToDieMenuPage == LastToDieMenuPage.Stats)
            {
                OpenLastToDieStatsPage(false);
            }
            else if (_lastToDieMenuPage == LastToDieMenuPage.Difficulty)
            {
                OpenLastToDieDifficultyPage(false);
            }
            else
            {
                CloseLastToDieMenu();
            }

            return;
        }

        if (IsKeyPressed(keyboard, Keys.Up))
        {
            SetLastToDieMenuHoverIndex((_lastToDieMenuHoverIndex <= 0 ? buttonLabels.Length : _lastToDieMenuHoverIndex) - 1, buttonLabels.Length);
        }
        else if (IsKeyPressed(keyboard, Keys.Down))
        {
            SetLastToDieMenuHoverIndex((_lastToDieMenuHoverIndex + 1 + buttonLabels.Length) % buttonLabels.Length, buttonLabels.Length);
        }

        var hoveredButtonIndex = GetHoveredLastToDieMenuButtonIndex(mouse.Position, layout);
        if (hoveredButtonIndex >= 0)
        {
            _lastToDieMenuHoverIndex = hoveredButtonIndex;
        }
        else if (_lastToDieMenuHoverIndex < 0 && buttonLabels.Length > 0)
        {
            _lastToDieMenuHoverIndex = Math.Clamp(_lastToDieMenuHoverIndex, -1, buttonLabels.Length - 1);
        }

        if (IsKeyPressed(keyboard, Keys.Enter))
        {
            ActivateLastToDieMenuButton(_lastToDieMenuHoverIndex >= 0 ? _lastToDieMenuHoverIndex : 0);
            return;
        }

        var clickPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed;
        if (clickPressed && _lastToDieMenuHoverIndex >= 0)
        {
            ActivateLastToDieMenuButton(_lastToDieMenuHoverIndex);
        }
    }

    private string[] GetLastToDieMenuButtonLabels()
    {
        return _lastToDieMenuPage switch
        {
            LastToDieMenuPage.Stats => ["Back"],
            LastToDieMenuPage.Difficulty => ["Standard", "Hardcore", "Back"],
            _ => ["Play", "Stats", "Back"],
        };
    }

    private void ActivateLastToDieMenuButton(int index)
    {
        switch (_lastToDieMenuPage)
        {
            case LastToDieMenuPage.Stats:
                if (index == 0)
                {
                    OpenLastToDieStatsPage(false);
                }
                break;

            case LastToDieMenuPage.Difficulty:
                switch (index)
                {
                    case 0:
                        TryStartLastToDieRun(LastToDieDifficulty.Standard);
                        break;
                    case 1:
                        TryStartLastToDieRun(LastToDieDifficulty.Hardcore);
                        break;
                    case 2:
                        OpenLastToDieDifficultyPage(false);
                        break;
                }
                break;

            default:
                switch (index)
                {
                    case 0:
                        OpenLastToDieDifficultyPage(true);
                        break;
                    case 1:
                        OpenLastToDieStatsPage(true);
                        break;
                    case 2:
                        CloseLastToDieMenu();
                        break;
                }
                break;
        }
    }

    private void OpenLastToDieStatsPage(bool open)
    {
        _lastToDieMenuPage = open ? LastToDieMenuPage.Stats : LastToDieMenuPage.Root;
        _lastToDieMenuHoverIndex = 0;
    }

    private void OpenLastToDieDifficultyPage(bool open)
    {
        _lastToDieMenuPage = open ? LastToDieMenuPage.Difficulty : LastToDieMenuPage.Root;
        _lastToDieMenuHoverIndex = 0;
    }

    private void SetLastToDieMenuHoverIndex(int index, int itemCount)
    {
        if (itemCount <= 0)
        {
            _lastToDieMenuHoverIndex = -1;
            return;
        }

        _lastToDieMenuHoverIndex = Math.Clamp(index, 0, itemCount - 1);
    }

    private LastToDieMenuLayout GetLastToDieMenuLayout(int buttonCount, bool statsPage)
    {
        var plaqueTexture = _lastToDieMenuPlaqueTexture ?? _menuPlaqueTexture;
        var buttonTexture = _lastToDieMenuTextBoxSoloTexture ?? _menuTextBoxSoloTexture;
        if (plaqueTexture is null || buttonTexture is null)
        {
            return new LastToDieMenuLayout(Rectangle.Empty, Rectangle.Empty, [], 1f);
        }

        var scale = MathF.Min(1f, (ViewportHeight - 72f) / Math.Max(1f, plaqueTexture.Height)) * 0.78f;
        if (scale <= 0f)
        {
            scale = 0.78f;
        }

        var plaqueBounds = new Rectangle(
            (int)MathF.Round(MathF.Max(24f, ViewportWidth * 0.05f)),
            (int)MathF.Round((ViewportHeight - (plaqueTexture.Height * scale)) * 0.5f),
            Math.Max(1, (int)MathF.Round(plaqueTexture.Width * scale)),
            Math.Max(1, (int)MathF.Round(plaqueTexture.Height * scale)));

        var contentBounds = new Rectangle(
            plaqueBounds.X + (int)MathF.Round(28f * scale),
            plaqueBounds.Y + (int)MathF.Round(34f * scale),
            plaqueBounds.Width - (int)MathF.Round(56f * scale),
            plaqueBounds.Height - (int)MathF.Round(68f * scale));

        var buttonWidth = Math.Max(1, (int)MathF.Round(buttonTexture.Width * scale));
        var buttonHeight = Math.Max(1, (int)MathF.Round(buttonTexture.Height * scale));
        var buttonX = plaqueBounds.X + ((plaqueBounds.Width - buttonWidth) / 2);
        var buttonGap = (int)MathF.Round(16f * scale);
        var buttonBounds = new Rectangle[Math.Max(0, buttonCount)];

        if (buttonCount > 0)
        {
            if (statsPage)
            {
                buttonBounds[0] = new Rectangle(
                    buttonX,
                    plaqueBounds.Bottom - buttonHeight - (int)MathF.Round(18f * scale),
                    buttonWidth,
                    buttonHeight);
            }
            else
            {
                var totalHeight = (buttonCount * buttonHeight) + ((buttonCount - 1) * buttonGap);
                var startY = plaqueBounds.Y + ((plaqueBounds.Height - totalHeight) / 2);
                for (var index = 0; index < buttonCount; index += 1)
                {
                    buttonBounds[index] = new Rectangle(buttonX, startY + index * (buttonHeight + buttonGap), buttonWidth, buttonHeight);
                }
            }
        }

        return new LastToDieMenuLayout(plaqueBounds, contentBounds, buttonBounds, scale);
    }

    private static int GetHoveredLastToDieMenuButtonIndex(Point mousePosition, LastToDieMenuLayout layout)
    {
        for (var index = 0; index < layout.ButtonBounds.Length; index += 1)
        {
            if (layout.ButtonBounds[index].Contains(mousePosition))
            {
                return index;
            }
        }

        return -1;
    }

    private void DrawLastToDieMenu()
    {
        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), new Color(4, 6, 10, 220));

        // Draw bottom bar and runners (in animated mode only) - behind everything else
        if (_menuBackgroundMode != MenuBackgroundMode.Static)
        {
            const int bottomBarHeight = 76;
            var barY = viewportHeight - bottomBarHeight;
            var bottomBarBounds = new Rectangle(0, barY, viewportWidth, bottomBarHeight);
            _spriteBatch.Draw(_pixel, bottomBarBounds, new Color(0x57, 0x4f, 0x47));
            _menuBottomBarRunners.Draw(bottomBarBounds);
        }

        DrawLastToDieMenuLogo(viewportWidth);

        var buttonLabels = GetLastToDieMenuButtonLabels();
        var layout = GetLastToDieMenuLayout(buttonLabels.Length, _lastToDieMenuPage == LastToDieMenuPage.Stats);
        var plaqueTexture = _lastToDieMenuPlaqueTexture ?? _menuPlaqueTexture;
        var buttonTexture = _lastToDieMenuTextBoxSoloTexture ?? _menuTextBoxSoloTexture;
        if (plaqueTexture is not null && layout.PlaqueBounds != Rectangle.Empty)
        {
            DrawLoadedSpriteFrame(plaqueTexture, layout.PlaqueBounds, Color.White);
        }

        DrawLastToDieMenuHeader(layout);
        if (_lastToDieMenuPage == LastToDieMenuPage.Stats)
        {
            DrawLastToDieStatsPage(layout);
        }
        else if (_lastToDieMenuPage == LastToDieMenuPage.Difficulty)
        {
            DrawLastToDieDifficultyPage(layout);
        }

        for (var index = 0; index < layout.ButtonBounds.Length && index < buttonLabels.Length; index += 1)
        {
            DrawLastToDieMenuButton(buttonTexture, layout.ButtonBounds[index], buttonLabels[index], hovered: index == _lastToDieMenuHoverIndex, layout.Scale);
        }

        if (!string.IsNullOrWhiteSpace(_menuStatusMessage))
        {
            DrawShadowedMenuBitmapFontText(
                _menuStatusMessage,
                new Vector2(layout.PlaqueBounds.X, viewportHeight - 42f),
                new Color(255, 255, 255),
                0.72f);
        }
    }

    private void DrawLastToDieMenuHeader(LastToDieMenuLayout layout)
    {
        if (layout.ContentBounds == Rectangle.Empty)
        {
            return;
        }

        if (_lastToDieMenuPage == LastToDieMenuPage.Root)
        {
            return;
        }

        var title = _lastToDieMenuPage switch
        {
            LastToDieMenuPage.Stats => "Stats",
            LastToDieMenuPage.Difficulty => "Difficulty",
            _ => "Last to Die",
        };
        var subtitle = _lastToDieMenuPage == LastToDieMenuPage.Stats
            ? "Track your best solo runs."
            : _lastToDieMenuPage == LastToDieMenuPage.Difficulty
                ? string.Empty
                : "Survive the escalating gauntlet.";
        DrawShadowedMenuBitmapFontText(
            title,
            new Vector2(layout.ContentBounds.X, layout.ContentBounds.Y),
            Color.White,
            1.06f * layout.Scale);
        if (!string.IsNullOrEmpty(subtitle))
        {
            DrawShadowedMenuBitmapFontText(
                subtitle,
                new Vector2(layout.ContentBounds.X, layout.ContentBounds.Y + (26f * layout.Scale)),
                new Color(236, 236, 236),
                0.58f * layout.Scale);
        }
    }

    private void DrawLastToDieStatsPage(LastToDieMenuLayout layout)
    {
        if (layout.ContentBounds == Rectangle.Empty)
        {
            return;
        }

        var stats = _lastToDieStats;
        var lines = new[]
        {
            $"Highest round completed: {stats.HighestRoundCompleted}",
            $"Most damage: {stats.MostDamageSingleRun}",
            $"Most healing: {stats.MostHealingSingleRun}",
            $"Longest combo: {stats.LongestComboSingleRun}",
            $"Total damage: {stats.TotalDamageLifetime}",
            stats.HasRecordedRun
                ? $"Last run: Round {stats.LastRunRound}, {FormatLastToDieMenuDuration(stats.LastRunElapsedTicks)}"
                : "Last run: None",
        };

        var lineY = layout.ContentBounds.Y + (70f * layout.Scale);
        var lineScale = 0.67f * layout.Scale;
        var lineSpacing = Math.Max(20f, 26f * layout.Scale);
        for (var index = 0; index < lines.Length; index += 1)
        {
            DrawShadowedMenuBitmapFontText(lines[index], new Vector2(layout.ContentBounds.X, lineY), Color.White, lineScale);
            lineY += lineSpacing;
        }
    }

    private void DrawLastToDieDifficultyPage(LastToDieMenuLayout layout)
    {
        if (layout.ContentBounds == Rectangle.Empty || _lastToDieMenuHoverIndex != 1)
        {
            return;
        }

        var scale = 0.58f * layout.Scale;
        var prefix = "You and your opponents' max HP is ";
        var emphasis = "reduced to 25";
        var suffix = ".";
        var totalWidth =
            MeasureMenuBitmapFontWidth(prefix, scale)
            + MeasureMenuBitmapFontWidth(emphasis, scale)
            + MeasureMenuBitmapFontWidth(suffix, scale);
        var startX = layout.ContentBounds.X + ((layout.ContentBounds.Width - totalWidth) * 0.5f);
        var y = layout.ButtonBounds.Length > 0
            ? layout.ButtonBounds[^1].Bottom + (22f * layout.Scale)
            : layout.ContentBounds.Y + (76f * layout.Scale);
        var position = new Vector2(startX, y);
        DrawShadowedMenuBitmapFontText(prefix, position, Color.White, scale);
        position.X += MeasureMenuBitmapFontWidth(prefix, scale);
        DrawShadowedMenuBitmapFontText(emphasis, position, new Color(235, 58, 58), scale);
        position.X += MeasureMenuBitmapFontWidth(emphasis, scale);
        DrawShadowedMenuBitmapFontText(suffix, position, Color.White, scale);
    }

    private static string FormatLastToDieMenuDuration(int ticks)
    {
        if (ticks <= 0)
        {
            return "0:00";
        }

        var totalSeconds = Math.Max(0, (int)MathF.Round(ticks / (float)SimulationConfig.DefaultTicksPerSecond));
        var minutes = totalSeconds / 60;
        var seconds = totalSeconds % 60;
        return $"{minutes}:{seconds:00}";
    }

    private void DrawLastToDieMenuButton(LoadedSpriteFrame? texture, Rectangle bounds, string label, bool hovered, float plaqueScale)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        if (texture is not null)
        {
            DrawLoadedSpriteFrame(texture, bounds, hovered ? new Color(224, 224, 224) : Color.White);
        }
        else
        {
            _spriteBatch.Draw(_pixel, bounds, hovered ? new Color(178, 178, 178) : new Color(118, 118, 118));
        }

        DrawCenteredShadowedMenuBitmapFontText(label, bounds, Color.White, plaqueScale, 1f);
    }

    private void DrawCenteredShadowedMenuBitmapFontText(string text, Rectangle bounds, Color color, float plaqueScale, float textScaleMultiplier)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var textScale = GetMenuFontScaleToFit(text, bounds.Width - MathF.Max(14f, 26f * plaqueScale), bounds.Height - MathF.Max(8f, 12f * plaqueScale)) * textScaleMultiplier;
        var maxTextScale = GetMenuFontScaleToFit(text, bounds.Width - 2f, bounds.Height - 2f);
        textScale = MathF.Min(textScale, maxTextScale);
        var measuredWidth = MeasureMenuBitmapFontWidth(text, textScale);
        var lineHeight = MeasureMenuBitmapFontHeight(textScale);
        var position = new Vector2(
            bounds.X + ((bounds.Width - measuredWidth) * 0.5f),
            bounds.Y + ((bounds.Height - lineHeight) * 0.5f));
        DrawShadowedMenuBitmapFontText(text, position, color, textScale);
    }

    private void DrawShadowedMenuBitmapFontText(string text, Vector2 position, Color color, float scale)
    {
        DrawMenuBitmapFontText(text, position + new Vector2(2f, 2f), Color.Black * 0.82f, scale);
        DrawMenuBitmapFontText(text, position, color, scale);
    }

    private void DrawLastToDieMenuLogo(int viewportWidth)
    {
        EnsureLastToDieLogoTexture();
        if (_lastToDieLogoTexture is null)
        {
            return;
        }

        const float targetWidth = 300f;
        var scale = targetWidth / Math.Max(1f, _lastToDieLogoTexture.Width);
        var targetHeight = _lastToDieLogoTexture.Height * scale;
        var destination = new Rectangle(
            (int)MathF.Round(viewportWidth - targetWidth - 36f),
            32,
            (int)MathF.Round(targetWidth),
            (int)MathF.Round(targetHeight));
        DrawLoadedSpriteFrame(_lastToDieLogoTexture, destination, Color.White);
    }

    private void EnsureLastToDieLogoTexture()
    {
        var path = ContentRoot.GetPath("Sprites", "Menu", "LastToDie", "last2die.png");
        if (string.IsNullOrWhiteSpace(path) || !CanLoadSpriteFrameFromPath(path))
        {
            DisposeLastToDieLogoTexture();
            return;
        }

        if (_lastToDieLogoTexture is not null
            && string.Equals(_lastToDieLogoTexturePath, path, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        DisposeLastToDieLogoTexture();
        _lastToDieLogoTexture = LoadSpriteFrameFromPath(path);
        _lastToDieLogoTexturePath = path;
    }

    private void DisposeLastToDieLogoTexture()
    {
        _lastToDieLogoTexture?.Dispose();
        _lastToDieLogoTexture = null;
        _lastToDieLogoTexturePath = null;
    }
}
