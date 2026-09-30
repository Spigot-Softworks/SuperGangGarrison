#nullable enable

using Microsoft.Xna.Framework;
using OpenGarrison.Client.Plugins;
using OpenGarrison.Core;
using System;
using System.Collections.Generic;

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class MainMenuPageController
    {
        private readonly IMenuContext _context;

        public MainMenuPageController(IMenuContext context)
        {
            _context = context;
        }

        public void OpenMainMenuPage(MainMenuPage page)
        {
            if (IsRestrictedBrowserEdition || ClientShared.ClientDistribution.IsGg2Only) page = MainMenuPage.Root;
            _context._mainMenuPage = page;
            _context._mainMenuHoverIndex = -1;
            _context._mainMenuBottomBarHover = false;
        }

        public List<MenuPageButton> BuildMainMenuButtons()
        {
            var buttons = new List<MenuPageButton>();
            var (stackedActions, soloAction, bottomBarLabel, bottomBarAction) = GetCurrentMainMenuActions();
            var layout = _context.GetCenteredPlaqueMenuLayout(
                tall: false,
                stackedActions.Count,
                includeSoloButton: soloAction is not null,
                includeBottomBarButton: bottomBarAction is not null || (!ClientShared.ClientDistribution.IsGg2Only && _context._menuBackgroundMode != Core.MenuBackgroundMode.Static));

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

            if (_context._mainMenuPage == MainMenuPage.Root && !IsRestrictedBrowserEdition && !ClientShared.ClientDistribution.IsGg2Only)
            {
                if (GarrisonBuilderFeature.CanOpenFromMainMenu)
                {
                    var builderBounds = _context.GetBottomCenterPlaqueButtonBounds(layout);
                    if (builderBounds != Rectangle.Empty)
                    {
                        buttons.Add(new MenuPageButton("Builder", builderBounds, _context.OpenGarrisonBuilderFromMainMenu, IsBottomBarCenterButton: true));
                    }
                }

                var friendsBounds = _context.GetBottomRightPlaqueButtonBounds(layout);
                if (friendsBounds != Rectangle.Empty)
                {
                    buttons.Add(new MenuPageButton("Profile", friendsBounds, _context.OpenFriendsMenu, IsBottomBarRightButton: true));
                }
            }

            return buttons;
        }

        public void DrawCurrentMainMenuPage(IReadOnlyList<MenuPageButton> buttons)
        {
            var (stackedActions, soloAction, bottomBarLabel, bottomBarAction) = GetCurrentMainMenuActions();
            var layout = _context.GetCenteredPlaqueMenuLayout(
                tall: false,
                stackedActions.Count,
                includeSoloButton: soloAction is not null,
                includeBottomBarButton: bottomBarAction is not null || (!ClientShared.ClientDistribution.IsGg2Only && _context._menuBackgroundMode != Core.MenuBackgroundMode.Static));
            var hoveredStackedIndex = -1;
            var soloHovered = false;
            var bottomHovered = false;
            var bottomCenterHovered = false;
            var bottomRightHovered = false;

            for (var index = 0; index < buttons.Count; index += 1)
            {
                if (index != _context._mainMenuHoverIndex)
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

            _context.DrawPlaqueMenuLayout(layout, stackedActions, soloAction, bottomBarAction is not null, bottomBarLabel, hoveredStackedIndex, soloHovered, bottomHovered, 1.15f);
            if (_context._mainMenuPage == MainMenuPage.Root && !IsRestrictedBrowserEdition && !ClientShared.ClientDistribution.IsGg2Only)
            {
                if (GarrisonBuilderFeature.CanOpenFromMainMenu)
                {
                    _context.DrawBottomCenterPlaqueButton(layout, "Builder", bottomCenterHovered, 1.15f);
                }

                _context.DrawBottomRightPlaqueButton(layout, "Profile", bottomRightHovered, 1.15f);
            }
        }

        private (List<MenuPageAction> StackedActions, MenuPageAction? SoloAction, string BottomBarLabel, Action? BottomBarAction) GetCurrentMainMenuActions()
        {
            if (ClientShared.ClientDistribution.IsGg2Only)
            {
                return ([
                    new("Play", _context.OpenLobbyBrowser),
                    new("Settings", () => _context.OpenOptionsMenu(fromGameplay: false)),
                ], null, string.Empty, null);
            }
            if (IsRestrictedBrowserEdition)
            {
                return ([
                    new("Servers", _context.OpenGg2LobbyBrowser),
                    new("Practice", _context.OpenPracticeSetupMenu),
                    new("Last to Die", () => _context.OpenLastToDieMenu()),
                    new("Settings", () => _context.OpenOptionsMenu(fromGameplay: false)),
                ], null, string.Empty, null);
            }
            return _context._mainMenuPage switch
            {
                MainMenuPage.PlayOnline => (
                    [
                        new MenuPageAction("Join (Lobby)", _context.OpenLobbyBrowser),
                        new MenuPageAction("Join (IP)", _context.OpenManualConnectMenu),
                        new MenuPageAction("Watch", _context.OpenWatchBrowser),
                        new MenuPageAction("Host Match", _context.OpenHostSetupMenu),
                    ],
                    null,
                    "Back",
                    () => OpenMainMenuPage(MainMenuPage.Root)),
                MainMenuPage.PlayOffline => (
                    [
                        new MenuPageAction("Practice", _context.OpenPracticeSetupMenu),
                    ],
                    null,
                    "Back",
                    () => OpenMainMenuPage(MainMenuPage.Root)),
                _ => (
                    BuildRootMainMenuActions(),
                    null,
                    "Quit",
                    _context.OpenQuitPrompt),
            };
        }

        private List<MenuPageAction> BuildRootMainMenuActions()
        {
            var actions = new List<MenuPageAction>
            {
                new MenuPageAction("Play Online", () => OpenMainMenuPage(MainMenuPage.PlayOnline)),
                new MenuPageAction("Play Offline", () => OpenMainMenuPage(MainMenuPage.PlayOffline)),
                new MenuPageAction("Last to Die", () => _context.OpenLastToDieMenu()),
                new MenuPageAction("Settings", () => _context.OpenOptionsMenu(fromGameplay: false)),
            };

            AddPluginMenuActions(actions, ClientPluginMenuLocation.MainMenuRoot);
            return actions;
        }

        public void AddPluginMenuActions(List<MenuPageAction> actions, ClientPluginMenuLocation location, int insertIndex = -1)
        {
            if (IsRestrictedBrowserEdition || ClientShared.ClientDistribution.IsGg2Only) return;
            var pluginEntries = _context._clientPluginHost?.GetMenuEntries(location) ?? [];
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
