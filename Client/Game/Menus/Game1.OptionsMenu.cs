#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Globalization;
using OpenGarrison.Client.Plugins;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void UpdateOptionsMenu(KeyboardState keyboard, MouseState mouse)
    {
        if (IsKeyPressed(keyboard, Keys.Escape))
        {
            if (_editingPlayerName)
            {
                _editingPlayerName = false;
                _playerNameEditBuffer = _world.LocalPlayer.DisplayName;
                return;
            }

            CloseOptionsMenu();
            return;
        }

        if (_editingPlayerName)
        {
            return;
        }

        var buttons = BuildOptionsMenuButtons();
        _optionsHoverIndex = -1;
        for (var index = 0; index < buttons.Count; index += 1)
        {
            if (!buttons[index].Bounds.Contains(mouse.Position))
            {
                continue;
            }

            _optionsHoverIndex = index;
            break;
        }

        var clickPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed;
        if (!clickPressed || _optionsHoverIndex < 0)
        {
            return;
        }

        buttons[_optionsHoverIndex].Activate();
    }

    private void DrawOptionsMenu()
    {
        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * 0.8f);
        var actions = GetVisibleOptionsMenuActions(out _);
        var stackedActions = new List<MenuPageAction>(actions)
        {
            new("Next Page", AdvanceOptionsPage),
        };
        var layout = GetCenteredPlaqueMenuLayout(tall: true, stackedActions.Count, includeBottomBarButton: false);
        var hoveredStackedIndex = _optionsHoverIndex >= 0 && _optionsHoverIndex < stackedActions.Count
            ? _optionsHoverIndex
            : -1;
        var soloHovered = _optionsHoverIndex == stackedActions.Count;
        DrawPlaqueMenuLayout(layout, stackedActions, new MenuPageAction("Back", CloseOptionsMenu), false, string.Empty, hoveredStackedIndex, soloHovered, false);
    }

    private static string GetParticleModeLabel(int particleMode)
    {
        return particleMode switch
        {
            0 => "Normal",
            2 => "Alternative (faster)",
            _ => "Disabled",
        };
    }

    private static MusicMode GetNextMusicMode(MusicMode musicMode)
    {
        return musicMode switch
        {
            MusicMode.None => MusicMode.MenuOnly,
            MusicMode.MenuOnly => MusicMode.InGameOnly,
            MusicMode.InGameOnly => MusicMode.MenuAndInGame,
            _ => MusicMode.None,
        };
    }

    private static string GetMusicModeLabel(MusicMode musicMode)
    {
        return musicMode switch
        {
            MusicMode.None => "None",
            MusicMode.MenuOnly => "Menu Only",
            MusicMode.InGameOnly => "In-Game Only",
            _ => "Menu and In-Game",
        };
    }

    private static string GetGibLevelLabel(int gibLevel)
    {
        return gibLevel switch
        {
            0 => "0, No blood or gibs",
            1 => "1, Blood only",
            2 => "2, Blood and medium gibs",
            _ => $"{gibLevel.ToString(CultureInfo.InvariantCulture)}, Full blood and gibs",
        };
    }

    private static string GetCorpseDurationLabel(int corpseDurationMode)
    {
        return corpseDurationMode == ClientSettings.CorpseDurationInfinite
            ? "Infinite"
            : "300 ticks";
    }

    private List<MenuPageButton> BuildOptionsMenuButtons()
    {
        var buttons = new List<MenuPageButton>();
        var visibleActions = GetVisibleOptionsMenuActions(out _);
        var stackedCount = visibleActions.Count + 1;
        var layout = GetCenteredPlaqueMenuLayout(tall: true, stackedCount, includeBottomBarButton: false);

        for (var index = 0; index < visibleActions.Count && index < layout.StackedButtonBounds.Length; index += 1)
        {
            buttons.Add(new MenuPageButton(visibleActions[index].Label, layout.StackedButtonBounds[index], visibleActions[index].Activate));
        }

        if (visibleActions.Count < layout.StackedButtonBounds.Length)
        {
            buttons.Add(new MenuPageButton("Next Page", layout.StackedButtonBounds[visibleActions.Count], AdvanceOptionsPage));
        }

        buttons.Add(new MenuPageButton("Back", layout.SoloButtonBounds, CloseOptionsMenu));
        return buttons;
    }

    private List<MenuPageAction> GetVisibleOptionsMenuActions(out int pageCount)
    {
        const int itemsPerPage = 5;
        var allActions = BuildOptionsMenuActions();
        pageCount = Math.Max(1, (int)Math.Ceiling(allActions.Count / (float)itemsPerPage));
        _optionsPageIndex = ((_optionsPageIndex % pageCount) + pageCount) % pageCount;
        var startIndex = _optionsPageIndex * itemsPerPage;
        var count = Math.Min(itemsPerPage, Math.Max(0, allActions.Count - startIndex));
        return count > 0
            ? allActions.GetRange(startIndex, count)
            : [];
    }

    private List<MenuPageAction> BuildOptionsMenuActions()
    {
        var actions = new List<MenuPageAction>
        {
            new($"Player Name: {(_editingPlayerName ? _playerNameEditBuffer + "_" : _world.LocalPlayer.DisplayName)}", BeginEditingPlayerName),
            new($"Fullscreen: {(_graphics.IsFullScreen ? "On" : "Off")}", ToggleFullscreenSetting),
            new($"Music: {GetMusicModeLabel(_musicMode)}", CycleMusicModeSetting),
            new($"Aspect Ratio: {GetIngameResolutionLabel(_ingameResolution)}", CycleIngameResolutionSetting),
            new($"Particles: {GetParticleModeLabel(_particleMode)}", CycleParticleModeSetting),
            new($"Gibs: {GetGibLevelLabel(_gibLevel)}", CycleGibLevelSetting),
            new($"Corpses: {GetCorpseDurationLabel(_corpseDurationMode)}", CycleCorpseDurationSetting),
            new($"Healer Radar: {(_healerRadarEnabled ? "Enabled" : "Disabled")}", ToggleHealerRadarSetting),
            new($"Show Healer: {(_showHealerEnabled ? "Enabled" : "Disabled")}", ToggleShowHealerSetting),
            new($"Show Healing: {(_showHealingEnabled ? "Enabled" : "Disabled")}", ToggleShowHealingSetting),
            new($"Healthbar: {(_showHealthBarEnabled ? "Enabled" : "Disabled")}", ToggleShowHealthBarSetting),
            new($"Persistent Name: {(_showPersistentSelfNameEnabled ? "Enabled" : "Disabled")}", TogglePersistentSelfNameSetting),
            new($"Sprite Shadow: {(_spriteDropShadowEnabled ? "Enabled" : "Disabled")}", ToggleSpriteDropShadowSetting),
            new($"Kill Cam: {(_killCamEnabled ? "Enabled" : "Disabled")}", ToggleKillCamSetting),
            new($"V Sync: {(_graphics.SynchronizeWithVerticalRetrace ? "Enabled" : "Disabled")}", ToggleVSyncSetting),
            new("Controls", OpenControlsMenuFromOptions),
        };

        if (HasClientPluginOptions())
        {
            actions.Add(new MenuPageAction("Plugin Options", OpenPluginOptionsMenuFromOptions));
        }

        return actions;
    }

    private void AdvanceOptionsPage()
    {
        GetVisibleOptionsMenuActions(out var pageCount);
        _optionsPageIndex = (_optionsPageIndex + 1) % Math.Max(1, pageCount);
        _optionsHoverIndex = -1;
    }

    private void BeginEditingPlayerName()
    {
        _editingPlayerName = true;
        _playerNameEditBuffer = _world.LocalPlayer.DisplayName;
    }

    private void ToggleFullscreenSetting()
    {
        _clientSettings.Fullscreen = !_clientSettings.Fullscreen;
        ApplyGraphicsSettings();
    }

    private void CycleMusicModeSetting()
    {
        _musicMode = GetNextMusicMode(_musicMode);
        StopMenuMusic();
        StopFaucetMusic();
        StopIngameMusic();
        PersistClientSettings();
    }

    private void CycleIngameResolutionSetting()
    {
        _clientSettings.IngameResolution = GetNextIngameResolution(_clientSettings.IngameResolution);
        ApplyGraphicsSettings();
    }

    private void CycleParticleModeSetting()
    {
        _particleMode = (_particleMode + 2) % 3;
        PersistClientSettings();
    }

    private void CycleGibLevelSetting()
    {
        _gibLevel = _gibLevel switch
        {
            0 => 1,
            1 => 2,
            2 => 3,
            _ => 0,
        };
        PersistClientSettings();
    }

    private void CycleCorpseDurationSetting()
    {
        _corpseDurationMode = _corpseDurationMode == ClientSettings.CorpseDurationInfinite
            ? ClientSettings.CorpseDurationDefault
            : ClientSettings.CorpseDurationInfinite;
        if (_corpseDurationMode != ClientSettings.CorpseDurationInfinite)
        {
            ResetRetainedDeadBodies();
        }

        PersistClientSettings();
    }

    private void ToggleHealerRadarSetting()
    {
        _healerRadarEnabled = !_healerRadarEnabled;
        PersistClientSettings();
    }

    private void ToggleShowHealerSetting()
    {
        _showHealerEnabled = !_showHealerEnabled;
        PersistClientSettings();
    }

    private void ToggleShowHealingSetting()
    {
        _showHealingEnabled = !_showHealingEnabled;
        PersistClientSettings();
    }

    private void ToggleShowHealthBarSetting()
    {
        _showHealthBarEnabled = !_showHealthBarEnabled;
        PersistClientSettings();
    }

    private void TogglePersistentSelfNameSetting()
    {
        _showPersistentSelfNameEnabled = !_showPersistentSelfNameEnabled;
        PersistClientSettings();
    }

    private void ToggleSpriteDropShadowSetting()
    {
        _spriteDropShadowEnabled = !_spriteDropShadowEnabled;
        PersistClientSettings();
    }

    private void ToggleKillCamSetting()
    {
        _killCamEnabled = !_killCamEnabled;
        PersistClientSettings();
    }

    private void ToggleVSyncSetting()
    {
        _clientSettings.VSync = !_clientSettings.VSync;
        ApplyGraphicsSettings();
    }

    private void OpenControlsMenuFromOptions()
    {
        OpenControlsMenu(_optionsMenuOpenedFromGameplay);
    }

    private void OpenPluginOptionsMenuFromOptions()
    {
        OpenPluginOptionsMenu(_optionsMenuOpenedFromGameplay);
    }

    private void GetOptionsMenuLayout(int rowCount, out float xbegin, out float ybegin, out float spacing, out float width, out float valueX)
    {
        xbegin = 40f;
        valueX = 240f;
        width = 320f;
        if (ViewportHeight < 540)
        {
            spacing = 26f;
            width = 340f;
            valueX = 232f;
        }
        else
        {
            spacing = 30f;
        }

        var compactLayout = ViewportHeight < 540;
        var defaultY = compactLayout ? 104f : 170f;
        var minY = compactLayout ? 24f : 40f;
        var bottomPadding = compactLayout ? 18f : 40f;
        var estimatedTextHeight = compactLayout ? 18f : 22f;
        var totalHeight = Math.Max(0, rowCount - 1) * spacing + estimatedTextHeight;
        ybegin = MathF.Min(defaultY, MathF.Max(minY, ViewportHeight - bottomPadding - totalHeight));
    }

    private void DrawMenuPanelBackdrop(Rectangle rectangle, float alpha)
    {
        if (rectangle.Width <= 0 || rectangle.Height <= 0)
        {
            return;
        }

        DrawInsetHudPanel(
            rectangle,
            new Color(184, 178, 160) * (alpha * 0.35f),
            new Color(24, 27, 32) * (alpha * 0.85f));
    }

    private void DrawMenuPlaqueRows(Vector2 position, int rowCount, float spacing, float width, float alpha)
    {
        for (var index = 0; index < rowCount; index += 1)
        {
            DrawMenuPlaque(position.X - 6f, position.Y + (index * spacing) - 4f, width, alpha);
        }
    }

    private void DrawMenuPlaque(float x, float y, float width, float alpha)
    {
        if (width <= 0f)
        {
            return;
        }

        var tint = Color.White * alpha;
        const float plaqueSpriteWidth = 17f;
        if (!TryDrawScreenSprite("gbMenuLayoutS", 0, new Vector2(x, y), tint, Vector2.One))
        {
            _spriteBatch.Draw(
                _pixel,
                new Rectangle((int)MathF.Round(x), (int)MathF.Round(y), Math.Max(1, (int)MathF.Round(width)), 17),
                new Color(70, 74, 82) * (alpha * 0.55f));
            return;
        }

        var middleScaleX = Math.Max(1f, (width / (plaqueSpriteWidth - 1f)) - 2f);
        TryDrawScreenSprite("gbMenuLayoutS", 1, new Vector2(x + plaqueSpriteWidth, y), tint, new Vector2(middleScaleX, 1f));
        TryDrawScreenSprite("gbMenuLayoutS", 2, new Vector2(x - plaqueSpriteWidth + width + 1f, y), tint, Vector2.One);
    }
}
