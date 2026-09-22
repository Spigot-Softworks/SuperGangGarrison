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
    private Guid _hostedLastToDieRecordedStatsAttemptId;
    private LastToDieWirePhase? _hostedLastToDieObservedPhase;
    private bool _hostedLastToDieSoloReadyCommandSent;
    private bool _hostedLastToDieSoloStartCommandSent;
    private Point _hostedLastToDieMousePosition;
    private int _hostedLastToDieRewardHoverIndex = -1;
    private float _hostedLastToDieRoomCodeFeedbackSeconds;
    private bool _hostedLastToDieRoomCodeCopyFailed;
    private int _hostedLastToDieObservedStageNumber;
    private float _hostedLastToDieStageIntroSecondsRemaining;
    private bool _hostedLastToDieLobbyMousePressArmed = true;
    private bool? _hostedLastToDieOptimisticReadyState;
    private ulong _hostedLastToDieReadyCommandId;
    private bool _hostedLastToDieRetryMusicPending;
    private bool? _hostedLastToDieSoloSimulationPauseState;
    private long _hostedLastToDieSoloSimulationPauseRetryAtMilliseconds;

    private bool IsHostedLastToDieActive()
        => _networkClient.IsConnected
            && _networkClient.LastToDieState.Snapshot is not null;

    private bool IsCoopLastToDieActive()
        => IsHostedLastToDieActive()
            && _networkClient.LastToDieState.Snapshot is { MaximumPlayers: > 1 };

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
        var snapshot = _networkClient.LastToDieState.Snapshot;
        ReconcileHostedLastToDieSoloSimulationPause(snapshot);
        if (!IsHostedLastToDieActive()
            || snapshot is null)
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

        if (!CanUpdateHostedLastToDieMenuInput())
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
                if (_peerRoomSession is not null) return;
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
                if (_hostedRewardInput.ReconcileHostedContext(
                        snapshot.RunId,
                        localPlayer.ActiveOfferId,
                        _networkClient.ConnectionGeneration,
                        Environment.TickCount64))
                {
                    _rewardInputCommandId = 0;
                }
                if (_rewardInputCommandId != 0
                    && _networkClient.LastToDieState.TryGetCommandResult(_rewardInputCommandId, out var rewardResult)
                    && rewardResult.Result != LastToDieCommandResultKind.Accepted)
                {
                    _rewardInputCommandId = 0;
                    _hostedRewardInput.Reject();
                }
                var rewardLayout = GetLastToDieChoiceMenuLayout(localPlayer.ActiveOfferChoices.Count);
                _hostedLastToDieRewardHoverIndex = GetLastToDieChoiceHoverIndex(
                    mouse.Position,
                    rewardLayout);
                if (localPlayer.ActiveOfferId != 0
                    && UpdateLastToDieRewardInput(_hostedRewardInput, rewardLayout, keyboard, mouse,
                        index => index < localPlayer.ActiveOfferChoices.Count,
                        index => _networkClient.SendLastToDieCommand(
                            LastToDieCommandKind.RerollReward,
                            localPlayer.ActiveOfferChoices[index],
                            localPlayer.ActiveOfferId),
                        index => index >= 0
                            && localPlayer.ActiveOfferSlots is { } activeOfferSlots
                            && index < activeOfferSlots.Count
                            && activeOfferSlots[index].RerollsRemaining > 0
                            && activeOfferSlots[index].HasEligibleReplacement))
                {
                    _rewardInputCommandId = _networkClient.SendLastToDieCommand(
                        LastToDieCommandKind.SelectReward,
                        localPlayer.ActiveOfferChoices[_hostedRewardInput.SelectedIndex],
                        localPlayer.ActiveOfferId);
                    if (_rewardInputCommandId == 0) _hostedRewardInput.Reject();
                }
                break;

        }
    }

    private void ReconcileHostedLastToDieSoloSimulationPause(
        LastToDieRunSnapshotMessage? snapshot)
    {
        if (HasManagedRoom || IsEmbeddedSessionOwner) { ReconcileManagedSoloPause(snapshot); return; }
        var nowMilliseconds = Environment.TickCount64;
        var ownsEligibleSoloServer = IsHostedServerRunning
            && _networkClient.IsConnected
            && snapshot?.MaximumPlayers == 1;
        if (!ownsEligibleSoloServer)
        {
            if (_hostedLastToDieSoloSimulationPauseState == true
                && IsHostedServerRunning
                && nowMilliseconds >= _hostedLastToDieSoloSimulationPauseRetryAtMilliseconds)
            {
                if (!TrySendHostedServerAdminCommand("ltd_pause 0", out _, out _))
                {
                    _hostedLastToDieSoloSimulationPauseRetryAtMilliseconds = nowMilliseconds + 500;
                    return;
                }
            }

            _hostedLastToDieSoloSimulationPauseState = null;
            _hostedLastToDieSoloSimulationPauseRetryAtMilliseconds = 0;
            return;
        }

        var soloSnapshot = snapshot!;
        var shouldPause = ShouldPauseHostedLastToDieSoloSimulation(
            isHostedServerRunning: true,
            isConnected: true,
            maximumPlayers: soloSnapshot.MaximumPlayers,
            phase: soloSnapshot.Phase,
            hasOpenGameplayOverlay: HasOpenGameplayOverlay() || !IsWindowInputActive,
            isLoading: IsGameplayLoadingForMenuInput());
        if (_hostedLastToDieSoloSimulationPauseState is null && !shouldPause)
        {
            _hostedLastToDieSoloSimulationPauseState = false;
            return;
        }

        if (_hostedLastToDieSoloSimulationPauseState == shouldPause
            || nowMilliseconds < _hostedLastToDieSoloSimulationPauseRetryAtMilliseconds)
        {
            return;
        }

        if (!TrySendHostedServerAdminCommand(
                shouldPause ? "ltd_pause 1" : "ltd_pause 0",
                out _,
                out _))
        {
            _hostedLastToDieSoloSimulationPauseRetryAtMilliseconds = nowMilliseconds + 500;
            return;
        }

        _hostedLastToDieSoloSimulationPauseState = shouldPause;
        _hostedLastToDieSoloSimulationPauseRetryAtMilliseconds = 0;
    }

    private bool ShouldPauseHostedLastToDieSoloClientSimulation()
    {
        var snapshot = _networkClient.LastToDieState.Snapshot;
        return snapshot is not null
            && ShouldPauseHostedLastToDieSoloSimulation(
                IsHostedServerRunning || IsManagedRoomOwner || IsEmbeddedSessionOwner,
                _networkClient.IsConnected,
                snapshot.MaximumPlayers,
                snapshot.Phase,
                HasOpenGameplayOverlay() || !IsWindowInputActive,
                IsGameplayLoadingForMenuInput());
    }

    internal static bool ShouldPauseHostedLastToDieSoloSimulation(
        bool isHostedServerRunning,
        bool isConnected,
        int maximumPlayers,
        LastToDieWirePhase phase,
        bool hasOpenGameplayOverlay,
        bool isLoading = false)
    {
        return isHostedServerRunning
            && isConnected
            && maximumPlayers == 1
            && phase == LastToDieWirePhase.Playing
            && !isLoading
            && hasOpenGameplayOverlay;
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
        HideJoiningServerLoadingOverlay();
        EnsureLastToDieSurvivorCarouselAssets();
        if (_hostedLastToDieObservedRunId != snapshot.RunId)
        {
            _hostedLastToDieObservedRunId = snapshot.RunId;
            _hostedLastToDieObservedAliveBySlot.Clear();
            _hostedLastToDieObservedPhase = null;
            _hostedLastToDieObservedStageNumber = 0;
            _hostedLastToDieStageIntroSecondsRemaining = 0f;
        }

        PersistHostedLastToDieRunStats(snapshot);

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

        if (snapshot.Phase == LastToDieWirePhase.Playing)
        {
            if (_hostedLastToDieObservedPhase != LastToDieWirePhase.Playing
                || _hostedLastToDieObservedStageNumber != snapshot.StageNumber)
            {
                _hostedLastToDieStageIntroSecondsRemaining = LastToDieStageIntroDurationSeconds;
            }
            else
            {
                _hostedLastToDieStageIntroSecondsRemaining = Math.Max(
                    0f,
                    _hostedLastToDieStageIntroSecondsRemaining - Math.Max(0f, _gameplayPresentationDeltaSeconds));
            }

            _hostedLastToDieObservedStageNumber = snapshot.StageNumber;
        }
        else
        {
            _hostedLastToDieStageIntroSecondsRemaining = 0f;
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

    private void PersistHostedLastToDieRunStats(LastToDieRunSnapshotMessage snapshot)
    {
        if (snapshot.Phase is not (LastToDieWirePhase.Won or LastToDieWirePhase.Lost)
            || snapshot.AttemptId == Guid.Empty
            || snapshot.AttemptId == _hostedLastToDieRecordedStatsAttemptId)
        {
            return;
        }

        var localPlayer = snapshot.Players.FirstOrDefault(
            player => player.Slot == _networkClient.LocalPlayerSlot);
        if (localPlayer is null)
        {
            return;
        }

        if (_lastToDieStats.RecordRun(localPlayer.ScoreUnits, snapshot.CompletedRounds, snapshot.AttemptId))
        {
            _lastToDieStats.Save();
        }
        if (_peerRoomSession is { IsOwner: false }) _pendingRunClaims.Enqueue(snapshot.AttemptId);
        _hostedLastToDieRecordedStatsAttemptId = snapshot.AttemptId;
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
            && (IsHostedServerRunning || IsManagedRoomOwner || IsEmbeddedSessionOwner)
            && RelayRoomCode.TryNormalize(_hostedLastToDieRoomCode, out var roomCode)
            && clicked
            && roomCodeBounds.Contains(mouse.Position))
        {
            if (OperatingSystem.IsBrowser() && OpenGarrison.ClientShared.BrowserPreferenceStore.CopyText is { } copy)
                _managedRoomCopyTask = copy(roomCode);
            else
            {
                _hostedLastToDieRoomCodeCopyFailed = !TrySetClipboardText(roomCode);
                _hostedLastToDieRoomCodeFeedbackSeconds = 2f;
            }
            return;
        }

        if (IsCoopLastToDieActive() && clicked && GetHostedLastToDieVoiceButtonBounds().Contains(mouse.Position))
        {
            ToggleVoiceChannelMembership();
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

        var allReady = snapshot.Players.Count > 0
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
        LeaveManagedRoom();
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
            (ViewportWidth - (width * 2) - 20) / 2,
            (int)MathF.Round(ViewportHeight * 0.82f),
            width,
            height);
    }

    private Rectangle GetHostedLastToDieVoiceButtonBounds()
    {
        var roomCode = GetHostedLastToDieRoomCodeButtonBounds();
        var showRoomCode = (IsHostedServerRunning || IsManagedRoomOwner || IsEmbeddedSessionOwner)
            && _networkClient.LastToDieState.Snapshot?.Players.Any(player => player.Slot == _networkClient.LocalPlayerSlot && player.IsHost) == true
            && RelayRoomCode.TryNormalize(_hostedLastToDieRoomCode, out _);
        return new Rectangle(showRoomCode ? roomCode.Right + 20 : (ViewportWidth - roomCode.Width) / 2,
            roomCode.Y, roomCode.Width, roomCode.Height);
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

        var remainingTicks = ResolveHostedLastToDieRemainingTicks(
            snapshot.StageEndServerTick,
            snapshot.ServerTick);
        DrawTimerFontTextRightAligned(
            FormatHudTimerText(remainingTicks),
            new Vector2(ViewportWidth - 18f, 18f),
            Color.White,
            1f);
        DrawBitmapFontTextRightAligned(
            $"Stage {snapshot.StageNumber}",
            new Vector2(ViewportWidth - 18f, 46f),
            new Color(232, 232, 232),
            1f);
        DrawBitmapFontTextRightAligned(
            $"{snapshot.EnemyCount} Enemies",
            new Vector2(ViewportWidth - 18f, 66f),
            new Color(210, 196, 160),
            1f);
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
                1f);
        }

        if (ShouldShowHostedLastToDieStageIntro(snapshot.Phase, _hostedLastToDieStageIntroSecondsRemaining))
        {
            var introProgress = 1f - (_hostedLastToDieStageIntroSecondsRemaining / LastToDieStageIntroDurationSeconds);
            var fadeAlpha = introProgress < 0.32f
                ? Math.Clamp(introProgress / 0.32f, 0f, 1f)
                : Math.Clamp(1f - ((introProgress - 0.32f) / 0.68f), 0f, 1f);
            DrawHudTextCentered(
                "SURVIVE!",
                new Vector2(ViewportWidth / 2f, ViewportHeight * 0.2f),
                new Color(241, 232, 203) * (fadeAlpha * 0.96f),
                2.4f);
        }
    }

    internal static bool ShouldShowHostedLastToDieStageIntro(
        LastToDieWirePhase phase,
        float secondsRemaining)
    {
        return phase == LastToDieWirePhase.Playing && secondsRemaining > 0f;
    }

    internal static int ResolveHostedLastToDieRemainingTicks(
        long stageEndServerTick,
        long serverTick)
    {
        return (int)Math.Clamp(
            stageEndServerTick - serverTick,
            0L,
            int.MaxValue);
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
        if (!ShouldShowHostedLastToDieLobby(snapshot.MaximumPlayers))
        {
            // Solo is admitted through the same authoritative lobby phase as
            // co-op, but it immediately sends Ready and Start. Keep that
            // handshake from exposing co-op controls while those commands
            // round-trip through the embedded/managed server. The generic
            // stage-loading copy is reserved for an actual stage barrier;
            // during this short startup phase there is no reliable map/world
            // detail to show yet.
            DrawHudTextCentered(
                "Starting Last to Die...",
                new Vector2(ViewportWidth / 2f, ViewportHeight * 0.47f),
                Color.White,
                1.15f);
            return;
        }

        if (OperatingSystem.IsBrowser()) _browserHostedLobbyDrawCount++;
        if (_peerRoomSession is not null)
        {
            DrawHudTextCentered("Connecting players...", new Vector2(ViewportWidth / 2f, ViewportHeight * .45f), Color.White, 1.2f);
            return;
        }
        var connected = snapshot.Players.Count(player => player.IsConnected);
        DrawHudTextCentered(
            $"Players: {connected}/{snapshot.MaximumPlayers}",
            new Vector2(ViewportWidth / 2f, ViewportHeight * 0.31f),
            Color.White,
            1.15f);

        for (var slot = 1; slot <= snapshot.MaximumPlayers; slot += 1)
        {
            var player = snapshot.Players.FirstOrDefault(candidate => candidate.Slot == slot);
            var y = ViewportHeight * (0.37f + ((slot - 1) * (snapshot.MaximumPlayers > 2 ? 0.075f : 0.09f)));
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

        var allReady = snapshot.Players.Count > 0
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
        if (localPlayer.IsHost
            && (IsHostedServerRunning || IsManagedRoomOwner || IsEmbeddedSessionOwner)
            && RelayRoomCode.TryNormalize(_hostedLastToDieRoomCode, out _))
        {
            DrawHostedLastToDieLobbyButton(
                GetHostedLastToDieRoomCodeButtonBounds(),
                _hostedLastToDieRoomCodeFeedbackSeconds > 0f
                    ? _hostedLastToDieRoomCodeCopyFailed ? "COPY FAILED" : "ROOM CODE COPIED!"
                    : $"ROOM: {_hostedLastToDieRoomCode} (COPY)",
                enabled: true);
        }

        if (IsCoopLastToDieActive())
        {
            DrawHostedLastToDieLobbyButton(
                GetHostedLastToDieVoiceButtonBounds(),
                GetVoiceChannelActionLabel(),
                enabled: _voiceChat?.ServerState?.VoiceChannelRequiresJoin == true);
        }

        DrawHostedLastToDieLobbyButton(
            GetHostedLastToDieExitButtonBounds(),
            "EXIT",
            enabled: true);
    }

    internal static bool ShouldShowHostedLastToDieLobby(int maximumPlayers)
        => maximumPlayers != 1;

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

        var targetStage = localPlayer.ActiveOfferTargetStage > 0
            ? localPlayer.ActiveOfferTargetStage
            : snapshot.StageNumber + 1;
        var selectionNumber = Math.Max(1, localPlayer.ActiveOfferSelectionNumber);
        var selectionsRequired = Math.Max(selectionNumber, localPlayer.ActiveOfferSelectionsRequired);
        DrawBitmapFontText(
            $"Stage {targetStage} Perks",
            new Vector2(layout.Panel.X + 28f, layout.Panel.Y + 24f),
            Color.White,
            1f);
        DrawBitmapFontText(
            $"Pick {selectionNumber} of {selectionsRequired}. Select a perk, then Confirm or Enter.",
            new Vector2(layout.Panel.X + 28f, layout.Panel.Y + 58f),
            new Color(212, 212, 212),
            1f);

        for (var index = 0; index < localPlayer.ActiveOfferChoices.Count; index += 1)
        {
            var perkId = localPlayer.ActiveOfferChoices[index];
            var hasDefinition = HostedLastToDiePerks.TryGetValue(perkId, out var definition);
            var bounds = layout.CardBounds[index];
            var offerSlot = localPlayer.ActiveOfferSlots is { } offerSlots
                && index < offerSlots.Count
                    ? offerSlots[index]
                    : null;
            var tier = offerSlot?.Tier ?? LastToDieWirePerkTier.Standard;
            var isSelected = index == _hostedRewardInput.SelectedIndex;
            var isHovered = index == _hostedLastToDieRewardHoverIndex || isSelected;
            DrawHostedLastToDieTierCard(bounds, tier, perkId, index, isHovered, isSelected);

            DrawBitmapFontText(
                $"{index + 1}",
                new Vector2(bounds.X + 14f, bounds.Y + 12f),
                new Color(236, 224, 198),
                1f);
            DrawBitmapFontText(
                hasDefinition ? definition!.DisplayName : perkId,
                new Vector2(bounds.X + 14f, bounds.Y + 44f),
                Color.White,
                1f);

            var descriptionLines = WrapMenuParagraph(
                hasDefinition
                    ? ResolveLastToDiePerkDescriptionBindingLabels(
                        definition!.Description,
                        GetBindingDisplayName(_inputBindings.InteractWeapon))
                    : "Perk details unavailable.",
                28);
            var lineY = bounds.Y + 84f;
            for (var lineIndex = 0; lineIndex < descriptionLines.Length; lineIndex += 1)
            {
                DrawBitmapFontText(
                    descriptionLines[lineIndex],
                    new Vector2(bounds.X + 14f, lineY),
                    new Color(214, 214, 214),
                    1f);
                lineY += 20f;
            }

            var rerollEnabled = !_hostedRewardInput.Submitted
                && offerSlot is { RerollsRemaining: > 0, HasEligibleReplacement: true };
            DrawLastToDieRewardReroll(layout, index, enabled: rerollEnabled);

            if (isHovered && tier != LastToDieWirePerkTier.Standard)
            {
                var tierLabel = tier == LastToDieWirePerkTier.Ultra ? "Ultra" : "Rare";
                var tierColor = tier == LastToDieWirePerkTier.Ultra
                    ? new Color(255, 106, 106)
                    : new Color(235, 150, 255);
                DrawBitmapFontText(
                    tierLabel,
                    new Vector2(bounds.Right - MeasureBitmapFontWidth(tierLabel, 1f) - 14f, bounds.Y + 12f),
                    tierColor,
                    1f);
            }
        }

        DrawBitmapFontText(
            $"Offer {localPlayer.ActiveOfferOrdinal} - Selection {selectionNumber}/{selectionsRequired}",
            new Vector2(layout.Panel.X + 28f, layout.Panel.Bottom - 42f),
            new Color(188, 188, 188),
            1f);
        DrawLastToDieRewardConfirm(_hostedRewardInput, layout);
    }

    private void DrawHostedLastToDieTierCard(
        Rectangle bounds,
        LastToDieWirePerkTier tier,
        string perkId,
        int cardIndex,
        bool hovered,
        bool selected)
    {
        if (tier == LastToDieWirePerkTier.Standard)
        {
            _spriteBatch.Draw(_pixel, bounds, hovered ? new Color(70, 38, 38, 240) : new Color(34, 37, 43, 232));
            _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 3),
                selected ? new Color(255, 214, 82) : hovered ? new Color(210, 78, 78) : new Color(118, 126, 140));
            _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Bottom - 3, bounds.Width, 3), new Color(14, 16, 19));
            return;
        }

        var ultra = tier == LastToDieWirePerkTier.Ultra;
        var top = ultra ? new Color(35, 7, 12) : new Color(39, 20, 57);
        var bottom = ultra ? new Color(8, 8, 12) : new Color(17, 11, 31);
        const int strips = 8;
        for (var strip = 0; strip < strips; strip += 1)
        {
            var y = bounds.Y + (bounds.Height * strip / strips);
            var nextY = bounds.Y + (bounds.Height * (strip + 1) / strips);
            var tint = Color.Lerp(top, bottom, strip / (float)(strips - 1));
            if (hovered)
            {
                tint = Color.Lerp(tint, ultra ? new Color(112, 17, 26) : new Color(86, 39, 113), 0.25f);
            }
            _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, y, bounds.Width, Math.Max(1, nextY - y)), tint);
        }

        var seed = StableLastToDieCardSeed(perkId, cardIndex, tier);
        var now = Environment.TickCount64;
        var borderA = ultra ? new Color(105, 19, 25) : new Color(91, 50, 137);
        var borderB = ultra ? new Color(246, 61, 67) : new Color(224, 102, 222);
        DrawLastToDieSegmentedBorder(bounds, seed, borderA, borderB, selected);

        var sparkleCount = ultra ? 3 : 4;
        for (var sparkle = 0; sparkle < sparkleCount; sparkle += 1)
        {
            var positionSeed = seed + (uint)(sparkle * 0x9E3779B9u);
            var x = bounds.X + 10 + (int)(positionSeed % (uint)Math.Max(1, bounds.Width - 20));
            var y = bounds.Y + 8 + (int)((positionSeed >> 12) % (uint)Math.Max(1, bounds.Height - 16));
            var oscillation = 0.5f + (0.5f * MathF.Sin((float)(now / 260d + sparkle + (seed % 19))));
            var alpha = (byte)(10 + (oscillation * (ultra ? 28 : 24)));
            var glint = ultra
                ? new Color((byte)255, (byte)96, (byte)96, alpha)
                : new Color((byte)255, (byte)183, (byte)252, alpha);
            _spriteBatch.Draw(_pixel, new Rectangle(x, y, 2, 2), glint);
        }

        if (selected)
        {
            _spriteBatch.Draw(_pixel, new Rectangle(bounds.X + 4, bounds.Y + 4, bounds.Width - 8, 2), new Color(255, 214, 82));
        }
    }

    private void DrawLastToDieSegmentedBorder(
        Rectangle bounds,
        uint seed,
        Color start,
        Color end,
        bool selected)
    {
        const int segments = 10;
        const int thickness = 4;
        for (var index = 0; index < segments; index += 1)
        {
            var color = selected
                ? new Color(255, 214, 82)
                : Color.Lerp(start, end, index / (float)(segments - 1));
            var x = bounds.X + (bounds.Width * index / segments);
            var nextX = bounds.X + (bounds.Width * (index + 1) / segments);
            var width = Math.Max(1, nextX - x);
            _spriteBatch.Draw(_pixel, new Rectangle(x, bounds.Y, width, thickness), color);
            _spriteBatch.Draw(_pixel, new Rectangle(x, bounds.Bottom - thickness, width, thickness), color);
        }

        var leftColor = selected ? new Color(255, 214, 82) : Color.Lerp(start, end, (seed & 1u) == 0 ? 0.25f : 0.65f);
        var rightColor = selected ? new Color(255, 214, 82) : Color.Lerp(start, end, (seed & 1u) == 0 ? 0.65f : 0.25f);
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, thickness, bounds.Height), leftColor);
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.Right - thickness, bounds.Y, thickness, bounds.Height), rightColor);
    }

    private static uint StableLastToDieCardSeed(string perkId, int cardIndex, LastToDieWirePerkTier tier)
    {
        var hash = 2166136261u;
        foreach (var character in perkId)
        {
            hash = (hash ^ character) * 16777619u;
        }
        hash = (hash ^ (uint)cardIndex) * 16777619u;
        return (hash ^ (uint)tier) * 16777619u;
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
