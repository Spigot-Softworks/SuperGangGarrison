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
            DrawEngineerMetalHud();
            DrawEngineerSentryHud();
        }

        public void DrawEngineerMetalHud()
        {
            if (_game._world.LocalPlayer.ClassId != PlayerClass.Engineer)
            {
                return;
            }

            var viewportWidth = _game.ViewportWidth;
            var viewportHeight = _game.ViewportHeight;
            var hudFrameIndex = _game._world.LocalPlayer.Team == PlayerTeam.Blue ? 1 : 0;
            var defaultMetalPosition = new Vector2(viewportWidth - 70f, viewportHeight - 85f);
            if (_game.TryResolveHudElement(HudElementId.ClassEngineerMetal, out var resolved))
            {
                var metalPosition = resolved.Origin;
                var hudScale = resolved.Layout.Scale;
                Vector2 TransformPoint(Vector2 point) => metalPosition + ((point - defaultMetalPosition) * hudScale);

                _game.TryDrawScreenSprite("NutsNBoltsHudS", hudFrameIndex, metalPosition, Color.White, new Vector2(2f * hudScale, 2f * hudScale));
                _game.DrawHudTextRightAligned(((int)MathF.Floor(_game.GetPlayerMetal(_game._world.LocalPlayer))).ToString(CultureInfo.InvariantCulture), TransformPoint(new Vector2(viewportWidth - 66f, viewportHeight - 91f)), Color.White, 1.5f * hudScale);
            }
        }

        public void DrawEngineerSentryHud()
        {
            if (_game._world.LocalPlayer.ClassId != PlayerClass.Engineer)
            {
                return;
            }

            var localSentry = GetLocalOwnedSentry();
            if (localSentry is null)
            {
                return;
            }

            var viewportHeight = _game.ViewportHeight;
            var hudFrameIndex = _game._world.LocalPlayer.Team == PlayerTeam.Blue ? 1 : 0;
            var defaultSentryPosition = new Vector2(5f, viewportHeight - 145f);
            if (!_game.TryResolveHudElement(HudElementId.ClassEngineerSentry, out var resolved))
            {
                return;
            }

            var sentryPosition = resolved.Origin;
            var hudScale = resolved.Layout.Scale;
            Vector2 TransformPoint(Vector2 point) => sentryPosition + ((point - defaultSentryPosition) * hudScale);
            var sentryHealthBarPosition = TransformPoint(new Vector2(45f, viewportHeight - 123f));
            _game.DrawScreenHealthBar(
                new Rectangle(
                    (int)MathF.Round(sentryHealthBarPosition.X),
                    (int)MathF.Round(sentryHealthBarPosition.Y),
                    Math.Max(1, (int)MathF.Round(42f * hudScale)),
                    Math.Max(1, (int)MathF.Round(38f * hudScale))),
                localSentry.Health,
                localSentry.MaxHealth,
                false,
                fillDirection: HudFillDirection.VerticalBottomToTop);
            _game.TryDrawScreenSprite("SentryHUD", hudFrameIndex, sentryPosition, Color.White, new Vector2(2f * hudScale, 2f * hudScale));
            var sentryHpColor = localSentry.Health > (localSentry.MaxHealth / 3.5f) ? Color.White : Color.Red;
            _game.DrawHudTextCentered(Math.Max(localSentry.Health, 0).ToString(CultureInfo.InvariantCulture), TransformPoint(new Vector2(69f, viewportHeight - 105f)), sentryHpColor, 1f * hudScale);
            var ownedCount = GetLocalOwnedSentryCount();
            if (ownedCount > 1)
            {
                _game.DrawHudTextCentered($"x{ownedCount}", TransformPoint(new Vector2(69f, viewportHeight - 88f)), new Color(214, 214, 214), 0.8f * hudScale);
            }
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

        public int GetLocalOwnedSentryCount()
        {
            var ownedCount = 0;
            foreach (var sentry in _game._world.Sentries)
            {
                if (sentry.OwnerPlayerId == _game.GetPlayerStateKey(_game._world.LocalPlayer))
                {
                    ownedCount += 1;
                }
            }

            return ownedCount;
        }
    }
}
