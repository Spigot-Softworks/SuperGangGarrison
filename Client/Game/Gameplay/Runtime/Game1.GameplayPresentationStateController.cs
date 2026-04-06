#nullable enable

using Microsoft.Xna.Framework;
using System;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class GameplayPresentationStateController
    {
        private readonly Game1 _game;

        public GameplayPresentationStateController(Game1 game)
        {
            _game = game;
        }

        public void HandleGameplayMapTransitionIfNeeded()
        {
            var currentLevelName = _game._world.Level.Name;
            var currentMapAreaIndex = _game._world.Level.MapAreaIndex;
            if (_game._observedGameplayMapAreaIndex < 0 || string.IsNullOrWhiteSpace(_game._observedGameplayLevelName))
            {
                _game._observedGameplayLevelName = currentLevelName;
                _game._observedGameplayMapAreaIndex = currentMapAreaIndex;
                return;
            }

            if (string.Equals(_game._observedGameplayLevelName, currentLevelName, StringComparison.OrdinalIgnoreCase)
                && _game._observedGameplayMapAreaIndex == currentMapAreaIndex)
            {
                return;
            }

            _game.ResetGameplayTransitionEffects();
            _game._wasDeathCamActive = false;
            _game._wasMatchEnded = false;
            if (_game._navEditorEnabled)
            {
                _game.DisableNavEditor("nav editor closed after map change");
            }

            _game._observedGameplayLevelName = currentLevelName;
            _game._observedGameplayMapAreaIndex = currentMapAreaIndex;
        }

        public void UpdateGameplayWindowState()
        {
            var wantsMouseVisible = _game.ShouldShowGameplayMouseCursor();
            _game.IsMouseVisible = wantsMouseVisible && !_game.ShouldUseSoftwareMenuCursor();

            var sourceTag = _game._world.Level.ImportedFromSource ? "src" : "fallback";
            var lifeTag = _game._world.LocalPlayerAwaitingJoin ? "joining" : _game._world.LocalPlayer.IsAlive ? "alive" : $"respawn:{_game._world.LocalPlayerRespawnTicks}";
            var remoteLifeTag = _game._networkClient.IsConnected
                ? $"remotes:{_game._world.RemoteSnapshotPlayers.Count}"
                : _game._world.EnemyPlayerEnabled
                    ? "offline:npc:on"
                    : "offline:npc:off";
            var carryingIntelTag = _game._world.LocalPlayer.IsCarryingIntel ? "yes" : "no";
            var heavyTag = _game.GetPlayerIsHeavyEating(_game._world.LocalPlayer)
                ? $" eat:{_game.GetPlayerHeavyEatTicksRemaining(_game._world.LocalPlayer)}"
                : string.Empty;
            var sniperTag = _game.GetPlayerIsSniperScoped(_game._world.LocalPlayer)
                ? $" scope:{_game.GetPlayerSniperRifleDamage(_game._world.LocalPlayer)}"
                : string.Empty;
            var consoleTag = _game._consoleOpen ? " console:open" : string.Empty;
            var netTag = _game._networkClient.IsConnected ? $" net:{_game._networkClient.ServerDescription}" : string.Empty;
            _game.Window.Title = $"OpenGarrison.Client - {_game._world.LocalPlayer.DisplayName} ({_game._world.LocalPlayer.ClassName}) - {_game._world.Level.Name} [{sourceTag}] - {lifeTag} - HP {_game._world.LocalPlayer.Health}/{_game._world.LocalPlayer.MaxHealth} - Ammo {_game._world.LocalPlayer.CurrentShells}/{_game._world.LocalPlayer.MaxShells} - {remoteLifeTag} - Caps {_game._world.RedCaps}:{_game._world.BlueCaps} - Carrying {carryingIntelTag} - BlueIntel {(Game1.GetIntelStateLabel(_game._world.BlueIntel))} - Frame {_game._world.Frame} - Pos ({_game._world.LocalPlayer.X:F1}, {_game._world.LocalPlayer.Y:F1}) - AirJumps {_game._world.LocalPlayer.RemainingAirJumps}{heavyTag}{sniperTag}{consoleTag}{netTag}";
        }
    }
}
