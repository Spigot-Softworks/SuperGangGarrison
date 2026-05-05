#nullable enable

using Microsoft.Xna.Framework;
using OpenGarrison.Core;
using System.Diagnostics;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const int BrowserOfflineSimulationMaxCatchUpTicks = 2;
    private const double BrowserOfflineSimulationMaxElapsedSeconds = 0.08d;

    private void AdvanceGameplaySimulation(GameTime gameTime, PlayerInputSnapshot networkInput)
    {
        var browserSimulationStartTimestamp = ShouldMeasureClientPerformanceDurations() ? Stopwatch.GetTimestamp() : 0L;
        var simulationTickCount = 0;
        if (_networkClient.IsConnected)
        {
            AdvanceNetworkInputLane(networkInput);
        }
        else
        {
            if (ShouldSuspendOfflineGameplaySimulation())
            {
                RecordBrowserSimulationDuration(browserSimulationStartTimestamp, simulationTickCount);
                return;
            }

            BeginBotDiagnosticsFrame(gameTime);
            var elapsedSeconds = gameTime.ElapsedGameTime.TotalSeconds;
            if (OperatingSystem.IsBrowser())
            {
                elapsedSeconds = Math.Min(elapsedSeconds, BrowserOfflineSimulationMaxElapsedSeconds);
                simulationTickCount = _simulator.Step(
                    elapsedSeconds,
                    OnPracticeSimulationBeforeTick,
                    OnPracticeSimulationAfterTick,
                    BrowserOfflineSimulationMaxCatchUpTicks);
            }
            else
            {
                simulationTickCount = _simulator.Step(
                    elapsedSeconds,
                    OnPracticeSimulationBeforeTick,
                    OnPracticeSimulationAfterTick);
            }

            FinalizeBotDiagnosticsFrame();
        }

        RecordBrowserSimulationDuration(browserSimulationStartTimestamp, simulationTickCount);
    }

    private void OnPracticeSimulationBeforeTick()
    {
        UpdatePracticeBots();
        OnNavEditorTraversalCaptureBeforeTick();
        OnScoreRouteRecorderBeforeTick();
    }

    private void OnPracticeSimulationAfterTick()
    {
        OnNavEditorTraversalCaptureAfterTick();
        OnScoreRouteRecorderAfterTick();
        if (IsPracticeSessionActive)
        {
            _practiceSessionElapsedTicks += 1;
        }

        AdvanceLastToDieSimulationTick();
        UpdateLastToDieBotReactions();
    }
}
