#nullable enable

namespace OpenGarrison.Client;

public partial class Game1
{
    private void ResetPracticeNavigationState()
    {
    }

    private void LoadPracticeNavigationAssetsForCurrentLevel()
    {
        AddConsoleLine(GetPracticeNavigationDiagnosticsSummary());
    }

    private string GetPracticeNavigationDiagnosticsSummary()
    {
        return "nav clientbot-navpoints";
    }
}
