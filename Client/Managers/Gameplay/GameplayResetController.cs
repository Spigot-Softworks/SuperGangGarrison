#nullable enable

using Microsoft.Xna.Framework;

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class GameplayResetController
    {
        private readonly IGameplayContext _context;

        public GameplayResetController(IGameplayContext context)
        {
            _context = context;
        }

        public void ResetGameplayRuntimeState()
        {
            _context.StopLocalRapidFireWeaponAudio();
            // Stage/map presentation resets keep the membership for this connection.
            // EnsureVoiceChat resets it when the actual network connection changes.
            if (_context._networkClient.IsConnected) _context._voiceChat?.SuspendCapture();
            else _context.ResetVoiceChat();
            _context.CancelPracticeNavigationWarmup();
            _context.ResetClientTimingState();
            _context.NetworkPresentation.LastAppliedSnapshotFrame = 0;
            _context.NetworkPresentation.LastBufferedSnapshotFrame = 0;
            _context.NetworkPresentation.HasReceivedSnapshot = false;
            _context.NetworkPresentation.LastSnapshotReceivedTimeSeconds = -1d;
            _context.NetworkPresentation.LatestSnapshotServerTimeSeconds = -1d;
            _context.NetworkPresentation.LatestSnapshotReceivedClockSeconds = -1d;
            _context.NetworkPresentation.HasFilteredServerClockOffset = false;
            _context.NetworkPresentation.FilteredServerClockOffsetSampleServerTimeSeconds = -1d;
            _context.NetworkPresentation.NetworkSnapshotInterpolationDurationSeconds = 1f / _context._config.TicksPerSecond;
            _context.NetworkPresentation.SmoothedSnapshotIntervalSeconds = 1f / _context._config.TicksPerSecond;
            _context.NetworkPresentation.SmoothedSnapshotJitterSeconds = 0f;
            _context.NetworkPresentation.LocalPlayerInterpolationBackTimeSeconds = _context.GetMinimumLocalPlayerInterpolationBackTimeSeconds();
            _context.NetworkPresentation.RemotePlayerInterpolationBackTimeSeconds = _context.GetMinimumRemotePlayerInterpolationBackTimeSeconds();
            _context.NetworkPresentation.ProjectileInterpolationBackTimeSeconds = ProjectileMinimumInterpolationBackTimeSeconds;
            _context.NetworkPresentation.NetworkInterpolationWarmupSnapshotsRemaining = 0;
            _context.NetworkPresentation.NetworkInterpolationWarmupUntilClockSeconds = -1d;
            _context.NetworkPresentation.LocalPlayerRenderTimeSeconds = 0d;
            _context.NetworkPresentation.RemotePlayerRenderTimeSeconds = 0d;
            _context.NetworkPresentation.LastLocalPlayerRenderTimeClockSeconds = -1d;
            _context.NetworkPresentation.LastRemotePlayerRenderTimeClockSeconds = -1d;
            _context.NetworkPresentation.HasLocalPlayerRenderTime = false;
            _context.NetworkPresentation.HasRemotePlayerRenderTime = false;
            _context.ClearPendingNetworkSoundEvents();
            _context.ClearPendingNetworkVisualEvents();
            _context.ClearPendingNetworkDamageEvents();
            _context._authoritativeExplosionPresentations.Clear();
            _context._gameplayAccountSessionTask = null;
            _context._pendingGameplayAccountAttachRequestId = 0;
            _context._gameplayAccountTokenExpiresAt = DateTimeOffset.MinValue;
            _context._nextGameplayAccountAttachAttemptAt = DateTimeOffset.MinValue;
            _context.ResetBuffBannerReadySoundObservation();
            _context.ResetVotePresentation();
            _context.ResetHealingCharacterEffects();
            _context.ResetBackstabVisuals();
            _context.LocalPrediction.HasPredictedLocalPlayerPosition = false;
            _context.LocalPrediction.HasPredictedLocalPlayerTickStartPosition = false;
            _context.LocalPrediction.HasSmoothedLocalPlayerRenderPosition = false;
            _context.LocalPrediction.HasPredictedLocalActionState = false;
            _context.LocalPrediction.ServerLocalPredictionEnabled = false;
            _context.LocalPrediction.PredictedLocalPlayerShadow = null;
            _context.LocalPrediction.PredictedLocalPlayerRenderCorrectionOffset = Vector2.Zero;
            _context.LocalPrediction.LastPredictedRenderSmoothingTimeSeconds = -1d;
            _context.ResetSmoothCameraState();
            _context.ResetCameraPanningState();
            _context.LocalPrediction.PendingPredictedInputs.Clear();
            _context._localPlayerSnapshotEntityId = null;
            _context.NetworkPresentation.LastAppliedSnapshotLocalPlayerId = null;
            _context.NetworkPresentation.NetworkWorldWarmupActive = false;
            _context.NetworkPresentation.NetworkWorldWarmupFullSnapshotApplied = false;
            _context.NetworkPresentation.NetworkWorldWarmupAppliedSnapshotsAfterFull = 0;
            _context.NetworkPresentation.NetworkWorldWarmupAcceptNextAppliedSnapshotAsBaseline = false;
            _context.NetworkPresentation.NetworkPresentationObservedLastToDiePhase = null;
            _context.ResetSnapshotPresentationHistories();
            _context.ResetCivviePogoTrickPresentationObservation();
            _context._localOverheadChatMessage = null;
            _context._overheadChatMessagesBySlot.Clear();
            _context.ClearRemoteCustomBubbleStates();
            _context.ResetSnapshotStateHistory();
        }

        public void ResetGameplayTransitionEffects()
        {
            _context.StopLocalRapidFireWeaponAudio();
            _context.StopIngameMusic();
            _context.StopMenuMusic();
            _context.StopLastToDieMenuMusic();
            _context.StopLastToDieIngameMusic();
            _context.StopLastToDieGameOverSound();
            _context.ResetTransientPresentationEffects();
            _context.ResetProcessedNetworkEventHistory();
            _context.ResetBuffBannerReadySoundObservation();
        }
}
