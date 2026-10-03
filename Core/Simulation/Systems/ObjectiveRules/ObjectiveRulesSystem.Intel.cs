namespace OpenGarrison.Core;

internal sealed partial class ObjectiveRulesSystem
{
    internal const float IntelMarkerSize = 24f;
    internal const int IntelReturnTicks = PlayerEntity.IntelRechargeMaxTicks;
    internal const int IntelPickupCooldownTicksAfterDrop = 300;

    public TeamIntelligenceState RedIntel { get; internal set; }

    public TeamIntelligenceState BlueIntel { get; internal set; }

    public void ForceDropLocalIntel()
    {
        TryDropCarriedIntel();
    }

    public bool ForceGiveEnemyIntelToLocalPlayer()
    {
        if (!_host.LocalPlayer.IsAlive
            || _host.LocalPlayer.IsCarryingIntel
            || _host.LocalPlayer.IsLastToDieSpyAfterlifeActive
            || (_host.LocalPlayer.ClassId == PlayerClass.Spy && _host.LocalPlayer.IsSpyCloaked))
        {
            return false;
        }

        var enemyIntel = GetEnemyIntelState(_host.LocalPlayerTeam);
        if (!enemyIntel.IsAtBase && !enemyIntel.IsDropped)
        {
            return false;
        }

        var carriedRechargeTicks = enemyIntel.IsDropped ? enemyIntel.ReturnTicksRemaining : 0f;
        enemyIntel.PickUp();
        _host.LocalPlayer.PickUpIntel(carriedRechargeTicks);
        HaltSpySuperjumpHorizontalMomentumOnIntelPickup(_host.LocalPlayer);
        _host.WorldEffects.RegisterWorldSoundEvent("IntelGetSnd", _host.LocalPlayer.X, _host.LocalPlayer.Y);
        return true;
    }

    internal void TryDropCarriedIntel()
    {
        TryDropCarriedIntel(_host.LocalPlayer);
    }

    internal void TryDropCarriedIntel(PlayerEntity player)
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
        _host.WorldEffects.RegisterWorldSoundEvent("IntelDropSnd", player.X, player.Y);
        _host.KillFeed.RecordIntelDroppedObjectiveLog(player);
    }

    internal void TryPickUpEnemyIntel(PlayerEntity player)
    {
        if (player.IsCarryingIntel
            || !player.IsAlive
            || player.IsLastToDieSpyAfterlifeActive
            || player.IntelPickupCooldownTicks > 0
            || player.IsInsideBlockingTeamGate(_host.Level, player.Team)
            || (player.ClassId == PlayerClass.Spy && player.IsSpyCloaked))
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

        if (player.IsInsideBlockingTeamGate(_host.Level, player.Team, carryingIntel: true))
        {
            return;
        }

        if (_host.DecisionGate.ShouldCancelPickup(
                WorldPickupKind.Intelligence,
                player,
                (int)enemyIntel.Team,
                enemyIntel.Team.ToString(),
                enemyIntel.X,
                enemyIntel.Y))
        {
            return;
        }

        var carriedRechargeTicks = enemyIntel.IsDropped ? enemyIntel.ReturnTicksRemaining : 0f;
        enemyIntel.PickUp();
        player.PickUpIntel(carriedRechargeTicks);
        HaltSpySuperjumpHorizontalMomentumOnIntelPickup(player);
        _host.WorldEffects.RegisterWorldSoundEvent("IntelGetSnd", player.X, player.Y);
        _host.KillFeed.RecordIntelPickedUpObjectiveLog(player);
    }

    internal static void HaltSpySuperjumpHorizontalMomentumOnIntelPickup(PlayerEntity player)
    {
        player.HaltSpySuperjumpHorizontalMomentum();
    }

    internal void TryScoreCarriedIntel(PlayerEntity player)
    {
        if (!player.IsCarryingIntel || player.IsLastToDieSpyAfterlifeActive)
        {
            return;
        }

        var ownBase = _host.Level.GetIntelBase(player.Team);
        if (!ownBase.HasValue)
        {
            return;
        }

        if (!player.IntersectsMarker(ownBase.Value.X, ownBase.Value.Y, IntelMarkerSize, IntelMarkerSize))
        {
            return;
        }

        if (_host.MatchRules.Mode != GameModeKind.Scr
            && !_host.DecisionGate.TryAwardTeamScore(player.Team, 1, "intel_capture", player.Id))
        {
            return;
        }

        player.ScoreIntel();
        _host.Scorekeeping.AwardObjectiveCapturePoints(player);
        GetEnemyIntelState(player.Team).ResetToBase();
        _host.WorldEffects.RegisterWorldSoundEvent("IntelPutSnd", player.X, player.Y);
        _host.KillFeed.RecordIntelCapturedObjectiveLog(player);

        if (_host.MatchRules.Mode != GameModeKind.Scr
            && player.Team == PlayerTeam.Red
            && ShouldEndMatchOnRedTeamIntelCapture())
        {
            _host.DecisionGate.TryEndRound(PlayerTeam.Red, "special_red_intel_capture");
        }
    }

    internal bool IsIntelAtHome(TeamIntelligenceState intelState)
    {
        var homeBase = _host.Level.GetIntelBase(intelState.Team);
        if (!homeBase.HasValue)
        {
            return intelState.IsAtBase;
        }

        return _host.NearlyEqual(intelState.X, homeBase.Value.X) && _host.NearlyEqual(intelState.Y, homeBase.Value.Y);
    }

    internal TeamIntelligenceState GetEnemyIntelState(PlayerTeam team)
    {
        return team == PlayerTeam.Blue ? RedIntel : BlueIntel;
    }

    internal TeamIntelligenceState CreateIntelState(PlayerTeam team)
    {
        var intelBase = _host.Level.GetIntelBase(team);
        if (intelBase.HasValue)
        {
            return new TeamIntelligenceState(team, intelBase.Value.X, intelBase.Value.Y);
        }

        var fallbackSpawn = _host.Level.GetSpawn(team, 0);
        return new TeamIntelligenceState(team, fallbackSpawn.X, fallbackSpawn.Y);
    }

    internal static int GetPlayerIntelReturnTicks(PlayerEntity player)
    {
        return Math.Clamp((int)MathF.Round(player.IntelRechargeTicks), 0, IntelReturnTicks);
    }
}
