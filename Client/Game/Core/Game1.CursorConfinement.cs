#nullable enable

using System;
using System.Runtime.InteropServices;

namespace OpenGarrison.Client;

public partial class Game1
{
    private bool _cursorConfinedToWindow;
    private IntPtr _cursorConfinedWindowHandle;

    /// <summary>
    /// Keeps the cursor inside the game window while playing in a windowed display
    /// mode, so a flick of the mouse cannot click another program mid-fight. The
    /// cursor is released whenever a menu is open (pause/in-game menus, main menu,
    /// options, console, builder) or the window loses focus. Screen-filling modes
    /// already constrain the cursor and are left alone.
    /// </summary>
    private void UpdateGameplayCursorConfinement()
    {
        if (OperatingSystem.IsBrowser())
        {
            return;
        }

        SetCursorConfinedToWindow(ShouldConfineCursorToWindow());
    }

    private bool ShouldConfineCursorToWindow()
    {
        return IsActive
            && !IsScreenFillingDisplayMode(_menuManager.DisplaySettings.DisplayMode)
            && _gameplaySessionKind != GameplaySessionKind.None
            && !_mainMenuOpen
            && !HasOpenGameplayBlockingMenu()
            && !_consoleOpen
            && !_passwordPromptOpen;
    }

    private void SetCursorConfinedToWindow(bool confined)
    {
        IntPtr handle;
        try
        {
            handle = Window.Handle;
        }
        catch (Exception)
        {
            return;
        }

        if (handle == IntPtr.Zero
            || (confined == _cursorConfinedToWindow && handle == _cursorConfinedWindowHandle))
        {
            return;
        }

        try
        {
            SDL_SetWindowGrab(handle, confined ? 1 : 0);
            _cursorConfinedToWindow = confined;
            _cursorConfinedWindowHandle = handle;
        }
        catch (DllNotFoundException)
        {
            _cursorConfinedWindowHandle = handle;
        }
        catch (EntryPointNotFoundException)
        {
            _cursorConfinedWindowHandle = handle;
        }
        catch (BadImageFormatException)
        {
            _cursorConfinedWindowHandle = handle;
        }
    }

    // SDL 2: confines the mouse (not the keyboard, unless SDL_HINT_GRAB_KEYBOARD is set).
    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    private static extern void SDL_SetWindowGrab(IntPtr window, int grabbed);
}
