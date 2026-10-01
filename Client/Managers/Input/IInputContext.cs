#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OpenGarrison.Client;

public interface IInputContext
{
    InputManager Input { get; }
    SessionManager Session { get; }
    HostingManager Hosting { get; }
    bool _chatOpen { get; set; }
    bool _consoleOpen { get; set; }
    bool _editingConnectPort { get; set; }
    bool _editingFriendCode { get; set; }
    bool _editingFriendMessage { get; set; }
    bool _editingFriendNickname { get; set; }
    bool _editingPlayerName { get; set; }
    bool _friendsMenuOpen { get; set; }
    bool _hostSetupOpen { get; set; }
    bool _mainMenuOpen { get; set; }
    bool _manualConnectOpen { get; set; }
    bool _namePromptOpen { get; set; }
    OpenGarrison.Client.NetworkGameClient _networkClient { get; }
    bool _optionsMenuOpen { get; set; }
    string _passwordPromptMessage { get; set; }
    bool _passwordPromptOpen { get; set; }
    bool _playerCardEditorOpen { get; set; }
    string _playerNameEditBuffer { get; set; }
    int _playerNameEditCursorIndex { get; set; }
    int _playerNameEditSelectionStart { get; set; }
    bool _practiceSetupOpen { get; set; }
    bool IsWindowInputActive { get; }
    void CommitPlayerNamePrompt();
    (string Text, int CursorIndex, int SelectionStart) DeleteTextSelectionOrBackspace(string text, int cursorIndex, int selectionStart);
    void ExecuteConsoleCommand();
    void ExecuteConsoleCommand(string commandText);
    bool HandleGarrisonBuilderTextInput(char character);
    bool HandleManagedRoomText(char character);
    (string Text, int CursorIndex, int SelectionStart) InsertTextCharacterAtCursor(string text, char character, int cursorIndex, int selectionStart, int maxLength);
    void SaveFriendNicknameFromInput();
    void SetLocalPlayerNameFromSettings(string playerName);
    void SubmitChatMessage();
    void TryConnectFromMenu();
    bool TryHandleAccountDialogTextInput(char character);
    bool TryHandlePlayerCardBioTextInput(char character);
    bool TryHandlePracticeMapBrowserTextInput(char character);
    void TrySendFriendRequestFromInput();
    bool TrySendSelectedFriendDirectMessageFromInput();
}
