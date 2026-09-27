#nullable enable

using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace OpenGarrison.Client;

/// <summary>Native wire support for the chat plugin required by Vindicator's GG2 2.9.2 server.</summary>
internal static class LegacyGg2ChatWire
{
    internal const string SupportedPlugin = "chat@bad8081d64f0b808a2bb3a6d88978fb5";
    internal const byte PluginPacket = 55;

    internal static bool TryGetPluginId(string pluginList, out byte pluginId)
    {
        pluginId = 0;
        var plugins = (pluginList ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < plugins.Length && index <= byte.MaxValue; index++)
        {
            if (!string.Equals(plugins[index], SupportedPlugin, StringComparison.Ordinal))
            {
                continue;
            }

            pluginId = checked((byte)index);
            return true;
        }

        return false;
    }

    internal static byte[] CreateHello(byte pluginId) => Wrap(pluginId, [0]);

    internal static byte[] CreateChat(byte pluginId, string text, bool teamOnly)
    {
        var bytes = Encoding.Latin1.GetBytes(text ?? string.Empty);
        if (bytes.Length == 0 || bytes.Length > byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(text), "GG2 chat messages must contain 1 to 255 bytes.");
        }

        var body = new byte[bytes.Length + 2];
        body[0] = teamOnly ? (byte)2 : (byte)1;
        body[1] = checked((byte)bytes.Length);
        bytes.CopyTo(body, 2);
        return Wrap(pluginId, body);
    }

    internal static LegacyGg2ChatMessage ReadChatMessage(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 2)
        {
            throw new InvalidDataException("GG2 chat plugin message is truncated.");
        }

        var sender = payload[0];
        if (sender is >= 200 and <= 204)
        {
            throw new NotSupportedException("GG2 chat plugin vote packets use a separate payload format.");
        }

        var team = (byte)0;
        var offset = 1;
        if (sender < 200)
        {
            if (payload.Length < 3)
            {
                throw new InvalidDataException("GG2 player chat plugin message is truncated.");
            }

            team = payload[offset++];
        }

        var length = payload[offset++];
        if (payload.Length - offset != length)
        {
            throw new InvalidDataException("GG2 chat plugin message length is invalid.");
        }

        return new LegacyGg2ChatMessage(sender, team, Encoding.Latin1.GetString(payload[offset..]));
    }

    private static byte[] Wrap(byte pluginId, byte[] body)
    {
        var result = new byte[body.Length + 4];
        result[0] = PluginPacket;
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(1), checked((ushort)(body.Length + 1)));
        result[3] = pluginId;
        body.CopyTo(result, 4);
        return result;
    }
}

internal readonly record struct LegacyGg2ChatMessage(byte Sender, byte Team, string Text);
