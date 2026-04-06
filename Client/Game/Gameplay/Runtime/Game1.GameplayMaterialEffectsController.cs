#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class GameplayMaterialEffectsController
    {
        private readonly Game1 _game;

        public GameplayMaterialEffectsController(Game1 game)
        {
            _game = game;
        }

        public void ResetTransientEffects()
        {
            _game._pendingWeaponShellVisuals.Clear();
            _game._shellVisuals.Clear();
            _game._looseSheetVisuals.Clear();
        }

        public void AdvanceLooseSheetVisuals()
        {
            for (var index = _game._looseSheetVisuals.Count - 1; index >= 0; index -= 1)
            {
                var sheet = _game._looseSheetVisuals[index];
                sheet.TicksRemaining -= 1;
                if (sheet.IsBurning)
                {
                    sheet.BurnTicksRemaining -= 1;
                    sheet.BurnAnimationTicks += 1;
                }

                if (sheet.TicksRemaining <= 0 || (sheet.IsBurning && sheet.BurnTicksRemaining <= 0))
                {
                    _game._looseSheetVisuals.RemoveAt(index);
                    continue;
                }

                var sheetX = sheet.X;
                var sheetY = sheet.Y;
                var velocityX = sheet.VelocityX;
                var velocityY = sheet.VelocityY;
                AdvanceLooseSheetAxis(ref sheetX, sheetY, ref velocityX, horizontal: true);
                AdvanceLooseSheetAxis(ref sheetY, sheetX, ref velocityY, horizontal: false);

                if (!sheet.IsBurning && IsLooseSheetIgnited(sheetX, sheetY))
                {
                    sheet.IsBurning = true;
                    sheet.SpriteName = "SheetBurning";
                    sheet.BurnTicksRemaining = LooseSheetVisual.BurnLifetimeTicks;
                    sheet.BurnAnimationTicks = 0;
                }

                if (!sheet.IsBurning && !IsLooseSheetBlocked(sheetX, sheetY + 1f))
                {
                    velocityY = MathF.Min(1.4f, velocityY + 0.035f);
                }
                else if (!sheet.IsBurning)
                {
                    velocityX *= 0.95f;
                }
                else
                {
                    velocityY = MathF.Max(-1.8f, velocityY - 0.2f);
                }

                velocityX *= 0.985f;
                sheet.X = sheetX;
                sheet.Y = sheetY;
                sheet.VelocityX = velocityX;
                sheet.VelocityY = velocityY;
                sheet.RotationRadians += sheet.RotationSpeedRadians;
            }
        }

        public void AdvanceShellVisuals()
        {
            if (_game._particleMode != 0)
            {
                _game._pendingWeaponShellVisuals.Clear();
                _game._shellVisuals.Clear();
                return;
            }

            const float clientTickSeconds = 1f / ClientUpdateTicksPerSecond;
            for (var index = _game._pendingWeaponShellVisuals.Count - 1; index >= 0; index -= 1)
            {
                var pendingShell = _game._pendingWeaponShellVisuals[index];
                pendingShell.DelaySeconds -= clientTickSeconds;
                if (pendingShell.DelaySeconds > 0f)
                {
                    continue;
                }

                SpawnPendingWeaponShellVisual(pendingShell);
                _game._pendingWeaponShellVisuals.RemoveAt(index);
            }

            var gravityPerTick = ScaleSourceTickDistance(0.7f);
            var settleSpeed = ScaleSourceTickDistance(1f);
            for (var index = _game._shellVisuals.Count - 1; index >= 0; index -= 1)
            {
                var shell = _game._shellVisuals[index];
                if (shell.TicksUntilFade > 0)
                {
                    shell.TicksUntilFade -= 1;
                }
                else
                {
                    shell.Fade = true;
                }

                if (shell.Fade)
                {
                    shell.Alpha -= 0.05f;
                }

                if (shell.Alpha < 0.3f)
                {
                    _game._shellVisuals.RemoveAt(index);
                    continue;
                }

                if (shell.Stuck)
                {
                    continue;
                }

                shell.RotationDegrees += shell.RotationSpeedDegrees;

                if (IsShellBlocked(shell.X + shell.VelocityX, shell.Y))
                {
                    var normalizedAngle = (shell.RotationDegrees % 360f + 360f) % 360f;
                    shell.RotationDegrees = normalizedAngle > 0f && normalizedAngle < 180f ? 90f : 270f;
                    shell.VelocityX *= -0.6f;
                    shell.RotationSpeedDegrees *= 0.8f;
                }

                if (IsShellBlocked(shell.X, shell.Y + shell.VelocityY))
                {
                    shell.VelocityY *= -0.7f;
                    shell.VelocityY = MathF.Max(-ScaleSourceTickDistance(2.5f), shell.VelocityY);
                    shell.VelocityX *= 0.7f;
                    shell.RotationSpeedDegrees *= 0.8f;

                    var normalizedAngle = (shell.RotationDegrees % 360f + 360f) % 360f;
                    shell.RotationDegrees = normalizedAngle > 90f && normalizedAngle < 270f ? 180f : 0f;
                    if (MathF.Abs(shell.VelocityY) < settleSpeed)
                    {
                        shell.Stuck = true;
                        shell.RotationSpeedDegrees = 0f;
                        shell.VelocityY = 0f;
                    }
                }

                shell.X += shell.VelocityX;
                shell.Y += shell.VelocityY;
                if (!shell.Stuck)
                {
                    shell.VelocityY += gravityPerTick;
                }
            }
        }

        public void DrawLooseSheetVisuals(Vector2 cameraPosition)
        {
            for (var index = 0; index < _game._looseSheetVisuals.Count; index += 1)
            {
                var sheet = _game._looseSheetVisuals[index];
                var sprite = _game._runtimeAssets.GetSprite(sheet.SpriteName);
                var alpha = sheet.TicksRemaining <= LooseSheetVisual.FadeTicks
                    ? sheet.TicksRemaining / (float)LooseSheetVisual.FadeTicks
                    : 1f;
                if (sprite is not null && sprite.Frames.Count > 0)
                {
                    var frameIndex = sheet.IsBurning ? Math.Clamp(sheet.BurnAnimationTicks / 4, 0, sprite.Frames.Count - 1) : 0;
                    _game._spriteBatch.Draw(sprite.Frames[frameIndex], new Vector2(sheet.X - cameraPosition.X, sheet.Y - cameraPosition.Y), null, Color.White * alpha, sheet.RotationRadians, sprite.Origin.ToVector2(), new Vector2(2f, 2f), SpriteEffects.None, 0f);
                    continue;
                }

                var rectangle = new Rectangle((int)(sheet.X - 5f - cameraPosition.X), (int)(sheet.Y - 5f - cameraPosition.Y), 10, 10);
                _game._spriteBatch.Draw(_game._pixel, rectangle, new Color(230, 230, 220) * alpha);
            }
        }

        public void DrawShellVisuals(Vector2 cameraPosition)
        {
            if (_game._particleMode != 0)
            {
                return;
            }

            var shellSprite = _game._runtimeAssets.GetSprite("ShellS");
            for (var index = 0; index < _game._shellVisuals.Count; index += 1)
            {
                var shell = _game._shellVisuals[index];
                if (shellSprite is not null && shellSprite.Frames.Count > 0)
                {
                    var frameIndex = Math.Clamp(shell.FrameIndex, 0, shellSprite.Frames.Count - 1);
                    _game._spriteBatch.Draw(shellSprite.Frames[frameIndex], new Vector2(shell.X - cameraPosition.X, shell.Y - cameraPosition.Y), null, Color.White * shell.Alpha, MathHelper.ToRadians(shell.RotationDegrees), shellSprite.Origin.ToVector2(), Vector2.One, SpriteEffects.None, 0f);
                    continue;
                }

                var shellRectangle = new Rectangle((int)(shell.X - 2f - cameraPosition.X), (int)(shell.Y - 2f - cameraPosition.Y), 4, 4);
                _game._spriteBatch.Draw(_game._pixel, shellRectangle, new Color(230, 210, 160) * shell.Alpha);
            }
        }

        public void QueueWeaponShellVisual(PlayerEntity player, float delaySeconds, int count)
        {
            QueueWeaponShellVisual(player, delaySeconds, count, player.ClassId);
        }

        public void QueueWeaponShellVisual(PlayerEntity player, float delaySeconds, int count, PlayerClass classId)
        {
            if (_game._particleMode != 0 || count <= 0)
            {
                return;
            }

            _game._pendingWeaponShellVisuals.Add(new PendingWeaponShellVisual(_game.GetPlayerStateKey(player), classId, player.Team, Math.Max(0f, delaySeconds), count));
        }

        public void SpawnLooseSheetVisual(float x, float y, float initialHorizontalSpeed)
        {
            string[] sheetSprites = ["SheetFalling1", "SheetFalling2", "SheetFalling3"];
            var horizontalVelocity = (initialHorizontalSpeed / ClientUpdateTicksPerSecond) + ((_game._visualRandom.NextSingle() * 0.6f) - 0.3f);
            var verticalVelocity = -0.8f - (_game._visualRandom.NextSingle() * 0.45f);
            _game._looseSheetVisuals.Add(new LooseSheetVisual(x, y, horizontalVelocity, verticalVelocity, ((_game._visualRandom.NextSingle() * 0.12f) - 0.06f) * MathF.PI, sheetSprites[_game._visualRandom.Next(sheetSprites.Length)]));
        }

        public bool IsShellBlocked(float x, float y)
        {
            foreach (var solid in _game._world.Level.Solids)
            {
                if (x >= solid.Left && x < solid.Right && y >= solid.Top && y < solid.Bottom)
                {
                    return true;
                }
            }

            foreach (var wall in _game._world.Level.GetRoomObjects(RoomObjectType.PlayerWall))
            {
                if (x >= wall.Left && x < wall.Right && y >= wall.Top && y < wall.Bottom)
                {
                    return true;
                }
            }

            return false;
        }

        public static float ScaleSourceTickDistance(float sourceDistance)
        {
            return sourceDistance * (LegacyMovementModel.SourceTicksPerSecond / (float)ClientUpdateTicksPerSecond);
        }

        private void SpawnPendingWeaponShellVisual(PendingWeaponShellVisual pendingShell)
        {
            var player = _game.FindPlayerById(pendingShell.PlayerId);
            if (player is null || !player.IsAlive)
            {
                return;
            }

            if (pendingShell.ClassId == PlayerClass.Spy && _game.GetPlayerVisibilityAlpha(player) <= 0.1f)
            {
                return;
            }

            for (var shellIndex = 0; shellIndex < pendingShell.Count; shellIndex += 1)
            {
                SpawnWeaponShellVisual(player, pendingShell.ClassId, pendingShell.Team);
            }
        }

        private void SpawnWeaponShellVisual(PlayerEntity player, PlayerClass classId, PlayerTeam team)
        {
            var spawnPosition = _game.GetWeaponShellSpawnOrigin(player);
            var facingScale = GetPlayerFacingScale(player);
            var aimRadians = MathF.PI * player.AimDirectionDegrees / 180f;
            var directionDegrees = player.AimDirectionDegrees;
            var frameIndex = 0;
            var speed = ScaleSourceTickDistance(2f + (_game._visualRandom.NextSingle() * 3f));
            var velocityOffsetX = 0f;
            var velocityOffsetY = 0f;

            switch (classId)
            {
                case PlayerClass.Heavy:
                    spawnPosition.Y += 4f;
                    directionDegrees += (140f - (_game._visualRandom.NextSingle() * 40f)) * facingScale;
                    break;
                case PlayerClass.Engineer:
                case PlayerClass.Scout:
                    frameIndex = 1;
                    directionDegrees += (140f - (_game._visualRandom.NextSingle() * 40f)) * facingScale;
                    break;
                case PlayerClass.Sniper:
                    frameIndex = 2;
                    directionDegrees += (100f + (_game._visualRandom.NextSingle() * 30f)) * facingScale;
                    velocityOffsetX -= ScaleSourceTickDistance(1f * facingScale);
                    velocityOffsetY -= ScaleSourceTickDistance(1f);
                    break;
                case PlayerClass.Medic:
                    frameIndex = team == PlayerTeam.Blue ? 4 : 3;
                    directionDegrees += (100f + (_game._visualRandom.NextSingle() * 30f)) * facingScale;
                    break;
                case PlayerClass.Spy:
                    spawnPosition.X += MathF.Cos(aimRadians) * 8f;
                    spawnPosition.Y += MathF.Sin(aimRadians) * 8f - 5f;
                    directionDegrees = 180f + player.AimDirectionDegrees + (70f - (_game._visualRandom.NextSingle() * 80f)) * facingScale;
                    speed *= 0.7f;
                    break;
                default:
                    return;
            }

            var directionRadians = directionDegrees * (MathF.PI / 180f);
            var rotationSpeed = ScaleSourceTickDistance(14f + (_game._visualRandom.NextSingle() * 18f)) * (_game._visualRandom.Next(2) == 0 ? -1f : 1f);
            _game._shellVisuals.Add(new ShellVisual(spawnPosition.X, spawnPosition.Y, (MathF.Cos(directionRadians) * speed) + velocityOffsetX, (MathF.Sin(directionRadians) * speed) + velocityOffsetY, frameIndex, _game._visualRandom.NextSingle() * 360f, rotationSpeed, fadeDelayTicks: (int)MathF.Round(GetSourceTicksAsSeconds(45f) * ClientUpdateTicksPerSecond)));
        }

        private void AdvanceLooseSheetAxis(ref float primaryCoordinate, float secondaryCoordinate, ref float velocity, bool horizontal)
        {
            if (MathF.Abs(velocity) <= 0.0001f)
            {
                velocity = 0f;
                return;
            }

            var remaining = velocity;
            while (MathF.Abs(remaining) > 0.0001f)
            {
                var step = MathF.Abs(remaining) > 1f ? MathF.Sign(remaining) : remaining;
                var nextPrimary = primaryCoordinate + step;
                var blocked = horizontal ? IsLooseSheetBlocked(nextPrimary, secondaryCoordinate) : IsLooseSheetBlocked(secondaryCoordinate, nextPrimary);
                if (blocked)
                {
                    velocity = horizontal ? velocity * -0.2f : 0f;
                    return;
                }

                primaryCoordinate = nextPrimary;
                remaining -= step;
            }
        }

        private bool IsLooseSheetBlocked(float x, float y)
        {
            foreach (var solid in _game._world.Level.Solids)
            {
                if (x >= solid.Left && x < solid.Right && y >= solid.Top && y < solid.Bottom)
                {
                    return true;
                }
            }

            foreach (var wall in _game._world.Level.GetRoomObjects(RoomObjectType.PlayerWall))
            {
                if (x >= wall.Left && x < wall.Right && y >= wall.Top && y < wall.Bottom)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsLooseSheetIgnited(float x, float y)
        {
            for (var index = 0; index < _game._world.Flames.Count; index += 1)
            {
                if (DistanceSquared(x, y, _game._world.Flames[index].X, _game._world.Flames[index].Y) <= 196f)
                {
                    return true;
                }
            }

            for (var index = 0; index < _game._world.Flares.Count; index += 1)
            {
                if (DistanceSquared(x, y, _game._world.Flares[index].X, _game._world.Flares[index].Y) <= 144f)
                {
                    return true;
                }
            }

            return false;
        }

        private static float DistanceSquared(float x1, float y1, float x2, float y2)
        {
            var deltaX = x2 - x1;
            var deltaY = y2 - y1;
            return (deltaX * deltaX) + (deltaY * deltaY);
        }
    }
}
