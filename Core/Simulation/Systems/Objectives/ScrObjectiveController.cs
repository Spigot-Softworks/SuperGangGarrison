namespace OpenGarrison.Core;

internal sealed class ScrObjectiveController
{
    private readonly IMatchObjectiveHost _host;

    public ScrObjectiveController(IMatchObjectiveHost host)
    {
        _host = host;
    }

    public void AdvanceObjectives()
    {
        if (_host.MatchState.IsEnded)
        {
            return;
        }

        if (_host.Level.ShouldSimulateControlPoints && _host.Objectives.ControlPoints.Points.Count > 0)
        {
            _host.UpdateControlPointState();
        }

        AdvanceIntelObjectives();
        _host.EvaluateMapLogicIntelTriggersIfNeeded();
    }

    private void AdvanceIntelObjectives()
    {
        if (_host.Level.IntelBases.Count == 0)
        {
            return;
        }

        var redWasDropped = _host.RedIntel.IsDropped;
        var blueWasDropped = _host.BlueIntel.IsDropped;
        _host.RedIntel.AdvanceTick();
        _host.BlueIntel.AdvanceTick();
        if (redWasDropped && _host.RedIntel.IsAtBase)
        {
            _host.RegisterWorldSoundEvent("IntelDropSnd", _host.RedIntel.X, _host.RedIntel.Y);
            _host.RecordIntelReturnedObjectiveLog(PlayerTeam.Red);
        }

        if (blueWasDropped && _host.BlueIntel.IsAtBase)
        {
            _host.RegisterWorldSoundEvent("IntelDropSnd", _host.BlueIntel.X, _host.BlueIntel.Y);
            _host.RecordIntelReturnedObjectiveLog(PlayerTeam.Blue);
        }

        foreach (var player in _host.EnumerateSimulatedPlayers())
        {
            if (!player.IsAlive)
            {
                continue;
            }

            _host.TryPickUpEnemyIntel(player);
            _host.TryScoreCarriedIntel(player);
        }
    }
}
