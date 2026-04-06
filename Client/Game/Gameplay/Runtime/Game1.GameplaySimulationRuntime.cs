#nullable enable

using Microsoft.Xna.Framework;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void AdvanceGameplaySimulation(GameTime gameTime, PlayerInputSnapshot networkInput)
    {
        if (_networkClient.IsConnected)
        {
            AdvanceNetworkInputLane(networkInput);
        }
        else
        {
            if (ShouldSuspendOfflineGameplaySimulation())
            {
                return;
            }

            BeginBotDiagnosticsFrame(gameTime);
            _simulator.Step(
                gameTime.ElapsedGameTime.TotalSeconds,
                OnPracticeSimulationBeforeTick,
                OnPracticeSimulationAfterTick);
            FinalizeBotDiagnosticsFrame();
        }
    }

    private void OnPracticeSimulationBeforeTick()
    {
        UpdatePracticeBots();
        OnNavEditorTraversalCaptureBeforeTick();
    }

    private void OnPracticeSimulationAfterTick()
    {
        OnNavEditorTraversalCaptureAfterTick();
        AdvanceLastToDieSimulationTick();
        UpdateLastToDieBotReactions();
    }
}
