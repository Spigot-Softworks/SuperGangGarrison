using System.Text;
using OpenGarrison.Client;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class LegacyGg2DeathCamTests
{
    [Fact]
    public void LocalGg2DeathCreatesAndClearsKillCam()
    {
        var session = ReDsmReplayTransport.ReDsmReplayTranslator.LegacyReplaySession.CreateLive(
            new LegacyGg2ServerHello("Test", "koth_corinth", "", false, ""));
        Send(session, LegacyGg2Wire.JoinUpdate, [1, 1]);
        Send(session, LegacyGg2Wire.PlayerJoin, Name("Killer"));
        Send(session, LegacyGg2Wire.PlayerJoin, Name("Local"));
        Send(session, LegacyGg2Wire.PlayerChangeTeam, [0, 0]);
        Send(session, LegacyGg2Wire.PlayerChangeTeam, [1, 1]);

        Send(session, 10, [1, 0, 255, 9]); // Local player killed by player zero.
        var deathCam = Assert.IsType<OpenGarrison.Protocol.SnapshotDeathCamState>(
            session.CreateSnapshotMessage().LocalDeathCam);
        Assert.Equal("Killer", deathCam.KillerName);
        Assert.Equal(deathCam.InitialTicks, deathCam.RemainingTicks);
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        Assert.True(world.ApplySnapshot(session.CreateSnapshotMessage(), session.GetLocalPlayerSlot()));
        Assert.False(world.LocalPlayer.IsAlive);
        Assert.Equal("Killer", world.LocalDeathCam?.KillerName);
        session.AdvanceLegacyFrame([]);
        Assert.Equal(deathCam.RemainingTicks - 1, session.CreateSnapshotMessage().LocalDeathCam?.RemainingTicks);

        Send(session, 5, [1, 0, 0]); // Local player respawns.
        Assert.Null(session.CreateSnapshotMessage().LocalDeathCam);
    }

    private static void Send(
        ReDsmReplayTransport.ReDsmReplayTranslator.LegacyReplaySession session,
        byte messageType, byte[] payload)
    {
        using var stream = new MemoryStream(payload);
        using var reader = new BinaryReader(stream, Encoding.Latin1);
        session.ApplyLiveMessage(reader, messageType);
    }

    private static byte[] Name(string value)
    {
        var name = Encoding.Latin1.GetBytes(value);
        return [checked((byte)name.Length), .. name];
    }
}
