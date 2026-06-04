#nullable enable

using Microsoft.Xna.Framework;
using OpenGarrison.Client.Plugins;
using OpenGarrison.Core;
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
            var layout = _game.GetCenteredPlaqueMenuLayout(
                tall: false,
                stackedActions.Count,
                includeSoloButton: soloAction is not null,
                includeBottomBarButton: bottomBarAction is not null || _game._menuBackgroundMode != Core.MenuBackgroundMode.Static);

            for (var index = 0; index < stackedActions.Count && index < layout.StackedButtonBounds.Length; index += 1)
            {
                buttons.Add(new MenuPageButton(stackedActions[index].Label, layout.StackedButtonBounds[index], stackedActions[index].Activate));
            }

            if (soloAction is not null)
            {
                buttons.Add(new MenuPageButton(soloAction.Value.Label, layout.SoloButtonBounds, soloAction.Value.Activate));
            }

            if (bottomBarAction is not null && layout.BottomBarButtonBounds.HasValue)
            {
                buttons.Add(new MenuPageButton(bottomBarLabel, layout.BottomBarButtonBounds.Value, bottomBarAction, IsBottomBarButton: true));
            }

            if (_game._mainMenuPage == MainMenuPage.Root)
            {
                if (GarrisonBuilderFeature.CanOpenFromMainMenu)
                {
                    var builderBounds = _game.GetBottomCenterPlaqueButtonBounds(layout);
                    if (builderBounds != Rectangle.Empty)
                    {
                        buttons.Add(new MenuPageButton("Builder", builderBounds, _game.OpenGarrisonBuilderFromMainMenu, IsBottomBarCenterButton: true));
                    }
                }

                var friendsBounds = _game.GetBottomRightPlaqueButtonBounds(layout);
                if (friendsBounds != Rectangle.Empty)
                {
                    buttons.Add(new MenuPageButton("Profile", friendsBounds, _game.OpenFriendsMenu, IsBottomBarRightButton: true));
                }
            }

            return buttons;
        }

        public void DrawCurrentMainMenuPage(IReadOnlyList<MenuPageButton> buttons)
        {
            var (stackedActions, soloAction, bottomBarLabel, bottomBarAction) = GetCurrentMainMenuActions();
            var layout = _game.GetCenteredPlaqueMenuLayout(
                tall: false,
                stackedActions.Count,
                includeSoloButton: soloAction is not null,
                includeBottomBarButton: bottomBarAction is not null || _game._menuBackgroundMode != Core.MenuBackgroundMode.Static);
            var hoveredStackedIndex = -1;
            var soloHovered = false;
            var bottomHovered = false;
            var bottomCenterHovered = false;
            var bottomRightHovered = false;

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
                else if (buttons[index].IsBottomBarCenterButton)
                {
                    bottomCenterHovered = true;
                }
                else if (buttons[index].IsBottomBarRightButton)
                {
                    bottomRightHovered = true;
                }
                else if (index < stackedActions.Count)
                {
                    hoveredStackedIndex = index;
                }
                else if (soloAction is not null)
                {
                    soloHovered = true;
                }
            }

            _game.DrawPlaqueMenuLayout(layout, stackedActions, soloAction, bottomBarAction is not null, bottomBarLabel, hoveredStackedIndex, soloHovered, bottomHovered, 1.15f);
            if (_game._mainMenuPage == MainMenuPage.Root)
            {
                if (GarrisonBuilderFeature.CanOpenFromMainMenu)
                {
                    _game.DrawBottomCenterPlaqueButton(layout, "Builder", bottomCenterHovered, 1.15f);
                }

                _game.DrawBottomRightPlaqueButton(layout, "Profile", bottomRightHovered, 1.15f);
            }
        }

        private (List<MenuPageAction> StackedActions, MenuPageAction? SoloAction, string BottomBarLabel, Action? BottomBarAction) GetCurrentMainMenuActions()
        {
            return _game._mainMenuPage switch
            {
                MainMenuPage.PlayOnline => (
                    [
                        new MenuPageAction("Host Match", _game.OpenHostSetupMenu),
                        new MenuPageAction("Join (IP)", _game.OpenManualConnectMenu),
                        new MenuPageAction("Join (Lobby)", _game.OpenLobbyBrowser),
                        new MenuPageAction("Watch", _game.OpenWatchBrowser),
                    ],
                    null,
                    "Back",
                    () => OpenMainMenuPage(MainMenuPage.Root)),
                MainMenuPage.PlayOffline => (
                    [
                        new MenuPageAction("Practice", _game.OpenPracticeSetupMenu),
                        new MenuPageAction("Minigames", () => OpenMainMenuPage(MainMenuPage.Minigames)),
                    ],
                    null,
                    "Back",
                    () => OpenMainMenuPage(MainMenuPage.Root)),
                MainMenuPage.Minigames => (
                    [
                        new MenuPageAction("Jump", () => _game.OpenJumpMenu()),
                        new MenuPageAction("Last to Die", () => _game.OpenLastToDieMenu()),
                    ],
                    null,
                    "Back",
                    () => OpenMainMenuPage(MainMenuPage.PlayOffline)),
                MainMenuPage.Credits => (
                    [
                        new MenuPageAction("Play Credits", _game.OpenCreditsMenu),
                    ],
                    null,
                    "Back",
                    () => OpenMainMenuPage(MainMenuPage.Root)),
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
