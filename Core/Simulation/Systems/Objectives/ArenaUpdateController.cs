namespace OpenGarrison.Core;

internal sealed class ArenaUpdateController
{
    private readonly IMatchObjectiveHost _host;

    public ArenaUpdateController(IMatchObjectiveHost host)
    {
        _host = host;
    }

    public void AdvanceObjectives()
    {
        if (_host.MatchState.IsEnded)
        {
            return;
        }

        if (_host.Objectives.Arena.UnlockTicksRemaining > 0)
        {
            _host.Objectives.Arena.UnlockTicksRemaining -= 1;
        }

        var redCappers = _host.CountPlayersInArenaCaptureZone(PlayerTeam.Red);
        var blueCappers = _host.CountPlayersInArenaCaptureZone(PlayerTeam.Blue);
        var defended = redCappers > 0 && blueCappers > 0;
        PlayerTeam? capTeam = null;
        var cappers = 0;

        if (redCappers > 0 && blueCappers == 0 && _host.Objectives.Arena.PointTeam != PlayerTeam.Red)
        {
            capTeam = PlayerTeam.Red;
            cappers = redCappers;
        }
        else if (blueCappers > 0 && redCappers == 0 && _host.Objectives.Arena.PointTeam != PlayerTeam.Blue)
        {
            capTeam = PlayerTeam.Blue;
            cappers = blueCappers;
        }

        if (_host.Objectives.Arena.CappingTicks > 0f && _host.Objectives.Arena.CappingTeam != capTeam)
        {
            cappers = 0;
        }
        else if (_host.Objectives.Arena.PointTeam.HasValue && capTeam == _host.Objectives.Arena.PointTeam.Value)
        {
            cappers = 0;
        }

        _host.Objectives.Arena.Cappers = cappers;

        var capStrength = 0f;
        for (var index = 1; index <= cappers; index += 1)
        {
            capStrength += index <= 2 ? 1f : 0.5f;
        }

        if (_host.Objectives.Arena.UnlockTicksRemaining > 0)
        {
            _host.Objectives.Arena.CappingTicks = 0f;
            _host.Objectives.Arena.CappingTeam = null;
            return;
        }

        if (capTeam.HasValue && cappers > 0 && _host.Objectives.Arena.CappingTicks < ArenaObjectiveState.PointCapTimeTicksDefault)
        {
            _host.Objectives.Arena.CappingTicks += capStrength * _host.ConfiguredCaptureSpeedMultiplierPerPlayer;
            _host.Objectives.Arena.CappingTeam = capTeam;
        }
        else if (_host.Objectives.Arena.CappingTicks > 0f && cappers == 0 && !defended)
        {
            _host.Objectives.Arena.CappingTicks -= 1f;
            if (_host.Objectives.Arena.PointTeam == PlayerTeam.Blue)
            {
                _host.Objectives.Arena.CappingTicks -= blueCappers * 0.5f;
            }
            else if (_host.Objectives.Arena.PointTeam == PlayerTeam.Red)
            {
                _host.Objectives.Arena.CappingTicks -= redCappers * 0.5f;
            }
        }

        if (_host.Objectives.Arena.CappingTicks <= 0f)
        {
            _host.Objectives.Arena.CappingTicks = 0f;
            _host.Objectives.Arena.CappingTeam = null;
            return;
        }

        if (_host.Objectives.Arena.CappingTicks >= ArenaObjectiveState.PointCapTimeTicksDefault && _host.Objectives.Arena.CappingTeam.HasValue)
        {
            var winner = _host.Objectives.Arena.CappingTeam.Value;
            if (!_host.TryEndRound(winner, "arena_point_capture"))
            {
                return;
            }

            _host.Objectives.Arena.PointTeam = winner;
            _host.Objectives.Arena.RecordRoundWin(winner);

        }
    }
}
