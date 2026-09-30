#nullable enable

using System;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private bool _pendingMapTeamSelection;

    private void UpdatePendingMapTeamSelection()
    {
        if (!_pendingMapTeamSelection) return;
        if (!_networkClient.IsConnected || _networkClient.IsSpectator || !_world.LocalPlayerAwaitingJoin)
        {
            _pendingMapTeamSelection = false;
            return;
        }
        if (_world.MatchState.IsEnded || !ShouldOpenDeferredMapTeamSelection(
                _networkWorldWarmupActive,
                _networkWorldWarmupFullSnapshotApplied,
                _networkWorldWarmupAppliedSnapshotsAfterFull,
                IsNetworkInterpolationWarmupActive()))
        {
            return;
        }

        _pendingMapTeamSelection = false;
        OpenOnlineTeamSelection(clearPendingSelections: true, statusMessage: string.Empty);
    }

    internal static bool ShouldOpenDeferredMapTeamSelection(bool warmupActive, bool hasBaseline,
        int snapshotsAfterBaseline, bool interpolationWarmupActive)
        => !warmupActive || (hasBaseline
            && snapshotsAfterBaseline >= NetworkWorldWarmupMinimumAppliedSnapshotsAfterFull
            && !interpolationWarmupActive);

    private void CloseGameplaySelectionMenus()
    {
        _teamSelectOpen = false;
        _classSelectOpen = false;
    }

    private void DismissGameplayTeamSelection()
    {
        _pendingMapTeamSelection = false;
        if (_world.LocalPlayerAwaitingJoin && _networkClient.IsConnected
            && !_networkClient.IsReplayConnection)
        {
            BeginOnlineSpectateSelection();
            return;
        }
        CloseGameplaySelectionMenus();
    }

    private void OpenGameplayTeamSelection()
    {
        if (IsWatchOnlySession())
        {
            CloseGameplaySelectionMenus();
            _menuStatusMessage = "Watch mode cannot join teams.";
            return;
        }

        if (_world.LocalPlayerAwaitingJoin && TryApplyMapAutoJoinSelection())
        {
            return;
        }

        if (!_world.CanNetworkPlayerChangeTeamByMapBehavior(SimulationWorld.LocalPlayerSlot))
        {
            CloseGameplaySelectionMenus();
            _menuStatusMessage = "Team changes are locked by this map.";
            return;
        }

        _teamSelectOpen = true;
        _classSelectOpen = false;
    }

    private void OpenGameplayClassSelection()
    {
        if (IsWatchOnlySession())
        {
            CloseGameplaySelectionMenus();
            _menuStatusMessage = "Watch mode cannot select classes.";
            return;
        }

        if (_world.LocalPlayerAwaitingJoin && TryApplyMapAutoJoinSelection())
        {
            return;
        }

        if (!CanLocalPlayerSelectClassByMapBehavior(_world.GetNetworkPlayerClassDefinition(SimulationWorld.LocalPlayerSlot)))
        {
            CloseGameplaySelectionMenus();
            _menuStatusMessage = "Class changes are locked by this map.";
            return;
        }

        _classSelectOpen = true;
        _teamSelectOpen = false;
        WarmBrowserClassSelectionAssets(_pendingClassSelectTeam ?? _world.LocalPlayerTeam);
    }

    private void ToggleGameplayTeamSelection()
    {
        if (IsWatchOnlySession())
        {
            CloseGameplaySelectionMenus();
            _menuStatusMessage = "Watch mode cannot join teams.";
            return;
        }

        if (_world.LocalPlayerAwaitingJoin && TryApplyMapAutoJoinSelection())
        {
            return;
        }

        // N opens selection for an unjoined slot. Escape/Back dismisses it
        // through spectator mode so the player can watch the match.
        if (_world.LocalPlayerAwaitingJoin)
        {
            _teamSelectOpen = true;
            _classSelectOpen = false;
            return;
        }

        if (!_world.CanNetworkPlayerChangeTeamByMapBehavior(SimulationWorld.LocalPlayerSlot))
        {
            CloseGameplaySelectionMenus();
            _menuStatusMessage = "Team changes are locked by this map.";
            return;
        }

        var shouldOpen = !_teamSelectOpen;
        _teamSelectOpen = shouldOpen;
        if (shouldOpen)
        {
            _classSelectOpen = false;
        }
    }

    private void ToggleGameplayClassSelection()
    {
        if (IsWatchOnlySession())
        {
            CloseGameplaySelectionMenus();
            _menuStatusMessage = "Watch mode cannot select classes.";
            return;
        }

        if (!CanLocalPlayerSelectClassByMapBehavior(_world.GetNetworkPlayerClassDefinition(SimulationWorld.LocalPlayerSlot)))
        {
            CloseGameplaySelectionMenus();
            _menuStatusMessage = "Class changes are locked by this map.";
            return;
        }

        var shouldOpen = !_classSelectOpen;
        _classSelectOpen = shouldOpen;
        if (shouldOpen)
        {
            _teamSelectOpen = false;
        }
    }

    private void BeginOnlineSpectateSelection()
    {
        if (IsWatchOnlySession())
        {
            CloseGameplaySelectionMenus();
            return;
        }

        ResetLocalPredictionForAuthorityTransition();
        _networkClient.ClearPendingTeamSelection();
        _networkClient.ClearPendingClassSelection();
        _networkClient.QueueSpectateSelection();
        CloseGameplaySelectionMenus();
        _menuStatusMessage = "Switching to spectator mode...";
    }

    private void BeginOfflinePracticeSpectateSelection()
    {
        if (!IsPracticeSessionActive)
        {
            _menuStatusMessage = GetOfflineSpectateUnavailableMessage();
            return;
        }

        _offlinePracticeSpectatorMode = true;
        _world.PrepareLocalPlayerJoin();
        ApplyPracticeTeamSelection(_world.LocalPlayerTeam);
        ResetSpectatorTracking(enableTracking: true);
        _respawnCameraDetached = false;
        _respawnCameraCenter = GetDefaultFreeCameraCenter();
        CloseGameplaySelectionMenus();
        _menuStatusMessage = "Spectating Practice.";
    }

    private void BeginOnlineTeamSelection(PlayerTeam selectedTeam)
    {
        if (IsWatchOnlySession())
        {
            CloseGameplaySelectionMenus();
            _menuStatusMessage = "Watch mode cannot join teams.";
            return;
        }

        ResetLocalPredictionForAuthorityTransition();
        _networkClient.QueueTeamSelection(selectedTeam);
        if (_networkClient.IsLegacyGg2Connection && _networkClient.IsSpectator)
        {
            // GG2 can silently reject an unbalanced team. Wait for its roster
            // update to move our spectator slot before offering class selection.
            _legacyGg2TeamRequestStartedAtMilliseconds = Environment.TickCount64;
            CloseGameplaySelectionMenus();
            _menuStatusMessage = selectedTeam == PlayerTeam.Red
                ? "Joining RED team..."
                : "Joining BLU team...";
            return;
        }

        _menuStatusMessage = selectedTeam switch
        {
            PlayerTeam.Red => "Joining RED team. Select a class.",
            PlayerTeam.Blue => "Joining BLU team. Select a class.",
            _ => "Joining team. Select a class.",
        };
        OpenGameplayClassSelection();
    }

    private void ApplyOfflineTeamSelection(PlayerTeam selectedTeam)
    {
        _world.TryRequestNetworkPlayerTeamSelection(SimulationWorld.LocalPlayerSlot, selectedTeam);
        ApplyPracticeTeamSelection(selectedTeam);
        OpenGameplayClassSelection();
    }

    private void ApplyOfflineClassSelection(PlayerClass selectedClass)
    {
        if (!CharacterClassCatalog.RuntimeRegistry.TryGetClassBinding(selectedClass, out var binding))
        {
            return;
        }

        ApplyOfflineClassSelection(binding.ClassId);
    }

    private void ApplyOfflineClassSelection(string gameplayClassId)
    {
        ClearOfflinePracticeSpectatorMode();
        if (_world.LocalPlayerAwaitingJoin)
        {
            _world.CompleteLocalPlayerJoin(gameplayClassId);
            ApplyPracticeDummyPreferencesAfterJoin();
            return;
        }

        _world.TrySetLocalClass(gameplayClassId);
        ApplyPracticeDummyPreferencesAfterJoin();
    }

    private void ClearOfflinePracticeSpectatorMode()
    {
        if (!_offlinePracticeSpectatorMode)
        {
            return;
        }

        _offlinePracticeSpectatorMode = false;
        ResetSpectatorTracking(enableTracking: false);
        _respawnCameraDetached = false;
    }

    private bool TryApplyMapAutoJoinSelection()
    {
        if (!TryGetMapAutoJoinSelection(out var team, out var gameplayClassId))
        {
            return false;
        }

        _pendingClassSelectTeam = team;
        if (_networkClient.IsConnected)
        {
            ResetLocalPredictionForAuthorityTransition();
            _networkClient.QueueTeamSelection(team);
            _networkClient.QueueGameplayClassSelection(gameplayClassId);
            CloseGameplaySelectionMenus();
            _menuStatusMessage = team switch
            {
                PlayerTeam.Red => "Joining RED team.",
                PlayerTeam.Blue => "Joining BLU team.",
                _ => "Joining team.",
            };
            return true;
        }

        _world.TryRequestNetworkPlayerTeamSelection(SimulationWorld.LocalPlayerSlot, team);
        ApplyPracticeTeamSelection(team);
        ApplyOfflineClassSelection(gameplayClassId);
        CloseGameplaySelectionMenus();
        _menuStatusMessage = team switch
        {
            PlayerTeam.Red => "Joined RED team.",
            PlayerTeam.Blue => "Joined BLU team.",
            _ => "Joined team.",
        };
        return true;
    }

    private bool TryGetMapAutoJoinSelection(out PlayerTeam team, out string gameplayClassId)
    {
        team = PlayerTeam.Red;
        gameplayClassId = string.Empty;
        for (var index = 0; index < _world.Level.SpawnClassBehaviors.Count; index += 1)
        {
            var behavior = _world.Level.SpawnClassBehaviors[index];
            if (!behavior.SkipTeamSelect
                || !SpawnClassBehaviorMetadata.TryGetForcedGameplayClassId(behavior.ForcedClass, out gameplayClassId))
            {
                continue;
            }

            team = behavior.Team switch
            {
                SpawnClassBehaviorTeam.Red => PlayerTeam.Red,
                SpawnClassBehaviorTeam.Blue => PlayerTeam.Blue,
                _ => GetAutoSelectedTeam(GetTeamBalance()),
            };
            return true;
        }

        return false;
    }

    private bool CanLocalPlayerSelectClassByMapBehavior(CharacterClassDefinition definition)
    {
        var team = _pendingClassSelectTeam ?? _world.GetNetworkPlayerConfiguredTeam(SimulationWorld.LocalPlayerSlot);
        return !TryGetLocalMapSpawnClassBehavior(team, out var behavior)
            || behavior.AllowClassChange
            || _world.LocalPlayerAwaitingJoin;
    }

    private bool TryResolveLocalMapForcedGameplayClass(out string gameplayClassId)
    {
        var team = _pendingClassSelectTeam ?? _world.GetNetworkPlayerConfiguredTeam(SimulationWorld.LocalPlayerSlot);
        if (TryGetLocalMapSpawnClassBehavior(team, out var behavior)
            && SpawnClassBehaviorMetadata.TryGetForcedGameplayClassId(behavior.ForcedClass, out gameplayClassId))
        {
            return true;
        }

        gameplayClassId = string.Empty;
        return false;
    }

    private bool TryGetLocalMapSpawnClassBehavior(PlayerTeam team, out SpawnClassBehaviorMarker behavior)
    {
        return _world.TryGetMapSpawnClassBehavior(team, out behavior);
    }
}
