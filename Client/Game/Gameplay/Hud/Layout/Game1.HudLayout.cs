#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace OpenGarrison.Client;

public partial class Game1
{
    private HudLayoutProfile _hudLayoutProfile = new();
    private readonly Dictionary<string, HudResolvedElement> _hudResolvedElements = new(StringComparer.Ordinal);
    private HudEditorController? _hudEditorController;
    private bool _hudEditorOpen;
    private bool _hudEditorOpenedFromOptions;
    private int _hudEditorDummyAbilitySlotCount;

    private HudEditorController HudEditor => _hudEditorController ??= new HudEditorController(this);

    private void LoadHudLayout()
    {
        _hudLayoutProfile = HudLayoutStore.Load();
    }

    private void SaveHudLayout()
    {
        HudLayoutStore.Save(_hudLayoutProfile);
    }

    private void ResetHudLayoutElements()
    {
        _hudLayoutProfile.ResetElements();
        SaveHudLayout();
    }

    private void BeginHudElementFrame()
    {
        _hudResolvedElements.Clear();
        _hudLayoutProfile.ClearRuntimeDefaults();
    }

    private bool TryResolveHudElement(string id, out HudResolvedElement resolved)
    {
        if (!_hudLayoutProfile.TryResolve(id, ViewportWidth, ViewportHeight, out resolved))
        {
            return false;
        }

        _hudResolvedElements[id] = resolved;
        return true;
    }

    private bool TryResolveHudElementOrigin(string id, out Vector2 origin)
    {
        if (TryResolveHudElement(id, out var resolved))
        {
            origin = resolved.Origin;
            return true;
        }

        origin = Vector2.Zero;
        return false;
    }

    private bool TryResolveHudElementEvenIfHidden(string id, out HudResolvedElement resolved)
    {
        return _hudLayoutProfile.TryResolveEvenIfHidden(id, ViewportWidth, ViewportHeight, out resolved);
    }

    private void UpdateHudElementBounds(string id, Rectangle bounds)
    {
        if (!_hudResolvedElements.TryGetValue(id, out var resolved))
        {
            return;
        }

        _hudResolvedElements[id] = resolved with { Bounds = bounds };
    }

    private void SetHudElementRuntimeDefault(HudElementLayout layout)
    {
        _hudLayoutProfile.SetRuntimeDefault(layout);
    }

    private Dictionary<string, HudResolvedElement> GetHudEditorElements()
    {
        return new Dictionary<string, HudResolvedElement>(_hudResolvedElements, StringComparer.Ordinal);
    }

    private void SetHudElementOrigin(string id, Vector2 origin)
    {
        _hudLayoutProfile.SetElementOrigin(id, origin, ViewportWidth, ViewportHeight);
    }

    private bool SetHudElementScale(string id, float scale)
    {
        return _hudLayoutProfile.SetElementScale(id, scale);
    }

    private void OpenHudEditor(bool openedFromOptions)
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

    private void CloseHudEditor()
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

    private void UpdateHudEditor(KeyboardState keyboard, MouseState mouse)
    {
        HudEditor.Update(keyboard, mouse);
    }

    private void DrawHudEditor()
    {
        HudEditor.Draw();
    }

    private int GetHudEditorDummyAbilitySlotCount()
    {
        return _hudEditorOpen ? _hudEditorDummyAbilitySlotCount : 0;
    }

    private void AddHudEditorDummyAbilitySlot()
    {
        if (!_hudEditorOpen)
        {
            return;
        }

        _hudEditorDummyAbilitySlotCount = Math.Min(_hudEditorDummyAbilitySlotCount + 1, 8);
    }
}
