#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Core;

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class GameplayMaterialEffectsController
    {
        private const int BrowserMaxLooseSheetVisuals = 12;
        private const int BrowserLooseSheetLifetimeTicks = 90;
        private const int BrowserLooseSheetFadeTicks = 24;
        private const float BrowserLooseSheetSpawnChance = 0.4f;
        private const int MaxCivvieMoneySheetVisuals = 40;
        private const int CivvieMoneySheetLifetimeTicks = 90;
        private const int CivvieMoneySheetFadeTicks = 18;
        private const float CivvieMoneySheetDrawScale = 1.2f;
        private static readonly Color CivvieMoneySheetTint = new(0, 114, 3);
        private readonly IGameplayContext _context;
        private readonly List<OpenGarrison.Client.Game1.ShellVisual> _shellVisuals = new();
        private readonly List<OpenGarrison.Client.Game1.PendingWeaponShellVisual> _pendingWeaponShellVisuals = new();
        private readonly List<OpenGarrison.Client.Game1.LooseSheetVisual> _looseSheetVisuals = new();

        public GameplayMaterialEffectsController(IGameplayContext context)
        {
            _context = context;
        }

        internal int ShellVisualCount => _shellVisuals.Count;

        internal int LooseSheetVisualCount => _looseSheetVisuals.Count;

        internal int CivvieMoneySheetVisualCount
        {
            get
            {
                var count = 0;
                for (var index = 0; index < _looseSheetVisuals.Count; index += 1)
                {
                    if (_looseSheetVisuals[index].IsCivvieMoney)
                    {
                        count += 1;
                    }
                }

                return count;
            }
        }

        public void ResetTransientEffects()
        {
            _pendingWeaponShellVisuals.Clear();
            _shellVisuals.Clear();
            _looseSheetVisuals.Clear();
        }

        public void AdvanceLooseSheetVisuals()
        {
            for (var index = _looseSheetVisuals.Count - 1; index >= 0; index -= 1)
            {
                var sheet = _looseSheetVisuals[index];
                sheet.TicksRemaining -= 1;
                if (sheet.IsBurning)
                {
                    sheet.BurnTicksRemaining -= 1;
                    sheet.BurnAnimationTicks += 1;
                }

                if (sheet.TicksRemaining <= 0 || (sheet.IsBurning && sheet.BurnTicksRemaining <= 0))
                {
                    _looseSheetVisuals.RemoveAt(index);
                    continue;
                }

                var sheetX = sheet.X;
                var sheetY = sheet.Y;
                var velocityX = sheet.VelocityX;
                var velocityY = sheet.VelocityY;
                AdvanceLooseSheetAxis(ref sheetX, sheetY, ref velocityX, horizontal: true);
                AdvanceLooseSheetAxis(ref sheetY, sheetX, ref velocityY, horizontal: false);

                if (!sheet.IsBurning
                    && !_context.UseReducedBrowserEffects
                    && IsLooseSheetIgnited(sheetX, sheetY))
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
            if (_context.GameplayRuntimeSettings.ParticleMode != 0)
            {
                _pendingWeaponShellVisuals.Clear();
                _shellVisuals.Clear();
                return;
            }

            const float clientTickSeconds = 1f / ClientUpdateTicksPerSecond;
            for (var index = _pendingWeaponShellVisuals.Count - 1; index >= 0; index -= 1)
            {
                var pendingShell = _pendingWeaponShellVisuals[index];
                pendingShell.DelaySeconds -= clientTickSeconds;
                if (pendingShell.DelaySeconds > 0f)
                {
                    continue;
                }

                SpawnPendingWeaponShellVisual(pendingShell);
                _pendingWeaponShellVisuals.RemoveAt(index);
            }

            var baseGravityPerTick = ScaleSourceTickDistance(0.7f);
            var settleSpeed = ScaleSourceTickDistance(1f);
            for (var index = _shellVisuals.Count - 1; index >= 0; index -= 1)
            {
                var shell = _shellVisuals[index];
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
                    _shellVisuals.RemoveAt(index);
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
                    shell.VelocityY *= -shell.FloorBounceFactor;
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
                    shell.VelocityY += baseGravityPerTick * shell.GravityScale;
                }
            }
        }

        public void DrawLooseSheetVisuals(Vector2 cameraPosition)
        {
            for (var index = 0; index < _looseSheetVisuals.Count; index += 1)
            {
                var sheet = _looseSheetVisuals[index];
                var sprite = _context.GetResolvedSprite(sheet.SpriteName);
                var alpha = sheet.TicksRemaining <= sheet.FadeTicksRemaining
                    ? sheet.TicksRemaining / (float)sheet.FadeTicksRemaining
                    : 1f;
                if (sprite is not null && sprite.Frames.Count > 0)
                {
                    var frameIndex = sheet.IsBurning ? Math.Clamp(sheet.BurnAnimationTicks / 4, 0, sprite.Frames.Count - 1) : 0;
                    var tint = sheet.IsBurning ? Color.White : sheet.Tint;
                    _context.DrawLoadedSpriteFrame(
                        sprite.Frames[frameIndex],
                        new Vector2(sheet.X - cameraPosition.X, sheet.Y - cameraPosition.Y),
                        null,
                        tint * alpha,
                        sheet.RotationRadians,
                        sprite.Origin.ToVector2(),
                        new Vector2(sheet.DrawScale, sheet.DrawScale),
                        SpriteEffects.None,
                        0f);
                    continue;
                }

                var rectangle = new Rectangle((int)(sheet.X - 5f - cameraPosition.X), (int)(sheet.Y - 5f - cameraPosition.Y), 10, 10);
                _context._spriteBatch.Draw(_context._pixel, rectangle, new Color(230, 230, 220) * alpha);
            }
        }

        public void DrawShellVisuals(Vector2 cameraPosition)
        {
            if (_context.GameplayRuntimeSettings.ParticleMode != 0)
            {
                return;
            }

            for (var index = 0; index < _shellVisuals.Count; index += 1)
            {
                var shell = _shellVisuals[index];
                if (!shell.DrawAsPixel)
                {
                    var shellSprite = _context.GetResolvedSprite(shell.SpriteName ?? "ShellS");
                    if (shellSprite is not null && shellSprite.Frames.Count > 0)
                    {
                        var frameIndex = Math.Clamp(shell.FrameIndex, 0, shellSprite.Frames.Count - 1);
                        _context.DrawLoadedSpriteFrame(shellSprite.Frames[frameIndex], new Vector2(shell.X - cameraPosition.X, shell.Y - cameraPosition.Y), null, Color.White * shell.Alpha, MathHelper.ToRadians(shell.RotationDegrees), shellSprite.Origin.ToVector2(), Vector2.One, SpriteEffects.None, 0f);
                        continue;
                    }
                }

                var halfWidth = shell.PixelWidth * 0.5f;
                var halfHeight = shell.PixelHeight * 0.5f;
                var shellRectangle = new Rectangle(
                    (int)(shell.X - halfWidth - cameraPosition.X),
                    (int)(shell.Y - halfHeight - cameraPosition.Y),
                    shell.PixelWidth,
                    shell.PixelHeight);
                _context._spriteBatch.Draw(_context._pixel, shellRectangle, shell.Tint * shell.Alpha);
            }
        }

        public void SpawnBottleShardBurst(float x, float y, int teamCount, float burstDirectionDegrees = 270f)
        {
            if (_context.GameplayRuntimeSettings.ParticleMode != 0)
            {
                return;
            }

            var spriteName = teamCount >= (int)PlayerTeam.Blue
                ? "BlueStrongDrinkShardS"
                : "RedStrongDrinkShardS";
            var shardSprite = _context.GetResolvedSprite(spriteName);
            var frameCount = shardSprite is not null && shardSprite.Frames.Count > 0
                ? shardSprite.Frames.Count
                : 7;
            var fadeDelayTicks = (int)MathF.Round(GetSourceTicksAsSeconds(50f) * ClientUpdateTicksPerSecond);
            var burstRadians = burstDirectionDegrees * (MathF.PI / 180f);
            var burstNormalX = MathF.Cos(burstRadians);
            var burstNormalY = MathF.Sin(burstRadians);
            var upwardBias = ScaleSourceTickDistance(4.0f);
            for (var index = 0; index < frameCount; index += 1)
            {
                var angle = (_context._visualRandom.NextSingle() * MathF.PI * 2f) - MathF.PI;
                var speed = ScaleSourceTickDistance(5.0f + (_context._visualRandom.NextSingle() * 4.5f));
                var burstPush = ScaleSourceTickDistance(3.5f + (_context._visualRandom.NextSingle() * 3.0f));
                var velocityX = (MathF.Cos(angle) * speed) + (burstNormalX * burstPush);
                // Negative Y is up in screen space — always lift shards a bit for a cooler pop.
                var velocityY = (MathF.Sin(angle) * speed * 0.85f) + (burstNormalY * burstPush) - upwardBias;
                var rotationSpeed = ScaleSourceTickDistance(8f + (_context._visualRandom.NextSingle() * 12f))
                    * (_context._visualRandom.Next(2) == 0 ? -1f : 1f);
                _shellVisuals.Add(new ShellVisual(
                    x + ((_context._visualRandom.NextSingle() - 0.5f) * 3f),
                    y + ((_context._visualRandom.NextSingle() - 0.5f) * 3f),
                    velocityX,
                    velocityY,
                    frameIndex: index,
                    rotationDegrees: _context._visualRandom.NextSingle() * 360f,
                    rotationSpeedDegrees: rotationSpeed,
                    fadeDelayTicks: fadeDelayTicks,
                    spriteName: spriteName,
                    gravityScale: 0.45f,
                    floorBounceFactor: 0.5f));
            }
        }

        public void QueueWeaponShellVisual(PlayerEntity player, float delaySeconds, int count)
        {
            QueueWeaponShellVisual(player, delaySeconds, count, player.ClassId);
        }

        public void QueueWeaponShellVisual(PlayerEntity player, float delaySeconds, int count, PlayerClass classId)
        {
            if (_context.GameplayRuntimeSettings.ParticleMode != 0 || count <= 0)
            {
                return;
            }

            _pendingWeaponShellVisuals.Add(new PendingWeaponShellVisual(_context.GetPlayerStateKey(player), classId, player.Team, Math.Max(0f, delaySeconds), count));
        }

        public void QueueWeaponShellVisual(PlayerEntity player, float delaySeconds, int count, PlayerClass classId, string spriteName)
        {
            if (_context.GameplayRuntimeSettings.ParticleMode != 0 || count <= 0)
            {
                return;
            }

            _pendingWeaponShellVisuals.Add(new PendingWeaponShellVisual(_context.GetPlayerStateKey(player), classId, player.Team, Math.Max(0f, delaySeconds), count, spriteName));
        }

        internal void QueueResettingWeaponShellVisual(PlayerEntity player, float delaySeconds, int count)
        {
            if (_context.GameplayRuntimeSettings.ParticleMode != 0 || count <= 0)
            {
                return;
            }

            var playerStateKey = _context.GetPlayerStateKey(player);
            for (var pendingIndex = _pendingWeaponShellVisuals.Count - 1; pendingIndex >= 0; pendingIndex -= 1)
            {
                var pendingShell = _pendingWeaponShellVisuals[pendingIndex];
                if (pendingShell.PlayerId == playerStateKey
                    && pendingShell.ClassId == player.ClassId)
                {
                    _pendingWeaponShellVisuals.RemoveAt(pendingIndex);
                }
            }

            QueueWeaponShellVisual(player, delaySeconds, count);
        }

        internal void QueueResettingWeaponShellVisual(PlayerEntity player, float delaySeconds, int count, string spriteName)
        {
            if (_context.GameplayRuntimeSettings.ParticleMode != 0 || count <= 0)
            {
                return;
            }

            var playerStateKey = _context.GetPlayerStateKey(player);
            for (var pendingIndex = _pendingWeaponShellVisuals.Count - 1; pendingIndex >= 0; pendingIndex -= 1)
            {
                var pendingShell = _pendingWeaponShellVisuals[pendingIndex];
                if (pendingShell.PlayerId == playerStateKey
                    && pendingShell.ClassId == player.ClassId
                    && pendingShell.SpriteName == spriteName)
                {
                    _pendingWeaponShellVisuals.RemoveAt(pendingIndex);
                }
            }

            QueueWeaponShellVisual(player, delaySeconds, count, player.ClassId, spriteName);
        }

        public void SpawnCivvieMoneyVisual(CivvieMoneyTrailSpawn spawn)
        {
            if (!AreCivvieMoneyParticlesEnabled(_context.GameplayRuntimeSettings.ParticleMode))
            {
                return;
            }

            PruneCivvieMoneySheetVisuals();

            string[] sheetSprites = ["SheetFalling1", "SheetFalling2", "SheetFalling3"];
            var spriteIndex = CivvieMoneyTrailRules.GetDeterministicSpriteIndex(
                spawn.Frame,
                spawn.OwnerPlayerId,
                sheetSprites.Length);
            var horizontalVelocity = (spawn.HorizontalSpeed / ClientUpdateTicksPerSecond)
                + CivvieMoneyTrailRules.GetDeterministicSignedOffset(spawn.Frame, spawn.OwnerPlayerId, salt: 0x484F525A, magnitude: 0.3f);
            var verticalVelocity = -0.8f
                - (CivvieMoneyTrailRules.GetDeterministicUnitFloat(spawn.Frame, spawn.OwnerPlayerId, salt: 0x56454C59) * 0.45f);
            var rotationSpeed = CivvieMoneyTrailRules.GetDeterministicSignedOffset(
                spawn.Frame,
                spawn.OwnerPlayerId,
                salt: 0x524F5453,
                magnitude: 0.06f) * MathF.PI;
            _looseSheetVisuals.Add(new LooseSheetVisual(
                spawn.X,
                spawn.Y,
                horizontalVelocity,
                verticalVelocity,
                rotationSpeed,
                sheetSprites[spriteIndex],
                CivvieMoneySheetLifetimeTicks,
                CivvieMoneySheetFadeTicks,
                isCivvieMoney: true,
                CivvieMoneySheetTint,
                CivvieMoneySheetDrawScale));
        }

        public void SpawnCivvieMoneyBurstVisual(CivvieMoneyBurstSpawn spawn)
        {
            if (!AreCivvieMoneyParticlesEnabled(_context.GameplayRuntimeSettings.ParticleMode))
            {
                return;
            }

            PruneCivvieMoneySheetVisuals();

            string[] sheetSprites = ["SheetFalling1", "SheetFalling2", "SheetFalling3"];
            var spriteIndex = CivvieMoneyTrailRules.GetDeterministicSpriteIndex(
                spawn.Frame,
                spawn.OwnerPlayerId + spawn.ParticleIndex,
                sheetSprites.Length);
            var horizontalVelocity = spawn.VelocityX;
            var verticalVelocity = spawn.VelocityY;
            var rotationSpeed = CivvieMoneyTrailRules.GetDeterministicSignedOffset(
                spawn.Frame,
                spawn.OwnerPlayerId,
                salt: 0x42555254 ^ spawn.ParticleIndex,
                magnitude: 0.08f) * MathF.PI;
            _looseSheetVisuals.Add(new LooseSheetVisual(
                spawn.X,
                spawn.Y,
                horizontalVelocity,
                verticalVelocity,
                rotationSpeed,
                sheetSprites[spriteIndex],
                CivvieMoneySheetLifetimeTicks,
                CivvieMoneySheetFadeTicks,
                isCivvieMoney: true,
                CivvieMoneySheetTint,
                CivvieMoneySheetDrawScale));
        }

        public void SpawnLooseSheetVisual(float x, float y, float initialHorizontalSpeed, string? spriteName = null, bool isCivvieMoney = false)
        {
            string[] sheetSprites = ["SheetFalling1", "SheetFalling2", "SheetFalling3"];
            if (isCivvieMoney && !AreCivvieMoneyParticlesEnabled(_context.GameplayRuntimeSettings.ParticleMode))
            {
                return;
            }

            if (_context.UseReducedBrowserEffects)
            {
                if (_context._visualRandom.NextSingle() > BrowserLooseSheetSpawnChance)
                {
                    return;
                }

                while (_looseSheetVisuals.Count >= BrowserMaxLooseSheetVisuals)
                {
                    _looseSheetVisuals.RemoveAt(0);
                }
            }
            else if (isCivvieMoney)
            {
                PruneCivvieMoneySheetVisuals();
            }

            var horizontalVelocity = (initialHorizontalSpeed / ClientUpdateTicksPerSecond) + ((_context._visualRandom.NextSingle() * 0.6f) - 0.3f);
            var verticalVelocity = -0.8f - (_context._visualRandom.NextSingle() * 0.45f);
            var lifetimeTicks = _context.UseReducedBrowserEffects
                ? BrowserLooseSheetLifetimeTicks
                : isCivvieMoney
                    ? CivvieMoneySheetLifetimeTicks
                    : LooseSheetVisual.LifetimeTicks;
            var fadeTicks = _context.UseReducedBrowserEffects
                ? BrowserLooseSheetFadeTicks
                : isCivvieMoney
                    ? CivvieMoneySheetFadeTicks
                    : LooseSheetVisual.FadeTicks;
            _looseSheetVisuals.Add(new LooseSheetVisual(
                x,
                y,
                horizontalVelocity,
                verticalVelocity,
                ((_context._visualRandom.NextSingle() * 0.12f) - 0.06f) * MathF.PI,
                string.IsNullOrWhiteSpace(spriteName) || isCivvieMoney ? sheetSprites[_context._visualRandom.Next(sheetSprites.Length)] : spriteName,
                lifetimeTicks,
                fadeTicks,
                isCivvieMoney,
                isCivvieMoney ? CivvieMoneySheetTint : Color.White,
                isCivvieMoney ? CivvieMoneySheetDrawScale : 2f));
        }

        private void PruneCivvieMoneySheetVisuals()
        {
            var civvieMoneyCount = 0;
            for (var index = 0; index < _looseSheetVisuals.Count; index += 1)
            {
                if (_looseSheetVisuals[index].IsCivvieMoney)
                {
                    civvieMoneyCount += 1;
                }
            }

            while (civvieMoneyCount >= MaxCivvieMoneySheetVisuals)
            {
                var removed = false;
                for (var index = 0; index < _looseSheetVisuals.Count; index += 1)
                {
                    if (!_looseSheetVisuals[index].IsCivvieMoney)
                    {
                        continue;
                    }

                    _looseSheetVisuals.RemoveAt(index);
                    civvieMoneyCount -= 1;
                    removed = true;
                    break;
                }

                if (!removed)
                {
                    break;
                }
            }
        }

        public bool IsShellBlocked(float x, float y)
        {
            if (_context._world.Level.ContainsSolidPoint(x, y))
                return true;

            foreach (var wall in _context._world.Level.GetRoomObjects(RoomObjectType.PlayerWall))
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
            var player = _context.FindPlayerById(pendingShell.PlayerId);
            if (player is null || !player.IsAlive)
            {
                return;
            }

            if (pendingShell.ClassId == PlayerClass.Spy && _context.GetPlayerVisibilityAlpha(player) <= 0.1f)
            {
                return;
            }

            for (var shellIndex = 0; shellIndex < pendingShell.Count; shellIndex += 1)
            {
                SpawnWeaponShellVisual(player, pendingShell.ClassId, pendingShell.Team, pendingShell.SpriteName);
            }
        }

        private void SpawnWeaponShellVisual(PlayerEntity player, PlayerClass classId, PlayerTeam team, string? spriteName = null)
        {
            var spawnPosition = _context.GetWeaponShellSpawnOrigin(player);
            var facingScale = GetPlayerFacingScale(player);
            var aimRadians = MathF.PI * player.AimDirectionDegrees / 180f;
            var directionDegrees = player.AimDirectionDegrees;
            var frameIndex = 0;
            var speed = ScaleSourceTickDistance(2f + (_context._visualRandom.NextSingle() * 3f));
            var velocityOffsetX = 0f;
            var velocityOffsetY = 0f;

            if (spriteName == "NailgunMagS")
            {
                // Spawn at x=18, y=12 in weapon sprite space (weapon anchor at playerOrigin + (-10+9)*facingScale, playerOrigin+0)
                // → from player body origin: +17*facingScale horizontal, +12 vertical
                spawnPosition.X += 5f * facingScale;
                spawnPosition.Y += 8f;
                var velX = ScaleSourceTickDistance(-1.5f) * facingScale;
                var velY = -ScaleSourceTickDistance(1.5f);
                var rotSpeed = ScaleSourceTickDistance(6f + (_context._visualRandom.NextSingle() * 4f)) * (_context._visualRandom.Next(2) == 0 ? -1f : 1f);
                _shellVisuals.Add(new ShellVisual(spawnPosition.X, spawnPosition.Y, velX, velY, 0, _context._visualRandom.NextSingle() * 360f, rotSpeed, fadeDelayTicks: (int)MathF.Round(GetSourceTicksAsSeconds(45f) * ClientUpdateTicksPerSecond), spriteName: "NailgunMagS"));
                return;
            }

            switch (classId)
            {
                case PlayerClass.Heavy:
                    spawnPosition.Y += 4f;
                    directionDegrees += (140f - (_context._visualRandom.NextSingle() * 40f)) * facingScale;
                    break;
                case PlayerClass.Engineer:
                case PlayerClass.Scout:
                    frameIndex = 1;
                    directionDegrees += (140f - (_context._visualRandom.NextSingle() * 40f)) * facingScale;
                    break;
                case PlayerClass.Sniper:
                    frameIndex = 2;
                    directionDegrees += (100f + (_context._visualRandom.NextSingle() * 30f)) * facingScale;
                    velocityOffsetX -= ScaleSourceTickDistance(1f * facingScale);
                    velocityOffsetY -= ScaleSourceTickDistance(1f);
                    break;
                case PlayerClass.Medic:
                    frameIndex = team == PlayerTeam.Blue ? 4 : 3;
                    directionDegrees += (100f + (_context._visualRandom.NextSingle() * 30f)) * facingScale;
                    break;
                case PlayerClass.Spy:
                    spawnPosition.X += MathF.Cos(aimRadians) * 8f;
                    spawnPosition.Y += MathF.Sin(aimRadians) * 8f - 5f;
                    directionDegrees = 180f + player.AimDirectionDegrees + (70f - (_context._visualRandom.NextSingle() * 80f)) * facingScale;
                    speed *= 0.7f;
                    break;
                default:
                    return;
            }

            var directionRadians = directionDegrees * (MathF.PI / 180f);
            var rotationSpeed = ScaleSourceTickDistance(14f + (_context._visualRandom.NextSingle() * 18f)) * (_context._visualRandom.Next(2) == 0 ? -1f : 1f);
            _shellVisuals.Add(new ShellVisual(spawnPosition.X, spawnPosition.Y, (MathF.Cos(directionRadians) * speed) + velocityOffsetX, (MathF.Sin(directionRadians) * speed) + velocityOffsetY, frameIndex, _context._visualRandom.NextSingle() * 360f, rotationSpeed, fadeDelayTicks: (int)MathF.Round(GetSourceTicksAsSeconds(45f) * ClientUpdateTicksPerSecond)));
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
            if (_context._world.Level.ContainsSolidPoint(x, y))
                return true;

            foreach (var wall in _context._world.Level.GetRoomObjects(RoomObjectType.PlayerWall))
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
            for (var index = 0; index < _context._world.Flames.Count; index += 1)
            {
                if (DistanceSquared(x, y, _context._world.Flames[index].X, _context._world.Flames[index].Y) <= 196f)
                {
                    return true;
                }
            }

            for (var index = 0; index < _context._world.Flares.Count; index += 1)
            {
                if (DistanceSquared(x, y, _context._world.Flares[index].X, _context._world.Flares[index].Y) <= 144f)
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
