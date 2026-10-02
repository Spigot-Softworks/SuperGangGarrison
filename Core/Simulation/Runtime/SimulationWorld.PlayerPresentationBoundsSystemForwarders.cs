using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

// Forwarders kept for callers outside the world partials (Client, Server, bots,
// plugins, tests). Callers should move to the system directly over time.
public sealed partial class SimulationWorld
{
    private void GetCachedPlayerPresentationHitBounds(PlayerEntity player, out float left, out float top, out float right, out float bottom)
        => PresentationBounds.GetCachedPlayerPresentationHitBounds(player, out left, out top, out right, out bottom);
    private static string? GetPlayerPresentationBodySpriteName(SimulationWorld world, PlayerEntity player)
        => world.PresentationBounds.GetPlayerPresentationBodySpriteName(player);
    private static void GetPlayerPresentationHitBounds(SimulationWorld world, PlayerEntity player, out float left, out float top, out float right, out float bottom)
        => world.PresentationBounds.GetPlayerPresentationHitBounds(player, out left, out top, out right, out bottom);
    private static string? GetPlayerSpriteName(PlayerClass classId, PlayerTeam team)
        => PlayerPresentationBoundsSystem.GetPlayerSpriteName(classId, team);
    private static string? GetPlayerSpritePrefix(PlayerClass classId)
        => PlayerPresentationBoundsSystem.GetPlayerSpritePrefix(classId);
    private static string? GetPresentationFacingSpriteName(PlayerClass classId, PlayerTeam team, Func<GameplayClassPresentationDefinition, string> facingLeftSuffixSelector, Func<GameplayClassPresentationDefinition, string> facingRightSuffixSelector, bool facingLeft, string legacyFacingLeftSuffix, string legacyFacingRightSuffix)
        => PlayerPresentationBoundsSystem.GetPresentationFacingSpriteName(classId, team, facingLeftSuffixSelector, facingRightSuffixSelector, facingLeft, legacyFacingLeftSuffix, legacyFacingRightSuffix);
    private static string? GetPresentationSpriteName(PlayerClass classId, PlayerTeam team, Func<GameplayClassPresentationDefinition, string> suffixSelector, string legacySuffix)
        => PlayerPresentationBoundsSystem.GetPresentationSpriteName(classId, team, suffixSelector, legacySuffix);
    private static string? GetTeamSpriteName(PlayerClass classId, PlayerTeam team, string suffix)
        => PlayerPresentationBoundsSystem.GetTeamSpriteName(classId, team, suffix);
}
