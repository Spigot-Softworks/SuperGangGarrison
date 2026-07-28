using System;
using System.Collections.Generic;
using OpenGarrison.Protocol;

namespace OpenGarrison.Server;

/// <summary>
/// Per-session exactly-once ledger for protocol-64 one-shot input commands.
/// Transport retries may deliver a command more than once; only the first
/// accepted command enters the simulation queue.
/// </summary>
internal sealed class Protocol64InputCommandLedger
{
    private const int MaximumPendingCommands = 128;
    private const int MaximumRetainedResults = 512;

    private readonly Queue<Protocol64InputCommand> _pending = new();
    private readonly HashSet<ulong> _pendingIds = new();
    private readonly Dictionary<ulong, Protocol64InputCommandResult> _completed = new();
    private readonly Queue<ulong> _completedOrder = new();
    private uint _lastAcceptedSequence;

    public int PendingCount => _pending.Count;

    public bool TryEnqueue(
        Protocol64InputCommand command,
        uint serverTick,
        out Protocol64InputCommandResult? immediateResult)
    {
        if (command.CommandId == 0)
        {
            immediateResult = Rejected(command, serverTick, "Input command ID must be non-zero.");
            return false;
        }

        if (_completed.TryGetValue(command.CommandId, out var completed))
        {
            immediateResult = completed with
            {
                Result = Protocol64InputCommandResultKind.Duplicate,
                ServerTick = serverTick,
                Reason = "Input command was already completed.",
            };
            return false;
        }

        if (!_pendingIds.Add(command.CommandId))
        {
            immediateResult = new Protocol64InputCommandResult(
                command.CommandId,
                command.InputSequence,
                Protocol64InputCommandResultKind.Duplicate,
                serverTick,
                "Input command is already queued.",
                command.CommandSequence);
            return false;
        }

        if (_pending.Count >= MaximumPendingCommands)
        {
            _pendingIds.Remove(command.CommandId);
            immediateResult = Rejected(command, serverTick, "Input command queue is full.");
            return false;
        }

        var commandSequence = command.CommandSequence == 0
            ? command.InputSequence
            : command.CommandSequence;
        if (_lastAcceptedSequence != 0 && !IsSequenceNewer(commandSequence, _lastAcceptedSequence))
        {
            _pendingIds.Remove(command.CommandId);
            immediateResult = Rejected(command, serverTick, "Input command sequence is stale.");
            return false;
        }

        _lastAcceptedSequence = commandSequence;
        _pending.Enqueue(command);
        immediateResult = null;
        return true;
    }

    public bool TryDequeue(out Protocol64InputCommand command)
    {
        if (_pending.Count == 0)
        {
            command = null!;
            return false;
        }

        command = _pending.Dequeue();
        _pendingIds.Remove(command.CommandId);
        return true;
    }

    public Protocol64InputCommandResult Complete(
        Protocol64InputCommand command,
        uint serverTick,
        bool consumed,
        string reason = "")
    {
        var result = new Protocol64InputCommandResult(
            command.CommandId,
            command.InputSequence,
            consumed
                ? Protocol64InputCommandResultKind.Consumed
                : Protocol64InputCommandResultKind.Rejected,
            serverTick,
            reason ?? string.Empty,
            command.CommandSequence);

        _completed[command.CommandId] = result;
        _completedOrder.Enqueue(command.CommandId);
        while (_completedOrder.Count > MaximumRetainedResults)
        {
            _completed.Remove(_completedOrder.Dequeue());
        }

        return result;
    }

    public bool TryGetCompleted(ulong commandId, out Protocol64InputCommandResult result)
        => _completed.TryGetValue(commandId, out result!);

    public bool Acknowledge(ulong commandId)
        => _completed.Remove(commandId);

    private static Protocol64InputCommandResult Rejected(
        Protocol64InputCommand command,
        uint serverTick,
        string reason)
        => new(
            command.CommandId,
            command.InputSequence,
            Protocol64InputCommandResultKind.Rejected,
            serverTick,
            reason,
            command.CommandSequence);

    private static bool IsSequenceNewer(uint candidate, uint baseline)
    {
        var difference = unchecked(candidate - baseline);
        return difference != 0 && difference < 0x80000000u;
    }
}
