using System;
using System.Collections.Generic;
using System.Net;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using OpenGarrison.Server;
using OpenGarrison.Server.Plugins;
using static ServerHelpers;

sealed class ClientSession(byte slot, int userId, ServerTransportPeer peer, string name, TimeSpan lastSeen)
{
    private const int MinimumSnapshotHistoryLimit = 12;
    private const int SnapshotHistorySlackFrames = 12;
    private const int MaximumSnapshotHistoryLimit = 48;
    private const int MaximumPendingInputStates = 512;
    private const int AcknowledgedSoundEventHistoryLimit = 4096;
    private readonly List<SequencedPlayerInputSnapshot> _pendingInputs = new();
    private readonly Dictionary<ulong, SnapshotBaselineState> _snapshotStatesByFrame = new();
    private readonly Queue<ulong> _snapshotFrameOrder = new();
    private readonly Dictionary<ulong, ulong[]> _snapshotSoundEventIdsByFrame = new();
    private readonly HashSet<ulong> _acknowledgedSoundEventIds = new();
    private readonly Queue<ulong> _acknowledgedSoundEventOrder = new();

    public ClientSession(byte slot, int userId, IPEndPoint endPoint, string name, TimeSpan lastSeen)
        : this(slot, userId, ServerTransportPeer.FromUdpEndPoint(endPoint), name, lastSeen)
    {
    }

    public byte Slot { get; set; } = slot;
    public int UserId { get; } = userId;
    public ServerTransportPeer Peer { get; } = peer;
    public IPEndPoint EndPoint => Peer.UdpEndPoint ?? throw new InvalidOperationException($"Client peer {Peer} has no UDP endpoint.");
    public IPEndPoint? UdpEndPoint => Peer.UdpEndPoint;
    public IPAddress? RemoteAddress => Peer.RemoteAddress;
    public string RemoteDescription => Peer.Description;
    public bool IsLoopbackConnection => Peer.IsLoopback;
    public string Name { get; set; } = name;
    public ulong BadgeMask { get; set; }
    public string FriendCode { get; set; } = string.Empty;
    public string PlayerCardJson { get; set; } = string.Empty;
    public TimeSpan ConnectedAt { get; } = lastSeen;
    public TimeSpan LastSeen { get; set; } = lastSeen;
    public PlayerInputSnapshot LatestReceivedInput { get; private set; }
    public PlayerInputSnapshot LatestAppliedInput { get; private set; }
    public bool HasAcceptedInput { get; private set; }
    public uint LastReceivedInputSequence { get; private set; }
    public uint LastProcessedInputSequence { get; private set; }
    public int PendingInputCount => _pendingInputs.Count;
    public uint LastTeamCommandSequence { get; set; }
    public uint LastClassCommandSequence { get; set; }
    public uint LastSpectateCommandSequence { get; set; }
    public uint LastGameplayLoadoutCommandSequence { get; set; }
    public ulong LastAcknowledgedSnapshotFrame { get; private set; }
    public bool IsAuthorized { get; set; } = true;
    public bool IsWatchOnly { get; set; }
    public TimeSpan LastPasswordRequestSentAt { get; set; } = TimeSpan.MinValue;
    public OpenGarrisonServerAdminPermissions AdminPermissions { get; set; } = OpenGarrisonServerAdminPermissions.None;
    public TimeSpan AdminAuthenticatedAt { get; set; } = TimeSpan.MinValue;
    public string PendingAdminChatCommand { get; set; } = string.Empty;
    public TimeSpan PendingAdminChatCommandQueuedAt { get; set; } = TimeSpan.MinValue;
    public bool IsGagged { get; set; }
    public int SnapshotHistoryCount => _snapshotFrameOrder.Count;

    public bool TrySetLatestInput(uint sequence, PlayerInputSnapshot input)
    {
        if (HasAcceptedInput
            && (!IsSequenceNewer(sequence, LastProcessedInputSequence) || HasPendingInput(sequence)))
        {
            return false;
        }

        InsertPendingInput(sequence, input);
        HasAcceptedInput = true;
        if (LastReceivedInputSequence == 0 || IsSequenceNewer(sequence, LastReceivedInputSequence))
        {
            LastReceivedInputSequence = sequence;
            LatestReceivedInput = input;
        }

        TrimPendingInputQueue();
        return true;
    }

    public bool TryGetInputForNextTick(out PlayerInputSnapshot input)
    {
        if (_pendingInputs.Count > 0)
        {
            var pendingInput = _pendingInputs[0];
            _pendingInputs.RemoveAt(0);
            LatestAppliedInput = pendingInput.Input;
            LastProcessedInputSequence = pendingInput.Sequence;
            input = LatestAppliedInput;
            return true;
        }

        if (HasAcceptedInput)
        {
            input = LatestAppliedInput;
            return true;
        }

        input = default;
        return false;
    }

    private bool HasPendingInput(uint sequence)
    {
        for (var index = 0; index < _pendingInputs.Count; index += 1)
        {
            if (_pendingInputs[index].Sequence == sequence)
            {
                return true;
            }
        }

        return false;
    }

    private void InsertPendingInput(uint sequence, PlayerInputSnapshot input)
    {
        var insertIndex = _pendingInputs.Count;
        while (insertIndex > 0 && IsSequenceNewer(_pendingInputs[insertIndex - 1].Sequence, sequence))
        {
            insertIndex -= 1;
        }

        _pendingInputs.Insert(insertIndex, new SequencedPlayerInputSnapshot(sequence, input));
    }

    private void TrimPendingInputQueue()
    {
        while (_pendingInputs.Count > MaximumPendingInputStates)
        {
            var droppedInput = _pendingInputs[0];
            _pendingInputs.RemoveAt(0);
            LatestAppliedInput = droppedInput.Input;
            LastProcessedInputSequence = droppedInput.Sequence;
        }
    }

    public void RememberSnapshotState(SnapshotMessage snapshot)
    {
        if (snapshot.IsDelta)
        {
            RememberResolvedSnapshotState(SnapshotDelta.ToFullSnapshot(snapshot));
            return;
        }

        RememberResolvedSnapshotState(snapshot);
    }

    public void RememberResolvedSnapshotState(SnapshotMessage fullSnapshot)
    {
        if (fullSnapshot.IsDelta)
        {
            throw new InvalidOperationException("Resolved snapshot history must store non-delta snapshots.");
        }

        var baseline = SnapshotBaselineState.FromSnapshot(fullSnapshot);
        if (!_snapshotStatesByFrame.ContainsKey(fullSnapshot.Frame))
        {
            _snapshotFrameOrder.Enqueue(fullSnapshot.Frame);
        }

        _snapshotStatesByFrame[fullSnapshot.Frame] = baseline;
        RememberSnapshotSoundEvents(fullSnapshot);
        TrimSnapshotHistory();
    }

    public void AcknowledgeSnapshot(ulong frame)
    {
        if (!_snapshotStatesByFrame.ContainsKey(frame) || frame <= LastAcknowledgedSnapshotFrame)
        {
            return;
        }

        AcknowledgeSnapshotSoundEvents(frame);
        LastAcknowledgedSnapshotFrame = frame;
        PruneOlderSnapshotHistory(frame);
    }

    public bool TryGetSnapshotState(ulong frame, out SnapshotBaselineState snapshot)
    {
        return _snapshotStatesByFrame.TryGetValue(frame, out snapshot!);
    }

    public bool HasAcknowledgedSoundEvent(ulong eventId)
    {
        return eventId != 0 && _acknowledgedSoundEventIds.Contains(eventId);
    }

    public void ResetSnapshotHistory()
    {
        LastAcknowledgedSnapshotFrame = 0;
        _snapshotStatesByFrame.Clear();
        _snapshotFrameOrder.Clear();
        _snapshotSoundEventIdsByFrame.Clear();
        _acknowledgedSoundEventIds.Clear();
        _acknowledgedSoundEventOrder.Clear();
    }

    private void TrimSnapshotHistory()
    {
        var targetHistoryCount = ComputeTargetHistoryCount();
        while (_snapshotFrameOrder.Count > targetHistoryCount)
        {
            var oldestFrame = _snapshotFrameOrder.Dequeue();
            _snapshotStatesByFrame.Remove(oldestFrame);
            _snapshotSoundEventIdsByFrame.Remove(oldestFrame);
            if (oldestFrame == LastAcknowledgedSnapshotFrame)
            {
                LastAcknowledgedSnapshotFrame = 0;
            }
        }
    }

    private void PruneOlderSnapshotHistory(ulong acknowledgedFrame)
    {
        while (_snapshotFrameOrder.Count > 0 && _snapshotFrameOrder.Peek() < acknowledgedFrame)
        {
            var removedFrame = _snapshotFrameOrder.Dequeue();
            _snapshotStatesByFrame.Remove(removedFrame);
            _snapshotSoundEventIdsByFrame.Remove(removedFrame);
        }
    }

    private void RememberSnapshotSoundEvents(SnapshotMessage snapshot)
    {
        if (snapshot.SoundEvents.Count == 0)
        {
            _snapshotSoundEventIdsByFrame.Remove(snapshot.Frame);
            return;
        }

        var soundEventIds = new List<ulong>(snapshot.SoundEvents.Count);
        for (var index = 0; index < snapshot.SoundEvents.Count; index += 1)
        {
            var eventId = snapshot.SoundEvents[index].EventId;
            if (eventId != 0)
            {
                soundEventIds.Add(eventId);
            }
        }

        if (soundEventIds.Count == 0)
        {
            _snapshotSoundEventIdsByFrame.Remove(snapshot.Frame);
            return;
        }

        _snapshotSoundEventIdsByFrame[snapshot.Frame] = soundEventIds.ToArray();
    }

    private void AcknowledgeSnapshotSoundEvents(ulong frame)
    {
        if (!_snapshotSoundEventIdsByFrame.TryGetValue(frame, out var soundEventIds))
        {
            return;
        }

        for (var index = 0; index < soundEventIds.Length; index += 1)
        {
            AddAcknowledgedSoundEvent(soundEventIds[index]);
        }
    }

    private void AddAcknowledgedSoundEvent(ulong eventId)
    {
        if (eventId == 0 || !_acknowledgedSoundEventIds.Add(eventId))
        {
            return;
        }

        _acknowledgedSoundEventOrder.Enqueue(eventId);
        while (_acknowledgedSoundEventOrder.Count > AcknowledgedSoundEventHistoryLimit)
        {
            _acknowledgedSoundEventIds.Remove(_acknowledgedSoundEventOrder.Dequeue());
        }
    }

    private int ComputeTargetHistoryCount()
    {
        if (_snapshotFrameOrder.Count == 0)
        {
            return MinimumSnapshotHistoryLimit;
        }

        var newestFrame = _snapshotFrameOrder.Peek();
        foreach (var frame in _snapshotFrameOrder)
        {
            newestFrame = frame;
        }

        var pendingFrames = LastAcknowledgedSnapshotFrame == 0 || newestFrame <= LastAcknowledgedSnapshotFrame
            ? _snapshotFrameOrder.Count
            : (int)Math.Clamp(newestFrame - LastAcknowledgedSnapshotFrame, 0UL, int.MaxValue);
        var targetHistoryCount = pendingFrames + SnapshotHistorySlackFrames;
        return Math.Clamp(targetHistoryCount, MinimumSnapshotHistoryLimit, MaximumSnapshotHistoryLimit);
    }

    private readonly record struct SequencedPlayerInputSnapshot(uint Sequence, PlayerInputSnapshot Input);
}
