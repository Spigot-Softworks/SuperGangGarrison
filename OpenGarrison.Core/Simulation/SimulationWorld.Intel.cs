namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private const float IntelMarkerSize = 24f;
    private const int IntelReturnTicks = PlayerEntity.IntelRechargeMaxTicks;
    private const int IntelPickupCooldownTicksAfterDrop = 300;

    public TeamIntelligenceState RedIntel { get; private set; }

    public TeamIntelligenceState BlueIntel { get; private set; }

    public void ForceDropLocalIntel()
    {
        TryDropCarriedIntel();
    }

    public bool ForceGiveEnemyIntelToLocalPlayer()
    {
        if (!LocalPlayer.IsAlive || LocalPlayer.IsCarryingIntel)
        {
            return false;
        }

        var enemyIntel = GetEnemyIntelState(LocalPlayerTeam);
        if (!enemyIntel.IsAtBase && !enemyIntel.IsDropped)
        {
            return false;
        }

        var carriedRechargeTicks = enemyIntel.IsDropped ? enemyIntel.ReturnTicksRemaining : 0f;
        enemyIntel.PickUp();
        LocalPlayer.PickUpIntel(carriedRechargeTicks);
        RegisterWorldSoundEvent("IntelGetSnd", LocalPlayer.X, LocalPlayer.Y);
        return true;
    }

    private void TryDropCarriedIntel()
    {
        TryDropCarriedIntel(LocalPlayer);
    }

    private void TryDropCarriedIntel(PlayerEntity player)
    {
        if (!player.IsCarryingIntel)
        {
            return;
        }

        GetEnemyIntelState(player.Team).Drop(
            player.X,
            player.Y,
            GetPlayerIntelReturnTicks(player));
        player.DropIntel(IntelPickupCooldownTicksAfterDrop);
        RegisterWorldSoundEvent("IntelDropSnd", player.X, player.Y);
        RecordIntelDroppedObjectiveLog(player);
    }

    private void TryPickUpEnemyIntel(PlayerEntity player)
    {
        if (player.IsCarryingIntel
            || player.IntelPickupCooldownTicks > 0
            || player.IsInsideBlockingTeamGate(Level, player.Team))
        {
            return;
        }

        var enemyIntel = GetEnemyIntelState(player.Team);
        if (!enemyIntel.IsAtBase && !enemyIntel.IsDropped)
        {
            return;
        }

        if (!player.IntersectsMarker(enemyIntel.X, enemyIntel.Y, IntelMarkerSize, IntelMarkerSize))
        {
            return;
        }

        var carriedRechargeTicks = enemyIntel.IsDropped ? enemyIntel.ReturnTicksRemaining : 0f;
        enemyIntel.PickUp();
        player.PickUpIntel(carriedRechargeTicks);
        RegisterWorldSoundEvent("IntelGetSnd", player.X, player.Y);
        RecordIntelPickedUpObjectiveLog(player);
    }

    private void TryScoreCarriedIntel(PlayerEntity player)
    {
        if (!player.IsCarryingIntel)
        {
            return;
        }

        var ownBase = Level.GetIntelBase(player.Team);
        if (!ownBase.HasValue)
        {
            return;
        }

        if (!player.IntersectsMarker(ownBase.Value.X, ownBase.Value.Y, IntelMarkerSize, IntelMarkerSize))
        {
            return;
        }

        player.ScoreIntel();
        GetEnemyIntelState(player.Team).ResetToBase();
        RegisterWorldSoundEvent("IntelPutSnd", player.X, player.Y);
        RecordIntelCapturedObjectiveLog(player);

        if (player.Team == PlayerTeam.Blue)
        {
            BlueCaps += 1;
        }
        else
        {
            RedCaps += 1;
        }
    }

    private bool IsIntelAtHome(TeamIntelligenceState intelState)
    {
        var homeBase = Level.GetIntelBase(intelState.Team);
        if (!homeBase.HasValue)
        {
            return intelState.IsAtBase;
        }

        return NearlyEqual(intelState.X, homeBase.Value.X) && NearlyEqual(intelState.Y, homeBase.Value.Y);
    }

    private TeamIntelligenceState GetEnemyIntelState(PlayerTeam team)
    {
        return team == PlayerTeam.Blue ? RedIntel : BlueIntel;
    }

    private TeamIntelligenceState CreateIntelState(PlayerTeam team)
    {
        var intelBase = Level.GetIntelBase(team);
        if (intelBase.HasValue)
        {
            return new TeamIntelligenceState(team, intelBase.Value.X, intelBase.Value.Y);
        }

        var fallbackSpawn = Level.GetSpawn(team, 0);
        return new TeamIntelligenceState(team, fallbackSpawn.X, fallbackSpawn.Y);
    }

    private static int GetPlayerIntelReturnTicks(PlayerEntity player)
    {
        return Math.Clamp((int)MathF.Round(player.IntelRechargeTicks), 0, IntelReturnTicks);
    }
}
