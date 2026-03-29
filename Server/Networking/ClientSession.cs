using System;
using System.Collections.Generic;
using System.Net;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using static ServerHelpers;

sealed class ClientSession(byte slot, IPEndPoint endPoint, string name, TimeSpan lastSeen)
{
    private const int SnapshotHistoryLimit = 96;
    private readonly Dictionary<ulong, SnapshotMessage> _snapshotStatesByFrame = new();
    private readonly Queue<ulong> _snapshotFrameOrder = new();

    public byte Slot { get; set; } = slot;
    public IPEndPoint EndPoint { get; } = endPoint;
    public string Name { get; set; } = name;
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
    public ulong LastAcknowledgedSnapshotFrame { get; private set; }
    public bool IsAuthorized { get; set; } = true;
    public TimeSpan LastPasswordRequestSentAt { get; set; } = TimeSpan.MinValue;

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
        var fullSnapshot = snapshot.IsDelta
            ? SnapshotDelta.ToFullSnapshot(snapshot)
            : snapshot;
        if (!_snapshotStatesByFrame.ContainsKey(fullSnapshot.Frame))
        {
            _snapshotFrameOrder.Enqueue(fullSnapshot.Frame);
        }

        _snapshotStatesByFrame[fullSnapshot.Frame] = fullSnapshot;
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

    public bool TryGetSnapshotState(ulong frame, out SnapshotMessage snapshot)
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
        while (_snapshotFrameOrder.Count > SnapshotHistoryLimit)
        {
            var oldestFrame = _snapshotFrameOrder.Dequeue();
            if (oldestFrame == LastAcknowledgedSnapshotFrame)
            {
                _snapshotFrameOrder.Enqueue(oldestFrame);
                continue;
            }

            _snapshotStatesByFrame.Remove(oldestFrame);
        }
    }

    private void PruneOlderSnapshotHistory(ulong acknowledgedFrame)
    {
        while (_snapshotFrameOrder.Count > 0 && _snapshotFrameOrder.Peek() < acknowledgedFrame)
        {
            _snapshotStatesByFrame.Remove(_snapshotFrameOrder.Dequeue());
        }
    }
}
