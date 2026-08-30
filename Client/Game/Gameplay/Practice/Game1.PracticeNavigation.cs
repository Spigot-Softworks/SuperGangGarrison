#nullable enable

using OpenGarrison.Core.BotBrain;
using OpenGarrison.Core;
using System.Diagnostics;
using System.Threading.Tasks;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const string PracticeNavigationWarmupMessage = "Loading...";

    private bool _practiceNavigationWarmupPending;
    private Task<PracticeNavigationWarmupResult>? _practiceNavigationWarmupTask;
    private SimpleLevel? _practiceNavigationWarmupLevel;
    private PlayerClass[] _practiceNavigationWarmupClasses = [];

    private sealed record PracticeNavigationWarmupResult(
        bool Success,
        string Diagnostics);

    private static void ResetPracticeNavigationState()
    {
    }

    private void QueuePracticeNavigationWarmupForCurrentLevel()
    {
        if (_world.Level is null)
        {
            return;
        }

        CancelPracticeNavigationWarmup();
        _practiceNavigationWarmupLevel = _world.Level;
        _practiceNavigationWarmupClasses = GetEligiblePracticeBotClassCycle().ToArray();
        _practiceNavigationWarmupPending = true;
        ShowLoadingOverlay(PracticeNavigationWarmupMessage);
    }

    private bool IsPracticeNavigationWarmupBlockingGameplay()
    {
        return _practiceNavigationWarmupPending
            || _practiceNavigationWarmupTask is not null;
    }

    private bool UpdatePracticeNavigationWarmup()
    {
        if (!_practiceNavigationWarmupPending && _practiceNavigationWarmupTask is null)
        {
            return false;
        }

        if (_practiceNavigationWarmupTask is null)
        {
            var level = _practiceNavigationWarmupLevel;
            var classes = _practiceNavigationWarmupClasses;
            if (level is null)
            {
                CancelPracticeNavigationWarmup();
                return false;
            }

            _practiceNavigationWarmupPending = false;
            _practiceNavigationWarmupTask = Task.Run(() => BuildPracticeNavigationWarmup(level, classes));
            return true;
        }

        var task = _practiceNavigationWarmupTask;
        if (!task.IsCompleted)
        {
            return true;
        }

        _practiceNavigationWarmupTask = null;
        var levelToWarm = _practiceNavigationWarmupLevel;
        _practiceNavigationWarmupLevel = null;
        _practiceNavigationWarmupClasses = [];

        PracticeNavigationWarmupResult result;
        try
        {
            result = task.GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            result = new PracticeNavigationWarmupResult(
                Success: false,
                Diagnostics: $" botbrain-warmup failed={exception.GetType().Name}: {exception.Message}");
        }

        // The graph/cache work is immutable and safe to build off-thread. The
        // combat spatial index belongs to SimulationWorld, so finish that part
        // on the game thread after the background task has completed.
        if (levelToWarm is not null && ReferenceEquals(_world.Level, levelToWarm))
        {
            _world.WarmCombatSpatialIndices();
        }

        AddConsoleLine(GetPracticeNavigationDiagnosticsSummary() + result.Diagnostics);
        HideLoadingOverlay();
        return false;
    }

    private void CancelPracticeNavigationWarmup()
    {
        var task = _practiceNavigationWarmupTask;
        _practiceNavigationWarmupTask = null;
        _practiceNavigationWarmupPending = false;
        _practiceNavigationWarmupLevel = null;
        _practiceNavigationWarmupClasses = [];

        // A canceled warmup can still be finishing on a worker thread. It has
        // no SimulationWorld writes, but observe a completed fault so it does
        // not become an unobserved task exception.
        if (task is { IsCompleted: true, IsFaulted: true })
        {
            _ = task.Exception;
        }

        if (string.Equals(_loadingOverlayMessage, PracticeNavigationWarmupMessage, StringComparison.Ordinal))
        {
            HideLoadingOverlay();
        }
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
        var alphaGraph = Og2NavigationGraphStore.GetOrBuild(_world.Level, out var resolution);
        var warmedAlphaPaths = alphaGraph.WarmAlphaObjectiveRoutes(_world.Level, GetEligiblePracticeBotClassCycle());
        _world.WarmCombatSpatialIndices();
        var tapeLoaded = BotBrainObjectiveTapeStore.TryLoad(_world.Level, out _);
        var proofGraphCount = WarmPracticeBotBrainProofGraphsForCurrentLevel();
        stopwatch.Stop();

        if (warmTrace)
        {
            Console.WriteLine(
                $"[botbrain] practice-warm-result paths={warmedAlphaPaths} " +
                $"cache={alphaGraph.AlphaPathCacheCount} elapsedMs={stopwatch.Elapsed.TotalMilliseconds:0.0} " +
                $"source={resolution.Source} sourcePath=\"{resolution.Path}\"");
        }

        return
            $" botbrain-warmup alphaNodes={alphaGraph.NodeCount} alphaPaths={warmedAlphaPaths} " +
            $"tape={tapeLoaded} proofgraphs={proofGraphCount} elapsed={stopwatch.Elapsed.TotalMilliseconds:0.0}ms " +
            $"source={resolution.Source} sourcePath=\"{resolution.Path}\"";
    }

    private static PracticeNavigationWarmupResult BuildPracticeNavigationWarmup(
        SimpleLevel level,
        IReadOnlyList<PlayerClass> eligibleClasses)
    {
        try
        {
            var warmTrace = Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_WARM_TRACE") is "1" or "true" or "TRUE";
            if (warmTrace)
            {
                Console.WriteLine($"[botbrain] practice-warm-entry level={level.Name} classes={eligibleClasses.Count}");
            }

            var stopwatch = Stopwatch.StartNew();
            var alphaGraph = Og2NavigationGraphStore.GetOrBuild(level, out var resolution);
            var warmedAlphaPaths = alphaGraph.WarmAlphaObjectiveRoutes(level, eligibleClasses);
            var tapeLoaded = BotBrainObjectiveTapeStore.TryLoad(level, out _);
            var proofGraphCount = WarmPracticeBotBrainProofGraphs(level, eligibleClasses);
            stopwatch.Stop();

            if (warmTrace)
            {
                Console.WriteLine(
                    $"[botbrain] practice-warm-result paths={warmedAlphaPaths} " +
                    $"cache={alphaGraph.AlphaPathCacheCount} elapsedMs={stopwatch.Elapsed.TotalMilliseconds:0.0} " +
                    $"source={resolution.Source} sourcePath=\"{resolution.Path}\"");
            }

            return new PracticeNavigationWarmupResult(
                Success: true,
                Diagnostics:
                    $" botbrain-warmup alphaNodes={alphaGraph.NodeCount} alphaPaths={warmedAlphaPaths} " +
                    $"tape={tapeLoaded} proofgraphs={proofGraphCount} elapsed={stopwatch.Elapsed.TotalMilliseconds:0.0}ms " +
                    $"source={resolution.Source} sourcePath=\"{resolution.Path}\"");
        }
        catch (Exception exception)
        {
            return new PracticeNavigationWarmupResult(
                Success: false,
                Diagnostics: $" botbrain-warmup failed={exception.GetType().Name}: {exception.Message}");
        }
    }

    private int WarmPracticeBotBrainProofGraphsForCurrentLevel()
    {
        if (_world.Level is null)
        {
            return 0;
        }

        return WarmPracticeBotBrainProofGraphs(_world.Level, GetEligiblePracticeBotClassCycle());
    }

    private static int WarmPracticeBotBrainProofGraphs(
        SimpleLevel level,
        IReadOnlyList<PlayerClass> eligibleClasses)
    {
        var loadedCount = 0;
        Span<PlayerTeam> teams = [PlayerTeam.Red, PlayerTeam.Blue];
        foreach (var team in teams)
        {
            foreach (var classId in eligibleClasses)
            {
                if (VerifiedNavProofGraphAssetStore.TryLoad(level, team, classId, out _))
                {
                    loadedCount += 1;
                }
            }
        }

        return loadedCount;
    }
}
