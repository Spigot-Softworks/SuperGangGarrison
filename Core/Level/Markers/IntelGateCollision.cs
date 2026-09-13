namespace OpenGarrison.Core;

public static class IntelGateCollision
{
    // CTF players carry the opposing team's intel. A colored gate filters
    // that intel, while a neutral gate filters either team's carrier.
    public static bool BlocksPlayer(PlayerTeam? intelTeam, PlayerTeam playerTeam, bool carryingIntel)
        => carryingIntel && (!intelTeam.HasValue || intelTeam.Value != playerTeam);
}
