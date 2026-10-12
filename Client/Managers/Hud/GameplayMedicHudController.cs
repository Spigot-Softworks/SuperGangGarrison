#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class GameplayMedicHudController
    {
        private const float HealingTargetHudYRatio = 450f / 600f;
        private const float HealerStackedHudYRatio = 490f / 600f;
        private const float MedicAssistHudFallbackWidth = 220f;
        private const float MedicAssistHudHeight = 40f;
        private const int MedicAssistDummyHealth = 125;
        private const int MedicAssistDummyUberCharge = 1200;

        private readonly IHudContext _context;

        public GameplayMedicHudController(IHudContext context)
        {
            _context = context;
        }

        public void DrawMedicHud()
        {
            if (_context._world.LocalPlayer.ClassId != PlayerClass.Medic)
            {
                return;
            }

            var viewportWidth = _context.ViewportWidth;
            var viewportHeight = _context.ViewportHeight;
            var defaultIconPosition = new Vector2(viewportWidth - 80f, viewportHeight - 85f);
            if (!_context.TryResolveHudElement(HudElementId.ClassMedicUber, out var resolved))
            {
                return;
            }

            var iconPosition = resolved.Origin;
            var hudScale = resolved.Layout.Scale;
            Vector2 TransformPoint(Vector2 point) => iconPosition + ((point - defaultIconPosition) * hudScale);

            var hudFrameIndex = _context._world.LocalPlayer.Team == PlayerTeam.Blue ? 1 : 0;
            var uberPosition = TransformPoint(new Vector2(viewportWidth - 135f, viewportHeight - 100f));
            var uberRectangle = new Rectangle(
                (int)MathF.Round(uberPosition.X),
                (int)MathF.Round(uberPosition.Y),
                Math.Max(1, (int)MathF.Round(120f * hudScale)),
                Math.Max(1, (int)MathF.Round(32f * hudScale)));
            var iconBounds = new Rectangle(
                (int)MathF.Round(iconPosition.X - (34f * 2f * hudScale)),
                (int)MathF.Round(iconPosition.Y - (11f * 2f * hudScale)),
                Math.Max(1, (int)MathF.Round(68f * 2f * hudScale)),
                Math.Max(1, (int)MathF.Round(23f * 2f * hudScale)));
            var localMedic = _context._world.LocalPlayer;
            localMedic.GetMedicUberHudMeter(out _, out var meterMax);
            var meterValue = _context.GetPlayerMedicUberCharge(localMedic);
            _context.DrawScreenHealthBar(uberRectangle, meterValue, meterMax, false, Color.White, Color.Black);

            // Darken the uber icon and text when carrying intel with regular medigun (kritz can still activate)
            var deliveryMode = _context.GetPlayerMedicUberDeliveryMode(localMedic);
            var isKritz = deliveryMode == MedicUberDeliveryMode.Kritz;
            var isUberDisabled = localMedic.IsCarryingIntel && !isKritz;
            var iconColor = isUberDisabled ? new Color(128, 128, 128) : Color.White;
            var textColor = isUberDisabled ? new Color(108, 108, 92) : new Color(0xD9, 0xD9, 0xB7);
            _context.TryDrawScreenSprite("UberHudS", hudFrameIndex, iconPosition, iconColor, new Vector2(2f * hudScale, 2f * hudScale));
            if (_context.UsesGg2Presentation)
            {
                // UberHud: "SUPERBURST" centred at (w - 70, h - 90).
                _context.DrawHudTextCentered("SUPERBURST", TransformPoint(new Vector2(viewportWidth - 70f, viewportHeight - 90f)), Color.White, hudScale);
                _context.UpdateHudElementBounds(HudElementId.ClassMedicUber, Rectangle.Union(iconBounds, uberRectangle));
                return;
            }

            // Draw SUPER / BURST (or CRIT / CRAZE for kritz) stacked left-aligned after the '+' icon
            var labelScale = 0.5f * hudScale;
            var labelLineHeight = _context.MeasureMenuBitmapFontHeight(labelScale);
            var labelOrigin = TransformPoint(new Vector2(viewportWidth - 123f, viewportHeight - 89f - labelLineHeight - 1f));
            var (labelTop, labelBottom) = GetLastToDieMedicUberHudLabels(deliveryMode);
            _context.DrawMenuBitmapFontText(labelTop, new Vector2(labelOrigin.X, labelOrigin.Y), textColor, labelScale);
            _context.DrawMenuBitmapFontText(labelBottom, new Vector2(labelOrigin.X, labelOrigin.Y + labelLineHeight + 2f * hudScale - 1f * hudScale), textColor, labelScale);

            // Draw percentage right-aligned on the right side
            var percentScale = 1f * hudScale;
            var percentValue = meterMax > 0f
                ? (int)MathF.Round(100f * Math.Clamp(meterValue / meterMax, 0f, 1f))
                : 0;
            var percentText = $"{percentValue}%";
            var percentWidth = _context.MeasureMenuBitmapFontWidth(percentText, percentScale);
            var percentHeight = _context.MeasureMenuBitmapFontHeight(percentScale);
            var percentAnchor = TransformPoint(new Vector2(viewportWidth - 16f, viewportHeight - 85f - percentHeight / 2f));
            _context.DrawMenuBitmapFontText(percentText, new Vector2(percentAnchor.X - percentWidth, percentAnchor.Y), textColor, percentScale);
            _context.UpdateHudElementBounds(HudElementId.ClassMedicUber, Rectangle.Union(iconBounds, uberRectangle));
        }

        public void CollectMedicAssistHudElements(HudElementContext context, List<HudElementInstance> elements)
        {
            if (!_context._world.LocalPlayer.IsAlive)
            {
                return;
            }

            var healingTarget = GetLocalMedicHealingTarget();
            var healer = FindMedicHealingPlayer(_context.GetPlayerStateKey(_context._world.LocalPlayer));
            var showHealingTarget = _context.HudRuntimeSettings.ShowHealingEnabled
                && (healingTarget is not null || (_context._hudEditorOpen && _context._world.LocalPlayer.ClassId == PlayerClass.Medic));
            if (showHealingTarget)
            {
                SetMedicAssistRuntimeDefault(HudElementId.ClassMedicHealingTarget, HealingTargetHudYRatio, HudElementLayerClassMedicAssist);
                context.AddIfRegistered(elements, HudElementId.ClassMedicHealingTarget);
            }

            var showHealer = _context.HudRuntimeSettings.ShowHealerEnabled && (healer is not null || _context._hudEditorOpen);
            if (showHealer)
            {
                var yRatio = showHealingTarget ? HealerStackedHudYRatio : HealingTargetHudYRatio;
                SetMedicAssistRuntimeDefault(HudElementId.ClassMedicHealer, yRatio, HudElementLayerClassMedicAssist + 1);
                context.AddIfRegistered(elements, HudElementId.ClassMedicHealer);
            }
        }

        public void DrawMedicHealingTargetHud()
        {
            if (!_context.HudRuntimeSettings.ShowHealingEnabled || !_context._world.LocalPlayer.IsAlive)
            {
                return;
            }

            var healingTarget = GetLocalMedicHealingTarget();
            if (healingTarget is null && !_context._hudEditorOpen)
            {
                return;
            }

            var label = healingTarget is null ? "Healing: Target" : $"Healing: {GetHudPlayerLabel(healingTarget)}";
            var value = healingTarget?.Health ?? MedicAssistDummyHealth;
            var maxValue = healingTarget?.MaxHealth ?? MedicAssistDummyHealth;
            var targetTeam = healingTarget?.Team ?? _context._world.LocalPlayer.Team;
            if (_context.UsesGg2Presentation)
            {
                DrawGg2HealPanel(HudElementId.ClassMedicHealingTarget, label, value, maxValue, value, uberValue: null, uberMax: 0f);
                return;
            }

            DrawCenterStatusHud(HudElementId.ClassMedicHealingTarget, label, value, maxValue, 0.7f, targetTeam);
        }

        public void DrawMedicHealerHud()
        {
            if (!_context.HudRuntimeSettings.ShowHealerEnabled || !_context._world.LocalPlayer.IsAlive)
            {
                return;
            }

            var healer = FindMedicHealingPlayer(_context.GetPlayerStateKey(_context._world.LocalPlayer));
            if (healer is null && !_context._hudEditorOpen)
            {
                return;
            }

            var label = healer is null ? "Healer: Medic" : $"Healer: {GetHudPlayerLabel(healer)}";
            var healerTeam = healer?.Team ?? _context._world.LocalPlayer.Team;
            if (_context.UsesGg2Presentation)
            {
                var healerHealth = healer?.Health ?? MedicAssistDummyHealth;
                var healerMaxHealth = healer?.MaxHealth ?? MedicAssistDummyHealth;
                var uberValue = (float)MedicAssistDummyUberCharge;
                var uberMax = (float)PlayerEntity.MedicUberMaxCharge;
                if (healer is not null)
                {
                    healer.GetMedicUberHudMeter(out uberValue, out uberMax);
                }

                DrawGg2HealPanel(HudElementId.ClassMedicHealer, label, healerHealth, healerMaxHealth, healerHealth, uberValue, uberMax);
                return;
            }

            if (healer is not null)
            {
                healer.GetMedicUberHudMeter(out var meterValue, out var meterMax);
                DrawCenterStatusHud(HudElementId.ClassMedicHealer, label, meterValue, meterMax, 0.5f, healerTeam);
                return;
            }

            DrawCenterStatusHud(HudElementId.ClassMedicHealer, label, MedicAssistDummyUberCharge, PlayerEntity.MedicUberMaxCharge, 0.5f, healerTeam);
        }

        public void DrawHealerRadarHud(Vector2 cameraPosition, MouseState mouse)
        {
            if (!_context.HudRuntimeSettings.HealerRadarEnabled
                || !_context._world.LocalPlayer.IsAlive
                || _context._world.LocalPlayer.ClassId != PlayerClass.Medic
                || _context.IsLocalSpectatorPresentationActive())
            {
                return;
            }

            var viewportWidth = _context.ViewportWidth;
            var viewportHeight = _context.ViewportHeight;
            var viewBounds = new Rectangle((int)cameraPosition.X, (int)cameraPosition.Y, viewportWidth, viewportHeight);
            var cornerRadians = MathF.Asin(0.6f);
            var localPlayer = _context._world.LocalPlayer;
            var teamTextColor = localPlayer.Team == PlayerTeam.Blue ? new Color(100, 116, 132) : new Color(171, 78, 70);

            foreach (var teammate in _context.EnumerateRenderablePlayers())
            {
                if (ReferenceEquals(teammate, localPlayer)
                    || teammate.Team != localPlayer.Team
                    || _context.IsPlayerMutedByScoreboardSlot(teammate)
                    || !teammate.IsChatBubbleVisible
                    || (teammate.ChatBubbleFrameIndex != 45 && teammate.ChatBubbleFrameIndex != 49)
                    || viewBounds.Contains((int)teammate.X, (int)teammate.Y))
                {
                    continue;
                }

                var bubbleAlpha = MathHelper.Clamp(teammate.ChatBubbleAlpha, 0f, 1f);
                if (bubbleAlpha <= 0f)
                {
                    continue;
                }

                var theta = MathF.Atan2(localPlayer.Y - teammate.Y, teammate.X - localPlayer.X);
                if (theta < 0f)
                {
                    theta += MathF.PI * 2f;
                }

                var healthRatio = teammate.Health / (float)Math.Max(1, teammate.MaxHealth);
                var arrowFrame = Math.Clamp((int)MathF.Floor(healthRatio * 19f), 0, 19);
                var defaultAlertFrame = teammate.ChatBubbleFrameIndex == 49 ? 1 : 0;
                var detailedAlertFrame = GetMedicAlertClassFrame(teammate);
                var drawX = 0f;
                var drawY = 0f;
                var hovered = false;

                if (theta <= cornerRadians || theta > (MathF.PI * 2f) - cornerRadians)
                {
                    var unknown = ((viewportWidth / 2f) - (38f * MathF.Cos(theta))) * MathF.Tan(theta);
                    drawX = viewportWidth - (MathF.Cos(theta) * 38f);
                    drawY = (viewportHeight / 2f) - unknown;
                    hovered = mouse.X > drawX - 15f && mouse.Y > drawY - 15f && mouse.Y < drawY + 15f;
                    _context.TryDrawScreenSprite("MedRadarArrow", arrowFrame, new Vector2(drawX, drawY), Color.White * bubbleAlpha, Vector2.One, theta);
                    _context.TryDrawScreenSprite("MedAlert", hovered ? detailedAlertFrame : defaultAlertFrame, new Vector2(drawX, drawY), Color.White * bubbleAlpha, Vector2.One);
                    if (hovered)
                    {
                        var textY = theta < MathF.PI ? drawY + 20f : drawY - 20f;
                        _context.DrawBitmapFontTextRightAligned(GetHudPlayerLabel(teammate), new Vector2(viewportWidth, textY), teamTextColor * bubbleAlpha, 1f);
                    }
                }
                else if (theta > cornerRadians && theta <= MathF.PI - cornerRadians)
                {
                    var unknown = ((viewportHeight / 2f) - (38f * MathF.Sin(theta))) / MathF.Tan(theta);
                    drawX = unknown + (viewportWidth / 2f);
                    drawY = 38f * MathF.Sin(theta);
                    hovered = mouse.X > drawX - 15f && mouse.X < drawX + 15f && mouse.Y < drawY + 15f;
                    _context.TryDrawScreenSprite("MedRadarArrow", arrowFrame, new Vector2(drawX, drawY), Color.White * bubbleAlpha, Vector2.One, theta);
                    _context.TryDrawScreenSprite("MedAlert", hovered ? detailedAlertFrame : defaultAlertFrame, new Vector2(drawX, drawY), Color.White * bubbleAlpha, Vector2.One);
                    if (hovered)
                    {
                        _context.DrawBitmapFontTextCentered(GetHudPlayerLabel(teammate), new Vector2(drawX, drawY + 20f), teamTextColor * bubbleAlpha, 1f);
                    }
                }
                else if (theta > MathF.PI - cornerRadians && theta <= MathF.PI + cornerRadians)
                {
                    var unknown = ((viewportWidth / 2f) + (38f * MathF.Cos(theta))) * MathF.Tan(theta);
                    drawX = -(38f * MathF.Cos(theta));
                    drawY = unknown + (viewportHeight / 2f);
                    hovered = mouse.X < drawX + 15f && mouse.Y > drawY - 15f && mouse.Y < drawY + 15f;
                    _context.TryDrawScreenSprite("MedRadarArrow", arrowFrame, new Vector2(drawX, drawY), Color.White * bubbleAlpha, Vector2.One, theta);
                    _context.TryDrawScreenSprite("MedAlert", hovered ? detailedAlertFrame : defaultAlertFrame, new Vector2(drawX, drawY), Color.White * bubbleAlpha, Vector2.One);
                    if (hovered)
                    {
                        var textY = theta < MathF.PI ? drawY + 20f : drawY - 20f;
                        _context.DrawBitmapFontText(GetHudPlayerLabel(teammate), new Vector2(0f, textY), teamTextColor * bubbleAlpha, 1f);
                    }
                }
                else
                {
                    var unknown = ((viewportHeight / 2f) + (38f * MathF.Sin(theta))) / MathF.Tan(theta);
                    drawX = (viewportWidth / 2f) - unknown;
                    drawY = viewportHeight + (38f * MathF.Sin(theta));
                    hovered = mouse.X > drawX - 13f && mouse.X < drawX + 13f && mouse.Y > drawY - 13f;
                    _context.TryDrawScreenSprite("MedRadarArrow", arrowFrame, new Vector2(drawX, drawY), Color.White * bubbleAlpha, Vector2.One, theta);
                    _context.TryDrawScreenSprite("MedAlert", hovered ? detailedAlertFrame : defaultAlertFrame, new Vector2(drawX, drawY), Color.White * bubbleAlpha, Vector2.One);
                    if (hovered)
                    {
                        _context.DrawBitmapFontTextCentered(GetHudPlayerLabel(teammate), new Vector2(drawX, drawY - 20f), teamTextColor * bubbleAlpha, 1f);
                    }
                }
            }
        }

        private PlayerEntity? GetLocalMedicHealingTarget()
        {
            var healingTarget = _context._world.LocalPlayer.ClassId == PlayerClass.Medic
                && _context._world.LocalPlayer.IsMedicHealing
                && _context._world.LocalPlayer.MedicHealTargetId.HasValue
                    ? _context.FindPlayerById(_context._world.LocalPlayer.MedicHealTargetId.Value)
                    : null;
            return healingTarget is not null
                && healingTarget.IsAlive
                && healingTarget.Team == _context._world.LocalPlayer.Team
                    ? healingTarget
                    : null;
        }

        private void SetMedicAssistRuntimeDefault(string id, float viewportYRatio, int layer)
        {
            var y = MathF.Round(_context.ViewportHeight * viewportYRatio);
            _context.SetHudElementRuntimeDefault(new HudElementLayout(
                id,
                HudAnchor.TopCenter,
                new Vector2(0f, y),
                new Vector2(MedicAssistHudFallbackWidth, MedicAssistHudHeight),
                new Vector2(-MedicAssistHudFallbackWidth * 0.5f, 0f),
                Layer: layer));
        }

        public void DrawCenterStatusHud(string id, string label, float value, float maxValue, float textAlpha, PlayerTeam team)
        {
            if (!_context.TryResolveHudElement(id, out var resolved))
            {
                return;
            }

            const float textScale = 1f;
            var hudScale = resolved.Layout.Scale;
            var textWidth = _context.MeasureBitmapFontWidth(label, textScale * hudScale);
            var hudWidth = Math.Max(1, (int)MathF.Ceiling(textWidth + (20f * hudScale)));
            var hudHeight = Math.Max(1, (int)MathF.Round(MedicAssistHudHeight * hudScale));
            var hudX = (int)MathF.Round(resolved.Origin.X - (hudWidth * 0.5f));
            var hudY = (int)MathF.Round(resolved.Origin.Y);
            var destination = new Rectangle(hudX, hudY, hudWidth, hudHeight);
            var fillColor = team == PlayerTeam.Blue ? new Color(0x48, 0x5C, 0x67) : new Color(0xA5, 0x46, 0x40);
            _context.DrawRoundedRectangleFillThenBorder(destination, fillColor * 0.6f, new Color(0xD9, 0xD9, 0xB7) * 0.6f, outlineThickness: 2, radius: 8);
            var textColor = new Color(0xD9, 0xD9, 0xB7);
            _context.DrawHudTextCentered(label, new Vector2(resolved.Origin.X, hudY + (12f * hudScale)), textColor * textAlpha, textScale * hudScale);
            var barX = hudX + (int)MathF.Round(10f * hudScale);
            var barY = hudY + (int)MathF.Round(20f * hudScale);
            var barTotalWidth = Math.Max(1, hudWidth - (int)MathF.Round(20f * hudScale));
            var barHeight = Math.Max(1, (int)MathF.Round(8f * hudScale));
            var fillWidth = maxValue > 0f ? Math.Clamp((int)MathF.Round(barTotalWidth * MathF.Max(0f, value) / maxValue), 0, barTotalWidth) : 0;
            if (fillWidth > 0)
            {
                _context._spriteBatch.Draw(_context._pixel, new Rectangle(barX, barY, fillWidth, barHeight), _context.ApplyCurrentHudElementOpacity(textColor * 0.6f));
            }
            var emptyWidth = barTotalWidth - fillWidth;
            if (emptyWidth > 0)
            {
                _context._spriteBatch.Draw(_context._pixel, new Rectangle(barX + fillWidth, barY, emptyWidth, barHeight), _context.ApplyCurrentHudElementOpacity(Color.Black * 0.6f));
            }
            _context.UpdateHudElementBounds(id, destination);
        }

        /// <summary>
        /// GG2's HealedHud / HealingHud panel: a team panel (HealedHudS) as wide as the label
        /// + 20, the name centred 20 to the right of the element's centre, an HP box on the left
        /// (HealedHudHPS over a 44x32 bottom-up white bar, the HP number on it), an end cap
        /// (HealedHudCapS), and for the healer panel the medic's uber as a thin bar.
        /// </summary>
        private void DrawGg2HealPanel(string id, string label, float healthValue, float healthMax, int healthText, float? uberValue, float uberMax)
        {
            if (!_context.TryResolveHudElement(id, out var resolved))
            {
                return;
            }

            var s = resolved.Layout.Scale;
            var mid = resolved.Origin.X;
            var y = resolved.Origin.Y;
            var width = _context.MeasureBitmapFontWidth(label, s) + (20f * s);
            var frame = _context._world.LocalPlayer.Team == PlayerTeam.Blue ? 1 : 0;
            var left = mid - (width / 2f);

            DrawGg2StretchedSprite("HealedHudS", frame, left + (20f * s), y, width, 36f * s);
            _context.DrawHudTextCentered(label, new Vector2(mid + (20f * s), y + (12f * s)), Color.White, s);
            _context.DrawScreenHealthBar(
                ToRectangle(left - (24f * s), y + (2f * s), 44f * s, 32f * s),
                MathF.Max(0f, healthValue),
                MathF.Max(1f, healthMax),
                false,
                Color.White,
                Color.Black,
                HudFillDirection.VerticalBottomToTop);
            if (uberValue is { } uber)
            {
                _context.DrawScreenHealthBar(
                    ToRectangle(left + (30f * s), y + (20f * s), width - (20f * s), 8f * s),
                    MathF.Max(0f, uber),
                    MathF.Max(1f, uberMax),
                    false,
                    Color.White,
                    Color.Black);
            }

            DrawGg2StretchedSprite("HealedHudHPS", frame, left - (26f * s), y, 48f * s, 36f * s);
            DrawGg2StretchedSprite("HealedHudCapS", frame, mid + (width / 2f) + (18f * s), y, 6f * s, 36f * s);
            _context.DrawHudTextCentered(Math.Max(0, healthText).ToString(System.Globalization.CultureInfo.InvariantCulture), new Vector2(left - (2f * s), y + (18f * s)), Color.White, s);
            _context.UpdateHudElementBounds(id, ToRectangle(left - (26f * s), y, width + (50f * s), 36f * s));
        }

        private void DrawGg2StretchedSprite(string spriteName, int frameIndex, float x, float y, float width, float height)
        {
            if (_context.GetResolvedSprite(spriteName) is not { Frames.Count: > 0 } sprite)
            {
                return;
            }

            var frame = sprite.Frames[Math.Clamp(frameIndex, 0, sprite.Frames.Count - 1)];
            var scale = new Vector2(width / Math.Max(1, frame.Width), height / Math.Max(1, frame.Height));
            _context.TryDrawScreenSprite(spriteName, frameIndex, new Vector2(x, y) + (sprite.Origin.ToVector2() * scale), Color.White, scale);
        }

        private static Rectangle ToRectangle(float x, float y, float width, float height) => new(
            (int)MathF.Round(x),
            (int)MathF.Round(y),
            Math.Max(1, (int)MathF.Round(width)),
            Math.Max(1, (int)MathF.Round(height)));

        public PlayerEntity? FindMedicHealingPlayer(int playerId)
        {
            foreach (var candidate in _context.EnumerateRenderablePlayers())
            {
                if (candidate.ClassId != PlayerClass.Medic
                    || !candidate.IsAlive
                    || !candidate.IsMedicHealing
                    || !candidate.MedicHealTargetId.HasValue
                    || candidate.MedicHealTargetId.Value != playerId
                    || candidate.Team != _context._world.LocalPlayer.Team)
                {
                    continue;
                }

                return candidate;
            }

            return null;
        }

        private static int GetMedicAlertClassFrame(PlayerEntity player)
        {
            return ((player.Team == PlayerTeam.Blue ? 1 : 0) * 10) + GetLegacyMedicAlertClassIndex(player.ClassId) + 2;
        }

        private static int GetLegacyMedicAlertClassIndex(PlayerClass playerClass)
        {
            return playerClass switch
            {
                PlayerClass.Scout => 0,
                PlayerClass.Soldier => 1,
                PlayerClass.Sniper => 2,
                PlayerClass.Demoman => 3,
                PlayerClass.Medic => 4,
                PlayerClass.Engineer => 5,
                PlayerClass.Heavy => 6,
                PlayerClass.Spy => 7,
                PlayerClass.Pyro => 8,
                PlayerClass.Quote => 9,
                _ => 0,
            };
        }
}
