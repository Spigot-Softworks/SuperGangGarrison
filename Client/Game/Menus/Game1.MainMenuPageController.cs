#nullable enable

using OpenGarrison.Client.Plugins;
using System;
using System.Collections.Generic;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class MainMenuPageController
    {
        private readonly Game1 _game;

        public MainMenuPageController(Game1 game)
        {
            _game = game;
        }

        public void OpenMainMenuPage(MainMenuPage page)
        {
            _game._mainMenuPage = page;
            _game._mainMenuHoverIndex = -1;
            _game._mainMenuBottomBarHover = false;
        }

        public List<MenuPageButton> BuildMainMenuButtons()
        {
            var buttons = new List<MenuPageButton>();
            var (stackedActions, soloAction, bottomBarLabel, bottomBarAction) = GetCurrentMainMenuActions();
            var layout = _game.GetCenteredPlaqueMenuLayout(tall: false, stackedActions.Count, includeBottomBarButton: bottomBarAction is not null);

            for (var index = 0; index < stackedActions.Count && index < layout.StackedButtonBounds.Length; index += 1)
            {
                buttons.Add(new MenuPageButton(stackedActions[index].Label, layout.StackedButtonBounds[index], stackedActions[index].Activate));
            }

            buttons.Add(new MenuPageButton(soloAction.Label, layout.SoloButtonBounds, soloAction.Activate));

            if (bottomBarAction is not null && layout.BottomBarButtonBounds.HasValue)
            {
                buttons.Add(new MenuPageButton(bottomBarLabel, layout.BottomBarButtonBounds.Value, bottomBarAction, IsBottomBarButton: true));
            }

            return buttons;
        }

        public void DrawCurrentMainMenuPage(IReadOnlyList<MenuPageButton> buttons)
        {
            var (stackedActions, soloAction, bottomBarLabel, bottomBarAction) = GetCurrentMainMenuActions();
            var layout = _game.GetCenteredPlaqueMenuLayout(tall: false, stackedActions.Count, includeBottomBarButton: bottomBarAction is not null);
            var hoveredStackedIndex = -1;
            var soloHovered = false;
            var bottomHovered = false;

            for (var index = 0; index < buttons.Count; index += 1)
            {
                if (index != _game._mainMenuHoverIndex)
                {
                    continue;
                }

                if (buttons[index].IsBottomBarButton)
                {
                    bottomHovered = true;
                }
                else if (index < stackedActions.Count)
                {
                    hoveredStackedIndex = index;
                }
                else
                {
                    soloHovered = true;
                }
            }

            _game.DrawPlaqueMenuLayout(layout, stackedActions, soloAction, bottomBarAction is not null, bottomBarLabel, hoveredStackedIndex, soloHovered, bottomHovered, 1.15f);
        }

        private (List<MenuPageAction> StackedActions, MenuPageAction SoloAction, string BottomBarLabel, Action? BottomBarAction) GetCurrentMainMenuActions()
        {
            return _game._mainMenuPage switch
            {
                MainMenuPage.PlayOnline => (
                    [
                        new MenuPageAction("Host Match", _game.OpenHostSetupMenu),
                        new MenuPageAction("Join (IP)", _game.OpenManualConnectMenu),
                        new MenuPageAction("Join (Lobby)", _game.OpenLobbyBrowser),
                    ],
                    new MenuPageAction("Back", () => OpenMainMenuPage(MainMenuPage.Root)),
                    string.Empty,
                    null),
                MainMenuPage.PlayOffline => (
                    [
                        new MenuPageAction("Practice", _game.OpenPracticeSetupMenu),
                        new MenuPageAction("Minigames", () => OpenMainMenuPage(MainMenuPage.Minigames)),
                    ],
                    new MenuPageAction("Back", () => OpenMainMenuPage(MainMenuPage.Root)),
                    string.Empty,
                    null),
                MainMenuPage.Minigames => (
                    [
                        new MenuPageAction("Last to Die", () => _game.OpenLastToDieMenu()),
                    ],
                    new MenuPageAction("Back", () => OpenMainMenuPage(MainMenuPage.PlayOffline)),
                    string.Empty,
                    null),
                MainMenuPage.Credits => (
                    [
                        new MenuPageAction("Play Credits", _game.OpenCreditsMenu),
                    ],
                    new MenuPageAction("Back", () => OpenMainMenuPage(MainMenuPage.Root)),
                    string.Empty,
                    null),
                _ => (
                    BuildRootMainMenuActions(),
                    new MenuPageAction("Credits", () => OpenMainMenuPage(MainMenuPage.Credits)),
                    "Quit",
                    _game.OpenQuitPrompt),
            };
        }

        private List<MenuPageAction> BuildRootMainMenuActions()
        {
            var actions = new List<MenuPageAction>
            {
                new MenuPageAction("Play Online", () => OpenMainMenuPage(MainMenuPage.PlayOnline)),
                new MenuPageAction("Play Offline", () => OpenMainMenuPage(MainMenuPage.PlayOffline)),
                new MenuPageAction("Settings", () => _game.OpenOptionsMenu(fromGameplay: false)),
            };

            AddPluginMenuActions(actions, ClientPluginMenuLocation.MainMenuRoot);
            return actions;
        }

        public void AddPluginMenuActions(List<MenuPageAction> actions, ClientPluginMenuLocation location, int insertIndex = -1)
        {
            var pluginEntries = _game._clientPluginHost?.GetMenuEntries(location) ?? [];
            if (pluginEntries.Count == 0)
            {
                return;
            }

            var insertionIndex = insertIndex < 0
                ? actions.Count
                : Math.Clamp(insertIndex, 0, actions.Count);
            for (var index = 0; index < pluginEntries.Count; index += 1)
            {
                var entry = pluginEntries[index];
                actions.Insert(insertionIndex + index, new MenuPageAction(entry.Label, entry.Activate));
            }
        }
    }
}
