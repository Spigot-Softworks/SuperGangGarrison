namespace OpenGarrison.Core;

public enum ControlPointInitialOwnership
{
    ModeDefault = 0,
    Neutral = 1,
    Red = 2,
    Blue = 3,
}

public static class ControlPointInitialOwnershipMetadata
{
    public const string PropertyKey = "initialOwner";
    public const string DefaultPropertyValue = "modeDefault";

    public static bool TryParse(string? value, out ControlPointInitialOwnership ownership)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            ownership = ControlPointInitialOwnership.ModeDefault;
            return true;
        }

        switch (value.Trim().ToLowerInvariant())
        {
            case "modedefault":
            case "mode default":
            case "default":
                ownership = ControlPointInitialOwnership.ModeDefault;
                return true;
            case "neutral":
            case "none":
                ownership = ControlPointInitialOwnership.Neutral;
                return true;
            case "red":
                ownership = ControlPointInitialOwnership.Red;
                return true;
            case "blue":
                ownership = ControlPointInitialOwnership.Blue;
                return true;
            default:
                ownership = default;
                return false;
        }
    }

    public static string ToPropertyValue(ControlPointInitialOwnership ownership)
    {
        return ownership switch
        {
            ControlPointInitialOwnership.Neutral => "neutral",
            ControlPointInitialOwnership.Red => "red",
            ControlPointInitialOwnership.Blue => "blue",
            _ => DefaultPropertyValue,
        };
    }

    public static string GetDisplayLabel(ControlPointInitialOwnership ownership)
    {
        return ownership switch
        {
            ControlPointInitialOwnership.Neutral => "Neutral",
            ControlPointInitialOwnership.Red => "Red",
            ControlPointInitialOwnership.Blue => "Blue",
            _ => "Mode default",
        };
    }

    public static string GetDisplayLabel(string? propertyValue)
    {
        return TryParse(propertyValue, out var ownership)
            ? GetDisplayLabel(ownership)
            : propertyValue ?? string.Empty;
    }

    public static string CyclePropertyValue(string current)
    {
        if (!TryParse(current, out var ownership))
        {
            ownership = ControlPointInitialOwnership.ModeDefault;
        }

        var next = (ControlPointInitialOwnership)(((int)ownership + 1) % 4);
        return ToPropertyValue(next);
    }

    public static string CycleOverridePropertyValue(string current)
    {
        if (!TryParse(current, out var ownership) || ownership == ControlPointInitialOwnership.ModeDefault)
        {
            ownership = ControlPointInitialOwnership.Neutral;
        }

        var next = ownership switch
        {
            ControlPointInitialOwnership.Neutral => ControlPointInitialOwnership.Red,
            ControlPointInitialOwnership.Red => ControlPointInitialOwnership.Blue,
            _ => ControlPointInitialOwnership.Neutral,
        };
        return ToPropertyValue(next);
    }

    public static PlayerTeam? ToPlayerTeam(ControlPointInitialOwnership ownership)
    {
        return ownership switch
        {
            ControlPointInitialOwnership.Neutral => null,
            ControlPointInitialOwnership.Red => PlayerTeam.Red,
            ControlPointInitialOwnership.Blue => PlayerTeam.Blue,
            _ => null,
        };
    }
}
