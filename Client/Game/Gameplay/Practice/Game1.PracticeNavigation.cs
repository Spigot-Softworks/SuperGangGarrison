#nullable enable

using OpenGarrison.BotAI;

namespace OpenGarrison.Client;

public partial class Game1
{
    private BotNavigationLoadResult _practiceNavigationAssets = BotNavigationLoadResult.Empty;

    private void ResetPracticeNavigationState()
    {
        _practiceNavigationAssets = BotNavigationLoadResult.Empty;
    }

    private void LoadPracticeNavigationAssetsForCurrentLevel()
    {
        _practiceNavigationAssets = BotNavigationAssetStore.LoadForLevel(
            _world.Level,
            useModernRuntimeGeneration: true,
            allowSynchronousGeneration: true,
            preferFreshModernGeneration: false);
        AddConsoleLine(GetPracticeNavigationDiagnosticsSummary());
        foreach (var status in _practiceNavigationAssets.Statuses.Where(static status => status.IsLoaded && !status.IsStructurallyValid))
        {
            AddConsoleLine($"nav {BotNavigationClasses.GetShortLabel(status.ClassId)} invalid: {status.StructuralMessage}");
        }
    }

    private string GetPracticeNavigationDiagnosticsSummary()
    {
        if (!IsPracticeSessionActive && _practiceNavigationAssets.Statuses.Count == 0)
        {
            return "nav inactive";
        }

        if (_practiceNavigationAssets.Statuses.Count == 0)
        {
            return "nav not loaded";
        }

        var tokens = _practiceNavigationAssets.Statuses
            .Select(status => status.IsLoaded
                ? status.IsStructurallyValid
                    ? $"{BotNavigationClasses.GetShortLabel(status.ClassId)}:{status.NodeCount}/{status.EdgeCount}:{GetNavigationSourceLabel(status.Source)}"
                    : $"{BotNavigationClasses.GetShortLabel(status.ClassId)}:{status.NodeCount}/{status.EdgeCount}:{GetNavigationSourceLabel(status.Source)}:invalid"
                : $"{BotNavigationClasses.GetShortLabel(status.ClassId)}:missing")
            .ToArray();
        return $"{_practiceNavigationAssets.BuildSummary()} [{string.Join(" ", tokens)}]";
    }

    private static string GetNavigationSourceLabel(BotNavigationAssetSource source)
    {
        return source switch
        {
            BotNavigationAssetSource.GeneratedAtRuntime => "gen",
            BotNavigationAssetSource.RuntimeCache => "cache",
            BotNavigationAssetSource.ShippedContent => "ship",
            _ => "none",
        };
    }
}
