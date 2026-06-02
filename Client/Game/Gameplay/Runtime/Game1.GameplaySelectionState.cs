#nullable enable

using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void CloseGameplaySelectionMenus()
    {
        _teamSelectOpen = false;
        _classSelectOpen = false;
    }

    private void OpenGameplayTeamSelection()
    {
        if (IsWatchOnlySession())
        {
            CloseGameplaySelectionMenus();
            _menuStatusMessage = "Watch mode cannot join teams.";
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

        _networkClient.QueueTeamSelection(selectedTeam);
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
        _world.SetLocalPlayerTeam(selectedTeam);
        ApplyPracticeTeamSelection(selectedTeam);
        OpenGameplayClassSelection();
    }

    private void ApplyOfflineClassSelection(PlayerClass selectedClass)
    {
        ClearOfflinePracticeSpectatorMode();
        if (_world.LocalPlayerAwaitingJoin)
        {
            _world.CompleteLocalPlayerJoin(selectedClass);
            ApplyPracticeDummyPreferencesAfterJoin();
            return;
        }

        _world.TrySetLocalClass(selectedClass);
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
}
