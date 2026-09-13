using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using OpenGarrison.Protocol;

namespace OpenGarrison.Server;

/// <summary>Admission metadata supplied only by the private gateway, not gameplay packets.</summary>
internal static class ManagedRoomRuntime
{
    internal sealed record Participant(Guid ClientId, byte Slot);
    internal static readonly string RoomId = Environment.GetEnvironmentVariable("OPENGARRISON_MANAGED_ROOM_ID") ?? "";
    private static readonly string Token = Environment.GetEnvironmentVariable("OPENGARRISON_MANAGED_ROOM_TOKEN") ?? "";
    internal static readonly string ContentId = Environment.GetEnvironmentVariable("OPENGARRISON_ROOM_CONTENT_ID") ?? "dev";
    internal static bool Enabled => RoomId.Length > 0;
    internal static volatile bool Ready;
    internal static Func<string>? GetPhase;
    internal static readonly ConcurrentDictionary<ulong, Participant> Participants = new();

    internal static bool Authenticate(string? token) => Enabled && Token.Length >= 32 && token is not null
        && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(Token), Encoding.UTF8.GetBytes(token));

    internal static object Describe() => new
    {
        roomId = RoomId, kind = "LastToDie", status = Ready ? "ready" : "starting",
        protocolVersion = ProtocolVersion.Current, contentId = ContentId, phase = GetPhase?.Invoke() ?? "Lobby",
    };
}
