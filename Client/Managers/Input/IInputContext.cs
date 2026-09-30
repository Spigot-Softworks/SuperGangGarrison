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
    string _chatInput { get; set; }
    int _chatInputCursorIndex { get; set; }
    int _chatInputSelectionStart { get; set; }
    bool _chatOpen { get; set; }
    string _connectHostBuffer { get; set; }
    int _connectHostCursorIndex { get; set; }
    int _connectHostSelectionStart { get; set; }
    string _connectPortBuffer { get; set; }
    int _connectPortCursorIndex { get; set; }
    int _connectPortSelectionStart { get; set; }
    string _consoleInput { get; set; }
    int _consoleInputCursorIndex { get; set; }
    int _consoleInputSelectionStart { get; set; }
    bool _consoleOpen { get; set; }
    bool _editingConnectPort { get; set; }
    bool _editingFriendCode { get; set; }
    bool _editingFriendMessage { get; set; }
    bool _editingFriendNickname { get; set; }
    bool _editingPlayerName { get; set; }
    int _friendCodeCursorIndex { get; set; }
    string _friendCodeInputBuffer { get; set; }
    int _friendCodeSelectionStart { get; set; }
    int _friendMessageCursorIndex { get; set; }
    string _friendMessageInputBuffer { get; set; }
    int _friendMessageSelectionStart { get; set; }
    int _friendNicknameCursorIndex { get; set; }
    string _friendNicknameInputBuffer { get; set; }
    int _friendNicknameSelectionStart { get; set; }
    bool _friendsMenuOpen { get; set; }
    bool _hostSetupOpen { get; set; }
    bool _mainMenuOpen { get; set; }
    bool _manualConnectOpen { get; set; }
    bool _namePromptOpen { get; set; }
    OpenGarrison.Client.NetworkGameClient _networkClient { get; }
    bool _optionsMenuOpen { get; set; }
    string _passwordEditBuffer { get; set; }
    int _passwordEditCursorIndex { get; set; }
    int _passwordEditSelectionStart { get; set; }
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
