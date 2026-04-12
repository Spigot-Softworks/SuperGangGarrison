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
        var browserPresentationStartTimestamp = OperatingSystem.IsBrowser() ? Stopwatch.GetTimestamp() : 0L;
        var interpolationStartTimestamp = _networkDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L;
        UpdateInterpolatedWorldState();
        if (_networkDiagnosticsEnabled)
        {
            RecordInterpolationDuration(GetDiagnosticsElapsedMilliseconds(interpolationStartTimestamp));
        }

        HandleGameplayMapTransitionIfNeeded();
        UpdateLocalSentryNotice();
        UpdateIntelNotice();
        UpdateLocalPredictedRenderPosition();
        foreach (var player in EnumerateRenderablePlayers())
        {
            UpdatePlayerRenderState(player);
        }

        RemoveStalePlayerRenderState();
        AdvanceGameplayClientTicks(clientTicks);
        PlayPendingVisualEvents();
        PlayPendingSoundEvents();
        DispatchPendingDamageEventsToPlugins();
        QueuePendingExperimentalHealingHudIndicators();
        UpdateLocalRapidFireWeaponAudio();
        PlayDemoknightChargeReadySoundIfNeeded();
        PlayDeathCamSoundIfNeeded();
        PlayRoundEndSoundIfNeeded();
        PlayKillFeedAnnouncementSounds();
        EnsureIngameMusicPlaying();
        UpdateLastToDieSession(clientTicks);
        UpdateLastToDieCombatFeedbackPresentation();
        UpdateTeamSelect(keyboard, mouse);
        UpdateClassSelect(mouse);
        RecordBrowserPresentationDuration(browserPresentationStartTimestamp);
    }

    private void UpdateGameplayWindowState()
    {
        _gameplayPresentationStateController.UpdateGameplayWindowState();
    }
}
