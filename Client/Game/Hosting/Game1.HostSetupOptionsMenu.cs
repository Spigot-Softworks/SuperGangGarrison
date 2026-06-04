#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;
using System;

namespace OpenGarrison.Client;

public partial class Game1
{
    private static readonly string[] HostSetupOptionsTabLabels = ["Basic", "Advanced"];

    private void DrawHostSetupOptionsMenuOverlay()
    {
        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * 0.78f);

        if (_menuBackgroundMode != MenuBackgroundMode.Static)
        {
            const int bottomBarHeight = 76;
            var barY = viewportHeight - bottomBarHeight;
            var bottomBarBounds = new Rectangle(0, barY, viewportWidth, bottomBarHeight);
            _spriteBatch.Draw(_pixel, bottomBarBounds, new Color(0x57, 0x4f, 0x47));
            _menuBottomBarRunners.Draw(bottomBarBounds);
        }

        var layout = HostSetupOptionsMenuLayout.Create(viewportWidth, viewportHeight);
        var optionCount = _hostSetupState.GetHostOptionsRowCount();
        ClampHostOptionsScrollOffset(optionCount, layout.VisibleRowCount);

        DrawRoundedRectangleOutline(layout.Panel, new Color(59, 51, 46), new Color(213, 205, 188), outlineThickness: 2, radius: 8);

        const float headerScale = 1f;
        const float rowTextScale = 1f;
        const float rowHorizontalPadding = 14f;
        const float rowColumnGap = 20f;
        var rowSpacing = layout.CompactLayout ? 4 : 6;
        var rowHeightWithoutSpacing = layout.RowHeight - rowSpacing;

        DrawBitmapFontText("Host Options", new Vector2(layout.ListBounds.X, layout.Panel.Y + 14f), Color.White, headerScale);
        DrawHostSetupOptionsMenuTabs(layout);

        if (optionCount > layout.VisibleRowCount)
        {
            var visibleStart = _hostOptionsScrollOffset + 1;
            var visibleEnd = Math.Min(optionCount, _hostOptionsScrollOffset + layout.VisibleRowCount);
            DrawBitmapFontText(
                $"{visibleStart}-{visibleEnd}/{optionCount}",
                new Vector2(layout.ListBounds.Right - (layout.CompactLayout ? 78f : 96f), layout.Panel.Y + 14f),
                new Color(186, 186, 186),
                1f);
        }

        var endIndex = Math.Min(optionCount, _hostOptionsScrollOffset + layout.VisibleRowCount);
        for (var index = _hostOptionsScrollOffset; index < endIndex; index += 1)
        {
            if (!_hostSetupState.TryGetHostOptionsRow(index, out var label, out var displayValue, out var editorKind, out var definition))
            {
                continue;
            }

            var visibleRow = index - _hostOptionsScrollOffset;
            var rowBounds = new Rectangle(
                layout.ListBounds.X,
                layout.ListBounds.Y + (visibleRow * layout.RowHeight),
                layout.ListBounds.Width,
                rowHeightWithoutSpacing);
            var isHovered = index == _hostOptionsHoverIndex;
            _spriteBatch.Draw(_pixel, rowBounds, isHovered ? new Color(75, 67, 62) : new Color(54, 47, 41));

            var textY = rowBounds.Y + ((rowBounds.Height - MeasureBitmapFontHeight(rowTextScale)) * 0.5f);
            var labelX = rowBounds.X + rowHorizontalPadding;
            var valueRightX = rowBounds.Right - rowHorizontalPadding;
            var trimmedValue = TrimBitmapMenuText(displayValue, rowBounds.Width * 0.42f, rowTextScale);
            var valueWidth = MeasureBitmapFontWidth(trimmedValue, rowTextScale);
            var valueX = valueRightX - valueWidth;
            var labelMaxWidth = Math.Max(40f, valueX - labelX - rowColumnGap);
            var trimmedLabel = TrimBitmapMenuText(label, labelMaxWidth, rowTextScale);
            DrawBitmapFontText(trimmedLabel, new Vector2(labelX, textY), Color.White, rowTextScale);

            var isEditingNumeric = IsHostSetupOptionsRowEditing(index, editorKind, definition);
            if (isEditingNumeric)
            {
                var valueBoxWidth = (int)(rowBounds.Width * 0.42f);
                var valueBoxHeight = rowBounds.Height - 12;
                if (valueBoxHeight < 18)
                {
                    valueBoxHeight = 18;
                }

                var valueBoxBounds = new Rectangle(
                    (int)(valueRightX - valueBoxWidth),
                    rowBounds.Y + ((rowBounds.Height - valueBoxHeight) / 2),
                    valueBoxWidth,
                    valueBoxHeight);
                var editBuffer = GetHostSetupOptionsEditBuffer(index, definition);
                var (cursorIndex, selectionStart) = GetHostSetupOptionsEditSelection(index);
                DrawMenuInputBoxScaled(valueBoxBounds, editBuffer, true, rowTextScale, cursorIndex, selectionStart);
            }
            else if (!string.IsNullOrWhiteSpace(trimmedValue))
            {
                DrawBitmapFontText(trimmedValue, new Vector2(valueX, textY), Color.White, rowTextScale);
            }
        }

        if (optionCount > layout.VisibleRowCount)
        {
            var trackBounds = new Rectangle(layout.Panel.Right - 20, layout.ListBounds.Y, 8, layout.ListBounds.Height);
            _spriteBatch.Draw(_pixel, trackBounds, new Color(22, 24, 28));

            var maxOffset = Math.Max(1, optionCount - layout.VisibleRowCount);
            var thumbHeight = Math.Max(24, (int)MathF.Round(trackBounds.Height * (layout.VisibleRowCount / (float)optionCount)));
            var thumbTravel = Math.Max(0, trackBounds.Height - thumbHeight);
            var thumbY = trackBounds.Y + (int)MathF.Round((_hostOptionsScrollOffset / (float)maxOffset) * thumbTravel);
            var thumbBounds = new Rectangle(trackBounds.X, thumbY, trackBounds.Width, thumbHeight);
            _spriteBatch.Draw(_pixel, thumbBounds, new Color(105, 105, 105));
        }

        var confirmHovered = _hostOptionsHoverIndex == optionCount;
        var resetHovered = _hostOptionsHoverIndex == optionCount + 1;
        DrawMenuButtonScaled(layout.ConfirmBounds, "Confirm", confirmHovered, 1f);
        DrawMenuButtonScaled(layout.ResetBounds, "Reset", resetHovered, 1f);
    }

    private void DrawHostSetupOptionsMenuTabs(HostSetupOptionsMenuLayout layout)
    {
        for (var index = 0; index < layout.TabBounds.Length; index += 1)
        {
            var selected = index == _hostSetupState.OptionsTabIndex;
            DrawMenuButtonScaled(layout.TabBounds[index], HostSetupOptionsTabLabels[index], selected, 1f);
        }
    }

    private void HandleHostSetupOptionsMenu(MouseState mouse, bool clickPressed)
    {
        var layout = HostSetupOptionsMenuLayout.Create(ViewportWidth, ViewportHeight);
        var optionCount = _hostSetupState.GetHostOptionsRowCount();
        _hostOptionsScrollOffset = Math.Clamp(
            _hostOptionsScrollOffset,
            0,
            Math.Max(0, optionCount - layout.VisibleRowCount));

        var optionsTrackBounds = new Rectangle(layout.ListBounds.Right + 8, layout.ListBounds.Y, 8, layout.ListBounds.Height);
        var hostOptionsScrollOffset = _hostOptionsScrollOffset;
        if (TryHandleScrollbarDrag(
                mouse,
                _previousMouse,
                ScrollbarOwners.HostSetupOptions,
                optionsTrackBounds,
                ref hostOptionsScrollOffset,
                optionCount,
                layout.VisibleRowCount))
        {
            _hostOptionsScrollOffset = hostOptionsScrollOffset;
            return;
        }

        _hostOptionsScrollOffset = hostOptionsScrollOffset;

        var wheelDelta = mouse.ScrollWheelValue - _previousMouse.ScrollWheelValue;
        if (wheelDelta != 0 && layout.ListBounds.Contains(mouse.Position))
        {
            var stepCount = Math.Max(1, Math.Abs(wheelDelta) / 120);
            _hostOptionsScrollOffset = Math.Clamp(
                _hostOptionsScrollOffset + (wheelDelta > 0 ? -stepCount : stepCount),
                0,
                Math.Max(0, optionCount - layout.VisibleRowCount));
        }

        _hostOptionsHoverIndex = -1;
        for (var tabIndex = 0; tabIndex < layout.TabBounds.Length; tabIndex += 1)
        {
            if (layout.TabBounds[tabIndex].Contains(mouse.Position))
            {
                _hostOptionsHoverIndex = optionCount + 2 + tabIndex;
                break;
            }
        }

        if (_hostOptionsHoverIndex < 0)
        {
            if (layout.ConfirmBounds.Contains(mouse.Position))
            {
                _hostOptionsHoverIndex = optionCount;
            }
            else if (layout.ResetBounds.Contains(mouse.Position))
            {
                _hostOptionsHoverIndex = optionCount + 1;
            }
            else if (layout.ListBounds.Contains(mouse.Position))
            {
                var visibleIndex = (mouse.Y - layout.ListBounds.Y) / layout.RowHeight;
                var hoverIndex = _hostOptionsScrollOffset + visibleIndex;
                if (visibleIndex >= 0 && hoverIndex >= 0 && hoverIndex < optionCount)
                {
                    _hostOptionsHoverIndex = hoverIndex;
                }
            }
        }

        if (!clickPressed)
        {
            return;
        }

        if (_hostOptionsHoverIndex == optionCount)
        {
            _hostSetupState.NavigateToMainScreen();
            _hostSetupFlowController.FocusHostSetupField(HostSetupEditField.ServerName);
            return;
        }

        if (_hostOptionsHoverIndex == optionCount + 1)
        {
            _hostSetupState.ResetHostingOptionsToDefaults();
            _menuStatusMessage = string.Empty;
            return;
        }

        if (_hostOptionsHoverIndex >= optionCount + 2 && _hostOptionsHoverIndex < optionCount + 2 + layout.TabBounds.Length)
        {
            var tabIndex = _hostOptionsHoverIndex - (optionCount + 2);
            _hostSetupState.SetHostOptionsTab(tabIndex);
            return;
        }

        if (_hostOptionsHoverIndex < 0 || _hostOptionsHoverIndex >= optionCount)
        {
            return;
        }

        HandleHostSetupOptionsRowClick(_hostOptionsHoverIndex, mouse, layout);
    }

    private void HandleHostSetupOptionsRowClick(int rowIndex, MouseState mouse, HostSetupOptionsMenuLayout layout)
    {
        if (_hostSetupState.OptionsTab == HostSetupOptionsTab.Basic)
        {
            HandleHostSetupBasicOptionsRowClick(rowIndex);
            return;
        }

        if (!_hostSetupState.TryGetAdvancedDefinitionForRow(rowIndex, out var definition))
        {
            return;
        }

        var visibleIndex = rowIndex - _hostOptionsScrollOffset;
        var rowBounds = new Rectangle(
            layout.ListBounds.X,
            layout.ListBounds.Y + (visibleIndex * layout.RowHeight),
            layout.ListBounds.Width,
            layout.RowHeight - (layout.CompactLayout ? 4 : 6));
        const float rowHorizontalPadding = 14f;
        var valueRightX = rowBounds.Right - rowHorizontalPadding;

        switch (definition.EditorKind)
        {
            case HostSetupCvarEditorKind.Toggle:
                _hostSetupState.ToggleAdvancedCvar(definition);
                _hostSetupState.ClearAdvancedCvarEditFocus();
                break;
            case HostSetupCvarEditorKind.Stepped:
            {
                var rawValue = _hostSetupState.GetAdvancedCvarRawValue(definition);
                var displayValue = $"< {HostSetupServerCvarCatalog.FormatDisplayValue(definition, rawValue)} >";
                var valueWidth = MeasureBitmapFontWidth(displayValue, 1f);
                var valueX = valueRightX - valueWidth;
                var direction = mouse.X < valueX + (valueWidth / 2f) ? -1 : 1;
                _hostSetupState.StepAdvancedCvar(definition, direction);
                _hostSetupState.ClearAdvancedCvarEditFocus();
                break;
            }
            case HostSetupCvarEditorKind.NumericText:
                _hostSetupState.ActiveAdvancedCvarName = definition.Name;
                _hostSetupState.SetAdvancedCvarRawValue(definition, _hostSetupState.GetAdvancedCvarRawValue(definition));
                _hostSetupEditField = HostSetupEditField.AdvancedCvar;
                _hostSetupState.AdvancedCvarCursorIndex = _hostSetupState.GetActiveAdvancedCvarEditBuffer().Length;
                _hostSetupState.AdvancedCvarSelectionStart = _hostSetupState.AdvancedCvarCursorIndex;
                if (IsTextFieldDoubleClick(TextFieldClickTarget.HostSetupAdvancedCvar))
                {
                    SelectAllTextInActiveField(TextFieldClickTarget.HostSetupAdvancedCvar);
                }

                break;
        }
    }

    private void HandleHostSetupBasicOptionsRowClick(int rowIndex)
    {
        switch (rowIndex)
        {
            case 0:
                _hostSetupFlowController.FocusHostSetupField(HostSetupEditField.TimeLimit);
                _hostSetupState.ClearAdvancedCvarEditFocus();
                if (IsTextFieldDoubleClick(TextFieldClickTarget.HostSetupTimeLimit))
                {
                    SelectAllTextInActiveField(TextFieldClickTarget.HostSetupTimeLimit);
                }

                break;
            case 1:
                _hostSetupFlowController.FocusHostSetupField(HostSetupEditField.CapLimit);
                _hostSetupState.ClearAdvancedCvarEditFocus();
                if (IsTextFieldDoubleClick(TextFieldClickTarget.HostSetupCapLimit))
                {
                    SelectAllTextInActiveField(TextFieldClickTarget.HostSetupCapLimit);
                }

                break;
            case 2:
                _hostSetupFlowController.FocusHostSetupField(HostSetupEditField.RespawnSeconds);
                _hostSetupState.ClearAdvancedCvarEditFocus();
                if (IsTextFieldDoubleClick(TextFieldClickTarget.HostSetupRespawnSeconds))
                {
                    SelectAllTextInActiveField(TextFieldClickTarget.HostSetupRespawnSeconds);
                }

                break;
            case 3:
                _hostSetupState.ToggleBasicAutoBalance();
                _hostSetupState.ClearAdvancedCvarEditFocus();
                break;
            case 4:
                _hostSetupState.ToggleBasicSecondaryAbilities();
                _hostSetupState.ClearAdvancedCvarEditFocus();
                break;
        }
    }

    private bool IsHostSetupOptionsRowEditing(int rowIndex, HostSetupCvarEditorKind editorKind, HostSetupServerCvarDefinition? definition)
    {
        if (editorKind != HostSetupCvarEditorKind.NumericText)
        {
            return false;
        }

        if (_hostSetupState.OptionsTab == HostSetupOptionsTab.Basic)
        {
            return rowIndex switch
            {
                0 => _hostSetupEditField == HostSetupEditField.TimeLimit,
                1 => _hostSetupEditField == HostSetupEditField.CapLimit,
                2 => _hostSetupEditField == HostSetupEditField.RespawnSeconds,
                _ => false,
            };
        }

        return _hostSetupEditField == HostSetupEditField.AdvancedCvar
            && definition is not null
            && string.Equals(_hostSetupState.ActiveAdvancedCvarName, definition.Name, StringComparison.OrdinalIgnoreCase);
    }

    private string GetHostSetupOptionsEditBuffer(int rowIndex, HostSetupServerCvarDefinition? definition)
    {
        if (_hostSetupState.OptionsTab == HostSetupOptionsTab.Basic)
        {
            return rowIndex switch
            {
                0 => _hostTimeLimitBuffer,
                1 => _hostCapLimitBuffer,
                2 => _hostRespawnSecondsBuffer,
                _ => string.Empty,
            };
        }

        return definition is null ? string.Empty : _hostSetupState.GetActiveAdvancedCvarEditBuffer();
    }

    private (int CursorIndex, int SelectionStart) GetHostSetupOptionsEditSelection(int rowIndex)
    {
        if (_hostSetupState.OptionsTab == HostSetupOptionsTab.Basic)
        {
            return rowIndex switch
            {
                0 => (_hostTimeLimitCursorIndex, _hostTimeLimitSelectionStart),
                1 => (_hostCapLimitCursorIndex, _hostCapLimitSelectionStart),
                2 => (_hostRespawnSecondsCursorIndex, _hostRespawnSecondsSelectionStart),
                _ => (0, 0),
            };
        }

        return (_hostSetupState.AdvancedCvarCursorIndex, _hostSetupState.AdvancedCvarSelectionStart);
    }
}
