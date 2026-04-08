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

    private string _hostTimeLimitBuffer
    {
        get => _hostSetupState.TimeLimitBuffer;
        set => _hostSetupState.TimeLimitBuffer = value;
    }

    private string _hostCapLimitBuffer
    {
        get => _hostSetupState.CapLimitBuffer;
        set => _hostSetupState.CapLimitBuffer = value;
    }

    private string _hostRespawnSecondsBuffer
    {
        get => _hostSetupState.RespawnSecondsBuffer;
        set => _hostSetupState.RespawnSecondsBuffer = value;
    }

    private bool _hostLobbyAnnounceEnabled
    {
        get => _hostSetupState.LobbyAnnounceEnabled;
        set => _hostSetupState.LobbyAnnounceEnabled = value;
    }

    private bool _hostAutoBalanceEnabled
    {
        get => _hostSetupState.AutoBalanceEnabled;
        set => _hostSetupState.AutoBalanceEnabled = value;
    }
}
