#nullable enable

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OpenGarrison.Client;

/// <summary>Reads the public GG2 lobby's native TCP server list.</summary>
internal static class LegacyGg2LobbyClient
{
    internal const string Host = "ganggarrison.com";
    internal const int Port = 29944;
    private const int MaxServers = 256;
    private const int MaxServerBlockBytes = 100_000;
    private static readonly HttpClient BrowserHttpClient = new();
    private static readonly byte[] ListRequest = Convert.FromHexString(
        "297d0df4430cbf61640a640897eaef57" + // GG2 lobby list request
        "1ccf16b1436d856f504dcc1af306aaa7"); // GG2 game ID
    private static readonly byte[] ProtocolId = Convert.FromHexString(
        LegacyGg2Wire.ProtocolUuid.Replace("-", string.Empty, StringComparison.Ordinal));

    internal static async Task<IReadOnlyList<LegacyGg2LobbyServer>> FetchAsync(
        string host = Host, int port = Port, CancellationToken cancellationToken = default)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(host, port, cancellationToken);
        using var stream = client.GetStream();
        await stream.WriteAsync(ListRequest, cancellationToken);

        var header = new byte[4];
        await stream.ReadExactlyAsync(header, cancellationToken);
        var count = BinaryPrimitives.ReadUInt32BigEndian(header);
        if (count > MaxServers)
        {
            throw new InvalidDataException($"GG2 lobby advertised too many servers ({count}).");
        }

        var servers = new List<LegacyGg2LobbyServer>((int)count);
        for (var index = 0; index < count; index += 1)
        {
            await stream.ReadExactlyAsync(header, cancellationToken);
            var length = BinaryPrimitives.ReadUInt32BigEndian(header);
            if (length is 0 or > MaxServerBlockBytes)
            {
                throw new InvalidDataException($"GG2 lobby sent an invalid server block length ({length}).");
            }

            var block = new byte[length];
            await stream.ReadExactlyAsync(block, cancellationToken);
            if (TryParseServerBlock(block, out var server))
            {
                servers.Add(server);
            }
        }

        return servers;
    }

    internal static async Task<IReadOnlyList<LegacyGg2LobbyServer>> FetchBrowserAsync(
        Uri serviceOrigin, CancellationToken cancellationToken = default)
    {
        var endpoint = new Uri(serviceOrigin, "/api/gg2/servers");
        using var response = await BrowserHttpClient.GetAsync(endpoint, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
        return ParseBrowserResponse(document.RootElement);
    }

    internal static IReadOnlyList<LegacyGg2LobbyServer> ParseBrowserResponse(JsonElement root)
    {
        if (!root.TryGetProperty("servers", out var entries) || entries.ValueKind != JsonValueKind.Array
            || entries.GetArrayLength() > MaxServers)
        {
            throw new InvalidDataException("GG2 browser received an invalid server list.");
        }

        var servers = new List<LegacyGg2LobbyServer>(entries.GetArrayLength());
        foreach (var entry in entries.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object) continue;
            try
            {
                servers.Add(new LegacyGg2LobbyServer(
                    entry.GetProperty("host").GetString() ?? string.Empty,
                    entry.GetProperty("port").GetInt32(),
                    entry.GetProperty("name").GetString() ?? "Unknown server",
                    entry.GetProperty("map").GetString() ?? "-",
                    entry.GetProperty("game").GetString() ?? "gg2",
                    entry.GetProperty("version").GetString() ?? string.Empty,
                    entry.GetProperty("players").GetInt32(),
                    entry.GetProperty("bots").GetInt32(),
                    entry.GetProperty("slots").GetInt32(),
                    entry.GetProperty("isPrivate").GetBoolean(),
                    entry.GetProperty("isCompatible").GetBoolean()));
            }
            catch (Exception exception) when (exception is KeyNotFoundException or InvalidOperationException or FormatException)
            {
                // Ignore one malformed advertisement without hiding the rest.
            }
        }
        return servers;
    }

    internal static bool TryParseServerBlock(ReadOnlySpan<byte> block, out LegacyGg2LobbyServer server)
    {
        server = default;
        try
        {
            var reader = new BlockReader(block);
            var transport = reader.ReadByte();
            var port = reader.ReadUInt16();
            var address = $"{reader.ReadByte()}.{reader.ReadByte()}.{reader.ReadByte()}.{reader.ReadByte()}";
            reader.Skip(18); // lobby server ID
            var slots = reader.ReadUInt16();
            var players = reader.ReadUInt16();
            var bots = reader.ReadUInt16();
            var flags = reader.ReadUInt16();
            var infoCount = reader.ReadUInt16();
            string name = "Unknown server", map = "-", game = "gg2", version = string.Empty;
            var compatibleProtocol = false;
            for (var index = 0; index < infoCount; index += 1)
            {
                var key = reader.ReadString(reader.ReadByte());
                var value = reader.ReadBytes(reader.ReadUInt16());
                if (key == "protocol_id") compatibleProtocol = value.SequenceEqual(ProtocolId);
                else if (key == "name") name = Decode(value);
                else if (key == "map") map = Decode(value);
                else if (key == "game_short") game = Decode(value);
                else if (key == "game_ver") version = Decode(value);
            }

            server = new LegacyGg2LobbyServer(
                address, port, string.IsNullOrWhiteSpace(name) ? "Unknown server" : name.Trim(),
                string.IsNullOrWhiteSpace(map) ? "-" : map.Trim(), game.Trim(), version.Trim(),
                players, bots, slots, (flags & 1) != 0,
                transport == 0 && port != 0 && compatibleProtocol);
            return true;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    private static string Decode(ReadOnlySpan<byte> bytes) => Encoding.UTF8.GetString(bytes);

    private ref struct BlockReader
    {
        private readonly ReadOnlySpan<byte> _block;
        private int _offset;

        public BlockReader(ReadOnlySpan<byte> block)
        {
            _block = block;
            _offset = 0;
        }

        public byte ReadByte() => ReadBytes(1)[0];

        public ushort ReadUInt16() => BinaryPrimitives.ReadUInt16BigEndian(ReadBytes(2));

        public string ReadString(int length) => Decode(ReadBytes(length));

        public void Skip(int length) => ReadBytes(length);

        public ReadOnlySpan<byte> ReadBytes(int length)
        {
            if (length < 0 || length > _block.Length - _offset)
            {
                throw new InvalidDataException("GG2 lobby server block is truncated.");
            }

            var value = _block.Slice(_offset, length);
            _offset += length;
            return value;
        }
    }
}

internal readonly record struct LegacyGg2LobbyServer(
    string Host,
    int Port,
    string Name,
    string Map,
    string Game,
    string Version,
    int Players,
    int Bots,
    int Slots,
    bool IsPrivate,
    bool IsCompatible);
