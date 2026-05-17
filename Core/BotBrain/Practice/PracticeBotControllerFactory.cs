using OpenGarrison.Core;

namespace OpenGarrison.Core.BotBrain;

public static class PracticeBotControllerFactory
{
    public const string BotModeEnvironmentVariable = "OG_BOT_MODE";

    public static IPracticeBotController Create(OfflineBotControllerMode mode)
    {
        return new BotBrainPracticeBotController();
    }

    public static OfflineBotControllerMode ResolveModeFromEnvironment(OfflineBotControllerMode defaultMode)
    {
        var value = Environment.GetEnvironmentVariable(BotModeEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultMode;
        }

        return ParseMode(value, defaultMode);
    }

    public static OfflineBotControllerMode ParseMode(string value, OfflineBotControllerMode defaultMode)
    {
        var normalized = value.Trim();
        if (Enum.TryParse<OfflineBotControllerMode>(normalized, ignoreCase: true, out var parsed))
        {
            return NormalizeMode(parsed);
        }

        normalized = normalized.Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal);
        return defaultMode;
    }

    public static OfflineBotControllerMode NormalizeMode(OfflineBotControllerMode mode)
    {
        return OfflineBotControllerMode.BotBrain;
    }
}
