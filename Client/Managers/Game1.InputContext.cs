#nullable enable

using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Client.Plugins;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Client;

public partial class Game1 : IInputContext
{



    bool IInputContext._chatOpen { get => _chatOpen; set => _chatOpen = value; }










    bool IInputContext._consoleOpen { get => _consoleOpen; set => _consoleOpen = value; }

    bool IInputContext._editingConnectPort { get => _editingConnectPort; set => _editingConnectPort = value; }

    bool IInputContext._editingFriendCode { get => _editingFriendCode; set => _editingFriendCode = value; }

    bool IInputContext._editingFriendMessage { get => _editingFriendMessage; set => _editingFriendMessage = value; }

    bool IInputContext._editingFriendNickname { get => _editingFriendNickname; set => _editingFriendNickname = value; }

    bool IInputContext._editingPlayerName { get => _editingPlayerName; set => _editingPlayerName = value; }










    bool IInputContext._friendsMenuOpen { get => _friendsMenuOpen; set => _friendsMenuOpen = value; }

    bool IInputContext._hostSetupOpen { get => _hostSetupOpen; set => _hostSetupOpen = value; }

    bool IInputContext._mainMenuOpen { get => _mainMenuOpen; set => _mainMenuOpen = value; }

    bool IInputContext._manualConnectOpen { get => _manualConnectOpen; set => _manualConnectOpen = value; }

    bool IInputContext._namePromptOpen { get => _namePromptOpen; set => _namePromptOpen = value; }

    OpenGarrison.Client.NetworkGameClient IInputContext._networkClient { get => _networkClient; }

    bool IInputContext._optionsMenuOpen { get => _optionsMenuOpen; set => _optionsMenuOpen = value; }




    string IInputContext._passwordPromptMessage { get => _passwordPromptMessage; set => _passwordPromptMessage = value; }

    bool IInputContext._passwordPromptOpen { get => _passwordPromptOpen; set => _passwordPromptOpen = value; }

    bool IInputContext._playerCardEditorOpen { get => _playerCardEditorOpen; set => _playerCardEditorOpen = value; }

    string IInputContext._playerNameEditBuffer { get => _playerNameEditBuffer; set => _playerNameEditBuffer = value; }

    int IInputContext._playerNameEditCursorIndex { get => _playerNameEditCursorIndex; set => _playerNameEditCursorIndex = value; }

    int IInputContext._playerNameEditSelectionStart { get => _playerNameEditSelectionStart; set => _playerNameEditSelectionStart = value; }

    bool IInputContext._practiceSetupOpen { get => _practiceSetupOpen; set => _practiceSetupOpen = value; }

    bool IInputContext.IsWindowInputActive { get => IsWindowInputActive; }

    InputManager IInputContext.Input => _inputManager;

    SessionManager IInputContext.Session => _sessionManager;

    HostingManager IInputContext.Hosting => _hostingManager;

    void IInputContext.CommitPlayerNamePrompt() { CommitPlayerNamePrompt(); }

    ValueTuple<string, int, int> IInputContext.DeleteTextSelectionOrBackspace(string text, int cursorIndex, int selectionStart) => DeleteTextSelectionOrBackspace(text, cursorIndex, selectionStart);

    void IInputContext.ExecuteConsoleCommand() { ExecuteConsoleCommand(); }

    void IInputContext.ExecuteConsoleCommand(string commandText) { ExecuteConsoleCommand(commandText); }

    bool IInputContext.HandleGarrisonBuilderTextInput(char character) => HandleGarrisonBuilderTextInput(character);

    bool IInputContext.HandleManagedRoomText(char character) => HandleManagedRoomText(character);

    ValueTuple<string, int, int> IInputContext.InsertTextCharacterAtCursor(string text, char character, int cursorIndex, int selectionStart, int maxLength) => InsertTextCharacterAtCursor(text, character, cursorIndex, selectionStart, maxLength);

    void IInputContext.SaveFriendNicknameFromInput() { SaveFriendNicknameFromInput(); }

    void IInputContext.SetLocalPlayerNameFromSettings(string playerName) { SetLocalPlayerNameFromSettings(playerName); }

    void IInputContext.SubmitChatMessage() { SubmitChatMessage(); }

    void IInputContext.TryConnectFromMenu() { TryConnectFromMenu(); }

    bool IInputContext.TryHandleAccountDialogTextInput(char character) => TryHandleAccountDialogTextInput(character);

    bool IInputContext.TryHandlePlayerCardBioTextInput(char character) => TryHandlePlayerCardBioTextInput(character);

    bool IInputContext.TryHandlePracticeMapBrowserTextInput(char character) => TryHandlePracticeMapBrowserTextInput(character);

    void IInputContext.TrySendFriendRequestFromInput() { TrySendFriendRequestFromInput(); }

    bool IInputContext.TrySendSelectedFriendDirectMessageFromInput() => TrySendSelectedFriendDirectMessageFromInput();

}
