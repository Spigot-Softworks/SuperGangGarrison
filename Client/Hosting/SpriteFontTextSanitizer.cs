#nullable enable

using System.Text;

namespace OpenGarrison.Client;

internal static class SpriteFontTextSanitizer
{
    public static string Sanitize(string? text, IReadOnlyCollection<char> supportedCharacters)
    {
        ArgumentNullException.ThrowIfNull(supportedCharacters);
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            AppendSanitizedCharacter(builder, supportedCharacters, character);
        }

        return builder.ToString();
    }

    private static void AppendSanitizedCharacter(
        StringBuilder builder,
        IReadOnlyCollection<char> supportedCharacters,
        char character)
    {
        if (!char.IsControl(character) && supportedCharacters.Contains(character))
        {
            builder.Append(character);
            return;
        }

        switch (character)
        {
            case '\t':
            case '\r':
            case '\n':
            case '\u00a0':
                AppendFallback(builder, supportedCharacters, " ");
                return;
            case '\u2018':
            case '\u2019':
                AppendFallback(builder, supportedCharacters, "'");
                return;
            case '\u201c':
            case '\u201d':
                AppendFallback(builder, supportedCharacters, "\"");
                return;
            case '\u2013':
            case '\u2014':
            case '\u2212':
                AppendFallback(builder, supportedCharacters, "-");
                return;
            case '\u2022':
            case '\u25cf':
            case '\u25e6':
                AppendFallback(builder, supportedCharacters, "*");
                return;
            case '\u2026':
                AppendFallback(builder, supportedCharacters, "...");
                return;
            case '\u2190':
                AppendFallback(builder, supportedCharacters, "<-");
                return;
            case '\u2192':
                AppendFallback(builder, supportedCharacters, "->");
                return;
            default:
                AppendFallback(builder, supportedCharacters, "?");
                return;
        }
    }

    private static void AppendFallback(
        StringBuilder builder,
        IReadOnlyCollection<char> supportedCharacters,
        string fallback)
    {
        foreach (var character in fallback)
        {
            if (supportedCharacters.Contains(character))
            {
                builder.Append(character);
            }
            else if (supportedCharacters.Contains(' '))
            {
                builder.Append(' ');
            }
        }
    }
}
