#nullable enable

using Microsoft.Xna.Framework;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void OnWindowTextInput(object? sender, TextInputEventArgs e)
    {
        if (HandlePasswordPromptTextInput(e))
        {
            return;
        }

        if (_mainMenuOpen && _manualConnectOpen && HandleManualConnectTextInput(e))
        {
            return;
        }

        if (_mainMenuOpen && _hostSetupOpen && HandleHostSetupTextInput(e))
        {
            return;
        }

        if (_optionsMenuOpen && _editingPlayerName && HandleOptionsPlayerNameTextInput(e))
        {
            return;
        }

        if (HandleChatTextInput(e))
        {
            return;
        }

        HandleConsoleTextInput(e);
    }

    private bool HandleChatTextInput(TextInputEventArgs e)
    {
        if (!_chatOpen)
        {
            return false;
        }

        switch (e.Character)
        {
            case '\b':
                if (_chatInput.Length > 0)
                {
                    _chatInput = _chatInput[..^1];
                }
                break;
            case '\r':
            case '\n':
                SubmitChatMessage();
                break;
            default:
                if (!char.IsControl(e.Character) && _chatInput.Length < 120)
                {
                    _chatInput += e.Character;
                }
                break;
        }

        return true;
    }

    private bool HandlePasswordPromptTextInput(TextInputEventArgs e)
    {
        if (!_passwordPromptOpen)
        {
            return false;
        }

        switch (e.Character)
        {
            case '\b':
                if (_passwordEditBuffer.Length > 0)
                {
                    _passwordEditBuffer = _passwordEditBuffer[..^1];
                }
                break;
            case '\r':
            case '\n':
                if (!string.IsNullOrEmpty(_passwordEditBuffer))
                {
                    _passwordPromptMessage = "Submitting...";
                    _networkClient.SendPassword(_passwordEditBuffer);
                }
                else
                {
                    _passwordPromptMessage = "Password required.";
                }
                break;
            default:
                if (!char.IsControl(e.Character) && _passwordEditBuffer.Length < 32)
                {
                    _passwordEditBuffer += e.Character;
                    _passwordPromptMessage = string.Empty;
                }
                break;
        }

        return true;
    }

    private bool HandleManualConnectTextInput(TextInputEventArgs e)
    {
        switch (e.Character)
        {
            case '\b':
                if (_editingConnectPort)
                {
                    if (_connectPortBuffer.Length > 0)
                    {
                        _connectPortBuffer = _connectPortBuffer[..^1];
                    }
                }
                else if (_connectHostBuffer.Length > 0)
                {
                    _connectHostBuffer = _connectHostBuffer[..^1];
                }
                break;
            case '\t':
                _editingConnectHost = !_editingConnectHost;
                _editingConnectPort = !_editingConnectHost;
                break;
            case '\r':
            case '\n':
                TryConnectFromMenu();
                break;
            default:
                if (char.IsControl(e.Character))
                {
                    break;
                }

                if (_editingConnectPort)
                {
                    if (char.IsDigit(e.Character) && _connectPortBuffer.Length < 5)
                    {
                        _connectPortBuffer += e.Character;
                    }
                }
                else if (_connectHostBuffer.Length < 64)
                {
                    _connectHostBuffer += e.Character;
                }
                break;
        }

        return true;
    }

    private bool HandleHostSetupTextInput(TextInputEventArgs e)
    {
        if (IsServerLauncherMode && _hostSetupTab == HostSetupTab.ServerConsole)
        {
            return HandleHostedServerConsoleTextInput(e);
        }

        switch (e.Character)
        {
            case '\b':
                switch (_hostSetupEditField)
                {
                    case HostSetupEditField.ServerName:
                        if (_hostServerNameBuffer.Length > 0)
                        {
                            _hostServerNameBuffer = _hostServerNameBuffer[..^1];
                        }
                        break;
                    case HostSetupEditField.Port:
                        if (_hostPortBuffer.Length > 0)
                        {
                            _hostPortBuffer = _hostPortBuffer[..^1];
                        }
                        break;
                    case HostSetupEditField.Slots:
                        if (_hostSlotsBuffer.Length > 0)
                        {
                            _hostSlotsBuffer = _hostSlotsBuffer[..^1];
                        }
                        break;
                    case HostSetupEditField.Password:
                        if (_hostPasswordBuffer.Length > 0)
                        {
                            _hostPasswordBuffer = _hostPasswordBuffer[..^1];
                        }
                        break;
                    case HostSetupEditField.MapRotationFile:
                        if (_hostMapRotationFileBuffer.Length > 0)
                        {
                            _hostMapRotationFileBuffer = _hostMapRotationFileBuffer[..^1];
                        }
                        break;
                    case HostSetupEditField.TimeLimit:
                        if (_hostTimeLimitBuffer.Length > 0)
                        {
                            _hostTimeLimitBuffer = _hostTimeLimitBuffer[..^1];
                        }
                        break;
                    case HostSetupEditField.CapLimit:
                        if (_hostCapLimitBuffer.Length > 0)
                        {
                            _hostCapLimitBuffer = _hostCapLimitBuffer[..^1];
                        }
                        break;
                    case HostSetupEditField.RespawnSeconds:
                        if (_hostRespawnSecondsBuffer.Length > 0)
                        {
                            _hostRespawnSecondsBuffer = _hostRespawnSecondsBuffer[..^1];
                        }
                        break;
                }
                break;
            case '\t':
                CycleHostSetupField();
                break;
            case '\r':
            case '\n':
                TryHostFromSetup();
                break;
            default:
                if (char.IsControl(e.Character))
                {
                    break;
                }

                if (_hostSetupEditField == HostSetupEditField.None)
                {
                    _hostSetupEditField = HostSetupEditField.ServerName;
                }

                if (_hostSetupEditField == HostSetupEditField.ServerName)
                {
                    if (_hostServerNameBuffer.Length < 32)
                    {
                        _hostServerNameBuffer += e.Character;
                    }
                }
                else if (_hostSetupEditField == HostSetupEditField.Password)
                {
                    if (_hostPasswordBuffer.Length < 32)
                    {
                        _hostPasswordBuffer += e.Character;
                    }
                }
                else if (_hostSetupEditField == HostSetupEditField.MapRotationFile)
                {
                    if (_hostMapRotationFileBuffer.Length < 180)
                    {
                        _hostMapRotationFileBuffer += e.Character;
                    }
                }
                else if (char.IsDigit(e.Character))
                {
                    if (_hostSetupEditField == HostSetupEditField.Port && _hostPortBuffer.Length < 5)
                    {
                        _hostPortBuffer += e.Character;
                    }
                    else if (_hostSetupEditField == HostSetupEditField.Slots && _hostSlotsBuffer.Length < 2)
                    {
                        _hostSlotsBuffer += e.Character;
                    }
                    else if (_hostSetupEditField == HostSetupEditField.TimeLimit && _hostTimeLimitBuffer.Length < 3)
                    {
                        _hostTimeLimitBuffer += e.Character;
                    }
                    else if (_hostSetupEditField == HostSetupEditField.CapLimit && _hostCapLimitBuffer.Length < 3)
                    {
                        _hostCapLimitBuffer += e.Character;
                    }
                    else if (_hostSetupEditField == HostSetupEditField.RespawnSeconds && _hostRespawnSecondsBuffer.Length < 3)
                    {
                        _hostRespawnSecondsBuffer += e.Character;
                    }
                }
                break;
        }

        if (_hostSetupEditField != HostSetupEditField.None)
        {
            _menuStatusMessage = string.Empty;
        }

        return true;
    }

    private bool HandleHostedServerConsoleTextInput(TextInputEventArgs e)
    {
        switch (e.Character)
        {
            case '\b':
                _hostedServerConsole.BackspaceCommandInput();
                break;
            case '\r':
            case '\n':
                ExecuteHostedServerCommandFromUi(_hostedServerConsole.CreateSnapshot().CommandInput);
                break;
            case '\t':
                _hostSetupEditField = HostSetupEditField.ServerConsoleCommand;
                break;
            default:
                _hostedServerConsole.AppendCommandInput(e.Character, 120);
                break;
        }

        if (_hostSetupEditField != HostSetupEditField.None)
        {
            _menuStatusMessage = string.Empty;
        }

        return true;
    }

    private bool HandleOptionsPlayerNameTextInput(TextInputEventArgs e)
    {
        switch (e.Character)
        {
            case '\b':
                if (_playerNameEditBuffer.Length > 0)
                {
                    _playerNameEditBuffer = _playerNameEditBuffer[..^1];
                }
                break;
            case '\r':
            case '\n':
                SetLocalPlayerNameFromSettings(_playerNameEditBuffer);
                _editingPlayerName = false;
                break;
            default:
                if (!char.IsControl(e.Character) && _playerNameEditBuffer.Length < 20 && e.Character != '#')
                {
                    _playerNameEditBuffer += e.Character;
                }
                break;
        }

        return true;
    }

    private void HandleConsoleTextInput(TextInputEventArgs e)
    {
        if (!_consoleOpen)
        {
            return;
        }

        switch (e.Character)
        {
            case '\b':
                if (_consoleInput.Length > 0)
                {
                    _consoleInput = _consoleInput[..^1];
                }
                break;
            case '\r':
                ExecuteConsoleCommand();
                break;
            case '`':
            case '~':
                break;
            default:
                if (!char.IsControl(e.Character))
                {
                    _consoleInput += e.Character;
                }
                break;
        }
    }

    private void CycleHostSetupField()
    {
        _hostSetupEditField = _hostSetupEditField switch
        {
            HostSetupEditField.ServerName => HostSetupEditField.Port,
            HostSetupEditField.Port => HostSetupEditField.Slots,
            HostSetupEditField.Slots => HostSetupEditField.Password,
            HostSetupEditField.Password => HostSetupEditField.MapRotationFile,
            HostSetupEditField.MapRotationFile => HostSetupEditField.TimeLimit,
            HostSetupEditField.TimeLimit => HostSetupEditField.CapLimit,
            HostSetupEditField.CapLimit => HostSetupEditField.RespawnSeconds,
            HostSetupEditField.RespawnSeconds => HostSetupEditField.ServerName,
            _ => HostSetupEditField.ServerName,
        };
    }
}
