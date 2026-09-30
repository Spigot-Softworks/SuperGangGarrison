using OpenGarrison.Client;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using OpenGarrison.SessionRuntime;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class RunRecordingTests
{
    private static void Tick(EmbeddedSessionHost host, params NetworkGameClient[] clients)
    {
        host.Advance(1d / 30);
        foreach (var client in clients)
        foreach (var message in client.ReceiveMessages())
            if (message is WelcomeMessage welcome) client.SetLocalPlayerSlot(welcome.PlayerSlot);
            else if (message is SnapshotMessage snapshot)
            {
                client.AcknowledgeSnapshot(snapshot.Frame);
                client.NotifyWorldSnapshotApplied(snapshot);
            }
    }

    [Fact]
    public void RecordingDecoderRejectsOversizedCompressedInput()
    {
        Assert.Throws<InvalidDataException>(() => LastToDieRecording.Read(new byte[LastToDieRecording.MaximumCompressedBytes + 1]));
    }
}
