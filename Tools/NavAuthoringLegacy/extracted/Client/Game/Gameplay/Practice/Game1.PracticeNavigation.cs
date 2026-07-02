#nullable enable

namespace OpenGarrison.Client;

public partial class Game1
{
    private static void ResetPracticeNavigationState()
    {
    }

    private void LoadPracticeNavigationAssetsForCurrentLevel()
    {
        AddConsoleLine(GetPracticeNavigationDiagnosticsSummary());
    }

    private static string GetPracticeNavigationDiagnosticsSummary()
    {
        return "nav clientbot-navpoints";
    }
}
