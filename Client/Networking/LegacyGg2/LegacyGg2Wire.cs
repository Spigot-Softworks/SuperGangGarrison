#nullable enable

using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using OpenGarrison.Core;
using OpenGarrison.Protocol;

namespace OpenGarrison.Client;

/// <summary>
/// Client-to-server wire format for the vanilla GG2 2.9.2 protocol. These
/// values come from GG2's Constants.xml and Message Builders scripts.
/// </summary>
internal static class LegacyGg2Wire
{
    internal const byte Hello = 0;
    internal const byte PlayerJoin = 1;
    internal const byte PlayerLeave = 2;
    internal const byte PlayerChangeTeam = 3;
    internal const byte PlayerChangeClass = 4;
    internal const byte InputState = 6;
    internal const byte ChangeMap = 7;
    internal const byte BuildSentry = 16;
    internal const byte DestroySentry = 17;
    internal const byte DropIntel = 21;
    internal const byte HeavyEat = 24;
    internal const byte ToggleZoom = 41;
    internal const byte ServerFull = 11;
    internal const byte PasswordRequest = 25;
    internal const byte PasswordWrong = 27;
    internal const byte IncompatibleProtocol = 43;
    internal const byte JoinUpdate = 44;
    internal const byte DownloadMap = 45;
    internal const byte ReserveSlot = 60;

    internal const int MaxPlayerNameBytes = 20;
    internal const string ProtocolUuid = "b31c2209-4256-9a19-d0ef-c71c5373bd75";

    private static readonly Encoding Latin1 = Encoding.Latin1;

    internal static byte[] CreateHello()
    {
        // GG2's parseUuid writes the 16 hex pairs in text order. Guid.ToByteArray
        // uses a different byte order for the first three fields.
        var result = new byte[17];
        result[0] = Hello;
        Convert.FromHexString(ProtocolUuid.Replace("-", string.Empty, StringComparison.Ordinal)).CopyTo(result, 1);
        return result;
    }

    internal static byte[] CreatePassword(string password)
    {
        var value = Latin1.GetBytes(password ?? string.Empty);
        if (value.Length > byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(password), "GG2 passwords cannot exceed 255 bytes.");
        }

        var result = new byte[value.Length + 1];
        result[0] = checked((byte)value.Length);
        value.CopyTo(result, 1);
        return result;
    }

    internal static byte[] CreateReserveSlot(string playerName)
    {
        var name = Latin1.GetBytes(playerName ?? string.Empty);
        var nameLength = Math.Min(name.Length, MaxPlayerNameBytes);
        var result = new byte[nameLength + 2];
        result[0] = ReserveSlot;
        result[1] = checked((byte)nameLength);
        name.AsSpan(0, nameLength).CopyTo(result.AsSpan(2));
        return result;
    }

    internal static byte[] CreateTeamSelection(PlayerTeam team)
        => [PlayerChangeTeam, team switch
        {
            PlayerTeam.Red => 0,
            PlayerTeam.Blue => 1,
            _ => 2,
        }];

    internal static byte[] CreateClassSelection(PlayerClass playerClass)
        => [PlayerChangeClass, playerClass switch
        {
            PlayerClass.Scout => 0,
            PlayerClass.Soldier => 1,
            PlayerClass.Sniper => 2,
            PlayerClass.Demoman => 3,
            PlayerClass.Medic => 4,
            PlayerClass.Engineer => 5,
            PlayerClass.Heavy => 6,
            PlayerClass.Spy => 7,
            PlayerClass.Pyro => 8,
            PlayerClass.Quote => 9,
            _ => throw new ArgumentOutOfRangeException(nameof(playerClass)),
        }];

    internal static byte[] CreateInputState(InputStateMessage input)
    {
        var buttons = input.Buttons;
        byte keyState = 0;
        if ((buttons & InputButtons.Taunt) != 0) keyState |= 0x01;
        if ((buttons & InputButtons.Down) != 0) keyState |= 0x02;
        if ((buttons & InputButtons.FireSecondary) != 0) keyState |= 0x08;
        if ((buttons & InputButtons.FirePrimary) != 0) keyState |= 0x10;
        if ((buttons & InputButtons.Right) != 0) keyState |= 0x20;
        if ((buttons & InputButtons.Left) != 0) keyState |= 0x40;
        if ((buttons & InputButtons.Up) != 0) keyState |= 0x80;

        // GameMaker's point_direction uses a positive angle above the x axis.
        var direction = Math.Atan2(-input.AimRelY, input.AimRelX);
        if (direction < 0d) direction += Math.PI * 2d;
        var aimDirection = (ushort)Math.Clamp((int)(direction * 65536d / (Math.PI * 2d)), 0, ushort.MaxValue);
        var aimDistance = (byte)Math.Clamp((int)(Math.Sqrt(
            (double)input.AimRelX * input.AimRelX + (double)input.AimRelY * input.AimRelY) / 2d), 0, byte.MaxValue);

        var result = new byte[5];
        result[0] = InputState;
        result[1] = keyState;
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(2), aimDirection);
        result[4] = aimDistance;
        return result;
    }

    internal static PlayerInputSnapshot KeepStockInput(PlayerInputSnapshot input)
        => input with
        {
            BuildSentry = false,
            DestroySentry = false,
            DebugKill = false,
            UseAbility = false,
            InteractWeapon = false,
            IsUsingBinoculars = false,
            SwapWeapon = false,
            ReadyUp = false,
            BuildDispenser = false,
            DestroyDispenser = false,
            ToggleSecondaryWeapon = false,
        };

    internal static byte? GetStockSpecialCommand(PlayerClass playerClass, bool hasSentry)
        => playerClass switch
        {
            PlayerClass.Engineer => hasSentry ? DestroySentry : BuildSentry,
            PlayerClass.Heavy => HeavyEat,
            PlayerClass.Sniper => ToggleZoom,
            _ => null,
        };

    internal static LegacyGg2ServerHello ReadServerHello(BinaryReader reader, byte? firstByte = null)
    {
        ArgumentNullException.ThrowIfNull(reader);
        if ((firstByte ?? reader.ReadByte()) != Hello)
        {
            throw new InvalidDataException("GG2 server did not accept the protocol hello.");
        }

        var serverName = ReadByteLengthString(reader);
        var mapName = ReadByteLengthString(reader);
        var mapMd5 = ReadByteLengthString(reader);
        var pluginsRequired = reader.ReadByte() != 0;
        var pluginListLength = reader.ReadUInt16();
        var pluginList = ReadString(reader, pluginListLength);
        return new LegacyGg2ServerHello(serverName, mapName, mapMd5, pluginsRequired, pluginList);
    }

    private static string ReadByteLengthString(BinaryReader reader) => ReadString(reader, reader.ReadByte());

    private static string ReadString(BinaryReader reader, int length)
    {
        var value = reader.ReadBytes(length);
        if (value.Length != length)
        {
            throw new EndOfStreamException("GG2 server hello was truncated.");
        }

        return Latin1.GetString(value);
    }
}

internal readonly record struct LegacyGg2ServerHello(
    string ServerName,
    string MapName,
    string MapMd5,
    bool PluginsRequired,
    string PluginList);
