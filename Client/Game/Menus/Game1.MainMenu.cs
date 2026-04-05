#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.IO;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private bool TryDrawOpenMenuOverlay()
    {
        switch (GetActiveMainMenuOverlay())
        {
            case MainMenuOverlayKind.OptionsMenu:
                DrawOptionsMenu();
                return true;
            case MainMenuOverlayKind.PluginOptionsMenu:
                DrawPluginOptionsMenu();
                return true;
            case MainMenuOverlayKind.ControlsMenu:
                DrawControlsMenu();
                return true;
            case MainMenuOverlayKind.LastToDieMenu:
                DrawLastToDieMenu();
                return true;
            case MainMenuOverlayKind.HostSetup:
                DrawHostSetupMenu();
                return true;
            case MainMenuOverlayKind.ClientPowers:
                DrawClientPowersMenu();
                return true;
            case MainMenuOverlayKind.PracticeSetup:
                DrawPracticeSetupMenu();
                return true;
            case MainMenuOverlayKind.Credits:
                DrawCreditsMenu();
                return true;
            case MainMenuOverlayKind.LobbyBrowser:
                DrawLobbyBrowserMenu();
                return true;
            case MainMenuOverlayKind.ManualConnect:
                DrawManualConnectMenu();
                return true;
            default:
                return false;
        }
    }

    private void UpdateMainMenu(KeyboardState keyboard, MouseState mouse)
    {
        if (IsKeyPressed(keyboard, Keys.Escape))
        {
            if (_optionsMenuOpen)
            {
                CloseOptionsMenu();
                return;
            }

            if (_mainMenuPage != MainMenuPage.Root)
            {
                OpenMainMenuPage(MainMenuPage.Root);
            }
            else
            {
                OpenQuitPrompt();
            }
            return;
        }

        if (_optionsMenuOpen)
        {
            return;
        }

        var buttons = BuildMainMenuButtons();
        _mainMenuHoverIndex = -1;
        _mainMenuBottomBarHover = false;
        for (var index = 0; index < buttons.Count; index += 1)
        {
            if (!buttons[index].Bounds.Contains(mouse.Position))
            {
                continue;
            }

            _mainMenuHoverIndex = index;
            _mainMenuBottomBarHover = buttons[index].IsBottomBarButton;
            break;
        }

        var clickPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed;
        if (clickPressed && _mainMenuHoverIndex >= 0)
        {
            buttons[_mainMenuHoverIndex].Activate();
        }
    }

    private void DrawMainMenu()
    {
        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;

        EnsureMenuBackgroundTexture(viewportWidth, viewportHeight);

        if (_menuBackgroundTexture is not null)
        {
            _spriteBatch.Draw(_menuBackgroundTexture, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.White);
        }
        else if (!TryDrawScreenSprite("MenuBackgroundS", _menuImageFrame, new Vector2(viewportWidth / 2f, viewportHeight / 2f), Color.White, Vector2.One))
        {
            _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), new Color(26, 24, 20));
        }

        DrawMenuBackgroundAttribution();

        if (!TryDrawOpenMenuOverlay())
        {
            var buttons = BuildMainMenuButtons();
            DrawCurrentMainMenuPage(buttons);

            DrawMenuStatusText();
            DrawQuitPrompt();
        }

        DrawDevMessagePopup();
    }

    private void EnsureMenuBackgroundTexture(int viewportWidth, int viewportHeight)
    {
        var (path, attributionText) = GetMenuBackgroundSelection(viewportWidth, viewportHeight);
        _menuBackgroundAttributionText = attributionText;
        if (string.IsNullOrWhiteSpace(path))
        {
            DisposeMenuBackgroundTexture();
            return;
        }

        if (_menuBackgroundTexture is not null
            && string.Equals(_menuBackgroundTexturePath, path, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        DisposeMenuBackgroundTexture();
        using var stream = File.OpenRead(path);
        _menuBackgroundTexture = Texture2D.FromStream(GraphicsDevice, stream);
        _menuBackgroundTexturePath = path;
    }

    private (string? Path, string AttributionText) GetMenuBackgroundSelection(int viewportWidth, int viewportHeight)
    {
        var pluginOverride = GetClientPluginMainMenuBackgroundOverride();
        if (pluginOverride is not null
            && !string.IsNullOrWhiteSpace(pluginOverride.ImagePath)
            && File.Exists(pluginOverride.ImagePath))
        {
            return (pluginOverride.ImagePath, pluginOverride.AttributionText);
        }

        return (GetDefaultMenuBackgroundPath(viewportWidth, viewportHeight), string.Empty);
    }

    private static string? GetDefaultMenuBackgroundPath(int viewportWidth, int viewportHeight)
    {
        var aspectRatio = viewportHeight <= 0 ? (16f / 9f) : viewportWidth / (float)viewportHeight;
        var fileName = aspectRatio <= 1.27f
            ? "background-5x4.png"
            : aspectRatio <= 1.4f
                ? "background-4x3.png"
                : "background.png";
        var path = ContentRoot.GetPath("Sprites", "Menu", "Title", fileName);
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            return path;
        }

        var fallbackPath = ContentRoot.GetPath("Sprites", "Menu", "Title", "background.png");
        return !string.IsNullOrWhiteSpace(fallbackPath) && File.Exists(fallbackPath)
            ? fallbackPath
            : null;
    }

    private void DisposeMenuBackgroundTexture()
    {
        _menuBackgroundTexture?.Dispose();
        _menuBackgroundTexture = null;
        _menuBackgroundTexturePath = null;
    }

    private void DrawMenuBackgroundAttribution()
    {
        if (string.IsNullOrWhiteSpace(_menuBackgroundAttributionText))
        {
            return;
        }

        var scale = ViewportHeight < 540 ? 0.82f : 0.95f;
        var position = new Vector2(ViewportWidth - 18f, ViewportHeight - 18f);
        DrawBitmapFontTextRightAligned(_menuBackgroundAttributionText, position + Vector2.One, Color.Black * 0.75f, scale);
        DrawBitmapFontTextRightAligned(_menuBackgroundAttributionText, position, Color.White, scale);
    }

    private void UpdateCreditsMenu(KeyboardState keyboard, MouseState mouse)
    {
        EnsureCreditsViewState();
        if (keyboard.IsKeyDown(Keys.Escape) && !_previousKeyboard.IsKeyDown(Keys.Escape))
        {
            CloseCreditsMenu();
            return;
        }

        var panel = GetCreditsPanelBounds();
        var backBounds = new Rectangle(panel.X + 30, panel.Bottom - 62, 180, 42);
        var clickPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed;
        if (clickPressed && backBounds.Contains(mouse.Position))
        {
            CloseCreditsMenu();
            return;
        }

        var wheelDelta = mouse.ScrollWheelValue - _previousMouse.ScrollWheelValue;
        var scrollStep = 30f;
        if (wheelDelta > 0)
        {
            _creditsScrollY = Math.Min(GetCreditsInitialScrollY(), _creditsScrollY + scrollStep);
        }
        else if (wheelDelta < 0)
        {
            _creditsScrollY = Math.Max(GetCreditsMinimumScrollY(), _creditsScrollY - scrollStep);
        }
        else
        {
            _creditsScrollY = Math.Max(GetCreditsMinimumScrollY(), _creditsScrollY - 2f);
        }
    }

    private void DrawCreditsMenu()
    {
        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * 0.82f);

        var panel = GetCreditsPanelBounds();
        var creditsSprite = _runtimeAssets.GetSprite("CreditsS");
        if (creditsSprite is not null && creditsSprite.Frames.Count > 0)
        {
            var creditsFrame = creditsSprite.Frames[0];
            const float creditsScale = 2f;
            var additionalCreditsScale = GetCreditsAdditionalTextScale();
            var additionalCreditsGap = GetCreditsAdditionalTextGap();
            var lineHeight = GetCreditsAdditionalLineHeight(additionalCreditsScale);
            var creditsX = (viewportWidth - creditsFrame.Width * creditsScale) / 2f;
            var additionalCreditsY = _creditsScrollY + creditsFrame.Height * creditsScale + additionalCreditsGap;
            var additionalCreditsColor = new Color(240, 228, 196);
            _spriteBatch.Draw(
                creditsFrame,
                new Vector2(creditsX, _creditsScrollY),
                null,
                Color.White,
                0f,
                Vector2.Zero,
                new Vector2(creditsScale, creditsScale),
                SpriteEffects.None,
                0f);

            foreach (var line in GetAdditionalCreditsLines())
            {
                DrawBitmapFontTextCentered(
                    line,
                    new Vector2(viewportWidth / 2f, additionalCreditsY),
                    additionalCreditsColor,
                    additionalCreditsScale);
                additionalCreditsY += lineHeight;
            }
        }
        else
        {
            DrawBitmapFontTextCentered("Credits unavailable", new Vector2(viewportWidth / 2f, viewportHeight / 2f), Color.White, 1.2f);
        }

        DrawMenuButton(new Rectangle(panel.X + 30, panel.Bottom - 62, 180, 42), "Back", false);
    }

    private void DrawMenuStatusText()
    {
        if (string.IsNullOrWhiteSpace(_menuStatusMessage))
        {
            return;
        }

        var position = new Vector2(ViewportWidth * 0.5f, ViewportHeight - 104f);
        var shadowPosition = position + Vector2.One * 2f;
        var scale = ViewportHeight < 540 ? 0.75f : 0.9f;
        var text = _menuStatusMessage;
        var shadowSize = _menuFont.MeasureString(text) * scale;
        var textSize = shadowSize;
        _spriteBatch.DrawString(_menuFont, text, shadowPosition - new Vector2(shadowSize.X * 0.5f, 0f), Color.Black * 0.6f, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        _spriteBatch.DrawString(_menuFont, text, position - new Vector2(textSize.X * 0.5f, 0f), new Color(235, 225, 180), 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    private void OpenMainMenuPage(MainMenuPage page)
    {
        _mainMenuPage = page;
        _mainMenuHoverIndex = -1;
        _mainMenuBottomBarHover = false;
    }

    private List<MenuPageButton> BuildMainMenuButtons()
    {
        var buttons = new List<MenuPageButton>();
        var (stackedActions, soloAction, bottomBarLabel, bottomBarAction) = GetCurrentMainMenuActions();
        var layout = GetCenteredPlaqueMenuLayout(tall: false, stackedActions.Count, includeBottomBarButton: bottomBarAction is not null);

        for (var index = 0; index < stackedActions.Count && index < layout.StackedButtonBounds.Length; index += 1)
        {
            buttons.Add(new MenuPageButton(stackedActions[index].Label, layout.StackedButtonBounds[index], stackedActions[index].Activate));
        }

        buttons.Add(new MenuPageButton(soloAction.Label, layout.SoloButtonBounds, soloAction.Activate));

        if (bottomBarAction is not null && layout.BottomBarButtonBounds.HasValue)
        {
            buttons.Add(new MenuPageButton(bottomBarLabel, layout.BottomBarButtonBounds.Value, bottomBarAction, IsBottomBarButton: true));
        }

        return buttons;
    }

    private void DrawCurrentMainMenuPage(IReadOnlyList<MenuPageButton> buttons)
    {
        var (stackedActions, soloAction, bottomBarLabel, bottomBarAction) = GetCurrentMainMenuActions();
        var layout = GetCenteredPlaqueMenuLayout(tall: false, stackedActions.Count, includeBottomBarButton: bottomBarAction is not null);
        var hoveredStackedIndex = -1;
        var soloHovered = false;
        var bottomHovered = false;

        for (var index = 0; index < buttons.Count; index += 1)
        {
            if (index != _mainMenuHoverIndex)
            {
                continue;
            }

            if (buttons[index].IsBottomBarButton)
            {
                bottomHovered = true;
            }
            else if (index < stackedActions.Count)
            {
                hoveredStackedIndex = index;
            }
            else
            {
                soloHovered = true;
            }
        }

        DrawPlaqueMenuLayout(layout, stackedActions, soloAction, bottomBarAction is not null, bottomBarLabel, hoveredStackedIndex, soloHovered, bottomHovered, 1.15f);
    }

    private (List<MenuPageAction> StackedActions, MenuPageAction SoloAction, string BottomBarLabel, Action? BottomBarAction) GetCurrentMainMenuActions()
    {
        return _mainMenuPage switch
        {
            MainMenuPage.PlayOnline => (
                [
                    new MenuPageAction("Host Match", OpenHostSetupMenu),
                    new MenuPageAction("Join (IP)", OpenManualConnectMenu),
                    new MenuPageAction("Join (Lobby)", OpenLobbyBrowser),
                ],
                new MenuPageAction("Back", () => OpenMainMenuPage(MainMenuPage.Root)),
                string.Empty,
                null),
            MainMenuPage.PlayOffline => (
                [
                    new MenuPageAction("Practice", OpenPracticeSetupMenu),
                    new MenuPageAction("Minigames", () => OpenMainMenuPage(MainMenuPage.Minigames)),
                ],
                new MenuPageAction("Back", () => OpenMainMenuPage(MainMenuPage.Root)),
                string.Empty,
                null),
            MainMenuPage.Minigames => (
                [
                    new MenuPageAction("Last to Die", () => OpenLastToDieMenu()),
                ],
                new MenuPageAction("Back", () => OpenMainMenuPage(MainMenuPage.PlayOffline)),
                string.Empty,
                null),
            MainMenuPage.Credits => (
                [
                    new MenuPageAction("Play Credits", OpenCreditsMenu),
                ],
                new MenuPageAction("Back", () => OpenMainMenuPage(MainMenuPage.Root)),
                string.Empty,
                null),
            _ => (
                [
                    new MenuPageAction("Play Online", () => OpenMainMenuPage(MainMenuPage.PlayOnline)),
                    new MenuPageAction("Play Offline", () => OpenMainMenuPage(MainMenuPage.PlayOffline)),
                    new MenuPageAction("Settings", () => OpenOptionsMenu(fromGameplay: false)),
                ],
                new MenuPageAction("Credits", () => OpenMainMenuPage(MainMenuPage.Credits)),
                "Quit",
                OpenQuitPrompt),
        };
    }

    private void OpenManualConnectMenu()
    {
        _manualConnectOpen = true;
        _editingConnectHost = true;
        _editingConnectPort = false;
        _optionsMenuOpen = false;
        _pluginOptionsMenuOpen = false;
        _controlsMenuOpen = false;
        CloseLobbyBrowser(clearStatus: false);
        _creditsOpen = false;
        _editingPlayerName = false;
        _menuStatusMessage = string.Empty;
    }

    private Rectangle GetCreditsPanelBounds()
    {
        return new Rectangle(0, 0, ViewportWidth, ViewportHeight);
    }

    private void OpenCreditsMenu()
    {
        _creditsOpen = true;
        _creditsScrollInitialized = false;
    }

    private void CloseCreditsMenu()
    {
        _creditsOpen = false;
        _creditsScrollInitialized = false;
    }

    private void EnsureCreditsViewState()
    {
        if (_creditsScrollInitialized)
        {
            return;
        }

        _creditsScrollY = GetCreditsInitialScrollY();
        _creditsScrollInitialized = true;
    }

    private float GetCreditsInitialScrollY()
    {
        var panel = GetCreditsPanelBounds();
        var availableTop = 28f;
        var availableBottom = panel.Bottom - 92f;
        var contentHeight = GetCreditsContentHeight();
        return availableTop + MathF.Max(0f, (availableBottom - availableTop - contentHeight) * 0.5f);
    }

    private float GetCreditsMinimumScrollY()
    {
        var panel = GetCreditsPanelBounds();
        var availableBottom = panel.Bottom - 92f;
        return Math.Min(GetCreditsInitialScrollY(), availableBottom - GetCreditsContentHeight());
    }

    private float GetCreditsContentHeight()
    {
        var creditsSprite = _runtimeAssets.GetSprite("CreditsS");
        if (creditsSprite is null || creditsSprite.Frames.Count == 0)
        {
            return 0f;
        }

        const float creditsScale = 2f;
        var contentHeight = creditsSprite.Frames[0].Height * creditsScale;
        if (GetAdditionalCreditsLines().Length == 0)
        {
            return contentHeight;
        }

        return contentHeight
            + GetCreditsAdditionalTextGap()
            + (GetAdditionalCreditsLines().Length * GetCreditsAdditionalLineHeight(GetCreditsAdditionalTextScale()));
    }

    private float GetCreditsAdditionalTextScale()
    {
        return ViewportHeight < 540 ? 1.35f : 1.5f;
    }

    private float GetCreditsAdditionalLineHeight(float scale)
    {
        return MeasureBitmapFontHeight(scale) + 4f;
    }

    private static float GetCreditsAdditionalTextGap()
    {
        return 22f;
    }

    private static string[] GetAdditionalCreditsLines()
    {
        return
        [
            "MonoGame Port by Graves",
            "with help from Soumeh",
            "and KevinKuntz",
        ];
    }
}
