using OpenGarrison.MLBot.Contracts;

namespace OpenGarrison.MLBot;

public static class MLBotModeResolver
{
    public const string BotModeEnvironmentVariable = "OG_BOT_MODE";

    public static MLBotControllerMode Resolve()
    {
        var configured = Environment.GetEnvironmentVariable(BotModeEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(configured))
        {
            return MLBotControllerMode.Legacy;
        }

        return configured.Trim().ToLowerInvariant() switch
        {
            "ml" or "learned" or "new" => MLBotControllerMode.ML,
            "ml_capture" or "ml-capture" or "capture" or "record" or "recording" => MLBotControllerMode.Capture,
            _ => MLBotControllerMode.Legacy,
        };
    }
}
