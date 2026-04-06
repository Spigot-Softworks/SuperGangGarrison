#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class GameplayMedicHudController
    {
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
            var hudFrameIndex = _game._world.LocalPlayer.Team == PlayerTeam.Blue ? 1 : 0;
            var uberRectangle = new Rectangle(viewportWidth - 135, viewportHeight - 100, 120, 32);
            _game.DrawScreenHealthBar(uberRectangle, _game.GetPlayerMedicUberCharge(_game._world.LocalPlayer), 2000f, false, Color.White, Color.Black);
            _game.TryDrawScreenSprite("UberHudS", hudFrameIndex, new Vector2(viewportWidth - 80f, viewportHeight - 85f), Color.White, new Vector2(2f, 2f));
            _game.DrawHudTextCentered("SUPERBURST", new Vector2(viewportWidth - 70f, viewportHeight - 90f), Color.White, 0.9f);
        }

        public void DrawMedicAssistHud()
        {
            if (!_game._world.LocalPlayer.IsAlive)
            {
                return;
            }

            var healingTarget = _game._world.LocalPlayer.ClassId == PlayerClass.Medic
                && _game._world.LocalPlayer.IsMedicHealing
                && _game._world.LocalPlayer.MedicHealTargetId.HasValue
                    ? _game.FindPlayerById(_game._world.LocalPlayer.MedicHealTargetId.Value)
                    : null;
            if (healingTarget is not null && !healingTarget.IsAlive)
            {
                healingTarget = null;
            }

            var healer = FindMedicHealingPlayer(_game.GetPlayerStateKey(_game._world.LocalPlayer));
            var drewHealingHud = false;
            if (_game._showHealingEnabled && healingTarget is not null)
            {
                DrawCenterStatusHud($"Healing: {GetHudPlayerLabel(healingTarget)}", healingTarget.Health, healingTarget.MaxHealth, 450f / 600f, 0.7f);
                drewHealingHud = true;
            }

            if (_game._showHealerEnabled && healer is not null)
            {
                DrawCenterStatusHud($"Healer: {GetHudPlayerLabel(healer)}", healer.MedicUberCharge, 2000f, drewHealingHud ? 490f / 600f : 450f / 600f, 0.5f);
            }
        }

        public void DrawHealerRadarHud(Vector2 cameraPosition, MouseState mouse)
        {
            if (!_game._healerRadarEnabled
                || !_game._world.LocalPlayer.IsAlive
                || _game._world.LocalPlayer.ClassId != PlayerClass.Medic
                || _game._networkClient.IsSpectator)
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
                var detailedAlertFrame = ((int)teammate.Team * 10) + (int)teammate.ClassId + 2;
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

        public void DrawCenterStatusHud(string label, float value, float maxValue, float viewportYRatio, float textAlpha)
        {
            var sprite = _game._runtimeAssets.GetSprite("HealedHudS");
            if (sprite is null || sprite.Frames.Count == 0)
            {
                return;
            }

            var frameIndex = _game._world.LocalPlayer.Team == PlayerTeam.Blue ? 1 : 0;
            var frame = sprite.Frames[Math.Clamp(frameIndex, 0, sprite.Frames.Count - 1)];
            var viewportWidth = _game.ViewportWidth;
            var viewportHeight = _game.ViewportHeight;
            var textWidth = _game.MeasureBitmapFontWidth(label, 0.7f);
            var hudWidth = (int)MathF.Ceiling(textWidth) + 20;
            var hudHeight = 40;
            var hudX = (viewportWidth / 2) - (hudWidth / 2);
            var hudY = (int)MathF.Round(viewportHeight * viewportYRatio);
            var destination = new Rectangle(hudX, hudY, hudWidth, hudHeight);
            _game._spriteBatch.Draw(frame, destination, Color.White * 0.5f);
            _game.DrawHudTextCentered(label, new Vector2(viewportWidth / 2f, hudY + 12f), Color.White * textAlpha, 0.7f);
            _game.DrawScreenHealthBar(new Rectangle(hudX + 10, hudY + 20, Math.Max(1, hudWidth - 20), 8), value, maxValue, false, Color.White, Color.Black);
        }

        public PlayerEntity? FindMedicHealingPlayer(int playerId)
        {
            foreach (var candidate in _game.EnumerateRenderablePlayers())
            {
                if (candidate.ClassId != PlayerClass.Medic
                    || !candidate.IsAlive
                    || !candidate.IsMedicHealing
                    || !candidate.MedicHealTargetId.HasValue
                    || candidate.MedicHealTargetId.Value != playerId)
                {
                    continue;
                }

                return candidate;
            }

            return null;
        }
    }
}
