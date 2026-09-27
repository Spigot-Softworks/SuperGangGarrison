using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using OpenGarrison.Client;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class LegacyGg2NetworkClientTransportTests
{
    [Fact]
    public async Task DownloadsMapRotationWithoutDroppingGameplayConnection()
    {
        var mapPath = ProjectSourceLocator.FindFile(Path.Combine("Core", "Content", "StockMaps", "koth_valley.png"));
        Assert.False(string.IsNullOrWhiteSpace(mapPath));
        var png = File.ReadAllBytes(mapPath);
        var md5 = Convert.ToHexStringLower(MD5.HashData(png));
        var cacheDirectory = Path.Combine(Path.GetTempPath(), "og-gg2-rotation-test-" + Guid.NewGuid().ToString("N"));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            Assert.True(LegacyGg2NetworkClientTransport.TryConnect(
                "127.0.0.1", port, cacheDirectory, out var transport, out var error), error);
            Assert.NotNull(transport);
            using (transport)
            {
                var serverTask = Task.Run(async () =>
                {
                    using var gameplayPeer = await listener.AcceptTcpClientAsync();
                    gameplayPeer.ReceiveTimeout = 5000;
                    using var gameplayStream = gameplayPeer.GetStream();
                    using var gameplayReader = new BinaryReader(gameplayStream, Encoding.Latin1, leaveOpen: true);
                    using var gameplayWriter = new BinaryWriter(gameplayStream, Encoding.Latin1, leaveOpen: true);
                    Assert.Equal(LegacyGg2Wire.CreateHello(), gameplayReader.ReadBytes(17));
                    gameplayWriter.Write((byte)LegacyGg2Wire.Hello);
                    WriteShortString(gameplayWriter, "Rotation test");
                    WriteShortString(gameplayWriter, "koth_corinth");
                    WriteShortString(gameplayWriter, "");
                    gameplayWriter.Write((byte)0);
                    gameplayWriter.Write((ushort)0);
                    gameplayWriter.Flush();
                    Assert.Equal(LegacyGg2Wire.ReserveSlot, gameplayReader.ReadByte());
                    _ = gameplayReader.ReadBytes(gameplayReader.ReadByte());
                    gameplayWriter.Write(LegacyGg2Wire.ReserveSlot);
                    gameplayWriter.Flush();
                    Assert.Equal(LegacyGg2Wire.PlayerJoin, gameplayReader.ReadByte());

                    gameplayWriter.Write((byte)LegacyGg2Wire.JoinUpdate);
                    gameplayWriter.Write((byte)0);
                    gameplayWriter.Write((byte)1);
                    gameplayWriter.Write((byte)LegacyGg2Wire.ChangeMap);
                    WriteShortString(gameplayWriter, "koth_corinth");
                    WriteShortString(gameplayWriter, "");
                    gameplayWriter.Write((byte)8); // FULL_UPDATE
                    gameplayWriter.Write((ushort)0);
                    gameplayWriter.Write((byte)0);
                    gameplayWriter.Write((ushort)0); // red intel
                    gameplayWriter.Write((ushort)0); // blue intel
                    gameplayWriter.Write((byte)3); // cap limit
                    gameplayWriter.Write((byte)0); // red caps
                    gameplayWriter.Write((byte)0); // blue caps
                    gameplayWriter.Write((byte)10); // respawn seconds
                    gameplayWriter.Write((ushort)0); // KOTH unlock
                    gameplayWriter.Write((ushort)5400);
                    gameplayWriter.Write((ushort)5400);
                    gameplayWriter.Write((byte)255); // control point
                    gameplayWriter.Write((byte)255);
                    gameplayWriter.Write((ushort)0);
                    gameplayWriter.Write(new byte[10]); // class limits
                    gameplayWriter.Write((byte)LegacyGg2Wire.PlayerJoin);
                    WriteShortString(gameplayWriter, "Interop");
                    gameplayWriter.Flush();

                    gameplayWriter.Write((byte)LegacyGg2Wire.ChangeMap);
                    WriteShortString(gameplayWriter, "koth_valley");
                    WriteShortString(gameplayWriter, md5);
                    gameplayWriter.Flush();

                    using var mapPeer = await listener.AcceptTcpClientAsync();
                    mapPeer.ReceiveTimeout = 5000;
                    using var mapStream = mapPeer.GetStream();
                    using var mapReader = new BinaryReader(mapStream, Encoding.Latin1, leaveOpen: true);
                    using var mapWriter = new BinaryWriter(mapStream, Encoding.Latin1, leaveOpen: true);
                    Assert.Equal(LegacyGg2Wire.CreateHello(), mapReader.ReadBytes(17));
                    mapWriter.Write((byte)LegacyGg2Wire.Hello);
                    WriteShortString(mapWriter, "Rotation test");
                    WriteShortString(mapWriter, "koth_valley");
                    WriteShortString(mapWriter, md5);
                    mapWriter.Write((byte)0);
                    mapWriter.Write((ushort)0);
                    mapWriter.Flush();
                    Assert.Equal(LegacyGg2Wire.DownloadMap, mapReader.ReadByte());
                    mapWriter.Write(checked((uint)png.Length));
                    mapWriter.Write(png);
                    mapWriter.Flush();
                    await Task.Delay(300);
                });

                transport.Send(ProtocolCodec.Serialize(new HelloMessage("Interop", ProtocolVersion.Current, 0)));
                var welcome = Assert.IsType<WelcomeMessage>(await WaitForMessage<WelcomeMessage>(transport));
                Assert.Equal("gg2_stock_koth_corinth", welcome.LevelName);
                Assert.Equal("gg2_stock_koth_corinth", Assert.IsType<SnapshotMessage>(
                    await WaitForMessage<SnapshotMessage>(transport)).LevelName);
                var rotated = Assert.IsType<SnapshotMessage>(await WaitForMessage<SnapshotMessage>(transport));
                Assert.Equal("gg2_" + md5, rotated.LevelName);
                Assert.Equal(GameModeKind.KingOfTheHill, (GameModeKind)rotated.GameMode);
                await serverTask;
                Assert.Equal(png, File.ReadAllBytes(Path.Combine(cacheDirectory, $"gg2_{md5}.png")));
            }
        }
        finally
        {
            listener.Stop();
            if (Directory.Exists(cacheDirectory)) Directory.Delete(cacheDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task DownloadsAdvertisedCustomMapBeforeReservingSlot()
    {
        var mapPath = ProjectSourceLocator.FindFile(Path.Combine("Core", "Content", "StockMaps", "koth_corinth.png"));
        Assert.False(string.IsNullOrWhiteSpace(mapPath));
        var png = File.ReadAllBytes(mapPath);
        var md5 = Convert.ToHexStringLower(MD5.HashData(png));
        var cacheDirectory = Path.Combine(Path.GetTempPath(), "og-gg2-join-test-" + Guid.NewGuid().ToString("N"));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            Assert.True(LegacyGg2NetworkClientTransport.TryConnect(
                "127.0.0.1", port, cacheDirectory, out var transport, out var error), error);
            Assert.NotNull(transport);
            using (transport)
            {
                var serverTask = Task.Run(async () =>
                {
                    using var peer = await listener.AcceptTcpClientAsync();
                    peer.ReceiveTimeout = 5000;
                    using var stream = peer.GetStream();
                    using var reader = new BinaryReader(stream, Encoding.Latin1, leaveOpen: true);
                    using var writer = new BinaryWriter(stream, Encoding.Latin1, leaveOpen: true);
                    Assert.Equal(LegacyGg2Wire.CreateHello(), reader.ReadBytes(17));
                    writer.Write((byte)LegacyGg2Wire.Hello);
                    WriteShortString(writer, "Map test");
                    WriteShortString(writer, "koth_corinth");
                    WriteShortString(writer, md5);
                    writer.Write((byte)0);
                    writer.Write((ushort)0);
                    writer.Flush();

                    Assert.Equal(LegacyGg2Wire.DownloadMap, reader.ReadByte());
                    writer.Write(checked((uint)png.Length));
                    writer.Write(png);
                    writer.Flush();
                    Assert.Equal(LegacyGg2Wire.ReserveSlot, reader.ReadByte());
                    Assert.Equal("Interop", Encoding.Latin1.GetString(reader.ReadBytes(reader.ReadByte())));
                    writer.Write(LegacyGg2Wire.ReserveSlot);
                    writer.Flush();
                    Assert.Equal(LegacyGg2Wire.PlayerJoin, reader.ReadByte());

                    writer.Write((byte)LegacyGg2Wire.JoinUpdate);
                    writer.Write((byte)0); // no existing players
                    writer.Write((byte)1); // first map area
                    writer.Write((byte)7); // CHANGE_MAP
                    WriteShortString(writer, "koth_corinth");
                    WriteShortString(writer, md5);
                    writer.Write((byte)8); // FULL_UPDATE
                    writer.Write((ushort)0);
                    writer.Write((byte)0); // no player states
                    writer.Write((ushort)0); // red intel count
                    writer.Write((ushort)0); // blue intel count
                    writer.Write((byte)3); // cap limit
                    writer.Write((byte)0); // red caps
                    writer.Write((byte)0); // blue caps
                    writer.Write((byte)10); // respawn seconds
                    writer.Write((ushort)0); // KOTH unlock
                    writer.Write((ushort)5400);
                    writer.Write((ushort)5400);
                    writer.Write((byte)255); // single control point state
                    writer.Write((byte)255);
                    writer.Write((ushort)0);
                    writer.Write(new byte[10]); // class limits
                    writer.Write((byte)1); // local PLAYER_JOIN
                    WriteShortString(writer, "Interop");
                    writer.Flush();
                    await Task.Delay(300);
                });

                transport.Send(ProtocolCodec.Serialize(new HelloMessage("Interop", ProtocolVersion.Current, 0)));
                var welcome = Assert.IsType<WelcomeMessage>(await WaitForMessage<WelcomeMessage>(transport));
                Assert.Equal("gg2_" + md5, welcome.LevelName);
                var snapshot = Assert.IsType<SnapshotMessage>(await WaitForMessage<SnapshotMessage>(transport));
                Assert.Equal(welcome.LevelName, snapshot.LevelName);
                Assert.Equal(GameModeKind.KingOfTheHill, (GameModeKind)snapshot.GameMode);
                await serverTask;
                Assert.Equal(png, File.ReadAllBytes(Path.Combine(cacheDirectory, $"gg2_{md5}.png")));
            }
        }
        finally
        {
            listener.Stop();
            if (Directory.Exists(cacheDirectory)) Directory.Delete(cacheDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task JoinsStockKothServerAndExchangesTeamClassInputAndChat()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Assert.True(LegacyGg2NetworkClientTransport.TryConnect("127.0.0.1", port, out var transport, out var error), error);
        Assert.NotNull(transport);
        using (transport)
        {
            var serverTask = Task.Run(async () =>
            {
                using var peer = await listener.AcceptTcpClientAsync();
                peer.ReceiveTimeout = 5000;
                using var stream = peer.GetStream();
                using var reader = new BinaryReader(stream, Encoding.Latin1, leaveOpen: true);
                using var writer = new BinaryWriter(stream, Encoding.Latin1, leaveOpen: true);

                Assert.Equal(LegacyGg2Wire.CreateHello(), reader.ReadBytes(17));
                writer.Write((byte)0);
                WriteShortString(writer, "Test GG2");
                WriteShortString(writer, "koth_corinth");
                WriteShortString(writer, "");
                writer.Write((byte)1);
                var pluginName = Encoding.Latin1.GetBytes(LegacyGg2ChatWire.SupportedPlugin);
                writer.Write(checked((ushort)pluginName.Length));
                writer.Write(pluginName);
                writer.Flush();

                Assert.Equal((byte)60, reader.ReadByte());
                var playerName = Encoding.Latin1.GetString(reader.ReadBytes(reader.ReadByte()));
                Assert.Equal("Interop", playerName);
                writer.Write((byte)60);
                writer.Flush();
                Assert.Equal((byte)1, reader.ReadByte());

                writer.Write((byte)44); // JOIN_UPDATE
                writer.Write((byte)1);  // one existing player
                writer.Write((byte)1);  // first map area
                writer.Write((byte)7);  // CHANGE_MAP
                WriteShortString(writer, "koth_corinth");
                WriteShortString(writer, "");
                writer.Write((byte)1); // existing PLAYER_JOIN
                WriteShortString(writer, "Existing");
                writer.Write(new byte[] { 4, 0, 1 }); // Soldier
                writer.Write(new byte[] { 3, 0, 1 }); // Blue
                writer.Write((byte)8);  // FULL_UPDATE
                writer.Write((ushort)0);
                writer.Write((byte)1);
                writer.Write(new byte[13]); // full scoreboard and queueJump
                writer.Write((ushort)0); // rewards
                writer.Write((byte)1); // has character
                writer.Write((byte)0); // keys
                writer.Write((ushort)0x4000); // GG2 90 degrees means upward
                writer.Write((byte)0); // aim distance
                writer.Write((ushort)1000); // x
                writer.Write((ushort)1000); // y
                writer.Write((sbyte)0); // hspeed
                writer.Write((sbyte)0); // vspeed
                writer.Write((byte)100); // health
                writer.Write((byte)4); // ammo
                writer.Write((byte)0); // movement flags
                writer.Write((byte)0); // animation offset
                writer.Write((byte)0); // class-specific state
                writer.Write((short)0); // alarm
                writer.Write((byte)0); // intel
                writer.Write((short)0); // intel recharge
                writer.Write((byte)1); // weapon ready
                writer.Write((byte)0); // weapon cooldown; Soldier has no third weapon byte
                writer.Write((ushort)0); // red intel count
                writer.Write((ushort)0); // blue intel count
                writer.Write((byte)3);   // cap limit
                writer.Write((byte)0);   // red caps
                writer.Write((byte)0);   // blue caps
                writer.Write((byte)10);  // respawn seconds
                writer.Write((ushort)0); // KOTH unlock
                writer.Write((ushort)5400);
                writer.Write((ushort)5400);
                var level = SimpleLevelFactory.CreateImportedLevel("Corinth");
                Assert.NotNull(level);
                foreach (var _ in level.GetRoomObjects(RoomObjectType.ControlPoint))
                {
                    writer.Write((byte)255);
                    writer.Write((byte)255);
                    writer.Write((ushort)0);
                }

                for (var index = 0; index < 10; index++) writer.Write((byte)0);
                writer.Write((byte)1); // local PLAYER_JOIN
                WriteShortString(writer, playerName);
                writer.Flush();

                Assert.Equal([55, 2, 0, 0, 0], reader.ReadBytes(5)); // chat hello
                Assert.Equal([3, 0], reader.ReadBytes(2)); // red team
                writer.Write((byte)3); // PLAYER_CHANGETEAM
                writer.Write((byte)1);
                writer.Write((byte)0);
                writer.Flush();

                Assert.Equal([4, 0], reader.ReadBytes(2)); // scout
                writer.Write((byte)4); // PLAYER_CHANGECLASS
                writer.Write((byte)1);
                writer.Write((byte)0);
                writer.Flush();

                Assert.Equal([6, 0xD0, 0, 0x40, 50], reader.ReadBytes(5));
                Assert.Equal([55, 5, 0, 0, 1, 2, (byte)'h', (byte)'i'], reader.ReadBytes(8));
                writer.Write(new byte[] { 55, 6, 0, 0, 1, 0, 2, (byte)'h', (byte)'i' });
                writer.Flush();

                writer.Write(new byte[] { 4, 1, 5 }); // local class becomes Engineer
                writer.Write((byte)54); // Soldier WEAPON_FIRE; final ushort is a random seed
                writer.Write((byte)0);
                writer.Write((ushort)1000);
                writer.Write((ushort)1000);
                writer.Write((sbyte)0);
                writer.Write((sbyte)0);
                writer.Write((ushort)0xC000);
                writer.Write((byte)16); // BUILD_SENTRY establishes a known blast location
                writer.Write((byte)0);
                writer.Write((ushort)1200);
                writer.Write((ushort)1250);
                writer.Write((sbyte)1);
                writer.Write((byte)17); // DESTROY_SENTRY emits the small GG2 explosion
                writer.Write(new byte[] { 0, byte.MaxValue, byte.MaxValue, 0 });
                writer.Write(new byte[] { 5, 1, 0, 0 }); // local PLAYER_SPAWN
                writer.Write((byte)9); // QUICK_UPDATE
                writer.Write((byte)2);
                WriteQuickPlayer(writer, 200, 200, 0x4000); // GG2's 90-degree wire angle presents as up
                WriteQuickPlayer(writer, 240, 250); // local Engineer now has a real spawn position
                writer.Flush();
                Assert.Equal((byte)16, reader.ReadByte()); // stock BUILD_SENTRY on special press
                Assert.Equal([6, 0x08, 0, 0, 0], reader.ReadBytes(5));
                Assert.Equal([6, 0x08, 0, 0, 0], reader.ReadBytes(5)); // held special has no extra command
                Assert.Equal([6, 0, 0, 0, 0], reader.ReadBytes(5)); // release
                Assert.Equal((byte)16, reader.ReadByte()); // next press sends another stock command
                Assert.Equal([6, 0x08, 0, 0, 0], reader.ReadBytes(5));
                Assert.Equal([6, 0, 0, 0, 0], reader.ReadBytes(5)); // SGG-only ability sends no GG2 key bit
                writer.Write(LegacyGg2Wire.PlayerLeave);
                writer.Write((byte)0); // another client leaves before the local roster entry
                writer.Flush();
                await Task.Delay(3000);
            });

            transport.Send(ProtocolCodec.Serialize(new HelloMessage("Interop", ProtocolVersion.Current, 0)));
            var welcome = Assert.IsType<WelcomeMessage>(await WaitForMessage<WelcomeMessage>(transport));
            Assert.Equal("Test GG2", welcome.ServerName);
            Assert.Equal((byte)129, welcome.PlayerSlot);
            Assert.Equal("gg2_stock_koth_corinth", welcome.LevelName);
            // The first control must be accepted immediately after Welcome.
            transport.Send(ProtocolCodec.Serialize(new ControlCommandMessage(1, ControlCommandKind.SelectTeam, (byte)PlayerTeam.Red)));
            var initialSnapshot = Assert.IsType<SnapshotMessage>(await WaitForMessage<SnapshotMessage>(transport));
            Assert.Equal(2, initialSnapshot.Players.Count);
            Assert.Equal(
                CharacterClassCatalog.RuntimeRegistry.CreatePlayerLoadoutState(PlayerClass.Soldier).PrimaryItemId,
                initialSnapshot.Players[0].GameplayPrimaryItemId);
            Assert.Equal(
                CharacterClassCatalog.RuntimeRegistry.CreatePlayerLoadoutState(PlayerClass.Scout).PrimaryItemId,
                initialSnapshot.Players[1].GameplayPrimaryItemId);

            var slotChange = Assert.IsType<SessionSlotChangedMessage>(await WaitForMessage<SessionSlotChangedMessage>(transport));
            Assert.Equal((byte)2, slotChange.PlayerSlot);

            var binding = CharacterClassCatalog.RuntimeRegistry.GetRequiredClassBinding(PlayerClass.Scout);
            transport.Send(ProtocolCodec.Serialize(new ControlCommandMessage(2, ControlCommandKind.SelectClass, 0, binding.ClassId)));
            transport.Send(ProtocolCodec.Serialize(new InputStateMessage(
                3, InputButtons.Left | InputButtons.Up | InputButtons.FirePrimary,
                0f, -100f, -1)));
            transport.Send(ProtocolCodec.Serialize(new ChatSubmitMessage("hi")));
            var chat = Assert.IsType<ChatRelayMessage>(await WaitForMessage<ChatRelayMessage>(transport));
            Assert.Equal("Interop", chat.PlayerName);
            Assert.Equal("hi", chat.Text);
            SnapshotMessage? engineerSnapshot;
            var sawSmallExplosion = false;
            do
            {
                engineerSnapshot = Assert.IsType<SnapshotMessage>(await WaitForMessage<SnapshotMessage>(transport));
                sawSmallExplosion |= engineerSnapshot.VisualEvents.Any(visual =>
                    visual.EffectName == "ExplosionSmall" && visual.X == 240f && visual.Y == 250f);
            } while (!engineerSnapshot.Players[1].IsAlive
                || engineerSnapshot.Players[1].ClassId != (byte)PlayerClass.Engineer);
            Assert.Equal(270f, engineerSnapshot.Players[0].AimDirectionDegrees);
            Assert.True(engineerSnapshot.Players[0].AimWorldY < engineerSnapshot.Players[0].Y);
            Assert.True(sawSmallExplosion);

            transport.Send(ProtocolCodec.Serialize(new InputStateMessage(4, InputButtons.FireSecondary, 0f, 0f, -1)));
            transport.Send(ProtocolCodec.Serialize(new InputStateMessage(5, InputButtons.FireSecondary, 0f, 0f, -1)));
            transport.Send(ProtocolCodec.Serialize(new InputStateMessage(6, InputButtons.None, 0f, 0f, -1)));
            transport.Send(ProtocolCodec.Serialize(new InputStateMessage(7, InputButtons.FireSecondary, 0f, 0f, -1)));
            transport.Send(ProtocolCodec.Serialize(new InputStateMessage(8, InputButtons.UseAbility, 0f, 0f, -1)));
            var shiftedSlot = Assert.IsType<SessionSlotChangedMessage>(await WaitForMessage<SessionSlotChangedMessage>(transport));
            Assert.Equal((byte)1, shiftedSlot.PlayerSlot);
            await serverTask;
            // A server close racing the next input must not surface a disposed stream.
            transport.Send(ProtocolCodec.Serialize(new InputStateMessage(9, InputButtons.None, 0f, 0f, -1)));
        }

        listener.Stop();
    }

    private static async Task<IProtocolMessage> WaitForMessage<T>(INetworkClientMessageTransport transport)
        where T : IProtocolMessage
    {
        var timer = Stopwatch.StartNew();
        var seen = new List<string>();
        while (timer.Elapsed < TimeSpan.FromSeconds(5))
        {
            while (transport.TryReceive(out var payload))
            {
                if (ProtocolCodec.TryDeserialize(payload, out var message) && message is T)
                {
                    return message;
                }
                if (message is not null) seen.Add(message.GetType().Name);
            }

            if (transport.TryConsumeDisconnectReason(out var reason))
            {
                throw new Xunit.Sdk.XunitException($"GG2 transport disconnected: {reason}; messages seen: {string.Join(", ", seen)}");
            }

            await Task.Delay(10);
        }

        throw new TimeoutException($"GG2 transport did not emit {typeof(T).Name}.");
    }

    private static void WriteShortString(BinaryWriter writer, string text)
    {
        var bytes = Encoding.Latin1.GetBytes(text);
        writer.Write(checked((byte)bytes.Length));
        writer.Write(bytes);
    }

    private static void WriteQuickPlayer(BinaryWriter writer, int x, int y, ushort aim = 0)
    {
        writer.Write((byte)1); // character
        writer.Write((byte)0); // keys
        writer.Write(aim);
        writer.Write((byte)50); // aim distance
        writer.Write(checked((ushort)(x * 5)));
        writer.Write(checked((ushort)(y * 5)));
        writer.Write((sbyte)0);
        writer.Write((sbyte)0);
        writer.Write((byte)100); // health
        writer.Write((byte)4); // ammo
        writer.Write((byte)0); // flags
    }
}
