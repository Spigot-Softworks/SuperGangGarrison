#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class FrameController
    {
        private readonly Game1 _game;

        public FrameController(Game1 game)
        {
            _game = game;
        }

        public int Update(GameTime gameTime)
        {
            var clientTicks = _game.ConsumeClientTickCount(gameTime);
            if (OperatingSystem.IsBrowser())
            {
                BrowserInputBridge.BeginFrame();
            }

            var windowActive = OperatingSystem.IsBrowser()
                ? BrowserInputBridge.IsFocused
                : _game.IsActive;
            var keyboard = windowActive ? Game1.GetCurrentKeyboardState() : default;
            var rawMouse = _game.GetConstrainedMouseState(Game1.GetCurrentMouseState());
            var mouse = _game.GetScaledMouseState(rawMouse);
            if (windowActive)
            {
                _game._lastKnownMousePosition = new Point(mouse.X, mouse.Y);
            }
            else
            {
                rawMouse = new MouseState(
                    rawMouse.X,
                    rawMouse.Y,
                    0,
                    ButtonState.Released,
                    ButtonState.Released,
                    ButtonState.Released,
                    ButtonState.Released,
                    ButtonState.Released);
                mouse = new MouseState(
                    mouse.X,
                    mouse.Y,
                    0,
                    ButtonState.Released,
                    ButtonState.Released,
                    ButtonState.Released,
                    ButtonState.Released,
                    ButtonState.Released);
                _game._lastKnownMousePosition = new Point(mouse.X, mouse.Y);
            }
            if (!_game._wasWindowActive && windowActive)
            {
                _game._previousKeyboard = keyboard;
                _game._previousMouse = mouse;
            }

            if (OperatingSystem.IsBrowser() && windowActive)
            {
                foreach (var character in BrowserInputBridge.DrainTextInput())
                {
                    _game.HandleBrowserTextInput(character);
                }
            }

            _game._clientPluginPreviousKeyboard = _game._previousKeyboard;
            _game._clientPluginKeyboard = keyboard;
            _game._wasWindowActive = windowActive;

            if (TryHandlePasswordPromptCancel(keyboard, mouse))
            {
                return clientTicks;
            }

            var muteAudioPressed = keyboard.IsKeyDown(Keys.F12) && !_game._previousKeyboard.IsKeyDown(Keys.F12);
            if (muteAudioPressed)
            {
                _game.ToggleAudioMute();
            }

            var toggleConsolePressed = InputBindingInput.IsPressed(
                _game._inputBindings.ToggleConsole,
                keyboard,
                _game._previousKeyboard,
                mouse,
                _game._previousMouse);
            if (toggleConsolePressed && !_game._mainMenuOpen)
            {
                _game._consoleOpen = !_game._consoleOpen;
                if (_game._consoleOpen)
                {
                    _game.InitializeConsoleInputCursor();
                }
            }

            _game.UpdateConsoleScrollState(keyboard, mouse);

            _game.HandleActiveTextFieldKeyboardShortcuts(keyboard, gameTime.ElapsedGameTime.TotalSeconds);

            if (TryUpdateNonGameplayFrame(gameTime, keyboard, mouse, clientTicks))
            {
                return clientTicks;
            }

            UpdateGameplayFrame(gameTime, keyboard, mouse, rawMouse, clientTicks);
            return clientTicks;
        }

        public void Draw(GameTime gameTime)
        {
            if (!TryDrawNonGameplayFrame())
            {
                DrawGameplayFrame(gameTime);
            }
        }

        private bool TryHandlePasswordPromptCancel(KeyboardState keyboard, MouseState mouse)
        {
            if (!_game._passwordPromptOpen || !keyboard.IsKeyDown(Keys.Escape) || _game._previousKeyboard.IsKeyDown(Keys.Escape))
            {
                return false;
            }

            _game.ReturnToMainMenu("Password entry canceled.");
            _game._previousKeyboard = keyboard;
            _game._previousMouse = mouse;
            _game.IsMouseVisible = !_game.ShouldUseSoftwareMenuCursor();
            return true;
        }

        private bool TryUpdateNonGameplayFrame(GameTime gameTime, KeyboardState keyboard, MouseState mouse, int clientTicks)
        {
            if (_game._startupSplashOpen)
            {
                _game.AdvanceStartupSplashTicks(clientTicks, keyboard, mouse);
                _game._world.SetLocalInput(default);
                _game._previousKeyboard = keyboard;
                _game._previousMouse = mouse;
                _game.IsMouseVisible = false;
                return true;
            }

            if (!_game._mainMenuOpen)
            {
                return false;
            }

            _game.AdvanceMenuClientTicks(clientTicks);
            _game._menuController.Update(gameTime, keyboard, mouse);
            if (_game._networkClient.IsConnected)
            {
                _game.ProcessNetworkMessages();
            }

            _game._world.SetLocalInput(default);
            _game._previousKeyboard = keyboard;
            _game._previousMouse = mouse;
            _game.IsMouseVisible = !_game._mainMenuChromeHidden && !_game.ShouldUseSoftwareMenuCursor();
            return true;
        }

        private void UpdateGameplayFrame(GameTime gameTime, KeyboardState keyboard, MouseState mouse, MouseState rawMouse, int clientTicks)
        {
            _game._gameplayController.UpdateFrame(gameTime, keyboard, mouse, rawMouse, clientTicks);
        }

        private bool TryDrawNonGameplayFrame()
        {
            if (_game._startupSplashOpen)
            {
                _game.BeginLogicalFrame(new Color(24, 32, 48));
                _game.DrawStartupSplash();
                _game.DrawVersionOverlay();
                _game.EndLogicalFrame();
                return true;
            }

            if (!_game._mainMenuOpen)
            {
                return false;
            }

            _game.BeginLogicalFrame(new Color(24, 32, 48));
            _game._menuController.Draw();
            if (!_game._mainMenuChromeHidden && _game.ShouldDrawSoftwareMenuCursor())
            {
                _game.DrawSoftwareMenuCursor(_game.GetScaledMouseState(_game.GetConstrainedMouseState(Game1.GetCurrentMouseState())));
            }

            if (!_game._mainMenuChromeHidden)
            {
                _game.DrawVersionOverlay();
            }

            _game.EndLogicalFrame();
            return true;
        }

        private void DrawGameplayFrame(GameTime gameTime)
        {
            _game._gameplayController.DrawFrame(gameTime);
        }

        public void DrawGameplayWorldForCamera(Vector2 cameraPosition, int viewportWidth, int viewportHeight, int? skippedDeadBodySourcePlayerId = null)
        {
            _game._gameplayController.DrawGameplayWorldForCamera(cameraPosition, viewportWidth, viewportHeight, skippedDeadBodySourcePlayerId);
        }
    }
}
