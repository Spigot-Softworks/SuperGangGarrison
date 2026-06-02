#nullable enable

using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void OpenHostSetupMenu()
    {
        if (OperatingSystem.IsBrowser())
        {
            _menuStatusMessage = "Hosting is unavailable in browser.";
            return;
        }

        _mainMenuOverlayStateController.OpenHostSetupMenu();
    }

    private void CloseHostSetupMenu(bool clearStatus = false)
    {
        _mainMenuOverlayStateController.CloseHostSetupMenu(clearStatus);
    }

    private void TryHostFromSetup(bool runInTerminal = false)
    {
        if (OperatingSystem.IsBrowser())
        {
            _menuStatusMessage = "Hosting is unavailable in browser.";
            return;
        }

        if (!_hostSetupState.TryBuildLaunchRequest(out var request, out var error))
        {
            _menuStatusMessage = error;
            return;
        }

        PersistClientSettings();
        if (IsServerLauncherMode)
        {
            if (runInTerminal)
            {
                BeginDedicatedServerTerminalLaunch(
                    request.ServerName,
                    request.Port,
                    request.MaxPlayers,
                    request.Password,
                    request.RconPassword,
                    request.TimeLimitMinutes,
                    request.CapLimit,
                    request.RespawnSeconds,
                    request.LobbyAnnounce,
                    request.AutoBalance,
                    request.SecondaryAbilitiesEnabled,
                    request.RequestedMap,
                    request.MapRotationFile);
            }
            else
            {
                BeginDedicatedServerLaunch(
                    request.ServerName,
                    request.Port,
                    request.MaxPlayers,
                    request.Password,
                    request.RconPassword,
                    request.TimeLimitMinutes,
                    request.CapLimit,
                    request.RespawnSeconds,
                    request.LobbyAnnounce,
                    request.AutoBalance,
                    request.SecondaryAbilitiesEnabled,
                    request.RequestedMap,
                    request.MapRotationFile);
            }
            return;
        }

        BeginHostedGame(
            request.ServerName,
            request.Port,
            request.MaxPlayers,
            request.Password,
            request.RconPassword,
            request.TimeLimitMinutes,
            request.CapLimit,
            request.RespawnSeconds,
            request.LobbyAnnounce,
            request.AutoBalance,
            request.SecondaryAbilitiesEnabled,
            request.RequestedMap,
            request.MapRotationFile);
    }

    private void AddSelectedAvailableMapToPlaylist()
    {
        _hostSetupState.AddSelectedAvailableMapToPlaylist();
        _menuStatusMessage = string.Empty;
    }

    private void RemoveSelectedPlaylistMap()
    {
        _hostSetupState.RemoveSelectedPlaylistMap();
        _menuStatusMessage = string.Empty;
    }

    private void MoveSelectedPlaylistMap(int direction)
    {
        _hostSetupState.MoveSelectedPlaylistMap(direction);
        _menuStatusMessage = string.Empty;
    }

    private void SortHostMapEntries(string? selectedLevelName = null)
    {
        _hostSetupState.SortMapEntries(selectedLevelName);
    }

    private bool SelectHostMapEntry(string? levelName)
    {
        return _hostSetupState.SelectMapEntry(levelName);
    }

    private int FindDefaultHostMapIndex()
    {
        return _hostSetupState.FindDefaultMapIndex();
    }

    private OpenGarrisonMapRotationEntry? GetSelectedHostMapEntry()
    {
        return _hostSetupState.GetSelectedMapEntry();
    }

    private string GetHostStockRotationSummary(int previewCount = 4)
    {
        return _hostSetupState.GetStockRotationSummary(previewCount);
    }

    private void EnsureSelectedHostMapVisible()
    {
        var layout = HostSetupMenuLayoutCalculator.CreateMenuLayout(
            ViewportWidth,
            ViewportHeight,
            _hostMapEntries.Count,
            IsServerLauncherMode,
            _hostSetupScreen);
        _hostSetupState.EnsureSelectedMapVisible(layout.VisibleRowCapacity);
    }
}
