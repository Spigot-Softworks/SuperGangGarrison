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
            // Stage/map presentation resets keep the membership for this connection.
            // EnsureVoiceChat resets it when the actual network connection changes.
            if (_context._networkClient.IsConnected) _context._voiceChat?.SuspendCapture();
            else _context.ResetVoiceChat();
            _context.CancelPracticeNavigationWarmup();
            _context.ResetClientTimingState();
            _context._lastAppliedSnapshotFrame = 0;
            _context._lastBufferedSnapshotFrame = 0;
            _context._hasReceivedSnapshot = false;
            _context._lastSnapshotReceivedTimeSeconds = -1d;
            _context._latestSnapshotServerTimeSeconds = -1d;
            _context._latestSnapshotReceivedClockSeconds = -1d;
            _context._networkSnapshotInterpolationDurationSeconds = 1f / _context._config.TicksPerSecond;
            _context._smoothedSnapshotIntervalSeconds = 1f / _context._config.TicksPerSecond;
            _context._smoothedSnapshotJitterSeconds = 0f;
            _context._localPlayerInterpolationBackTimeSeconds = _context.GetMinimumLocalPlayerInterpolationBackTimeSeconds();
            _context._remotePlayerInterpolationBackTimeSeconds = _context.GetMinimumRemotePlayerInterpolationBackTimeSeconds();
            _context._projectileInterpolationBackTimeSeconds = ProjectileMinimumInterpolationBackTimeSeconds;
            _context._networkInterpolationWarmupSnapshotsRemaining = 0;
            _context._networkInterpolationWarmupUntilClockSeconds = -1d;
            _context._localPlayerRenderTimeSeconds = 0d;
            _context._remotePlayerRenderTimeSeconds = 0d;
            _context._lastLocalPlayerRenderTimeClockSeconds = -1d;
            _context._lastRemotePlayerRenderTimeClockSeconds = -1d;
            _context._hasLocalPlayerRenderTime = false;
            _context._hasRemotePlayerRenderTime = false;
            _context._pendingNetworkSoundEvents.Clear();
            _context._pendingNetworkVisualEvents.Clear();
            _context._pendingNetworkDamageEvents.Clear();
            _context._authoritativeExplosionPresentations.Clear();
            _context._gameplayAccountSessionTask = null;
            _context._pendingGameplayAccountAttachRequestId = 0;
            _context._gameplayAccountTokenExpiresAt = DateTimeOffset.MinValue;
            _context._nextGameplayAccountAttachAttemptAt = DateTimeOffset.MinValue;
            _context.ResetBuffBannerReadySoundObservation();
            _context.ResetVotePresentation();
            _context.ResetHealingCharacterEffects();
            _context.ResetBackstabVisuals();
            _context._hasPredictedLocalPlayerPosition = false;
            _context._hasSmoothedLocalPlayerRenderPosition = false;
            _context._hasPredictedLocalActionState = false;
            _context._serverLocalPredictionEnabled = false;
            _context._predictedLocalPlayerShadow = null;
            _context._predictedLocalPlayerRenderCorrectionOffset = Vector2.Zero;
            _context._lastPredictedRenderSmoothingTimeSeconds = -1d;
            _context.ResetSmoothCameraState();
            _context.ResetCameraPanningState();
            _context._pendingPredictedInputs.Clear();
            _context._localPlayerSnapshotEntityId = null;
            _context._lastAppliedSnapshotLocalPlayerId = null;
            _context._networkWorldWarmupActive = false;
            _context._networkWorldWarmupFullSnapshotApplied = false;
            _context._networkWorldWarmupAppliedSnapshotsAfterFull = 0;
            _context._networkWorldWarmupAcceptNextAppliedSnapshotAsBaseline = false;
            _context._networkPresentationObservedLastToDiePhase = null;
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
