#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace OpenGarrison.Client;

public partial class Game1
{
    public HudLayoutProfile _hudLayoutProfile = new();
    public readonly Dictionary<string, HudResolvedElement> _hudResolvedElements = new(StringComparer.Ordinal);
    public readonly Dictionary<string, Rectangle> _hudElementLastBounds = new(StringComparer.Ordinal);
    public readonly Dictionary<string, float> _hudElementAutoFadeOpacities = new(StringComparer.Ordinal);
    public bool _hudEditorOpen;
    public bool _hudEditorOpenedFromOptions;
    public int _hudEditorDummyAbilitySlotCount;

    public HudEditorController HudEditor => _hudManager.Editor;

    public void LoadHudLayout()
    {
        _hudLayoutProfile = HudLayoutStore.Load();
    }

    public void SaveHudLayout()
    {
        HudLayoutStore.Save(_hudLayoutProfile);
    }

    public void ResetHudLayoutElements()
    {
        _hudLayoutProfile.ResetElements();
        SaveHudLayout();
    }

    public void BeginHudElementFrame()
    {
        _hudManager.LocalStatus.BeginHudFrame();
        _hudResolvedElements.Clear();
        _hudLayoutProfile.ClearRuntimeDefaults();
    }

    public bool TryResolveHudElement(string id, out HudResolvedElement resolved)
    {
        if (!_hudLayoutProfile.TryResolve(id, ViewportWidth, ViewportHeight, out resolved))
        {
            return false;
        }

        _hudResolvedElements[id] = resolved;
        RecordHudElementBounds(id, resolved.Bounds);
        return true;
    }


    public bool TryResolveHudElementEvenIfHidden(string id, out HudResolvedElement resolved)
    {
        return _hudLayoutProfile.TryResolveEvenIfHidden(id, ViewportWidth, ViewportHeight, out resolved);
    }

    public void UpdateHudElementBounds(string id, Rectangle bounds)
    {
        if (!_hudResolvedElements.TryGetValue(id, out var resolved))
        {
            return;
        }

        _hudResolvedElements[id] = resolved with { Bounds = bounds };
        RecordHudElementBounds(id, bounds);
    }

    private void RecordHudElementBounds(string id, Rectangle bounds)
    {
        if (bounds.Width > 0 && bounds.Height > 0)
        {
            _hudElementLastBounds[id] = bounds;
        }
    }

    public void SetHudElementRuntimeDefault(HudElementLayout layout)
    {
        _hudLayoutProfile.SetRuntimeDefault(layout);
    }

    public Dictionary<string, HudResolvedElement> GetHudEditorElements()
    {
        var elements = new Dictionary<string, HudResolvedElement>(_hudResolvedElements, StringComparer.Ordinal);
        foreach (var id in _hudLayoutProfile.Overrides.Keys)
        {
            if (!elements.ContainsKey(id)
                && _hudLayoutProfile.TryResolveEvenIfHidden(id, ViewportWidth, ViewportHeight, out var resolved))
            {
                elements[id] = resolved;
            }
        }

        return elements;
    }

    public void SetHudElementOrigin(string id, Vector2 origin)
    {
        _hudLayoutProfile.SetElementOrigin(id, origin, ViewportWidth, ViewportHeight);
    }

    public bool SetHudElementScale(string id, float scale)
    {
        return _hudLayoutProfile.SetElementScale(id, scale);
    }

    public bool SetHudElementVisibility(string id, bool visible)
    {
        return _hudLayoutProfile.SetElementVisibility(id, visible);
    }

    public void OpenHudEditor(bool openedFromOptions)
    {
        if (_mainMenuOpen)
        {
            _menuStatusMessage = "HUD editing is available in game.";
            return;
        }

        _hudEditorOpen = true;
        _hudEditorOpenedFromOptions = openedFromOptions;
        _hudEditorDummyAbilitySlotCount = 0;
        _optionsMenuOpen = false;
        _optionsMenuOpenedFromGameplay = false;
        _inGameMenuOpen = false;
        _inGameMenuAwaitingEscapeRelease = false;
        _controlsMenuOpen = false;
        _controlsMenuOpenedFromGameplay = false;
        _pendingControlsBinding = null;
        _pendingControllerControlsBinding = null;
        _pluginOptionsMenuOpen = false;
        HudEditor.Open();
    }

    public void CloseHudEditor()
    {
        _hudEditorOpen = false;
        SaveHudLayout();
        HudEditor.Close();
        _hudEditorDummyAbilitySlotCount = 0;

        if (_hudEditorOpenedFromOptions && !_mainMenuOpen)
        {
            _hudEditorOpenedFromOptions = false;
            OpenOptionsMenu(fromGameplay: true);
            _optionsPageIndex = 2;
        }
        else
        {
            _hudEditorOpenedFromOptions = false;
        }
    }

    public void UpdateHudEditor(KeyboardState keyboard, MouseState mouse)
    {
        HudEditor.Update(keyboard, mouse);
    }

    private void DrawHudEditor()
    {
        HudEditor.Draw();
    }

    public int GetHudEditorDummyAbilitySlotCount()
    {
        return _hudEditorOpen ? _hudEditorDummyAbilitySlotCount : 0;
    }

    public void AddHudEditorDummyAbilitySlot()
    {
        if (!_hudEditorOpen)
        {
            return;
        }

        _hudEditorDummyAbilitySlotCount = Math.Min(_hudEditorDummyAbilitySlotCount + 1, 8);
    }
}
