#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using OpenGarrison.Core;
using OpenGarrison.Core.LastToDie;
using OpenGarrison.Protocol;
using Microsoft.Xna.Framework;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void ProcessNetworkMessages()
    {
        UpdateGameplayAccountAttach();
        var suppressReplayCatchUpEvents = _replaySeekCatchUpActive;
        UpdatePendingNetworkMapSync();
        var processStartTimestamp = _networkDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L;
        var messages = _networkClient.ReceiveMessages();
        CaptureProtocol64RemovedProjectilePresentationEntities(
            _networkClient.Protocol64State.RemovedProjectileLifecycles,
            (ulong)Math.Max(0L, _world.Frame));
        _networkClient.ApplyProtocol64StateToWorld(_world);
        ApplyHostedLastToDiePredictionProfiles();
        ReconcileProtocol64PredictionState();
        if (_networkDiagnosticsEnabled)
        {
            RecordNetworkReceiveDiagnostics(_networkClient.LastReceiveDiagnostics);
        }

        var latestBufferedSnapshotFrame = Math.Max(_lastAppliedSnapshotFrame, _lastBufferedSnapshotFrame);
        SnapshotMessage? latestResolvedSnapshot = null;
        Dictionary<ulong, SnapshotBaselineState>? resolvedBatchSnapshotsByFrame = null;
        List<ResolvedSnapshotEntry>? resolvedBatchSnapshots = null;
        foreach (var message in messages)
        {
            RecordNetworkMessageProcessed(message);
            switch (message)
            {
                case AudioRelayMessage audio:
                    if (!_networkClient.IsReplayConnection) EnsureVoiceChat()?.Receive(audio, VoiceClockSeconds);
                    break;
                case ServerAudioStateMessage audioState:
                    if (!_networkClient.IsReplayConnection) EnsureVoiceChat()?.ApplyState(audioState);
                    break;
                case WelcomeMessage welcome:
                    HandleWelcomeMessage(welcome);
                    break;
                case ConnectionDeniedMessage denied:
                    HandleConnectionDeniedMessage(denied);
                    break;
                case PasswordRequestMessage:
                    HandlePasswordRequestMessage();
                    break;
                case PasswordResultMessage passwordResult:
                    HandlePasswordResultMessage(passwordResult);
                    break;
                case ChatRelayMessage chatRelay:
                    if (!suppressReplayCatchUpEvents)
                    {
                        HandleChatRelayMessage(chatRelay);
                    }
                    break;
                case AutoBalanceNoticeMessage notice:
                    if (!suppressReplayCatchUpEvents)
                    {
                        HandleAutoBalanceNoticeMessage(notice);
                    }
                    break;
                case SessionSlotChangedMessage slotChanged:
                    HandleSessionSlotChangedMessage(slotChanged);
                    break;
                case ControlAckMessage ack:
                    if (!suppressReplayCatchUpEvents)
                    {
                        HandleControlAckMessage(ack);
                    }
                    break;
                case ServerPluginMessage serverPluginMessage:
                    if (!suppressReplayCatchUpEvents
                        && !TryHandleBuiltInVotePresentationMessage(serverPluginMessage)
                        && !TryHandleBuiltInVipPresentationMessage(serverPluginMessage))
                    {
                        NotifyClientPluginsServerMessage(serverPluginMessage);
                    }
                    break;
                case PlayerSocialProfileUpdateMessage socialProfileUpdate:
                    foreach (var removedSlot in socialProfileUpdate.RemovedSlots)
                    {
                        _voiceChat?.RemoveSpeaker(removedSlot);
                        _scoreboardMutedSlots.Remove(removedSlot);
                    }
                    HandlePlayerSocialProfileUpdateMessage(socialProfileUpdate);
                    break;
                case CustomBubbleStateMessage customBubbleState:
                    HandleCustomBubbleStateMessage(customBubbleState);
                    break;
                case CustomBubbleClearMessage customBubbleClear:
                    HandleCustomBubbleClearMessage(customBubbleClear);
                    break;
                case GameplayAccountAttachResultMessage accountAttachResult:
                    if (!suppressReplayCatchUpEvents)
                    {
                        HandleGameplayAccountAttachResult(accountAttachResult);
                    }
                    break;
                case PlayerPointsStateMessage pointsState:
                    HandlePlayerPointsState(pointsState);
                    break;
                case VoteStateMessage voteState:
                    HandleVoteStateMessage(voteState, suppressReplayCatchUpEvents);
                    break;
                case VoteMenuMessage voteMenu:
                    if (!suppressReplayCatchUpEvents)
                    {
                        HandleVoteMenuMessage(voteMenu);
                    }
                    break;
                case SnapshotMessage snapshot:
                    TryHandleSnapshotMessage(
                        snapshot,
                        ref latestBufferedSnapshotFrame,
                        ref latestResolvedSnapshot,
                        ref resolvedBatchSnapshotsByFrame,
                        ref resolvedBatchSnapshots);
                    break;
            }
        }

        ObserveNetworkPresentationPhaseTransition();

        if (latestResolvedSnapshot is not null && resolvedBatchSnapshots is not null)
        {
            FinalizeResolvedSnapshotBatch(latestResolvedSnapshot, resolvedBatchSnapshots);
        }

        ApplyQueuedAuthoritativeSnapshots();
        CompleteReplaySeekCatchUpIfReady(latestResolvedSnapshot is not null);
        PublishCompletedDemoRecordingNoticeIfAvailable();

        if (_networkDiagnosticsEnabled)
        {
            RecordProcessNetworkMessagesDuration(GetDiagnosticsElapsedMilliseconds(processStartTimestamp));
        }

        if (_networkClient.TryConsumeDisconnectReason(out var disconnectReason))
        {
            if (TryReconnectPeerRoom()) return;
            if (TryReconnectManagedRoom()) return;
            if (TryHandleReplayDisconnect(disconnectReason))
            {
                return;
            }

            if (_gameplaySessionController.TryAdvancePendingConnectionCandidate(disconnectReason))
            {
                return;
            }

            ReturnToMainMenuWithNetworkStatus(disconnectReason, $"network disconnected: {disconnectReason}");
        }
    }

    private void ApplyQueuedAuthoritativeSnapshots()
    {
        if (_networkClient.IsReplayConnection)
        {
            while (_queuedAuthoritativeSnapshots.Count > 0)
            {
                ApplyNextQueuedAuthoritativeSnapshot();
            }

            return;
        }

        // During the join warmup, do not expose the world while snapshots are
        // still waiting behind the first full snapshot. Applying the bounded
        // backlog here is safe because gameplay and input remain behind the
        // loading overlay, and it prevents the first visible frame from being
        // several authoritative updates behind the server.
        if (IsNetworkWorldWarmupBlockingGameplay())
        {
            while (_queuedAuthoritativeSnapshots.Count > 0)
            {
                ApplyNextQueuedAuthoritativeSnapshot();
            }

            return;
        }

        ApplyNextQueuedAuthoritativeSnapshot();
    }

    private void ApplyHostedLastToDiePredictionProfiles()
    {
        if (!_networkClient.IsConnected)
        {
            _world.ReconcileRemoteLastToDieDemoknightPresentation(new HashSet<byte>());
            return;
        }

        var perksBySlot = new Dictionary<byte, IReadOnlyList<string>>();
        var killsBySlot = new Dictionary<byte, int>();
        var secondChanceConsumedBySlot = new Dictionary<byte, bool>();
        var demoknightServerSlots = new HashSet<byte>();
        if (_networkClient.LastToDieState.Snapshot is { } snapshot)
        {
            foreach (var player in snapshot.Players)
            {
                perksBySlot[player.Slot] = player.OwnedPerkIds;
                killsBySlot[player.Slot] = player.Kills;
                secondChanceConsumedBySlot[player.Slot] = player.SecondChanceConsumed;
                if (string.Equals(
                        player.SurvivorId,
                        global::OpenGarrison.Core.LastToDie.LastToDieSurvivorCatalog.DemoknightId.Value,
                        StringComparison.Ordinal))
                {
                    demoknightServerSlots.Add(player.Slot);
                }
            }
        }

        _world.ReconcileRemoteLastToDieDemoknightPresentation(demoknightServerSlots);

        // Only LocalPlayer is predicted. Server slots are not simulation slots:
        // this client may own server slot 2 while its local entity uses slot 1.
        var hasLocalProfile = !_networkClient.IsSpectator
            && perksBySlot.TryGetValue(_networkClient.LocalPlayerSlot, out _);
        _world.TrySetLastToDieSurvivorBuff(SimulationWorld.LocalPlayerSlot, hasLocalProfile);
        _world.TrySetNetworkPlayerAutomaticRespawnSuppressed(
            SimulationWorld.LocalPlayerSlot, hasLocalProfile);
        if (hasLocalProfile)
        {
            _world.TryApplyLastToDiePlayerPredictionProfile(
                SimulationWorld.LocalPlayerSlot,
                perksBySlot[_networkClient.LocalPlayerSlot],
                killsBySlot.GetValueOrDefault(_networkClient.LocalPlayerSlot),
                secondChanceConsumedBySlot.GetValueOrDefault(_networkClient.LocalPlayerSlot));
        }
        else
        {
            _world.ClearLastToDiePlayerPredictionProfile(SimulationWorld.LocalPlayerSlot);
        }
    }

    private static string GetTeamLabel(byte team)
    {
        return team switch
        {
            (byte)PlayerTeam.Red => "RED",
            (byte)PlayerTeam.Blue => "BLU",
            _ => "??",
        };
    }
}
