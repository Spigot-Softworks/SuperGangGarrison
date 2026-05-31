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
        UpdatePostGameMvpWinScreenState(keyboard, clientTicks);
        PlayPendingVisualEvents();
        PlayPendingSoundEvents();
        DispatchPendingDamageEventsToPlugins();
        QueuePendingExperimentalHealingHudIndicators();
        UpdateLocalRapidFireWeaponAudio();
        PlayDemoknightChargeReadySoundIfNeeded();
        PlayDeathCamSoundIfNeeded();
        PlayRoundEndSoundIfNeeded();
        PlayKillFeedAnnouncementSounds();
        var musicStartTimestamp = IsClientPerformanceDiagnosticsEnabled() ? Stopwatch.GetTimestamp() : 0L;
        EnsureIngameMusicPlaying();
        if (musicStartTimestamp > 0)
        {
            RecordClientPerformanceMetric(ClientPerformanceMetric.Music, GetDiagnosticsElapsedMilliseconds(musicStartTimestamp));
        }
        UpdateLastToDieSession(clientTicks);
        UpdateLastToDieCombatFeedbackPresentation();
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
