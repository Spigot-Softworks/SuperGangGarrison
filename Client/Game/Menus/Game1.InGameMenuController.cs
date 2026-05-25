#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Client.Plugins;
using System;
using System.Collections.Generic;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class InGameMenuController
    {
        private readonly Game1 _game;

        public InGameMenuController(Game1 game)
        {
            _game = game;
        }

        public void OpenInGameMenu()
        {
            _game._inGameMenuOpen = true;
            _game._inGameMenuAwaitingEscapeRelease = true;
            _game._inGameMenuHoverIndex = -1;
            _game._clientPowersOpen = false;
            _game._clientPowersOpenedFromGameplay = false;
            _game._optionsMenuOpen = false;
            _game._pluginOptionsMenuOpen = false;
            _game._controlsMenuOpen = false;
            _game.DismissCustomBubbleEditor();
            _game._editingPlayerName = false;
            _game._pendingControlsBinding = null;
        }

        public void CloseInGameMenu()
        {
            _game._inGameMenuOpen = false;
            _game._inGameMenuAwaitingEscapeRelease = false;
            _game._inGameMenuHoverIndex = -1;
        }

        public void UpdateInGameMenu(KeyboardState keyboard, MouseState mouse)
        {
            var items = GetInGameMenuActions();
            GetInGameMenuLayout(items.Count, out _, out var itemBounds, out _, out _);

            if (_game._inGameMenuAwaitingEscapeRelease)
            {
                if (!keyboard.IsKeyDown(Keys.Escape))
                {
                    _game._inGameMenuAwaitingEscapeRelease = false;
                }
            }
            else if (_game.IsKeyPressed(keyboard, Keys.Escape))
            {
                CloseInGameMenu();
                return;
            }

            _game._inGameMenuHoverIndex = -1;
            for (var index = 0; index < itemBounds.Length; index += 1)
            {
                if (itemBounds[index].Contains(mouse.Position))
                {
                    _game._inGameMenuHoverIndex = index;
                    break;
                }
            }

            var clickPressed = mouse.LeftButton == ButtonState.Pressed && _game._previousMouse.LeftButton != ButtonState.Pressed;
            if (!clickPressed || _game._inGameMenuHoverIndex < 0)
            {
                return;
            }

            items[_game._inGameMenuHoverIndex].Activate();
        }

        public void DrawInGameMenu()
        {
            var viewportWidth = _game.ViewportWidth;
            var viewportHeight = _game.ViewportHeight;
            _game._spriteBatch.Draw(_game._pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * 0.66f);

            var items = GetInGameMenuActions();
            GetInGameMenuLayout(items.Count, out var plaqueBounds, out var itemBounds, out var plaqueTexture, out var plaqueScale);
            if (plaqueTexture is not null)
            {
                _game.DrawLoadedSpriteFrame(plaqueTexture, plaqueBounds, Color.White);
            }
            else
            {
                _game.DrawMenuPanelBackdrop(plaqueBounds, 0.84f);
            }

            for (var index = 0; index < items.Count; index += 1)
            {
                var buttonTexture = _game.GetMenuStackedButtonTexture(index, items.Count);
                _game.DrawPlaqueMenuButton(
                    buttonTexture,
                    itemBounds[index],
                    items[index].Label,
                    index == _game._inGameMenuHoverIndex,
                    plaqueScale,
                    1f);
            }
        }

        private void GetInGameMenuLayout(int itemCount, out Rectangle plaqueBounds, out Rectangle[] itemBounds, out LoadedSpriteFrame? plaqueTexture, out float plaqueScale)
        {
            itemCount = Math.Max(1, itemCount);
            var useTallPlaque = itemCount >= 5;
            plaqueTexture = useTallPlaque ? _game._menuPlaqueTallTexture : _game._menuPlaqueTexture;

            var availableHeight = _game.ViewportHeight - 84f;
            var maxScale = _game.ViewportHeight < 540 ? 0.52f : 0.58f;
            plaqueScale = plaqueTexture is null
                ? maxScale
                : MathF.Max(0.42f, MathF.Min(maxScale, availableHeight / Math.Max(1f, plaqueTexture.Height)));

            var sideInset = (int)MathF.Round(10f * plaqueScale);
            var topInset = (int)MathF.Round(20f * plaqueScale);
            var bottomInset = (int)MathF.Round(14f * plaqueScale);
            var itemGap = Math.Max(4, (int)MathF.Round(10f * plaqueScale));

            var plaqueWidth = plaqueTexture is null
                ? (int)MathF.Round((_game.ViewportHeight < 540 ? 190f : 210f) * plaqueScale)
                : (int)MathF.Round(plaqueTexture.Width * plaqueScale);
            var fallbackHeight = Math.Max(20, (int)MathF.Round(48f * plaqueScale));
            var contentHeight = topInset + bottomInset;
            for (var index = 0; index < itemCount; index += 1)
            {
                var buttonTexture = _game.GetMenuStackedButtonTexture(index, itemCount);
                var buttonHeight = buttonTexture is null ? fallbackHeight : (int)MathF.Round(buttonTexture.Height * plaqueScale);
                contentHeight += buttonHeight;
                if (index < itemCount - 1)
                {
                    contentHeight += itemGap;
                }
            }

            var minimumTextureHeight = plaqueTexture is null
                ? 0
                : (int)MathF.Round(plaqueTexture.Height * plaqueScale * 0.68f);
            var plaqueHeight = Math.Max(contentHeight, minimumTextureHeight);

            var plaqueX = (int)MathF.Round(MathF.Max(18f, _game.ViewportWidth * 0.035f));
            var plaqueY = Math.Max(24, ((_game.ViewportHeight - plaqueHeight) / 2) + 18);
            plaqueBounds = new Rectangle(plaqueX, plaqueY, Math.Max(1, plaqueWidth), Math.Max(1, plaqueHeight));

            itemBounds = new Rectangle[itemCount];
            var currentY = plaqueBounds.Y + topInset;
            var fallbackWidth = Math.Max(120, plaqueBounds.Width - sideInset * 2);

            for (var index = 0; index < itemCount; index += 1)
            {
                var buttonTexture = _game.GetMenuStackedButtonTexture(index, itemCount);
                var buttonWidth = buttonTexture is null ? fallbackWidth : (int)MathF.Round(buttonTexture.Width * plaqueScale);
                var buttonHeight = buttonTexture is null ? fallbackHeight : (int)MathF.Round(buttonTexture.Height * plaqueScale);
                var buttonX = plaqueBounds.X + sideInset;
                itemBounds[index] = new Rectangle(buttonX, currentY, Math.Max(1, buttonWidth), Math.Max(1, buttonHeight));
                currentY += buttonHeight + itemGap;
            }
        }

        private List<MenuPageAction> GetInGameMenuActions()
        {
            if (_game.IsLastToDieSessionActive)
            {
                var lastToDieActions = new List<MenuPageAction>
                {
                    new("Resume", CloseInGameMenu),
                    new("Options", () =>
                    {
                        _game.OpenOptionsMenu(fromGameplay: true);
                        CloseInGameMenu();
                    }),
                    new("Loadout", () =>
                    {
                        _game.OpenGameplayLoadoutMenu();
                        CloseInGameMenu();
                    }),
                    new("Leave Last To Die", () => _game.ReturnToLastToDieMenu("Last To Die ended.")),
                    new("Quit Game", _game.OpenQuitPrompt),
                };
                if (_game._debugMenuEnabled)
                {
                    lastToDieActions.Insert(3, new MenuPageAction("Debug", () =>
                    {
                        _game.OpenDebugMenu();
                        CloseInGameMenu();
                    }));
                }
                _game.AddPluginMenuActions(lastToDieActions, ClientPluginMenuLocation.InGameMenu, insertIndex: 1);
                return lastToDieActions;
            }

            if (_game.IsPracticeSessionActive)
            {
                var practiceActions = new List<MenuPageAction>
                {
                    new("Resume", CloseInGameMenu),
                    new("Options", () =>
                    {
                        _game.OpenOptionsMenu(fromGameplay: true);
                        CloseInGameMenu();
                    }),
                    new("Practice Setup", _game.OpenPracticeSetupMenu),
                    new("Experimental Settings", () => _game.OpenClientPowersMenu(fromGameplay: true)),
                    new("Restart Practice", () =>
                    {
                        CloseInGameMenu();
                        _game.RestartPracticeSession();
                    }),
                    new("Leave Practice", () => _game.ReturnToMainMenu(_game.GetGameplayExitStatusMessage())),
                    new("Quit Game", _game.OpenQuitPrompt),
                };
                if (_game.CanOpenGameplayLoadoutMenu())
                {
                    practiceActions.Insert(1, new MenuPageAction("Loadout", () =>
                    {
                        _game.OpenGameplayLoadoutMenu();
                        CloseInGameMenu();
                    }));
                }

                if (_game._debugMenuEnabled)
                {
                    var insertIndex = _game.CanOpenGameplayLoadoutMenu() ? 6 : 5;
                    practiceActions.Insert(insertIndex, new MenuPageAction("Debug", () =>
                    {
                        _game.OpenDebugMenu();
                        CloseInGameMenu();
                    }));
                }

                _game.AddPluginMenuActions(practiceActions, ClientPluginMenuLocation.InGameMenu, insertIndex: 1);
                return practiceActions;
            }

            var defaultActions = new List<MenuPageAction>
            {
                new("Resume", CloseInGameMenu),
                new("Options", () =>
                {
                    _game.OpenOptionsMenu(fromGameplay: true);
                    CloseInGameMenu();
                }),
                new("Disconnect", () => _game.ReturnToMainMenu(_game.GetGameplayExitStatusMessage())),
                new("Quit Game", _game.OpenQuitPrompt),
            };
            if (_game.CanOpenGameplayLoadoutMenu())
            {
                defaultActions.Insert(1, new MenuPageAction("Loadout", () =>
                {
                    _game.OpenGameplayLoadoutMenu();
                    CloseInGameMenu();
                }));
            }

            if (_game._debugMenuEnabled)
            {
                var insertIndex = _game.CanOpenGameplayLoadoutMenu() ? 2 : 1;
                defaultActions.Insert(insertIndex, new MenuPageAction("Debug", () =>
                {
                    _game.OpenDebugMenu();
                    CloseInGameMenu();
                }));
            }

            _game.AddPluginMenuActions(defaultActions, ClientPluginMenuLocation.InGameMenu, insertIndex: 1);
            return defaultActions;
        }
    }
}
