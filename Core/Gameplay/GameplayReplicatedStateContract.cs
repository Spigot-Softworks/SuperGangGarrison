namespace OpenGarrison.Core;

public static class GameplayReplicatedStateContract
{
    public const int MaxIdentifierLength = 64;

    public static bool TryNormalizeIdentifier(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (trimmed.Length == 0 || trimmed.Length > MaxIdentifierLength)
        {
            return false;
        }

        for (var index = 0; index < trimmed.Length; index += 1)
        {
            if (!IsAllowedIdentifierCharacter(trimmed[index]))
            {
                return false;
            }
        }

        normalized = trimmed;
        return true;
    }

    private static bool IsAllowedIdentifierCharacter(char value)
    {
        return char.IsAsciiLetterOrDigit(value)
            || value is '.' or '_' or '-';
    }
}
