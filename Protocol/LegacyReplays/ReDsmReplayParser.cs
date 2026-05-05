using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OpenGarrison.Protocol;

public static class ReDsmReplayParser
{
    public const byte ReplayHeaderMarker = 254;
    public const byte ReplayEndMarker = 255;
    public const byte HelloMarker = 0;

    private static readonly Encoding Latin1 = Encoding.Latin1;

    public static ReDsmReplayFile Parse(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        using var stream = File.OpenRead(path);
        return Parse(stream);
    }

    public static ReDsmReplayFile Parse(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var reader = new BinaryReader(stream, Latin1, leaveOpen: true);
        var headerRecord = ReadRecord(reader);
        var header = ParseHeader(headerRecord);
        var frames = new List<ReDsmReplayTickFrame>();
        var hasEndMarker = false;
        var frameIndex = 0;

        while (stream.Position < stream.Length)
        {
            var record = ReadRecord(reader);
            if (record.Length == 1 && record[0] == ReplayEndMarker)
            {
                hasEndMarker = true;
                break;
            }

            frames.Add(new ReDsmReplayTickFrame(frameIndex, record));
            frameIndex += 1;
        }

        return new ReDsmReplayFile(header, frames, hasEndMarker);
    }

    private static ReDsmReplayHeader ParseHeader(byte[] headerRecord)
    {
        using var stream = new MemoryStream(headerRecord, writable: false);
        using var reader = new BinaryReader(stream, Latin1, leaveOpen: true);

        var headerMarker = reader.ReadByte();
        if (headerMarker != ReplayHeaderMarker)
        {
            throw new InvalidDataException($"Unexpected replay header marker 0x{headerMarker:X2}.");
        }

        var replayVersion = reader.ReadByte();
        var gameVersion = reader.ReadUInt16();
        var frameRateKind = reader.ReadByte();

        var helloMarker = reader.ReadByte();
        if (helloMarker != HelloMarker)
        {
            throw new InvalidDataException($"Unexpected replay hello marker 0x{helloMarker:X2}.");
        }

        var serverName = ReadByteLengthPrefixedString(reader);
        var mapName = ReadByteLengthPrefixedString(reader);
        var mapContentHash = ReadByteLengthPrefixedString(reader);
        var pluginsRequired = reader.ReadByte() != 0;
        var pluginList = ReadUInt16LengthPrefixedString(reader);
        var joinStatePayload = reader.ReadBytes(checked((int)(stream.Length - stream.Position)));

        return new ReDsmReplayHeader(
            replayVersion,
            gameVersion,
            frameRateKind,
            serverName,
            mapName,
            mapContentHash,
            pluginsRequired,
            pluginList,
            joinStatePayload);
    }

    private static byte[] ReadRecord(BinaryReader reader)
    {
        var remaining = reader.BaseStream.Length - reader.BaseStream.Position;
        if (remaining < sizeof(ushort))
        {
            throw new InvalidDataException("Replay record length prefix is truncated.");
        }

        var length = reader.ReadUInt16();
        if (length == 0)
        {
            throw new InvalidDataException("Replay record length must be non-zero.");
        }

        var payload = reader.ReadBytes(length);
        if (payload.Length != length)
        {
            throw new InvalidDataException("Replay record payload is truncated.");
        }

        return payload;
    }

    private static string ReadByteLengthPrefixedString(BinaryReader reader)
    {
        var length = reader.ReadByte();
        return ReadExactString(reader, length);
    }

    private static string ReadUInt16LengthPrefixedString(BinaryReader reader)
    {
        var length = reader.ReadUInt16();
        return ReadExactString(reader, length);
    }

    private static string ReadExactString(BinaryReader reader, int byteLength)
    {
        var bytes = reader.ReadBytes(byteLength);
        if (bytes.Length != byteLength)
        {
            throw new InvalidDataException("Replay string payload is truncated.");
        }

        return Latin1.GetString(bytes);
    }
}
