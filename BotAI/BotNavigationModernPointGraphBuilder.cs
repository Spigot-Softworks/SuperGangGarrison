using OpenGarrison.Core;

namespace OpenGarrison.BotAI;

public static class BotNavigationModernPointGraphBuilder
{
    public static BotNavigationAsset Build(
        SimpleLevel level,
        string? levelFingerprint = null,
        bool validateTraversals = false)
    {
        ArgumentNullException.ThrowIfNull(level);
        _ = validateTraversals;
        return GmlClientBotPointGraphBuilder.Build(level, levelFingerprint);
    }
}
