namespace OpenGarrison.Core;

internal sealed partial class ObjectiveRulesSystem
{
    internal void ConfigureSpecialCaptureTheFlagRules(bool endMatchOnRedTeamIntelCapture)
    {
        _host.MapRuntime.EndMatchOnRedTeamIntelCapture = endMatchOnRedTeamIntelCapture;
    }

    private bool ShouldEndMatchOnRedTeamIntelCapture()
    {
        return _host.MapRuntime.EndMatchOnRedTeamIntelCapture
            && _host.MatchRules.Mode == GameModeKind.CaptureTheFlag
            && !_host.MatchState.IsEnded;
    }
}
