#nullable enable

namespace OpenGarrison.ClientShared;

public static class RelayRoomCode
{
    public const int Length = 4;

    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static bool TryNormalize(string? value, out string roomCode)
    {
        roomCode = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        Span<char> compact = stackalloc char[Length];
        var count = 0;
        foreach (var character in value)
        {
            if (!char.IsAsciiLetterOrDigit(character))
            {
                continue;
            }

            if (count >= Length)
            {
                return false;
            }

            var normalized = char.ToUpperInvariant(character);
            if (!Alphabet.Contains(normalized))
            {
                return false;
            }

            compact[count] = normalized;
            count += 1;
        }

        if (count != Length)
        {
            return false;
        }

        roomCode = new string(compact);
        return true;
    }
}
