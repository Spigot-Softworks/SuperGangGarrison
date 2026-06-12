namespace OpenGarrison.Core;

public sealed record CustomMapScrSettings(
    int ScoreToWin,
    ScrWinWhenScore WinWhenScore,
    ScrRoundEndWin RoundEndWin,
    int RedStartingScore,
    int BlueStartingScore)
{
    public static CustomMapScrSettings Default { get; } = new(
        ScrMapSettingsMetadata.DefaultScoreToWin,
        ScrWinWhenScore.MoreEqual,
        ScrRoundEndWin.MorePoints,
        0,
        0);

    public bool TeamQualifiesForThreshold(PlayerTeam team, int score)
    {
        return WinWhenScore switch
        {
            ScrWinWhenScore.LessEqual => score <= ScoreToWin,
            _ => score >= ScoreToWin,
        };
    }

    public PlayerTeam? ResolveRoundEndWinner(int redCaps, int blueCaps)
    {
        return RoundEndWin switch
        {
            ScrRoundEndWin.LessPoints => ResolveLowerScoreWinner(redCaps, blueCaps),
            ScrRoundEndWin.Red => PlayerTeam.Red,
            ScrRoundEndWin.Blue => PlayerTeam.Blue,
            _ => ResolveHigherScoreWinner(redCaps, blueCaps),
        };
    }

    private static PlayerTeam? ResolveHigherScoreWinner(int redCaps, int blueCaps)
    {
        if (redCaps > blueCaps)
        {
            return PlayerTeam.Red;
        }

        if (blueCaps > redCaps)
        {
            return PlayerTeam.Blue;
        }

        return null;
    }

    private static PlayerTeam? ResolveLowerScoreWinner(int redCaps, int blueCaps)
    {
        if (redCaps < blueCaps)
        {
            return PlayerTeam.Red;
        }

        if (blueCaps < redCaps)
        {
            return PlayerTeam.Blue;
        }

        return null;
    }
}
