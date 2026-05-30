#nullable enable

using OpenGarrison.Core.BotBrain;
using OpenGarrison.Core;
using System.Diagnostics;

namespace OpenGarrison.Client;

public partial class Game1
{
    private static void ResetPracticeNavigationState()
    {
    }

    private void LoadPracticeNavigationAssetsForCurrentLevel()
    {
        AddConsoleLine(GetPracticeNavigationDiagnosticsSummary() + WarmPracticeBotBrainNavigationForCurrentLevel());
    }

    private static string GetPracticeNavigationDiagnosticsSummary()
    {
        return "nav clientbot-navpoints";
    }

    private string WarmPracticeBotBrainNavigationForCurrentLevel()
    {
        if (!OperatingSystem.IsBrowser()
            || _world.Level is null
            || GetOfflineEnemyBotCount() + GetOfflineFriendlyBotCount() <= 0)
        {
            return string.Empty;
        }

        var stopwatch = Stopwatch.StartNew();
        var graphLoaded = BotNavigationAssetStore.TryLoadCachedGraph(_world.Level, out _);
        var tapeLoaded = BotBrainObjectiveTapeStore.TryLoad(_world.Level, out _);
        var proofGraphCount = WarmPracticeBotBrainProofGraphsForCurrentLevel();
        stopwatch.Stop();

        return $" botbrain-warmup graph={graphLoaded} tape={tapeLoaded} proofgraphs={proofGraphCount} elapsed={stopwatch.Elapsed.TotalMilliseconds:0.0}ms";
    }

    private int WarmPracticeBotBrainProofGraphsForCurrentLevel()
    {
        if (_world.Level is null)
        {
            return 0;
        }

        var loadedCount = 0;
        var eligibleClasses = GetEligiblePracticeBotClassCycle();
        Span<PlayerTeam> teams = [PlayerTeam.Red, PlayerTeam.Blue];
        foreach (var team in teams)
        {
            foreach (var classId in eligibleClasses)
            {
                if (VerifiedNavProofGraphAssetStore.TryLoad(_world.Level, team, classId, out _))
                {
                    loadedCount += 1;
                }
            }
        }

        return loadedCount;
    }
}
