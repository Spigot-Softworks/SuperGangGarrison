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
        _practiceNavigationAssets = BotNavigationAssetStore.LoadForLevel(_world.Level);
        AddConsoleLine(GetPracticeNavigationDiagnosticsSummary());
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
                ? $"{BotNavigationProfiles.GetFileToken(status.Profile)}:{status.NodeCount}/{status.EdgeCount}"
                : $"{BotNavigationProfiles.GetFileToken(status.Profile)}:missing")
            .ToArray();
        return $"{_practiceNavigationAssets.BuildSummary()} [{string.Join(" ", tokens)}]";
    }
}
