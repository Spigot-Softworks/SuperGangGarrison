using OpenGarrison.Protocol;
using OpenGarrison.Server;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class Protocol64InputCommandLedgerTests
{
    [Fact]
    public void DuplicateCommandIsNotQueuedOrConsumedTwice()
    {
        var ledger = new Protocol64InputCommandLedger();
        var command = Command(1, 10);

        Assert.True(ledger.TryEnqueue(command, 100, out var immediate));
        Assert.Null(immediate);
        Assert.False(ledger.TryEnqueue(command, 101, out immediate));
        Assert.Equal(Protocol64InputCommandResultKind.Duplicate, immediate!.Result);
        Assert.Equal(1, ledger.PendingCount);

        Assert.True(ledger.TryDequeue(out var dequeued));
        var result = ledger.Complete(dequeued, 102, consumed: true);
        Assert.Equal(Protocol64InputCommandResultKind.Consumed, result.Result);
        Assert.True(ledger.TryGetCompleted(command.CommandId, out var retained));
        Assert.Equal(result, retained);
    }

    [Fact]
    public void StaleCommandSequenceIsRejected()
    {
        var ledger = new Protocol64InputCommandLedger();

        Assert.True(ledger.TryEnqueue(Command(1, 20), 100, out _));
        Assert.False(ledger.TryEnqueue(Command(2, 19), 101, out var result));

        Assert.Equal(Protocol64InputCommandResultKind.Rejected, result!.Result);
        Assert.Equal(1, ledger.PendingCount);
    }

    [Fact]
    public void MultipleCommandsCanShareAnInputFrameWhenCommandSequencesAdvance()
    {
        var ledger = new Protocol64InputCommandLedger();

        Assert.True(ledger.TryEnqueue(Command(1, 20) with { CommandSequence = 1 }, 100, out _));
        Assert.True(ledger.TryEnqueue(Command(2, 20) with { CommandSequence = 2 }, 100, out _));
        Assert.Equal(2, ledger.PendingCount);
    }

    [Fact]
    public void ResultStaysRetainedUntilAcknowledged()
    {
        var ledger = new Protocol64InputCommandLedger();
        var command = Command(1, 1);
        Assert.True(ledger.TryEnqueue(command, 1, out _));
        Assert.True(ledger.TryDequeue(out var dequeued));
        ledger.Complete(dequeued, 2, consumed: false, reason: "not playable");

        Assert.True(ledger.TryGetCompleted(1, out _));
        Assert.True(ledger.Acknowledge(1));
        Assert.False(ledger.TryGetCompleted(1, out _));
    }

    private static Protocol64InputCommand Command(ulong id, uint sequence)
        => new(
            id,
            sequence,
            Protocol64InputCommandKind.Jump,
            InputButtons.Up,
            0f,
            0f);
}
