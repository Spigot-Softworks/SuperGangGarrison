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
        _teamSelectOpen = true;
        _classSelectOpen = false;
    }

    private void OpenGameplayClassSelection()
    {
        _classSelectOpen = true;
        _teamSelectOpen = false;
    }

    private void ToggleGameplayTeamSelection()
    {
        var shouldOpen = !_teamSelectOpen;
        _teamSelectOpen = shouldOpen;
        if (shouldOpen)
        {
            _classSelectOpen = false;
        }
    }

    private void ToggleGameplayClassSelection()
    {
        var shouldOpen = !_classSelectOpen;
        _classSelectOpen = shouldOpen;
        if (shouldOpen)
        {
            _teamSelectOpen = false;
        }
    }

    private void BeginOnlineSpectateSelection()
    {
        _networkClient.QueueSpectateSelection();
        CloseGameplaySelectionMenus();
        _menuStatusMessage = "Switching to spectator mode...";
    }

    private void BeginOnlineTeamSelection(PlayerTeam selectedTeam)
    {
        _networkClient.QueueTeamSelection(selectedTeam);
        if (_networkClient.IsSpectator)
        {
            CloseGameplaySelectionMenus();
            _menuStatusMessage = selectedTeam switch
            {
                PlayerTeam.Red => "Joining RED team...",
                PlayerTeam.Blue => "Joining BLU team...",
                _ => "Joining team...",
            };
            return;
        }

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
        if (_world.LocalPlayerAwaitingJoin)
        {
            _world.CompleteLocalPlayerJoin(selectedClass);
            ApplyPracticeDummyPreferencesAfterJoin();
            return;
        }

        _world.TrySetLocalClass(selectedClass);
        ApplyPracticeDummyPreferencesAfterJoin();
    }
}
