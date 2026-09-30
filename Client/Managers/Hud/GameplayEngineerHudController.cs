#nullable enable

using System.Globalization;
using Microsoft.Xna.Framework;
using OpenGarrison.Core;

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class GameplayEngineerHudController
    {
        private const float SentryHudSourceX = 696f;
        private const float SentryHudScale = 2f;
        private const float SentryHudSpriteWidth = 43f;
        private const float SentryHudSpriteHeight = 32f;
        private const int EditorPreviewSentryHealth = 100;
        private const int EditorPreviewDispenserHealth = SentryEntity.DispenserMaxHealth;

        private readonly IHudContext _context;

        public GameplayEngineerHudController(IHudContext context)
        {
            _context = context;
        }

        public void DrawEngineerHud()
        {
            DrawEngineerMetalHud();
            DrawEngineerSentryHud();
            DrawEngineerDispenserHud();
        }

        public void DrawEngineerMetalHud()
        {
            if (_context._world.LocalPlayer.ClassId != PlayerClass.Engineer)
            {
                return;
            }

            var viewportWidth = _context.ViewportWidth;
            var viewportHeight = _context.ViewportHeight;
            var hudFrameIndex = _context._world.LocalPlayer.Team == PlayerTeam.Blue ? 1 : 0;
            var defaultMetalPosition = new Vector2(viewportWidth - 70f, viewportHeight - 85f);
            if (_context.TryResolveHudElement(HudElementId.ClassEngineerMetal, out var resolved))
            {
                var metalPosition = resolved.Origin;
                var hudScale = resolved.Layout.Scale;
                Vector2 TransformPoint(Vector2 point) => metalPosition + ((point - defaultMetalPosition) * hudScale);

                _context.TryDrawScreenSprite("NutsNBoltsHudS", hudFrameIndex, metalPosition, Color.White, new Vector2(2f * hudScale, 2f * hudScale));
                var metalText = ((int)MathF.Floor(_context.GetPlayerMetal(_context._world.LocalPlayer))).ToString(CultureInfo.InvariantCulture);
                var metalTextPosition = TransformPoint(new Vector2(viewportWidth - 66f, viewportHeight - 91f));
                _context.DrawHudTextRightAligned(metalText, metalTextPosition, Color.White, 1.5f * hudScale);
                var spriteBounds = new Rectangle(
                    (int)MathF.Round(metalPosition.X - (19f * 2f * hudScale)),
                    (int)MathF.Round(metalPosition.Y - (9f * 2f * hudScale)),
                    Math.Max(1, (int)MathF.Round(39f * 2f * hudScale)),
                    Math.Max(1, (int)MathF.Round(19f * 2f * hudScale)));
                var textBounds = new Rectangle(
                    (int)MathF.Floor(metalTextPosition.X - _context.MeasureBitmapFontWidth(metalText, 1.5f * hudScale)),
                    (int)MathF.Floor(metalTextPosition.Y),
                    Math.Max(1, (int)MathF.Ceiling(_context.MeasureBitmapFontWidth(metalText, 1.5f * hudScale))),
                    Math.Max(1, (int)MathF.Ceiling(_context.MeasureBitmapFontHeight(1.5f * hudScale))));
                _context.UpdateHudElementBounds(HudElementId.ClassEngineerMetal, Rectangle.Union(spriteBounds, textBounds));
            }
        }

        public void DrawEngineerSentryHud()
        {
            if (_context._world.LocalPlayer.ClassId != PlayerClass.Engineer)
            {
                return;
            }

            var localSentry = GetLocalOwnedSentry();
            if (localSentry is null && !_context._hudEditorOpen)
            {
                return;
            }

            var hudFrameIndex = _context._world.LocalPlayer.Team == PlayerTeam.Blue ? 1 : 0;
            if (!_context.TryResolveHudElement(HudElementId.ClassEngineerSentry, out var resolved))
            {
                return;
            }

            var sentryPosition = resolved.Origin;
            var hudScale = resolved.Layout.Scale;
            Vector2 TransformOffset(float x, float y) => sentryPosition + (new Vector2(x, y) * hudScale);
            var sentryHealthBarPosition = TransformOffset(40f, 22f);
            _context.DrawScreenHealthBar(
                new Rectangle(
                    (int)MathF.Round(sentryHealthBarPosition.X),
                    (int)MathF.Round(sentryHealthBarPosition.Y),
                    Math.Max(1, (int)MathF.Round(42f * hudScale)),
                    Math.Max(1, (int)MathF.Round(38f * hudScale))),
                localSentry?.Health ?? EditorPreviewSentryHealth,
                localSentry?.MaxHealth ?? EditorPreviewSentryHealth,
                false,
                fillDirection: HudFillDirection.VerticalBottomToTop);
            _context.TryDrawScreenSprite("SentryHUD", hudFrameIndex, sentryPosition, Color.White, new Vector2(SentryHudScale * hudScale, SentryHudScale * hudScale));
            var sentryHealth = localSentry?.Health ?? EditorPreviewSentryHealth;
            var sentryMaxHealth = localSentry?.MaxHealth ?? EditorPreviewSentryHealth;
            var sentryHpColor = sentryHealth > (sentryMaxHealth / 3.5f) ? Color.White : Color.Red;
            _context.DrawHudTextCentered(Math.Max(sentryHealth, 0).ToString(CultureInfo.InvariantCulture), TransformOffset(64f, 40f), sentryHpColor, 1f * hudScale);
            var ownedCount = GetLocalOwnedSentryCount();
            if (ownedCount > 1)
            {
                _context.DrawHudTextCentered($"x{ownedCount}", TransformOffset(64f, 57f), new Color(214, 214, 214), 0.8f * hudScale);
            }

            _context.UpdateHudElementBounds(HudElementId.ClassEngineerSentry, resolved.Layout.ResolveBounds(resolved.Origin));
        }

        public void DrawEngineerDispenserHud()
        {
            if (_context._world.LocalPlayer.ClassId != PlayerClass.Engineer)
            {
                return;
            }

            var localDispenser = GetLocalOwnedDispenser();
            if (localDispenser is null && !_context._hudEditorOpen)
            {
                return;
            }

            if (!_context.TryResolveHudElement(HudElementId.ClassEngineerDispenser, out var resolved))
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
            var healthHudSpriteName = _context._world.LocalPlayer.Team == PlayerTeam.Blue
                ? "PlayerHealthBlu"
                : "PlayerHealthRed";
            _context.TryDrawScreenSprite(healthHudSpriteName, 0, dispenserPosition, Color.White, healthHudScale);

            var crossSprite = _context.GetResolvedSprite("PlayerHealthCross");
            var crossPosition = dispenserPosition + new Vector2(40f * hudScale, -2f * hudScale);
            _context.TryDrawScreenSprite("PlayerHealthCross", 0, crossPosition, Color.White, healthHudScale);

            var medicineSprite = _context.GetResolvedSprite("PlayerHealthCrossInternalMedicine");
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
                    _context.TryDrawScreenSpritePart(
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
            _context.DrawHudTextCentered(
                Math.Max(health, 0).ToString(CultureInfo.InvariantCulture),
                healthTextPosition,
                dispenserHpColor,
                1f * hudScale);
            _context.UpdateHudElementBounds(HudElementId.ClassEngineerDispenser, resolved.Layout.ResolveBounds(resolved.Origin));
        }

        public void SetEngineerSentryRuntimeDefault()
        {
            _context.SetHudElementRuntimeDefault(_context.Hud.LocalStatus.CreateAbilityStackedHudElementLayout(
                HudElementId.ClassEngineerSentry,
                SentryHudSourceX,
                Vector2.Zero,
                GetSentryHudBoundsSize(),
                HudElementLayerClassEngineerSentry));
        }

        public void SetEngineerDispenserRuntimeDefault(bool stackAboveSentry)
        {
            var layout = _context.Hud.LocalStatus.CreateAbilityStackedHudElementLayout(
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

            _context.SetHudElementRuntimeDefault(layout);
        }

        public SentryEntity? GetLocalOwnedSentry()
        {
            foreach (var sentry in _context._world.Sentries)
            {
                if (sentry.OwnerPlayerId == _context.GetPlayerStateKey(_context._world.LocalPlayer)
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
            foreach (var sentry in _context._world.Sentries)
            {
                if (sentry.OwnerPlayerId == _context.GetPlayerStateKey(_context._world.LocalPlayer)
                    && !sentry.IsDispenser)
                {
                    ownedCount += 1;
                }
            }

            return ownedCount;
        }

        public SentryEntity? GetLocalOwnedDispenser()
        {
            foreach (var sentry in _context._world.Sentries)
            {
                if (sentry.OwnerPlayerId == _context.GetPlayerStateKey(_context._world.LocalPlayer)
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
