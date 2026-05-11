#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const int NavEditorPanelWidth = 0;
    private const int NavEditorPanelMargin = 0;
    private const int NavEditorPanelExpandedHeight = 0;
    private bool _navEditorEnabled;
    private bool _navEditorShowBotTags => _navEditorEnabled && false;
    private string _navEditorStatusMessage = "nav editor removed with legacy bot navigation stack";

    private void EnableNavEditor()
    {
        _navEditorEnabled = false;
        AddConsoleLine(_navEditorStatusMessage);
    }

    private void DisableNavEditor(string reason)
    {
        _navEditorEnabled = false;
        _navEditorStatusMessage = reason;
    }

    private void SaveNavEditorState()
    {
        AddConsoleLine(_navEditorStatusMessage);
    }

    private void ReloadNavEditorState(string statusMessage)
    {
        _navEditorStatusMessage = statusMessage;
        AddConsoleLine("nav editor reload skipped; editor was removed");
    }

    private void StartNavEditorRebuild()
    {
        AddConsoleLine(_navEditorStatusMessage);
    }

    private void SetNavEditorStatus(string statusMessage)
    {
        _navEditorStatusMessage = statusMessage;
    }

    private void UpdateNavEditor(KeyboardState keyboard, MouseState mouse, MouseState panelMouse, Vector2 cameraPosition, float deltaSeconds)
    {
        if (_navEditorEnabled)
        {
            _navEditorEnabled = false;
        }
    }

    private void DrawNavEditorOverlay(MouseState mouse, Vector2 cameraPosition)
    {
        _ = _navEditorEnabled;
    }

    private void DrawNavEditorPresentationOverlay(MouseState mouse)
    {
        _ = _navEditorEnabled;
    }

    private void OnNavEditorTraversalCaptureBeforeTick()
    {
        _ = _navEditorEnabled;
    }

    private void OnNavEditorTraversalCaptureAfterTick()
    {
        _ = _navEditorEnabled;
    }

    private bool HandleNavEditorTextInput(char character)
    {
        return _navEditorEnabled && character == '\0';
    }

    private bool ShouldBlockGameplayForNavEditor()
    {
        return _navEditorEnabled;
    }

    private void SetNavEditorTraversalCaptureInput(PlayerInputSnapshot gameplayInput)
    {
        if (_navEditorEnabled && gameplayInput.Up)
        {
            _navEditorStatusMessage = "nav editor traversal capture removed";
        }
    }

    private PlayerInputSnapshot ResolveNavEditorGameplayInput(PlayerInputSnapshot gameplayInput)
    {
        return _navEditorEnabled ? default : gameplayInput;
    }
}
