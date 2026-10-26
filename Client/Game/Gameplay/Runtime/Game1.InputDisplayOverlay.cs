#nullable enable

using Microsoft.Xna.Framework;
using OpenGarrison.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace OpenGarrison.Client;

// Fighting-game style input log ("show input on"): every newly pressed button is added as a
// line under the previous ones, labelled with its bound key (buttons pressed together share
// a line, like "A+M1"). Once the lines reach the bottom of the screen the oldest scroll off.
public partial class Game1
{
    private const int InputDisplayMaxEntries = 256;
    private const int InputDisplayLineHeight = 18;
    private const int InputDisplayTop = 96;

    private bool _inputDisplayEnabled;
    private PlayerInputSnapshot _inputDisplayPreviousInput;
    private readonly List<string> _inputDisplayEntries = new(InputDisplayMaxEntries);
    private readonly StringBuilder _inputDisplayBuilder = new();

    private void HandleInputDisplayConsoleCommand(string[] parts, int optionIndex)
    {
        var option = parts.Length > optionIndex ? parts[optionIndex].ToLowerInvariant() : "toggle";
        switch (option)
        {
            case "on":
            case "1":
                _inputDisplayEnabled = true;
                break;
            case "off":
            case "0":
                _inputDisplayEnabled = false;
                break;
            case "toggle":
                _inputDisplayEnabled = !_inputDisplayEnabled;
                break;
            case "clear":
                _inputDisplayEntries.Clear();
                break;
            case "status":
                break;
            default:
                AddConsoleLine("usage: show input <on|off|toggle|clear|status>");
                return;
        }

        if (!_inputDisplayEnabled)
        {
            _inputDisplayEntries.Clear();
        }

        AddConsoleLine(_inputDisplayEnabled ? "input display on" : "input display off");
    }

    public void RecordInputDisplay(PlayerInputSnapshot input)
    {
        var previous = _inputDisplayPreviousInput;
        _inputDisplayPreviousInput = input;
        if (!_inputDisplayEnabled || input.IsTypingChatMessage)
        {
            return;
        }

        _inputDisplayBuilder.Clear();
        AppendInputDisplayPress(input.Left, previous.Left, _inputBindings.MoveLeft);
        AppendInputDisplayPress(input.Right, previous.Right, _inputBindings.MoveRight);
        AppendInputDisplayPress(input.Up, previous.Up, _inputBindings.MoveUp);
        AppendInputDisplayPress(input.Down, previous.Down, _inputBindings.MoveDown);
        AppendInputDisplayPress(input.FirePrimary, previous.FirePrimary, "M1");
        AppendInputDisplayPress(input.FireSecondary, previous.FireSecondary, "M2");
        AppendInputDisplayPress(input.UseAbility, previous.UseAbility, _inputBindings.UseAbility);
        AppendInputDisplayPress(input.Taunt, previous.Taunt, _inputBindings.Taunt);
        if (_inputDisplayBuilder.Length == 0)
        {
            return;
        }

        if (_inputDisplayEntries.Count >= InputDisplayMaxEntries)
        {
            _inputDisplayEntries.RemoveAt(0);
        }

        _inputDisplayEntries.Add(_inputDisplayBuilder.ToString());
    }

    private void AppendInputDisplayPress(bool down, bool wasDown, InputBinding binding)
        => AppendInputDisplayPress(down, wasDown, GetBindingDisplayName(binding));

    private void AppendInputDisplayPress(bool down, bool wasDown, string label)
    {
        if (!down || wasDown)
        {
            return;
        }

        if (_inputDisplayBuilder.Length > 0)
        {
            _inputDisplayBuilder.Append('+');
        }

        _inputDisplayBuilder.Append(label);
    }

    private void DrawInputDisplayOverlay()
    {
        if (!_inputDisplayEnabled || _inputDisplayEntries.Count == 0)
        {
            return;
        }

        // Show as many of the newest entries as fit between the top offset and the bottom.
        var visibleCount = Math.Max(1, (ViewportHeight - InputDisplayTop - 8) / InputDisplayLineHeight);
        var firstIndex = Math.Max(0, _inputDisplayEntries.Count - visibleCount);
        var maxWidth = 0f;
        for (var index = firstIndex; index < _inputDisplayEntries.Count; index += 1)
        {
            maxWidth = MathF.Max(maxWidth, _consoleFont.MeasureString(_inputDisplayEntries[index]).X);
        }

        var lineCount = _inputDisplayEntries.Count - firstIndex;
        var background = new Rectangle(10, InputDisplayTop - 4, (int)MathF.Ceiling(maxWidth) + 16, lineCount * InputDisplayLineHeight + 8);
        _spriteBatch.Draw(_pixel, background, new Color(12, 16, 20, 170));
        var position = new Vector2(18, InputDisplayTop);
        for (var index = firstIndex; index < _inputDisplayEntries.Count; index += 1)
        {
            var color = index == _inputDisplayEntries.Count - 1 ? new Color(255, 232, 130) : new Color(232, 238, 244);
            _spriteBatch.DrawString(_consoleFont, _inputDisplayEntries[index], position, color);
            position.Y += InputDisplayLineHeight;
        }
    }
}
