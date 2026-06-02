#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Runtime.InteropServices;

namespace OpenGarrison.Client;

public partial class Game1
{
    private static bool IsShiftHeld(KeyboardState keyboard)
        => keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);

    private static bool HasTextSelection(int cursorIndex, int selectionStart)
        => cursorIndex != selectionStart;

    private static (int start, int length) GetTextSelectionRange(int cursorIndex, int selectionStart)
    {
        var start = Math.Min(cursorIndex, selectionStart);
        return (start, Math.Abs(cursorIndex - selectionStart));
    }

    private static string GetMenuInputDisplayText(string text, int cursorIndex, int selectionStart)
    {
        if (cursorIndex < 0 || cursorIndex > text.Length)
        {
            return text + "_";
        }

        return GetTextWithCursor(text, cursorIndex);
    }

    private static string GetTextWithCursor(string text, int cursorIndex)
    {
        cursorIndex = Math.Clamp(cursorIndex, 0, text.Length);
        return string.Concat(text.AsSpan(0, cursorIndex), "_", text.AsSpan(cursorIndex));
    }

    private const double TextInputDoubleClickThresholdSeconds = 0.45;
    private const double TextInputArrowRepeatDelaySeconds = 0.5;
    private const double TextInputArrowRepeatIntervalSeconds = 0.05;

    private enum TextFieldClickTarget
    {
        None,
        ManualConnectHost,
        ManualConnectPort,
        FriendsCode,
        FriendsNickname,
        FriendsMessage,
        OptionsPlayerName,
        HostSetupServerName,
        HostSetupPort,
        HostSetupSlots,
        HostSetupPassword,
        HostSetupRconPassword,
        HostSetupMapRotationFile,
        HostSetupTimeLimit,
        HostSetupCapLimit,
        HostSetupRespawnSeconds,
        HostSetupAdvancedCvar,
        HostSetupConsoleCommand,
    }

    private TextFieldClickTarget _lastTextInputClickTarget;
    private double _lastTextInputClickTimeSeconds;

    private ArrowKeyRepeatState _textInputLeftArrowRepeatState;
    private ArrowKeyRepeatState _textInputRightArrowRepeatState;

    private struct ArrowKeyRepeatState
    {
        public bool IsHeld;
        public double HeldTimeSeconds;
        public double RepeatTimerSeconds;
    }

    private void DrawBitmapFontTextWithSelection(
        string text,
        Vector2 position,
        int cursorIndex,
        int selectionStart,
        Color normalTextColor,
        Color selectionTextColor,
        Color selectionBackgroundColor,
        float scale = 1f)
    {
        if (!HasTextSelection(cursorIndex, selectionStart))
        {
            DrawBitmapFontText(GetTextWithCursor(text, cursorIndex), position, normalTextColor, scale);
            return;
        }

        var (start, length) = GetTextSelectionRange(cursorIndex, selectionStart);
        if (start < 0 || start >= text.Length || length <= 0)
        {
            DrawBitmapFontText(GetTextWithCursor(text, cursorIndex), position, normalTextColor, scale);
            return;
        }

        var before = text[..start];
        var selected = text.Substring(start, length);
        var after = text[(start + length)..];
        DrawBitmapFontText(before, position, normalTextColor, scale);

        var selectionX = position.X + MeasureBitmapFontWidth(before, scale);
        var selectionWidth = MeasureBitmapFontWidth(selected, scale);
        var selectionHeight = MeasureBitmapFontHeight(scale);
        _spriteBatch.Draw(
            _pixel,
            new Rectangle(
                (int)MathF.Floor(selectionX),
                (int)MathF.Floor(position.Y),
                Math.Max(1, (int)MathF.Ceiling(selectionWidth)),
                (int)MathF.Ceiling(selectionHeight)),
            selectionBackgroundColor);
        DrawBitmapFontText(selected, new Vector2(selectionX, position.Y), selectionTextColor, scale);
        DrawBitmapFontText(after, new Vector2(selectionX + selectionWidth, position.Y), normalTextColor, scale);
    }

    private void DrawSpriteFontTextWithSelection(
        SpriteFont font,
        string text,
        Vector2 position,
        int cursorIndex,
        int selectionStart,
        Color normalTextColor,
        Color selectionTextColor,
        Color selectionBackgroundColor)
    {
        if (!HasTextSelection(cursorIndex, selectionStart))
        {
            _spriteBatch.DrawString(font, GetTextWithCursor(text, cursorIndex), position, normalTextColor);
            return;
        }

        var (start, length) = GetTextSelectionRange(cursorIndex, selectionStart);
        if (start < 0 || start >= text.Length || length <= 0)
        {
            _spriteBatch.DrawString(font, GetTextWithCursor(text, cursorIndex), position, normalTextColor);
            return;
        }

        var before = text[..start];
        var selected = text.Substring(start, length);
        var after = text[(start + length)..];
        _spriteBatch.DrawString(font, before, position, normalTextColor);

        var selectionX = position.X + font.MeasureString(before).X;
        var selectionWidth = font.MeasureString(selected).X;
        var selectionHeight = font.MeasureString("M").Y;
        _spriteBatch.Draw(
            _pixel,
            new Rectangle(
                (int)MathF.Floor(selectionX),
                (int)MathF.Floor(position.Y),
                Math.Max(1, (int)MathF.Ceiling(selectionWidth)),
                (int)MathF.Ceiling(selectionHeight)),
            selectionBackgroundColor);
        _spriteBatch.DrawString(font, selected, new Vector2(selectionX, position.Y), selectionTextColor);
        _spriteBatch.DrawString(font, after, new Vector2(selectionX + selectionWidth, position.Y), normalTextColor);
    }

    private static double GetCurrentTimeSeconds()
        => Environment.TickCount64 / 1000.0;

    private bool IsTextFieldDoubleClick(TextFieldClickTarget target)
    {
        var now = GetCurrentTimeSeconds();
        var isDoubleClick = target != TextFieldClickTarget.None
            && _lastTextInputClickTarget == target
            && (now - _lastTextInputClickTimeSeconds) <= TextInputDoubleClickThresholdSeconds;

        _lastTextInputClickTarget = target;
        _lastTextInputClickTimeSeconds = now;
        return isDoubleClick;
    }

    private static bool IsTextArrowRepeatTick(bool keyDown, bool keyPressed, ref ArrowKeyRepeatState state, double elapsedSeconds)
    {
        if (keyPressed)
        {
            state.IsHeld = true;
            state.HeldTimeSeconds = 0;
            state.RepeatTimerSeconds = TextInputArrowRepeatDelaySeconds;
            return true;
        }

        if (!keyDown)
        {
            state.IsHeld = false;
            return false;
        }

        if (!state.IsHeld)
        {
            return false;
        }

        state.HeldTimeSeconds += elapsedSeconds;
        state.RepeatTimerSeconds -= elapsedSeconds;
        if (state.HeldTimeSeconds >= TextInputArrowRepeatDelaySeconds && state.RepeatTimerSeconds <= 0)
        {
            state.RepeatTimerSeconds += TextInputArrowRepeatIntervalSeconds;
            return true;
        }

        return false;
    }

    private void SelectAllTextInActiveField(TextFieldClickTarget clickTarget)
    {
        switch (clickTarget)
        {
            case TextFieldClickTarget.ManualConnectHost:
                _connectHostCursorIndex = _connectHostBuffer.Length;
                _connectHostSelectionStart = 0;
                break;
            case TextFieldClickTarget.ManualConnectPort:
                _connectPortCursorIndex = _connectPortBuffer.Length;
                _connectPortSelectionStart = 0;
                break;
            case TextFieldClickTarget.FriendsCode:
                _friendCodeCursorIndex = _friendCodeInputBuffer.Length;
                _friendCodeSelectionStart = 0;
                break;
            case TextFieldClickTarget.FriendsNickname:
                _friendNicknameCursorIndex = _friendNicknameInputBuffer.Length;
                _friendNicknameSelectionStart = 0;
                break;
            case TextFieldClickTarget.FriendsMessage:
                _friendMessageCursorIndex = _friendMessageInputBuffer.Length;
                _friendMessageSelectionStart = 0;
                break;
            case TextFieldClickTarget.OptionsPlayerName:
                _playerNameEditCursorIndex = _playerNameEditBuffer.Length;
                _playerNameEditSelectionStart = 0;
                break;
            case TextFieldClickTarget.HostSetupServerName:
                _hostServerNameCursorIndex = _hostServerNameBuffer.Length;
                _hostServerNameSelectionStart = 0;
                break;
            case TextFieldClickTarget.HostSetupPort:
                _hostPortCursorIndex = _hostPortBuffer.Length;
                _hostPortSelectionStart = 0;
                break;
            case TextFieldClickTarget.HostSetupSlots:
                _hostSlotsCursorIndex = _hostSlotsBuffer.Length;
                _hostSlotsSelectionStart = 0;
                break;
            case TextFieldClickTarget.HostSetupPassword:
                _hostPasswordCursorIndex = _hostPasswordBuffer.Length;
                _hostPasswordSelectionStart = 0;
                break;
            case TextFieldClickTarget.HostSetupRconPassword:
                _hostRconPasswordCursorIndex = _hostRconPasswordBuffer.Length;
                _hostRconPasswordSelectionStart = 0;
                break;
            case TextFieldClickTarget.HostSetupMapRotationFile:
                _hostMapRotationFileCursorIndex = _hostMapRotationFileBuffer.Length;
                _hostMapRotationFileSelectionStart = 0;
                break;
            case TextFieldClickTarget.HostSetupTimeLimit:
                _hostTimeLimitCursorIndex = _hostTimeLimitBuffer.Length;
                _hostTimeLimitSelectionStart = 0;
                break;
            case TextFieldClickTarget.HostSetupCapLimit:
                _hostCapLimitCursorIndex = _hostCapLimitBuffer.Length;
                _hostCapLimitSelectionStart = 0;
                break;
            case TextFieldClickTarget.HostSetupRespawnSeconds:
                _hostRespawnSecondsCursorIndex = _hostRespawnSecondsBuffer.Length;
                _hostRespawnSecondsSelectionStart = 0;
                break;
            case TextFieldClickTarget.HostSetupAdvancedCvar:
                _hostSetupState.AdvancedCvarCursorIndex = _hostSetupState.GetActiveAdvancedCvarEditBuffer().Length;
                _hostSetupState.AdvancedCvarSelectionStart = 0;
                break;
            case TextFieldClickTarget.HostSetupConsoleCommand:
                _hostedServerConsole.CommandInputCursorIndex = _hostedServerConsole.CommandInput.Length;
                _hostedServerConsole.CommandInputSelectionStart = 0;
                break;
            case TextFieldClickTarget.None:
            default:
                break;
        }
    }

    private void ResetTextFieldClickTarget()
    {
        _lastTextInputClickTarget = TextFieldClickTarget.None;
        _lastTextInputClickTimeSeconds = 0;
    }

    private bool HandleActiveTextFieldClipboardShortcuts(KeyboardState keyboard)
    {
        var ctrlHeld = keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl);
        if (!ctrlHeld)
        {
            return false;
        }

        if (IsKeyPressed(keyboard, Keys.C))
        {
            return CopyActiveSelection();
        }

        if (IsKeyPressed(keyboard, Keys.X))
        {
            return CutActiveSelection();
        }

        if (IsKeyPressed(keyboard, Keys.V))
        {
            return PasteActiveClipboard();
        }

        if (IsKeyPressed(keyboard, Keys.A))
        {
            return TrySelectAllActiveTextField();
        }

        return false;
    }

    private bool CopyActiveSelection()
    {
        string selectedText;
        if (_passwordPromptOpen)
        {
            selectedText = GetSelectedText(_passwordEditBuffer, _passwordEditCursorIndex, _passwordEditSelectionStart);
        }
        else if (_mainMenuOpen && _manualConnectOpen && _editingConnectHost)
        {
            selectedText = GetSelectedText(_connectHostBuffer, _connectHostCursorIndex, _connectHostSelectionStart);
        }
        else if (_mainMenuOpen && _manualConnectOpen && _editingConnectPort)
        {
            selectedText = GetSelectedText(_connectPortBuffer, _connectPortCursorIndex, _connectPortSelectionStart);
        }
        else if (_mainMenuOpen && _friendsMenuOpen && _editingFriendCode)
        {
            selectedText = GetSelectedText(_friendCodeInputBuffer, _friendCodeCursorIndex, _friendCodeSelectionStart);
        }
        else if (_mainMenuOpen && _friendsMenuOpen && _editingFriendNickname)
        {
            selectedText = GetSelectedText(_friendNicknameInputBuffer, _friendNicknameCursorIndex, _friendNicknameSelectionStart);
        }
        else if (_mainMenuOpen && _friendsMenuOpen && _editingFriendMessage)
        {
            selectedText = GetSelectedText(_friendMessageInputBuffer, _friendMessageCursorIndex, _friendMessageSelectionStart);
        }
        else if (_optionsMenuOpen && _editingPlayerName)
        {
            selectedText = GetSelectedText(_playerNameEditBuffer, _playerNameEditCursorIndex, _playerNameEditSelectionStart);
        }
        else if (_mainMenuOpen && _hostSetupOpen && _hostSetupEditField == HostSetupEditField.ServerName)
        {
            selectedText = GetSelectedText(_hostServerNameBuffer, _hostServerNameCursorIndex, _hostServerNameSelectionStart);
        }
        else if (_mainMenuOpen && _hostSetupOpen && _hostSetupEditField == HostSetupEditField.Port)
        {
            selectedText = GetSelectedText(_hostPortBuffer, _hostPortCursorIndex, _hostPortSelectionStart);
        }
        else if (_mainMenuOpen && _hostSetupOpen && _hostSetupEditField == HostSetupEditField.Slots)
        {
            selectedText = GetSelectedText(_hostSlotsBuffer, _hostSlotsCursorIndex, _hostSlotsSelectionStart);
        }
        else if (_mainMenuOpen && _hostSetupOpen && _hostSetupEditField == HostSetupEditField.Password)
        {
            selectedText = GetSelectedText(_hostPasswordBuffer, _hostPasswordCursorIndex, _hostPasswordSelectionStart);
        }
        else if (_mainMenuOpen && _hostSetupOpen && _hostSetupEditField == HostSetupEditField.RconPassword)
        {
            selectedText = GetSelectedText(_hostRconPasswordBuffer, _hostRconPasswordCursorIndex, _hostRconPasswordSelectionStart);
        }
        else if (_mainMenuOpen && _hostSetupOpen && _hostSetupEditField == HostSetupEditField.MapRotationFile && _hostUsePlaylistFile)
        {
            selectedText = GetSelectedText(_hostMapRotationFileBuffer, _hostMapRotationFileCursorIndex, _hostMapRotationFileSelectionStart);
        }
        else if (_mainMenuOpen && _hostSetupOpen && _hostSetupEditField == HostSetupEditField.TimeLimit)
        {
            selectedText = GetSelectedText(_hostTimeLimitBuffer, _hostTimeLimitCursorIndex, _hostTimeLimitSelectionStart);
        }
        else if (_mainMenuOpen && _hostSetupOpen && _hostSetupEditField == HostSetupEditField.CapLimit)
        {
            selectedText = GetSelectedText(_hostCapLimitBuffer, _hostCapLimitCursorIndex, _hostCapLimitSelectionStart);
        }
        else if (_mainMenuOpen && _hostSetupOpen && _hostSetupEditField == HostSetupEditField.RespawnSeconds)
        {
            selectedText = GetSelectedText(_hostRespawnSecondsBuffer, _hostRespawnSecondsCursorIndex, _hostRespawnSecondsSelectionStart);
        }
        else if (_mainMenuOpen && _hostSetupOpen && _hostSetupEditField == HostSetupEditField.AdvancedCvar)
        {
            var buffer = _hostSetupState.GetActiveAdvancedCvarEditBuffer();
            selectedText = GetSelectedText(buffer, _hostSetupState.AdvancedCvarCursorIndex, _hostSetupState.AdvancedCvarSelectionStart);
        }
        else if (_mainMenuOpen && _hostSetupOpen && _hostSetupTab == HostSetupTab.ServerConsole && _hostSetupEditField == HostSetupEditField.ServerConsoleCommand)
        {
            var commandInput = _hostedServerConsole.CommandInput;
            selectedText = GetSelectedText(commandInput, _hostedServerConsole.CommandInputCursorIndex, _hostedServerConsole.CommandInputSelectionStart);
        }
        else if (_chatOpen)
        {
            selectedText = GetSelectedText(_chatInput, _chatInputCursorIndex, _chatInputSelectionStart);
        }
        else if (_consoleOpen)
        {
            selectedText = GetSelectedText(_consoleInput, _consoleInputCursorIndex, _consoleInputSelectionStart);
        }
        else
        {
            return false;
        }

        if (string.IsNullOrEmpty(selectedText))
        {
            return false;
        }

        return TrySetClipboardText(selectedText);
    }

    private bool TrySelectAllActiveTextField()
    {
        if (_passwordPromptOpen)
        {
            _passwordEditCursorIndex = _passwordEditBuffer.Length;
            _passwordEditSelectionStart = 0;
            return true;
        }

        if (_chatOpen)
        {
            _chatInputCursorIndex = _chatInput.Length;
            _chatInputSelectionStart = 0;
            return true;
        }

        if (_consoleOpen)
        {
            _consoleInputCursorIndex = _consoleInput.Length;
            _consoleInputSelectionStart = 0;
            return true;
        }

        var clickTarget = GetActiveTextFieldClickTarget();
        if (clickTarget == TextFieldClickTarget.None)
        {
            return false;
        }

        SelectAllTextInActiveField(clickTarget);
        return true;
    }

    private TextFieldClickTarget GetActiveTextFieldClickTarget()
    {
        if (_mainMenuOpen && _manualConnectOpen)
        {
            if (_editingConnectHost)
            {
                return TextFieldClickTarget.ManualConnectHost;
            }

            if (_editingConnectPort)
            {
                return TextFieldClickTarget.ManualConnectPort;
            }
        }

        if (_optionsMenuOpen && _editingPlayerName)
        {
            return TextFieldClickTarget.OptionsPlayerName;
        }

        if (_mainMenuOpen && _friendsMenuOpen && _editingFriendCode)
        {
            return TextFieldClickTarget.FriendsCode;
        }

        if (_mainMenuOpen && _friendsMenuOpen && _editingFriendNickname)
        {
            return TextFieldClickTarget.FriendsNickname;
        }

        if (_mainMenuOpen && _friendsMenuOpen && _editingFriendMessage)
        {
            return TextFieldClickTarget.FriendsMessage;
        }

        if (_mainMenuOpen && _hostSetupOpen)
        {
            return _hostSetupEditField switch
            {
                HostSetupEditField.ServerName => TextFieldClickTarget.HostSetupServerName,
                HostSetupEditField.Port => TextFieldClickTarget.HostSetupPort,
                HostSetupEditField.Slots => TextFieldClickTarget.HostSetupSlots,
                HostSetupEditField.Password => TextFieldClickTarget.HostSetupPassword,
                HostSetupEditField.RconPassword => TextFieldClickTarget.HostSetupRconPassword,
                HostSetupEditField.MapRotationFile => TextFieldClickTarget.HostSetupMapRotationFile,
                HostSetupEditField.TimeLimit => TextFieldClickTarget.HostSetupTimeLimit,
                HostSetupEditField.CapLimit => TextFieldClickTarget.HostSetupCapLimit,
                HostSetupEditField.RespawnSeconds => TextFieldClickTarget.HostSetupRespawnSeconds,
                HostSetupEditField.AdvancedCvar => TextFieldClickTarget.HostSetupAdvancedCvar,
                HostSetupEditField.ServerConsoleCommand when _hostSetupTab == HostSetupTab.ServerConsole => TextFieldClickTarget.HostSetupConsoleCommand,
                _ => TextFieldClickTarget.None,
            };
        }

        return TextFieldClickTarget.None;
    }

    private bool CutActiveSelection()
    {
        if (_passwordPromptOpen)
        {
            if (CutSelectionFromField(
                _passwordEditBuffer,
                _passwordEditCursorIndex,
                _passwordEditSelectionStart,
                out var newText,
                out var newCursorIndex,
                out var newSelectionStart))
            {
                _passwordEditBuffer = newText;
                _passwordEditCursorIndex = newCursorIndex;
                _passwordEditSelectionStart = newSelectionStart;
                return true;
            }

            return false;
        }

        if (_mainMenuOpen && _manualConnectOpen && _editingConnectHost)
        {
            if (CutSelectionFromField(
                _connectHostBuffer,
                _connectHostCursorIndex,
                _connectHostSelectionStart,
                out var newText,
                out var newCursorIndex,
                out var newSelectionStart))
            {
                _connectHostBuffer = newText;
                _connectHostCursorIndex = newCursorIndex;
                _connectHostSelectionStart = newSelectionStart;
                return true;
            }

            return false;
        }

        if (_mainMenuOpen && _manualConnectOpen && _editingConnectPort)
        {
            if (CutSelectionFromField(
                _connectPortBuffer,
                _connectPortCursorIndex,
                _connectPortSelectionStart,
                out var newText,
                out var newCursorIndex,
                out var newSelectionStart))
            {
                _connectPortBuffer = newText;
                _connectPortCursorIndex = newCursorIndex;
                _connectPortSelectionStart = newSelectionStart;
                return true;
            }

            return false;
        }

        if (_mainMenuOpen && _friendsMenuOpen && _editingFriendCode)
        {
            if (CutSelectionFromField(
                _friendCodeInputBuffer,
                _friendCodeCursorIndex,
                _friendCodeSelectionStart,
                out var newText,
                out var newCursorIndex,
                out var newSelectionStart))
            {
                _friendCodeInputBuffer = newText;
                _friendCodeCursorIndex = newCursorIndex;
                _friendCodeSelectionStart = newSelectionStart;
                return true;
            }

            return false;
        }

        if (_mainMenuOpen && _friendsMenuOpen && _editingFriendNickname)
        {
            if (CutSelectionFromField(
                _friendNicknameInputBuffer,
                _friendNicknameCursorIndex,
                _friendNicknameSelectionStart,
                out var newText,
                out var newCursorIndex,
                out var newSelectionStart))
            {
                _friendNicknameInputBuffer = newText;
                _friendNicknameCursorIndex = newCursorIndex;
                _friendNicknameSelectionStart = newSelectionStart;
                return true;
            }

            return false;
        }

        if (_mainMenuOpen && _friendsMenuOpen && _editingFriendMessage)
        {
            if (CutSelectionFromField(
                _friendMessageInputBuffer,
                _friendMessageCursorIndex,
                _friendMessageSelectionStart,
                out var newText,
                out var newCursorIndex,
                out var newSelectionStart))
            {
                _friendMessageInputBuffer = newText;
                _friendMessageCursorIndex = newCursorIndex;
                _friendMessageSelectionStart = newSelectionStart;
                return true;
            }

            return false;
        }

        if (_optionsMenuOpen && _editingPlayerName)
        {
            if (CutSelectionFromField(
                _playerNameEditBuffer,
                _playerNameEditCursorIndex,
                _playerNameEditSelectionStart,
                out var newText,
                out var newCursorIndex,
                out var newSelectionStart))
            {
                _playerNameEditBuffer = newText;
                _playerNameEditCursorIndex = newCursorIndex;
                _playerNameEditSelectionStart = newSelectionStart;
                return true;
            }

            return false;
        }

        if (_mainMenuOpen && _hostSetupOpen && _hostSetupEditField == HostSetupEditField.ServerName)
        {
            if (CutSelectionFromField(
                _hostServerNameBuffer,
                _hostServerNameCursorIndex,
                _hostServerNameSelectionStart,
                out var newText,
                out var newCursorIndex,
                out var newSelectionStart))
            {
                _hostServerNameBuffer = newText;
                _hostServerNameCursorIndex = newCursorIndex;
                _hostServerNameSelectionStart = newSelectionStart;
                return true;
            }

            return false;
        }

        if (_mainMenuOpen && _hostSetupOpen && _hostSetupEditField == HostSetupEditField.Port)
        {
            if (CutSelectionFromField(
                _hostPortBuffer,
                _hostPortCursorIndex,
                _hostPortSelectionStart,
                out var newText,
                out var newCursorIndex,
                out var newSelectionStart))
            {
                _hostPortBuffer = newText;
                _hostPortCursorIndex = newCursorIndex;
                _hostPortSelectionStart = newSelectionStart;
                return true;
            }

            return false;
        }

        if (_mainMenuOpen && _hostSetupOpen && _hostSetupEditField == HostSetupEditField.Slots)
        {
            if (CutSelectionFromField(
                _hostSlotsBuffer,
                _hostSlotsCursorIndex,
                _hostSlotsSelectionStart,
                out var newText,
                out var newCursorIndex,
                out var newSelectionStart))
            {
                _hostSlotsBuffer = newText;
                _hostSlotsCursorIndex = newCursorIndex;
                _hostSlotsSelectionStart = newSelectionStart;
                return true;
            }

            return false;
        }

        if (_mainMenuOpen && _hostSetupOpen && _hostSetupEditField == HostSetupEditField.Password)
        {
            if (CutSelectionFromField(
                _hostPasswordBuffer,
                _hostPasswordCursorIndex,
                _hostPasswordSelectionStart,
                out var newText,
                out var newCursorIndex,
                out var newSelectionStart))
            {
                _hostPasswordBuffer = newText;
                _hostPasswordCursorIndex = newCursorIndex;
                _hostPasswordSelectionStart = newSelectionStart;
                return true;
            }

            return false;
        }

        if (_mainMenuOpen && _hostSetupOpen && _hostSetupEditField == HostSetupEditField.RconPassword)
        {
            if (CutSelectionFromField(
                _hostRconPasswordBuffer,
                _hostRconPasswordCursorIndex,
                _hostRconPasswordSelectionStart,
                out var newText,
                out var newCursorIndex,
                out var newSelectionStart))
            {
                _hostRconPasswordBuffer = newText;
                _hostRconPasswordCursorIndex = newCursorIndex;
                _hostRconPasswordSelectionStart = newSelectionStart;
                return true;
            }

            return false;
        }

        if (_mainMenuOpen && _hostSetupOpen && _hostSetupEditField == HostSetupEditField.MapRotationFile && _hostUsePlaylistFile)
        {
            if (CutSelectionFromField(
                _hostMapRotationFileBuffer,
                _hostMapRotationFileCursorIndex,
                _hostMapRotationFileSelectionStart,
                out var newText,
                out var newCursorIndex,
                out var newSelectionStart))
            {
                _hostMapRotationFileBuffer = newText;
                _hostMapRotationFileCursorIndex = newCursorIndex;
                _hostMapRotationFileSelectionStart = newSelectionStart;
                return true;
            }

            return false;
        }

        if (_mainMenuOpen && _hostSetupOpen && _hostSetupEditField == HostSetupEditField.TimeLimit)
        {
            if (CutSelectionFromField(
                _hostTimeLimitBuffer,
                _hostTimeLimitCursorIndex,
                _hostTimeLimitSelectionStart,
                out var newText,
                out var newCursorIndex,
                out var newSelectionStart))
            {
                _hostTimeLimitBuffer = newText;
                _hostTimeLimitCursorIndex = newCursorIndex;
                _hostTimeLimitSelectionStart = newSelectionStart;
                return true;
            }

            return false;
        }

        if (_mainMenuOpen && _hostSetupOpen && _hostSetupEditField == HostSetupEditField.CapLimit)
        {
            if (CutSelectionFromField(
                _hostCapLimitBuffer,
                _hostCapLimitCursorIndex,
                _hostCapLimitSelectionStart,
                out var newText,
                out var newCursorIndex,
                out var newSelectionStart))
            {
                _hostCapLimitBuffer = newText;
                _hostCapLimitCursorIndex = newCursorIndex;
                _hostCapLimitSelectionStart = newSelectionStart;
                return true;
            }

            return false;
        }

        if (_mainMenuOpen && _hostSetupOpen && _hostSetupEditField == HostSetupEditField.RespawnSeconds)
        {
            if (CutSelectionFromField(
                _hostRespawnSecondsBuffer,
                _hostRespawnSecondsCursorIndex,
                _hostRespawnSecondsSelectionStart,
                out var newText,
                out var newCursorIndex,
                out var newSelectionStart))
            {
                _hostRespawnSecondsBuffer = newText;
                _hostRespawnSecondsCursorIndex = newCursorIndex;
                _hostRespawnSecondsSelectionStart = newSelectionStart;
                return true;
            }

            return false;
        }

        if (_mainMenuOpen && _hostSetupOpen && _hostSetupEditField == HostSetupEditField.AdvancedCvar)
        {
            var buffer = _hostSetupState.GetActiveAdvancedCvarEditBuffer();
            if (CutSelectionFromField(
                buffer,
                _hostSetupState.AdvancedCvarCursorIndex,
                _hostSetupState.AdvancedCvarSelectionStart,
                out var newText,
                out var newCursorIndex,
                out var newSelectionStart))
            {
                _hostSetupState.SetActiveAdvancedCvarEditBuffer(newText);
                _hostSetupState.AdvancedCvarCursorIndex = newCursorIndex;
                _hostSetupState.AdvancedCvarSelectionStart = newSelectionStart;
                return true;
            }

            return false;
        }

        if (_mainMenuOpen && _hostSetupOpen && _hostSetupTab == HostSetupTab.ServerConsole && _hostSetupEditField == HostSetupEditField.ServerConsoleCommand)
        {
            if (CutSelectionFromField(
                _hostedServerConsole.CommandInput,
                _hostedServerConsole.CommandInputCursorIndex,
                _hostedServerConsole.CommandInputSelectionStart,
                out var newText,
                out var newCursorIndex,
                out var newSelectionStart))
            {
                _hostedServerConsole.CommandInput = newText;
                _hostedServerConsole.CommandInputCursorIndex = newCursorIndex;
                _hostedServerConsole.CommandInputSelectionStart = newSelectionStart;
                return true;
            }

            return false;
        }

        if (_chatOpen)
        {
            if (CutSelectionFromField(
                _chatInput,
                _chatInputCursorIndex,
                _chatInputSelectionStart,
                out var newText,
                out var newCursorIndex,
                out var newSelectionStart))
            {
                _chatInput = newText;
                _chatInputCursorIndex = newCursorIndex;
                _chatInputSelectionStart = newSelectionStart;
                return true;
            }

            return false;
        }

        if (_consoleOpen)
        {
            if (CutSelectionFromField(
                _consoleInput,
                _consoleInputCursorIndex,
                _consoleInputSelectionStart,
                out var newText,
                out var newCursorIndex,
                out var newSelectionStart))
            {
                _consoleInput = newText;
                _consoleInputCursorIndex = newCursorIndex;
                _consoleInputSelectionStart = newSelectionStart;
                return true;
            }

            return false;
        }

        return false;
    }

    private bool PasteActiveClipboard()
    {
        if (!TryGetClipboardText(out var pasteText) || string.IsNullOrEmpty(pasteText))
        {
            return false;
        }

        if (_passwordPromptOpen)
        {
            var result = InsertTextAtCursor(
                _passwordEditBuffer,
                pasteText,
                _passwordEditCursorIndex,
                _passwordEditSelectionStart,
                32,
                c => !char.IsControl(c));
            _passwordEditBuffer = result.Text;
            _passwordEditCursorIndex = result.CursorIndex;
            _passwordEditSelectionStart = result.SelectionStart;
            return true;
        }

        if (_mainMenuOpen && _manualConnectOpen && _editingConnectHost)
        {
            var result = InsertTextAtCursor(
                _connectHostBuffer,
                pasteText,
                _connectHostCursorIndex,
                _connectHostSelectionStart,
                64,
                c => !char.IsControl(c));
            _connectHostBuffer = result.Text;
            _connectHostCursorIndex = result.CursorIndex;
            _connectHostSelectionStart = result.SelectionStart;
            return true;
        }

        if (_mainMenuOpen && _manualConnectOpen && _editingConnectPort)
        {
            var result = InsertTextAtCursor(
                _connectPortBuffer,
                pasteText,
                _connectPortCursorIndex,
                _connectPortSelectionStart,
                5,
                c => char.IsDigit(c));
            _connectPortBuffer = result.Text;
            _connectPortCursorIndex = result.CursorIndex;
            _connectPortSelectionStart = result.SelectionStart;
            return true;
        }

        if (_mainMenuOpen && _friendsMenuOpen && _editingFriendCode)
        {
            var result = InsertTextAtCursor(
                _friendCodeInputBuffer,
                pasteText,
                _friendCodeCursorIndex,
                _friendCodeSelectionStart,
                20,
                c => char.IsAsciiLetterOrDigit(c) || c == '-');
            _friendCodeInputBuffer = result.Text.ToUpperInvariant();
            _friendCodeCursorIndex = result.CursorIndex;
            _friendCodeSelectionStart = result.SelectionStart;
            return true;
        }

        if (_mainMenuOpen && _friendsMenuOpen && _editingFriendNickname)
        {
            var result = InsertTextAtCursor(
                _friendNicknameInputBuffer,
                pasteText,
                _friendNicknameCursorIndex,
                _friendNicknameSelectionStart,
                20,
                c => !char.IsControl(c) && c != '#');
            _friendNicknameInputBuffer = result.Text;
            _friendNicknameCursorIndex = result.CursorIndex;
            _friendNicknameSelectionStart = result.SelectionStart;
            return true;
        }

        if (_mainMenuOpen && _friendsMenuOpen && _editingFriendMessage)
        {
            var result = InsertTextAtCursor(
                _friendMessageInputBuffer,
                pasteText,
                _friendMessageCursorIndex,
                _friendMessageSelectionStart,
                500,
                c => !char.IsControl(c));
            _friendMessageInputBuffer = result.Text;
            _friendMessageCursorIndex = result.CursorIndex;
            _friendMessageSelectionStart = result.SelectionStart;
            return true;
        }

        if (_optionsMenuOpen && _editingPlayerName)
        {
            var result = InsertTextAtCursor(
                _playerNameEditBuffer,
                pasteText,
                _playerNameEditCursorIndex,
                _playerNameEditSelectionStart,
                20,
                c => !char.IsControl(c) && c != '#');
            _playerNameEditBuffer = result.Text;
            _playerNameEditCursorIndex = result.CursorIndex;
            _playerNameEditSelectionStart = result.SelectionStart;
            return true;
        }

        if (_mainMenuOpen && _hostSetupOpen && _hostSetupEditField == HostSetupEditField.ServerName)
        {
            var result = InsertTextAtCursor(
                _hostServerNameBuffer,
                pasteText,
                _hostServerNameCursorIndex,
                _hostServerNameSelectionStart,
                32,
                c => !char.IsControl(c));
            _hostServerNameBuffer = result.Text;
            _hostServerNameCursorIndex = result.CursorIndex;
            _hostServerNameSelectionStart = result.SelectionStart;
            return true;
        }

        if (_mainMenuOpen && _hostSetupOpen && _hostSetupEditField == HostSetupEditField.Port)
        {
            var result = InsertTextAtCursor(
                _hostPortBuffer,
                pasteText,
                _hostPortCursorIndex,
                _hostPortSelectionStart,
                5,
                c => char.IsDigit(c));
            _hostPortBuffer = result.Text;
            _hostPortCursorIndex = result.CursorIndex;
            _hostPortSelectionStart = result.SelectionStart;
            return true;
        }

        if (_mainMenuOpen && _hostSetupOpen && _hostSetupEditField == HostSetupEditField.Slots)
        {
            var result = InsertTextAtCursor(
                _hostSlotsBuffer,
                pasteText,
                _hostSlotsCursorIndex,
                _hostSlotsSelectionStart,
                2,
                c => char.IsDigit(c));
            _hostSlotsBuffer = result.Text;
            _hostSlotsCursorIndex = result.CursorIndex;
            _hostSlotsSelectionStart = result.SelectionStart;
            return true;
        }

        if (_mainMenuOpen && _hostSetupOpen && _hostSetupEditField == HostSetupEditField.Password)
        {
            var result = InsertTextAtCursor(
                _hostPasswordBuffer,
                pasteText,
                _hostPasswordCursorIndex,
                _hostPasswordSelectionStart,
                32,
                c => !char.IsControl(c));
            _hostPasswordBuffer = result.Text;
            _hostPasswordCursorIndex = result.CursorIndex;
            _hostPasswordSelectionStart = result.SelectionStart;
            return true;
        }

        if (_mainMenuOpen && _hostSetupOpen && _hostSetupEditField == HostSetupEditField.RconPassword)
        {
            var result = InsertTextAtCursor(
                _hostRconPasswordBuffer,
                pasteText,
                _hostRconPasswordCursorIndex,
                _hostRconPasswordSelectionStart,
                64,
                c => !char.IsControl(c));
            _hostRconPasswordBuffer = result.Text;
            _hostRconPasswordCursorIndex = result.CursorIndex;
            _hostRconPasswordSelectionStart = result.SelectionStart;
            return true;
        }

        if (_mainMenuOpen && _hostSetupOpen && _hostSetupEditField == HostSetupEditField.MapRotationFile && _hostUsePlaylistFile)
        {
            var result = InsertTextAtCursor(
                _hostMapRotationFileBuffer,
                pasteText,
                _hostMapRotationFileCursorIndex,
                _hostMapRotationFileSelectionStart,
                180,
                c => !char.IsControl(c));
            _hostMapRotationFileBuffer = result.Text;
            _hostMapRotationFileCursorIndex = result.CursorIndex;
            _hostMapRotationFileSelectionStart = result.SelectionStart;
            return true;
        }

        if (_mainMenuOpen && _hostSetupOpen && _hostSetupEditField == HostSetupEditField.TimeLimit)
        {
            var result = InsertTextAtCursor(
                _hostTimeLimitBuffer,
                pasteText,
                _hostTimeLimitCursorIndex,
                _hostTimeLimitSelectionStart,
                3,
                c => char.IsDigit(c));
            _hostTimeLimitBuffer = result.Text;
            _hostTimeLimitCursorIndex = result.CursorIndex;
            _hostTimeLimitSelectionStart = result.SelectionStart;
            return true;
        }

        if (_mainMenuOpen && _hostSetupOpen && _hostSetupEditField == HostSetupEditField.CapLimit)
        {
            var result = InsertTextAtCursor(
                _hostCapLimitBuffer,
                pasteText,
                _hostCapLimitCursorIndex,
                _hostCapLimitSelectionStart,
                3,
                c => char.IsDigit(c));
            _hostCapLimitBuffer = result.Text;
            _hostCapLimitCursorIndex = result.CursorIndex;
            _hostCapLimitSelectionStart = result.SelectionStart;
            return true;
        }

        if (_mainMenuOpen && _hostSetupOpen && _hostSetupEditField == HostSetupEditField.RespawnSeconds)
        {
            var result = InsertTextAtCursor(
                _hostRespawnSecondsBuffer,
                pasteText,
                _hostRespawnSecondsCursorIndex,
                _hostRespawnSecondsSelectionStart,
                3,
                c => char.IsDigit(c));
            _hostRespawnSecondsBuffer = result.Text;
            _hostRespawnSecondsCursorIndex = result.CursorIndex;
            _hostRespawnSecondsSelectionStart = result.SelectionStart;
            return true;
        }

        if (_mainMenuOpen && _hostSetupOpen && _hostSetupEditField == HostSetupEditField.AdvancedCvar)
        {
            var buffer = _hostSetupState.GetActiveAdvancedCvarEditBuffer();
            if (!HostSetupServerCvarCatalog.TryGetDefinition(_hostSetupState.ActiveAdvancedCvarName ?? string.Empty, out var definition))
            {
                return false;
            }

            var result = InsertTextAtCursor(
                buffer,
                pasteText,
                _hostSetupState.AdvancedCvarCursorIndex,
                _hostSetupState.AdvancedCvarSelectionStart,
                24,
                c => HostSetupServerCvarCatalog.IsValidInputCharacter(definition, c));
            _hostSetupState.SetActiveAdvancedCvarEditBuffer(result.Text);
            _hostSetupState.AdvancedCvarCursorIndex = result.CursorIndex;
            _hostSetupState.AdvancedCvarSelectionStart = result.SelectionStart;
            return true;
        }

        if (_mainMenuOpen && _hostSetupOpen && _hostSetupTab == HostSetupTab.ServerConsole && _hostSetupEditField == HostSetupEditField.ServerConsoleCommand)
        {
            var result = InsertTextAtCursor(
                _hostedServerConsole.CommandInput,
                pasteText,
                _hostedServerConsole.CommandInputCursorIndex,
                _hostedServerConsole.CommandInputSelectionStart,
                120,
                c => !char.IsControl(c));
            _hostedServerConsole.CommandInput = result.Text;
            _hostedServerConsole.CommandInputCursorIndex = result.CursorIndex;
            _hostedServerConsole.CommandInputSelectionStart = result.SelectionStart;
            return true;
        }

        if (_chatOpen)
        {
            var result = InsertTextAtCursor(
                _chatInput,
                pasteText,
                _chatInputCursorIndex,
                _chatInputSelectionStart,
                120,
                c => !char.IsControl(c));
            _chatInput = result.Text;
            _chatInputCursorIndex = result.CursorIndex;
            _chatInputSelectionStart = result.SelectionStart;
            return true;
        }

        if (_consoleOpen)
        {
            var result = InsertTextAtCursor(
                _consoleInput,
                pasteText,
                _consoleInputCursorIndex,
                _consoleInputSelectionStart,
                int.MaxValue,
                c => !char.IsControl(c));
            _consoleInput = result.Text;
            _consoleInputCursorIndex = result.CursorIndex;
            _consoleInputSelectionStart = result.SelectionStart;
            return true;
        }

        return false;
    }

    private void HandleHostSetupFieldBackspace()
    {
        if (!_mainMenuOpen || !_hostSetupOpen)
        {
            return;
        }

        switch (_hostSetupEditField)
        {
            case HostSetupEditField.ServerName:
            {
                var result = DeleteTextSelectionOrBackspace(_hostServerNameBuffer, _hostServerNameCursorIndex, _hostServerNameSelectionStart);
                _hostServerNameBuffer = result.Text;
                _hostServerNameCursorIndex = result.CursorIndex;
                _hostServerNameSelectionStart = result.SelectionStart;
                break;
            }
            case HostSetupEditField.Port:
            {
                var result = DeleteTextSelectionOrBackspace(_hostPortBuffer, _hostPortCursorIndex, _hostPortSelectionStart);
                _hostPortBuffer = result.Text;
                _hostPortCursorIndex = result.CursorIndex;
                _hostPortSelectionStart = result.SelectionStart;
                break;
            }
            case HostSetupEditField.Slots:
            {
                var result = DeleteTextSelectionOrBackspace(_hostSlotsBuffer, _hostSlotsCursorIndex, _hostSlotsSelectionStart);
                _hostSlotsBuffer = result.Text;
                _hostSlotsCursorIndex = result.CursorIndex;
                _hostSlotsSelectionStart = result.SelectionStart;
                break;
            }
            case HostSetupEditField.Password:
            {
                var result = DeleteTextSelectionOrBackspace(_hostPasswordBuffer, _hostPasswordCursorIndex, _hostPasswordSelectionStart);
                _hostPasswordBuffer = result.Text;
                _hostPasswordCursorIndex = result.CursorIndex;
                _hostPasswordSelectionStart = result.SelectionStart;
                break;
            }
            case HostSetupEditField.RconPassword:
            {
                var result = DeleteTextSelectionOrBackspace(_hostRconPasswordBuffer, _hostRconPasswordCursorIndex, _hostRconPasswordSelectionStart);
                _hostRconPasswordBuffer = result.Text;
                _hostRconPasswordCursorIndex = result.CursorIndex;
                _hostRconPasswordSelectionStart = result.SelectionStart;
                break;
            }
            case HostSetupEditField.MapRotationFile when _hostUsePlaylistFile:
            {
                var result = DeleteTextSelectionOrBackspace(_hostMapRotationFileBuffer, _hostMapRotationFileCursorIndex, _hostMapRotationFileSelectionStart);
                _hostMapRotationFileBuffer = result.Text;
                _hostMapRotationFileCursorIndex = result.CursorIndex;
                _hostMapRotationFileSelectionStart = result.SelectionStart;
                break;
            }
            case HostSetupEditField.TimeLimit:
            {
                var result = DeleteTextSelectionOrBackspace(_hostTimeLimitBuffer, _hostTimeLimitCursorIndex, _hostTimeLimitSelectionStart);
                _hostTimeLimitBuffer = result.Text;
                _hostTimeLimitCursorIndex = result.CursorIndex;
                _hostTimeLimitSelectionStart = result.SelectionStart;
                break;
            }
            case HostSetupEditField.CapLimit:
            {
                var result = DeleteTextSelectionOrBackspace(_hostCapLimitBuffer, _hostCapLimitCursorIndex, _hostCapLimitSelectionStart);
                _hostCapLimitBuffer = result.Text;
                _hostCapLimitCursorIndex = result.CursorIndex;
                _hostCapLimitSelectionStart = result.SelectionStart;
                break;
            }
            case HostSetupEditField.RespawnSeconds:
            {
                var result = DeleteTextSelectionOrBackspace(_hostRespawnSecondsBuffer, _hostRespawnSecondsCursorIndex, _hostRespawnSecondsSelectionStart);
                _hostRespawnSecondsBuffer = result.Text;
                _hostRespawnSecondsCursorIndex = result.CursorIndex;
                _hostRespawnSecondsSelectionStart = result.SelectionStart;
                break;
            }
            case HostSetupEditField.AdvancedCvar:
            {
                var buffer = _hostSetupState.GetActiveAdvancedCvarEditBuffer();
                var result = DeleteTextSelectionOrBackspace(buffer, _hostSetupState.AdvancedCvarCursorIndex, _hostSetupState.AdvancedCvarSelectionStart);
                _hostSetupState.SetActiveAdvancedCvarEditBuffer(result.Text);
                _hostSetupState.AdvancedCvarCursorIndex = result.CursorIndex;
                _hostSetupState.AdvancedCvarSelectionStart = result.SelectionStart;
                break;
            }
            case HostSetupEditField.MapNameFilter:
            {
                var result = DeleteTextSelectionOrBackspace(
                    _hostSetupState.AvailableMapNameFilterBuffer,
                    _hostSetupState.AvailableMapNameFilterCursorIndex,
                    _hostSetupState.AvailableMapNameFilterSelectionStart);
                _hostSetupState.AvailableMapNameFilterBuffer = result.Text;
                _hostSetupState.AvailableMapNameFilterCursorIndex = result.CursorIndex;
                _hostSetupState.AvailableMapNameFilterSelectionStart = result.SelectionStart;
                _hostSetupState.NotifyAvailableFiltersChanged();
                break;
            }
            case HostSetupEditField.ServerConsoleCommand:
            {
                var commandInput = _hostedServerConsole.CommandInput;
                var result = DeleteTextSelectionOrBackspace(commandInput, _hostedServerConsole.CommandInputCursorIndex, _hostedServerConsole.CommandInputSelectionStart);
                _hostedServerConsole.CommandInput = result.Text;
                _hostedServerConsole.CommandInputCursorIndex = result.CursorIndex;
                _hostedServerConsole.CommandInputSelectionStart = result.SelectionStart;
                break;
            }
        }
    }

    private void HandleHostSetupFieldCharacterInput(char character)
    {
        if (char.IsControl(character) || !_mainMenuOpen || !_hostSetupOpen)
        {
            return;
        }

        switch (_hostSetupEditField)
        {
            case HostSetupEditField.ServerName:
            {
                var result = InsertTextCharacterAtCursor(_hostServerNameBuffer, character, _hostServerNameCursorIndex, _hostServerNameSelectionStart, 32);
                _hostServerNameBuffer = result.Text;
                _hostServerNameCursorIndex = result.CursorIndex;
                _hostServerNameSelectionStart = result.SelectionStart;
                break;
            }
            case HostSetupEditField.Port:
            {
                var result = InsertTextAtCursor(_hostPortBuffer, character.ToString(), _hostPortCursorIndex, _hostPortSelectionStart, 5, c => char.IsDigit(c));
                _hostPortBuffer = result.Text;
                _hostPortCursorIndex = result.CursorIndex;
                _hostPortSelectionStart = result.SelectionStart;
                break;
            }
            case HostSetupEditField.Slots:
            {
                var result = InsertTextAtCursor(_hostSlotsBuffer, character.ToString(), _hostSlotsCursorIndex, _hostSlotsSelectionStart, 2, c => char.IsDigit(c));
                _hostSlotsBuffer = result.Text;
                _hostSlotsCursorIndex = result.CursorIndex;
                _hostSlotsSelectionStart = result.SelectionStart;
                break;
            }
            case HostSetupEditField.Password:
            {
                var result = InsertTextCharacterAtCursor(_hostPasswordBuffer, character, _hostPasswordCursorIndex, _hostPasswordSelectionStart, 32);
                _hostPasswordBuffer = result.Text;
                _hostPasswordCursorIndex = result.CursorIndex;
                _hostPasswordSelectionStart = result.SelectionStart;
                break;
            }
            case HostSetupEditField.RconPassword:
            {
                var result = InsertTextCharacterAtCursor(_hostRconPasswordBuffer, character, _hostRconPasswordCursorIndex, _hostRconPasswordSelectionStart, 64);
                _hostRconPasswordBuffer = result.Text;
                _hostRconPasswordCursorIndex = result.CursorIndex;
                _hostRconPasswordSelectionStart = result.SelectionStart;
                break;
            }
            case HostSetupEditField.MapRotationFile when _hostUsePlaylistFile:
            {
                var result = InsertTextCharacterAtCursor(_hostMapRotationFileBuffer, character, _hostMapRotationFileCursorIndex, _hostMapRotationFileSelectionStart, 180);
                _hostMapRotationFileBuffer = result.Text;
                _hostMapRotationFileCursorIndex = result.CursorIndex;
                _hostMapRotationFileSelectionStart = result.SelectionStart;
                break;
            }
            case HostSetupEditField.TimeLimit:
            {
                var result = InsertTextAtCursor(_hostTimeLimitBuffer, character.ToString(), _hostTimeLimitCursorIndex, _hostTimeLimitSelectionStart, 3, c => char.IsDigit(c));
                _hostTimeLimitBuffer = result.Text;
                _hostTimeLimitCursorIndex = result.CursorIndex;
                _hostTimeLimitSelectionStart = result.SelectionStart;
                break;
            }
            case HostSetupEditField.CapLimit:
            {
                var result = InsertTextAtCursor(_hostCapLimitBuffer, character.ToString(), _hostCapLimitCursorIndex, _hostCapLimitSelectionStart, 3, c => char.IsDigit(c));
                _hostCapLimitBuffer = result.Text;
                _hostCapLimitCursorIndex = result.CursorIndex;
                _hostCapLimitSelectionStart = result.SelectionStart;
                break;
            }
            case HostSetupEditField.RespawnSeconds:
            {
                var result = InsertTextAtCursor(_hostRespawnSecondsBuffer, character.ToString(), _hostRespawnSecondsCursorIndex, _hostRespawnSecondsSelectionStart, 3, c => char.IsDigit(c));
                _hostRespawnSecondsBuffer = result.Text;
                _hostRespawnSecondsCursorIndex = result.CursorIndex;
                _hostRespawnSecondsSelectionStart = result.SelectionStart;
                break;
            }
            case HostSetupEditField.AdvancedCvar:
            {
                if (!HostSetupServerCvarCatalog.TryGetDefinition(_hostSetupState.ActiveAdvancedCvarName ?? string.Empty, out var definition)
                    || !HostSetupServerCvarCatalog.IsValidInputCharacter(definition, character))
                {
                    break;
                }

                var buffer = _hostSetupState.GetActiveAdvancedCvarEditBuffer();
                var result = InsertTextAtCursor(
                    buffer,
                    character.ToString(),
                    _hostSetupState.AdvancedCvarCursorIndex,
                    _hostSetupState.AdvancedCvarSelectionStart,
                    24,
                    c => HostSetupServerCvarCatalog.IsValidInputCharacter(definition, c));
                _hostSetupState.SetActiveAdvancedCvarEditBuffer(result.Text);
                _hostSetupState.AdvancedCvarCursorIndex = result.CursorIndex;
                _hostSetupState.AdvancedCvarSelectionStart = result.SelectionStart;
                break;
            }
            case HostSetupEditField.MapNameFilter:
            {
                var result = InsertTextCharacterAtCursor(
                    _hostSetupState.AvailableMapNameFilterBuffer,
                    character,
                    _hostSetupState.AvailableMapNameFilterCursorIndex,
                    _hostSetupState.AvailableMapNameFilterSelectionStart,
                    48);
                _hostSetupState.AvailableMapNameFilterBuffer = result.Text;
                _hostSetupState.AvailableMapNameFilterCursorIndex = result.CursorIndex;
                _hostSetupState.AvailableMapNameFilterSelectionStart = result.SelectionStart;
                _hostSetupState.NotifyAvailableFiltersChanged();
                break;
            }
            case HostSetupEditField.ServerConsoleCommand:
            {
                var result = InsertTextCharacterAtCursor(_hostedServerConsole.CommandInput, character, _hostedServerConsole.CommandInputCursorIndex, _hostedServerConsole.CommandInputSelectionStart, 120);
                _hostedServerConsole.CommandInput = result.Text;
                _hostedServerConsole.CommandInputCursorIndex = result.CursorIndex;
                _hostedServerConsole.CommandInputSelectionStart = result.SelectionStart;
                break;
            }
        }
    }

    private static string GetSelectedText(string text, int cursorIndex, int selectionStart)
    {
        if (!HasTextSelection(cursorIndex, selectionStart))
        {
            return string.Empty;
        }

        var (start, length) = GetTextSelectionRange(cursorIndex, selectionStart);
        return text.Substring(start, length);
    }

    private bool CutSelectionFromField(string fieldText, int cursorIndex, int selectionStart, out string newText, out int newCursorIndex, out int newSelectionStart)
    {
        newText = fieldText;
        newCursorIndex = cursorIndex;
        newSelectionStart = selectionStart;

        var selectedText = GetSelectedText(fieldText, cursorIndex, selectionStart);
        if (string.IsNullOrEmpty(selectedText))
        {
            return false;
        }

        if (!TrySetClipboardText(selectedText))
        {
            return false;
        }

        var result = DeleteTextSelection(fieldText, cursorIndex, selectionStart);
        newText = result.Text;
        newCursorIndex = result.CursorIndex;
        newSelectionStart = result.SelectionStart;
        return true;
    }

    private static bool TryGetClipboardText(out string text)
    {
        text = string.Empty;
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            return TryGetClipboardTextWindows(out text);
        }
        catch
        {
            return false;
        }
    }

    private static bool TrySetClipboardText(string text)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            return TrySetClipboardTextWindows(text);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetClipboardTextWindows(out string text)
    {
        text = string.Empty;
        if (!OpenClipboard(IntPtr.Zero))
        {
            return false;
        }

        try
        {
            var handle = GetClipboardData(CF_UNICODETEXT);
            if (handle == IntPtr.Zero)
            {
                return false;
            }

            var pointer = GlobalLock(handle);
            if (pointer == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                text = Marshal.PtrToStringUni(pointer) ?? string.Empty;
                return true;
            }
            finally
            {
                GlobalUnlock(handle);
            }
        }
        finally
        {
            CloseClipboard();
        }
    }

    private static bool TrySetClipboardTextWindows(string text)
    {
        if (!OpenClipboard(IntPtr.Zero))
        {
            return false;
        }

        try
        {
            if (!EmptyClipboard())
            {
                return false;
            }

            var unicodeText = text ?? string.Empty;
            var bytes = (unicodeText.Length + 1) * 2;
            var global = GlobalAlloc(GMEM_MOVEABLE | GMEM_ZEROINIT, (UIntPtr)bytes);
            if (global == IntPtr.Zero)
            {
                return false;
            }

            var pointer = GlobalLock(global);
            if (pointer == IntPtr.Zero)
            {
                GlobalFree(global);
                return false;
            }

            try
            {
                Marshal.Copy(unicodeText.ToCharArray(), 0, pointer, unicodeText.Length);
                Marshal.WriteInt16(pointer, unicodeText.Length * 2, 0);
                if (SetClipboardData(CF_UNICODETEXT, global) == IntPtr.Zero)
                {
                    GlobalFree(global);
                    return false;
                }

                global = IntPtr.Zero;
                return true;
            }
            finally
            {
                GlobalUnlock(pointer);
            }
        }
        finally
        {
            CloseClipboard();
        }
    }

    private const uint CF_UNICODETEXT = 13;
    private const uint GMEM_MOVEABLE = 0x0002;
    private const uint GMEM_ZEROINIT = 0x0040;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetClipboardData(uint uFormat);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalUnlock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalFree(IntPtr hMem);

    private bool HandleActiveTextFieldKeyboardShortcuts(KeyboardState keyboard, double elapsedSeconds)
    {
        if (HandleActiveTextFieldClipboardShortcuts(keyboard))
        {
            return true;
        }

        var leftDown = keyboard.IsKeyDown(Keys.Left);
        var rightDown = keyboard.IsKeyDown(Keys.Right);
        var leftPressed = leftDown && !_previousKeyboard.IsKeyDown(Keys.Left);
        var rightPressed = rightDown && !_previousKeyboard.IsKeyDown(Keys.Right);
        var leftRepeat = IsTextArrowRepeatTick(leftDown, leftPressed, ref _textInputLeftArrowRepeatState, elapsedSeconds);
        var rightRepeat = IsTextArrowRepeatTick(rightDown, rightPressed, ref _textInputRightArrowRepeatState, elapsedSeconds);
        if (!leftRepeat && !rightRepeat)
        {
            return false;
        }

        var shiftHeld = IsShiftHeld(keyboard);

        if (_passwordPromptOpen)
        {
            if (leftRepeat)
            {
                var result = MoveTextCursorLeft(_passwordEditCursorIndex, _passwordEditSelectionStart, shiftHeld);
                _passwordEditCursorIndex = result.CursorIndex;
                _passwordEditSelectionStart = result.SelectionStart;
            }

            if (rightRepeat)
            {
                var result = MoveTextCursorRight(_passwordEditCursorIndex, _passwordEditSelectionStart, _passwordEditBuffer, shiftHeld);
                _passwordEditCursorIndex = result.CursorIndex;
                _passwordEditSelectionStart = result.SelectionStart;
            }

            return true;
        }

        if (_mainMenuOpen && _manualConnectOpen)
        {
            if (_editingConnectHost)
            {
                if (leftRepeat)
                {
                    var result = MoveTextCursorLeft(_connectHostCursorIndex, _connectHostSelectionStart, shiftHeld);
                    _connectHostCursorIndex = result.CursorIndex;
                    _connectHostSelectionStart = result.SelectionStart;
                }

                if (rightRepeat)
                {
                    var result = MoveTextCursorRight(_connectHostCursorIndex, _connectHostSelectionStart, _connectHostBuffer, shiftHeld);
                    _connectHostCursorIndex = result.CursorIndex;
                    _connectHostSelectionStart = result.SelectionStart;
                }

                return true;
            }

            if (_editingConnectPort)
            {
                if (leftRepeat)
                {
                    var result = MoveTextCursorLeft(_connectPortCursorIndex, _connectPortSelectionStart, shiftHeld);
                    _connectPortCursorIndex = result.CursorIndex;
                    _connectPortSelectionStart = result.SelectionStart;
                }

                if (rightRepeat)
                {
                    var result = MoveTextCursorRight(_connectPortCursorIndex, _connectPortSelectionStart, _connectPortBuffer, shiftHeld);
                    _connectPortCursorIndex = result.CursorIndex;
                    _connectPortSelectionStart = result.SelectionStart;
                }

                return true;
            }
        }

        if (_mainMenuOpen && _friendsMenuOpen)
        {
            if (_editingFriendCode)
            {
                if (leftRepeat)
                {
                    var result = MoveTextCursorLeft(_friendCodeCursorIndex, _friendCodeSelectionStart, shiftHeld);
                    _friendCodeCursorIndex = result.CursorIndex;
                    _friendCodeSelectionStart = result.SelectionStart;
                }

                if (rightRepeat)
                {
                    var result = MoveTextCursorRight(_friendCodeCursorIndex, _friendCodeSelectionStart, _friendCodeInputBuffer, shiftHeld);
                    _friendCodeCursorIndex = result.CursorIndex;
                    _friendCodeSelectionStart = result.SelectionStart;
                }

                return true;
            }

            if (_editingFriendNickname)
            {
                if (leftRepeat)
                {
                    var result = MoveTextCursorLeft(_friendNicknameCursorIndex, _friendNicknameSelectionStart, shiftHeld);
                    _friendNicknameCursorIndex = result.CursorIndex;
                    _friendNicknameSelectionStart = result.SelectionStart;
                }

                if (rightRepeat)
                {
                    var result = MoveTextCursorRight(_friendNicknameCursorIndex, _friendNicknameSelectionStart, _friendNicknameInputBuffer, shiftHeld);
                    _friendNicknameCursorIndex = result.CursorIndex;
                    _friendNicknameSelectionStart = result.SelectionStart;
                }

                return true;
            }

            if (_editingFriendMessage)
            {
                if (leftRepeat)
                {
                    var result = MoveTextCursorLeft(_friendMessageCursorIndex, _friendMessageSelectionStart, shiftHeld);
                    _friendMessageCursorIndex = result.CursorIndex;
                    _friendMessageSelectionStart = result.SelectionStart;
                }

                if (rightRepeat)
                {
                    var result = MoveTextCursorRight(_friendMessageCursorIndex, _friendMessageSelectionStart, _friendMessageInputBuffer, shiftHeld);
                    _friendMessageCursorIndex = result.CursorIndex;
                    _friendMessageSelectionStart = result.SelectionStart;
                }

                return true;
            }
        }

        if (_optionsMenuOpen && _editingPlayerName)
        {
            if (leftRepeat)
            {
                var result = MoveTextCursorLeft(_playerNameEditCursorIndex, _playerNameEditSelectionStart, shiftHeld);
                _playerNameEditCursorIndex = result.CursorIndex;
                _playerNameEditSelectionStart = result.SelectionStart;
            }

            if (rightRepeat)
            {
                var result = MoveTextCursorRight(_playerNameEditCursorIndex, _playerNameEditSelectionStart, _playerNameEditBuffer, shiftHeld);
                _playerNameEditCursorIndex = result.CursorIndex;
                _playerNameEditSelectionStart = result.SelectionStart;
            }

            return true;
        }

        if (_mainMenuOpen && _hostSetupOpen)
        {
            switch (_hostSetupEditField)
            {
                case HostSetupEditField.ServerName:
                    if (leftRepeat)
                    {
                        var result = MoveTextCursorLeft(_hostServerNameCursorIndex, _hostServerNameSelectionStart, shiftHeld);
                        _hostServerNameCursorIndex = result.CursorIndex;
                        _hostServerNameSelectionStart = result.SelectionStart;
                    }

                    if (rightRepeat)
                    {
                        var result = MoveTextCursorRight(_hostServerNameCursorIndex, _hostServerNameSelectionStart, _hostServerNameBuffer, shiftHeld);
                        _hostServerNameCursorIndex = result.CursorIndex;
                        _hostServerNameSelectionStart = result.SelectionStart;
                    }

                    return true;
                case HostSetupEditField.Port:
                    if (leftRepeat)
                    {
                        var result = MoveTextCursorLeft(_hostPortCursorIndex, _hostPortSelectionStart, shiftHeld);
                        _hostPortCursorIndex = result.CursorIndex;
                        _hostPortSelectionStart = result.SelectionStart;
                    }

                    if (rightRepeat)
                    {
                        var result = MoveTextCursorRight(_hostPortCursorIndex, _hostPortSelectionStart, _hostPortBuffer, shiftHeld);
                        _hostPortCursorIndex = result.CursorIndex;
                        _hostPortSelectionStart = result.SelectionStart;
                    }

                    return true;
                case HostSetupEditField.Slots:
                    if (leftRepeat)
                    {
                        var result = MoveTextCursorLeft(_hostSlotsCursorIndex, _hostSlotsSelectionStart, shiftHeld);
                        _hostSlotsCursorIndex = result.CursorIndex;
                        _hostSlotsSelectionStart = result.SelectionStart;
                    }

                    if (rightRepeat)
                    {
                        var result = MoveTextCursorRight(_hostSlotsCursorIndex, _hostSlotsSelectionStart, _hostSlotsBuffer, shiftHeld);
                        _hostSlotsCursorIndex = result.CursorIndex;
                        _hostSlotsSelectionStart = result.SelectionStart;
                    }

                    return true;
                case HostSetupEditField.Password:
                    if (leftRepeat)
                    {
                        var result = MoveTextCursorLeft(_hostPasswordCursorIndex, _hostPasswordSelectionStart, shiftHeld);
                        _hostPasswordCursorIndex = result.CursorIndex;
                        _hostPasswordSelectionStart = result.SelectionStart;
                    }

                    if (rightRepeat)
                    {
                        var result = MoveTextCursorRight(_hostPasswordCursorIndex, _hostPasswordSelectionStart, _hostPasswordBuffer, shiftHeld);
                        _hostPasswordCursorIndex = result.CursorIndex;
                        _hostPasswordSelectionStart = result.SelectionStart;
                    }

                    return true;
                case HostSetupEditField.RconPassword:
                    if (leftRepeat)
                    {
                        var result = MoveTextCursorLeft(_hostRconPasswordCursorIndex, _hostRconPasswordSelectionStart, shiftHeld);
                        _hostRconPasswordCursorIndex = result.CursorIndex;
                        _hostRconPasswordSelectionStart = result.SelectionStart;
                    }

                    if (rightRepeat)
                    {
                        var result = MoveTextCursorRight(_hostRconPasswordCursorIndex, _hostRconPasswordSelectionStart, _hostRconPasswordBuffer, shiftHeld);
                        _hostRconPasswordCursorIndex = result.CursorIndex;
                        _hostRconPasswordSelectionStart = result.SelectionStart;
                    }

                    return true;
                case HostSetupEditField.MapRotationFile when _hostUsePlaylistFile:
                    if (leftRepeat)
                    {
                        var result = MoveTextCursorLeft(_hostMapRotationFileCursorIndex, _hostMapRotationFileSelectionStart, shiftHeld);
                        _hostMapRotationFileCursorIndex = result.CursorIndex;
                        _hostMapRotationFileSelectionStart = result.SelectionStart;
                    }

                    if (rightRepeat)
                    {
                        var result = MoveTextCursorRight(_hostMapRotationFileCursorIndex, _hostMapRotationFileSelectionStart, _hostMapRotationFileBuffer, shiftHeld);
                        _hostMapRotationFileCursorIndex = result.CursorIndex;
                        _hostMapRotationFileSelectionStart = result.SelectionStart;
                    }

                    return true;
                case HostSetupEditField.TimeLimit:
                    if (leftRepeat)
                    {
                        var result = MoveTextCursorLeft(_hostTimeLimitCursorIndex, _hostTimeLimitSelectionStart, shiftHeld);
                        _hostTimeLimitCursorIndex = result.CursorIndex;
                        _hostTimeLimitSelectionStart = result.SelectionStart;
                    }

                    if (rightRepeat)
                    {
                        var result = MoveTextCursorRight(_hostTimeLimitCursorIndex, _hostTimeLimitSelectionStart, _hostTimeLimitBuffer, shiftHeld);
                        _hostTimeLimitCursorIndex = result.CursorIndex;
                        _hostTimeLimitSelectionStart = result.SelectionStart;
                    }

                    return true;
                case HostSetupEditField.CapLimit:
                    if (leftRepeat)
                    {
                        var result = MoveTextCursorLeft(_hostCapLimitCursorIndex, _hostCapLimitSelectionStart, shiftHeld);
                        _hostCapLimitCursorIndex = result.CursorIndex;
                        _hostCapLimitSelectionStart = result.SelectionStart;
                    }

                    if (rightRepeat)
                    {
                        var result = MoveTextCursorRight(_hostCapLimitCursorIndex, _hostCapLimitSelectionStart, _hostCapLimitBuffer, shiftHeld);
                        _hostCapLimitCursorIndex = result.CursorIndex;
                        _hostCapLimitSelectionStart = result.SelectionStart;
                    }

                    return true;
                case HostSetupEditField.RespawnSeconds:
                    if (leftRepeat)
                    {
                        var result = MoveTextCursorLeft(_hostRespawnSecondsCursorIndex, _hostRespawnSecondsSelectionStart, shiftHeld);
                        _hostRespawnSecondsCursorIndex = result.CursorIndex;
                        _hostRespawnSecondsSelectionStart = result.SelectionStart;
                    }

                    if (rightRepeat)
                    {
                        var result = MoveTextCursorRight(_hostRespawnSecondsCursorIndex, _hostRespawnSecondsSelectionStart, _hostRespawnSecondsBuffer, shiftHeld);
                        _hostRespawnSecondsCursorIndex = result.CursorIndex;
                        _hostRespawnSecondsSelectionStart = result.SelectionStart;
                    }

                    return true;
                case HostSetupEditField.AdvancedCvar:
                {
                    var buffer = _hostSetupState.GetActiveAdvancedCvarEditBuffer();
                    if (leftRepeat)
                    {
                        var result = MoveTextCursorLeft(_hostSetupState.AdvancedCvarCursorIndex, _hostSetupState.AdvancedCvarSelectionStart, shiftHeld);
                        _hostSetupState.AdvancedCvarCursorIndex = result.CursorIndex;
                        _hostSetupState.AdvancedCvarSelectionStart = result.SelectionStart;
                    }

                    if (rightRepeat)
                    {
                        var result = MoveTextCursorRight(_hostSetupState.AdvancedCvarCursorIndex, _hostSetupState.AdvancedCvarSelectionStart, buffer, shiftHeld);
                        _hostSetupState.AdvancedCvarCursorIndex = result.CursorIndex;
                        _hostSetupState.AdvancedCvarSelectionStart = result.SelectionStart;
                    }

                    return true;
                }
                case HostSetupEditField.ServerConsoleCommand:
                    if (leftRepeat)
                    {
                        var result = MoveTextCursorLeft(_hostedServerConsole.CommandInputCursorIndex, _hostedServerConsole.CommandInputSelectionStart, shiftHeld);
                        _hostedServerConsole.CommandInputCursorIndex = result.CursorIndex;
                        _hostedServerConsole.CommandInputSelectionStart = result.SelectionStart;
                    }

                    if (rightRepeat)
                    {
                        var result = MoveTextCursorRight(_hostedServerConsole.CommandInputCursorIndex, _hostedServerConsole.CommandInputSelectionStart, _hostedServerConsole.CommandInput, shiftHeld);
                        _hostedServerConsole.CommandInputCursorIndex = result.CursorIndex;
                        _hostedServerConsole.CommandInputSelectionStart = result.SelectionStart;
                    }

                    return true;
            }
        }

        if (_chatOpen)
        {
            if (leftRepeat)
            {
                var result = MoveTextCursorLeft(_chatInputCursorIndex, _chatInputSelectionStart, shiftHeld);
                _chatInputCursorIndex = result.CursorIndex;
                _chatInputSelectionStart = result.SelectionStart;
            }

            if (rightRepeat)
            {
                var result = MoveTextCursorRight(_chatInputCursorIndex, _chatInputSelectionStart, _chatInput, shiftHeld);
                _chatInputCursorIndex = result.CursorIndex;
                _chatInputSelectionStart = result.SelectionStart;
            }

            return true;
        }

        if (_consoleOpen)
        {
            if (leftRepeat)
            {
                var result = MoveTextCursorLeft(_consoleInputCursorIndex, _consoleInputSelectionStart, shiftHeld);
                _consoleInputCursorIndex = result.CursorIndex;
                _consoleInputSelectionStart = result.SelectionStart;
            }

            if (rightRepeat)
            {
                var result = MoveTextCursorRight(_consoleInputCursorIndex, _consoleInputSelectionStart, _consoleInput, shiftHeld);
                _consoleInputCursorIndex = result.CursorIndex;
                _consoleInputSelectionStart = result.SelectionStart;
            }

            return true;
        }

        return false;
    }

    private (string Text, int CursorIndex, int SelectionStart) DeleteTextSelectionOrBackspace(string text, int cursorIndex, int selectionStart)
    {
        if (HasTextSelection(cursorIndex, selectionStart))
        {
            return DeleteTextSelection(text, cursorIndex, selectionStart);
        }

        cursorIndex = Math.Clamp(cursorIndex, 0, text.Length);
        if (cursorIndex <= 0)
        {
            selectionStart = cursorIndex;
            return (text, cursorIndex, selectionStart);
        }

        text = text.Remove(cursorIndex - 1, 1);
        cursorIndex -= 1;
        selectionStart = cursorIndex;
        return (text, cursorIndex, selectionStart);
    }

    private (string Text, int CursorIndex, int SelectionStart) DeleteTextSelection(string text, int cursorIndex, int selectionStart)
    {
        var (start, length) = GetTextSelectionRange(cursorIndex, selectionStart);
        if (length == 0)
        {
            return (text, cursorIndex, selectionStart);
        }

        text = text.Remove(start, length);
        cursorIndex = start;
        selectionStart = start;
        return (text, cursorIndex, selectionStart);
    }

    private (string Text, int CursorIndex, int SelectionStart) InsertTextCharacterAtCursor(string text, char character, int cursorIndex, int selectionStart, int maxLength)
    {
        return InsertTextAtCursor(text, character.ToString(), cursorIndex, selectionStart, maxLength, c => !char.IsControl(c));
    }

    private (string Text, int CursorIndex, int SelectionStart) InsertTextAtCursor(
        string text,
        string insertText,
        int cursorIndex,
        int selectionStart,
        int maxLength,
        Func<char, bool>? allowCharacter = null)
    {
        var deletionResult = DeleteTextSelection(text, cursorIndex, selectionStart);
        text = deletionResult.Text;
        cursorIndex = deletionResult.CursorIndex;
        selectionStart = deletionResult.SelectionStart;
        cursorIndex = Math.Clamp(cursorIndex, 0, text.Length);
        selectionStart = Math.Clamp(selectionStart, 0, text.Length);

        allowCharacter ??= c => !char.IsControl(c);
        var builder = new System.Text.StringBuilder();
        foreach (var character in insertText)
        {
            if (allowCharacter(character))
            {
                builder.Append(character);
            }
        }

        var pasteText = builder.ToString();
        if (pasteText.Length == 0 || text.Length >= maxLength)
        {
            return (text, cursorIndex, selectionStart);
        }

        var remaining = maxLength - text.Length;
        if (pasteText.Length > remaining)
        {
            pasteText = pasteText[..remaining];
        }

        text = text.Insert(cursorIndex, pasteText);
        cursorIndex += pasteText.Length;
        selectionStart = cursorIndex;
        return (text, cursorIndex, selectionStart);
    }

    private static (int CursorIndex, int SelectionStart) MoveTextCursorLeft(int cursorIndex, int selectionStart, bool shiftHeld)
    {
        if (!shiftHeld && HasTextSelection(cursorIndex, selectionStart))
        {
            var (start, _) = GetTextSelectionRange(cursorIndex, selectionStart);
            cursorIndex = start;
            selectionStart = cursorIndex;
            return (cursorIndex, selectionStart);
        }

        if (!shiftHeld)
        {
            selectionStart = cursorIndex;
        }

        cursorIndex = Math.Max(0, cursorIndex - 1);
        if (!shiftHeld)
        {
            selectionStart = cursorIndex;
        }

        return (cursorIndex, selectionStart);
    }

    private static (int CursorIndex, int SelectionStart) MoveTextCursorRight(int cursorIndex, int selectionStart, string text, bool shiftHeld)
    {
        if (!shiftHeld && HasTextSelection(cursorIndex, selectionStart))
        {
            var (start, length) = GetTextSelectionRange(cursorIndex, selectionStart);
            cursorIndex = start + length;
            selectionStart = cursorIndex;
            return (cursorIndex, selectionStart);
        }

        if (!shiftHeld)
        {
            selectionStart = cursorIndex;
        }

        cursorIndex = Math.Min(text.Length, cursorIndex + 1);
        if (!shiftHeld)
        {
            selectionStart = cursorIndex;
        }

        return (cursorIndex, selectionStart);
    }

    private void InitializePlayerNameEditCursor()
    {
        _playerNameEditCursorIndex = _playerNameEditBuffer.Length;
        _playerNameEditSelectionStart = _playerNameEditCursorIndex;
    }

    private void InitializeConnectHostCursor()
    {
        _connectHostCursorIndex = _connectHostBuffer.Length;
        _connectHostSelectionStart = _connectHostCursorIndex;
    }

    private void InitializeConnectPortCursor()
    {
        _connectPortCursorIndex = _connectPortBuffer.Length;
        _connectPortSelectionStart = _connectPortCursorIndex;
    }

    private void InitializeFriendCodeCursor()
    {
        _friendCodeCursorIndex = _friendCodeInputBuffer.Length;
        _friendCodeSelectionStart = _friendCodeCursorIndex;
    }

    private void InitializeFriendNicknameCursor()
    {
        _friendNicknameCursorIndex = _friendNicknameInputBuffer.Length;
        _friendNicknameSelectionStart = _friendNicknameCursorIndex;
    }

    private void InitializeFriendMessageCursor()
    {
        _friendMessageCursorIndex = _friendMessageInputBuffer.Length;
        _friendMessageSelectionStart = _friendMessageCursorIndex;
    }

    private void InitializePasswordEditCursor()
    {
        _passwordEditCursorIndex = _passwordEditBuffer.Length;
        _passwordEditSelectionStart = _passwordEditCursorIndex;
    }

    private void InitializeChatInputCursor()
    {
        _chatInputCursorIndex = _chatInput.Length;
        _chatInputSelectionStart = _chatInputCursorIndex;
    }

    private void InitializeConsoleInputCursor()
    {
        _consoleInputCursorIndex = _consoleInput.Length;
        _consoleInputSelectionStart = _consoleInputCursorIndex;
    }

    private void InitializeHostSetupFieldCursor(HostSetupEditField field)
    {
        switch (field)
        {
            case HostSetupEditField.ServerName:
                _hostServerNameCursorIndex = _hostServerNameBuffer.Length;
                _hostServerNameSelectionStart = _hostServerNameCursorIndex;
                break;
            case HostSetupEditField.Port:
                _hostPortCursorIndex = _hostPortBuffer.Length;
                _hostPortSelectionStart = _hostPortCursorIndex;
                break;
            case HostSetupEditField.Slots:
                _hostSlotsCursorIndex = _hostSlotsBuffer.Length;
                _hostSlotsSelectionStart = _hostSlotsCursorIndex;
                break;
            case HostSetupEditField.Password:
                _hostPasswordCursorIndex = _hostPasswordBuffer.Length;
                _hostPasswordSelectionStart = _hostPasswordCursorIndex;
                break;
            case HostSetupEditField.RconPassword:
                _hostRconPasswordCursorIndex = _hostRconPasswordBuffer.Length;
                _hostRconPasswordSelectionStart = _hostRconPasswordCursorIndex;
                break;
            case HostSetupEditField.MapRotationFile when _hostUsePlaylistFile:
                _hostMapRotationFileCursorIndex = _hostMapRotationFileBuffer.Length;
                _hostMapRotationFileSelectionStart = _hostMapRotationFileCursorIndex;
                break;
            case HostSetupEditField.TimeLimit:
                _hostTimeLimitCursorIndex = _hostTimeLimitBuffer.Length;
                _hostTimeLimitSelectionStart = _hostTimeLimitCursorIndex;
                break;
            case HostSetupEditField.CapLimit:
                _hostCapLimitCursorIndex = _hostCapLimitBuffer.Length;
                _hostCapLimitSelectionStart = _hostCapLimitCursorIndex;
                break;
            case HostSetupEditField.RespawnSeconds:
                _hostRespawnSecondsCursorIndex = _hostRespawnSecondsBuffer.Length;
                _hostRespawnSecondsSelectionStart = _hostRespawnSecondsCursorIndex;
                break;
            case HostSetupEditField.AdvancedCvar:
                _hostSetupState.AdvancedCvarCursorIndex = _hostSetupState.GetActiveAdvancedCvarEditBuffer().Length;
                _hostSetupState.AdvancedCvarSelectionStart = _hostSetupState.AdvancedCvarCursorIndex;
                break;
            case HostSetupEditField.MapNameFilter:
                _hostSetupState.AvailableMapNameFilterCursorIndex = _hostSetupState.AvailableMapNameFilterBuffer.Length;
                _hostSetupState.AvailableMapNameFilterSelectionStart = _hostSetupState.AvailableMapNameFilterCursorIndex;
                break;
            case HostSetupEditField.ServerConsoleCommand:
                _hostedServerConsole.CommandInputCursorIndex = _hostedServerConsole.CommandInput.Length;
                _hostedServerConsole.CommandInputSelectionStart = _hostedServerConsole.CommandInputCursorIndex;
                break;
            case HostSetupEditField.None:
            default:
                break;
        }
    }
}
