namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private bool _endMatchOnRedTeamIntelCapture;

    public void ConfigureSpecialCaptureTheFlagRules(bool endMatchOnRedTeamIntelCapture)
    {
        _endMatchOnRedTeamIntelCapture = endMatchOnRedTeamIntelCapture;
    }

    private bool ShouldEndMatchOnRedTeamIntelCapture()
    {
        return _endMatchOnRedTeamIntelCapture
            && MatchRules.Mode == GameModeKind.CaptureTheFlag
            && !MatchState.IsEnded;
    }
}
