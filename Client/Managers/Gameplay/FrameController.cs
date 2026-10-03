#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class FrameController
    {
        private readonly IGameplayContext _context;
        private readonly OpenGarrison.Client.WindowInputFilter _windowInputFilter = new();
        private bool _wasWindowActive = true;
        private Microsoft.Xna.Framework.Input.KeyboardState _clientPluginPreviousKeyboard;
        private Microsoft.Xna.Framework.Input.KeyboardState _clientPluginKeyboard;
        // Draw code must use the same focus-sanitized mouse sample as Update. Reading
        // Mouse.GetState directly during Draw lets an inactive window click through.
        private Microsoft.Xna.Framework.Input.MouseState _frameMouseState;
        private Microsoft.Xna.Framework.Input.MouseState _frameRawMouseState;
        private bool _suppressFullscreenToggleUntilRelease;

        public FrameController(IGameplayContext context)
        {
            _context = context;
        }

        internal LoadingOverlayState LoadingOverlay { get; } = new();

        internal MouseState FrameRawMouseState => _frameRawMouseState;

        internal MouseState FrameMouseState => _frameMouseState;

        internal KeyboardState ClientPluginKeyboard => _clientPluginKeyboard;

        internal bool WasWindowActive => _wasWindowActive;

        internal bool WasClientPluginKeyPressedThisFrame(Keys key)
        {
            return _clientPluginKeyboard.IsKeyDown(key)
                && !_clientPluginPreviousKeyboard.IsKeyDown(key);
        }

        internal void ResetClientPluginKeyboard()
        {
            _clientPluginPreviousKeyboard = default;
            _clientPluginKeyboard = default;
        }

        internal void RebaseWindowActivation()
        {
            _wasWindowActive = false;
        }

        internal void LoseFocus()
        {
            _windowInputFilter.LoseFocus();
        }

        internal GamePadState FilterController(bool windowActive, GamePadState gamePad, bool hasSelectionActivity)
        {
            return _windowInputFilter.FilterController(windowActive, gamePad, hasSelectionActivity);
        }

        public int Update(GameTime gameTime)
        {
            var clientTicks = _context.ConsumeClientTickCount(gameTime);
            _context.PumpPeerRoom(gameTime.ElapsedGameTime.TotalSeconds);
            _context.UpdateOfflinePracticeMapVote();
            _context.PumpEmbeddedSession(gameTime.ElapsedGameTime.TotalSeconds);
            _context.PumpRunUploads(gameTime.ElapsedGameTime.TotalSeconds);
            _context.UpdateEmbeddedConsoleCommand();
            if (OperatingSystem.IsBrowser())
            {
                BrowserInputBridge.BeginFrame();
            }

            var wasWindowActive = _wasWindowActive;
            var windowActive = _context.IsWindowInputActive;
            var keyboard = windowActive ? Game1.GetCurrentKeyboardState() : default;
            var rawMouse = Game1.GetCurrentMouseState();
            _windowInputFilter.Filter(windowActive, ref keyboard, ref rawMouse);
            if (windowActive) rawMouse = _context.GetConstrainedMouseState(rawMouse);
            var mouse = _context.GetScaledMouseState(rawMouse);
            _context._lastKnownMousePosition = new Point(mouse.X, mouse.Y);
            _frameRawMouseState = rawMouse;
            _frameMouseState = mouse;

            if (wasWindowActive && !windowActive)
            {
                _context.HandleWindowFocusLost(mouse);
            }
            else if (!wasWindowActive && windowActive)
            {
                _context._previousKeyboard = keyboard;
                _context._previousMouse = mouse;
            }
            _context.UpdateControllerInputState(windowActive, keyboard, mouse);

            if (OperatingSystem.IsBrowser() && windowActive)
            {
                foreach (var character in BrowserInputBridge.DrainTextInput())
                {
                    _context.HandleBrowserTextInput(character);
                }
            }

            _clientPluginPreviousKeyboard = _context._previousKeyboard;
            _clientPluginKeyboard = keyboard;
            _wasWindowActive = windowActive;

            if (TryHandlePasswordPromptCancel(keyboard, mouse))
            {
                _context.EnsureWindowInactiveInputReleased(windowActive, mouse);
                return clientTicks;
            }

            var muteAudioPressed = keyboard.IsKeyDown(Keys.F12) && !_context._previousKeyboard.IsKeyDown(Keys.F12);
            if (muteAudioPressed)
            {
                _context.ToggleAudioMute();
            }

            var fullscreenDown = keyboard.IsKeyDown(Keys.F11);
            if (!fullscreenDown)
            {
                _suppressFullscreenToggleUntilRelease = false;
            }

            var toggleFullscreenPressed = fullscreenDown && !_context._previousKeyboard.IsKeyDown(Keys.F11);
            if (toggleFullscreenPressed)
            {
                var deferFullscreenToggle = Game1.ShouldDeferFullscreenToggle(
                    _context._startupSplashOpen,
                    LoadingOverlay.Visible,
                    _context.Gameplay.Bootstrap.IsContentBootstrapComplete,
                    _context.SessionTransitions.LastToDieConnectionPresentationPending);
                if (deferFullscreenToggle)
                {
                    // Consume the edge while startup/loading owns the
                    // presentation.  A held F11 must not toggle as soon as
                    // loading finishes.
                    _suppressFullscreenToggleUntilRelease = true;
                }
                else if (!_suppressFullscreenToggleUntilRelease)
                {
                    _context.ToggleFullscreenHotkey();
                }
            }

            var toggleConsolePressed = InputBindingInput.IsPressed(
                _context._inputBindings.ToggleConsole,
                keyboard,
                _context._previousKeyboard,
                mouse,
                _context._previousMouse);
            if (toggleConsolePressed && !_context._mainMenuOpen)
            {
                _context._consoleOpen = !_context._consoleOpen;
                if (_context._consoleOpen)
                {
                    _context.InitializeConsoleInputCursor();
                }
            }

            _context.UpdateConsoleScrollState(keyboard, mouse);

            _context.HandleActiveTextFieldKeyboardShortcuts(keyboard, gameTime.ElapsedGameTime.TotalSeconds);
            _context.UpdateMenuStatusMessageExpiry();
            _context.UpdateAccountOperation();
            _context.UpdateCrtUnlockSequence(keyboard, gameTime.TotalGameTime);

            if (TryUpdateNonGameplayFrame(gameTime, keyboard, mouse, clientTicks))
            {
                _context.EnsureWindowInactiveInputReleased(windowActive, mouse);
                return clientTicks;
            }

            UpdateGameplayFrame(gameTime, keyboard, mouse, rawMouse, clientTicks);
            _context.EnsureWindowInactiveInputReleased(windowActive, mouse);
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
            var escapePressed = keyboard.IsKeyDown(Keys.Escape) && !_context._previousKeyboard.IsKeyDown(Keys.Escape);
            if (!_context._passwordPromptOpen || (!escapePressed && !_context.IsControllerMenuBackPressed()))
            {
                return false;
            }

            _context.ReturnToMainMenu("Password entry canceled.");
            _context._previousKeyboard = keyboard;
            _context._previousMouse = mouse;
            _context.IsMouseVisible = !_context.ShouldUseSoftwareMenuCursor();
            return true;
        }

        private bool TryUpdateNonGameplayFrame(GameTime gameTime, KeyboardState keyboard, MouseState mouse, int clientTicks)
        {
            if (_context._startupSplashOpen)
            {
                _context.AdvanceStartupSplashTicks(clientTicks, keyboard, mouse);
                _context._world.NetworkPlayerRules.SetLocalInput(default);
                _context._previousKeyboard = keyboard;
                _context._previousMouse = mouse;
                _context.IsMouseVisible = false;
                return true;
            }

            if (!_context._mainMenuOpen)
            {
                return false;
            }

            _context.AdvanceMenuClientTicks(clientTicks);
            _context.Menus.Menu.Update(gameTime, keyboard, mouse);
            if (_context._networkClient.IsConnected)
            {
                _context.ProcessNetworkMessages();
            }

            _context._world.NetworkPlayerRules.SetLocalInput(default);
            _context._previousKeyboard = keyboard;
            _context._previousMouse = mouse;
            _context.IsMouseVisible = !_context._mainMenuChromeHidden && !_context.ShouldUseSoftwareMenuCursor();
            return true;
        }

        private void UpdateGameplayFrame(GameTime gameTime, KeyboardState keyboard, MouseState mouse, MouseState rawMouse, int clientTicks)
        {
            _context.Gameplay.Gameplay.UpdateFrame(gameTime, keyboard, mouse, rawMouse, clientTicks);
        }

        private bool TryDrawNonGameplayFrame()
        {
            if (_context._startupSplashOpen)
            {
            _context.BeginLogicalFrame(new Color(24, 32, 48));
            _context.DrawStartupSplash();
            _context.DrawLoadingOverlay();
            _context.DrawVersionOverlay();
            _context.EndLogicalFrame();
            return true;
            }

            if (!_context._mainMenuOpen)
            {
                return false;
            }

            _context.BeginLogicalFrame(new Color(24, 32, 48));
            _context.Menus.Menu.Draw();
            _context.DrawLoadingOverlay();
            if (!_context._mainMenuChromeHidden && _context.ShouldDrawSoftwareMenuCursor())
            {
                _context.DrawSoftwareMenuCursor(_context.GetFrameMouseState());
            }

            if (!_context._mainMenuChromeHidden)
            {
                _context.DrawVersionOverlay();
            }

            _context.EndLogicalFrame();
            return true;
        }

        private void DrawGameplayFrame(GameTime gameTime)
        {
            _context.Gameplay.Gameplay.DrawFrame(gameTime);
        }

    public void DrawGameplayWorldForCamera(Vector2 cameraPosition, int viewportWidth, int viewportHeight, int? skippedDeadBodySourcePlayerId = null)
    {
        _context.Gameplay.Gameplay.DrawGameplayWorldForCamera(cameraPosition, viewportWidth, viewportHeight, skippedDeadBodySourcePlayerId);
    }
}

public partial class Game1
{

    public void HandleWindowFocusLost(MouseState releasedMouse)
    {
        _gameplayManager.Frame.LoseFocus();
        _crtUnlockSequenceProgress = 0;
        ReleaseGameplayInputForFocusLoss();
        _previousKeyboard = default;
        _previousMouse = releasedMouse;
        _gameplayManager.Frame.ResetClientPluginKeyboard();
        _suppressPrimaryFireUntilMouseRelease = false;
        _suppressSecondaryFireUntilMouseRelease = false;
        _autoFireActive = false;
        ScrollbarDrag.Clear();
        ResetTextFieldClickTarget();
        ResetBubbleMenuInteractionState();
        BeginClosingBuildMenu();
        ResetBuildMenuInputSelection();
        ResetClientPluginBubbleMenuInputState();
        _hostMapPreviewPanActive = false;
        _playerCardDraggingPortrait = false;
        _playerCardDraggingColorWheel = false;
        if (_builderEditorEnabled) FinishGarrisonBuilderGestures();
        _builderPlacementDragging = false;
        _builderEraseDragging = false;
        _builderLayerOffsetDragging = false;
        _builderPanelDragTarget = LegacyBuilderPanelDragTarget.None;
        _builderPanelDragHeaderToggleCandidate = false;
        IsMouseVisible = true;
    }

    private void ReleaseGameplayInputForFocusLoss()
    {
        _world.NetworkPlayerRules.SetLocalInput(default);
        _localPredictionState.LatestPredictedLocalInput = default;
        _localPredictionState.PreviousPredictedLocalInput = default;
        _latchedJumpPressSequence = 0;
        ClearPendingPredictedInputEdges();
        _currentGamePad = default;
        _previousGamePad = default;
    }

    public void EnsureWindowInactiveInputReleased(bool windowActive, MouseState releasedMouse)
    {
        if (windowActive)
        {
            return;
        }

        _world.NetworkPlayerRules.SetLocalInput(default);
        _previousKeyboard = default;
        _previousMouse = releasedMouse;
        IsMouseVisible = true;
}
}
