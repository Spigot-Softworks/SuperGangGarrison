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
                _hostSetupState.BackspaceActiveField();
                break;
            case '\t':
                _hostSetupState.CycleField();
                break;
            case '\r':
            case '\n':
                TryHostFromSetup();
                break;
            default:
                _hostSetupState.AppendCharacterToActiveField(e.Character);
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
}
