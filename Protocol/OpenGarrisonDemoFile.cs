#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OpenGarrison.Protocol;

public sealed record OpenGarrisonDemoMessage(int DueMilliseconds, byte[] Payload);

public sealed record OpenGarrisonDemoFile(
    string RemoteDescription,
    string CompletionReason,
    IReadOnlyList<OpenGarrisonDemoMessage> Messages)
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("OGDM");
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    public const byte CurrentVersion = 1;

    public static OpenGarrisonDemoFile Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var stream = File.OpenRead(path);
        return Read(stream);
    }

    public static OpenGarrisonDemoFile Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var reader = new BinaryReader(stream, Utf8, leaveOpen: true);
        var magic = reader.ReadBytes(Magic.Length);
        if (magic.Length != Magic.Length || !magic.AsSpan().SequenceEqual(Magic))
        {
            throw new InvalidDataException("Demo file magic did not match the expected OGDM header.");
        }

        var version = reader.ReadByte();
        if (version != CurrentVersion)
        {
            throw new InvalidDataException($"Demo file version {version} is not supported.");
        }

        var remoteDescription = ReadString(reader, maxBytes: 256);
        var completionReason = ReadString(reader, maxBytes: 256);
        var messageCount = reader.ReadInt32();
        if (messageCount < 0)
        {
            throw new InvalidDataException("Demo file contained a negative message count.");
        }

        var messages = new List<OpenGarrisonDemoMessage>(messageCount);
        for (var index = 0; index < messageCount; index += 1)
        {
            var dueMilliseconds = reader.ReadInt32();
            var payloadLength = reader.ReadInt32();
            if (dueMilliseconds < 0 || payloadLength < 0)
            {
                throw new InvalidDataException("Demo file contained a negative message schedule or payload length.");
            }

            var payload = reader.ReadBytes(payloadLength);
            if (payload.Length != payloadLength)
            {
                throw new InvalidDataException("Demo file ended before a scheduled message payload was fully read.");
            }

            messages.Add(new OpenGarrisonDemoMessage(dueMilliseconds, payload));
        }

        return new OpenGarrisonDemoFile(remoteDescription, completionReason, messages);
    }

    public static void Write(string path, OpenGarrisonDemoFile demo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(demo);
        using var stream = File.Create(path);
        Write(stream, demo);
    }

    public static void Write(Stream stream, OpenGarrisonDemoFile demo)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(demo);
        using var writer = new BinaryWriter(stream, Utf8, leaveOpen: true);
        writer.Write(Magic);
        writer.Write(CurrentVersion);
        WriteString(writer, demo.RemoteDescription ?? string.Empty, maxBytes: 256, "remote description");
        WriteString(writer, demo.CompletionReason ?? string.Empty, maxBytes: 256, "completion reason");
        writer.Write(demo.Messages.Count);

        for (var index = 0; index < demo.Messages.Count; index += 1)
        {
            var message = demo.Messages[index];
            if (message.DueMilliseconds < 0)
            {
                throw new InvalidDataException("Demo messages cannot be scheduled before time zero.");
            }

            writer.Write(message.DueMilliseconds);
            writer.Write(message.Payload.Length);
            writer.Write(message.Payload);
        }
    }

    private static void WriteString(BinaryWriter writer, string value, int maxBytes, string fieldName)
    {
        var bytes = Utf8.GetBytes(value);
        if (bytes.Length > ushort.MaxValue || bytes.Length > maxBytes)
        {
            throw new InvalidDataException($"Demo {fieldName} exceeded the configured byte limit.");
        }

        writer.Write((ushort)bytes.Length);
        writer.Write(bytes);
    }

    private static string ReadString(BinaryReader reader, int maxBytes)
    {
        var byteLength = reader.ReadUInt16();
        if (byteLength > maxBytes)
        {
            throw new InvalidDataException("Demo string exceeded the configured byte limit.");
        }

        var bytes = reader.ReadBytes(byteLength);
        if (bytes.Length != byteLength)
        {
            throw new InvalidDataException("Demo file ended while reading a string payload.");
        }

        return Utf8.GetString(bytes);
    }
}
