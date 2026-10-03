#nullable enable

using OpenGarrison.Core.BotBrain;
using OpenGarrison.Core;
using System.Diagnostics;
using System.Threading.Tasks;

namespace OpenGarrison.Client;

public partial class Game1
{
    public const string PracticeNavigationWarmupMessage = "Loading...";

    public bool _practiceNavigationWarmupPending;
    public bool _practiceNavigationWarmupPresentationPending;
    public Task<PracticeNavigationWarmupResult>? _practiceNavigationWarmupTask;
    public SimpleLevel? _practiceNavigationWarmupLevel;
    private PlayerClass[] _practiceNavigationWarmupClasses = [];

    public sealed record PracticeNavigationWarmupResult(
        bool Success,
        string Diagnostics);

    public static void ResetPracticeNavigationState()
    {
    }

    public void QueuePracticeNavigationWarmupForCurrentLevel()
    {
        if (_world.Level is null)
        {
            return;
        }

        CancelPracticeNavigationWarmup();
        _practiceNavigationWarmupLevel = _world.Level;
        _practiceNavigationWarmupClasses = GetEligiblePracticeBotClassCycle().ToArray();
        _practiceNavigationWarmupPending = true;
        _practiceNavigationWarmupPresentationPending = OperatingSystem.IsBrowser();
        ShowLoadingOverlay(PracticeNavigationWarmupMessage);
    }

    public bool IsPracticeNavigationWarmupBlockingGameplay()
    {
        return _practiceNavigationWarmupPending
            || _practiceNavigationWarmupTask is not null;
    }

    public bool UpdatePracticeNavigationWarmup()
    {
        if (!_practiceNavigationWarmupPending && _practiceNavigationWarmupTask is null)
        {
            return false;
        }

        if (_practiceNavigationWarmupTask is null)
        {
            // A browser Task.Run still shares the UI thread. Present the bar
            // before scheduling graph construction so its compositor animation
            // has a frame to start before that work blocks managed rendering.
            if (_practiceNavigationWarmupPresentationPending) return true;
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
            _world.GeometryResolver.WarmSpatialIndices();
        }

        AddConsoleLine(GetPracticeNavigationDiagnosticsSummary() + result.Diagnostics);
        HideLoadingOverlay();
        if (!result.Success && OperatingSystem.IsBrowser())
        {
            ReturnToMainMenu();
            OpenPracticeSetupMenu();
            SetPersistedMenuStatusMessage("Practice navigation could not load. Refresh the game and try again.");
        }
        return false;
    }

    public void CancelPracticeNavigationWarmup()
    {
        var task = _practiceNavigationWarmupTask;
        _practiceNavigationWarmupTask = null;
        _practiceNavigationWarmupPending = false;
        _practiceNavigationWarmupPresentationPending = false;
        _practiceNavigationWarmupLevel = null;
        _practiceNavigationWarmupClasses = [];

        // A canceled warmup can still be finishing on a worker thread. It has
        // no SimulationWorld writes, but observe a completed fault so it does
        // not become an unobserved task exception.
        if (task is { IsCompleted: true, IsFaulted: true })
        {
            _ = task.Exception;
        }

        if (string.Equals(_loadingOverlayState.Message, PracticeNavigationWarmupMessage, StringComparison.Ordinal))
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
        var provider = new NavigationGraphProvider();
        var alphaGraph = provider.PreloadGraph(_world.Level);
        var warmedAlphaPaths = alphaGraph.WarmAlphaObjectiveRoutes(_world.Level, GetEligiblePracticeBotClassCycle());
        _world.GeometryResolver.WarmSpatialIndices();
        stopwatch.Stop();

        if (warmTrace)
        {
            Console.WriteLine(
                $"[botbrain] practice-warm-result paths={warmedAlphaPaths} " +
                $"cache={alphaGraph.AlphaPathCacheCount} elapsedMs={stopwatch.Elapsed.TotalMilliseconds:0.0} " +
                $"source={provider.LastPreloadSource} sourcePath=\"{provider.LastSourcePath}\"");
        }

        return
            $" botbrain-warmup alphaNodes={alphaGraph.NodeCount} alphaPaths={warmedAlphaPaths} " +
            $"elapsed={stopwatch.Elapsed.TotalMilliseconds:0.0}ms " +
            $"source={provider.LastPreloadSource} sourcePath=\"{provider.LastSourcePath}\"";
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
            var provider = new NavigationGraphProvider();
            var alphaGraph = provider.PreloadGraph(level);
            var warmedAlphaPaths = alphaGraph.WarmAlphaObjectiveRoutes(level, eligibleClasses);
            stopwatch.Stop();

            if (warmTrace)
            {
                Console.WriteLine(
                    $"[botbrain] practice-warm-result paths={warmedAlphaPaths} " +
                    $"cache={alphaGraph.AlphaPathCacheCount} elapsedMs={stopwatch.Elapsed.TotalMilliseconds:0.0} " +
                    $"source={provider.LastPreloadSource} sourcePath=\"{provider.LastSourcePath}\"");
            }

            return new PracticeNavigationWarmupResult(
                Success: true,
                Diagnostics:
                    $" botbrain-warmup alphaNodes={alphaGraph.NodeCount} alphaPaths={warmedAlphaPaths} " +
                    $"elapsed={stopwatch.Elapsed.TotalMilliseconds:0.0}ms " +
                    $"source={provider.LastPreloadSource} sourcePath=\"{provider.LastSourcePath}\"");
        }
        catch (Exception exception)
        {
            return new PracticeNavigationWarmupResult(
                Success: false,
                Diagnostics: $" botbrain-warmup failed={exception.GetType().Name}: {exception.Message}");
        }
    }
}
