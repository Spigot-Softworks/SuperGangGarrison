#nullable enable

using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void OpenHostSetupMenu()
    {
        _hostSetupOpen = true;
        _menuStatusMessage = string.Empty;
        _manualConnectOpen = false;
        CloseLobbyBrowser(clearStatus: false);
        _optionsMenuOpen = false;
        _pluginOptionsMenuOpen = false;
        _creditsOpen = false;
        _editingPlayerName = false;
        _hostSetupState.PrepareForOpen(_clientSettings.HostDefaults);
        EnsureSelectedHostMapVisible();
    }

    private void TryHostFromSetup(bool runInTerminal = false)
    {
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
                    request.TimeLimitMinutes,
                    request.CapLimit,
                    request.RespawnSeconds,
                    request.LobbyAnnounce,
                    request.AutoBalance,
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
                    request.TimeLimitMinutes,
                    request.CapLimit,
                    request.RespawnSeconds,
                    request.LobbyAnnounce,
                    request.AutoBalance,
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
            request.TimeLimitMinutes,
            request.CapLimit,
            request.RespawnSeconds,
            request.LobbyAnnounce,
            request.AutoBalance,
            request.RequestedMap,
            request.MapRotationFile);
    }

    private void ToggleSelectedHostMap()
    {
        _hostSetupState.ToggleSelectedMap();
        EnsureSelectedHostMapVisible();
        _menuStatusMessage = string.Empty;
    }

    private void MoveSelectedHostMap(int direction)
    {
        _hostSetupState.MoveSelectedMap(direction);
        EnsureSelectedHostMapVisible();
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
            IsServerLauncherMode);
        _hostSetupState.EnsureSelectedMapVisible(layout.VisibleRowCapacity);
    }
}
