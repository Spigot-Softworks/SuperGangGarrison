using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OpenGarrison.Protocol.Gg2;

/// <summary>The outcome of <see cref="Gg2ServerMessageReader.TryRead"/>.</summary>
public enum Gg2ReadStatus
{
    /// <summary>One message was read; <c>consumed</c> bytes belong to it.</summary>
    Message,

    /// <summary>The buffer ends inside a message. Nothing was consumed; read again with more bytes.</summary>
    NeedMoreData,
}

/// <summary>
/// Reads GG2 server messages from an incremental byte buffer, so the same code runs on a
/// TCP stream, a WebSocket relay in the browser, or a replay frame. GG2 messages have no
/// length prefix, so the reader keeps what their sizes depend on:
/// <list type="bullet">
/// <item>the roster (player count and each player's class), updated as it reads
/// PLAYER_JOIN / PLAYER_LEAVE / PLAYER_CHANGECLASS / BALANCE;</item>
/// <item>the map layout (HUD kind, control point and moving platform counts), which the
/// caller sets with <see cref="SetMapLayout"/> after loading the map a CHANGE_MAP names
/// and before reading further;</item>
/// <item>which generators still stand (a destroyed one stops being serialized).</item>
/// </list>
/// Formats follow GG2 Modern's ClientBeginStep, deserializeState and the objects' user
/// events 11/13.
/// </summary>
public sealed class Gg2ServerMessageReader
{
    private static readonly Encoding Latin1 = Encoding.Latin1;

    private readonly List<byte> _rosterClasses = new();
    private Gg2MapLayout _layout = Gg2MapLayout.Unknown;
    private bool _redGeneratorStanding = true;
    private bool _blueGeneratorStanding = true;

    /// <summary>Players in roster order, by GG2 class id.</summary>
    public IReadOnlyList<byte> RosterClasses => _rosterClasses;

    public Gg2MapLayout MapLayout => _layout;

    /// <summary>
    /// Whether a layout has been given since the last CHANGE_MAP. State updates need it;
    /// reading one without it throws.
    /// </summary>
    public bool HasMapLayout { get; private set; }

    /// <summary>The local player's roster index from JOIN_UPDATE, kept in step with PLAYER_LEAVE.</summary>
    public int LocalPlayerIndex { get; private set; } = -1;

    /// <summary>Sets the current map's layout. Call after loading the map a CHANGE_MAP named.</summary>
    public void SetMapLayout(Gg2MapLayout layout)
    {
        _layout = layout;
        HasMapLayout = true;
        _redGeneratorStanding = true;
        _blueGeneratorStanding = true;
    }

    /// <summary>
    /// Reads one message from the start of <paramref name="data"/>. Returns
    /// <see cref="Gg2ReadStatus.NeedMoreData"/> without consuming anything or changing
    /// state when the message is incomplete. Throws <see cref="InvalidDataException"/> for
    /// an unknown message type or impossible content.
    /// </summary>
    public Gg2ReadStatus TryRead(ReadOnlySpan<byte> data, out Gg2ServerMessage? message, out int consumed)
    {
        message = null;
        consumed = 0;
        if (data.IsEmpty)
        {
            return Gg2ReadStatus.NeedMoreData;
        }

        var reader = new SpanReader(data);
        var type = reader.ReadByte();
        var parsed = ReadPayload(ref reader, type);
        if (reader.Truncated)
        {
            return Gg2ReadStatus.NeedMoreData;
        }

        Apply(parsed);
        message = parsed;
        consumed = reader.Position;
        return Gg2ReadStatus.Message;
    }

    /// <summary>
    /// Reads the server's reply to HELLO: either HELLO with its payload, or a one-byte
    /// refusal (PASSWORD_REQUEST, INCOMPATIBLE_PROTOCOL, SERVER_FULL, KICK...).
    /// Same contract as <see cref="TryRead"/>.
    /// </summary>
    public Gg2ReadStatus TryReadHandshake(ReadOnlySpan<byte> data, out Gg2ServerMessage? message, out int consumed)
        => TryRead(data, out message, out consumed);

    private Gg2ServerMessage ReadPayload(ref SpanReader reader, byte type)
    {
        switch (type)
        {
            case Gg2Protocol.Hello:
                return new Gg2ServerHello(
                    reader.ReadString8(),
                    reader.ReadString8(),
                    reader.ReadString8(),
                    reader.ReadByte() != 0,
                    reader.ReadString16());
            case Gg2Protocol.ReserveSlot:
            case Gg2Protocol.ServerFull:
            case Gg2Protocol.PasswordRequest:
            case Gg2Protocol.PasswordWrong:
            case Gg2Protocol.IncompatibleProtocol:
            case Gg2Protocol.ArenaWaitForPlayers:
            case Gg2Protocol.ArenaStartRound:
            case Gg2Protocol.ArenaRestart:
            case Gg2Protocol.UnlockCp:
            case Gg2Protocol.Ping:
                return new Gg2SignalMessage(type);
            case Gg2Protocol.Kick:
                return new Gg2KickMessage(reader.ReadByte());
            case Gg2Protocol.JoinUpdate:
                return new Gg2JoinUpdateMessage(reader.ReadByte(), reader.ReadByte());
            case Gg2Protocol.ChangeMap:
                return new Gg2ChangeMapMessage(reader.ReadString8(), reader.ReadString8());
            case Gg2Protocol.PlayerJoin:
                return new Gg2PlayerJoinMessage(reader.ReadString8());
            case Gg2Protocol.PlayerLeave:
                return new Gg2PlayerLeaveMessage(reader.ReadByte());
            case Gg2Protocol.PlayerChangeTeam:
                return new Gg2PlayerChangeTeamMessage(reader.ReadByte(), reader.ReadByte());
            case Gg2Protocol.PlayerChangeClass:
                return new Gg2PlayerChangeClassMessage(reader.ReadByte(), reader.ReadByte());
            case Gg2Protocol.PlayerChangeName:
                return new Gg2PlayerChangeNameMessage(reader.ReadByte(), reader.ReadString8());
            case Gg2Protocol.PlayerSpawn:
                return new Gg2PlayerSpawnMessage(reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
            case Gg2Protocol.PlayerDeath:
                return new Gg2PlayerDeathMessage(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
            case Gg2Protocol.Balance:
            {
                var playerIndex = reader.ReadByte();
                return new Gg2BalanceMessage(playerIndex, playerIndex == Gg2Protocol.NoPlayer ? null : reader.ReadByte());
            }
            case Gg2Protocol.ChatBubble:
                return new Gg2ChatBubbleMessage(reader.ReadByte(), reader.ReadByte());
            case Gg2Protocol.BuildSentry:
                return new Gg2BuildSentryMessage(reader.ReadByte(), reader.ReadUInt16(), reader.ReadUInt16(), reader.ReadSByte());
            case Gg2Protocol.DestroySentry:
                return new Gg2DestroySentryMessage(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
            case Gg2Protocol.GrabIntel:
            case Gg2Protocol.ScoreIntel:
            case Gg2Protocol.DropIntel:
            case Gg2Protocol.UberCharged:
            case Gg2Protocol.Uber:
            case Gg2Protocol.Omnomnomnom:
            case Gg2Protocol.ToggleZoom:
                return new Gg2PlayerEventMessage(type, reader.ReadByte());
            case Gg2Protocol.ReturnIntel:
                return new Gg2ReturnIntelMessage(reader.ReadByte());
            case Gg2Protocol.GeneratorDestroy:
                return new Gg2GeneratorDestroyMessage(reader.ReadByte());
            case Gg2Protocol.ArenaEndRound:
                return ReadArenaEndRound(ref reader);
            case Gg2Protocol.MapEnd:
                return new Gg2MapEndMessage(reader.ReadString8(), reader.ReadByte(), reader.ReadByte());
            case Gg2Protocol.RewardChallengeCode:
                return new Gg2RewardChallengeMessage(reader.ReadBytes(16));
            case Gg2Protocol.RewardUpdate:
                return new Gg2RewardUpdateMessage(reader.ReadByte(), reader.ReadString16());
            case Gg2Protocol.MessageString:
                return new Gg2MessageStringMessage(reader.ReadString8());
            case Gg2Protocol.SentryPosition:
                return new Gg2SentryPositionMessage(reader.ReadByte(), reader.ReadUInt16(), reader.ReadUInt16());
            case Gg2Protocol.WeaponFire:
                return new Gg2WeaponFireMessage(reader.ReadByte(), reader.ReadUInt16(), reader.ReadUInt16(), reader.ReadSByte(), reader.ReadSByte(), reader.ReadUInt16());
            case Gg2Protocol.PluginPacket:
            {
                var length = reader.ReadUInt16();
                if (length == 0 && !reader.Truncated)
                {
                    throw new InvalidDataException("GG2 plugin packet is empty.");
                }

                var pluginId = reader.ReadByte();
                return new Gg2PluginPacketMessage(pluginId, reader.ReadBytes(Math.Max(0, length - 1)));
            }
            case Gg2Protocol.ClientSettings:
                return new Gg2ClientSettingsMessage(reader.ReadByte(), reader.ReadByte());
            case Gg2Protocol.InputState:
            case Gg2Protocol.QuickUpdate:
            case Gg2Protocol.FullUpdate:
            case Gg2Protocol.CapsUpdate:
                return ReadStateUpdate(ref reader, type);
            default:
                throw new InvalidDataException($"GG2 server sent unknown message 0x{type:X2}.");
        }
    }

    private static Gg2ArenaEndRoundMessage ReadArenaEndRound(ref SpanReader reader)
    {
        var winner = reader.ReadByte();
        var redMvpCount = reader.ReadByte();
        var blueMvpCount = reader.ReadByte();
        var redWins = reader.ReadByte();
        var blueWins = reader.ReadByte();
        var red = ReadMvps(ref reader, redMvpCount);
        var blue = ReadMvps(ref reader, blueMvpCount);
        return new Gg2ArenaEndRoundMessage(winner, redWins, blueWins, red, blue);
    }

    private static Gg2ArenaMvp[] ReadMvps(ref SpanReader reader, int count)
    {
        var result = new Gg2ArenaMvp[count];
        for (var index = 0; index < count && !reader.Truncated; index += 1)
        {
            result[index] = new Gg2ArenaMvp(reader.ReadByte(), reader.ReadByte(), reader.ReadUInt16(), reader.ReadByte());
        }

        return result;
    }

    private Gg2StateUpdateMessage ReadStateUpdate(ref SpanReader reader, byte updateType)
    {
        var full = updateType == Gg2Protocol.FullUpdate;
        var quickOrFull = full || updateType == Gg2Protocol.QuickUpdate;
        var tdmInvulnerability = full ? reader.ReadUInt16() : (ushort)0;
        var playerCount = reader.ReadByte();
        if (reader.Truncated)
        {
            return EmptyUpdate(updateType);
        }

        if (playerCount != _rosterClasses.Count)
        {
            throw new InvalidDataException(
                $"GG2 state update lists {playerCount} players; the roster has {_rosterClasses.Count}.");
        }

        if (quickOrFull && !HasMapLayout)
        {
            // Input updates never touch platforms or the HUD, so they can be read before
            // the map is known; quick and full updates cannot.
            throw new InvalidOperationException("GG2 state update read before the map layout was set.");
        }

        var players = Array.Empty<Gg2PlayerState>();
        var platforms = Array.Empty<Gg2PlatformState>();
        if (updateType != Gg2Protocol.CapsUpdate)
        {
            players = new Gg2PlayerState[playerCount];
            for (var index = 0; index < playerCount && !reader.Truncated; index += 1)
            {
                players[index] = ReadPlayerState(ref reader, updateType, _rosterClasses[index], playerCount);
            }

            if (quickOrFull)
            {
                platforms = new Gg2PlatformState[_layout.MovingPlatformCount];
                for (var index = 0; index < platforms.Length && !reader.Truncated; index += 1)
                {
                    platforms[index] = new Gg2PlatformState(reader.ReadUInt16(), reader.ReadUInt16(), reader.ReadByte());
                }
            }
        }

        Gg2FullUpdateTail? fullTail = null;
        Gg2CapsUpdateTail? capsTail = null;
        if (full)
        {
            var redIntel = ReadIntelSet(ref reader);
            var blueIntel = ReadIntelSet(ref reader);
            var capLimit = reader.ReadByte();
            var redCaps = reader.ReadByte();
            var blueCaps = reader.ReadByte();
            var respawnSeconds = reader.ReadByte();
            var hud = ReadHudState(ref reader, full: true);
            var classLimits = reader.ReadBytes(Gg2Protocol.ClassCount);
            fullTail = new Gg2FullUpdateTail(redIntel, blueIntel, capLimit, redCaps, blueCaps, respawnSeconds, hud, classLimits);
        }
        else if (updateType == Gg2Protocol.CapsUpdate)
        {
            if (!HasMapLayout)
            {
                throw new InvalidOperationException("GG2 caps update read before the map layout was set.");
            }

            var redCaps = reader.ReadByte();
            var blueCaps = reader.ReadByte();
            var respawnSeconds = reader.ReadByte();
            capsTail = new Gg2CapsUpdateTail(redCaps, blueCaps, respawnSeconds, ReadHudState(ref reader, full: false));
        }

        return new Gg2StateUpdateMessage(updateType, tdmInvulnerability, players, platforms, fullTail, capsTail);
    }

    private static Gg2StateUpdateMessage EmptyUpdate(byte updateType)
        => new(updateType, 0, Array.Empty<Gg2PlayerState>(), Array.Empty<Gg2PlatformState>(), null, null);

    private static Gg2PlayerState ReadPlayerState(ref SpanReader reader, byte updateType, byte classId, int playerCount)
    {
        var full = updateType == Gg2Protocol.FullUpdate;
        var quickOrFull = full || updateType == Gg2Protocol.QuickUpdate;
        Gg2PlayerStats? stats = null;
        if (full)
        {
            stats = new Gg2PlayerStats(
                Kills: reader.ReadByte(),
                Deaths: reader.ReadByte(),
                Caps: reader.ReadByte(),
                Assists: reader.ReadByte(),
                Destruction: reader.ReadByte(),
                Stabs: reader.ReadByte(),
                Healing: reader.ReadUInt16(),
                Defenses: reader.ReadByte(),
                Invulns: reader.ReadByte(),
                Bonus: reader.ReadByte(),
                Points: reader.ReadByte(),
                QueueJump: reader.ReadByte(),
                Rewards: reader.ReadString16(),
                DominationKills: reader.ReadBytes(Math.Max(0, playerCount - 1)));
        }

        var subobjects = reader.ReadByte();
        Gg2CharacterState? character = null;
        if ((subobjects & 0x01) != 0)
        {
            var keyState = reader.ReadByte();
            var aimDirection = reader.ReadUInt16();
            var aimDistance = reader.ReadByte();
            Gg2CharacterQuickState? quick = null;
            if (quickOrFull)
            {
                quick = new Gg2CharacterQuickState(
                    reader.ReadUInt16(),
                    reader.ReadUInt16(),
                    reader.ReadSByte(),
                    reader.ReadSByte(),
                    reader.ReadByte(),
                    reader.ReadByte(),
                    reader.ReadByte());
            }

            Gg2CharacterFullState? fullState = null;
            if (full)
            {
                var animationOffset = reader.ReadByte();
                var classValue = reader.ReadByte();
                var intelAlarm = reader.ReadInt16();
                var hasIntel = reader.ReadByte();
                var intelRecharge = reader.ReadInt16();
                fullState = new Gg2CharacterFullState(animationOffset, classValue, intelAlarm, hasIntel, intelRecharge, ReadWeaponState(ref reader, classId));
            }

            character = new Gg2CharacterState(keyState, aimDirection, aimDistance, quick, fullState);
        }

        Gg2SentryState? sentry = null;
        if ((subobjects & 0x02) != 0 && quickOrFull)
        {
            sbyte? startDirection = null;
            ushort? x = null;
            ushort? y = null;
            if (full)
            {
                startDirection = reader.ReadSByte();
                x = reader.ReadUInt16();
                y = reader.ReadUInt16();
            }

            var packed = reader.ReadByte();
            byte? verticalSpeed = full && (packed & 0x80) == 0 ? reader.ReadByte() : null;
            sentry = new Gg2SentryState(startDirection, x, y, packed, verticalSpeed);
        }
        else if ((subobjects & 0x02) != 0)
        {
            // Input updates send no sentry bytes; keep the flag so callers know it exists.
            sentry = new Gg2SentryState(null, null, null, 0, null);
        }

        return new Gg2PlayerState(stats, character, sentry);
    }

    private static Gg2WeaponState ReadWeaponState(ref SpanReader reader, byte classId)
    {
        if (classId == Gg2Protocol.ClassMedic)
        {
            return new Gg2WeaponState(0, 0, reader.ReadByte(), null);
        }

        var ready = reader.ReadByte();
        var cooldown = reader.ReadByte();
        if (classId != Gg2Protocol.ClassDemoman)
        {
            return new Gg2WeaponState(ready, cooldown, null, null);
        }

        var lobbed = reader.ReadByte();
        var mines = new Gg2MineState[lobbed];
        for (var index = 0; index < lobbed && !reader.Truncated; index += 1)
        {
            mines[index] = new Gg2MineState(reader.ReadUInt16(), reader.ReadUInt16(), reader.ReadSByte(), reader.ReadSByte(), reader.ReadByte());
        }

        return new Gg2WeaponState(ready, cooldown, null, mines);
    }

    private static Gg2IntelState[] ReadIntelSet(ref SpanReader reader)
    {
        var count = reader.ReadUInt16();
        if (reader.Truncated)
        {
            return Array.Empty<Gg2IntelState>();
        }

        if (count > 64)
        {
            throw new InvalidDataException($"GG2 full update lists {count} intelligence instances.");
        }

        var result = new Gg2IntelState[count];
        for (var index = 0; index < count && !reader.Truncated; index += 1)
        {
            result[index] = new Gg2IntelState(reader.ReadUInt16(), reader.ReadUInt16(), reader.ReadInt16());
        }

        return result;
    }

    private Gg2HudState? ReadHudState(ref SpanReader reader, bool full)
    {
        switch (_layout.HudKind)
        {
            case Gg2HudKind.CaptureTheFlag:
                return new Gg2CtfHudState(reader.ReadByte(), reader.ReadUInt32());
            case Gg2HudKind.ControlPoint:
            {
                var minutes = reader.ReadByte();
                var timer = reader.ReadUInt32();
                var setup = reader.ReadUInt16();
                return new Gg2ControlPointHudState(minutes, timer, setup, ReadPoints(ref reader, _layout.ControlPointCount));
            }
            case Gg2HudKind.KingOfTheHill:
            case Gg2HudKind.DoubleKingOfTheHill:
            {
                var unlock = reader.ReadUInt16();
                var red = reader.ReadUInt16();
                var blue = reader.ReadUInt16();
                var pointCount = _layout.HudKind == Gg2HudKind.KingOfTheHill ? 1 : 2;
                return new Gg2KothHudState(unlock, red, blue, ReadPoints(ref reader, pointCount));
            }
            case Gg2HudKind.Arena:
            {
                Gg2ArenaRoundState? round = null;
                if (full)
                {
                    round = new Gg2ArenaRoundState(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadSByte(), reader.ReadUInt16());
                }

                var minutes = reader.ReadByte();
                var timer = reader.ReadUInt32();
                var unlock = reader.ReadUInt16();
                var roundStart = reader.ReadByte() * 2;
                var point = ReadPoint(ref reader);
                return new Gg2ArenaHudState(round, minutes, timer, unlock, roundStart, point);
            }
            case Gg2HudKind.TeamDeathmatch:
                return new Gg2TeamDeathmatchHudState(reader.ReadByte(), reader.ReadUInt32(), reader.ReadUInt16());
            case Gg2HudKind.Generator:
            {
                var minutes = reader.ReadByte();
                var timer = reader.ReadUInt32();
                Gg2GeneratorState? blue = _blueGeneratorStanding ? new Gg2GeneratorState(reader.ReadUInt16(), reader.ReadUInt16()) : null;
                Gg2GeneratorState? red = _redGeneratorStanding ? new Gg2GeneratorState(reader.ReadUInt16(), reader.ReadUInt16()) : null;
                return new Gg2GeneratorHudState(minutes, timer, blue, red);
            }
            case Gg2HudKind.Invasion:
                return new Gg2InvasionHudState(reader.ReadByte(), reader.ReadUInt32(), reader.ReadUInt16());
            default:
                return null;
        }
    }

    private static Gg2ControlPointState[] ReadPoints(ref SpanReader reader, int count)
    {
        var points = new Gg2ControlPointState[Math.Max(0, count)];
        for (var index = 0; index < points.Length && !reader.Truncated; index += 1)
        {
            points[index] = ReadPoint(ref reader);
        }

        return points;
    }

    private static Gg2ControlPointState ReadPoint(ref SpanReader reader)
        => new(reader.ReadSByte(), reader.ReadSByte(), reader.ReadUInt16());

    /// <summary>Roster and layout bookkeeping, applied only once a message is complete.</summary>
    private void Apply(Gg2ServerMessage message)
    {
        switch (message)
        {
            case Gg2PlayerJoinMessage:
                _rosterClasses.Add(Gg2Protocol.ClassScout);
                break;
            case Gg2PlayerLeaveMessage leave:
                if (leave.PlayerIndex >= _rosterClasses.Count)
                {
                    throw new InvalidDataException($"GG2 PLAYER_LEAVE names player {leave.PlayerIndex} of {_rosterClasses.Count}.");
                }

                _rosterClasses.RemoveAt(leave.PlayerIndex);
                if (leave.PlayerIndex < LocalPlayerIndex)
                {
                    LocalPlayerIndex -= 1;
                }
                else if (leave.PlayerIndex == LocalPlayerIndex)
                {
                    LocalPlayerIndex = -1;
                }

                break;
            case Gg2PlayerChangeClassMessage change:
                SetRosterClass(change.PlayerIndex, change.ClassId);
                break;
            case Gg2BalanceMessage { NewClass: { } newClass } balance:
                SetRosterClass(balance.PlayerIndex, newClass);
                break;
            case Gg2JoinUpdateMessage join:
                LocalPlayerIndex = join.LocalPlayerIndex;
                break;
            case Gg2ChangeMapMessage:
                // The next state update depends on the new map; the caller supplies it.
                HasMapLayout = false;
                break;
            case Gg2GeneratorDestroyMessage destroy:
                if (destroy.Team == Gg2Protocol.TeamRed)
                {
                    _redGeneratorStanding = false;
                }
                else if (destroy.Team == Gg2Protocol.TeamBlue)
                {
                    _blueGeneratorStanding = false;
                }

                break;
        }
    }

    private void SetRosterClass(byte playerIndex, byte classId)
    {
        if (playerIndex >= _rosterClasses.Count)
        {
            throw new InvalidDataException($"GG2 message names player {playerIndex} of {_rosterClasses.Count}.");
        }

        if (classId >= Gg2Protocol.ClassCount)
        {
            throw new InvalidDataException($"GG2 class id {classId} is out of range.");
        }

        _rosterClasses[playerIndex] = classId;
    }

    /// <summary>Little-endian reader over a span that flags, rather than throws on, running out.</summary>
    private ref struct SpanReader
    {
        private readonly ReadOnlySpan<byte> _data;

        public SpanReader(ReadOnlySpan<byte> data)
        {
            _data = data;
            Position = 0;
            Truncated = false;
        }

        public int Position { get; private set; }

        public bool Truncated { get; private set; }

        private bool Take(int count, out ReadOnlySpan<byte> bytes)
        {
            if (Truncated || Position + count > _data.Length)
            {
                Truncated = true;
                bytes = default;
                return false;
            }

            bytes = _data.Slice(Position, count);
            Position += count;
            return true;
        }

        public byte ReadByte() => Take(1, out var bytes) ? bytes[0] : (byte)0;

        public sbyte ReadSByte() => unchecked((sbyte)ReadByte());

        public ushort ReadUInt16() => Take(2, out var bytes) ? BinaryPrimitives.ReadUInt16LittleEndian(bytes) : (ushort)0;

        public short ReadInt16() => Take(2, out var bytes) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : (short)0;

        public uint ReadUInt32() => Take(4, out var bytes) ? BinaryPrimitives.ReadUInt32LittleEndian(bytes) : 0u;

        public byte[] ReadBytes(int count) => Take(count, out var bytes) ? bytes.ToArray() : Array.Empty<byte>();

        public string ReadString8() => ReadString(ReadByte());

        public string ReadString16() => ReadString(ReadUInt16());

        private string ReadString(int length) => Take(length, out var bytes) ? Latin1.GetString(bytes) : string.Empty;
    }
}
