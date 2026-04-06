#nullable enable

using System.Globalization;
using Microsoft.Xna.Framework;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class GameplayEngineerHudController
    {
        private readonly Game1 _game;

        public GameplayEngineerHudController(Game1 game)
        {
            _game = game;
        }

        public void DrawEngineerHud()
        {
            if (_game._world.LocalPlayer.ClassId != PlayerClass.Engineer)
            {
                return;
            }

            var viewportWidth = _game.ViewportWidth;
            var viewportHeight = _game.ViewportHeight;
            var hudFrameIndex = _game._world.LocalPlayer.Team == PlayerTeam.Blue ? 1 : 0;
            _game.TryDrawScreenSprite("NutsNBoltsHudS", hudFrameIndex, new Vector2(viewportWidth - 70f, viewportHeight - 85f), Color.White, new Vector2(2f, 2f));
            _game.DrawHudTextRightAligned(((int)MathF.Floor(_game.GetPlayerMetal(_game._world.LocalPlayer))).ToString(CultureInfo.InvariantCulture), new Vector2(viewportWidth - 66f, viewportHeight - 91f), Color.White, 1.5f);

            var localSentry = GetLocalOwnedSentry();
            if (localSentry is null)
            {
                return;
            }

            _game.DrawScreenHealthBar(new Rectangle(45, viewportHeight - 123, 42, 38), localSentry.Health, SentryEntity.MaxHealth, false, fillDirection: HudFillDirection.VerticalBottomToTop);
            _game.TryDrawScreenSprite("SentryHUD", hudFrameIndex, new Vector2(5f, viewportHeight - 145f), Color.White, new Vector2(2f, 2f));
            var sentryHpColor = localSentry.Health > (SentryEntity.MaxHealth / 3.5f) ? Color.White : Color.Red;
            _game.DrawHudTextCentered(Math.Max(localSentry.Health, 0).ToString(CultureInfo.InvariantCulture), new Vector2(69f, viewportHeight - 105f), sentryHpColor, 1f);
        }

        public SentryEntity? GetLocalOwnedSentry()
        {
            foreach (var sentry in _game._world.Sentries)
            {
                if (sentry.OwnerPlayerId == _game.GetPlayerStateKey(_game._world.LocalPlayer))
                {
                    return sentry;
                }
            }

            return null;
        }
    }
}
