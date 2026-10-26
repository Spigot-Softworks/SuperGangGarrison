#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private bool _builderScriptOpen;
    private string _builderScriptText = "";
    private int _builderScriptCursor;
    private int _builderScriptAnchor;
    private int _builderScriptScroll;
    private int _builderScriptColumnScroll;
    private int _builderScriptSymbolScroll;
    private string _builderScriptMessage = "";
    private readonly List<int> _builderBulkGroupTargets = [];
    private readonly List<(string Text, int Cursor, int Anchor)> _builderScriptUndo = [];
    private string? _lastReportedMapScriptError;

    private const string BuilderMapScriptExample = """
        -- Name objects and assign Groups in their Properties first.
        -- Point numbers match the editor: 1, 2, 3, ...
        function on_start()
            -- group("blue_spawn"):disable()
        end

        function on_capture(point, team)
            if point == 1 and team == BLU then
                -- group("red_spawn"):disable()
                -- group("blue_spawn"):enable()
            end
        end

        -- Other events: on_tick(dt), on_signal(name, active)
        -- map.after(3, function() object("door"):enable() end)
        """;

    private void OpenGarrisonBuilderMapScript()
    {
        if (!CommitGarrisonBuilderActiveEdits()) return;
        _builderScriptText = MapScriptMetadata.Read(_builderDocument.Metadata);
        _builderScriptCursor = _builderScriptText.Length;
        _builderScriptAnchor = _builderScriptCursor;
        _builderScriptScroll = 0;
        _builderScriptColumnScroll = 0;
        _builderScriptSymbolScroll = 0;
        _builderScriptUndo.Clear();
        _builderScriptMessage = _world.MapScriptError ?? "Lua map events. Click a named object or group to insert it.";
        _builderScriptOpen = true;
    }

    private void ApplyGarrisonBuilderMapScript()
    {
        if (MapScriptMetadata.Validate(_builderScriptText) is { } error)
        { _builderScriptMessage = error; return; }
        RecordGarrisonBuilderHistory();
        var metadata = new Dictionary<string, string>(_builderDocument.Metadata, StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(_builderScriptText)) metadata.Remove(MapScriptMetadata.SourceKey);
        else metadata[MapScriptMetadata.SourceKey] = _builderScriptText;
        _builderDocument = (_builderDocument with { Metadata = metadata }).NormalizeForEditing();
        _builderDirty = true;
        _builderStatus = "map script saved; quick test to run it";
        _builderScriptOpen = false;
    }

    private Rectangle GetBuilderScriptBounds()
        => new(20, 24, Math.Max(320, BuilderViewportWidth - 40), Math.Max(240, BuilderViewportHeight - 48));
    private Rectangle GetBuilderScriptCodeBounds()
    {
        var panel = GetBuilderScriptBounds();
        return new(panel.X + 12, panel.Y + 76, Math.Max(80, panel.Width - 270), Math.Max(60, panel.Height - 154));
    }
    private Rectangle GetBuilderScriptButtonBounds(int index)
    {
        var panel = GetBuilderScriptBounds();
        var width = Math.Min(120, (panel.Width - 36) / 4);
        return new(panel.X + 12 + index * (width + 6), panel.Y + 36, width, 28);
    }
    private List<(string Label, string Expression)> GetBuilderScriptSymbols()
    {
        var result = _builderEntities.Where(entity => MapObjectMetadata.Name(entity.Properties).Length > 0)
            .OrderBy(entity => MapObjectMetadata.Name(entity.Properties), StringComparer.OrdinalIgnoreCase)
            .Select(entity => MapLogicMetadata.IsLogicOutputEntityType(entity.Type)
                ? ("Node: " + MapObjectMetadata.Name(entity.Properties), "map.pulse(" + LuaMapString(MapObjectMetadata.Name(entity.Properties)) + ")")
                : ("Object: " + MapObjectMetadata.Name(entity.Properties), "object(" + LuaMapString(MapObjectMetadata.Name(entity.Properties)) + ")")).ToList();
        result.AddRange(_builderEntities.SelectMany(entity => MapObjectMetadata.ParseGroups(MapObjectMetadata.Groups(entity.Properties)))
            .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase)
            .Select(name => ("Group: " + name, "group(" + LuaMapString(name) + ")")));
        return result;
    }
    private static string LuaMapString(string value)
        => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t") + "\"";

    private void EditBuilderScript(string replacement)
    {
        var start = Math.Min(_builderScriptCursor, _builderScriptAnchor);
        var end = Math.Max(_builderScriptCursor, _builderScriptAnchor);
        if (_builderScriptText.Length - (end - start) + replacement.Length > MapScriptMetadata.MaximumSourceLength)
        { _builderScriptMessage = "Script limit: 64 KB."; return; }
        _builderScriptUndo.Add((_builderScriptText, _builderScriptCursor, _builderScriptAnchor));
        if (_builderScriptUndo.Count > 100) _builderScriptUndo.RemoveAt(0);
        _builderScriptText = _builderScriptText[..start] + replacement + _builderScriptText[end..];
        _builderScriptCursor = start + replacement.Length;
        _builderScriptAnchor = _builderScriptCursor;
        FollowBuilderScriptCaret();
        _builderScriptMessage = "Ctrl+S: apply  |  Ctrl+Enter: check  |  Esc: cancel";
    }

    private bool HandleBuilderScriptText(char character)
    {
        if (!_builderScriptOpen) return false;
        if (!char.IsControl(character)) EditBuilderScript(character.ToString());
        return true;
    }

    private bool UpdateGarrisonBuilderMapScript(KeyboardState keyboard, MouseState mouse)
    {
        if (!_builderScriptOpen) return false;
        var originalCursor = _builderScriptCursor;
        var ctrl = keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl);
        var shift = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);
        if (IsKeyPressed(keyboard, Keys.Escape)) { _builderScriptOpen = false; return true; }
        if (ctrl && IsKeyPressed(keyboard, Keys.S)) { ApplyGarrisonBuilderMapScript(); return true; }
        if (ctrl && IsKeyPressed(keyboard, Keys.Enter))
        { _builderScriptMessage = MapScriptMetadata.Validate(_builderScriptText) ?? "Syntax OK. Quick test to check map events and object names."; return true; }
        if (ctrl && IsKeyPressed(keyboard, Keys.A)) { _builderScriptAnchor = 0; _builderScriptCursor = _builderScriptText.Length; }
        if (ctrl && IsKeyPressed(keyboard, Keys.Z) && _builderScriptUndo.Count > 0)
        {
            (_builderScriptText, _builderScriptCursor, _builderScriptAnchor) = _builderScriptUndo[^1];
            _builderScriptUndo.RemoveAt(_builderScriptUndo.Count - 1);
        }
        if (ctrl && (IsKeyPressed(keyboard, Keys.C) || IsKeyPressed(keyboard, Keys.X)))
        {
            var start = Math.Min(_builderScriptCursor, _builderScriptAnchor);
            var length = Math.Abs(_builderScriptCursor - _builderScriptAnchor);
            if (length > 0 && TrySetClipboardText(_builderScriptText.Substring(start, length)) && IsKeyPressed(keyboard, Keys.X)) EditBuilderScript("");
        }
        if (ctrl && IsKeyPressed(keyboard, Keys.V) && TryGetClipboardText(out var paste))
            EditBuilderScript(paste.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\t", "    "));
        if (!ctrl && IsKeyPressed(keyboard, Keys.Enter)) EditBuilderScript("\n");
        if (!ctrl && IsKeyPressed(keyboard, Keys.Tab)) EditBuilderScript("    ");
        if (IsKeyPressed(keyboard, Keys.Back))
        {
            if (_builderScriptAnchor == _builderScriptCursor) _builderScriptAnchor = Math.Max(0, _builderScriptCursor - 1);
            EditBuilderScript("");
        }
        if (IsKeyPressed(keyboard, Keys.Delete))
        {
            if (_builderScriptAnchor == _builderScriptCursor) _builderScriptAnchor = Math.Min(_builderScriptText.Length, _builderScriptCursor + 1);
            EditBuilderScript("");
        }
        var cursor = _builderScriptCursor;
        if (IsKeyPressed(keyboard, Keys.Left)) cursor--;
        if (IsKeyPressed(keyboard, Keys.Right)) cursor++;
        cursor = Math.Clamp(cursor, 0, _builderScriptText.Length);
        var lineStart = _builderScriptText.LastIndexOf('\n', Math.Max(0, cursor - 1), Math.Max(0, cursor)) + 1;
        var lineEnd = _builderScriptText.IndexOf('\n', Math.Clamp(cursor, 0, _builderScriptText.Length));
        if (lineEnd < 0) lineEnd = _builderScriptText.Length;
        if (IsKeyPressed(keyboard, Keys.Home)) cursor = ctrl ? 0 : lineStart;
        if (IsKeyPressed(keyboard, Keys.End)) cursor = ctrl ? _builderScriptText.Length : lineEnd;
        if (IsKeyPressed(keyboard, Keys.Up))
        {
            var previousStart = lineStart > 1 ? _builderScriptText.LastIndexOf('\n', lineStart - 2) + 1 : 0;
            cursor = Math.Min(Math.Max(0, lineStart - 1), previousStart + cursor - lineStart);
        }
        if (IsKeyPressed(keyboard, Keys.Down))
        {
            var nextEnd = lineEnd < _builderScriptText.Length ? _builderScriptText.IndexOf('\n', lineEnd + 1) : -1;
            cursor = Math.Min(nextEnd < 0 ? _builderScriptText.Length : nextEnd, lineEnd + 1 + cursor - lineStart);
        }
        cursor = Math.Clamp(cursor, 0, _builderScriptText.Length);
        if (cursor != _builderScriptCursor) { _builderScriptCursor = cursor; if (!shift) _builderScriptAnchor = cursor; }
        if (_builderScriptCursor != originalCursor) FollowBuilderScriptCaret();
        var code = GetBuilderScriptCodeBounds();
        var wheel = mouse.ScrollWheelValue - _previousMouse.ScrollWheelValue;
        if (wheel != 0)
        {
            if (mouse.X > code.Right) _builderScriptSymbolScroll = Math.Max(0, _builderScriptSymbolScroll + (wheel > 0 ? -3 : 3));
            else _builderScriptScroll = Math.Max(0, _builderScriptScroll + (wheel > 0 ? -3 : 3));
        }
        if (IsLeftMouseClickPressed(mouse, _previousMouse))
        {
            if (GetBuilderScriptButtonBounds(0).Contains(mouse.Position)) ApplyGarrisonBuilderMapScript();
            else if (GetBuilderScriptButtonBounds(1).Contains(mouse.Position))
                _builderScriptMessage = MapScriptMetadata.Validate(_builderScriptText) ?? "Syntax OK. Quick test to check map events and object names.";
            else if (GetBuilderScriptButtonBounds(2).Contains(mouse.Position))
            {
                if (_builderScriptText.Length == 0) EditBuilderScript(BuilderMapScriptExample);
                else _builderScriptMessage = "Clear the editor first to insert the example. Ctrl+A, then Delete.";
            }
            else if (GetBuilderScriptButtonBounds(3).Contains(mouse.Position)) _builderScriptOpen = false;
            else if (code.Contains(mouse.Position))
            {
                var lines = _builderScriptText.Split('\n');
                var row = Math.Clamp(_builderScriptScroll + (mouse.Y - code.Y - 4) / 20, 0, lines.Length - 1);
                var column = Math.Min(lines[row].Length, _builderScriptColumnScroll);
                while (column < lines[row].Length && MeasureBitmapFontWidth(lines[row][Math.Min(lines[row].Length, _builderScriptColumnScroll)..(column + 1)], 1f) < mouse.X - code.X - 46) column++;
                _builderScriptCursor = lines.Take(row).Sum(line => line.Length + 1) + column;
                _builderScriptAnchor = _builderScriptCursor;
            }
            else
            {
                var symbols = GetBuilderScriptSymbols();
                var symbol = _builderScriptSymbolScroll + (mouse.Y - code.Y - 24) / 22;
                if (mouse.X > code.Right + 12 && mouse.Y >= code.Y + 24 && symbol >= 0 && symbol < symbols.Count)
                    EditBuilderScript(symbols[symbol].Expression);
            }
        }
        return true;
    }

    private void FollowBuilderScriptCaret()
    {
        var prefix = _builderScriptText[.._builderScriptCursor];
        var row = prefix.Count(character => character == '\n');
        var visible = Math.Max(1, (GetBuilderScriptCodeBounds().Height - 8) / 20);
        if (row < _builderScriptScroll) _builderScriptScroll = row;
        if (row >= _builderScriptScroll + visible) _builderScriptScroll = row - visible + 1;
        var linePrefix = prefix[(prefix.LastIndexOf('\n') + 1)..];
        _builderScriptColumnScroll = Math.Min(_builderScriptColumnScroll, linePrefix.Length);
        while (_builderScriptColumnScroll < linePrefix.Length
            && MeasureBitmapFontWidth(linePrefix[_builderScriptColumnScroll..], 1f) > GetBuilderScriptCodeBounds().Width - 60)
            _builderScriptColumnScroll++;
    }

    private void DrawGarrisonBuilderMapScript(MouseState mouse)
    {
        if (!_builderScriptOpen) return;
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, BuilderViewportWidth, BuilderViewportHeight), Color.Black * 0.8f);
        var panel = GetBuilderScriptBounds();
        DrawMenuPanelBackdrop(panel, 1f);
        DrawBitmapFontText("Map script (Lua)", new Vector2(panel.X + 12, panel.Y + 10), Color.White, 1f);
        var labels = new[] { "Apply (Ctrl+S)", "Check syntax", "Insert example", "Cancel" };
        for (var i = 0; i < labels.Length; i++) DrawBuilderMenuButton(GetBuilderScriptButtonBounds(i), labels[i], GetBuilderScriptButtonBounds(i).Contains(mouse.Position));
        var code = GetBuilderScriptCodeBounds();
        _spriteBatch.Draw(_pixel, code, new Color(20, 24, 30));
        var lines = _builderScriptText.Split('\n');
        var visible = Math.Max(1, (code.Height - 8) / 20);
        _builderScriptScroll = Math.Clamp(_builderScriptScroll, 0, Math.Max(0, lines.Length - visible));
        var offset = 0;
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            if (index >= _builderScriptScroll && index < _builderScriptScroll + visible)
            {
                var y = code.Y + 4 + (index - _builderScriptScroll) * 20;
                DrawBitmapFontText((index + 1).ToString(), new Vector2(code.X + 5, y), Color.Gray, 1f);
                var columnStart = Math.Min(line.Length, _builderScriptColumnScroll);
                var display = TruncateGarrisonBuilderPropertyLabel(line[columnStart..], code.Width - 52, 1f);
                var selectionStart = Math.Max(offset + columnStart, Math.Min(_builderScriptCursor, _builderScriptAnchor));
                var selectionEnd = Math.Min(offset + line.Length, Math.Max(_builderScriptCursor, _builderScriptAnchor));
                if (selectionEnd > selectionStart)
                {
                    var selectionX = code.X + 46 + MeasureBitmapFontWidth(line[columnStart..(selectionStart - offset)], 1f);
                    var selectionWidth = Math.Min(code.Right - 4 - selectionX,
                        MeasureBitmapFontWidth(line[(selectionStart - offset)..(selectionEnd - offset)], 1f));
                    if (selectionWidth > 0) _spriteBatch.Draw(_pixel,
                        new Rectangle((int)selectionX, y, (int)selectionWidth, 18), new Color(50, 80, 120));
                }
                DrawBitmapFontText(display, new Vector2(code.X + 46, y), line.TrimStart().StartsWith("--", StringComparison.Ordinal) ? Color.Gray : new Color(210, 226, 240), 1f);
                if (_builderScriptCursor >= offset && _builderScriptCursor <= offset + line.Length)
                {
                    var caretColumn = _builderScriptCursor - offset;
                    var x = code.X + 46 + (caretColumn >= columnStart ? MeasureBitmapFontWidth(line[columnStart..caretColumn], 1f) : -100);
                    if (x >= code.X + 46 && x < code.Right - 4) _spriteBatch.Draw(_pixel, new Rectangle((int)x, y, 1, 16), Color.White);
                }
            }
            offset += line.Length + 1;
        }
        DrawBitmapFontText("Named objects / groups", new Vector2(code.Right + 14, code.Y), Color.White, 1f);
        var symbols = GetBuilderScriptSymbols();
        _builderScriptSymbolScroll = Math.Clamp(_builderScriptSymbolScroll, 0, Math.Max(0, symbols.Count - (code.Height - 24) / 22));
        for (var i = 0; i < Math.Min(symbols.Count - _builderScriptSymbolScroll, (code.Height - 24) / 22); i++)
            DrawBitmapFontText(TruncateGarrisonBuilderPropertyLabel(symbols[i + _builderScriptSymbolScroll].Label, 226, 1f),
                new Vector2(code.Right + 14, code.Y + 24 + i * 22), new Color(150, 215, 180), 1f);
        DrawBitmapFontText(TruncateGarrisonBuilderPropertyLabel("Events: on_start, on_capture(point, team), on_tick(dt), on_signal(name, active)", panel.Width - 24, 1f),
            new Vector2(panel.X + 12, panel.Bottom - 65), new Color(180, 195, 215), 1f);
        DrawBitmapFontText(TruncateGarrisonBuilderPropertyLabel(_builderScriptMessage.Replace('\n', ' '), panel.Width - 24, 1f),
            new Vector2(panel.X + 12, panel.Bottom - 40), new Color(255, 214, 118), 1f);
        DrawBitmapFontText(TruncateGarrisonBuilderPropertyLabel("Use object(\"door\"):disable() or group(\"blue_spawn\"):enable(). Names and Groups live in Properties.", panel.Width - 24, 1f),
            new Vector2(panel.X + 12, panel.Bottom - 20), Color.Gray, 1f);
    }

    private void BeginGarrisonBuilderBulkGroups()
    {
        BeginEditingGarrisonBuilderSelectedEntity();
        _builderBulkGroupTargets.Clear();
        _builderBulkGroupTargets.AddRange(_builderSelectedEntityIndices);
        _builderPropertyEditMode = GarrisonBuilderPropertyEditMode.EditValue;
        _builderPropertyEditKey = MapObjectMetadata.GroupsKey;
        _builderPropertyEditBuffer = MapObjectMetadata.Groups(_builderEntities[_builderSelectedEntityIndex].Properties);
        _builderPropertyCursorIndex = _builderPropertyEditBuffer.Length;
        _builderPropertySelectionStart = _builderPropertyCursorIndex;
    }

    private void ApplyGarrisonBuilderBulkGroups(IReadOnlyDictionary<string, string> edited)
    {
        if (_builderBulkGroupTargets.Count == 0) return;
        var groups = MapObjectMetadata.Groups(edited);
        foreach (var index in _builderBulkGroupTargets)
        {
            if (index < 0 || index >= _builderEntities.Count || index == _builderSelectedEntityIndex) continue;
            var entity = _builderEntities[index];
            var properties = new Dictionary<string, string>(entity.Properties) { [MapObjectMetadata.GroupsKey] = groups };
            _builderEntities[index] = entity with { Properties = properties };
        }
    }

    private void ReportMapScriptError()
    {
        var error = _world.MapScriptError;
        if (error != _lastReportedMapScriptError && error is not null) AddConsoleLine("Map script: " + error);
        _lastReportedMapScriptError = error;
    }
}
