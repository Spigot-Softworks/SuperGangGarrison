#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using Microsoft.Xna.Framework;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void ProcessNetworkMessages()
    {
        UpdatePendingNetworkMapSync();
        var processStartTimestamp = _networkDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L;
        var messages = _networkClient.ReceiveMessages();
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
                    HandleChatRelayMessage(chatRelay);
                    break;
                case AutoBalanceNoticeMessage notice:
                    HandleAutoBalanceNoticeMessage(notice);
                    break;
                case SessionSlotChangedMessage slotChanged:
                    HandleSessionSlotChangedMessage(slotChanged);
                    break;
                case ControlAckMessage ack:
                    HandleControlAckMessage(ack);
                    break;
                case ServerPluginMessage serverPluginMessage:
                    if (!TryHandleBuiltInVotePresentationMessage(serverPluginMessage)
                        && !TryHandleBuiltInVipPresentationMessage(serverPluginMessage))
                    {
                        NotifyClientPluginsServerMessage(serverPluginMessage);
                    }
                    break;
                case PlayerSocialProfileUpdateMessage socialProfileUpdate:
                    HandlePlayerSocialProfileUpdateMessage(socialProfileUpdate);
                    break;
                case CustomBubbleStateMessage customBubbleState:
                    HandleCustomBubbleStateMessage(customBubbleState);
                    break;
                case CustomBubbleClearMessage customBubbleClear:
                    HandleCustomBubbleClearMessage(customBubbleClear);
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

        if (latestResolvedSnapshot is not null && resolvedBatchSnapshots is not null)
        {
            FinalizeResolvedSnapshotBatch(latestResolvedSnapshot, resolvedBatchSnapshots);
        }

        ApplyQueuedAuthoritativeSnapshots();
        PublishCompletedDemoRecordingNoticeIfAvailable();

        if (_networkDiagnosticsEnabled)
        {
            RecordProcessNetworkMessagesDuration(GetDiagnosticsElapsedMilliseconds(processStartTimestamp));
        }

        if (_networkClient.TryConsumeDisconnectReason(out var disconnectReason))
        {
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
            return;
        }

        var perksBySlot = new Dictionary<byte, IReadOnlyList<string>>();
        if (_networkClient.LastToDieState.Snapshot is { } snapshot)
        {
            foreach (var player in snapshot.Players)
            {
                perksBySlot[player.Slot] = player.OwnedPerkIds;
            }
        }

        foreach (var slot in SimulationWorld.NetworkPlayerSlots)
        {
            _world.TrySetNetworkPlayerAutomaticRespawnSuppressed(
                slot,
                perksBySlot.ContainsKey(slot));
            if (perksBySlot.TryGetValue(slot, out var perkIds))
            {
                _world.TryApplyLastToDiePlayerPredictionProfile(slot, perkIds);
            }
            else
            {
                _world.ClearLastToDiePlayerPredictionProfile(slot);
            }
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
