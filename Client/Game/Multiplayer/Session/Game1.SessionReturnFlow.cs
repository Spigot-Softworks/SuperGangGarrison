#nullable enable

using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void ReturnToMainMenu(string? statusMessage = null)
    {
        ResetActiveSessionState();
        ResetToMainMenuState(statusMessage);
    }

    private void ResetActiveSessionState()
    {
        ResetPracticeBotManagerState(releaseWorldSlots: true);
        ResetPracticeNavigationState();
        _networkClient.Disconnect();
        ResetGameplayTransitionEffects();
        ReinitializeSimulationForTickRate(SimulationConfig.DefaultTicksPerSecond);
        ResetGameplayRuntimeState();
        StopHostedServer();
        ResetSpectatorTracking(enableTracking: false);
        ResetLastToDieState();
    }
}
