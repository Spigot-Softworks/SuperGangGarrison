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

            var wasWindowActive = _game._wasWindowActive;
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
                rawMouse = CreateReleasedMouseState(rawMouse);
                mouse = CreateReleasedMouseState(mouse);
                _game._lastKnownMousePosition = new Point(mouse.X, mouse.Y);
            }

            if (wasWindowActive && !windowActive)
            {
                _game.HandleWindowFocusLost(mouse);
            }
            else if (!wasWindowActive && windowActive)
            {
                _game._previousKeyboard = keyboard;
                _game._previousMouse = mouse;
            }
            _game.UpdateControllerInputState(windowActive, keyboard, mouse);

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
                _game.EnsureWindowInactiveInputReleased(windowActive, mouse);
                return clientTicks;
            }

            var muteAudioPressed = keyboard.IsKeyDown(Keys.F12) && !_game._previousKeyboard.IsKeyDown(Keys.F12);
            if (muteAudioPressed)
            {
                _game.ToggleAudioMute();
            }

            var toggleFullscreenPressed = keyboard.IsKeyDown(Keys.F11) && !_game._previousKeyboard.IsKeyDown(Keys.F11);
            if (toggleFullscreenPressed)
            {
                _game.ToggleFullscreenHotkey();
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
            _game.UpdateMenuStatusMessageExpiry();

            if (TryUpdateNonGameplayFrame(gameTime, keyboard, mouse, clientTicks))
            {
                _game.EnsureWindowInactiveInputReleased(windowActive, mouse);
                return clientTicks;
            }

            UpdateGameplayFrame(gameTime, keyboard, mouse, rawMouse, clientTicks);
            _game.EnsureWindowInactiveInputReleased(windowActive, mouse);
            return clientTicks;
        }

        public void Draw(GameTime gameTime)
        {
            if (!TryDrawNonGameplayFrame())
            {
                DrawGameplayFrame(gameTime);
            }
        }

        private static MouseState CreateReleasedMouseState(MouseState mouse)
        {
            return new MouseState(
                mouse.X,
                mouse.Y,
                0,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released);
        }

        private bool TryHandlePasswordPromptCancel(KeyboardState keyboard, MouseState mouse)
        {
            var escapePressed = keyboard.IsKeyDown(Keys.Escape) && !_game._previousKeyboard.IsKeyDown(Keys.Escape);
            if (!_game._passwordPromptOpen || (!escapePressed && !_game.IsControllerMenuBackPressed()))
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
            _game.DrawLoadingOverlay();
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
            _game.DrawLoadingOverlay();
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

    private void HandleWindowFocusLost(MouseState releasedMouse)
    {
        _previousKeyboard = default;
        _previousMouse = releasedMouse;
        _clientPluginPreviousKeyboard = default;
        _clientPluginKeyboard = default;
        _world.SetLocalInput(default);
        _suppressPrimaryFireUntilMouseRelease = false;
        _suppressSecondaryFireUntilMouseRelease = false;
        _autoFireActive = false;
        ScrollbarDrag.Clear();
        ResetTextFieldClickTarget();
        ResetBubbleMenuInteractionState();
        ResetClientPluginBubbleMenuInputState();
        _hostMapPreviewPanActive = false;
        _playerCardDraggingPortrait = false;
        _playerCardDraggingColorWheel = false;
        _builderPlacementDragging = false;
        _builderEraseDragging = false;
        _builderLayerOffsetDragging = false;
        _builderPanelDragTarget = LegacyBuilderPanelDragTarget.None;
        _builderPanelDragHeaderToggleCandidate = false;
        IsMouseVisible = true;
    }

    private void EnsureWindowInactiveInputReleased(bool windowActive, MouseState releasedMouse)
    {
        if (windowActive)
        {
            return;
        }

        _world.SetLocalInput(default);
        _previousKeyboard = default;
        _previousMouse = releasedMouse;
        IsMouseVisible = true;
    }
}
