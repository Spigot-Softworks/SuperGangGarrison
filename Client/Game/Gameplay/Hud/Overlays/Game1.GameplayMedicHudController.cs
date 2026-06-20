#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class GameplayMedicHudController
    {
        private const float HealingTargetHudYRatio = 450f / 600f;
        private const float HealerStackedHudYRatio = 490f / 600f;
        private const float MedicAssistHudFallbackWidth = 220f;
        private const float MedicAssistHudHeight = 40f;
        private const int MedicAssistDummyHealth = 125;
        private const int MedicAssistDummyUberCharge = 1200;

        private readonly Game1 _game;

        public GameplayMedicHudController(Game1 game)
        {
            _game = game;
        }

        public void DrawMedicHud()
        {
            if (_game._world.LocalPlayer.ClassId != PlayerClass.Medic)
            {
                return;
            }

            var viewportWidth = _game.ViewportWidth;
            var viewportHeight = _game.ViewportHeight;
            var defaultIconPosition = new Vector2(viewportWidth - 80f, viewportHeight - 85f);
            if (!_game.TryResolveHudElement(HudElementId.ClassMedicUber, out var resolved))
            {
                return;
            }

            var iconPosition = resolved.Origin;
            var hudScale = resolved.Layout.Scale;
            Vector2 TransformPoint(Vector2 point) => iconPosition + ((point - defaultIconPosition) * hudScale);

            var hudFrameIndex = _game._world.LocalPlayer.Team == PlayerTeam.Blue ? 1 : 0;
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
            _game._world.LocalPlayer.GetMedicUberHudMeter(out var meterValue, out var meterMax);
            _game.DrawScreenHealthBar(uberRectangle, meterValue, meterMax, false, Color.White, Color.Black);

            // Darken the uber icon and text when carrying intel with regular medigun (kritz can still activate)
            var isKritz = _game._world.LocalPlayer.HasEquippedBehavior(BuiltInGameplayBehaviorIds.MedigunCrit);
            var isUberDisabled = _game._world.LocalPlayer.IsCarryingIntel && !isKritz;
            var iconColor = isUberDisabled ? new Color(128, 128, 128) : Color.White;
            var textColor = isUberDisabled ? new Color(108, 108, 92) : new Color(0xD9, 0xD9, 0xB7);
            _game.TryDrawScreenSprite("UberHudS", hudFrameIndex, iconPosition, iconColor, new Vector2(2f * hudScale, 2f * hudScale));

            // Draw SUPER / BURST (or CRIT / CRAZE for kritz) stacked left-aligned after the '+' icon
            var labelScale = 0.5f * hudScale;
            var labelLineHeight = _game.MeasureMenuBitmapFontHeight(labelScale);
            var labelOrigin = TransformPoint(new Vector2(viewportWidth - 123f, viewportHeight - 89f - labelLineHeight - 1f));
            var labelTop = isKritz ? "CRIT" : "SUPER";
            var labelBottom = isKritz ? "CRAZE" : "BURST";
            _game.DrawMenuBitmapFontText(labelTop, new Vector2(labelOrigin.X, labelOrigin.Y), textColor, labelScale);
            _game.DrawMenuBitmapFontText(labelBottom, new Vector2(labelOrigin.X, labelOrigin.Y + labelLineHeight + 2f * hudScale - 1f * hudScale), textColor, labelScale);

            // Draw percentage right-aligned on the right side
            var percentScale = 1f * hudScale;
            var percentValue = meterMax > 0f
                ? (int)MathF.Round(100f * Math.Clamp(meterValue / meterMax, 0f, 1f))
                : 0;
            var percentText = $"{percentValue}%";
            var percentWidth = _game.MeasureMenuBitmapFontWidth(percentText, percentScale);
            var percentHeight = _game.MeasureMenuBitmapFontHeight(percentScale);
            var percentAnchor = TransformPoint(new Vector2(viewportWidth - 16f, viewportHeight - 85f - percentHeight / 2f));
            _game.DrawMenuBitmapFontText(percentText, new Vector2(percentAnchor.X - percentWidth, percentAnchor.Y), textColor, percentScale);
            _game.UpdateHudElementBounds(HudElementId.ClassMedicUber, Rectangle.Union(iconBounds, uberRectangle));
        }

        public void CollectMedicAssistHudElements(HudElementContext context, List<HudElementInstance> elements)
        {
            if (!_game._world.LocalPlayer.IsAlive)
            {
                return;
            }

            var healingTarget = GetLocalMedicHealingTarget();
            var healer = FindMedicHealingPlayer(_game.GetPlayerStateKey(_game._world.LocalPlayer));
            var showHealingTarget = _game._showHealingEnabled
                && (healingTarget is not null || (_game._hudEditorOpen && _game._world.LocalPlayer.ClassId == PlayerClass.Medic));
            if (showHealingTarget)
            {
                SetMedicAssistRuntimeDefault(HudElementId.ClassMedicHealingTarget, HealingTargetHudYRatio, HudElementLayerClassMedicAssist);
                context.AddIfRegistered(elements, HudElementId.ClassMedicHealingTarget);
            }

            var showHealer = _game._showHealerEnabled && (healer is not null || _game._hudEditorOpen);
            if (showHealer)
            {
                var yRatio = showHealingTarget ? HealerStackedHudYRatio : HealingTargetHudYRatio;
                SetMedicAssistRuntimeDefault(HudElementId.ClassMedicHealer, yRatio, HudElementLayerClassMedicAssist + 1);
                context.AddIfRegistered(elements, HudElementId.ClassMedicHealer);
            }
        }

        public void DrawMedicHealingTargetHud()
        {
            if (!_game._showHealingEnabled || !_game._world.LocalPlayer.IsAlive)
            {
                return;
            }

            var healingTarget = GetLocalMedicHealingTarget();
            if (healingTarget is null && !_game._hudEditorOpen)
            {
                return;
            }

            var label = healingTarget is null ? "Healing: Target" : $"Healing: {GetHudPlayerLabel(healingTarget)}";
            var value = healingTarget?.Health ?? MedicAssistDummyHealth;
            var maxValue = healingTarget?.MaxHealth ?? MedicAssistDummyHealth;
            var targetTeam = healingTarget?.Team ?? _game._world.LocalPlayer.Team;
            DrawCenterStatusHud(HudElementId.ClassMedicHealingTarget, label, value, maxValue, 0.7f, targetTeam);
        }

        public void DrawMedicHealerHud()
        {
            if (!_game._showHealerEnabled || !_game._world.LocalPlayer.IsAlive)
            {
                return;
            }

            var healer = FindMedicHealingPlayer(_game.GetPlayerStateKey(_game._world.LocalPlayer));
            if (healer is null && !_game._hudEditorOpen)
            {
                return;
            }

            var label = healer is null ? "Healer: Medic" : $"Healer: {GetHudPlayerLabel(healer)}";
            var healerTeam = healer?.Team ?? _game._world.LocalPlayer.Team;
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
            if (!_game._healerRadarEnabled
                || !_game._world.LocalPlayer.IsAlive
                || _game._world.LocalPlayer.ClassId != PlayerClass.Medic
                || _game.IsLocalSpectatorPresentationActive())
            {
                return;
            }

            var viewportWidth = _game.ViewportWidth;
            var viewportHeight = _game.ViewportHeight;
            var viewBounds = new Rectangle((int)cameraPosition.X, (int)cameraPosition.Y, viewportWidth, viewportHeight);
            var cornerRadians = MathF.Asin(0.6f);
            var localPlayer = _game._world.LocalPlayer;
            var teamTextColor = localPlayer.Team == PlayerTeam.Blue ? new Color(100, 116, 132) : new Color(171, 78, 70);

            foreach (var teammate in _game.EnumerateRenderablePlayers())
            {
                if (ReferenceEquals(teammate, localPlayer)
                    || teammate.Team != localPlayer.Team
                    || _game.IsPlayerMutedByScoreboardSlot(teammate)
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
                    _game.TryDrawScreenSprite("MedRadarArrow", arrowFrame, new Vector2(drawX, drawY), Color.White * bubbleAlpha, Vector2.One, theta);
                    _game.TryDrawScreenSprite("MedAlert", hovered ? detailedAlertFrame : defaultAlertFrame, new Vector2(drawX, drawY), Color.White * bubbleAlpha, Vector2.One);
                    if (hovered)
                    {
                        var textY = theta < MathF.PI ? drawY + 20f : drawY - 20f;
                        _game.DrawBitmapFontTextRightAligned(GetHudPlayerLabel(teammate), new Vector2(viewportWidth, textY), teamTextColor * bubbleAlpha, 1f);
                    }
                }
                else if (theta > cornerRadians && theta <= MathF.PI - cornerRadians)
                {
                    var unknown = ((viewportHeight / 2f) - (38f * MathF.Sin(theta))) / MathF.Tan(theta);
                    drawX = unknown + (viewportWidth / 2f);
                    drawY = 38f * MathF.Sin(theta);
                    hovered = mouse.X > drawX - 15f && mouse.X < drawX + 15f && mouse.Y < drawY + 15f;
                    _game.TryDrawScreenSprite("MedRadarArrow", arrowFrame, new Vector2(drawX, drawY), Color.White * bubbleAlpha, Vector2.One, theta);
                    _game.TryDrawScreenSprite("MedAlert", hovered ? detailedAlertFrame : defaultAlertFrame, new Vector2(drawX, drawY), Color.White * bubbleAlpha, Vector2.One);
                    if (hovered)
                    {
                        _game.DrawBitmapFontTextCentered(GetHudPlayerLabel(teammate), new Vector2(drawX, drawY + 20f), teamTextColor * bubbleAlpha, 1f);
                    }
                }
                else if (theta > MathF.PI - cornerRadians && theta <= MathF.PI + cornerRadians)
                {
                    var unknown = ((viewportWidth / 2f) + (38f * MathF.Cos(theta))) * MathF.Tan(theta);
                    drawX = -(38f * MathF.Cos(theta));
                    drawY = unknown + (viewportHeight / 2f);
                    hovered = mouse.X < drawX + 15f && mouse.Y > drawY - 15f && mouse.Y < drawY + 15f;
                    _game.TryDrawScreenSprite("MedRadarArrow", arrowFrame, new Vector2(drawX, drawY), Color.White * bubbleAlpha, Vector2.One, theta);
                    _game.TryDrawScreenSprite("MedAlert", hovered ? detailedAlertFrame : defaultAlertFrame, new Vector2(drawX, drawY), Color.White * bubbleAlpha, Vector2.One);
                    if (hovered)
                    {
                        var textY = theta < MathF.PI ? drawY + 20f : drawY - 20f;
                        _game.DrawBitmapFontText(GetHudPlayerLabel(teammate), new Vector2(0f, textY), teamTextColor * bubbleAlpha, 1f);
                    }
                }
                else
                {
                    var unknown = ((viewportHeight / 2f) + (38f * MathF.Sin(theta))) / MathF.Tan(theta);
                    drawX = (viewportWidth / 2f) - unknown;
                    drawY = viewportHeight + (38f * MathF.Sin(theta));
                    hovered = mouse.X > drawX - 13f && mouse.X < drawX + 13f && mouse.Y > drawY - 13f;
                    _game.TryDrawScreenSprite("MedRadarArrow", arrowFrame, new Vector2(drawX, drawY), Color.White * bubbleAlpha, Vector2.One, theta);
                    _game.TryDrawScreenSprite("MedAlert", hovered ? detailedAlertFrame : defaultAlertFrame, new Vector2(drawX, drawY), Color.White * bubbleAlpha, Vector2.One);
                    if (hovered)
                    {
                        _game.DrawBitmapFontTextCentered(GetHudPlayerLabel(teammate), new Vector2(drawX, drawY - 20f), teamTextColor * bubbleAlpha, 1f);
                    }
                }
            }
        }

        private PlayerEntity? GetLocalMedicHealingTarget()
        {
            var healingTarget = _game._world.LocalPlayer.ClassId == PlayerClass.Medic
                && _game._world.LocalPlayer.IsMedicHealing
                && _game._world.LocalPlayer.MedicHealTargetId.HasValue
                    ? _game.FindPlayerById(_game._world.LocalPlayer.MedicHealTargetId.Value)
                    : null;
            return healingTarget is not null
                && healingTarget.IsAlive
                && healingTarget.Team == _game._world.LocalPlayer.Team
                    ? healingTarget
                    : null;
        }

        private void SetMedicAssistRuntimeDefault(string id, float viewportYRatio, int layer)
        {
            var y = MathF.Round(_game.ViewportHeight * viewportYRatio);
            _game.SetHudElementRuntimeDefault(new HudElementLayout(
                id,
                HudAnchor.TopCenter,
                new Vector2(0f, y),
                new Vector2(MedicAssistHudFallbackWidth, MedicAssistHudHeight),
                new Vector2(-MedicAssistHudFallbackWidth * 0.5f, 0f),
                Layer: layer));
        }

        public void DrawCenterStatusHud(string id, string label, float value, float maxValue, float textAlpha, PlayerTeam team)
        {
            if (!_game.TryResolveHudElement(id, out var resolved))
            {
                return;
            }

            const float textScale = 1f;
            var hudScale = resolved.Layout.Scale;
            var textWidth = _game.MeasureBitmapFontWidth(label, textScale * hudScale);
            var hudWidth = Math.Max(1, (int)MathF.Ceiling(textWidth + (20f * hudScale)));
            var hudHeight = Math.Max(1, (int)MathF.Round(MedicAssistHudHeight * hudScale));
            var hudX = (int)MathF.Round(resolved.Origin.X - (hudWidth * 0.5f));
            var hudY = (int)MathF.Round(resolved.Origin.Y);
            var destination = new Rectangle(hudX, hudY, hudWidth, hudHeight);
            var fillColor = team == PlayerTeam.Blue ? new Color(0x48, 0x5C, 0x67) : new Color(0xA5, 0x46, 0x40);
            _game.DrawRoundedRectangleFillThenBorder(destination, fillColor * 0.6f, new Color(0xD9, 0xD9, 0xB7) * 0.6f, outlineThickness: 2, radius: 8);
            var textColor = new Color(0xD9, 0xD9, 0xB7);
            _game.DrawHudTextCentered(label, new Vector2(resolved.Origin.X, hudY + (12f * hudScale)), textColor * textAlpha, textScale * hudScale);
            var barX = hudX + (int)MathF.Round(10f * hudScale);
            var barY = hudY + (int)MathF.Round(20f * hudScale);
            var barTotalWidth = Math.Max(1, hudWidth - (int)MathF.Round(20f * hudScale));
            var barHeight = Math.Max(1, (int)MathF.Round(8f * hudScale));
            var fillWidth = maxValue > 0f ? Math.Clamp((int)MathF.Round(barTotalWidth * MathF.Max(0f, value) / maxValue), 0, barTotalWidth) : 0;
            if (fillWidth > 0)
            {
                _game._spriteBatch.Draw(_game._pixel, new Rectangle(barX, barY, fillWidth, barHeight), _game.ApplyCurrentHudElementOpacity(textColor * 0.6f));
            }
            var emptyWidth = barTotalWidth - fillWidth;
            if (emptyWidth > 0)
            {
                _game._spriteBatch.Draw(_game._pixel, new Rectangle(barX + fillWidth, barY, emptyWidth, barHeight), _game.ApplyCurrentHudElementOpacity(Color.Black * 0.6f));
            }
            _game.UpdateHudElementBounds(id, destination);
        }

        public PlayerEntity? FindMedicHealingPlayer(int playerId)
        {
            foreach (var candidate in _game.EnumerateRenderablePlayers())
            {
                if (candidate.ClassId != PlayerClass.Medic
                    || !candidate.IsAlive
                    || !candidate.IsMedicHealing
                    || !candidate.MedicHealTargetId.HasValue
                    || candidate.MedicHealTargetId.Value != playerId
                    || candidate.Team != _game._world.LocalPlayer.Team)
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
}
