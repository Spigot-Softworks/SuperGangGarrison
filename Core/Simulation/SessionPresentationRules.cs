using System;

namespace OpenGarrison.Core;

public static class SessionPresentationRules
{
    private const int PresentationSeedSalt = 0x50524553;

    public static int DerivePresentationSeed(string levelName, string mapContentHash, int tickRate)
    {
        return HashCode.Combine(
            StringComparer.OrdinalIgnoreCase.GetHashCode(levelName ?? string.Empty),
            StringComparer.OrdinalIgnoreCase.GetHashCode(mapContentHash ?? string.Empty),
            tickRate,
            PresentationSeedSalt);
    }
}
