using System.IO;
using OpenGarrison.Protocol.Gg2;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class Gg2ProtocolCodecTests
{
    private static readonly Gg2MapLayout CtfLayout = new(Gg2HudKind.CaptureTheFlag, 0, 1);

    [Fact]
    public void JoinSequenceRoundTripsByteForByteWhenFedOneByteAtATime()
    {
        var (messages, encoded) = BuildJoinSequence();
        var stream = Concat(encoded);

        var reader = new Gg2ServerMessageReader();
        var buffer = new List<byte>();
        var decoded = new List<byte[]>();
        foreach (var value in stream)
        {
            buffer.Add(value);
            while (true)
            {
                var status = reader.TryRead(buffer.ToArray(), out var message, out var consumed);
                if (status == Gg2ReadStatus.NeedMoreData)
                {
                    break;
                }

                decoded.Add(Gg2ServerMessageWriter.Write(message!, RosterBefore(message!, reader), reader.MapLayout));
                buffer.RemoveRange(0, consumed);
                if (message is Gg2ChangeMapMessage)
                {
                    reader.SetMapLayout(CtfLayout);
                }
            }
        }

        Assert.Empty(buffer);
        Assert.Equal(messages.Count, decoded.Count);
        for (var index = 0; index < encoded.Count; index += 1)
        {
            Assert.Equal(encoded[index], decoded[index]);
        }

        Assert.Equal(new byte[] { Gg2Protocol.ClassMedic, Gg2Protocol.ClassDemoman, Gg2Protocol.ClassEngineer }, reader.RosterClasses);
        Assert.Equal(2, reader.LocalPlayerIndex);
    }

    [Fact]
    public void FullUpdateFieldsDecodeToGg2Values()
    {
        var (_, encoded) = BuildJoinSequence();
        var reader = new Gg2ServerMessageReader();
        Gg2StateUpdateMessage? full = null;
        foreach (var bytes in encoded)
        {
            Assert.Equal(Gg2ReadStatus.Message, reader.TryRead(bytes, out var message, out var consumed));
            Assert.Equal(bytes.Length, consumed);
            if (message is Gg2ChangeMapMessage)
            {
                reader.SetMapLayout(CtfLayout);
            }

            full = message as Gg2StateUpdateMessage ?? full;
        }

        Assert.NotNull(full);
        Assert.True(full!.IsFullUpdate);
        var medic = full.Players[0];
        Assert.Equal((byte)1, medic.Character!.Full!.Weapon.HealTarget);
        Assert.Equal(120.4f, medic.Character.Quick!.Value.X, 3);
        Assert.Equal(-2f, medic.Character.Quick.Value.HorizontalSpeed, 3);
        Assert.True(medic.Character.Quick.Value.IsCloaked);
        Assert.Equal(3, medic.Character.Quick.Value.MoveStatus);
        Assert.Equal(90f, medic.Character.AimDirectionDegrees, 3);
        var demo = full.Players[1];
        Assert.Equal(2, demo.Character!.Full!.Weapon.Mines!.Count);
        Assert.Equal(-1.4f, demo.Character.Full.Weapon.Mines[1].HorizontalSpeed, 3);
        var engineer = full.Players[2];
        Assert.Null(engineer.Character);
        Assert.False(engineer.Sentry!.Built);
        Assert.Equal(55, engineer.Sentry.Health);
        Assert.Equal((byte)7, engineer.Sentry.VerticalSpeed);
        Assert.Single(full.Platforms);
        var tail = full.Full!;
        Assert.Single(tail.RedIntel);
        Assert.Empty(tail.BlueIntel);
        Assert.Equal((byte)3, tail.CapLimit);
        var hud = Assert.IsType<Gg2CtfHudState>(tail.Hud);
        Assert.Equal(15u * 60u * 30u, hud.Timer);
    }

    [Fact]
    public void TruncatedMessageConsumesNothingAndLeavesTheRosterAlone()
    {
        var reader = new Gg2ServerMessageReader();
        var join = Gg2ServerMessageWriter.Write(new Gg2PlayerJoinMessage("Someone"), [], CtfLayout);
        Assert.Equal(Gg2ReadStatus.NeedMoreData, reader.TryRead(join.AsSpan(0, join.Length - 1), out var message, out var consumed));
        Assert.Null(message);
        Assert.Equal(0, consumed);
        Assert.Empty(reader.RosterClasses);

        Assert.Equal(Gg2ReadStatus.Message, reader.TryRead(join, out message, out consumed));
        Assert.Equal(join.Length, consumed);
        Assert.Single(reader.RosterClasses);
    }

    [Fact]
    public void InputStateNeedsNoMapLayoutButQuickUpdateDoes()
    {
        var reader = new Gg2ServerMessageReader();
        Assert.Equal(Gg2ReadStatus.Message, reader.TryRead(Gg2ServerMessageWriter.Write(new Gg2PlayerJoinMessage("A"), [], default), out _, out _));
        var input = new Gg2StateUpdateMessage(
            Gg2Protocol.InputState,
            0,
            [new Gg2PlayerState(null, new Gg2CharacterState(Gg2Protocol.KeyLeft, 0, 10, null, null), null)],
            [],
            null,
            null);
        var inputBytes = Gg2ServerMessageWriter.Write(input, reader.RosterClasses, default);
        Assert.Equal(1 + 1 + 1 + 4, inputBytes.Length);
        Assert.Equal(Gg2ReadStatus.Message, reader.TryRead(inputBytes, out var message, out _));
        Assert.Equal(Gg2Protocol.KeyLeft, ((Gg2StateUpdateMessage)message!).Players[0].Character!.KeyState);

        var quick = new Gg2StateUpdateMessage(Gg2Protocol.QuickUpdate, 0, input.Players, [], null, null);
        Assert.Throws<InvalidOperationException>(() => reader.TryRead(Gg2ServerMessageWriter.Write(quick, reader.RosterClasses, default), out _, out _));
    }

    [Fact]
    public void PlayerCountMismatchAndUnknownTypesAreRejected()
    {
        var reader = new Gg2ServerMessageReader();
        reader.SetMapLayout(CtfLayout);
        Assert.Throws<InvalidDataException>(() => reader.TryRead([Gg2Protocol.InputState, 1, 0], out _, out _));
        Assert.Throws<InvalidDataException>(() => reader.TryRead([0xEE], out _, out _));
    }

    [Fact]
    public void DestroyedGeneratorStopsBeingSerialized()
    {
        var layout = new Gg2MapLayout(Gg2HudKind.Generator, 0, 0);
        var reader = new Gg2ServerMessageReader();
        reader.SetMapLayout(layout);
        var both = new Gg2StateUpdateMessage(Gg2Protocol.CapsUpdate, 0, [], [], null,
            new Gg2CapsUpdateTail(0, 0, 5, new Gg2GeneratorHudState(10, 100, new Gg2GeneratorState(4000, 200), new Gg2GeneratorState(3000, 0))));
        var bothBytes = Gg2ServerMessageWriter.Write(both, [], layout);
        Assert.Equal(Gg2ReadStatus.Message, reader.TryRead(bothBytes, out _, out var consumed));
        Assert.Equal(bothBytes.Length, consumed);

        Assert.Equal(Gg2ReadStatus.Message, reader.TryRead([Gg2Protocol.GeneratorDestroy, Gg2Protocol.TeamRed], out _, out _));
        var blueOnly = both with { Caps = both.Caps! with { Hud = new Gg2GeneratorHudState(10, 100, new Gg2GeneratorState(4000, 200), null) } };
        var blueOnlyBytes = Gg2ServerMessageWriter.Write(blueOnly, [], layout);
        Assert.Equal(bothBytes.Length - 4, blueOnlyBytes.Length);
        Assert.Equal(Gg2ReadStatus.Message, reader.TryRead(blueOnlyBytes, out var message, out consumed));
        Assert.Equal(blueOnlyBytes.Length, consumed);
        var hud = Assert.IsType<Gg2GeneratorHudState>(((Gg2StateUpdateMessage)message!).Caps!.Hud);
        Assert.Null(hud.Red);
    }

    [Fact]
    public void PlayerLeaveShiftsTheLocalIndexLikeGg2()
    {
        var reader = new Gg2ServerMessageReader();
        Feed(reader, new Gg2JoinUpdateMessage(2, 1));
        Feed(reader, new Gg2PlayerJoinMessage("A"));
        Feed(reader, new Gg2PlayerJoinMessage("B"));
        Feed(reader, new Gg2PlayerJoinMessage("Me"));
        Feed(reader, new Gg2PlayerLeaveMessage(0));
        Assert.Equal(1, reader.LocalPlayerIndex);
        Assert.Equal(2, reader.RosterClasses.Count);
    }

    [Fact]
    public void ClientInputMatchesTheTranslatorsEncoding()
    {
        Assert.Equal(
            [6, 0xD8, 0x00, 0x40, 50],
            Gg2ClientMessages.InputStateFromAim(
                Gg2Protocol.KeyLeft | Gg2Protocol.KeyUp | Gg2Protocol.KeyPrimary | Gg2Protocol.KeySecondary,
                0f,
                -100f));
        Assert.Equal(Convert.FromHexString("00b31c220942569a19d0efc71c5373bd75"), Gg2ClientMessages.Hello());
        Assert.Equal([55, 3, 0, 9, 1, 2], Gg2ClientMessages.PluginPacket(9, [1, 2]));
        Assert.Equal([3, (byte)'a', (byte)'b', (byte)'c'], Gg2ClientMessages.Password("abc"));
    }

    private static void Feed(Gg2ServerMessageReader reader, Gg2ServerMessage message)
    {
        var bytes = Gg2ServerMessageWriter.Write(message, reader.RosterClasses, reader.MapLayout);
        Assert.Equal(Gg2ReadStatus.Message, reader.TryRead(bytes, out _, out var consumed));
        Assert.Equal(bytes.Length, consumed);
    }

    /// <summary>
    /// Re-encoding needs the roster the message was read against. Roster edits apply after
    /// a message is read, so undo the one this message made.
    /// </summary>
    private static IReadOnlyList<byte> RosterBefore(Gg2ServerMessage message, Gg2ServerMessageReader reader)
    {
        var roster = reader.RosterClasses.ToList();
        switch (message)
        {
            case Gg2PlayerJoinMessage:
                roster.RemoveAt(roster.Count - 1);
                break;
        }

        return roster;
    }

    private static (List<Gg2ServerMessage> Messages, List<byte[]> Encoded) BuildJoinSequence()
    {
        var messages = new List<Gg2ServerMessage>
        {
            new Gg2JoinUpdateMessage(2, 1),
            new Gg2ChangeMapMessage("ctf_test", string.Empty),
            new Gg2PlayerJoinMessage("Doc"),
            new Gg2PlayerChangeClassMessage(0, Gg2Protocol.ClassMedic),
            new Gg2PlayerChangeTeamMessage(0, Gg2Protocol.TeamRed),
            new Gg2PlayerJoinMessage("Boom"),
            new Gg2PlayerChangeClassMessage(1, Gg2Protocol.ClassDemoman),
            new Gg2PlayerChangeTeamMessage(1, Gg2Protocol.TeamBlue),
            new Gg2PlayerJoinMessage("Me"),
            new Gg2PlayerChangeClassMessage(2, Gg2Protocol.ClassEngineer),
            new Gg2PlayerChangeTeamMessage(2, Gg2Protocol.TeamBlue),
        };

        var stats = new Gg2PlayerStats(1, 2, 0, 3, 0, 0, 450, 0, 1, 0, 7, 0, "rewards", [0, 0]);
        var medic = new Gg2PlayerState(
            stats,
            new Gg2CharacterState(
                Gg2Protocol.KeyPrimary,
                16384,
                40,
                new Gg2CharacterQuickState(602, 1500, -17, 0, 120, 40, 0x01 | (3 << 1)),
                new Gg2CharacterFullState(2, 128, 0, 0, 0, new Gg2WeaponState(0, 0, 1, null))),
            null);
        var demo = new Gg2PlayerState(
            stats,
            new Gg2CharacterState(
                0,
                0,
                0,
                new Gg2CharacterQuickState(1000, 1000, 0, 17, 174, 6, 0),
                new Gg2CharacterFullState(0, 0, 30, 1, -5, new Gg2WeaponState(1, 4, null,
                [
                    new Gg2MineState(500, 600, 3, -2, 1),
                    new Gg2MineState(700, 800, -7, 0, 0),
                ]))),
            null);
        var engineer = new Gg2PlayerState(stats, null, new Gg2SentryState(-1, 2000, 3000, 55, 7));
        messages.Add(new Gg2StateUpdateMessage(
            Gg2Protocol.FullUpdate,
            90,
            [medic, demo, engineer],
            [new Gg2PlatformState(100, 200, 1)],
            new Gg2FullUpdateTail(
                [new Gg2IntelState(5000, 1000, -1)],
                [],
                3,
                1,
                2,
                5,
                new Gg2CtfHudState(15, 15 * 60 * 30),
                [0, 0, 0, 0, 0, 0, 0, 0, 0, 0]),
            null));

        // Encode against the roster each message is read with: joins add a scout, class
        // changes apply afterwards, so track them here the same way.
        var roster = new List<byte>();
        var encoded = new List<byte[]>();
        var layout = Gg2MapLayout.Unknown;
        foreach (var message in messages)
        {
            encoded.Add(Gg2ServerMessageWriter.Write(message, roster, layout));
            switch (message)
            {
                case Gg2PlayerJoinMessage:
                    roster.Add(Gg2Protocol.ClassScout);
                    break;
                case Gg2PlayerChangeClassMessage change:
                    roster[change.PlayerIndex] = change.ClassId;
                    break;
                case Gg2ChangeMapMessage:
                    layout = CtfLayout;
                    break;
            }
        }

        return (messages, encoded);
    }

    private static byte[] Concat(List<byte[]> parts) => parts.SelectMany(part => part).ToArray();
}
