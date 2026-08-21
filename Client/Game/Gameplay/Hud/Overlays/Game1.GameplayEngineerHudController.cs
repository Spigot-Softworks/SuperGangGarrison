#nullable enable

using System.Globalization;
using Microsoft.Xna.Framework;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class GameplayEngineerHudController
    {
        private const float SentryHudSourceX = 696f;
        private const float SentryHudScale = 2f;
        private const float SentryHudSpriteWidth = 43f;
        private const float SentryHudSpriteHeight = 32f;
        private const int EditorPreviewSentryHealth = 100;
        private const int EditorPreviewDispenserHealth = SentryEntity.DispenserMaxHealth;

        private readonly Game1 _game;

        public GameplayEngineerHudController(Game1 game)
        {
            _game = game;
        }

        public void DrawEngineerHud()
        {
            DrawEngineerMetalHud();
            DrawEngineerSentryHud();
            DrawEngineerDispenserHud();
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
                var metalText = ((int)MathF.Floor(_game.GetPlayerMetal(_game._world.LocalPlayer))).ToString(CultureInfo.InvariantCulture);
                var metalTextPosition = TransformPoint(new Vector2(viewportWidth - 66f, viewportHeight - 91f));
                _game.DrawHudTextRightAligned(metalText, metalTextPosition, Color.White, 1.5f * hudScale);
                var spriteBounds = new Rectangle(
                    (int)MathF.Round(metalPosition.X - (19f * 2f * hudScale)),
                    (int)MathF.Round(metalPosition.Y - (9f * 2f * hudScale)),
                    Math.Max(1, (int)MathF.Round(39f * 2f * hudScale)),
                    Math.Max(1, (int)MathF.Round(19f * 2f * hudScale)));
                var textBounds = new Rectangle(
                    (int)MathF.Floor(metalTextPosition.X - _game.MeasureBitmapFontWidth(metalText, 1.5f * hudScale)),
                    (int)MathF.Floor(metalTextPosition.Y),
                    Math.Max(1, (int)MathF.Ceiling(_game.MeasureBitmapFontWidth(metalText, 1.5f * hudScale))),
                    Math.Max(1, (int)MathF.Ceiling(_game.MeasureBitmapFontHeight(1.5f * hudScale))));
                _game.UpdateHudElementBounds(HudElementId.ClassEngineerMetal, Rectangle.Union(spriteBounds, textBounds));
            }
        }

        public void DrawEngineerSentryHud()
        {
            if (_game._world.LocalPlayer.ClassId != PlayerClass.Engineer)
            {
                return;
            }

            var localSentry = GetLocalOwnedSentry();
            if (localSentry is null && !_game._hudEditorOpen)
            {
                return;
            }

            var hudFrameIndex = _game._world.LocalPlayer.Team == PlayerTeam.Blue ? 1 : 0;
            if (!_game.TryResolveHudElement(HudElementId.ClassEngineerSentry, out var resolved))
            {
                return;
            }

            var sentryPosition = resolved.Origin;
            var hudScale = resolved.Layout.Scale;
            Vector2 TransformOffset(float x, float y) => sentryPosition + (new Vector2(x, y) * hudScale);
            var sentryHealthBarPosition = TransformOffset(40f, 22f);
            _game.DrawScreenHealthBar(
                new Rectangle(
                    (int)MathF.Round(sentryHealthBarPosition.X),
                    (int)MathF.Round(sentryHealthBarPosition.Y),
                    Math.Max(1, (int)MathF.Round(42f * hudScale)),
                    Math.Max(1, (int)MathF.Round(38f * hudScale))),
                localSentry?.Health ?? EditorPreviewSentryHealth,
                localSentry?.MaxHealth ?? EditorPreviewSentryHealth,
                false,
                fillDirection: HudFillDirection.VerticalBottomToTop);
            _game.TryDrawScreenSprite("SentryHUD", hudFrameIndex, sentryPosition, Color.White, new Vector2(SentryHudScale * hudScale, SentryHudScale * hudScale));
            var sentryHealth = localSentry?.Health ?? EditorPreviewSentryHealth;
            var sentryMaxHealth = localSentry?.MaxHealth ?? EditorPreviewSentryHealth;
            var sentryHpColor = sentryHealth > (sentryMaxHealth / 3.5f) ? Color.White : Color.Red;
            _game.DrawHudTextCentered(Math.Max(sentryHealth, 0).ToString(CultureInfo.InvariantCulture), TransformOffset(64f, 40f), sentryHpColor, 1f * hudScale);
            var ownedCount = GetLocalOwnedSentryCount();
            if (ownedCount > 1)
            {
                _game.DrawHudTextCentered($"x{ownedCount}", TransformOffset(64f, 57f), new Color(214, 214, 214), 0.8f * hudScale);
            }

            _game.UpdateHudElementBounds(HudElementId.ClassEngineerSentry, resolved.Layout.ResolveBounds(resolved.Origin));
        }

        public void DrawEngineerDispenserHud()
        {
            if (_game._world.LocalPlayer.ClassId != PlayerClass.Engineer)
            {
                return;
            }

            var localDispenser = GetLocalOwnedDispenser();
            if (localDispenser is null && !_game._hudEditorOpen)
            {
                return;
            }

            if (!_game.TryResolveHudElement(HudElementId.ClassEngineerDispenser, out var resolved))
            {
                return;
            }

            var dispenserPosition = resolved.Origin;
            var hudScale = resolved.Layout.Scale;
            var health = localDispenser?.Health ?? EditorPreviewDispenserHealth;
            var maxHealth = localDispenser?.MaxHealth ?? EditorPreviewDispenserHealth;

            // Use the stock player-health plaque as the generic structure health
            // element. The dispenser deliberately has no character/sentry picture;
            // only the blank team plaque, medical cross, fill, and HP text are shown.
            var healthHudScale = new Vector2(2f * hudScale, 2f * hudScale);
            var healthHudSpriteName = _game._world.LocalPlayer.Team == PlayerTeam.Blue
                ? "PlayerHealthBlu"
                : "PlayerHealthRed";
            _game.TryDrawScreenSprite(healthHudSpriteName, 0, dispenserPosition, Color.White, healthHudScale);

            var crossSprite = _game.GetResolvedSprite("PlayerHealthCross");
            var crossPosition = dispenserPosition + new Vector2(40f * hudScale, -2f * hudScale);
            _game.TryDrawScreenSprite("PlayerHealthCross", 0, crossPosition, Color.White, healthHudScale);

            var medicineSprite = _game.GetResolvedSprite("PlayerHealthCrossInternalMedicine");
            if (medicineSprite is not null && medicineSprite.Frames.Count > 0)
            {
                var medicineFrame = medicineSprite.Frames[0];
                var sourceRectangle = Game1.GetLocalHealthMedicineSourceRectangle(
                    medicineFrame.Width,
                    medicineFrame.Height,
                    medicineFrame.OpaqueBounds,
                    health,
                    maxHealth);
                if (sourceRectangle is { } fillSource)
                {
                    var fillPosition = crossPosition + new Vector2(0f, fillSource.Y * healthHudScale.Y);
                    _game.TryDrawScreenSpritePart(
                        "PlayerHealthCrossInternalMedicine",
                        0,
                        fillSource,
                        fillPosition,
                        Color.Green,
                        healthHudScale);
                }
            }

            var dispenserHpColor = health > (maxHealth / 3.5f) ? Color.White : Color.Red;
            var healthTextPosition = crossPosition + new Vector2(
                (crossSprite?.Frames[0].Width ?? 0) * healthHudScale.X / 2f,
                (crossSprite?.Frames[0].Height ?? 0) * healthHudScale.Y / 2f);
            _game.DrawHudTextCentered(
                Math.Max(health, 0).ToString(CultureInfo.InvariantCulture),
                healthTextPosition,
                dispenserHpColor,
                1f * hudScale);
            _game.UpdateHudElementBounds(HudElementId.ClassEngineerDispenser, resolved.Layout.ResolveBounds(resolved.Origin));
        }

        public void SetEngineerSentryRuntimeDefault()
        {
            _game.SetHudElementRuntimeDefault(_game._gameplayLocalStatusHudController.CreateAbilityStackedHudElementLayout(
                HudElementId.ClassEngineerSentry,
                SentryHudSourceX,
                Vector2.Zero,
                GetSentryHudBoundsSize(),
                HudElementLayerClassEngineerSentry));
        }

        public void SetEngineerDispenserRuntimeDefault(bool stackAboveSentry)
        {
            var layout = _game._gameplayLocalStatusHudController.CreateAbilityStackedHudElementLayout(
                HudElementId.ClassEngineerDispenser,
                SentryHudSourceX,
                Vector2.Zero,
                GetSentryHudBoundsSize(),
                HudElementLayerClassEngineerDispenser);
            if (stackAboveSentry)
            {
                layout = layout with
                {
                    Offset = layout.Offset - new Vector2(0f, SentryHudSpriteHeight * SentryHudScale + 10f),
                };
            }

            _game.SetHudElementRuntimeDefault(layout);
        }

        public SentryEntity? GetLocalOwnedSentry()
        {
            foreach (var sentry in _game._world.Sentries)
            {
                if (sentry.OwnerPlayerId == _game.GetPlayerStateKey(_game._world.LocalPlayer)
                    && !sentry.IsDispenser)
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
                if (sentry.OwnerPlayerId == _game.GetPlayerStateKey(_game._world.LocalPlayer)
                    && !sentry.IsDispenser)
                {
                    ownedCount += 1;
                }
            }

            return ownedCount;
        }

        public SentryEntity? GetLocalOwnedDispenser()
        {
            foreach (var sentry in _game._world.Sentries)
            {
                if (sentry.OwnerPlayerId == _game.GetPlayerStateKey(_game._world.LocalPlayer)
                    && sentry.IsDispenser)
                {
                    return sentry;
                }
            }

            return null;
        }

        private static Vector2 GetSentryHudBoundsSize()
        {
            return new Vector2(SentryHudSpriteWidth * SentryHudScale, SentryHudSpriteHeight * SentryHudScale);
        }
    }
}
