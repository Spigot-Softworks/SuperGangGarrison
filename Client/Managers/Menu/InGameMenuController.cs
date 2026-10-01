#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Client.Plugins;
using OpenGarrison.Protocol;
using System;
using System.Collections.Generic;

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class InGameMenuController
    {
        private readonly IMenuContext _context;
        private bool _inGameMenuAwaitingEscapeRelease;
        private int _inGameMenuHoverIndex = -1;

        public InGameMenuController(IMenuContext context)
        {
            _context = context;
        }

        internal void ResetAwaitingEscapeRelease()
        {
            _inGameMenuAwaitingEscapeRelease = false;
        }

        internal void ResetHoverIndex()
        {
            _inGameMenuHoverIndex = 0;
        }

        public void OpenInGameMenu()
        {
            _context._jukeboxMenuOpen = false;
            _context._inGameMenuOpen = true;
            _inGameMenuAwaitingEscapeRelease = true;
            _inGameMenuHoverIndex = -1;
            _context._clientPowersOpen = false;
            _context._clientPowersOpenedFromGameplay = false;
            _context._optionsMenuOpen = false;
            _context._pluginOptionsMenuOpen = false;
            _context._controlsMenuOpen = false;
            _context.DismissCustomBubbleEditor();
            _context._editingPlayerName = false;
            _context._pendingControlsBinding = null;
            _context._pendingControllerControlsBinding = null;
        }

        public void CloseInGameMenu()
        {
            _context._inGameMenuOpen = false;
            _inGameMenuAwaitingEscapeRelease = false;
            _inGameMenuHoverIndex = -1;
        }

        public void UpdateInGameMenu(KeyboardState keyboard, MouseState mouse)
        {
            var items = GetInGameMenuActions();
            GetInGameMenuLayout(items.Count, out _, out var itemBounds, out _, out _);

            if (_inGameMenuAwaitingEscapeRelease)
            {
                if (!keyboard.IsKeyDown(Keys.Escape))
                {
                    _inGameMenuAwaitingEscapeRelease = false;
                }
            }
            else if (_context.IsKeyPressed(keyboard, Keys.Escape) || _context.IsControllerMenuBackPressed())
            {
                CloseInGameMenu();
                return;
            }

            var mouseHoverIndex = -1;
            for (var index = 0; index < itemBounds.Length; index += 1)
            {
                if (itemBounds[index].Contains(mouse.Position))
                {
                    mouseHoverIndex = index;
                    break;
                }
            }

            if (_context.ShouldUseMouseMenuHover(mouse) && mouseHoverIndex >= 0)
            {
                _inGameMenuHoverIndex = mouseHoverIndex;
            }
            else if (!_context.IsControllerMenuInputActive())
            {
                _inGameMenuHoverIndex = -1;
            }

            if (_context.TryConsumeControllerMenuNavigation(out _, out var verticalStep) && verticalStep != 0)
            {
                _inGameMenuHoverIndex = MoveControllerMenuSelection(_inGameMenuHoverIndex, items.Count, verticalStep);
            }
            else if (_context.IsControllerMenuInputActive() && items.Count > 0 && _inGameMenuHoverIndex < 0)
            {
                _inGameMenuHoverIndex = 0;
            }

            var clickPressed = mouse.LeftButton == ButtonState.Pressed && _context._previousMouse.LeftButton != ButtonState.Pressed;
            var controllerConfirmPressed = _context.IsControllerMenuConfirmPressed();
            if ((!clickPressed && !controllerConfirmPressed) || _inGameMenuHoverIndex < 0)
            {
                return;
            }

            items[_inGameMenuHoverIndex].Activate();
            if (controllerConfirmPressed)
            {
                _context.ConsumeControllerMenuConfirmPress();
            }
        }

        public void DrawInGameMenu()
        {
            var viewportWidth = _context.ViewportWidth;
            var viewportHeight = _context.ViewportHeight;
            _context._spriteBatch.Draw(_context._pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * 0.66f);

            var items = GetInGameMenuActions();
            GetInGameMenuLayout(items.Count, out var plaqueBounds, out var itemBounds, out var plaqueTexture, out var plaqueScale);
            if (plaqueTexture is not null)
            {
                _context.DrawLoadedSpriteFrame(plaqueTexture, plaqueBounds, Color.White);
            }
            else
            {
                _context.DrawMenuPanelBackdrop(plaqueBounds, 0.84f);
            }

            for (var index = 0; index < items.Count; index += 1)
            {
                var buttonTexture = _context.GetMenuStackedButtonTexture(index, items.Count);
                _context.DrawPlaqueMenuButton(
                    buttonTexture,
                    itemBounds[index],
                    items[index].Label,
                    index == _inGameMenuHoverIndex,
                    plaqueScale,
                    1f);
            }
        }

        private void GetInGameMenuLayout(int itemCount, out Rectangle plaqueBounds, out Rectangle[] itemBounds, out LoadedSpriteFrame? plaqueTexture, out float plaqueScale)
        {
            itemCount = Math.Max(1, itemCount);
            var useTallPlaque = itemCount >= 5;
            plaqueTexture = useTallPlaque ? _context.MenuResources.PlaqueTallTexture : _context.MenuResources.PlaqueTexture;

            var availableHeight = _context.ViewportHeight - 84f;
            var maxScale = _context.ViewportHeight < 540 ? 0.52f : 0.58f;
            plaqueScale = plaqueTexture is null
                ? maxScale
                : MathF.Max(0.42f, MathF.Min(maxScale, availableHeight / Math.Max(1f, plaqueTexture.Height)));

            var sideInset = (int)MathF.Round(10f * plaqueScale);
            var topInset = (int)MathF.Round(20f * plaqueScale);
            var bottomInset = (int)MathF.Round(14f * plaqueScale);
            var itemGap = Math.Max(4, (int)MathF.Round(10f * plaqueScale));

            var plaqueWidth = plaqueTexture is null
                ? (int)MathF.Round((_context.ViewportHeight < 540 ? 190f : 210f) * plaqueScale)
                : (int)MathF.Round(plaqueTexture.Width * plaqueScale);
            var fallbackHeight = Math.Max(20, (int)MathF.Round(48f * plaqueScale));
            var contentHeight = topInset + bottomInset;
            for (var index = 0; index < itemCount; index += 1)
            {
                var buttonTexture = _context.GetMenuStackedButtonTexture(index, itemCount);
                var buttonHeight = buttonTexture is null ? fallbackHeight : (int)MathF.Round(buttonTexture.Height * plaqueScale);
                contentHeight += buttonHeight;
                if (index < itemCount - 1)
                {
                    contentHeight += itemGap;
                }
            }

            var minimumTextureHeight = plaqueTexture is null
                ? 0
                : (int)MathF.Round(plaqueTexture.Height * plaqueScale * 0.68f);
            var plaqueHeight = Math.Max(contentHeight, minimumTextureHeight);

            var plaqueX = (int)MathF.Round(MathF.Max(18f, _context.ViewportWidth * 0.035f));
            var plaqueY = Math.Max(24, ((_context.ViewportHeight - plaqueHeight) / 2) + 18);
            plaqueBounds = new Rectangle(plaqueX, plaqueY, Math.Max(1, plaqueWidth), Math.Max(1, plaqueHeight));

            itemBounds = new Rectangle[itemCount];
            var currentY = plaqueBounds.Y + topInset;
            var fallbackWidth = Math.Max(120, plaqueBounds.Width - sideInset * 2);

            for (var index = 0; index < itemCount; index += 1)
            {
                var buttonTexture = _context.GetMenuStackedButtonTexture(index, itemCount);
                var buttonWidth = buttonTexture is null ? fallbackWidth : (int)MathF.Round(buttonTexture.Width * plaqueScale);
                var buttonHeight = buttonTexture is null ? fallbackHeight : (int)MathF.Round(buttonTexture.Height * plaqueScale);
                var buttonX = plaqueBounds.X + sideInset;
                itemBounds[index] = new Rectangle(buttonX, currentY, Math.Max(1, buttonWidth), Math.Max(1, buttonHeight));
                currentY += buttonHeight + itemGap;
            }
        }

        public List<MenuPageAction> GetInGameMenuActions()
        {
            if (OpenGarrison.ClientShared.ClientDistribution.IsGg2Only)
            {
                var gg2Actions = new List<MenuPageAction>
                {
                    new("Resume", CloseInGameMenu),
                    new("Settings", () =>
                    {
                        _context.OpenOptionsMenu(fromGameplay: true);
                        CloseInGameMenu();
                    }),
                    new("Disconnect", () => _context.ReturnToMainMenu(_context.GetGameplayExitStatusMessage())),
                    new("Quit Game", _context.OpenQuitPrompt),
                };
                AddGameplaySelectionActions(gg2Actions);
                return gg2Actions;
            }
            if (_context._jukeboxMenuOpen) return _context.GetSessionJukeboxActions();
            if (_context.IsLastToDieSessionActive)
            {
                var lastToDieActions = new List<MenuPageAction>
                {
                    new("Resume", CloseInGameMenu),
                    new("Options", () =>
                    {
                        _context.OpenOptionsMenu(fromGameplay: true);
                        CloseInGameMenu();
                    }),
                    new("Social", OpenSocialMenu),
                    new("Leave Last To Die", () => _context.ReturnToLastToDieMenu("Last To Die ended.")),
                    new("Quit Game", _context.OpenQuitPrompt),
                };
                if (_context._peerRoomSession is not null || _context.IsEmbeddedSessionOwner)
                    lastToDieActions.Insert(1, new("Jukebox", _context.OpenSessionJukebox));
                if (_context.IsPeerRoomOwner)
                    lastToDieActions.Insert(1, new("Return to Lobby", () =>
                    {
                        CloseInGameMenu();
                        _context._peerRoomSession?.Connection.Send(new("lobby"));
                    }));
                if (_context._debugMenuEnabled)
                {
                    lastToDieActions.Insert(2, new MenuPageAction("Debug", () =>
                    {
                        _context.OpenDebugMenu();
                        CloseInGameMenu();
                    }));
                }
                _context.AddPluginMenuActions(lastToDieActions, ClientPluginMenuLocation.InGameMenu, insertIndex: 1);
                return FilterDistributionActions(lastToDieActions);
            }

            if (_context._garrisonBuilderQuickTestActive)
            {
                return
                [
                    new("Options", () =>
                    {
                        _context.OpenOptionsMenu(fromGameplay: true);
                        CloseInGameMenu();
                    }),
                    new("Social", OpenSocialMenu),
                    new("Return to editor", () =>
                    {
                        CloseInGameMenu();
                        _context.ReturnToGarrisonBuilderFromQuickTest();
                    }),
                ];
            }

            if (_context.IsPracticeSessionActive)
            {
                var practiceActions = new List<MenuPageAction>
                {
                    new("Resume", CloseInGameMenu),
                    new("Options", () =>
                    {
                        _context.OpenOptionsMenu(fromGameplay: true);
                        CloseInGameMenu();
                    }),
                    new("Social", OpenSocialMenu),
                    new("Practice Setup", _context.OpenPracticeSetupMenu),
                    new("Leave Practice", () => _context.ReturnToMainMenu(_context.GetGameplayExitStatusMessage())),
                    new("Quit Game", _context.OpenQuitPrompt),
                };

                AddGameplaySelectionActions(practiceActions);
                practiceActions.Insert(1, new("Jukebox", _context.OpenSessionJukebox));
                practiceActions.Insert(2, new("Call Vote", () => { CloseInGameMenu(); _context.OpenPracticeVoteMenu(); }));

                if (_context._debugMenuEnabled)
                {
                    var insertIndex = Math.Min(practiceActions.Count, 5);
                    practiceActions.Insert(insertIndex, new MenuPageAction("Debug", () =>
                    {
                        _context.OpenDebugMenu();
                        CloseInGameMenu();
                    }));
                }

                _context.AddPluginMenuActions(practiceActions, ClientPluginMenuLocation.InGameMenu, insertIndex: 1);
                return FilterDistributionActions(practiceActions);
            }

            var defaultActions = new List<MenuPageAction>
            {
                new("Resume", CloseInGameMenu),
                new("Options", () =>
                {
                    _context.OpenOptionsMenu(fromGameplay: true);
                    CloseInGameMenu();
                }),
                new("Social", OpenSocialMenu),
                new("Disconnect", () => _context.ReturnToMainMenu(_context.GetGameplayExitStatusMessage())),
                new("Quit Game", _context.OpenQuitPrompt),
            };

            if (_context._gameplaySessionKind == GameplaySessionKind.Online
                && _context._networkClient.IsConnected
                && !_context.IsHostedLastToDieActive()
                && !_context._networkClient.IsReplayConnection)
            {
                defaultActions.Insert(3, new MenuPageAction("Call Vote", () =>
                {
                    CloseInGameMenu();
                    _context._networkClient.SendVoteCommand(VoteCommandKind.OpenMenu);
                }));
            }

            if (_context._networkClient.IsConnected && !_context._networkClient.IsReplayConnection)
            {
                defaultActions.Insert(2, new MenuPageAction(_context.GetVoiceMuteActionLabel(), _context.ToggleVoiceMute));
                if (_context.IsCoopLastToDieActive())
                    defaultActions.Insert(3, new MenuPageAction(_context.GetVoiceChannelActionLabel(), _context.ToggleVoiceChannelMembership));
            }

            AddGameplaySelectionActions(defaultActions);
            if (_context._peerRoomSession is not null || _context.IsEmbeddedSessionOwner)
                defaultActions.Insert(1, new("Jukebox", _context.OpenSessionJukebox));
            if (_context.IsPeerRoomOwner)
                defaultActions.Insert(1, new("Return to Lobby", () =>
                {
                    CloseInGameMenu();
                    _context._peerRoomSession?.Connection.Send(new("lobby"));
                }));

            if (_context._debugMenuEnabled)
            {
                var insertIndex = Math.Min(defaultActions.Count, 3);
                defaultActions.Insert(insertIndex, new MenuPageAction("Debug", () =>
                {
                    _context.OpenDebugMenu();
                    CloseInGameMenu();
                }));
            }

            _context.AddPluginMenuActions(defaultActions, ClientPluginMenuLocation.InGameMenu, insertIndex: 1);
            return FilterDistributionActions(defaultActions);
        }

        private static List<MenuPageAction> FilterDistributionActions(List<MenuPageAction> actions)
        {
            if (IsRestrictedBrowserEdition)
                actions.RemoveAll(action => action.Label is "Social" or "Quit Game" or "Debug");
            return actions;
        }

        private void AddGameplaySelectionActions(List<MenuPageAction> actions)
        {
            if (!_context.CanOfferGameplaySelectionMenusFromInGameMenu())
            {
                return;
            }

            var insertIndex = Math.Min(actions.Count, actions.Count > 1 ? 2 : 1);
            actions.Insert(insertIndex, new MenuPageAction("Select Team", () =>
            {
                CloseInGameMenu();
                _context.OpenGameplayTeamSelection();
            }));

            if (_context._world.LocalPlayerAwaitingJoin)
            {
                return;
            }

            actions.Insert(insertIndex + 1, new MenuPageAction("Select Class", () =>
            {
                CloseInGameMenu();
                _context.OpenGameplayClassSelection();
            }));
        }

        private void OpenSocialMenu()
        {
            _context.OpenFriendsMenu();
        }
}
