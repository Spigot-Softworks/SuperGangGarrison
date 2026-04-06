#nullable enable

using System.Collections.Generic;
using Microsoft.Xna.Framework;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class GameplayPlayerRenderController
    {
        private readonly Game1 _game;

        public GameplayPlayerRenderController(Game1 game)
        {
            _game = game;
        }

        public void DrawPlayer(PlayerEntity player, Vector2 cameraPosition, Color aliveColor, Color deadColor)
        {
            if (!player.IsAlive)
            {
                return;
            }

            var visibilityAlpha = _game.GetPlayerVisibilityAlpha(player);
            if (visibilityAlpha <= 0f)
            {
                return;
            }

            var renderPosition = _game.GetRenderPosition(player, allowInterpolation: !ReferenceEquals(player, _game._world.LocalPlayer));
            var rectangle = GetPlayerScreenBounds(player, renderPosition, cameraPosition);
            var fallbackColor = aliveColor * visibilityAlpha;
            var spriteTint = _game.GetPlayerColor(player, Color.White);
            var bodySelection = _game.GetPlayerBodySpriteSelection(player);
            _game.DrawExperimentalDemoknightChargeBlur(player, cameraPosition, spriteTint, visibilityAlpha, bodySelection);
            _game.DrawCapturedPointHealingGhosting(player, renderPosition, cameraPosition, visibilityAlpha, bodySelection);
            if (!_game.TryDrawPlayerSprite(player, cameraPosition, spriteTint, bodySelection))
            {
                _game._spriteBatch.Draw(_game._pixel, rectangle, fallbackColor);
            }

            _game.DrawExperimentalStickyGibBloodOverlay(player, cameraPosition, visibilityAlpha);
            if (!_game.GetPlayerIsHeavyEating(player) && !player.IsTaunting && !_game._world.IsPlayerHumiliated(player))
            {
                _game.TryDrawWeaponSprite(player, cameraPosition, spriteTint, visibilityAlpha, bodySelection);
            }

            _game.DrawAfterburnOverlay(player, renderPosition, cameraPosition, visibilityAlpha);
            _game.DrawDominationIndicator(player, cameraPosition, visibilityAlpha);
            _game.DrawChatBubble(player, cameraPosition);
            _game.TryDrawAdditionalHealthBar(player, cameraPosition, visibilityAlpha);
        }

        public IEnumerable<PlayerEntity> EnumerateRenderablePlayers()
        {
            if (!_game._networkClient.IsSpectator)
            {
                yield return _game._world.LocalPlayer;
            }

            foreach (var player in _game.EnumerateRemotePlayersForView())
            {
                yield return player;
            }
        }

        public static string GetHudPlayerLabel(PlayerEntity player)
        {
            return player.DisplayName;
        }
    }
}
