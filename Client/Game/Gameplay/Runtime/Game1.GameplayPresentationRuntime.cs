#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Diagnostics;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void HandleGameplayMapTransitionIfNeeded()
    {
        _gameplayPresentationStateController.HandleGameplayMapTransitionIfNeeded();
    }

    private void UpdateGameplayPresentation(GameTime gameTime, KeyboardState keyboard, MouseState mouse, int clientTicks)
    {
        var browserPresentationStartTimestamp = ShouldMeasureClientPerformanceDurations() ? Stopwatch.GetTimestamp() : 0L;
        var interpolationStartTimestamp = (_networkDiagnosticsEnabled || IsClientPerformanceDiagnosticsEnabled()) ? Stopwatch.GetTimestamp() : 0L;
        UpdateInterpolatedWorldState();
        if (_networkDiagnosticsEnabled)
        {
            RecordInterpolationDuration(GetDiagnosticsElapsedMilliseconds(interpolationStartTimestamp));
        }

        if (interpolationStartTimestamp > 0)
        {
            RecordClientPerformanceMetric(ClientPerformanceMetric.Interpolation, GetDiagnosticsElapsedMilliseconds(interpolationStartTimestamp));
        }

        HandleGameplayMapTransitionIfNeeded();
        UpdateLocalSentryNotice();
        UpdateIntelNotice();
        UpdateLocalPredictedRenderPosition();
        var renderStateStartTimestamp = IsClientPerformanceDiagnosticsEnabled() ? Stopwatch.GetTimestamp() : 0L;
        foreach (var player in EnumerateRenderablePlayers())
        {
            UpdatePlayerRenderState(player);
        }
        if (renderStateStartTimestamp > 0)
        {
            RecordClientPerformanceMetric(ClientPerformanceMetric.RenderStates, GetDiagnosticsElapsedMilliseconds(renderStateStartTimestamp));
        }

        RemoveStalePlayerRenderState();
        AdvanceGameplayClientTicks(clientTicks);
        UpdateVotePresentation(clientTicks);
        UpdateVipPresentation(clientTicks);
        UpdatePostGameMvpWinScreenState(keyboard, clientTicks);
        PlayPendingCivvieMoneyTrailSpawns();
        ObserveCivvieUmbrellaShieldBlocksFromPlayerState();
        ObserveCivviePogoTrickPresentationFromPlayerState();
        PlayPendingVisualEvents();
        PlayPendingSoundEvents();
        ObservePlayerHealthChangesForHealingCharacterEffects();
        DispatchPendingDamageEventsToPlugins();
        QueuePendingExperimentalHealingHudIndicators();
        AdvanceHealingCharacterEffects((float)gameTime.ElapsedGameTime.TotalSeconds);
        UpdateLocalRapidFireWeaponAudio();
        PlayDemoknightChargeReadySoundIfNeeded();
        PlayDeathCamSoundIfNeeded();
        PlayRoundEndSoundIfNeeded();
        PlayKillFeedAnnouncementSounds();
        var musicStartTimestamp = IsClientPerformanceDiagnosticsEnabled() ? Stopwatch.GetTimestamp() : 0L;
        EnsureIngameMusicPlaying();
        UpdateDynamicMusic(gameTime, clientTicks);
        if (musicStartTimestamp > 0)
        {
            RecordClientPerformanceMetric(ClientPerformanceMetric.Music, GetDiagnosticsElapsedMilliseconds(musicStartTimestamp));
        }
        UpdateLastToDieSession(clientTicks);
        UpdateLastToDieCombatFeedbackPresentation();
        UpdateEvasionMissPopups();
        UpdateHeavyDashDodgePopup();
        UpdateTeamSelect(keyboard, mouse);
        UpdateClassSelect(mouse);
        RecordBrowserPresentationDuration(browserPresentationStartTimestamp);
    }

    private void UpdateGameplayWindowState()
    {
        _gameplayPresentationStateController.UpdateGameplayWindowState();
    }
}
