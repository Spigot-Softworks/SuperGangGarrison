#nullable enable

using Microsoft.Xna.Framework;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class GameplayResetController
    {
        private readonly Game1 _game;

        public GameplayResetController(Game1 game)
        {
            _game = game;
        }

        public void ResetGameplayRuntimeState()
        {
            _game.ResetClientTimingState();
            _game._lastAppliedSnapshotFrame = 0;
            _game._lastBufferedSnapshotFrame = 0;
            _game._hasReceivedSnapshot = false;
            _game._lastSnapshotReceivedTimeSeconds = -1d;
            _game._latestSnapshotServerTimeSeconds = -1d;
            _game._latestSnapshotReceivedClockSeconds = -1d;
            _game._networkSnapshotInterpolationDurationSeconds = 1f / _game._config.TicksPerSecond;
            _game._smoothedSnapshotIntervalSeconds = 1f / _game._config.TicksPerSecond;
            _game._smoothedSnapshotJitterSeconds = 0f;
            _game._remotePlayerInterpolationBackTimeSeconds = RemotePlayerMinimumInterpolationBackTimeSeconds;
            _game._remotePlayerRenderTimeSeconds = 0d;
            _game._lastRemotePlayerRenderTimeClockSeconds = -1d;
            _game._hasRemotePlayerRenderTime = false;
            _game._pendingNetworkVisualEvents.Clear();
            _game._pendingNetworkDamageEvents.Clear();
            _game.ResetBackstabVisuals();
            _game._hasPredictedLocalPlayerPosition = false;
            _game._hasSmoothedLocalPlayerRenderPosition = false;
            _game._predictedLocalPlayerRenderCorrectionOffset = Vector2.Zero;
            _game._lastPredictedRenderSmoothingTimeSeconds = -1d;
            _game._pendingPredictedInputs.Clear();
            _game._localPlayerSnapshotEntityId = null;
            _game._entityInterpolationTracks.Clear();
            _game._intelInterpolationTracks.Clear();
            _game._entitySnapshotHistories.Clear();
            _game._intelSnapshotHistories.Clear();
            _game._remotePlayerSnapshotHistories.Clear();
            _game.ResetSnapshotStateHistory();
            _game._interpolatedEntityPositions.Clear();
            _game._interpolatedIntelPositions.Clear();
        }

        public void ResetGameplayTransitionEffects()
        {
            _game.StopLocalRapidFireWeaponAudio();
            _game.StopIngameMusic();
            _game.ResetTransientPresentationEffects();
            _game.ResetProcessedNetworkEventHistory();
        }
    }
}
