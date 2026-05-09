#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const float JumpPadBuildNoticeCost = 50f;

    private void DrawBuildMenuHud()
    {
        if (!_buildMenuOpen)
        {
            return;
        }

        var viewportHeight = ViewportHeight;
        var frameIndex = _world.LocalPlayer.Team == PlayerTeam.Blue ? 1 : 0;
        TryDrawScreenSprite("BuildMenuS", frameIndex, new Vector2(_buildMenuX, viewportHeight / 2f), Color.White * _buildMenuAlpha, Vector2.One);
    }

    private void UpdateBuildMenuState(KeyboardState keyboard, MouseState mouse)
    {
        if (ShouldCloseBuildMenuForGameplayState())
        {
            BeginClosingBuildMenu();
            AdvanceBuildMenuAnimation();
            return;
        }

        var player = _world.LocalPlayer;
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
            if (HasLocalOwnedJumpPad())
            {
                return;
            }

            if (GetPlayerMetal(player) < JumpPadBuildNoticeCost)
            {
                ShowNotice(NoticeKind.NutsNBolts);
            }

            return;
        }

        if (GetPlayerMetal(player) < player.MaxMetal)
        {
            ShowNotice(NoticeKind.NutsNBolts);
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
        if (_networkClient.IsSpectator
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
