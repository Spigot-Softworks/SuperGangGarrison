namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private void UpdateCaptureTheFlagState()
    {
        if (MatchState.IsEnded)
        {
            return;
        }

        var redWasDropped = RedIntel.IsDropped;
        var blueWasDropped = BlueIntel.IsDropped;
        RedIntel.AdvanceTick();
        BlueIntel.AdvanceTick();
        if (redWasDropped && RedIntel.IsAtBase)
        {
            RegisterWorldSoundEvent("IntelDropSnd", RedIntel.X, RedIntel.Y);
            RecordIntelReturnedObjectiveLog(PlayerTeam.Red);
        }

        if (blueWasDropped && BlueIntel.IsAtBase)
        {
            RegisterWorldSoundEvent("IntelDropSnd", BlueIntel.X, BlueIntel.Y);
            RecordIntelReturnedObjectiveLog(PlayerTeam.Blue);
        }

        foreach (var player in EnumerateSimulatedPlayers())
        {
            if (!player.IsAlive)
            {
                continue;
            }

            TryPickUpEnemyIntel(player);
            TryScoreCarriedIntel(player);
        }
    }

    private void UpdateArenaState()
    {
        if (MatchState.IsEnded)
        {
            return;
        }

        if (_arenaUnlockTicksRemaining > 0)
        {
            _arenaUnlockTicksRemaining -= 1;
        }

        var redCappers = CountPlayersInArenaCaptureZone(PlayerTeam.Red);
        var blueCappers = CountPlayersInArenaCaptureZone(PlayerTeam.Blue);
        var defended = redCappers > 0 && blueCappers > 0;
        PlayerTeam? capTeam = null;
        var cappers = 0;

        if (redCappers > 0 && blueCappers == 0 && _arenaPointTeam != PlayerTeam.Red)
        {
            capTeam = PlayerTeam.Red;
            cappers = redCappers;
        }
        else if (blueCappers > 0 && redCappers == 0 && _arenaPointTeam != PlayerTeam.Blue)
        {
            capTeam = PlayerTeam.Blue;
            cappers = blueCappers;
        }

        if (_arenaCappingTicks > 0f && _arenaCappingTeam != capTeam)
        {
            cappers = 0;
        }
        else if (_arenaPointTeam.HasValue && capTeam == _arenaPointTeam.Value)
        {
            cappers = 0;
        }

        _arenaCappers = cappers;

        var capStrength = 0f;
        for (var index = 1; index <= cappers; index += 1)
        {
            capStrength += index <= 2 ? 1f : 0.5f;
        }

        if (_arenaUnlockTicksRemaining > 0)
        {
            _arenaCappingTicks = 0f;
            _arenaCappingTeam = null;
            return;
        }

        if (capTeam.HasValue && cappers > 0 && _arenaCappingTicks < ArenaPointCapTimeTicksDefault)
        {
            _arenaCappingTicks += capStrength;
            _arenaCappingTeam = capTeam;
        }
        else if (_arenaCappingTicks > 0f && cappers == 0 && !defended)
        {
            _arenaCappingTicks -= 1f;
            if (_arenaPointTeam == PlayerTeam.Blue)
            {
                _arenaCappingTicks -= blueCappers * 0.5f;
            }
            else if (_arenaPointTeam == PlayerTeam.Red)
            {
                _arenaCappingTicks -= redCappers * 0.5f;
            }
        }

        if (_arenaCappingTicks <= 0f)
        {
            _arenaCappingTicks = 0f;
            _arenaCappingTeam = null;
            return;
        }

        if (_arenaCappingTicks >= ArenaPointCapTimeTicksDefault && _arenaCappingTeam.HasValue)
        {
            _arenaPointTeam = _arenaCappingTeam.Value;
            EndArenaRound(_arenaPointTeam.Value);
        }
    }


    private void AdvanceMatchState()
    {
        if (MatchRules.Mode == GameModeKind.Arena)
        {
            AdvanceArenaMatchState();
            return;
        }
        if (MatchRules.Mode == GameModeKind.ControlPoint)
        {
            AdvanceControlPointMatchState();
            return;
        }
        if (MatchRules.Mode == GameModeKind.Generator)
        {
            AdvanceGeneratorMatchState();
            return;
        }

        if (MatchState.IsEnded)
        {
            return;
        }

        var capWinner = GetCapLimitWinner();
        if (capWinner.HasValue)
        {
            MatchState = MatchState with { Phase = MatchPhase.Ended, WinnerTeam = capWinner };
            QueuePendingMapChange();
            return;
        }

        if (MatchState.Phase == MatchPhase.Overtime)
        {
            if (!AreCaptureTheFlagObjectivesSettled())
            {
                return;
            }

            MatchState = MatchState with
            {
                Phase = MatchPhase.Ended,
                WinnerTeam = GetHigherCapWinner(),
            };
            QueuePendingMapChange();
            return;
        }

        if (MatchState.TimeRemainingTicks > 0)
        {
            MatchState = MatchState with { TimeRemainingTicks = MatchState.TimeRemainingTicks - 1 };
            if (MatchState.TimeRemainingTicks > 0)
            {
                return;
            }
        }

        if (AreCaptureTheFlagObjectivesSettled())
        {
            MatchState = MatchState with { Phase = MatchPhase.Ended, WinnerTeam = GetHigherCapWinner() };
            QueuePendingMapChange();
            return;
        }

        MatchState = MatchState with { Phase = MatchPhase.Overtime, WinnerTeam = null };
    }

    private void AdvanceArenaMatchState()
    {
        if (MatchState.IsEnded)
        {
            return;
        }

        var redAlive = ArenaRedAliveCount;
        var blueAlive = ArenaBlueAliveCount;
        var redPlayers = ArenaRedPlayerCount;
        var bluePlayers = ArenaBluePlayerCount;

        if (redPlayers > 0 && bluePlayers > 0)
        {
            if (redAlive == 0 && blueAlive > 0)
            {
                EndArenaRound(PlayerTeam.Blue);
                return;
            }

            if (blueAlive == 0 && redAlive > 0)
            {
                EndArenaRound(PlayerTeam.Red);
                return;
            }
        }

        if (MatchState.TimeRemainingTicks > 0)
        {
            MatchState = MatchState with { TimeRemainingTicks = MatchState.TimeRemainingTicks - 1 };
            if (MatchState.TimeRemainingTicks > 0)
            {
                return;
            }
        }

        if (redAlive > 0 && blueAlive > 0 && redPlayers > 0 && bluePlayers > 0)
        {
            MatchState = MatchState with { Phase = MatchPhase.Overtime, WinnerTeam = null };
            return;
        }

        MatchState = MatchState with { Phase = MatchPhase.Ended, WinnerTeam = null };
        QueuePendingMapChange();
    }

    private void EndArenaRound(PlayerTeam winner)
    {
        if (MatchState.IsEnded)
        {
            return;
        }

        if (winner == PlayerTeam.Red)
        {
            _arenaRedConsecutiveWins += 1;
            _arenaBlueConsecutiveWins = 0;
        }
        else
        {
            _arenaBlueConsecutiveWins += 1;
            _arenaRedConsecutiveWins = 0;
        }

        MatchState = MatchState with { Phase = MatchPhase.Ended, WinnerTeam = winner };
        QueuePendingMapChange();
    }

    private bool AreCaptureTheFlagObjectivesSettled()
    {
        return IsIntelAtHome(RedIntel) && IsIntelAtHome(BlueIntel);
    }

    private PlayerTeam? GetCapLimitWinner()
    {
        if (RedCaps >= MatchRules.CapLimit)
        {
            return PlayerTeam.Red;
        }

        if (BlueCaps >= MatchRules.CapLimit)
        {
            return PlayerTeam.Blue;
        }

        return null;
    }

    private PlayerTeam? GetHigherCapWinner()
    {
        if (RedCaps > BlueCaps)
        {
            return PlayerTeam.Red;
        }

        if (BlueCaps > RedCaps)
        {
            return PlayerTeam.Blue;
        }

        return null;
    }

    private static bool NearlyEqual(float left, float right)
    {
        return MathF.Abs(left - right) <= 0.01f;
    }
}
