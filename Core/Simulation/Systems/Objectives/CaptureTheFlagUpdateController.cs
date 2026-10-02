namespace OpenGarrison.Core;

internal sealed class CaptureTheFlagUpdateController
{
    private readonly IMatchObjectiveHost _host;

    public CaptureTheFlagUpdateController(IMatchObjectiveHost host)
    {
        _host = host;
    }

    public void AdvanceObjectives()
    {
        if (_host.MatchState.IsEnded)
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
