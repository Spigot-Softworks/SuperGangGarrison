#nullable enable

using System.Collections.Generic;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private int _hostSetupHoverIndex
    {
        get => _hostSetupState.HoverIndex;
        set => _hostSetupState.HoverIndex = value;
    }

    private int _hostSetupPlaylistHoverIndex
    {
        get => _hostSetupState.PlaylistHoverIndex;
        set => _hostSetupState.PlaylistHoverIndex = value;
    }

    private int _hostMapIndex
    {
        get => _hostSetupState.MapIndex;
        set => _hostSetupState.MapIndex = value;
    }

    private int _hostMapScrollOffset
    {
        get => _hostSetupState.MapScrollOffset;
        set => _hostSetupState.MapScrollOffset = value;
    }

    private int _hostSetupContentScrollOffset
    {
        get => _hostSetupState.ContentScrollOffset;
        set => _hostSetupState.ContentScrollOffset = value;
    }

    private List<OpenGarrisonMapRotationEntry> _hostMapEntries
    {
        get => _hostSetupState.MapEntries;
        set => _hostSetupState.MapEntries = value ?? new List<OpenGarrisonMapRotationEntry>();
    }

    private HostSetupEditField _hostSetupEditField
    {
        get => _hostSetupState.EditField;
        set => _hostSetupState.EditField = value;
    }

    private HostSetupTab _hostSetupTab
    {
        get => _hostSetupState.Tab;
        set => _hostSetupState.Tab = value;
    }

    private HostSetupScreen _hostSetupScreen
    {
        get => _hostSetupState.Screen;
        set => _hostSetupState.Screen = value;
    }

    private int _hostOptionsHoverIndex
    {
        get => _hostSetupState.OptionsHoverIndex;
        set => _hostSetupState.OptionsHoverIndex = value;
    }

    private int _hostOptionsScrollOffset
    {
        get => _hostSetupState.OptionsScrollOffset;
        set => _hostSetupState.OptionsScrollOffset = value;
    }

    private string _hostServerNameBuffer
    {
        get => _hostSetupState.ServerNameBuffer;
        set => _hostSetupState.ServerNameBuffer = value;
    }

    private string _hostPortBuffer
    {
        get => _hostSetupState.PortBuffer;
        set => _hostSetupState.PortBuffer = value;
    }

    private string _hostSlotsBuffer
    {
        get => _hostSetupState.SlotsBuffer;
        set => _hostSetupState.SlotsBuffer = value;
    }

    private string _hostPasswordBuffer
    {
        get => _hostSetupState.PasswordBuffer;
        set => _hostSetupState.PasswordBuffer = value;
    }

    private string _hostRconPasswordBuffer
    {
        get => _hostSetupState.RconPasswordBuffer;
        set => _hostSetupState.RconPasswordBuffer = value;
    }

    private string _hostMapRotationFileBuffer
    {
        get => _hostSetupState.MapRotationFileBuffer;
        set => _hostSetupState.MapRotationFileBuffer = value;
    }

    private bool _hostUsePlaylistFile
    {
        get => _hostSetupState.UsePlaylistFile;
        set => _hostSetupState.UsePlaylistFile = value;
    }

    private string _hostTimeLimitBuffer
    {
        get => _hostSetupState.TimeLimitBuffer;
        set
        {
            _hostSetupState.TimeLimitBuffer = value;
            _hostSetupState.NotifyLinkedBasicHostSettingsChanged();
        }
    }

    private string _hostCapLimitBuffer
    {
        get => _hostSetupState.CapLimitBuffer;
        set
        {
            _hostSetupState.CapLimitBuffer = value;
            _hostSetupState.NotifyLinkedBasicHostSettingsChanged();
        }
    }

    private string _hostRespawnSecondsBuffer
    {
        get => _hostSetupState.RespawnSecondsBuffer;
        set
        {
            _hostSetupState.RespawnSecondsBuffer = value;
            _hostSetupState.NotifyLinkedBasicHostSettingsChanged();
        }
    }

    private int _hostServerNameCursorIndex
    {
        get => _hostSetupState.ServerNameCursorIndex;
        set => _hostSetupState.ServerNameCursorIndex = value;
    }

    private int _hostServerNameSelectionStart
    {
        get => _hostSetupState.ServerNameSelectionStart;
        set => _hostSetupState.ServerNameSelectionStart = value;
    }

    private int _hostPortCursorIndex
    {
        get => _hostSetupState.PortCursorIndex;
        set => _hostSetupState.PortCursorIndex = value;
    }

    private int _hostPortSelectionStart
    {
        get => _hostSetupState.PortSelectionStart;
        set => _hostSetupState.PortSelectionStart = value;
    }

    private int _hostSlotsCursorIndex
    {
        get => _hostSetupState.SlotsCursorIndex;
        set => _hostSetupState.SlotsCursorIndex = value;
    }

    private int _hostSlotsSelectionStart
    {
        get => _hostSetupState.SlotsSelectionStart;
        set => _hostSetupState.SlotsSelectionStart = value;
    }

    private int _hostPasswordCursorIndex
    {
        get => _hostSetupState.PasswordCursorIndex;
        set => _hostSetupState.PasswordCursorIndex = value;
    }

    private int _hostPasswordSelectionStart
    {
        get => _hostSetupState.PasswordSelectionStart;
        set => _hostSetupState.PasswordSelectionStart = value;
    }

    private int _hostRconPasswordCursorIndex
    {
        get => _hostSetupState.RconPasswordCursorIndex;
        set => _hostSetupState.RconPasswordCursorIndex = value;
    }

    private int _hostRconPasswordSelectionStart
    {
        get => _hostSetupState.RconPasswordSelectionStart;
        set => _hostSetupState.RconPasswordSelectionStart = value;
    }

    private int _hostMapRotationFileCursorIndex
    {
        get => _hostSetupState.MapRotationFileCursorIndex;
        set => _hostSetupState.MapRotationFileCursorIndex = value;
    }

    private int _hostMapRotationFileSelectionStart
    {
        get => _hostSetupState.MapRotationFileSelectionStart;
        set => _hostSetupState.MapRotationFileSelectionStart = value;
    }

    private int _hostTimeLimitCursorIndex
    {
        get => _hostSetupState.TimeLimitCursorIndex;
        set => _hostSetupState.TimeLimitCursorIndex = value;
    }

    private int _hostTimeLimitSelectionStart
    {
        get => _hostSetupState.TimeLimitSelectionStart;
        set => _hostSetupState.TimeLimitSelectionStart = value;
    }

    private int _hostCapLimitCursorIndex
    {
        get => _hostSetupState.CapLimitCursorIndex;
        set => _hostSetupState.CapLimitCursorIndex = value;
    }

    private int _hostCapLimitSelectionStart
    {
        get => _hostSetupState.CapLimitSelectionStart;
        set => _hostSetupState.CapLimitSelectionStart = value;
    }

    private int _hostRespawnSecondsCursorIndex
    {
        get => _hostSetupState.RespawnSecondsCursorIndex;
        set => _hostSetupState.RespawnSecondsCursorIndex = value;
    }

    private int _hostRespawnSecondsSelectionStart
    {
        get => _hostSetupState.RespawnSecondsSelectionStart;
        set => _hostSetupState.RespawnSecondsSelectionStart = value;
    }

    private bool _hostLobbyAnnounceEnabled
    {
        get => _hostSetupState.LobbyAnnounceEnabled;
        set => _hostSetupState.LobbyAnnounceEnabled = value;
    }

    private bool _hostAutoBalanceEnabled
    {
        get => _hostSetupState.AutoBalanceEnabled;
        set
        {
            _hostSetupState.AutoBalanceEnabled = value;
            _hostSetupState.NotifyLinkedBasicHostSettingsChanged();
        }
    }

    private bool _hostSecondaryAbilitiesEnabled
    {
        get => _hostSetupState.SecondaryAbilitiesEnabled;
        set
        {
            _hostSetupState.SecondaryAbilitiesEnabled = value;
            _hostSetupState.NotifyLinkedBasicHostSettingsChanged();
        }
    }
}
