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
                    if (welcome.Version != ProtocolVersion.Current)
                    {
                        AddConsoleLine($"protocol mismatch: server={welcome.Version} client={ProtocolVersion.Current}");
                        _networkClient.Disconnect();
                        _menuStatusMessage = "Protocol mismatch.";
                        break;
                    }

                    if (!CustomMapSyncService.EnsureMapAvailable(
                            welcome.LevelName,
                            welcome.IsCustomMap,
                            welcome.MapDownloadUrl,
                            welcome.MapContentHash,
                            out var welcomeMapError))
                    {
                        ReturnToMainMenu(welcomeMapError);
                        AddConsoleLine($"custom map sync failed: {welcomeMapError}");
                        break;
                    }

                    ReinitializeSimulationForTickRate(welcome.TickRate);
                    _networkClient.SetLocalPlayerSlot(welcome.PlayerSlot);
                    _networkClient.ClearPendingTeamSelection();
                    _networkClient.ClearPendingClassSelection();
                    ResetClientTimingState();
                    if (!_world.TryLoadLevel(welcome.LevelName))
                    {
                        var loadError = $"Failed to load map: {welcome.LevelName}";
                        ReturnToMainMenu(loadError);
                        AddConsoleLine(loadError);
                        break;
                    }
                    _world.PrepareLocalPlayerJoin();
                    _pendingHostedConnectTicks = -1;
                    _lastAppliedSnapshotFrame = 0;
                    _lastBufferedSnapshotFrame = 0;
                    _hasReceivedSnapshot = false;
                    _lastSnapshotReceivedTimeSeconds = -1d;
                    _latestSnapshotServerTimeSeconds = -1d;
                    _latestSnapshotReceivedClockSeconds = -1d;
                    _networkSnapshotInterpolationDurationSeconds = 1f / _config.TicksPerSecond;
                    _smoothedSnapshotIntervalSeconds = 1f / _config.TicksPerSecond;
                    _smoothedSnapshotJitterSeconds = 0f;
                    _remotePlayerInterpolationBackTimeSeconds = RemotePlayerMinimumInterpolationBackTimeSeconds;
                    _remotePlayerRenderTimeSeconds = 0d;
                    _lastRemotePlayerRenderTimeClockSeconds = -1d;
                    _hasRemotePlayerRenderTime = false;
                    ResetSnapshotStateHistory();
                    ResetTransientPresentationEffects();
                    ResetProcessedNetworkEventHistory();
                    _localPlayerSnapshotEntityId = null;
                    _consoleOpen = false;
                    _mainMenuOpen = false;
                    _manualConnectOpen = false;
                    _optionsMenuOpen = false;
                    _optionsMenuOpenedFromGameplay = false;
                    _inGameMenuOpen = false;
                    _controlsMenuOpen = false;
                    _controlsMenuOpenedFromGameplay = false;
                    _pendingControlsBinding = null;
                    _teamSelectOpen = !_networkClient.IsSpectator;
                    _classSelectOpen = false;
                    _menuStatusMessage = _networkClient.IsSpectator ? "Connected as spectator." : string.Empty;
                    StopMenuMusic();
                    AddConsoleLine(
                        _networkClient.IsSpectator
                            ? $"connected to {welcome.ServerName} ({welcome.LevelName}) as spectator tickrate={welcome.TickRate}"
                            : $"connected to {welcome.ServerName} ({welcome.LevelName}) tickrate={welcome.TickRate}");
                    break;
                case ConnectionDeniedMessage denied:
                    ReturnToMainMenu(denied.Reason);
                    AddConsoleLine($"connect denied: {denied.Reason}");
                    break;
                case PasswordRequestMessage:
                    _passwordPromptOpen = true;
                    _passwordEditBuffer = string.Empty;
                    _passwordPromptMessage = "Server requires a password.";
                    _consoleOpen = false;
                    _inGameMenuOpen = false;
                    _optionsMenuOpen = false;
                    _controlsMenuOpen = false;
                    _teamSelectOpen = false;
                    _classSelectOpen = false;
                    AddConsoleLine("server requires a password");
                    break;
                case PasswordResultMessage passwordResult:
                    if (passwordResult.Accepted)
                    {
                        _passwordPromptOpen = false;
                        _passwordEditBuffer = string.Empty;
                        _passwordPromptMessage = string.Empty;
                        if (!_networkClient.IsSpectator)
                        {
                            _teamSelectOpen = true;
                        }
                        AddConsoleLine("password accepted");
                        break;
                    }

                    ReturnToMainMenu(string.IsNullOrWhiteSpace(passwordResult.Reason) ? "Password rejected." : passwordResult.Reason);
                    AddConsoleLine($"password rejected: {passwordResult.Reason}");
                    break;
                case ChatRelayMessage chatRelay:
                    AppendChatLine(chatRelay.PlayerName, chatRelay.Text, chatRelay.Team);
                    break;
                case AutoBalanceNoticeMessage notice:
                    if (notice.Kind == AutoBalanceNoticeKind.Pending)
                    {
                        var delaySeconds = Math.Max(1, notice.DelaySeconds);
                        var fromLabel = GetTeamLabel(notice.FromTeam);
                        var toLabel = GetTeamLabel(notice.ToTeam);
                        var label = fromLabel == "??" || toLabel == "??"
                            ? $"Auto-balance in {delaySeconds}s."
                            : $"Auto-balance in {delaySeconds}s (moving {fromLabel} to {toLabel}).";
                        ShowAutoBalanceNotice(label, delaySeconds);
                        AddConsoleLine(label);
                    }
                    else
                    {
                        var toLabel = GetTeamLabel(notice.ToTeam);
                        var label = string.IsNullOrWhiteSpace(notice.PlayerName)
                            ? "Auto-balance applied."
                            : toLabel == "??"
                                ? $"Auto-balance: {notice.PlayerName} moved."
                                : $"Auto-balance: {notice.PlayerName} moved to {toLabel}.";
                        ShowAutoBalanceNotice(label, 6);
                        AddConsoleLine(label);
                    }
                    break;
                case SessionSlotChangedMessage slotChanged:
                    var wasSpectator = _networkClient.IsSpectator;
                    _networkClient.SetLocalPlayerSlot(slotChanged.PlayerSlot);
                    if (_networkClient.IsSpectator)
                    {
                        _teamSelectOpen = false;
                        _classSelectOpen = false;
                        _menuStatusMessage = "Connected as spectator.";
                    }
                    else if (wasSpectator)
                    {
                        _teamSelectOpen = false;
                        _classSelectOpen = true;
                        _menuStatusMessage = string.Empty;
                    }

                    AddConsoleLine($"session slot changed to {_networkClient.LocalPlayerSlot}");
                    break;
                case ControlAckMessage ack:
                    _networkClient.AcknowledgeControlCommand(ack.Sequence, ack.Kind);
                    if (!ack.Accepted)
                    {
                        var description = ack.Kind switch
                        {
                            ControlCommandKind.SelectTeam => "team selection rejected",
                            ControlCommandKind.SelectClass => "class selection rejected",
                            ControlCommandKind.Spectate => "spectate request rejected",
                            _ => "control command rejected",
                        };
                        if (ack.Kind == ControlCommandKind.SelectTeam && _networkClient.IsSpectator)
                        {
                            _teamSelectOpen = true;
                            _classSelectOpen = false;
                        }

                        _menuStatusMessage = description;
                        AddConsoleLine(description);
                    }

                    break;
                case SnapshotMessage snapshot:
                    if ((!string.Equals(snapshot.LevelName, _world.Level.Name, StringComparison.OrdinalIgnoreCase)
                            || snapshot.MapAreaIndex != _world.Level.MapAreaIndex)
                        && !CustomMapSyncService.EnsureMapAvailable(
                            snapshot.LevelName,
                            snapshot.IsCustomMap,
                            snapshot.MapDownloadUrl,
                            snapshot.MapContentHash,
                            out var snapshotMapError))
                    {
                        ReturnToMainMenu(snapshotMapError);
                        AddConsoleLine($"custom map sync failed: {snapshotMapError}");
                        break;
                    }

                    if (snapshot.Frame <= latestBufferedSnapshotFrame)
                    {
                        RecordStaleSnapshot();
                        break;
                    }

                    SnapshotMessage? baselineSnapshot = null;
                    if (snapshot.IsDelta && snapshot.BaselineFrame != 0
                        && !(resolvedBatchSnapshotsByFrame?.TryGetValue(snapshot.BaselineFrame, out baselineSnapshot) ?? false)
                        && !TryGetSnapshotState(snapshot.BaselineFrame, out baselineSnapshot))
                    {
                        RecordMissingBaselineSnapshot();
                        AddConsoleLine($"snapshot {snapshot.Frame} missing baseline {snapshot.BaselineFrame}");
                        break;
                    }

                    SnapshotMessage resolvedSnapshot;
                    try
                    {
                        resolvedSnapshot = SnapshotDelta.ToFullSnapshot(snapshot, baselineSnapshot);
                    }
                    catch (InvalidOperationException ex)
                    {
                        RecordRejectedSnapshot();
                        AddConsoleLine($"snapshot {snapshot.Frame} rejected: {ex.Message}");
                        break;
                    }

                    var localSnapshotPlayer = resolvedSnapshot.Players.FirstOrDefault(player => player.Slot == _networkClient.LocalPlayerSlot);
                    if (_networkDiagnosticsEnabled && localSnapshotPlayer is not null && _hasPredictedLocalPlayerPosition)
                    {
                        RecordPredictionError(Vector2.Distance(_predictedLocalPlayerPosition, new Vector2(localSnapshotPlayer.X, localSnapshotPlayer.Y)));
                    }

                    _localPlayerSnapshotEntityId = localSnapshotPlayer?.PlayerId;
                    for (var visualIndex = 0; visualIndex < resolvedSnapshot.VisualEvents.Count; visualIndex += 1)
                    {
                        var visualEvent = resolvedSnapshot.VisualEvents[visualIndex];
                        if (!ShouldProcessNetworkEvent(visualEvent.EventId, _processedNetworkVisualEventIds, _processedNetworkVisualEventOrder))
                        {
                            continue;
                        }

                        _pendingNetworkVisualEvents.Add(visualEvent);
                    }

                    if (resolvedBatchSnapshotsByFrame is null)
                    {
                        resolvedBatchSnapshotsByFrame = new Dictionary<ulong, SnapshotMessage>();
                    }

                    resolvedBatchSnapshotsByFrame[resolvedSnapshot.Frame] = resolvedSnapshot;
                    resolvedBatchSnapshots ??= new List<SnapshotMessage>();
                    resolvedBatchSnapshots.Add(resolvedSnapshot);
                    latestResolvedSnapshot = resolvedSnapshot;
                    latestBufferedSnapshotFrame = resolvedSnapshot.Frame;
                    break;
            }
        }

        if (latestResolvedSnapshot is not null && resolvedBatchSnapshots is not null)
        {
            UpdateSnapshotTiming(
                latestResolvedSnapshot.Frame,
                latestResolvedSnapshot.TickRate,
                resolvedBatchSnapshots.Count);
            for (var snapshotIndex = 0; snapshotIndex < resolvedBatchSnapshots.Count; snapshotIndex += 1)
            {
                var resolvedBatchSnapshot = resolvedBatchSnapshots[snapshotIndex];
                RememberSnapshotState(resolvedBatchSnapshot);
                CaptureRemoteInterpolationTargets(resolvedBatchSnapshot);
                EnqueueAuthoritativeSnapshot(resolvedBatchSnapshot);
            }

            _networkClient.AcknowledgeSnapshot(latestResolvedSnapshot.Frame);
        }

        ApplyNextQueuedAuthoritativeSnapshot();

        if (_networkDiagnosticsEnabled)
        {
            RecordProcessNetworkMessagesDuration(GetDiagnosticsElapsedMilliseconds(processStartTimestamp));
        }

        if (_networkClient.TryConsumeDisconnectReason(out var disconnectReason))
        {
            ReturnToMainMenu(disconnectReason);
            AddConsoleLine($"network disconnected: {disconnectReason}");
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

    private void EnqueueAuthoritativeSnapshot(SnapshotMessage snapshot)
    {
        if (snapshot.Frame <= _lastBufferedSnapshotFrame)
        {
            return;
        }

        _queuedAuthoritativeSnapshots.Enqueue(snapshot);
        _lastBufferedSnapshotFrame = snapshot.Frame;
        while (_queuedAuthoritativeSnapshots.Count > MaxQueuedAuthoritativeSnapshots)
        {
            _queuedAuthoritativeSnapshots.Dequeue();
        }
    }

    private void ApplyNextQueuedAuthoritativeSnapshot()
    {
        if (_queuedAuthoritativeSnapshots.Count == 0)
        {
            return;
        }

        var snapshot = _queuedAuthoritativeSnapshots.Dequeue();
        var applySnapshotStartTimestamp = _networkDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L;
        var previousLevelName = _world.Level.Name;
        var previousMapAreaIndex = _world.Level.MapAreaIndex;
        var wasAwaitingJoin = _world.LocalPlayerAwaitingJoin;
        if (!_world.ApplySnapshot(snapshot, _networkClient.LocalPlayerSlot))
        {
            if (_networkDiagnosticsEnabled)
            {
                RecordApplySnapshotDuration(GetDiagnosticsElapsedMilliseconds(applySnapshotStartTimestamp));
                RecordRejectedSnapshot();
            }

            AddConsoleLine($"snapshot rejected for slot {_networkClient.LocalPlayerSlot}");
        }
        else
        {
            _lastAppliedSnapshotFrame = snapshot.Frame;
            if (_queuedAuthoritativeSnapshots.Count == 0)
            {
                _lastBufferedSnapshotFrame = _lastAppliedSnapshotFrame;
            }

            if (_networkDiagnosticsEnabled)
            {
                RecordApplySnapshotDuration(GetDiagnosticsElapsedMilliseconds(applySnapshotStartTimestamp));
                RecordAppliedSnapshot();
            }

            _pendingClassSelectTeam = null;
            var reconcileStartTimestamp = _networkDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L;
            ReconcileLocalPrediction(snapshot.LastProcessedInputSequence);
            if (_networkDiagnosticsEnabled)
            {
                RecordReconcileDuration(GetDiagnosticsElapsedMilliseconds(reconcileStartTimestamp));
            }

            ReopenJoinMenusAfterMapTransition(previousLevelName, previousMapAreaIndex, wasAwaitingJoin);
        }
    }

    private void ReopenJoinMenusAfterMapTransition(string previousLevelName, int previousMapAreaIndex, bool wasAwaitingJoin)
    {
        if (_networkClient.IsSpectator)
        {
            _teamSelectOpen = false;
            _classSelectOpen = false;
            return;
        }

        var mapChanged = !string.Equals(previousLevelName, _world.Level.Name, StringComparison.OrdinalIgnoreCase)
            || previousMapAreaIndex != _world.Level.MapAreaIndex;
        if (!_world.LocalPlayerAwaitingJoin || (!mapChanged && wasAwaitingJoin))
        {
            return;
        }

        _networkClient.ClearPendingTeamSelection();
        _networkClient.ClearPendingClassSelection();
        _teamSelectOpen = true;
        _classSelectOpen = false;
        _chatOpen = false;
        _consoleOpen = false;
        _menuStatusMessage = string.Empty;
    }
}
