#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const float JumpPadBuildNoticeCost = 50f;
    private const float DispenserBuildNoticeCost = 100f;
    private bool _buildMenuNumericInputConsumed;
    private bool _buildMenuSentrySelectionPressed;
    private bool _buildMenuDispenserSelectionPressed;
    private bool _buildMenuDestroySentrySelectionPressed;
    private bool _buildMenuDestroyDispenserSelectionPressed;

    private void ResetBuildMenuInputSelection()
    {
        _buildMenuNumericInputConsumed = false;
        _buildMenuSentrySelectionPressed = false;
        _buildMenuDispenserSelectionPressed = false;
        _buildMenuDestroySentrySelectionPressed = false;
        _buildMenuDestroyDispenserSelectionPressed = false;
    }

    private PlayerInputSnapshot ApplyBuildMenuInputSelection(PlayerInputSnapshot input)
    {
        if (!_buildMenuNumericInputConsumed)
        {
            return input;
        }

        return input with
        {
            BuildSentry = _buildMenuSentrySelectionPressed,
            BuildDispenser = _buildMenuDispenserSelectionPressed,
            DestroySentry = _buildMenuDestroySentrySelectionPressed,
            DestroyDispenser = _buildMenuDestroyDispenserSelectionPressed,
        };
    }

    private void DrawBuildMenuHud()
    {
        if (!_buildMenuOpen && !_hudEditorOpen)
        {
            return;
        }

        if (_world.LocalPlayer.ClassId != PlayerClass.Engineer
            || !CanDrawGameplayBuildHud()
            || !TryResolveHudElement(HudElementId.ClassEngineerBuildMenu, out var resolved))
        {
            return;
        }

        var frameIndex = _world.LocalPlayer.Team == PlayerTeam.Blue ? 1 : 0;
        const float DefaultBuildMenuX = 37f;
        var animatedOrigin = resolved.Origin + new Vector2(
            _hudEditorOpen && !_buildMenuOpen ? 0f : _buildMenuX - DefaultBuildMenuX,
            0f);
        var alpha = _hudEditorOpen && !_buildMenuOpen ? 1f : _buildMenuAlpha;
        var scale = new Vector2(resolved.Layout.Scale, resolved.Layout.Scale);
        TryDrawScreenSprite("BuildMenuS", frameIndex, animatedOrigin, Color.White * alpha, scale);
        UpdateHudElementBounds(
            HudElementId.ClassEngineerBuildMenu,
            resolved.Layout.ResolveBounds(animatedOrigin));
    }

    private void UpdateBuildMenuState(KeyboardState keyboard, MouseState mouse)
    {
        ResetBuildMenuInputSelection();

        if (ShouldCloseBuildMenuForGameplayState())
        {
            BeginClosingBuildMenu();
            AdvanceBuildMenuAnimation();
            return;
        }

        if (_scoreboardOpen || _scoreboardAlpha > 0.02f)
        {
            AdvanceBuildMenuAnimation();
            return;
        }

        var player = _world.LocalPlayer;
        if (player.ClassId == PlayerClass.Engineer)
        {
            var onePressed = IsKeyPressed(keyboard, Keys.D1) || IsKeyPressed(keyboard, Keys.NumPad1);
            var twoPressed = IsKeyPressed(keyboard, Keys.D2) || IsKeyPressed(keyboard, Keys.NumPad2);
            var threePressed = IsKeyPressed(keyboard, Keys.D3) || IsKeyPressed(keyboard, Keys.NumPad3);
            var fourPressed = IsKeyPressed(keyboard, Keys.D4) || IsKeyPressed(keyboard, Keys.NumPad4);
            var zeroPressed = IsKeyPressed(keyboard, Keys.D0) || IsKeyPressed(keyboard, Keys.NumPad0);

            if (onePressed)
            {
                _buildMenuNumericInputConsumed = true;
                if (_buildMenuOpen && !_buildMenuClosing)
                {
                    _buildMenuSentrySelectionPressed = true;
                    BeginClosingBuildMenu();
                }
                else
                {
                    ToggleBuildMenu();
                }
            }
            else if (_buildMenuOpen && twoPressed)
            {
                _buildMenuNumericInputConsumed = true;
                _buildMenuDestroySentrySelectionPressed = true;
                BeginClosingBuildMenu();
            }
            else if (_buildMenuOpen && threePressed)
            {
                _buildMenuNumericInputConsumed = true;
                _buildMenuDispenserSelectionPressed = true;
                BeginClosingBuildMenu();
                TryShowEngineerBuildResourceNotice(player, DispenserBuildNoticeCost);
            }
            else if (_buildMenuOpen && fourPressed)
            {
                _buildMenuNumericInputConsumed = true;
                _buildMenuDestroyDispenserSelectionPressed = true;
                BeginClosingBuildMenu();
            }
            else if (_buildMenuOpen && zeroPressed)
            {
                _buildMenuNumericInputConsumed = true;
                BeginClosingBuildMenu();
            }
        }

        var specialPressed = mouse.RightButton == ButtonState.Pressed && _previousMouse.RightButton == ButtonState.Released;
        if (specialPressed)
        {
            BeginClosingBuildMenu();
            HandleEngineerSpecialPressed(player);
        }

        AdvanceBuildMenuAnimation();
    }

    private void HandleEngineerSpecialPressed(PlayerEntity player)
    {
        var ownedSentryCount = GetLocalOwnedSentryCount();
        var canBuildAdditionalSentry = player.ClassId == PlayerClass.Engineer
            && IsOfflineBotSessionActive
            && IsLastToDieSessionActive
            && _lastToDieRun?.ChosenPerks.Contains(LastToDiePerkKind.EngineerOutputInducer) == true
            && ownedSentryCount < 2;
        if (!canBuildAdditionalSentry && GetLocalOwnedSentry() is not null)
        {
            return;
        }

        var localPlayerId = GetPlayerStateKey(player);
        var ownedSentryBlocksAdditionalBuild = false;
        foreach (var sentry in _world.Sentries)
        {
            if (!sentry.IsNear(player.X, player.Y, 50f))
            {
                continue;
            }

            if (canBuildAdditionalSentry && sentry.OwnerPlayerId == localPlayerId)
            {
                ownedSentryBlocksAdditionalBuild = true;
            }
        }

        if (ownedSentryBlocksAdditionalBuild)
        {
            return;
        }

        if (TryShowEngineerBuildResourceNotice(player, player.MaxMetal))
        {
            return;
        }

        foreach (var sentry in _world.Sentries)
        {
            if (sentry.IsNear(player.X, player.Y, 50f))
            {
                ShowNotice(NoticeKind.TooClose);
                return;
            }
        }

        if (player.IsInSpawnRoom)
        {
            return;
        }
    }

    private bool TryShowEngineerBuildResourceNotice(PlayerEntity player, float requiredMetal)
    {
        if (!IsEngineerBuildResourceInsufficient(GetPlayerMetal(player), requiredMetal))
        {
            return false;
        }

        ShowNotice(NoticeKind.NutsNBolts);
        return true;
    }

    internal static bool IsEngineerBuildResourceInsufficient(float metal, float requiredMetal)
    {
        return metal < requiredMetal;
    }

    private bool HasLocalOwnedJumpPad()
    {
        var localPlayerId = GetPlayerStateKey(_world.LocalPlayer);
        foreach (var jumpPad in _world.JumpPads)
        {
            if (jumpPad.OwnerPlayerId == localPlayerId)
            {
                return true;
            }
        }

        return false;
    }

    private void TryShowEngineerJumpPadBuildNoticeOnUtilityPress(PlayerInputSnapshot input)
    {
        if (!input.UseAbility || _latestPredictedLocalInput.UseAbility)
        {
            return;
        }

        var player = _world.LocalPlayer;
        if (IsLocalSpectatorPresentationActive()
            || player.ClassId != PlayerClass.Engineer
            || !player.IsAlive
            || player.IsInSpawnRoom
            || _world.LocalPlayerAwaitingJoin
            || _world.IsPlayerHumiliated(player)
            || HasLocalOwnedJumpPad())
        {
            return;
        }

        if (GetPlayerMetal(player) < JumpPadBuildNoticeCost)
        {
            ShowNotice(NoticeKind.NutsNBolts);
        }
    }

    private void ToggleBuildMenu()
    {
        if (_buildMenuOpen && !_buildMenuClosing)
        {
            BeginClosingBuildMenu();
            return;
        }

        if (_buildMenuOpen && _buildMenuClosing)
        {
            _buildMenuClosing = false;
            _buildMenuAlpha = MathF.Max(_buildMenuAlpha, 0.01f);
            return;
        }

        _buildMenuOpen = true;
        _buildMenuClosing = false;
        _buildMenuAlpha = 0.01f;
        _buildMenuX = -37f;
    }

    private void BeginClosingBuildMenu()
    {
        if (!_buildMenuOpen)
        {
            return;
        }

        _buildMenuClosing = true;
    }

    private void AdvanceBuildMenuAnimation()
    {
        if (!_buildMenuOpen)
        {
            return;
        }

        if (!_buildMenuClosing)
        {
            if (_buildMenuAlpha < 0.99f)
            {
                _buildMenuAlpha = AdvanceOpeningAlpha(_buildMenuAlpha, 0.01f, 0.99f);
            }

            if (_buildMenuX < 37f)
            {
                _buildMenuX = MathF.Min(37f, _buildMenuX + ScaleLegacyUiDistance(15f));
            }

            return;
        }

        if (_buildMenuAlpha > 0.01f)
        {
            _buildMenuAlpha = AdvanceClosingAlpha(_buildMenuAlpha, 0.01f);
        }

        _buildMenuX -= ScaleLegacyUiDistance(15f);
        if (_buildMenuX < -37f)
        {
            _buildMenuOpen = false;
            _buildMenuClosing = false;
        }
    }
}
