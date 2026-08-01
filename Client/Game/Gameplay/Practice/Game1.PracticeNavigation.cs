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
        if (_world.Level is null)
        {
            return string.Empty;
        }

        var warmTrace = Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_WARM_TRACE") is "1" or "true" or "TRUE";
        if (warmTrace)
        {
            Console.WriteLine($"[botbrain] practice-warm-entry level={_world.Level.Name} bots={GetOfflineEnemyBotCount() + GetOfflineFriendlyBotCount()}");
        }

        var stopwatch = Stopwatch.StartNew();
        var graphLoaded = BotNavigationAssetStore.TryLoadCachedGraph(_world.Level, out _);
        var alphaGraph = Og2NavigationGraphStore.GetOrBuild(_world.Level);
        var warmedAlphaPaths = alphaGraph.WarmAlphaObjectiveRoutes(_world.Level, GetEligiblePracticeBotClassCycle());
        _world.WarmCombatSpatialIndices();
        var tapeLoaded = BotBrainObjectiveTapeStore.TryLoad(_world.Level, out _);
        var proofGraphCount = WarmPracticeBotBrainProofGraphsForCurrentLevel();
        stopwatch.Stop();

        if (warmTrace)
        {
            Console.WriteLine($"[botbrain] practice-warm-result paths={warmedAlphaPaths} cache={alphaGraph.AlphaPathCacheCount} elapsedMs={stopwatch.Elapsed.TotalMilliseconds:0.0}");
        }

        return $" botbrain-warmup graph={graphLoaded} alphaNodes={alphaGraph.NodeCount} alphaPaths={warmedAlphaPaths} tape={tapeLoaded} proofgraphs={proofGraphCount} elapsed={stopwatch.Elapsed.TotalMilliseconds:0.0}ms";
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
