namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{

    public void ConfigureSpecialCaptureTheFlagRules(bool endMatchOnRedTeamIntelCapture)
    {
        MapRuntime.EndMatchOnRedTeamIntelCapture = endMatchOnRedTeamIntelCapture;
    }

    private bool ShouldEndMatchOnRedTeamIntelCapture()
    {
        return MapRuntime.EndMatchOnRedTeamIntelCapture
            && MatchRules.Mode == GameModeKind.CaptureTheFlag
            && !MatchState.IsEnded;
    }
}
