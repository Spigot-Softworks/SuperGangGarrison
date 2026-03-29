#nullable enable

using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private Vector2 GetCameraTopLeft(int viewportWidth, int viewportHeight, int mouseX, int mouseY)
    {
        Vector2 cameraTopLeft;
        if (_killCamEnabled && !_world.LocalPlayer.IsAlive && _world.LocalDeathCam is not null)
        {
            cameraTopLeft = GetDeathCamCameraTopLeft(viewportWidth, viewportHeight);
            TrackLiveCamera(cameraTopLeft);
            return RoundToSourcePixels(cameraTopLeft);
        }

        var localViewPosition = GetLocalViewPosition();
        if (_world.LocalPlayer.IsAlive && GetPlayerIsSniperScoped(_world.LocalPlayer))
        {
            cameraTopLeft = new Vector2(
                localViewPosition.X + mouseX - viewportWidth,
                localViewPosition.Y + mouseY - viewportHeight);
        }
        else
        {
            var halfViewportWidth = viewportWidth / 2f;
            var halfViewportHeight = viewportHeight / 2f;

            cameraTopLeft = new Vector2(
                localViewPosition.X - halfViewportWidth,
                localViewPosition.Y - halfViewportHeight);
        }

        TrackLiveCamera(cameraTopLeft);
        return RoundToSourcePixels(cameraTopLeft);
    }

    private bool IsRespawnFreeCameraActive()
    {
        return _networkClient.IsSpectator
            || (!_world.LocalPlayerAwaitingJoin
                && !_world.LocalPlayer.IsAlive
                && _world.LocalDeathCam is null);
    }

    private void UpdateRespawnCameraState(float deltaSeconds, KeyboardState keyboard)
    {
        if (!IsRespawnFreeCameraActive())
        {
            _respawnCameraDetached = false;
            _respawnCameraCenter = GetDefaultFreeCameraCenter();
            return;
        }

        if (!_respawnCameraDetached)
        {
            _respawnCameraCenter = GetDefaultFreeCameraCenter();
        }

        if (!IsGameplayInputBlocked())
        {
            var moveAmount = 600f * deltaSeconds;
            var moved = false;

            if (keyboard.IsKeyDown(_inputBindings.MoveLeft))
            {
                _respawnCameraCenter.X -= moveAmount;
                moved = true;
            }
            else if (keyboard.IsKeyDown(_inputBindings.MoveRight))
            {
                _respawnCameraCenter.X += moveAmount;
                moved = true;
            }

            if (keyboard.IsKeyDown(_inputBindings.MoveUp))
            {
                _respawnCameraCenter.Y -= moveAmount;
                moved = true;
            }
            else if (keyboard.IsKeyDown(_inputBindings.MoveDown))
            {
                _respawnCameraCenter.Y += moveAmount;
                moved = true;
            }

            if (moved)
            {
                _respawnCameraDetached = true;
            }
        }

        _respawnCameraCenter = ClampRespawnCameraCenter(_respawnCameraCenter);
    }

    private Vector2 ClampRespawnCameraCenter(Vector2 position)
    {
        var halfViewportWidth = ViewportWidth / 2f;
        var halfViewportHeight = ViewportHeight / 2f;
        var maxX = Math.Max(halfViewportWidth, _world.Bounds.Width - halfViewportWidth);
        var maxY = Math.Max(halfViewportHeight, _world.Bounds.Height - halfViewportHeight);
        return new Vector2(
            Math.Clamp(position.X, halfViewportWidth, maxX),
            Math.Clamp(position.Y, halfViewportHeight, maxY));
    }

    private Vector2 GetLocalViewPosition()
    {
        if (_networkClient.IsSpectator)
        {
            if (_respawnCameraDetached)
            {
                return _respawnCameraCenter;
            }

            var spectatorFocus = GetSpectatorFocusPlayer();
            if (spectatorFocus is not null)
            {
                return GetRenderPosition(spectatorFocus);
            }

            return _respawnCameraCenter;
        }

        if (IsRespawnFreeCameraActive())
        {
            return _respawnCameraCenter;
        }

        if (_networkClient.IsConnected && _world.LocalPlayer.IsAlive)
        {
            if (_hasSmoothedLocalPlayerRenderPosition)
            {
                return _smoothedLocalPlayerRenderPosition;
            }

            if (_hasPredictedLocalPlayerPosition)
            {
                return _predictedLocalPlayerPosition;
            }
        }

        return new Vector2(_world.LocalPlayer.X, _world.LocalPlayer.Y);
    }

    private Vector2 GetDefaultFreeCameraCenter()
    {
        if (_networkClient.IsSpectator)
        {
            var spectatorFocus = GetSpectatorFocusPlayer();
            if (spectatorFocus is not null)
            {
                return GetRenderPosition(spectatorFocus);
            }

            return _respawnCameraCenter;
        }

        return new Vector2(_world.LocalPlayer.X, _world.LocalPlayer.Y);
    }

    private bool IsUsingPredictedLocalState(PlayerEntity player)
    {
        return _networkClient.IsConnected
            && ReferenceEquals(player, _world.LocalPlayer)
            && _hasPredictedLocalActionState;
    }

    private bool GetPlayerIsHeavyEating(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.IsHeavyEating
            : player.IsHeavyEating;
    }

    private int GetPlayerHeavyEatTicksRemaining(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.HeavyEatTicksRemaining
            : player.HeavyEatTicksRemaining;
    }

    private int GetPlayerHeavyEatCooldownTicksRemaining(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.HeavyEatCooldownTicksRemaining
            : player.HeavyEatCooldownTicksRemaining;
    }

    private bool GetPlayerIsSniperScoped(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.IsSniperScoped
            : player.IsSniperScoped;
    }

    private int GetPlayerSniperChargeTicks(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.SniperChargeTicks
            : player.SniperChargeTicks;
    }

    private int GetPlayerSniperRifleDamage(PlayerEntity player)
    {
        if (player.ClassId != PlayerClass.Sniper || !GetPlayerIsSniperScoped(player))
        {
            return PlayerEntity.SniperBaseDamage;
        }

        var chargeTicks = GetPlayerSniperChargeTicks(player);
        return PlayerEntity.SniperBaseDamage + (int)MathF.Floor(MathF.Sqrt(chargeTicks * 125f / 6f));
    }

    private bool GetPlayerIsSpyCloaked(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.IsSpyCloaked
            : player.IsSpyCloaked;
    }

    private float GetPlayerSpyCloakAlpha(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.SpyCloakAlpha
            : player.SpyCloakAlpha;
    }

    private bool GetPlayerIsSpyVisibleToEnemies(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.IsSpyVisibleToEnemies
            : player.IsSpyVisibleToEnemies;
    }

    private float GetPlayerVisibilityAlpha(PlayerEntity player)
    {
        if (!player.IsAlive)
        {
            return 1f;
        }

        var bodyVisibilityScale = GetSpyBackstabBodyVisibilityScale(player);
        if (!GetPlayerIsSpyCloaked(player))
        {
            return bodyVisibilityScale;
        }

        if (_networkClient.IsSpectator)
        {
            return bodyVisibilityScale;
        }

        var cloakAlpha = Math.Clamp(GetPlayerSpyCloakAlpha(player), 0f, 1f);
        if (ReferenceEquals(player, _world.LocalPlayer))
        {
            return Math.Max(cloakAlpha, PlayerEntity.SpyMinAllyCloakAlpha) * bodyVisibilityScale;
        }

        if (player.Team == _world.LocalPlayer.Team)
        {
            var allyAlpha = GetPlayerIsSpyBackstabReady(player)
                ? Math.Max(cloakAlpha, PlayerEntity.SpyMinAllyCloakAlpha)
                : cloakAlpha;
            return allyAlpha * bodyVisibilityScale;
        }

        if (IsSpyHiddenFromLocalViewer(player))
        {
            return 0f;
        }

        return cloakAlpha * bodyVisibilityScale;
    }

    private bool GetPlayerIsSpyBackstabReady(PlayerEntity player)
    {
        if (!IsUsingPredictedLocalState(player))
        {
            return player.IsSpyBackstabReady;
        }

        return _predictedLocalActionState.SpyBackstabWindupTicksRemaining <= 0
            && _predictedLocalActionState.SpyBackstabRecoveryTicksRemaining <= 0;
    }

    private bool GetPlayerIsSpyBackstabAnimating(PlayerEntity player)
    {
        if (!IsUsingPredictedLocalState(player))
        {
            return player.IsSpyBackstabAnimating;
        }

        return _predictedLocalActionState.SpyBackstabVisualTicksRemaining > 0;
    }

    private int GetPlayerSpyBackstabVisualTicksRemaining(PlayerEntity player)
    {
        if (!IsUsingPredictedLocalState(player))
        {
            return player.SpyBackstabVisualTicksRemaining;
        }

        return _predictedLocalActionState.SpyBackstabVisualTicksRemaining;
    }

    private float GetSpyBackstabBodyVisibilityScale(PlayerEntity player)
    {
        if (player.ClassId != PlayerClass.Spy)
        {
            return 1f;
        }

        var visualTicksRemaining = GetPlayerSpyBackstabVisualTicksRemaining(player);
        if (visualTicksRemaining <= 0)
        {
            return 1f;
        }

        const int bodyFadeTicks = 4;
        var elapsedTicks = StabAnimEntity.TotalLifetimeTicks - visualTicksRemaining;
        if (elapsedTicks < bodyFadeTicks)
        {
            return Math.Clamp(1f - (elapsedTicks / (float)bodyFadeTicks), 0f, 1f);
        }

        if (visualTicksRemaining <= StabAnimEntity.FadeOutTicks)
        {
            return Math.Clamp(1f - (visualTicksRemaining / (float)StabAnimEntity.FadeOutTicks), 0f, 1f);
        }

        return 0f;
    }

    private bool IsSpyHiddenFromLocalViewer(PlayerEntity player)
    {
        if (_networkClient.IsSpectator
            || ReferenceEquals(player, _world.LocalPlayer)
            || player.ClassId != PlayerClass.Spy
            || player.Team == _world.LocalPlayer.Team
            || !GetPlayerIsSpyCloaked(player)
            || !_world.LocalPlayer.IsAlive)
        {
            return false;
        }

        return IsSpyHiddenFromLocalViewer(player.Id, player.Team, player.X);
    }

    private bool IsSpyHiddenFromLocalViewer(int ownerId, PlayerTeam ownerTeam, float spyX)
    {
        if (_networkClient.IsSpectator
            || !_world.LocalPlayer.IsAlive
            || ownerId == _world.LocalPlayer.Id
            || ownerTeam == _world.LocalPlayer.Team)
        {
            return false;
        }

        var viewerFacingSign = IsFacingLeftByAim(_world.LocalPlayer) ? -1 : 1;
        return Math.Sign(spyX - _world.LocalPlayer.X) == -viewerFacingSign;
    }

    private float GetPlayerMedicUberCharge(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.MedicUberCharge
            : player.MedicUberCharge;
    }

    private float GetPlayerMetal(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.Metal
            : player.Metal;
    }

    private int GetPlayerCurrentShells(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.CurrentShells
            : player.CurrentShells;
    }

    private int GetPlayerPrimaryCooldownTicks(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.PrimaryCooldownTicks
            : player.PrimaryCooldownTicks;
    }

    private int GetPlayerReloadTicksUntilNextShell(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.ReloadTicksUntilNextShell
            : player.ReloadTicksUntilNextShell;
    }

    private int GetPlayerPyroFlareCooldownTicks(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.PyroFlareCooldownTicks
            : player.PyroFlareCooldownTicks;
    }

    private float GetPlayerIntelRechargeTicks(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.IntelRechargeTicks
            : player.IntelRechargeTicks;
    }

    private int GetPlayerMedicNeedleRefillTicks(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.MedicNeedleRefillTicks
            : player.MedicNeedleRefillTicks;
    }

    private PlayerEntity? GetSpectatorFocusPlayer()
    {
        PlayerEntity? fallback = null;
        foreach (var player in EnumerateRemotePlayersForView())
        {
            if (player.IsAlive)
            {
                return player;
            }

            fallback ??= player;
        }

        return fallback;
    }

    private IEnumerable<PlayerEntity> EnumerateRemotePlayersForView()
    {
        if (_networkClient.IsConnected)
        {
            for (var index = 0; index < _world.RemoteSnapshotPlayers.Count; index += 1)
            {
                yield return _world.RemoteSnapshotPlayers[index];
            }

            yield break;
        }

        if (IsPracticeSessionActive)
        {
            foreach (var bot in EnumeratePracticeBotPlayersForView())
            {
                yield return bot;
            }
        }

        if (_config.EnableEnemyTrainingDummy && _world.EnemyPlayerEnabled)
        {
            yield return _world.EnemyPlayer;
        }

        if (_config.EnableFriendlySupportDummy && _world.FriendlyDummyEnabled)
        {
            yield return _world.FriendlyDummy;
        }
    }

    private static string GetIntelStateLabel(TeamIntelligenceState intelState)
    {
        if (intelState.IsAtBase)
        {
            return "home";
        }

        if (intelState.IsDropped)
        {
            return $"dropped:{intelState.ReturnTicksRemaining}";
        }

        return "carried";
    }

    private PlayerEntity? FindPlayerById(int playerId)
    {
        if ((_localPlayerSnapshotEntityId ?? _world.LocalPlayer.Id) == playerId)
        {
            return _world.LocalPlayer;
        }

        foreach (var player in EnumerateRemotePlayersForView())
        {
            if (player.Id == playerId)
            {
                return player;
            }
        }

        return null;
    }
}
