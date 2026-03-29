#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private bool TryHandlePasswordPromptCancel(KeyboardState keyboard, MouseState mouse)
    {
        if (!_passwordPromptOpen || !keyboard.IsKeyDown(Keys.Escape) || _previousKeyboard.IsKeyDown(Keys.Escape))
        {
            return false;
        }

        ReturnToMainMenu("Password entry canceled.");
        _previousKeyboard = keyboard;
        _previousMouse = mouse;
        IsMouseVisible = !ShouldUseSoftwareMenuCursor();
        return true;
    }

    private bool TryUpdateNonGameplayFrame(GameTime gameTime, KeyboardState keyboard, MouseState mouse, int clientTicks)
    {
        if (_startupSplashOpen)
        {
            AdvanceStartupSplashTicks(clientTicks, keyboard, mouse);
            _world.SetLocalInput(default);
            _previousKeyboard = keyboard;
            _previousMouse = mouse;
            IsMouseVisible = false;
            return true;
        }

        if (!_mainMenuOpen)
        {
            return false;
        }

        AdvanceMenuClientTicks(clientTicks);
        UpdateMenuState(keyboard, mouse);
        if (_networkClient.IsConnected)
        {
            ProcessNetworkMessages();
        }

        _world.SetLocalInput(default);
        _previousKeyboard = keyboard;
        _previousMouse = mouse;
        IsMouseVisible = !ShouldUseSoftwareMenuCursor();
        return true;
    }

    private void UpdateGameplayFrame(GameTime gameTime, KeyboardState keyboard, MouseState mouse, int clientTicks)
    {
        if (_networkClient.IsConnected)
        {
            ProcessNetworkMessages();
        }

        UpdateGameplayScreenState(keyboard);
        UpdateGameplayMenuState(keyboard, mouse);
        UpdateRespawnCameraState((float)gameTime.ElapsedGameTime.TotalSeconds, keyboard);
        var cameraPosition = GetCameraTopLeft(ViewportWidth, ViewportHeight, mouse.X, mouse.Y);
        var (gameplayInput, networkInput) = BuildGameplayInputs(keyboard, mouse, cameraPosition);
        CapturePendingPredictedInputEdges(keyboard, mouse, networkInput);
        _world.SetLocalInput(gameplayInput);
        UpdateBubbleMenuState(keyboard);
        UpdateScoreboardState(keyboard);
        AdvanceGameplaySimulation(gameTime, networkInput);
        UpdateGameplayPresentation(gameTime, mouse, clientTicks);
        UpdateGameplayWindowState();
        FinalizeGameplayFrame(keyboard, mouse);
    }

    private bool TryDrawNonGameplayFrame()
    {
        if (_startupSplashOpen)
        {
            BeginLogicalFrame(new Color(24, 32, 48));
            DrawStartupSplash();
            EndLogicalFrame();
            return true;
        }

        if (!_mainMenuOpen)
        {
            return false;
        }

        BeginLogicalFrame(new Color(24, 32, 48));
        DrawMainMenu();
        if (ShouldDrawSoftwareMenuCursor())
        {
            DrawSoftwareMenuCursor(GetScaledMouseState(GetConstrainedMouseState(Mouse.GetState())));
        }
        EndLogicalFrame();
        return true;
    }

    private void DrawGameplayFrame(GameTime gameTime)
    {
        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        var mouse = GetScaledMouseState(GetConstrainedMouseState(Mouse.GetState()));
        var cameraPosition = GetCameraTopLeft(viewportWidth, viewportHeight, mouse.X, mouse.Y);
        PrepareDeathCamCaptureIfNeeded(viewportWidth, viewportHeight);

        BeginLogicalFrame(new Color(24, 32, 48));
        if (!DrawDeathCamCaptureOverlay(viewportWidth, viewportHeight))
        {
            DrawGameplayWorldForCamera(cameraPosition, viewportWidth, viewportHeight);
        }
        DrawGameplayHudLayers(mouse, cameraPosition);
        DrawGameplayModalOverlays(mouse);
        EndLogicalFrame();
    }

    private void DrawGameplayWorldForCamera(Vector2 cameraPosition, int viewportWidth, int viewportHeight)
    {
        var worldRectangle = new Rectangle(
            (int)-cameraPosition.X,
            (int)-cameraPosition.Y,
            (int)_world.Bounds.Width,
            (int)_world.Bounds.Height);

        var playerRectangle = GetLocalPlayerRectangle(cameraPosition);

        var mapCenterX = (int)(_world.Bounds.Width / 2f - cameraPosition.X);
        var mapCenterY = (int)(_world.Bounds.Height / 2f - cameraPosition.Y);
        var centerLine = new Rectangle(mapCenterX - 1, 0, 2, viewportHeight);
        var centerColumn = new Rectangle(0, mapCenterY - 1, viewportWidth, 2);
        var worldTopBorder = new Rectangle(worldRectangle.X, worldRectangle.Y, worldRectangle.Width, 4);
        var worldBottomBorder = new Rectangle(worldRectangle.X, worldRectangle.Bottom - 4, worldRectangle.Width, 4);
        var worldLeftBorder = new Rectangle(worldRectangle.X, worldRectangle.Y, 4, worldRectangle.Height);
        var worldRightBorder = new Rectangle(worldRectangle.Right - 4, worldRectangle.Y, 4, worldRectangle.Height);
        var spawnRectangle = new Rectangle(
            (int)(_world.Level.LocalSpawn.X - 8f - cameraPosition.X),
            (int)(_world.Level.LocalSpawn.Y - 8f - cameraPosition.Y),
            16,
            16);

        DrawGameplayWorld(cameraPosition, worldRectangle, playerRectangle, centerLine, centerColumn, worldTopBorder, worldBottomBorder, worldLeftBorder, worldRightBorder, spawnRectangle);
    }
}
