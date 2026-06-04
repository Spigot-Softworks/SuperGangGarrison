namespace OpenGarrison.Core;

public static class BarrierTargetFiltersExtensions
{
    public static bool BlocksAnyPlayerMovement(this in BarrierTargetFilters targets)
    {
        return targets.Blocks(BarrierTargetKind.RedPlayers)
            || targets.Blocks(BarrierTargetKind.BluePlayers)
            || targets.Blocks(BarrierTargetKind.RedIntel)
            || targets.Blocks(BarrierTargetKind.BlueIntel);
    }

    public static bool BlocksAnyProjectile(this in BarrierTargetFilters targets)
    {
        return targets.Blocks(BarrierTargetKind.RedShots)
            || targets.Blocks(BarrierTargetKind.BlueShots);
    }
}
