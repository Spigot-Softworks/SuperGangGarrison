#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void UpdateClassSelect(MouseState mouse)
    {
        if (!_classSelectOpen)
        {
            _classSelectHoverIndex = -1;
            ResetClassSelectPortraitAnimation();
            if (_classSelectAlpha > 0.01f)
            {
                _classSelectAlpha = AdvanceClosingAlpha(_classSelectAlpha, 0.01f);
            }

            if (_classSelectPanelY > -120f)
            {
                _classSelectPanelY = MathF.Max(-120f, _classSelectPanelY - ScaleLegacyUiDistance(15f));
            }

            return;
        }

        var keyboard = GetCurrentKeyboardState();
        if (IsClassSelectCivilianShortcutPressed(keyboard))
        {
            if (ApplyDirectClassSelection(PlayerClass.Quote))
            {
                CloseGameplaySelectionMenus();
            }

            return;
        }

        if (TryGetClassSelectHotkeySelection(keyboard, out var hotkeyClass))
        {
            if (ApplyDirectClassSelection(hotkeyClass))
            {
                CloseGameplaySelectionMenus();
            }

            return;
        }

        if (_classSelectAlpha < 0.99f)
        {
            _classSelectAlpha = AdvanceOpeningAlpha(_classSelectAlpha, 0.01f, 0.99f);
        }

        if (_classSelectPanelY < 120f)
        {
            _classSelectPanelY = MathF.Min(120f, _classSelectPanelY + ScaleLegacyUiDistance(15f));
        }

        var panelLeft = GetClassSelectPanelLeft(ViewportWidth);
        var mouseHoverIndex = GetClassSelectHoverIndex(mouse.X, mouse.Y, panelLeft);
        if (ShouldUseMouseMenuHover(mouse) && mouseHoverIndex >= 0)
        {
            _classSelectHoverIndex = mouseHoverIndex;
        }
        else if (!IsControllerMenuInputActive())
        {
            _classSelectHoverIndex = -1;
        }

        if (TryConsumeControllerMenuNavigation(out var horizontalStep, out _) && horizontalStep != 0)
        {
            _classSelectHoverIndex = MoveControllerMenuSelectionClamped(_classSelectHoverIndex, 10, horizontalStep);
        }
        else if (IsControllerMenuInputActive() && _classSelectHoverIndex < 0)
        {
            _classSelectHoverIndex = 0;
        }

        AdvanceClassSelectPortraitAnimation();
        if (IsControllerMenuBackPressed())
        {
            CloseGameplaySelectionMenus();
            return;
        }

        var clickPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed;
        if ((!clickPressed && !IsControllerMenuConfirmPressed()) || _classSelectHoverIndex < 0)
        {
            return;
        }

        SuppressPrimaryFireUntilMouseRelease();
        if (ApplyClassSelection(_classSelectHoverIndex))
        {
            CloseGameplaySelectionMenus();
        }
    }

    private bool IsClassSelectCivilianShortcutPressed(KeyboardState keyboard)
    {
        return (keyboard.IsKeyDown(Keys.Q) && !_previousKeyboard.IsKeyDown(Keys.Q))
            || (IsControllerMenuInputActive() && IsControllerButtonPressed(ControllerCallMedicButton));
    }

    private void DrawClassSelectHud()
    {
        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        var panelLeft = GetClassSelectPanelLeft(viewportWidth);
        var alpha = Math.Clamp(_classSelectAlpha, 0.01f, 0.99f);
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * MathF.Min(0.8f, alpha));
        DrawClassSelectBackground(panelLeft, viewportWidth, alpha);

        var previewTeam = _pendingClassSelectTeam ?? _world.LocalPlayerTeam;
        if (_classSelectPanelY >= 120f && _classSelectHoverIndex >= 0 && _classSelectHoverIndex < 10)
        {
            var teamOffset = previewTeam == PlayerTeam.Blue ? 10 : 0;
            var drawX = GetClassSelectDrawX(_classSelectHoverIndex);
            var previewPosition = new Vector2(panelLeft + drawX, 0f);
            TryDrawScreenSprite("ClassSelectSpritesS", _classSelectHoverIndex + teamOffset, previewPosition, Color.White * alpha, Vector2.One);

            var lines = GetClassSelectDescription(_classSelectHoverIndex);
            float[] lineY = [80f, 100f, 120f, 130f, 140f];
            var lineCount = Math.Min(lines.Length, lineY.Length);
            for (var index = 0; index < lineCount; index += 1)
            {
                DrawBitmapFontText(lines[index], new Vector2(panelLeft + 495f, lineY[index]), Color.White * alpha, 1f);
            }
        }

        if (_classSelectPanelY >= 120f
            && _classSelectPortraitAnimationHoverIndex >= 0
            && _classSelectPortraitAnimationHoverIndex <= 9
            && !TryDrawClassSelectPortraitAnimation(
                _classSelectPortraitAnimationHoverIndex,
                _classSelectPortraitAnimationTeam ?? previewTeam,
                new Vector2(panelLeft + 230f, 128f),
                Color.White * alpha))
        {
            ResetClassSelectPortraitAnimation();
        }

    }

    private void DrawClassSelectBackground(float panelLeft, int viewportWidth, float alpha)
    {
        var stretchWidth = MathF.Max(0f, viewportWidth - panelLeft - 800f);
        if (stretchWidth > 0f)
        {
            TryDrawScreenSprite("ClassSelectBS", 0, new Vector2(panelLeft + 800f, _classSelectPanelY), Color.White * alpha, new Vector2(stretchWidth, 1f));
        }

        TryDrawScreenSprite("ClassSelectS", 0, new Vector2(panelLeft + 400f, _classSelectPanelY), Color.White * alpha, Vector2.One);
    }

    private static float GetClassSelectPanelLeft(int viewportWidth)
    {
        return viewportWidth >= 800
            ? 0f
            : (viewportWidth / 2f) - 400f;
    }

    private static int GetClassSelectHoverIndex(int mouseX, int mouseY, float panelLeft)
    {
        if (mouseY >= 50)
        {
            return -1;
        }

        int[] leftEdges = [24, 64, 104, 156, 196, 236, 288, 328, 368, 420];
        for (var index = 0; index < leftEdges.Length; index += 1)
        {
            var left = panelLeft + leftEdges[index];
            if (mouseX > left && mouseX < left + 36f)
            {
                return index;
            }
        }

        return -1;
    }

    private bool ApplyClassSelection(int hoverIndex)
    {
        var selectedClass = hoverIndex switch
        {
            0 => PlayerClass.Scout,
            1 => PlayerClass.Pyro,
            2 => PlayerClass.Soldier,
            3 => PlayerClass.Heavy,
            4 => PlayerClass.Demoman,
            5 => PlayerClass.Medic,
            6 => PlayerClass.Engineer,
            7 => PlayerClass.Spy,
            8 => PlayerClass.Sniper,
            _ => GetRandomPlayableClass(),
        };

        return ApplyDirectClassSelection(selectedClass);
    }

    private bool ApplyDirectClassSelection(PlayerClass selectedClass)
    {
        if (!CharacterClassCatalog.RuntimeRegistry.TryGetClassBinding(selectedClass, out _))
        {
            _menuStatusMessage = $"{selectedClass} is not available.";
            return false;
        }

        if (!IsClassAllowedForCurrentGameplaySession(selectedClass))
        {
            _menuStatusMessage = "Jump allows Rocketman or Detonator.";
            return false;
        }

        WarmBrowserPlayableClassAssets(selectedClass, _pendingClassSelectTeam ?? _world.LocalPlayerTeam);

        if (_networkClient.IsConnected)
        {
            ResetLocalPredictionForAuthorityTransition();
            _networkClient.QueueClassSelection(selectedClass);
            return true;
        }
        else
        {
            ApplyOfflineClassSelection(selectedClass);
            return true;
        }
    }

    private PlayerClass GetRandomPlayableClass()
    {
        if (IsJumpSessionActive)
        {
            return GetRandomJumpClass();
        }

        PlayerClass[] classes =
        [
            PlayerClass.Scout,
            PlayerClass.Pyro,
            PlayerClass.Soldier,
            PlayerClass.Heavy,
            PlayerClass.Demoman,
            PlayerClass.Medic,
            PlayerClass.Engineer,
            PlayerClass.Spy,
            PlayerClass.Sniper,
            PlayerClass.Quote,
        ];

        var availableClasses = new PlayerClass[classes.Length];
        var availableClassCount = 0;
        for (var index = 0; index < classes.Length; index += 1)
        {
            if (CharacterClassCatalog.RuntimeRegistry.TryGetClassBinding(classes[index], out _))
            {
                availableClasses[availableClassCount] = classes[index];
                availableClassCount += 1;
            }
        }

        return availableClassCount == 0
            ? PlayerClass.Scout
            : availableClasses[_visualRandom.Next(availableClassCount)];
    }

    private static int GetClassSelectDrawX(int hoverIndex)
    {
        int[] drawX = [24, 64, 104, 156, 196, 236, 288, 328, 368, 420];
        return drawX[Math.Clamp(hoverIndex, 0, drawX.Length - 1)];
    }

    private bool TryGetClassSelectHotkeySelection(KeyboardState keyboard, out PlayerClass selectedClass)
    {
        selectedClass = default;
        var pressedDigit = GetPressedDigit(keyboard);
        if (!pressedDigit.HasValue)
        {
            return false;
        }

        selectedClass = pressedDigit.Value switch
        {
            1 => PlayerClass.Scout,
            2 => PlayerClass.Pyro,
            3 => PlayerClass.Soldier,
            4 => PlayerClass.Heavy,
            5 => PlayerClass.Demoman,
            6 => PlayerClass.Medic,
            7 => PlayerClass.Engineer,
            8 => PlayerClass.Spy,
            9 => PlayerClass.Sniper,
            0 => GetRandomPlayableClass(),
            _ => default,
        };
        return true;
    }

    private static string[] GetClassSelectDescription(int hoverIndex)
    {
        return hoverIndex switch
        {
            0 => ["Runner", "Weapon: Scattergun", "Quick as the wind, the Runner", "excels in recovering objectives.", "He can double jump in mid-air."],
            1 => ["Firebug", "Weapon: Flamethrower", "Gets close to burn his foes.", "Pushes enemies and projectiles", "away with a burst of air."],
            2 => ["Rocketman", "Weapon: Rocket Launcher", "A fierce front-line fighter.", "Uses rocket jumps to traverse", "the map at great speed."],
            3 => ["Overweight", "Weapon: Minigun", "Slow but tough, he lays down", "a torrent of lead and takes", "a lot of punishment."],
            4 => ["Detonator", "Weapon: Grenade Launcher", "Fills chokepoints with explosives", "and can sticky jump to reach", "new positions."],
            5 => ["Healer", "Weapon: Syringe Gun", "Keeps teammates alive and builds", "up ubercharge for brief", "invulnerability."],
            6 => ["Constructor", "Weapon: Shotgun", "Builds sentries and support gear", "to lock down territory.", string.Empty],
            7 => ["Infiltrator", "Weapon: Revolver", "Uses disguise and cloaking to", "slip behind enemy lines and", "strike at key targets."],
            8 => ["Marksman", "Weapon: Sniper Rifle", "Picks enemies off from afar", "with charged shots and good", "positioning."],
            _ => ["Random", string.Empty, "Let fate decide your role", "for this life.", string.Empty],
        };
    }

    private void AdvanceClassSelectPortraitAnimation()
    {
        if (_classSelectPanelY < 120f)
        {
            ResetClassSelectPortraitAnimation();
            return;
        }

        var previewTeam = _pendingClassSelectTeam ?? _world.LocalPlayerTeam;
        if (_classSelectHoverIndex >= 0
            && (_classSelectPortraitAnimationHoverIndex != _classSelectHoverIndex
                || _classSelectPortraitAnimationTeam != previewTeam))
        {
            _classSelectPortraitAnimationHoverIndex = _classSelectHoverIndex;
            _classSelectPortraitAnimationTeam = previewTeam;
            _classSelectPortraitAnimationFrame = 0f;
            return;
        }

        if (_classSelectPortraitAnimationHoverIndex < 0)
        {
            return;
        }

        var spriteName = GetClassSelectPortraitAnimationSpriteName(_classSelectPortraitAnimationHoverIndex);
        if (spriteName is null)
        {
            return;
        }

        var sprite = GetResolvedSprite(spriteName);
        if (sprite is null || sprite.Frames.Count == 0)
        {
            return;
        }

        var perTeamFrames = sprite.Frames.Count / 2;
        if (perTeamFrames <= 0)
        {
            _classSelectPortraitAnimationFrame = 0f;
            return;
        }

        var maxFrame = perTeamFrames - 1;
        if (maxFrame <= 0)
        {
            _classSelectPortraitAnimationFrame = 0f;
            return;
        }

        _classSelectPortraitAnimationFrame = MathF.Min(maxFrame, _classSelectPortraitAnimationFrame + GetClassSelectPortraitAnimationAdvance(_clientUpdateElapsedSeconds));
    }

    private static float GetClassSelectPortraitAnimationAdvance(float clientUpdateElapsedSeconds)
    {
        var elapsedSeconds = Math.Min(clientUpdateElapsedSeconds, 1f / ClientUpdateTicksPerSecond);
        return 0.4f * LegacyMovementModel.SourceTicksPerSecond * elapsedSeconds;
    }

    private bool TryDrawClassSelectPortraitAnimation(int hoverIndex, PlayerTeam previewTeam, Vector2 position, Color tint)
    {
        var spriteName = GetClassSelectPortraitAnimationSpriteName(hoverIndex);
        if (spriteName is null)
        {
            return false;
        }

        var sprite = GetResolvedSprite(spriteName);
        if (sprite is null || sprite.Frames.Count == 0)
        {
            return false;
        }

        var perTeamFrames = sprite.Frames.Count / 2;
        if (perTeamFrames <= 0)
        {
            return false;
        }

        var teamOffset = previewTeam == PlayerTeam.Blue ? perTeamFrames : 0;
        var frameIndex = teamOffset + Math.Clamp((int)MathF.Floor(_classSelectPortraitAnimationFrame), 0, perTeamFrames - 1);
        return TryDrawScreenSprite(spriteName, frameIndex, position, tint, new Vector2(4f, 4f));
    }

    private static string? GetClassSelectPortraitAnimationSpriteName(int hoverIndex)
    {
        return hoverIndex switch
        {
            0 => "ScoutPortraitAnimationS",
            1 => "PyroPortraitAnimationS",
            2 => "SoldierPortraitanimationS",
            3 => "HeavyPortraitAnimationS",
            4 => "DemomanPortraitAnimationS",
            5 => "MedicPortraitAnimationS",
            6 => "EngineerPortraitAnimationS",
            7 => "SpyPortraitAnimationS",
            8 => "SniperPortraitAnimationS",
            9 => "RandomPortraitAnimationS",
            _ => null,
        };
    }

    private void ResetClassSelectPortraitAnimation()
    {
        _classSelectPortraitAnimationHoverIndex = -1;
        _classSelectPortraitAnimationTeam = null;
        _classSelectPortraitAnimationFrame = 0f;
    }
}
