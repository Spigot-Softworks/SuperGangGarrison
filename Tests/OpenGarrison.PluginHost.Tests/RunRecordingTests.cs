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

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void CompletedRunCanBeReplayedWithoutTrustingItsClaimedScore(int playerCount)
    {
        using var host = new EmbeddedSessionHost(new() { MaximumPlayers = playerCount, Seed = 123 }, requireRoomAdmission: playerCount > 1);
        using var owner = new NetworkGameClient();
        using var guest = new NetworkGameClient();
        var clients = playerCount == 1 ? new[] { owner } : new[] { owner, guest };
        for (var index = 0; index < clients.Length; index++)
        {
            var id = Guid.NewGuid();
            Assert.True(clients[index].Connect(new EmbeddedSessionClientTransport(host.CreatePeer(local: index == 0, slot: (byte)(index + 1), clientId: id)),
                "Recorder" + index, 0, out _, clientInstanceId: id));
        }
        LastToDieRecording? recording = null;
        OpenGarrison.Core.LastToDie.LastToDieRunOutcome? original = null;
        var readySent = new HashSet<byte>();
        var startSent = false;
        var survivorSent = new HashSet<byte>();
        var selectedOffers = new Dictionary<byte, ulong>();
        for (var tick = 0; tick < 12000; tick++)
        {
            Tick(host, clients);
            if (host.TryTakeRecordedRun(out recording!, out original!)) break;
            foreach (var client in clients)
            {
                var snapshot = client.LastToDieState.Snapshot;
                var player = snapshot?.Players.FirstOrDefault(p => p.Slot == client.LocalPlayerSlot);
                if (player is null) continue;
                if (readySent.Add(client.LocalPlayerSlot)) client.SendLastToDieCommand(LastToDieCommandKind.Ready);
                if (client == owner && snapshot!.Players.All(p => p.IsReady) && !startSent) { client.SendLastToDieCommand(LastToDieCommandKind.RequestStart); startSent = true; }
                if (snapshot!.Phase == LastToDieWirePhase.SurvivorChoice && survivorSent.Add(client.LocalPlayerSlot))
                { client.SendLastToDieCommand(LastToDieCommandKind.ChooseSurvivor, "ltd.survivor.engineer"); }
                if (snapshot.Phase == LastToDieWirePhase.RewardChoice && player.ActiveOfferId != 0 && selectedOffers.GetValueOrDefault(client.LocalPlayerSlot) != player.ActiveOfferId)
                { client.SendLastToDieCommand(LastToDieCommandKind.SelectReward, player.ActiveOfferChoices[0], player.ActiveOfferId); selectedOffers[client.LocalPlayerSlot] = player.ActiveOfferId; }
                if (snapshot.Phase == LastToDieWirePhase.Playing)
                {
                    host.World.TryGetNetworkPlayer(client.LocalPlayerSlot, out var actor);
                    var opponents = SimulationWorld.NetworkPlayerSlots
                        .Select(slot => host.World.TryGetNetworkPlayer(slot, out var target) ? target : null)
                        .Where(target => target is { IsAlive: true } && target.Team != actor.Team)
                        .OrderBy(target => MathF.Abs(target!.X - actor.X));
                    var nearest = opponents.FirstOrDefault();
                    client.SendInput(default(PlayerInputSnapshot) with
                    {
                        Right = true, FirePrimary = tick < 2400,
                        AimWorldX = nearest?.X ?? 1000, AimWorldY = nearest?.Y ?? 500,
                    }, 0, 0);
                }
            }
        }
        Assert.NotNull(recording);
        Assert.NotNull(original);
        Assert.Equal(playerCount, original.Participants.Count);
        if (playerCount == 1 && Environment.GetEnvironmentVariable("OG2_TEST_RECORDING_PATH") is { Length: > 0 } proofPath)
            File.WriteAllBytes(proofPath, recording.Compress());
        var replay = LastToDieRecordingVerifier.Verify(LastToDieRecording.Read(recording.Compress()));
        Assert.Equal(original.AttemptId, replay.AttemptId);
        Assert.Equal(original.CompletedFrame, replay.CompletedFrame);
        Assert.Equal(original.CompletedRounds, replay.CompletedRounds);
        Assert.Equal(original.Difficulty, replay.Difficulty);
        Assert.Equal(original.Participants, replay.Participants);
        Assert.Throws<InvalidDataException>(() => LastToDieRecordingVerifier.Verify(recording with { Ruleset = "invented" }));
        Assert.Throws<InvalidDataException>(() => LastToDieRecordingVerifier.Verify(recording with
        { ExpectedOutcome = original with { CompletedRounds = original.CompletedRounds + 100 } }));
    }

    [Fact]
    public void RecordingDecoderRejectsOversizedCompressedInput()
    {
        Assert.Throws<InvalidDataException>(() => LastToDieRecording.Read(new byte[LastToDieRecording.MaximumCompressedBytes + 1]));
    }
}
