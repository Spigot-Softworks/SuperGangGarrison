using System.Collections;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using OpenGarrison.Client;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class LegacyGg2WireTests
{
    [Fact]
    public void HelloUsesGg2UuidTextOrder()
    {
        Assert.Equal(
            Convert.FromHexString("00b31c220942569a19d0efc71c5373bd75"),
            LegacyGg2Wire.CreateHello());
    }

    [Fact]
    public void ReserveSlotAndJoinAreSeparateCommands()
    {
        Assert.Equal(
            [60, 3, (byte)'G', (byte)'G', (byte)'2'],
            LegacyGg2Wire.CreateReserveSlot("GG2"));
        Assert.Equal(20, LegacyGg2Wire.CreateReserveSlot(new string('A', 30))[1]);
        Assert.Equal((byte)1, LegacyGg2Wire.PlayerJoin);
    }

    [Fact]
    public void InputUsesGg2KeyBitsAndLittleEndianAngle()
    {
        var input = new InputStateMessage(
            1,
            InputButtons.Left | InputButtons.Up | InputButtons.FirePrimary | InputButtons.FireSecondary,
            0f,
            -100f,
            -1);

        Assert.Equal([6, 0xD8, 0x00, 0x40, 50], LegacyGg2Wire.CreateInputState(input));
    }

    [Fact]
    public void Gg2JumpReleasesWithoutSggInputAcknowledgement()
    {
        Assert.False(Game1.ShouldRetainJumpPressUntilAcknowledged(isLegacyGg2: true, latchedSequence: 10));
        Assert.True(Game1.ShouldRetainJumpPressUntilAcknowledged(isLegacyGg2: false, latchedSequence: 10));
        Assert.False(Game1.ShouldRetainJumpPressUntilAcknowledged(isLegacyGg2: false, latchedSequence: 0));
    }

    [Fact]
    public void LiveSnapshotsFollowStateTicksAndRosterChanges()
    {
        var isBoundary = ReDsmReplayTransport.ReDsmReplayTranslator.LegacyReplaySession.IsLiveSnapshotBoundary;
        Assert.True(isBoundary(6)); // INPUTSTATE
        Assert.True(isBoundary(9)); // QUICK_UPDATE
        Assert.True(isBoundary(5)); // PLAYER_SPAWN
        Assert.False(isBoundary(54)); // WEAPON_FIRE is folded into the next state tick
        Assert.False(isBoundary(55)); // plugin packet must not advance gameplay time
    }

    [Fact]
    public void LegacyInputKeepsStockControlsAndClearsSggActions()
    {
        var input = default(PlayerInputSnapshot) with
        {
            Left = true,
            Up = true,
            FirePrimary = true,
            FireSecondary = true,
            DropIntel = true,
            AimWorldX = 120f,
            BuildSentry = true,
            DestroySentry = true,
            UseAbility = true,
            InteractWeapon = true,
            SwapWeapon = true,
            ToggleSecondaryWeapon = true,
            BuildDispenser = true,
            DestroyDispenser = true,
        };

        var stock = LegacyGg2Wire.KeepStockInput(input);
        Assert.True(stock.Left && stock.Up && stock.FirePrimary && stock.FireSecondary && stock.DropIntel);
        Assert.Equal(120f, stock.AimWorldX);
        Assert.False(stock.BuildSentry || stock.DestroySentry || stock.UseAbility
            || stock.InteractWeapon || stock.SwapWeapon || stock.ToggleSecondaryWeapon
            || stock.BuildDispenser || stock.DestroyDispenser);
    }

    [Fact]
    public void StockSpecialCommandsStayClassSpecific()
    {
        Assert.Equal((byte)16, LegacyGg2Wire.GetStockSpecialCommand(PlayerClass.Engineer, hasSentry: false));
        Assert.Equal((byte)17, LegacyGg2Wire.GetStockSpecialCommand(PlayerClass.Engineer, hasSentry: true));
        Assert.Equal((byte)24, LegacyGg2Wire.GetStockSpecialCommand(PlayerClass.Heavy, hasSentry: false));
        Assert.Equal((byte)41, LegacyGg2Wire.GetStockSpecialCommand(PlayerClass.Sniper, hasSentry: false));
        Assert.Null(LegacyGg2Wire.GetStockSpecialCommand(PlayerClass.Demoman, hasSentry: false));
    }

    [Fact]
    public void TeamAndClassIdsFollowGg2Ordering()
    {
        Assert.Equal([3, 0], LegacyGg2Wire.CreateTeamSelection(PlayerTeam.Red));
        Assert.Equal([3, 1], LegacyGg2Wire.CreateTeamSelection(PlayerTeam.Blue));
        Assert.Equal([4, 3], LegacyGg2Wire.CreateClassSelection(PlayerClass.Demoman));
        Assert.Equal([4, 8], LegacyGg2Wire.CreateClassSelection(PlayerClass.Pyro));
    }

    [Fact]
    public void BundledGg2ControlsReachWireOnceAndAcknowledgeClientCommands()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Assert.True(LegacyGg2NetworkClientTransport.TryConnect(
            IPAddress.Loopback.ToString(), port, out var transportInterface, out var connectError), connectError);
        var transport = Assert.IsType<LegacyGg2NetworkClientTransport>(transportInterface);
        using var serverPeer = listener.AcceptTcpClient();
        var serverStream = serverPeer.GetStream();
        serverStream.ReadTimeout = 2000;

        typeof(LegacyGg2NetworkClientTransport)
            .GetField("_joined", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(transport, 1);
        ((IDictionary)typeof(LegacyGg2NetworkClientTransport)
            .GetField("_lastControlCommandSequences", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(transport)!).Add(ControlCommandKind.SelectTeam, uint.MaxValue);

        using var client = new NetworkGameClient();
        Assert.True(client.Connect(transport, "Tester", 0, out var clientConnectError), clientConnectError);
        var hello = new byte[LegacyGg2Wire.CreateHello().Length];
        serverStream.ReadExactly(hello);
        Assert.Equal(LegacyGg2Wire.CreateHello(), hello);

        client.QueueTeamSelection(PlayerTeam.Red);
        client.QueueClassSelection(PlayerClass.Demoman);
        client.SendInput(default, 0f, 0f);

        // Retransmit the exact same bundled controls before their local ACKs
        // are consumed. The adapter must acknowledge them again without
        // repeating the non-idempotent class change on the GG2 wire.
        var repeatedCommands = new ControlCommandMessage[]
        {
            new(1, ControlCommandKind.SelectTeam, (byte)PlayerTeam.Red),
            new(2, ControlCommandKind.SelectClass, (byte)PlayerClass.Demoman),
        };
        var repeatedInput = new InputStateMessage(
            2,
            InputButtons.None,
            0f,
            0f,
            -1,
            BundledControlCommands: repeatedCommands);
        transport.Send(ProtocolCodec.Serialize(repeatedInput));
        transport.Send(ProtocolCodec.Serialize(new ControlCommandMessage(
            0, ControlCommandKind.SelectTeam, (byte)PlayerTeam.Red)));

        var firstInput = new InputStateMessage(1, InputButtons.None, 0f, 0f, -1);
        var expectedWire = Concatenate(
            LegacyGg2Wire.CreateTeamSelection(PlayerTeam.Red),
            LegacyGg2Wire.CreateClassSelection(PlayerClass.Demoman),
            LegacyGg2Wire.CreateInputState(firstInput),
            LegacyGg2Wire.CreateInputState(repeatedInput));
        var actualWire = new byte[expectedWire.Length];
        serverStream.ReadExactly(actualWire);
        Assert.Equal(expectedWire, actualWire);

        var acknowledgements = client.ReceiveMessages().OfType<ControlAckMessage>().ToArray();
        Assert.Equal(5, acknowledgements.Length);
        Assert.All(acknowledgements.Take(4), acknowledgement => Assert.True(acknowledgement.Accepted));
        Assert.False(acknowledgements[4].Accepted);

        foreach (var acknowledgement in acknowledgements)
        {
            client.AcknowledgeControlCommand(acknowledgement.Sequence, acknowledgement.Kind);
        }

        var pendingCommands = (IDictionary)typeof(NetworkGameClient)
            .GetField("_pendingControlCommands", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(client)!;
        Assert.Empty(pendingCommands);
    }

    [Fact]
    public void ReadsServerHelloIncludingPluginDeclaration()
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.Latin1, leaveOpen: true))
        {
            writer.Write((byte)0);
            WriteByteLengthString(writer, "Vanilla");
            WriteByteLengthString(writer, "ctf_2dfort");
            WriteByteLengthString(writer, string.Empty);
            writer.Write((byte)0);
            writer.Write((ushort)0);
        }

        stream.Position = 0;
        using var reader = new BinaryReader(stream, Encoding.Latin1);
        var hello = LegacyGg2Wire.ReadServerHello(reader);

        Assert.Equal("Vanilla", hello.ServerName);
        Assert.Equal("ctf_2dfort", hello.MapName);
        Assert.Equal(string.Empty, hello.MapMd5);
        Assert.False(hello.PluginsRequired);
        Assert.Equal(string.Empty, hello.PluginList);
    }

    [Fact]
    public void VindicatorChatPluginUsesItsListIndexAndNativePacketFormat()
    {
        Assert.True(LegacyGg2ChatWire.TryGetPluginId(
            "other@123," + LegacyGg2ChatWire.SupportedPlugin, out var pluginId));
        Assert.Equal((byte)1, pluginId);
        Assert.Equal([55, 2, 0, 1, 0], LegacyGg2ChatWire.CreateHello(pluginId));
        Assert.Equal(
            [55, 5, 0, 1, 2, 2, (byte)'h', (byte)'i'],
            LegacyGg2ChatWire.CreateChat(pluginId, "hi", teamOnly: true));
    }

    [Fact]
    public void ReadsPlayerAndSystemChatPayloads()
    {
        Assert.Equal(
            new LegacyGg2ChatMessage(4, 2, "hi"),
            LegacyGg2ChatWire.ReadChatMessage([4, 2, 2, (byte)'h', (byte)'i']));
        Assert.Equal(
            new LegacyGg2ChatMessage(254, 0, "ok"),
            LegacyGg2ChatWire.ReadChatMessage([254, 2, (byte)'o', (byte)'k']));
    }

    private static void WriteByteLengthString(BinaryWriter writer, string value)
    {
        writer.Write(checked((byte)value.Length));
        writer.Write(Encoding.Latin1.GetBytes(value));
    }

    private static byte[] Concatenate(params byte[][] segments)
        => segments.SelectMany(static segment => segment).ToArray();
}
