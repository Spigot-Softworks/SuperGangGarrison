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
            const float xbegin = 40f;
            const float ybegin = 300f;
            const float spacing = 30f;
            const float width = 220f;
            var items = GetInGameMenuActions();

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

            if (mouse.X > xbegin && mouse.X < xbegin + width)
            {
                _game._inGameMenuHoverIndex = (int)MathF.Round((mouse.Y - ybegin) / spacing);
                if (_game._inGameMenuHoverIndex < 0 || _game._inGameMenuHoverIndex >= items.Count)
                {
                    _game._inGameMenuHoverIndex = -1;
                }
            }
            else
            {
                _game._inGameMenuHoverIndex = -1;
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
            _game._spriteBatch.Draw(_game._pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * 0.7f);

            var items = GetInGameMenuActions();
            const float xbegin = 40f;
            const float ybegin = 300f;
            const float spacing = 30f;
            const float width = 220f;
            _game.DrawMenuPanelBackdrop(new Rectangle((int)xbegin - 12, (int)ybegin - 24, (int)width + 28, items.Count * (int)spacing + 24), 0.82f);
            _game.DrawMenuPlaqueRows(new Vector2(xbegin, ybegin), items.Count, spacing, width, 0.72f);

            var position = new Vector2(xbegin, ybegin);
            for (var index = 0; index < items.Count; index += 1)
            {
                var color = index == _game._inGameMenuHoverIndex ? Color.Red : Color.White;
                _game.DrawBitmapFontText(items[index].Label, position, color, 1f);
                position.Y += spacing;
            }
        }

        private List<MenuPageAction> GetInGameMenuActions()
        {
            if (_game.IsLastToDieSessionActive)
            {
                var lastToDieActions = new List<MenuPageAction>
                {
                    new("Options", () =>
                    {
                        _game.OpenOptionsMenu(fromGameplay: true);
                        CloseInGameMenu();
                    }),
                    new("Return to Game", CloseInGameMenu),
                    new("Leave Last To Die", () => _game.ReturnToLastToDieMenu("Last To Die ended.")),
                    new("Quit Game", _game.OpenQuitPrompt),
                };
                _game.AddPluginMenuActions(lastToDieActions, ClientPluginMenuLocation.InGameMenu, insertIndex: 1);
                return lastToDieActions;
            }

            if (_game.IsPracticeSessionActive)
            {
                var practiceActions = new List<MenuPageAction>
                {
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
                    new("Return to Game", CloseInGameMenu),
                    new("Leave Practice", () => _game.ReturnToMainMenu(_game.GetGameplayExitStatusMessage())),
                    new("Quit Game", _game.OpenQuitPrompt),
                };
                if (_game.CanOpenGameplayLoadoutMenu())
                {
                    practiceActions.Insert(0, new MenuPageAction("Loadout", () =>
                    {
                        _game.OpenGameplayLoadoutMenu();
                        CloseInGameMenu();
                    }));
                }

                _game.AddPluginMenuActions(practiceActions, ClientPluginMenuLocation.InGameMenu, insertIndex: 1);
                return practiceActions;
            }

            var defaultActions = new List<MenuPageAction>
            {
                new("Options", () =>
                {
                    _game.OpenOptionsMenu(fromGameplay: true);
                    CloseInGameMenu();
                }),
                new("Return to Game", CloseInGameMenu),
                new("Disconnect", () => _game.ReturnToMainMenu(_game.GetGameplayExitStatusMessage())),
                new("Quit Game", _game.OpenQuitPrompt),
            };
            if (_game.CanOpenGameplayLoadoutMenu())
            {
                defaultActions.Insert(0, new MenuPageAction("Loadout", () =>
                {
                    _game.OpenGameplayLoadoutMenu();
                    CloseInGameMenu();
                }));
            }

            _game.AddPluginMenuActions(defaultActions, ClientPluginMenuLocation.InGameMenu, insertIndex: 1);
            return defaultActions;
        }
    }
}
