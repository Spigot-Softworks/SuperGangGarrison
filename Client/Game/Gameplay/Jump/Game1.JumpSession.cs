#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenGarrison.Client;

public partial class Game1
{
    private static readonly string[] JumpMapPrefixes = ["dj_", "rr_", "jt_", "rj_"];
    private static readonly PlayerClass[] JumpAllowedClasses = [PlayerClass.Soldier, PlayerClass.Demoman];

    private sealed class JumpRunState
    {
        public JumpRunState(string levelName, string displayName, PlayerClass classId)
        {
            LevelName = levelName;
            DisplayName = displayName;
            ClassId = classId;
        }

        public string LevelName { get; }

        public string DisplayName { get; }

        public PlayerClass ClassId { get; }

        public int ElapsedTicks { get; set; }
    }

    private bool _jumpMenuOpen;
    private int _jumpMenuHoverIndex = -1;
    private List<PracticeMapEntry> _jumpMapEntries = new();
    private int _jumpMapIndex;
    private PlayerClass _jumpSelectedClass = PlayerClass.Soldier;
    private JumpRunState? _jumpRun;

    private bool IsJumpSessionActive => _gameplaySessionKind == GameplaySessionKind.Jump;

    private void OpenJumpMenu(string? statusMessage = null)
    {
        _mainMenuOverlayStateController.OpenJumpMenu(statusMessage);
    }

    private void CloseJumpMenu(bool clearStatus = false)
    {
        _mainMenuOverlayStateController.CloseJumpMenu(clearStatus);
    }

    private void ResetJumpState()
    {
        _jumpRun = null;
    }

    private void PrepareJumpMenuMapEntries()
    {
        var previousLevelName = _jumpMapIndex >= 0 && _jumpMapIndex < _jumpMapEntries.Count
            ? _jumpMapEntries[_jumpMapIndex].LevelName
            : null;
        _jumpMapEntries = BuildPracticeMapEntries()
            .Where(IsJumpMapEntry)
            .OrderBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (_jumpMapEntries.Count == 0)
        {
            _jumpMapIndex = 0;
            return;
        }

        var preservedIndex = string.IsNullOrWhiteSpace(previousLevelName)
            ? -1
            : _jumpMapEntries.FindIndex(entry => string.Equals(entry.LevelName, previousLevelName, StringComparison.OrdinalIgnoreCase));
        _jumpMapIndex = preservedIndex >= 0
            ? preservedIndex
            : Math.Clamp(_jumpMapIndex, 0, _jumpMapEntries.Count - 1);
    }

    private static bool IsJumpMapEntry(PracticeMapEntry entry)
    {
        if (!entry.IsCustomMap)
        {
            return false;
        }

        var levelName = Path.GetFileNameWithoutExtension(entry.LevelName);
        return JumpMapPrefixes.Any(prefix => levelName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private void UpdateJumpMenu(KeyboardState keyboard, MouseState mouse)
    {
        var buttonLabels = GetJumpMenuButtonLabels();
        if (buttonLabels.Length == 0)
        {
            return;
        }

        var layout = GetLastToDieMenuLayout(buttonLabels.Length, statsPage: false);
        if (IsKeyPressed(keyboard, Keys.Escape) || IsControllerMenuBackPressed())
        {
            CloseJumpMenu();
            return;
        }

        if (IsKeyPressed(keyboard, Keys.Up))
        {
            SetJumpMenuHoverIndex((_jumpMenuHoverIndex <= 0 ? buttonLabels.Length : _jumpMenuHoverIndex) - 1, buttonLabels.Length);
        }
        else if (IsKeyPressed(keyboard, Keys.Down))
        {
            SetJumpMenuHoverIndex((_jumpMenuHoverIndex + 1 + buttonLabels.Length) % buttonLabels.Length, buttonLabels.Length);
        }

        if (TryConsumeControllerMenuNavigation(out var horizontalStep, out var verticalStep))
        {
            if (verticalStep != 0)
            {
                SetJumpMenuHoverIndex(MoveControllerMenuSelection(_jumpMenuHoverIndex, buttonLabels.Length, verticalStep), buttonLabels.Length);
            }
            else if (horizontalStep != 0)
            {
                CycleJumpMenuSelection(_jumpMenuHoverIndex, horizontalStep);
            }
        }

        var hoveredButtonIndex = ShouldUseMouseMenuHover(mouse)
            ? GetHoveredLastToDieMenuButtonIndex(mouse.Position, layout)
            : -1;
        if (hoveredButtonIndex >= 0)
        {
            _jumpMenuHoverIndex = hoveredButtonIndex;
        }
        else if (IsControllerMenuInputActive() && _jumpMenuHoverIndex < 0)
        {
            SetJumpMenuHoverIndex(0, buttonLabels.Length);
        }

        if (IsKeyPressed(keyboard, Keys.Left))
        {
            CycleJumpMenuSelection(_jumpMenuHoverIndex, -1);
        }
        else if (IsKeyPressed(keyboard, Keys.Right))
        {
            CycleJumpMenuSelection(_jumpMenuHoverIndex, 1);
        }

        if (IsKeyPressed(keyboard, Keys.Enter) || IsControllerMenuConfirmPressed())
        {
            ActivateJumpMenuButton(_jumpMenuHoverIndex >= 0 ? _jumpMenuHoverIndex : 0);
            return;
        }

        var clickPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed;
        if (clickPressed && _jumpMenuHoverIndex >= 0)
        {
            ActivateJumpMenuButton(_jumpMenuHoverIndex);
        }
    }

    private string[] GetJumpMenuButtonLabels()
    {
        if (_jumpMapEntries.Count == 0)
        {
            return ["Back"];
        }

        var mapEntry = GetSelectedJumpMapEntry();
        return
        [
            "Start",
            $"Map: {mapEntry?.DisplayName ?? "None"}",
            $"Class: {GetJumpClassLabel(_jumpSelectedClass)}",
            "Back",
        ];
    }

    private PracticeMapEntry? GetSelectedJumpMapEntry()
    {
        return _jumpMapIndex >= 0 && _jumpMapIndex < _jumpMapEntries.Count
            ? _jumpMapEntries[_jumpMapIndex]
            : null;
    }

    private void ActivateJumpMenuButton(int index)
    {
        if (_jumpMapEntries.Count == 0)
        {
            CloseJumpMenu();
            return;
        }

        switch (index)
        {
            case 0:
                TryStartJumpRun();
                break;
            case 1:
                CycleJumpMap(1);
                break;
            case 2:
                CycleJumpClass(1);
                break;
            case 3:
                CloseJumpMenu();
                break;
        }
    }

    private void CycleJumpMenuSelection(int index, int direction)
    {
        if (_jumpMapEntries.Count == 0)
        {
            return;
        }

        switch (index)
        {
            case 1:
                CycleJumpMap(direction);
                break;
            case 2:
                CycleJumpClass(direction);
                break;
        }
    }

    private void SetJumpMenuHoverIndex(int index, int itemCount)
    {
        _jumpMenuHoverIndex = itemCount <= 0
            ? -1
            : Math.Clamp(index, 0, itemCount - 1);
    }

    private void CycleJumpMap(int direction)
    {
        if (_jumpMapEntries.Count == 0)
        {
            _jumpMapIndex = 0;
            return;
        }

        _jumpMapIndex = (_jumpMapIndex + direction + _jumpMapEntries.Count) % _jumpMapEntries.Count;
    }

    private void CycleJumpClass(int direction)
    {
        var currentIndex = Array.IndexOf(JumpAllowedClasses, _jumpSelectedClass);
        if (currentIndex < 0)
        {
            currentIndex = 0;
        }

        var nextIndex = (currentIndex + direction + JumpAllowedClasses.Length) % JumpAllowedClasses.Length;
        _jumpSelectedClass = JumpAllowedClasses[nextIndex];
    }

    private bool TryStartJumpRun()
    {
        PrepareJumpMenuMapEntries();
        var mapEntry = GetSelectedJumpMapEntry();
        if (mapEntry is null)
        {
            _menuStatusMessage = "No staged Jump maps found.";
            return false;
        }

        if (!IsJumpClass(_jumpSelectedClass))
        {
            _jumpSelectedClass = PlayerClass.Soldier;
        }

        ResetJumpState();
        var experimentalSettings = _practiceExperimentalGameplaySettings with
        {
            DisableSelfDamage = true,
            EnableSelfDamageHealing = false,
        };
        if (!_gameplaySessionController.TryBeginOfflineBotSession(
                mapEntry.LevelName,
                GameplaySessionKind.Jump,
                SimulationConfig.DefaultTicksPerSecond,
                experimentalSettings,
                timeLimitMinutes: 60,
                capLimit: 255,
                respawnSeconds: 0,
                enableInstantRedTeamIntelCaptureWin: false,
                openJoinMenus: false,
                consoleSessionName: "Jump"))
        {
            return false;
        }

        PrepareJumpSoloWorld(_jumpSelectedClass);
        _jumpRun = new JumpRunState(mapEntry.LevelName, mapEntry.DisplayName, _jumpSelectedClass);
        _menuStatusMessage = string.Empty;
        InvalidateDiscordRichPresenceRefresh();
        UpdateDiscordRichPresence();
        return true;
    }

    private void PrepareJumpSoloWorld(PlayerClass playerClass)
    {
        _world.DespawnEnemyDummy();
        _world.DespawnFriendlyDummy();
        _world.SetLocalPlayerTeam(PlayerTeam.Red);
        _world.PrepareLocalPlayerJoin();
        _world.CompleteLocalPlayerJoin(playerClass);
    }

    private void DrawJumpMenu()
    {
        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), new Color(4, 6, 10, 220));

        if (_menuBackgroundMode != MenuBackgroundMode.Static)
        {
            const int bottomBarHeight = 76;
            var barY = viewportHeight - bottomBarHeight;
            var bottomBarBounds = new Rectangle(0, barY, viewportWidth, bottomBarHeight);
            _spriteBatch.Draw(_pixel, bottomBarBounds, new Color(0x57, 0x4f, 0x47));
            _menuBottomBarRunners.Draw(bottomBarBounds);
        }

        var buttonLabels = GetJumpMenuButtonLabels();
        var layout = GetLastToDieMenuLayout(buttonLabels.Length, statsPage: false);
        var plaqueTexture = _lastToDieMenuPlaqueTexture ?? _menuPlaqueTexture;
        var buttonTexture = _lastToDieMenuTextBoxSoloTexture ?? _menuTextBoxSoloTexture;
        if (plaqueTexture is not null && layout.PlaqueBounds != Rectangle.Empty)
        {
            DrawLoadedSpriteFrame(plaqueTexture, layout.PlaqueBounds, Color.White);
        }

        for (var index = 0; index < layout.ButtonBounds.Length && index < buttonLabels.Length; index += 1)
        {
            DrawLastToDieMenuButton(buttonTexture, layout.ButtonBounds[index], buttonLabels[index], hovered: index == _jumpMenuHoverIndex, layout.Scale);
        }

        if (!string.IsNullOrWhiteSpace(_menuStatusMessage))
        {
            DrawShadowedMenuBitmapFontText(
                _menuStatusMessage,
                new Vector2(layout.PlaqueBounds.X, viewportHeight - 42f),
                Color.White,
                0.72f);
        }
    }

    private void AdvanceJumpSimulationTick()
    {
        if (!IsJumpSessionActive || _jumpRun is null)
        {
            return;
        }

        _jumpRun.ElapsedTicks += 1;
    }

    private void DrawJumpHud()
    {
        if (!IsJumpSessionActive || _jumpRun is null)
        {
            return;
        }

        DrawTimerFontTextRightAligned(
            FormatHudTimerText(_jumpRun.ElapsedTicks),
            new Vector2(ViewportWidth - 18f, 18f),
            Color.White,
            1f);
    }

    private bool IsClassAllowedForCurrentGameplaySession(PlayerClass selectedClass)
    {
        return !IsJumpSessionActive || IsJumpClass(selectedClass);
    }

    private static bool IsJumpClass(PlayerClass playerClass)
    {
        return playerClass is PlayerClass.Soldier or PlayerClass.Demoman;
    }

    private PlayerClass GetRandomJumpClass()
    {
        return JumpAllowedClasses[_visualRandom.Next(JumpAllowedClasses.Length)];
    }

    private static string GetJumpClassLabel(PlayerClass playerClass)
    {
        return playerClass == PlayerClass.Demoman ? "Detonator" : "Rocketman";
    }
}
