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
        var currentLevelName = _world.Level.Name;
        var currentMapAreaIndex = _world.Level.MapAreaIndex;
        if (_observedGameplayMapAreaIndex < 0 || string.IsNullOrWhiteSpace(_observedGameplayLevelName))
        {
            _observedGameplayLevelName = currentLevelName;
            _observedGameplayMapAreaIndex = currentMapAreaIndex;
            return;
        }

        if (string.Equals(_observedGameplayLevelName, currentLevelName, StringComparison.OrdinalIgnoreCase)
            && _observedGameplayMapAreaIndex == currentMapAreaIndex)
        {
            return;
        }

        ResetGameplayTransitionEffects();
        _wasDeathCamActive = false;
        _wasMatchEnded = false;
        if (_navEditorEnabled)
        {
            DisableNavEditor("nav editor closed after map change");
        }

        _observedGameplayLevelName = currentLevelName;
        _observedGameplayMapAreaIndex = currentMapAreaIndex;
    }

    private void UpdateGameplayPresentation(GameTime gameTime, KeyboardState keyboard, MouseState mouse, int clientTicks)
    {
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
    }

    private void UpdateGameplayWindowState()
    {
        var wantsMouseVisible = ShouldShowGameplayMouseCursor();
        IsMouseVisible = wantsMouseVisible && !ShouldUseSoftwareMenuCursor();

        var sourceTag = _world.Level.ImportedFromSource ? "src" : "fallback";
        var lifeTag = _world.LocalPlayerAwaitingJoin ? "joining" : _world.LocalPlayer.IsAlive ? "alive" : $"respawn:{_world.LocalPlayerRespawnTicks}";
        var remoteLifeTag = _networkClient.IsConnected
            ? $"remotes:{_world.RemoteSnapshotPlayers.Count}"
            : _world.EnemyPlayerEnabled
                ? "offline:npc:on"
                : "offline:npc:off";
        var carryingIntelTag = _world.LocalPlayer.IsCarryingIntel ? "yes" : "no";
        var heavyTag = GetPlayerIsHeavyEating(_world.LocalPlayer)
            ? $" eat:{GetPlayerHeavyEatTicksRemaining(_world.LocalPlayer)}"
            : string.Empty;
        var sniperTag = GetPlayerIsSniperScoped(_world.LocalPlayer)
            ? $" scope:{GetPlayerSniperRifleDamage(_world.LocalPlayer)}"
            : string.Empty;
        var consoleTag = _consoleOpen ? " console:open" : string.Empty;
        var netTag = _networkClient.IsConnected ? $" net:{_networkClient.ServerDescription}" : string.Empty;
        Window.Title = $"OpenGarrison.Client - {_world.LocalPlayer.DisplayName} ({_world.LocalPlayer.ClassName}) - {_world.Level.Name} [{sourceTag}] - {lifeTag} - HP {_world.LocalPlayer.Health}/{_world.LocalPlayer.MaxHealth} - Ammo {_world.LocalPlayer.CurrentShells}/{_world.LocalPlayer.MaxShells} - {remoteLifeTag} - Caps {_world.RedCaps}:{_world.BlueCaps} - Carrying {carryingIntelTag} - BlueIntel {(GetIntelStateLabel(_world.BlueIntel))} - Frame {_world.Frame} - Pos ({_world.LocalPlayer.X:F1}, {_world.LocalPlayer.Y:F1}) - AirJumps {_world.LocalPlayer.RemainingAirJumps}{heavyTag}{sniperTag}{consoleTag}{netTag}";
    }
}
