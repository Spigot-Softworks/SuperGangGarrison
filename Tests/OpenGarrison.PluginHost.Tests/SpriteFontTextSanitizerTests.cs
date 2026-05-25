using OpenGarrison.Client;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class SpriteFontTextSanitizerTests
{
    private static readonly HashSet<char> ConsoleCharacters = new(
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 []():;.,!?'/\"-_*<>|=+");

    [Fact]
    public void SanitizeReplacesUnsupportedUnicodeWithConsoleSafeText()
    {
        var sanitized = SpriteFontTextSanitizer.Sanitize(
            "Launching \u201cServer\u201d \u2192 map \u2026 ok \u2705",
            ConsoleCharacters);

        Assert.Equal("Launching \"Server\" -> map ... ok ?", sanitized);
        Assert.All(sanitized, character => Assert.Contains(character, ConsoleCharacters));
    }

    [Fact]
    public void SanitizeReplacesControlCharactersWithSpaces()
    {
        var sanitized = SpriteFontTextSanitizer.Sanitize(
            "one\ttwo\r\nthree",
            ConsoleCharacters);

        Assert.Equal("one two  three", sanitized);
        Assert.All(sanitized, character => Assert.Contains(character, ConsoleCharacters));
    }
}
