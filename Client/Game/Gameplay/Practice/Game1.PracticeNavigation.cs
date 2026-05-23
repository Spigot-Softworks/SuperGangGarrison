#nullable enable

using OpenGarrison.Core.BotBrain;
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
        stopwatch.Stop();

        return $" botbrain-warmup graph={graphLoaded} tape={tapeLoaded} elapsed={stopwatch.Elapsed.TotalMilliseconds:0.0}ms";
    }
}
