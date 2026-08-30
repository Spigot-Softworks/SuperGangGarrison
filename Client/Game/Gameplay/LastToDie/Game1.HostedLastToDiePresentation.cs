#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;
using OpenGarrison.Protocol;

namespace OpenGarrison.Client;

public partial class Game1
{
    private static readonly IReadOnlyList<OpenGarrison.Core.LastToDie.LastToDieSurvivorDefinition>
        HostedLastToDieSurvivors = OpenGarrison.Core.LastToDie.LastToDieSurvivorCatalog
            .CreateStock()
            .Definitions;
    private static readonly IReadOnlyDictionary<string, OpenGarrison.Core.LastToDie.LastToDiePerkDefinition>
        HostedLastToDiePerks = OpenGarrison.Core.LastToDie.LastToDieExpansionPerkCatalog
            .CreateDefinitions()
             .ToDictionary(definition => definition.Id.Value, StringComparer.Ordinal);
    private readonly Dictionary<byte, bool> _hostedLastToDieObservedAliveBySlot = [];
    private Guid _hostedLastToDieObservedRunId;
    private LastToDieWirePhase? _hostedLastToDieObservedPhase;
    private bool _hostedLastToDieSoloReadyCommandSent;
    private bool _hostedLastToDieSoloStartCommandSent;
    private Point _hostedLastToDieMousePosition;
    private int _hostedLastToDieRewardHoverIndex = -1;
    private float _hostedLastToDieRoomCodeFeedbackSeconds;
    private bool _hostedLastToDieRoomCodeCopyFailed;
    private bool _hostedLastToDieLobbyMousePressArmed = true;
    private bool? _hostedLastToDieOptimisticReadyState;
    private ulong _hostedLastToDieReadyCommandId;
    private bool _hostedLastToDieRetryMusicPending;

    private bool IsHostedLastToDieActive()
        => _networkClient.IsConnected
            && _networkClient.LastToDieState.Snapshot is not null;

    private bool IsHostedLastToDieBlockingGameplay()
        => _networkClient.LastToDieState.Snapshot?.Phase is
            LastToDieWirePhase.Lobby
            or LastToDieWirePhase.SurvivorChoice
            or LastToDieWirePhase.RewardChoice
            or LastToDieWirePhase.LoadingStage
            or LastToDieWirePhase.Won
            or LastToDieWirePhase.Lost;

    private PlayerEntity? GetHostedLastToDieLivingTeammateForView()
    {
        var snapshot = _networkClient.LastToDieState.Snapshot;
        if (!_networkClient.IsConnected
            || snapshot?.Phase != LastToDieWirePhase.Playing
            || _world.LocalPlayer.IsAlive)
        {
            return null;
        }

        foreach (var participant in snapshot.Players)
        {
            if (participant.Slot == _networkClient.LocalPlayerSlot
                || !participant.IsConnected
                || !participant.IsAlive)
            {
                continue;
            }

            if (_world.TryGetNetworkPlayer(participant.Slot, out var teammate)
                && teammate.IsAlive)
            {
                return teammate;
            }
        }

        return null;
    }

    private bool ShouldConsumeHostedLastToDieBackInput()
    {
        var snapshot = _networkClient.LastToDieState.Snapshot;
        if (snapshot?.Phase == LastToDieWirePhase.Lobby)
        {
            return true;
        }

        if (snapshot?.Phase != LastToDieWirePhase.SurvivorChoice)
        {
            return false;
        }

        return snapshot.Players.Any(player =>
            player.Slot == _networkClient.LocalPlayerSlot
            && !string.IsNullOrWhiteSpace(player.SurvivorId));
    }

    private void UpdateHostedLastToDiePresentation(KeyboardState keyboard, MouseState mouse)
    {
        if (!IsHostedLastToDieActive()
            || _networkClient.LastToDieState.Snapshot is not { } snapshot)
        {
            return;
        }

        if (_hostedLastToDieObservedRunId == snapshot.RunId
            && ShouldExitCompletedHostedLastToDieSolo(
                snapshot.MaximumPlayers,
                _hostedLastToDieObservedPhase,
                snapshot.Phase))
        {
            ReturnToLastToDieMenu();
            StopHostedServer();
            return;
        }

        ObserveHostedLastToDiePresentationState(snapshot);
        if (snapshot.Phase != LastToDieWirePhase.Playing
            && _menuBackgroundMode != MenuBackgroundMode.Static)
        {
            // Gameplay entry deliberately disposes the animated menu scene.
            // Recreate and advance that independent scene for hosted LTD menus;
            // otherwise the old gameplay frame remains visible underneath.
            _animatedMenuBackgroundController.Initialize(_menuBackgroundMode);
            var presentationDeltaSeconds = Math.Max(0f, _gameplayPresentationDeltaSeconds);
            _animatedMenuBackgroundController.Update(presentationDeltaSeconds);
            _menuBottomBarRunners.Update(presentationDeltaSeconds);
        }

        if (_consoleOpen
            || _chatOpen
            || _passwordPromptOpen
            || HasOpenGameplayOverlay())
        {
            return;
        }

        CloseGameplaySelectionMenus();
        _hostedLastToDieMousePosition = mouse.Position;
        _hostedLastToDieRoomCodeFeedbackSeconds = Math.Max(
            0f,
            _hostedLastToDieRoomCodeFeedbackSeconds - _gameplayPresentationDeltaSeconds);

        var localPlayer = snapshot.Players.FirstOrDefault(
            player => player.Slot == _networkClient.LocalPlayerSlot);
        if (localPlayer is null)
        {
            return;
        }

        switch (snapshot.Phase)
        {
            case LastToDieWirePhase.Lobby:
                UpdateHostedLastToDieLobby(snapshot, localPlayer, keyboard, mouse);
                break;

            case LastToDieWirePhase.SurvivorChoice:
                UpdateLastToDieSurvivorCarousel(
                    keyboard,
                    mouse,
                    localPlayer.SurvivorId,
                    survivorId => _networkClient.SendLastToDieCommand(
                        LastToDieCommandKind.ChooseSurvivor,
                        survivorId),
                    () => _networkClient.SendLastToDieCommand(LastToDieCommandKind.Unready));
                break;

            case LastToDieWirePhase.RewardChoice:
                var rewardLayout = GetLastToDieChoiceMenuLayout(localPlayer.ActiveOfferChoices.Count);
                _hostedLastToDieRewardHoverIndex = GetLastToDieChoiceHoverIndex(
                    mouse.Position,
                    rewardLayout);
                var rewardIndex = GetHostedLastToDieDigitChoice(
                    keyboard,
                    localPlayer.ActiveOfferChoices.Count);
                var rewardClicked = mouse.LeftButton == ButtonState.Pressed
                    && _previousMouse.LeftButton != ButtonState.Pressed;
                if (rewardIndex < 0 && rewardClicked)
                {
                    rewardIndex = _hostedLastToDieRewardHoverIndex;
                }
                if (rewardIndex >= 0 && localPlayer.ActiveOfferId != 0)
                {
                    _networkClient.SendLastToDieCommand(
                        LastToDieCommandKind.SelectReward,
                        localPlayer.ActiveOfferChoices[rewardIndex],
                        localPlayer.ActiveOfferId);
                }
                break;

        }
    }

    internal static bool ShouldExitCompletedHostedLastToDieSolo(
        int maximumPlayers,
        LastToDieWirePhase? observedPhase,
        LastToDieWirePhase currentPhase)
        => maximumPlayers == 1
            && currentPhase == LastToDieWirePhase.Lobby
            && observedPhase is LastToDieWirePhase.Won or LastToDieWirePhase.Lost;

    private void ObserveHostedLastToDiePresentationState(LastToDieRunSnapshotMessage snapshot)
    {
        EnsureLastToDieSurvivorCarouselAssets();
        if (_hostedLastToDieObservedRunId != snapshot.RunId)
        {
            _hostedLastToDieObservedRunId = snapshot.RunId;
            _hostedLastToDieObservedAliveBySlot.Clear();
            _hostedLastToDieObservedPhase = null;
        }

        // The loading/menu track owns the connection gap only until the
        // authoritative LTD snapshot arrives.  Leaving this set forever
        // makes the music selector return to the menu branch even after the
        // server has entered Playing.
        if (ShouldClearHostedLastToDieConnectionPresentationPending(
                _networkClient.IsConnected,
                snapshot.Phase))
        {
            _lastToDieConnectionPresentationPending = false;
        }

        var enteredLostPhase = _hostedLastToDieObservedPhase != LastToDieWirePhase.Lost
            && snapshot.Phase == LastToDieWirePhase.Lost;
        if (_hostedLastToDieObservedPhase != snapshot.Phase)
        {
            if (snapshot.Phase != LastToDieWirePhase.Lost)
            {
                _hostedLastToDieRetryMusicPending = false;
            }
            _hostedLastToDieRewardHoverIndex = -1;
            if (snapshot.Phase == LastToDieWirePhase.RewardChoice)
            {
                ClearTransientLastToDieOverlaysForHostedRewardChoice();
            }
            if (snapshot.Phase == LastToDieWirePhase.Playing)
            {
                // A future lobby/choice screen should start with a fresh menu
                // scene, not retain the one that preceded this match.
                _animatedMenuBackgroundController.Reset();
            }
            if (snapshot.Phase == LastToDieWirePhase.SurvivorChoice)
            {
                ResetLastToDieSurvivorCarousel();
            }
            if (snapshot.Phase == LastToDieWirePhase.Lobby)
            {
                _hostedLastToDieSoloReadyCommandSent = false;
                _hostedLastToDieSoloStartCommandSent = false;
                _hostedLastToDieLobbyMousePressArmed = true;
                _hostedLastToDieOptimisticReadyState = null;
                _hostedLastToDieReadyCommandId = 0;
                CloseInGameMenu();
            }
            _hostedLastToDieObservedPhase = snapshot.Phase;
        }

        foreach (var player in snapshot.Players)
        {
            if (_hostedLastToDieObservedAliveBySlot.TryGetValue(player.Slot, out var wasAlive)
                && wasAlive
                && !player.IsAlive
                && snapshot.Phase is LastToDieWirePhase.Playing or LastToDieWirePhase.Lost)
            {
                if (player.Slot == _networkClient.LocalPlayerSlot)
                {
                    var teamWiped = snapshot.Phase == LastToDieWirePhase.Lost
                        || !snapshot.Players.Any(candidate =>
                            candidate.Slot != player.Slot
                            && candidate.IsConnected
                            && candidate.IsAlive);
                    TriggerLastToDieDeathFocusFailure(teamWiped);
                }
                else
                {
                    TryPlaySound(_lastToDiePlayerDieSound, 0.95f, 0f, 0f);
                }
            }
            _hostedLastToDieObservedAliveBySlot[player.Slot] = player.IsAlive;
        }

        if (enteredLostPhase)
        {
            var localPlayer = snapshot.Players.FirstOrDefault(
                player => player.Slot == _networkClient.LocalPlayerSlot);
            if (localPlayer?.IsAlive == false)
            {
                // The local player may have died earlier and already been
                // spectating their partner. The partner's later death still
                // has to promote that completed focus into the team failure UI.
                TriggerLastToDieDeathFocusFailure(openFailureOnComplete: true);
            }
            else
            {
                // Objective loss can terminate LTD while a survivor remains
                // alive, so it has no corpse transition to open the choices.
                OpenLastToDieFailureOverlay();
            }
        }
    }

    private void ClearTransientLastToDieOverlaysForHostedRewardChoice()
    {
        // Hosted reward choice owns this screen. Legacy/offline LTD overlays
        // can otherwise remain marked open after a forced or objective-driven
        // stage finish and cause HasOpenGameplayOverlay() to swallow every
        // hover, click, and number-key selection while the cards still draw.
        _lastToDieSurvivorMenuOpen = false;
        _lastToDiePerkMenuOpen = false;
        _lastToDieStageClearOverlayOpen = false;
        _lastToDieStageClearOverlayTicks = 0;
        _lastToDieFailureOverlayOpen = false;
        _lastToDieFailureOverlayTicks = 0;
        ClearLastToDieDeathFocusPresentation();
        CloseInGameMenu();
    }

    private void UpdateHostedLastToDieLobby(
        LastToDieRunSnapshotMessage snapshot,
        LastToDiePlayerSnapshotMessage localPlayer,
        KeyboardState keyboard,
        MouseState mouse)
    {
        if (snapshot.MaximumPlayers == 1)
        {
            if (!localPlayer.IsReady && !_hostedLastToDieSoloReadyCommandSent)
            {
                _hostedLastToDieSoloReadyCommandSent = true;
                _networkClient.SendLastToDieCommand(LastToDieCommandKind.Ready);
            }
            else if (localPlayer.IsReady
                     && localPlayer.IsHost
                     && !_hostedLastToDieSoloStartCommandSent)
            {
                _hostedLastToDieSoloStartCommandSent = true;
                _networkClient.SendLastToDieCommand(LastToDieCommandKind.RequestStart);
            }

            return;
        }

        ReconcileHostedLastToDieReadyCommand(localPlayer);
        var (readyBounds, startBounds) = GetHostedLastToDieLobbyButtonBounds(localPlayer.IsHost);
        var roomCodeBounds = GetHostedLastToDieRoomCodeButtonBounds();
        var exitBounds = GetHostedLastToDieExitButtonBounds();
        if (mouse.LeftButton == ButtonState.Released)
        {
            _hostedLastToDieLobbyMousePressArmed = true;
        }

        var clicked = mouse.LeftButton == ButtonState.Pressed
            && _hostedLastToDieLobbyMousePressArmed;
        if (clicked)
        {
            _hostedLastToDieLobbyMousePressArmed = false;
        }

        if (IsKeyPressed(keyboard, Keys.Escape)
            || IsControllerMenuBackPressed()
            || clicked && exitBounds.Contains(mouse.Position))
        {
            ExitHostedLastToDieLobby();
            return;
        }

        if (localPlayer.IsHost
            && IsHostedServerRunning
            && clicked
            && roomCodeBounds.Contains(mouse.Position))
        {
            _hostedLastToDieRoomCodeCopyFailed = !TrySetClipboardText(_clientIdentity.FriendCode);
            _hostedLastToDieRoomCodeFeedbackSeconds = 2f;
            return;
        }

        if (IsKeyPressed(keyboard, Keys.Space)
            || IsKeyPressed(keyboard, Keys.R)
            || clicked && readyBounds.Contains(mouse.Position))
        {
            if (!_hostedLastToDieOptimisticReadyState.HasValue)
            {
                var targetReady = !localPlayer.IsReady;
                _hostedLastToDieOptimisticReadyState = targetReady;
                _hostedLastToDieReadyCommandId = _networkClient.SendLastToDieCommand(
                    targetReady ? LastToDieCommandKind.Ready : LastToDieCommandKind.Unready);
                if (_hostedLastToDieReadyCommandId == 0)
                {
                    _hostedLastToDieOptimisticReadyState = null;
                }
            }
            return;
        }

        var allReady = snapshot.Players.Count == snapshot.MaximumPlayers
            && snapshot.Players.All(player => player.IsConnected && player.IsReady);
        if (localPlayer.IsHost
            && allReady
            && (IsKeyPressed(keyboard, Keys.Enter)
                || clicked && startBounds.Contains(mouse.Position)))
        {
            _networkClient.SendLastToDieCommand(LastToDieCommandKind.RequestStart);
        }
    }

    private void ReconcileHostedLastToDieReadyCommand(LastToDiePlayerSnapshotMessage localPlayer)
    {
        if (!_hostedLastToDieOptimisticReadyState.HasValue)
        {
            return;
        }

        if (localPlayer.IsReady == _hostedLastToDieOptimisticReadyState.Value
            || _hostedLastToDieReadyCommandId != 0
                && _networkClient.LastToDieState.TryGetCommandResult(
                    _hostedLastToDieReadyCommandId,
                    out var result)
                && result.Result != LastToDieCommandResultKind.Accepted)
        {
            _hostedLastToDieOptimisticReadyState = null;
            _hostedLastToDieReadyCommandId = 0;
        }
    }

    private void ExitHostedLastToDieLobby()
    {
        _networkClient.SendLastToDieLeave();
        _networkClient.Disconnect();
        if (IsHostedServerRunning)
        {
            StopHostedServer();
        }

        ReturnToLastToDieMenu();
    }

    private (Rectangle Ready, Rectangle Start) GetHostedLastToDieLobbyButtonBounds(bool isHost)
    {
        var width = Math.Clamp((int)MathF.Round(ViewportWidth * 0.24f), 220, 340);
        var height = Math.Clamp((int)MathF.Round(ViewportHeight * 0.072f), 44, 64);
        var gap = 20;
        var totalWidth = isHost ? (width * 2) + gap : width;
        var startX = (ViewportWidth - totalWidth) / 2;
        var y = (int)MathF.Round(ViewportHeight * 0.70f);
        return (
            new Rectangle(startX, y, width, height),
            isHost ? new Rectangle(startX + width + gap, y, width, height) : Rectangle.Empty);
    }

    private Rectangle GetHostedLastToDieRoomCodeButtonBounds()
    {
        var width = Math.Clamp((int)MathF.Round(ViewportWidth * 0.28f), 240, 380);
        var height = Math.Clamp((int)MathF.Round(ViewportHeight * 0.064f), 40, 58);
        return new Rectangle(
            (ViewportWidth - width) / 2,
            (int)MathF.Round(ViewportHeight * 0.82f),
            width,
            height);
    }

    private Rectangle GetHostedLastToDieExitButtonBounds()
    {
        var width = Math.Clamp((int)MathF.Round(ViewportWidth * 0.18f), 170, 260);
        var height = Math.Clamp((int)MathF.Round(ViewportHeight * 0.056f), 38, 52);
        return new Rectangle(
            (ViewportWidth - width) / 2,
            (int)MathF.Round(ViewportHeight * 0.91f),
            width,
            height);
    }

    private bool IsHostedLastToDieMenuMusicPhase()
        => ShouldPlayHostedLastToDieMenuMusicDuringTransition(
            _networkClient.IsConnected,
            _networkClient.LastToDieState.Snapshot?.Phase,
            _hostedLastToDieObservedRunId != Guid.Empty,
            _lastToDieConnectionPresentationPending,
            _hostedLastToDieRetryMusicPending);

    internal static bool ShouldPlayHostedLastToDieMenuMusicDuringTransition(
        bool isConnected,
        LastToDieWirePhase? phase,
        bool hasObservedRun,
        bool connectionPresentationPending,
        bool retryMusicPending)
    {
        if (connectionPresentationPending || retryMusicPending)
        {
            return true;
        }

        if (!isConnected)
        {
            return false;
        }

        // Stage replacement temporarily clears the semantic snapshot. Keep
        // LTD's menu track in control during that gap instead of allowing the
        // generic match track to start for a frame during Retry.
        return phase.HasValue
            ? ShouldPlayHostedLastToDieMenuMusic(phase.Value)
            : hasObservedRun;
    }

    internal static bool ShouldPlayHostedLastToDieMenuMusic(LastToDieWirePhase phase)
        => phase is LastToDieWirePhase.Lobby
            or LastToDieWirePhase.SurvivorChoice
            or LastToDieWirePhase.RewardChoice
            or LastToDieWirePhase.LoadingStage;

    internal static bool ShouldClearHostedLastToDieConnectionPresentationPending(
        bool isConnected,
        LastToDieWirePhase? phase)
        => isConnected && phase.HasValue;

    private int GetHostedLastToDieDigitChoice(KeyboardState keyboard, int count)
    {
        Keys[] digits =
        [
            Keys.D1,
            Keys.D2,
            Keys.D3,
            Keys.D4,
            Keys.D5,
            Keys.D6,
        ];
        Keys[] numpad =
        [
            Keys.NumPad1,
            Keys.NumPad2,
            Keys.NumPad3,
            Keys.NumPad4,
            Keys.NumPad5,
            Keys.NumPad6,
        ];
        for (var index = 0; index < Math.Min(count, digits.Length); index += 1)
        {
            if (IsKeyPressed(keyboard, digits[index])
                || IsKeyPressed(keyboard, numpad[index]))
            {
                return index;
            }
        }

        return -1;
    }

    private void DrawHostedLastToDieHud()
    {
        if (_networkClient.LastToDieState.Snapshot is not { } snapshot
            || snapshot.Phase != LastToDieWirePhase.Playing)
        {
            return;
        }

        var remainingTicks = (int)Math.Clamp(
            snapshot.StageEndServerTick - Math.Max(snapshot.ServerTick, _world.Frame),
            0L,
            int.MaxValue);
        DrawTimerFontTextRightAligned(
            FormatHudTimerText(remainingTicks),
            new Vector2(ViewportWidth - 18f, 18f),
            Color.White,
            1f);
        DrawBitmapFontTextRightAligned(
            $"Stage {snapshot.StageNumber}",
            new Vector2(ViewportWidth - 18f, 46f),
            new Color(232, 232, 232),
            0.92f);
        DrawBitmapFontTextRightAligned(
            $"{snapshot.EnemyCount} Enemies",
            new Vector2(ViewportWidth - 18f, 66f),
            new Color(210, 196, 160),
            0.92f);
        var reconnectingPlayer = snapshot.Players.FirstOrDefault(player =>
            !player.IsConnected
            && player.ReconnectGraceEndServerTick > snapshot.ServerTick);
        if (reconnectingPlayer is not null)
        {
            var remainingSeconds = (int)Math.Ceiling(
                (reconnectingPlayer.ReconnectGraceEndServerTick - snapshot.ServerTick)
                / (double)Math.Max(1, _config.TicksPerSecond));
            DrawBitmapFontTextRightAligned(
                $"Teammate reconnect: {remainingSeconds}s",
                new Vector2(ViewportWidth - 18f, 86f),
                new Color(255, 196, 96),
                0.82f);
        }
    }

    private void DrawHostedLastToDieModal()
    {
        if (_networkClient.LastToDieState.Snapshot is not { } snapshot
            || snapshot.Phase == LastToDieWirePhase.Playing
            || IsLastToDieFailureOverlayActive())
        {
            return;
        }

        var localPlayer = snapshot.Players.FirstOrDefault(
            player => player.Slot == _networkClient.LocalPlayerSlot);
        if (localPlayer is null)
        {
            return;
        }

        // Always erase the gameplay render first. Animated menu backgrounds may
        // still be initializing or may have transparent map margins.
        _spriteBatch.Draw(
            _pixel,
            new Rectangle(0, 0, ViewportWidth, ViewportHeight),
            new Color(24, 32, 48));
        _menuController.DrawBackground(ViewportWidth, ViewportHeight);
        DrawMainMenuBottomBar();
        _spriteBatch.Draw(
            _pixel,
            new Rectangle(0, 0, ViewportWidth, ViewportHeight),
            Color.Black * 0.68f);
        var title = snapshot.Phase switch
        {
            LastToDieWirePhase.Lobby => snapshot.MaximumPlayers == 1
                ? "LAST TO DIE"
                : GetHostedLastToDieRoomTitle(snapshot),
            LastToDieWirePhase.SurvivorChoice => string.Empty,
            LastToDieWirePhase.RewardChoice => string.Empty,
            LastToDieWirePhase.LoadingStage => "PREPARING STAGE",
            LastToDieWirePhase.Won => "RUN COMPLETE",
            LastToDieWirePhase.Lost => "GAME OVER!",
            _ => "LAST TO DIE",
        };
        if (!string.IsNullOrEmpty(title))
        {
            DrawHudTextCentered(
                title,
                new Vector2(ViewportWidth / 2f, ViewportHeight * 0.18f),
                new Color(241, 232, 203),
                snapshot.Phase == LastToDieWirePhase.Lost ? 2.35f : 1.8f);
        }

        switch (snapshot.Phase)
        {
            case LastToDieWirePhase.Lobby:
                DrawHostedLastToDieLobby(snapshot, localPlayer);
                break;
            case LastToDieWirePhase.SurvivorChoice:
                DrawLastToDieSurvivorCarousel(localPlayer.SurvivorId);
                break;
            case LastToDieWirePhase.RewardChoice:
                DrawHostedLastToDieRewardChoices(snapshot, localPlayer);
                break;
            case LastToDieWirePhase.LoadingStage:
                DrawHostedLastToDieLoading(snapshot);
                break;
            case LastToDieWirePhase.Won:
                DrawHudTextCentered(
                    snapshot.TerminalReason,
                    new Vector2(ViewportWidth / 2f, ViewportHeight * 0.48f),
                    new Color(220, 214, 214),
                    1f);
                DrawHudTextCentered(
                    "Returning to the Last to Die lobby...",
                    new Vector2(ViewportWidth / 2f, ViewportHeight * 0.62f),
                    new Color(184, 184, 184),
                    0.85f);
                break;
            case LastToDieWirePhase.Lost:
                // The dedicated death presentation owns Retry/Menu-or-Lobby.
                // Keep this modal free of an obsolete auto-return message while
                // the camera-to-death transition is still finishing.
                break;
        }
    }

    private void DrawHostedLastToDieLobby(
        LastToDieRunSnapshotMessage snapshot,
        LastToDiePlayerSnapshotMessage localPlayer)
    {
        var connected = snapshot.Players.Count(player => player.IsConnected);
        DrawHudTextCentered(
            $"Players: {connected}/{snapshot.MaximumPlayers}",
            new Vector2(ViewportWidth / 2f, ViewportHeight * 0.31f),
            Color.White,
            1.15f);

        for (var slot = 1; slot <= snapshot.MaximumPlayers; slot += 1)
        {
            var player = snapshot.Players.FirstOrDefault(candidate => candidate.Slot == slot);
            var y = ViewportHeight * (0.40f + ((slot - 1) * 0.09f));
            var name = player is null
                ? "Waiting for player..."
                : _world.TryGetNetworkPlayer((byte)slot, out var entity)
                    ? entity.DisplayName
                    : $"Player {slot}";
            var role = player is not null && player.IsHost ? "  [HOST]" : string.Empty;
            var status = player is null || !player.IsConnected
                ? "WAITING"
                : player.IsReady ? "READY" : "NOT READY";
            var statusColor = status == "READY"
                ? new Color(112, 224, 126)
                : status == "NOT READY" ? new Color(238, 194, 86) : new Color(168, 168, 168);
            DrawHudTextCentered(
                $"{name}{role}    {status}",
                new Vector2(ViewportWidth / 2f, y),
                statusColor,
                1f);
        }

        var allReady = snapshot.Players.Count == snapshot.MaximumPlayers
            && snapshot.Players.All(player => player.IsConnected && player.IsReady);
        var (readyBounds, startBounds) = GetHostedLastToDieLobbyButtonBounds(localPlayer.IsHost);
        var displayedReady = _hostedLastToDieOptimisticReadyState ?? localPlayer.IsReady;
        DrawHostedLastToDieLobbyButton(
            readyBounds,
            displayedReady ? "UNREADY" : "READY UP",
            enabled: true);
        if (localPlayer.IsHost)
        {
            DrawHostedLastToDieLobbyButton(startBounds, "START", enabled: allReady);
        }
        else
        {
            DrawHudTextCentered(
                "The host can start when everyone is ready.",
                new Vector2(ViewportWidth / 2f, ViewportHeight * 0.80f),
                new Color(190, 190, 190),
                0.8f);
        }
        if (localPlayer.IsHost && IsHostedServerRunning)
        {
            DrawHostedLastToDieLobbyButton(
                GetHostedLastToDieRoomCodeButtonBounds(),
                _hostedLastToDieRoomCodeFeedbackSeconds > 0f
                    ? _hostedLastToDieRoomCodeCopyFailed ? "COPY FAILED" : "ROOM CODE COPIED!"
                    : "COPY ROOM CODE",
                enabled: true);
        }

        DrawHostedLastToDieLobbyButton(
            GetHostedLastToDieExitButtonBounds(),
            "EXIT",
            enabled: true);
    }

    private string GetHostedLastToDieRoomTitle(LastToDieRunSnapshotMessage snapshot)
    {
        var host = snapshot.Players.FirstOrDefault(player => player.IsHost);
        var hostName = host is not null
            && _world.TryGetNetworkPlayer(host.Slot, out var hostEntity)
            && !string.IsNullOrWhiteSpace(hostEntity.DisplayName)
                ? hostEntity.DisplayName.Trim()
                : host?.Slot == _networkClient.LocalPlayerSlot
                    ? GetSocialPresenceDisplayName()
                    : "Host";
        var possessive = hostName.EndsWith("s", StringComparison.OrdinalIgnoreCase)
            ? $"{hostName}' Room"
            : $"{hostName}'s Room";
        return possessive;
    }

    private void DrawHostedLastToDieLobbyButton(Rectangle bounds, string label, bool enabled)
    {
        var hovered = bounds.Contains(_hostedLastToDieMousePosition);
        _spriteBatch.Draw(
            _pixel,
            bounds,
            enabled
                ? hovered ? new Color(164, 44, 44) : new Color(118, 28, 28)
                : new Color(58, 58, 58));
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 2), Color.White * 0.52f);
        DrawBitmapFontTextCentered(
            label,
            bounds.Center.ToVector2() - new Vector2(0f, 8f),
            enabled ? Color.White : new Color(132, 132, 132),
            1f);
    }

    private void DrawHostedLastToDieRewardChoices(
        LastToDieRunSnapshotMessage snapshot,
        LastToDiePlayerSnapshotMessage localPlayer)
    {
        if (localPlayer.ActiveOfferId == 0 || localPlayer.ActiveOfferChoices.Count == 0)
        {
            DrawHudTextCentered(
                "Perk selected. Waiting for your teammate.",
                new Vector2(ViewportWidth / 2f, ViewportHeight * 0.48f),
                new Color(214, 214, 214),
                1f);
            return;
        }

        var layout = GetLastToDieChoiceMenuLayout(localPlayer.ActiveOfferChoices.Count);
        _spriteBatch.Draw(_pixel, layout.Panel, new Color(22, 24, 29, 242));
        _spriteBatch.Draw(
            _pixel,
            new Rectangle(layout.Panel.X, layout.Panel.Y, layout.Panel.Width, 3),
            new Color(210, 210, 210));
        _spriteBatch.Draw(
            _pixel,
            new Rectangle(layout.Panel.X, layout.Panel.Bottom - 3, layout.Panel.Width, 3),
            new Color(76, 76, 76));

        DrawBitmapFontText(
            "Perks",
            new Vector2(layout.Panel.X + 28f, layout.Panel.Y + 24f),
            Color.White,
            1.22f);
        DrawBitmapFontText(
            snapshot.StageNumber == 0
                ? "Choose 1 perk."
                : "Choose 1 reward for the next stage.",
            new Vector2(layout.Panel.X + 28f, layout.Panel.Y + 58f),
            new Color(212, 212, 212),
            0.94f);

        for (var index = 0; index < localPlayer.ActiveOfferChoices.Count; index += 1)
        {
            var perkId = localPlayer.ActiveOfferChoices[index];
            var hasDefinition = HostedLastToDiePerks.TryGetValue(perkId, out var definition);
            var bounds = layout.CardBounds[index];
            var isHovered = index == _hostedLastToDieRewardHoverIndex;
            _spriteBatch.Draw(
                _pixel,
                bounds,
                isHovered ? new Color(70, 38, 38, 240) : new Color(34, 37, 43, 232));
            _spriteBatch.Draw(
                _pixel,
                new Rectangle(bounds.X, bounds.Y, bounds.Width, 3),
                isHovered ? new Color(210, 78, 78) : new Color(118, 126, 140));
            _spriteBatch.Draw(
                _pixel,
                new Rectangle(bounds.X, bounds.Bottom - 3, bounds.Width, 3),
                new Color(14, 16, 19));

            DrawBitmapFontText(
                $"{index + 1}",
                new Vector2(bounds.X + 14f, bounds.Y + 12f),
                new Color(236, 224, 198),
                1f);
            DrawBitmapFontText(
                hasDefinition ? definition!.DisplayName : perkId,
                new Vector2(bounds.X + 14f, bounds.Y + 44f),
                Color.White,
                0.98f);

            var descriptionLines = WrapMenuParagraph(
                hasDefinition ? definition!.Description : "Perk details unavailable.",
                28);
            var lineY = bounds.Y + 84f;
            for (var lineIndex = 0; lineIndex < descriptionLines.Length; lineIndex += 1)
            {
                DrawBitmapFontText(
                    descriptionLines[lineIndex],
                    new Vector2(bounds.X + 14f, lineY),
                    new Color(214, 214, 214),
                    0.88f);
                lineY += 20f;
            }
        }

        DrawBitmapFontText(
            $"Offer {localPlayer.ActiveOfferOrdinal} - Stage {snapshot.StageNumber + 1}",
            new Vector2(layout.Panel.X + 28f, layout.Panel.Bottom - 42f),
            new Color(188, 188, 188),
            0.88f);
    }

    private void DrawHostedLastToDieLoading(LastToDieRunSnapshotMessage snapshot)
    {
        var readyPlayers = snapshot.Players.Count(player => player.IsReady || !player.IsConnected);
        DrawHudTextCentered(
            $"Loading {snapshot.CurrentMap}",
            new Vector2(ViewportWidth / 2f, ViewportHeight * 0.43f),
            Color.White,
            1.15f);
        DrawHudTextCentered(
            snapshot.BaselineStartFrame == 0
                ? "Server is committing the stage world..."
                : $"World sync {readyPlayers}/{snapshot.Players.Count}",
            new Vector2(ViewportWidth / 2f, ViewportHeight * 0.54f),
            new Color(214, 214, 214),
            1f);
    }
}
