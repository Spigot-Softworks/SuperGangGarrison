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
    private readonly Dictionary<ulong, SnapshotBaselineState> _snapshotStatesByFrame = new();
    private readonly Queue<ulong> _snapshotFrameOrder = new();

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
    public uint LastTeamCommandSequence { get; set; }
    public uint LastClassCommandSequence { get; set; }
    public uint LastSpectateCommandSequence { get; set; }
    public uint LastGameplayLoadoutCommandSequence { get; set; }
    public ulong LastAcknowledgedSnapshotFrame { get; private set; }
    public bool IsAuthorized { get; set; } = true;
    public TimeSpan LastPasswordRequestSentAt { get; set; } = TimeSpan.MinValue;
    public OpenGarrisonServerAdminPermissions AdminPermissions { get; set; } = OpenGarrisonServerAdminPermissions.None;
    public TimeSpan AdminAuthenticatedAt { get; set; } = TimeSpan.MinValue;
    public string PendingAdminChatCommand { get; set; } = string.Empty;
    public TimeSpan PendingAdminChatCommandQueuedAt { get; set; } = TimeSpan.MinValue;
    public bool IsGagged { get; set; }
    public int SnapshotHistoryCount => _snapshotFrameOrder.Count;

    public bool TrySetLatestInput(uint sequence, PlayerInputSnapshot input)
    {
        if (HasAcceptedInput && !IsSequenceNewer(sequence, LastReceivedInputSequence))
        {
            return false;
        }

        HasAcceptedInput = true;
        LastReceivedInputSequence = sequence;
        LatestReceivedInput = input;
        return true;
    }

    public bool TryGetInputForNextTick(out PlayerInputSnapshot input)
    {
        if (HasAcceptedInput)
        {
            LatestAppliedInput = LatestReceivedInput;
            LastProcessedInputSequence = LastReceivedInputSequence;
            input = LatestAppliedInput;
            return true;
        }

        input = default;
        return false;
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
        TrimSnapshotHistory();
    }

    public void AcknowledgeSnapshot(ulong frame)
    {
        if (!_snapshotStatesByFrame.ContainsKey(frame) || frame <= LastAcknowledgedSnapshotFrame)
        {
            return;
        }

        LastAcknowledgedSnapshotFrame = frame;
        PruneOlderSnapshotHistory(frame);
    }

    public bool TryGetSnapshotState(ulong frame, out SnapshotBaselineState snapshot)
    {
        return _snapshotStatesByFrame.TryGetValue(frame, out snapshot!);
    }

    public void ResetSnapshotHistory()
    {
        LastAcknowledgedSnapshotFrame = 0;
        _snapshotStatesByFrame.Clear();
        _snapshotFrameOrder.Clear();
    }

    private void TrimSnapshotHistory()
    {
        var targetHistoryCount = ComputeTargetHistoryCount();
        while (_snapshotFrameOrder.Count > targetHistoryCount)
        {
            var oldestFrame = _snapshotFrameOrder.Dequeue();
            _snapshotStatesByFrame.Remove(oldestFrame);
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
            _snapshotStatesByFrame.Remove(_snapshotFrameOrder.Dequeue());
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
}
