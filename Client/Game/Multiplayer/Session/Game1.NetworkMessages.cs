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
        var processStartTimestamp = _networkDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L;
        var messages = _networkClient.ReceiveMessages();
        if (_networkDiagnosticsEnabled)
        {
            RecordNetworkReceiveDiagnostics(_networkClient.LastReceiveDiagnostics);
        }

        var latestBufferedSnapshotFrame = Math.Max(_lastAppliedSnapshotFrame, _lastBufferedSnapshotFrame);
        SnapshotMessage? latestResolvedSnapshot = null;
        Dictionary<ulong, SnapshotMessage>? resolvedBatchSnapshotsByFrame = null;
        List<SnapshotMessage>? resolvedBatchSnapshots = null;
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
                    NotifyClientPluginsServerMessage(serverPluginMessage);
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

        ApplyNextQueuedAuthoritativeSnapshot();

        if (_networkDiagnosticsEnabled)
        {
            RecordProcessNetworkMessagesDuration(GetDiagnosticsElapsedMilliseconds(processStartTimestamp));
        }

        if (_networkClient.TryConsumeDisconnectReason(out var disconnectReason))
        {
            ReturnToMainMenuWithNetworkStatus(disconnectReason, $"network disconnected: {disconnectReason}");
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
