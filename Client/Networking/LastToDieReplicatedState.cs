using OpenGarrison.Protocol;

namespace OpenGarrison.Client;

internal enum LastToDieSnapshotApplyKind : byte
{
    Applied = 1,
    Duplicate = 2,
    Stale = 3,
    Rejected = 4,
}

internal sealed record LastToDieSnapshotApplyResult(
    LastToDieSnapshotApplyKind Kind,
    string Reason = "")
{
    public bool Applied => Kind == LastToDieSnapshotApplyKind.Applied;
}

/// <summary>
/// Client-side read model for semantic LTD state. It never advances phases or
/// generates offers; it only accepts monotonic server snapshots and results.
/// </summary>
internal sealed class LastToDieReplicatedState
{
    private const int MaximumCommandResults = 128;
    private readonly Dictionary<ulong, LastToDieCommandResultMessage> _commandResults = [];
    private readonly Queue<ulong> _commandResultOrder = [];

    public LastToDieRunSnapshotMessage? Snapshot { get; private set; }

    public LastToDieSnapshotApplyResult ApplySnapshot(LastToDieRunSnapshotMessage snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.RunId == Guid.Empty || snapshot.StructuralRevision == 0 || snapshot.ServerTick < 0)
        {
            return new LastToDieSnapshotApplyResult(
                LastToDieSnapshotApplyKind.Rejected,
                "Last to Die snapshot identity is invalid.");
        }

        var current = Snapshot;
        if (current is not null)
        {
            if (snapshot.RunId != current.RunId)
            {
                return new LastToDieSnapshotApplyResult(
                    LastToDieSnapshotApplyKind.Rejected,
                    "Last to Die snapshot belongs to another run.");
            }

            if (snapshot.StructuralRevision < current.StructuralRevision
                || (snapshot.StructuralRevision == current.StructuralRevision
                    && snapshot.ServerTick < current.ServerTick))
            {
                return new LastToDieSnapshotApplyResult(
                    LastToDieSnapshotApplyKind.Stale,
                    "Last to Die snapshot is older than the applied state.");
            }

            if (snapshot.StructuralRevision == current.StructuralRevision
                && snapshot.ServerTick == current.ServerTick)
            {
                return new LastToDieSnapshotApplyResult(LastToDieSnapshotApplyKind.Duplicate);
            }
        }

        Snapshot = Freeze(snapshot);
        return new LastToDieSnapshotApplyResult(LastToDieSnapshotApplyKind.Applied);
    }

    public void ApplyCommandResult(LastToDieCommandResultMessage result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.CommandId == 0)
        {
            return;
        }

        if (_commandResults.ContainsKey(result.CommandId))
        {
            _commandResults[result.CommandId] = result;
            return;
        }

        _commandResults.Add(result.CommandId, result);
        _commandResultOrder.Enqueue(result.CommandId);
        while (_commandResultOrder.Count > MaximumCommandResults)
        {
            _commandResults.Remove(_commandResultOrder.Dequeue());
        }
    }

    public bool TryGetCommandResult(
        ulong commandId,
        out LastToDieCommandResultMessage result)
        => _commandResults.TryGetValue(commandId, out result!);

    public LastToDieRunSnapshotAckMessage? CreateSnapshotAcknowledgement()
        => Snapshot is null
            ? null
            : new LastToDieRunSnapshotAckMessage(Snapshot.RunId, Snapshot.StructuralRevision);

    public void Reset()
    {
        Snapshot = null;
        _commandResults.Clear();
        _commandResultOrder.Clear();
    }

    private static LastToDieRunSnapshotMessage Freeze(LastToDieRunSnapshotMessage snapshot)
    {
        var players = snapshot.Players
            .Select(player => player with
            {
                OwnedPerkIds = Array.AsReadOnly(player.OwnedPerkIds.ToArray()),
                ActiveOfferChoices = Array.AsReadOnly(player.ActiveOfferChoices.ToArray()),
            })
            .ToArray();
        return snapshot with { Players = Array.AsReadOnly(players) };
    }
}
