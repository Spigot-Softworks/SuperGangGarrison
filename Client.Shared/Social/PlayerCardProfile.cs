#nullable enable

using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenGarrison.ClientShared;

public sealed class PlayerCardProfile
{
    private static readonly JsonSerializerOptions WireJsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("background")]
    public string Background { get; set; } = "MenuBackground1.png";

    [JsonPropertyName("class")]
    public string Class { get; set; } = "Spy";

    [JsonPropertyName("team")]
    public string Team { get; set; } = "Blue";

    [JsonPropertyName("frame")]
    public int Frame { get; set; } = 0;

    [JsonPropertyName("portraitZoom")]
    public float PortraitZoom { get; set; } = 4.2f;

    [JsonPropertyName("portraitOffsetX")]
    public float PortraitOffsetX { get; set; } = 0f;

    [JsonPropertyName("portraitOffsetY")]
    public float PortraitOffsetY { get; set; } = 0f;

    [JsonPropertyName("color1")]
    public string Color1 { get; set; } = "#263880";

    [JsonPropertyName("color2")]
    public string Color2 { get; set; } = "#80483A";

    [JsonPropertyName("gradient")]
    public bool Gradient { get; set; } = true;

    [JsonPropertyName("portraitColor1")]
    public string PortraitColor1 { get; set; } = "#263880";

    [JsonPropertyName("portraitColor2")]
    public string PortraitColor2 { get; set; } = "#80483A";

    [JsonPropertyName("portraitGradient")]
    public bool PortraitGradient { get; set; } = true;

    public static PlayerCardProfile CreateDefault() => new();

    public static PlayerCardProfile Sanitize(PlayerCardProfile? profile)
    {
        profile ??= CreateDefault();
        profile.Version = Math.Clamp(profile.Version, 1, 1);
        profile.Background = SanitizeToken(profile.Background, "MenuBackground1.png", 96);
        profile.Class = SanitizeToken(profile.Class, "Spy", 24);
        profile.Team = string.Equals(profile.Team, "Red", StringComparison.OrdinalIgnoreCase) ? "Red" : "Blue";
        profile.Frame = Math.Clamp(profile.Frame, 0, 64);
        profile.PortraitZoom = Math.Clamp(float.IsFinite(profile.PortraitZoom) ? profile.PortraitZoom : 4.2f, 1.25f, 9f);
        profile.PortraitOffsetX = Math.Clamp(float.IsFinite(profile.PortraitOffsetX) ? profile.PortraitOffsetX : 0f, -80f, 80f);
        profile.PortraitOffsetY = Math.Clamp(float.IsFinite(profile.PortraitOffsetY) ? profile.PortraitOffsetY : 0f, -80f, 80f);
        profile.Color1 = SanitizeHexColor(profile.Color1, "#263880");
        profile.Color2 = SanitizeHexColor(profile.Color2, "#80483A");
        profile.PortraitColor1 = SanitizeHexColor(profile.PortraitColor1, profile.Color1);
        profile.PortraitColor2 = SanitizeHexColor(profile.PortraitColor2, profile.Color2);
        return profile;
    }

    public static string Serialize(PlayerCardProfile? profile)
    {
        try
        {
            return JsonSerializer.Serialize(Sanitize(profile), WireJsonOptions);
        }
        catch
        {
            return "{}";
        }
    }

    public static PlayerCardProfile Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return CreateDefault();
        }

        try
        {
            return Sanitize(JsonSerializer.Deserialize<PlayerCardProfile>(json, WireJsonOptions));
        }
        catch
        {
            return CreateDefault();
        }
    }

    private static string SanitizeToken(string? value, string fallback, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var sanitized = new string(value.Where(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-').ToArray()).Trim();
        if (sanitized.Length == 0)
        {
            return fallback;
        }

        return sanitized.Length > maxLength ? sanitized[..maxLength] : sanitized;
    }

    private static string SanitizeHexColor(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var text = value.Trim();
        if (text.Length == 6)
        {
            text = "#" + text;
        }

        if (text.Length != 7 || text[0] != '#')
        {
            return fallback;
        }

        for (var index = 1; index < text.Length; index += 1)
        {
            if (!Uri.IsHexDigit(text[index]))
            {
                return fallback;
            }
        }

        return text.ToUpperInvariant();
    }
}
