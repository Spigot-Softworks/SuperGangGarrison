#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using OpenGarrison.Core;
using OpenGarrison.Protocol;

namespace OpenGarrison.Client;

public partial class Game1
{
    private readonly List<ExplosionVisual> _explosions = new();
    private readonly List<ImpactVisual> _impactVisuals = new();
    private readonly List<AirBlastVisual> _airBlasts = new();
    private readonly List<BubblePopVisual> _bubblePops = new();
    private readonly List<BackstabVisual> _backstabVisuals = new();
    private readonly List<BloodVisual> _bloodVisuals = new();
    private readonly List<BloodSprayVisual> _bloodSprayVisuals = new();
    private readonly Dictionary<int, StickyGibBloodCoating> _stickyGibBloodCoatings = new();
    private readonly List<int> _staleStickyGibBloodPlayerIds = new();
    private readonly HashSet<int> _processedStickyGibBloodDropIds = new();
    private readonly List<int> _staleStickyGibBloodDropIds = new();
    private readonly List<PendingWeaponShellVisual> _pendingWeaponShellVisuals = new();
    private readonly List<ShellVisual> _shellVisuals = new();
    private readonly List<RocketSmokeVisual> _rocketSmokeVisuals = new();
    private readonly List<MineTrailVisual> _mineTrailVisuals = new();
    private readonly List<WallspinDustVisual> _wallspinDustVisuals = new();
    private readonly List<BlastJumpFlameVisual> _blastJumpFlameVisuals = new();
    private readonly List<FlameSmokeVisual> _flameSmokeVisuals = new();
    private readonly List<LooseSheetVisual> _looseSheetVisuals = new();
    private readonly List<SnapshotVisualEvent> _pendingNetworkVisualEvents = new();
    private readonly HashSet<ulong> _processedNetworkVisualEventIds = new();
    private readonly Queue<ulong> _processedNetworkVisualEventOrder = new();
    private int _nextClientBackstabVisualId = -1;

    private void ResetTransientPresentationEffects()
    {
        _gameplayImpactEffectsController.ResetTransientEffects();
        ResetRetainedDeadBodies();
        _gameplayGoreEffectsController.ResetTransientEffects();
        ResetExperimentalHealingHudIndicators();
        _pendingWeaponShellVisuals.Clear();
        _shellVisuals.Clear();
        _rocketSmokeVisuals.Clear();
        _mineTrailVisuals.Clear();
        _wallspinDustVisuals.Clear();
        _blastJumpFlameVisuals.Clear();
        _flameSmokeVisuals.Clear();
        _looseSheetVisuals.Clear();
        _pendingNetworkVisualEvents.Clear();
        _pendingNetworkDamageEvents.Clear();
    }

    private bool TryCreateExplosionVisual(WorldSoundEvent soundEvent, out ExplosionVisual? explosion)
    {
        return _gameplayImpactEffectsController.TryCreateExplosionVisual(soundEvent, out explosion);
    }

    private void AdvanceExplosionVisuals()
    {
        _gameplayImpactEffectsController.AdvanceExplosionVisuals();
    }

    private void AdvanceImpactVisuals()
    {
        _gameplayImpactEffectsController.AdvanceImpactVisuals();
    }

    private void AdvanceLooseSheetVisuals()
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

            if (sheet.TicksRemaining <= 0)
            {
                _looseSheetVisuals.RemoveAt(index);
                continue;
            }

            if (sheet.IsBurning && sheet.BurnTicksRemaining <= 0)
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

    private void AdvanceBloodVisuals()
    {
        _gameplayGoreEffectsController.AdvanceBloodVisuals();
    }

    private void AdvanceShellVisuals()
    {
        if (_particleMode != 0)
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

        var gravityPerTick = ScaleSourceTickDistance(0.7f);
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

    private void AdvanceBackstabVisuals()
    {
        _gameplayGoreEffectsController.AdvanceBackstabVisuals();
    }

    private void DrawBackstabVisuals(Vector2 cameraPosition)
    {
        _gameplayGoreEffectsController.DrawBackstabVisuals(cameraPosition);
    }

    private void AdvanceRocketSmokeVisuals()
    {
        _gameplaySmokeEffectsController.AdvanceRocketSmokeVisuals();
    }

    private void AdvanceFlameSmokeVisuals()
    {
        _gameplaySmokeEffectsController.AdvanceFlameSmokeVisuals();
    }

    private void AdvanceWallspinDustVisuals()
    {
        _gameplaySmokeEffectsController.AdvanceWallspinDustVisuals();
    }

    private void SpawnWallspinDustVisual(float x, float y, int emissionTicks = 1)
    {
        _gameplaySmokeEffectsController.SpawnWallspinDustVisual(x, y, emissionTicks);
    }

    private void AdvanceMineTrailVisuals()
    {
        _gameplaySmokeEffectsController.AdvanceMineTrailVisuals();
    }

    private void DrawBlastJumpFlameVisuals(Vector2 cameraPosition)
    {
        _gameplaySmokeEffectsController.DrawBlastJumpFlameVisuals(cameraPosition);
    }

    private void DrawRocketSmokeVisuals(Vector2 cameraPosition)
    {
        _gameplaySmokeEffectsController.DrawRocketSmokeVisuals(cameraPosition);
    }

    private void DrawFlameSmokeVisuals(Vector2 cameraPosition)
    {
        _gameplaySmokeEffectsController.DrawFlameSmokeVisuals(cameraPosition);
    }

    private void DrawExplosionVisuals(Vector2 cameraPosition)
    {
        _gameplayImpactEffectsController.DrawExplosionVisuals(cameraPosition);
    }

    private void DrawImpactVisuals(Vector2 cameraPosition)
    {
        _gameplayImpactEffectsController.DrawImpactVisuals(cameraPosition);
    }

    private void DrawLooseSheetVisuals(Vector2 cameraPosition)
    {
        for (var index = 0; index < _looseSheetVisuals.Count; index += 1)
        {
            var sheet = _looseSheetVisuals[index];
            var sprite = _runtimeAssets.GetSprite(sheet.SpriteName);
            var alpha = sheet.TicksRemaining <= LooseSheetVisual.FadeTicks
                ? sheet.TicksRemaining / (float)LooseSheetVisual.FadeTicks
                : 1f;
            if (sprite is not null && sprite.Frames.Count > 0)
            {
                var frameIndex = 0;
                if (sheet.IsBurning)
                {
                    frameIndex = Math.Clamp(sheet.BurnAnimationTicks / 4, 0, sprite.Frames.Count - 1);
                }

                _spriteBatch.Draw(
                    sprite.Frames[frameIndex],
                    new Vector2(sheet.X - cameraPosition.X, sheet.Y - cameraPosition.Y),
                    null,
                    Color.White * alpha,
                    sheet.RotationRadians,
                    sprite.Origin.ToVector2(),
                    new Vector2(2f, 2f),
                    SpriteEffects.None,
                    0f);
                continue;
            }

            var rectangle = new Rectangle(
                (int)(sheet.X - 5f - cameraPosition.X),
                (int)(sheet.Y - 5f - cameraPosition.Y),
                10,
                10);
            _spriteBatch.Draw(_pixel, rectangle, new Color(230, 230, 220) * alpha);
        }
    }

    private void DrawBloodVisuals(Vector2 cameraPosition)
    {
        _gameplayGoreEffectsController.DrawBloodVisuals(cameraPosition);
    }

    private void DrawMineTrailVisuals(Vector2 cameraPosition)
    {
        _gameplaySmokeEffectsController.DrawMineTrailVisuals(cameraPosition);
    }

    private void DrawWallspinDustVisuals(Vector2 cameraPosition)
    {
        _gameplaySmokeEffectsController.DrawWallspinDustVisuals(cameraPosition);
    }

    private void DrawShellVisuals(Vector2 cameraPosition)
    {
        if (_particleMode != 0)
        {
            return;
        }

        var shellSprite = _runtimeAssets.GetSprite("ShellS");
        for (var index = 0; index < _shellVisuals.Count; index += 1)
        {
            var shell = _shellVisuals[index];
            if (shellSprite is not null && shellSprite.Frames.Count > 0)
            {
                var frameIndex = Math.Clamp(shell.FrameIndex, 0, shellSprite.Frames.Count - 1);
                _spriteBatch.Draw(
                    shellSprite.Frames[frameIndex],
                    new Vector2(shell.X - cameraPosition.X, shell.Y - cameraPosition.Y),
                    null,
                    Color.White * shell.Alpha,
                    MathHelper.ToRadians(shell.RotationDegrees),
                    shellSprite.Origin.ToVector2(),
                    Vector2.One,
                    SpriteEffects.None,
                    0f);
                continue;
            }

            var shellRectangle = new Rectangle(
                (int)(shell.X - 2f - cameraPosition.X),
                (int)(shell.Y - 2f - cameraPosition.Y),
                4,
                4);
            _spriteBatch.Draw(_pixel, shellRectangle, new Color(230, 210, 160) * shell.Alpha);
        }
    }

    private void QueueWeaponShellVisual(PlayerEntity player, float delaySeconds, int count)
    {
        QueueWeaponShellVisual(player, delaySeconds, count, player.ClassId);
    }

    private void QueueWeaponShellVisual(PlayerEntity player, float delaySeconds, int count, PlayerClass classId)
    {
        if (_particleMode != 0 || count <= 0)
        {
            return;
        }

        _pendingWeaponShellVisuals.Add(new PendingWeaponShellVisual(
            GetPlayerStateKey(player),
            classId,
            player.Team,
            Math.Max(0f, delaySeconds),
            count));
    }

    private void SpawnPendingWeaponShellVisual(PendingWeaponShellVisual pendingShell)
    {
        var player = FindPlayerById(pendingShell.PlayerId);
        if (player is null || !player.IsAlive)
        {
            return;
        }

        if (pendingShell.ClassId == PlayerClass.Spy && GetPlayerVisibilityAlpha(player) <= 0.1f)
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
        var spawnPosition = GetWeaponShellSpawnOrigin(player);
        var facingScale = GetPlayerFacingScale(player);
        var aimRadians = MathF.PI * player.AimDirectionDegrees / 180f;
        var directionDegrees = player.AimDirectionDegrees;
        var frameIndex = 0;
        var speed = ScaleSourceTickDistance(2f + (_visualRandom.NextSingle() * 3f));
        var velocityOffsetX = 0f;
        var velocityOffsetY = 0f;

        switch (classId)
        {
            case PlayerClass.Heavy:
                spawnPosition.Y += 4f;
                directionDegrees += (140f - (_visualRandom.NextSingle() * 40f)) * facingScale;
                break;
            case PlayerClass.Engineer:
            case PlayerClass.Scout:
                frameIndex = 1;
                directionDegrees += (140f - (_visualRandom.NextSingle() * 40f)) * facingScale;
                break;
            case PlayerClass.Sniper:
                frameIndex = 2;
                directionDegrees += (100f + (_visualRandom.NextSingle() * 30f)) * facingScale;
                velocityOffsetX -= ScaleSourceTickDistance(1f * facingScale);
                velocityOffsetY -= ScaleSourceTickDistance(1f);
                break;
            case PlayerClass.Medic:
                frameIndex = team == PlayerTeam.Blue ? 4 : 3;
                directionDegrees += (100f + (_visualRandom.NextSingle() * 30f)) * facingScale;
                break;
            case PlayerClass.Spy:
                spawnPosition.X += MathF.Cos(aimRadians) * 8f;
                spawnPosition.Y += MathF.Sin(aimRadians) * 8f - 5f;
                directionDegrees = 180f + player.AimDirectionDegrees + (70f - (_visualRandom.NextSingle() * 80f)) * facingScale;
                speed *= 0.7f;
                break;
            default:
                return;
        }

        var directionRadians = directionDegrees * (MathF.PI / 180f);
        var rotationSpeed = ScaleSourceTickDistance(14f + (_visualRandom.NextSingle() * 18f))
            * (_visualRandom.Next(2) == 0 ? -1f : 1f);
        _shellVisuals.Add(new ShellVisual(
            spawnPosition.X,
            spawnPosition.Y,
            (MathF.Cos(directionRadians) * speed) + velocityOffsetX,
            (MathF.Sin(directionRadians) * speed) + velocityOffsetY,
            frameIndex,
            _visualRandom.NextSingle() * 360f,
            rotationSpeed,
            fadeDelayTicks: (int)MathF.Round(GetSourceTicksAsSeconds(45f) * ClientUpdateTicksPerSecond)));
    }

    private bool IsShellBlocked(float x, float y)
    {
        foreach (var solid in _world.Level.Solids)
        {
            if (x >= solid.Left && x < solid.Right && y >= solid.Top && y < solid.Bottom)
            {
                return true;
            }
        }

        foreach (var wall in _world.Level.GetRoomObjects(RoomObjectType.PlayerWall))
        {
            if (x >= wall.Left && x < wall.Right && y >= wall.Top && y < wall.Bottom)
            {
                return true;
            }
        }

        return false;
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
            var blocked = horizontal
                ? IsLooseSheetBlocked(nextPrimary, secondaryCoordinate)
                : IsLooseSheetBlocked(secondaryCoordinate, nextPrimary);
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
        foreach (var solid in _world.Level.Solids)
        {
            if (x >= solid.Left && x < solid.Right && y >= solid.Top && y < solid.Bottom)
            {
                return true;
            }
        }

        foreach (var wall in _world.Level.GetRoomObjects(RoomObjectType.PlayerWall))
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
        for (var index = 0; index < _world.Flames.Count; index += 1)
        {
            if (DistanceSquared(x, y, _world.Flames[index].X, _world.Flames[index].Y) <= 196f)
            {
                return true;
            }
        }

        for (var index = 0; index < _world.Flares.Count; index += 1)
        {
            if (DistanceSquared(x, y, _world.Flares[index].X, _world.Flares[index].Y) <= 144f)
            {
                return true;
            }
        }

        return false;
    }

    private static float ScaleSourceTickDistance(float sourceDistance)
    {
        return sourceDistance * (LegacyMovementModel.SourceTicksPerSecond / (float)ClientUpdateTicksPerSecond);
    }

    private void PlayPendingVisualEvents()
    {
        foreach (var visualEvent in _world.DrainPendingVisualEvents())
        {
            PlayVisualEvent(visualEvent.EffectName, visualEvent.X, visualEvent.Y, visualEvent.DirectionDegrees, visualEvent.Count);
        }

        foreach (var visualEvent in _pendingNetworkVisualEvents)
        {
            PlayVisualEvent(visualEvent.EffectName, visualEvent.X, visualEvent.Y, visualEvent.DirectionDegrees, visualEvent.Count);
        }

        _pendingNetworkVisualEvents.Clear();
    }

    private void PlayVisualEvent(string effectName, float x, float y, float directionDegrees, int count)
    {
        if (_gameplayImpactEffectsController.TryPlayVisualEvent(effectName, x, y, directionDegrees, count))
        {
            return;
        }

        if (_gameplayGoreEffectsController.TryPlayVisualEvent(effectName, x, y, directionDegrees, count))
        {
            return;
        }

        if (string.Equals(effectName, "WallspinDust", StringComparison.OrdinalIgnoreCase))
        {
            SpawnWallspinDustVisual(x, y);
            return;
        }

        if (string.Equals(effectName, "LooseSheet", StringComparison.OrdinalIgnoreCase))
        {
            SpawnLooseSheetVisual(x, y, directionDegrees);
            return;
        }

        return;
    }

    private void SpawnLooseSheetVisual(float x, float y, float initialHorizontalSpeed)
    {
        string[] sheetSprites = ["SheetFalling1", "SheetFalling2", "SheetFalling3"];
        var horizontalVelocity = (initialHorizontalSpeed / ClientUpdateTicksPerSecond) + ((_visualRandom.NextSingle() * 0.6f) - 0.3f);
        var verticalVelocity = -0.8f - (_visualRandom.NextSingle() * 0.45f);
        _looseSheetVisuals.Add(new LooseSheetVisual(
            x,
            y,
            horizontalVelocity,
            verticalVelocity,
            ((_visualRandom.NextSingle() * 0.12f) - 0.06f) * MathF.PI,
            sheetSprites[_visualRandom.Next(sheetSprites.Length)]));
    }

    private void SpawnBackstabVisual(int ownerId, PlayerTeam team, float x, float y, float directionDegrees)
    {
        _gameplayGoreEffectsController.SpawnBackstabVisual(ownerId, team, x, y, directionDegrees);
    }

    private void ResetBackstabVisuals()
    {
        _gameplayGoreEffectsController.ResetBackstabVisuals();
    }

    private static float NormalizeDirectionDegrees(float directionDegrees)
    {
        while (directionDegrees < 0f)
        {
            directionDegrees += 360f;
        }

        while (directionDegrees >= 360f)
        {
            directionDegrees -= 360f;
        }

        return directionDegrees;
    }

    private static float GetAngleDifferenceDegrees(float left, float right)
    {
        var difference = MathF.Abs(NormalizeDirectionDegrees(left) - NormalizeDirectionDegrees(right));
        return MathF.Min(difference, 360f - difference);
    }

    private static float DistanceSquared(float x1, float y1, float x2, float y2)
    {
        var deltaX = x2 - x1;
        var deltaY = y2 - y1;
        return (deltaX * deltaX) + (deltaY * deltaY);
    }

    private static bool IsBlastJumpVisualState(LegacyMovementState movementState)
    {
        return movementState == LegacyMovementState.ExplosionRecovery
            || movementState == LegacyMovementState.RocketJuggle
            || movementState == LegacyMovementState.FriendlyJuggle;
    }

    private static float GetBlastJumpSmokeProbability(PlayerEntity player)
    {
        var sourceTickProbability = player.MovementState switch
        {
            LegacyMovementState.ExplosionRecovery => 0.175f,
            LegacyMovementState.RocketJuggle => 0.25f,
            LegacyMovementState.FriendlyJuggle => float.Clamp(1f - ((player.RunPower + 1f) * 0.5f), 0f, 1f),
            _ => 0f,
        };
        return sourceTickProbability * (LegacyMovementModel.SourceTicksPerSecond / ClientUpdateTicksPerSecond);
    }

    private static float GetBlastJumpFlameProbability()
    {
        return (5f / 8f) * (LegacyMovementModel.SourceTicksPerSecond / ClientUpdateTicksPerSecond);
    }

    private static int GetBlastJumpFlameMinimumLifetimeTicks()
    {
        return (int)MathF.Ceiling(2f * ClientUpdateTicksPerSecond / LegacyMovementModel.SourceTicksPerSecond);
    }

    private static int GetBlastJumpFlameMaximumLifetimeTicks()
    {
        return (int)MathF.Ceiling(5f * ClientUpdateTicksPerSecond / LegacyMovementModel.SourceTicksPerSecond);
    }

    private static int GetWallspinDustMinimumLifetimeTicks()
    {
        return (int)MathF.Ceiling(GetSourceTicksAsSeconds(15f) * ClientUpdateTicksPerSecond);
    }

    private static int GetWallspinDustMaximumLifetimeTicks()
    {
        return (int)MathF.Ceiling(GetSourceTicksAsSeconds(30f) * ClientUpdateTicksPerSecond);
    }

    private void DrawExperimentalStickyGibBloodOverlay(PlayerEntity player, Vector2 cameraPosition, float visibilityAlpha)
    {
        _gameplayGoreEffectsController.DrawExperimentalStickyGibBloodOverlay(player, cameraPosition, visibilityAlpha);
    }

    private sealed class ExplosionVisual
    {
        public const int LifetimeSourceTicks = 13;

        public ExplosionVisual(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float X { get; }

        public float Y { get; }

        public int ElapsedSourceTicks { get; set; }

        public float PendingSourceTicks { get; set; }

        public Color LargeSpriteColor { get; set; } = Color.White;

        public Color SmallSpriteColor { get; set; } = Color.White;

        public Color FallbackOuterColor { get; set; } = new(255, 182, 68);

        public Color FallbackInnerColor { get; set; } = new(255, 240, 180);

        public float LargeScaleMultiplier { get; set; } = 1f;

        public float SmallScaleMultiplier { get; set; } = 1f;
    }

    private sealed class BubblePopVisual
    {
        public const int LifetimeSourceTicks = 2;

        public BubblePopVisual(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float X { get; }

        public float Y { get; }

        public int ElapsedSourceTicks { get; set; }

        public float PendingSourceTicks { get; set; }
    }

    private sealed class ImpactVisual
    {
        public const int LifetimeSourceTicks = 4;

        public ImpactVisual(float x, float y, float rotationRadians)
        {
            X = x;
            Y = y;
            RotationRadians = rotationRadians;
        }

        public float X { get; }

        public float Y { get; }

        public float RotationRadians { get; }

        public int ElapsedSourceTicks { get; set; }

        public float PendingSourceTicks { get; set; }
    }

    private sealed class BackstabVisual
    {
        public BackstabVisual(StabAnimEntity animation)
        {
            Animation = animation;
        }

        public StabAnimEntity Animation { get; }

        public float PendingSourceTicks { get; set; }
    }

    private sealed class AirBlastVisual
    {
        public const int LifetimeTicks = 8;

        public AirBlastVisual(float x, float y, float rotationRadians)
        {
            X = x;
            Y = y;
            RotationRadians = rotationRadians;
            TicksRemaining = LifetimeTicks;
        }

        public float X { get; }

        public float Y { get; }

        public float RotationRadians { get; }

        public int TicksRemaining { get; set; }
    }

    private sealed class BloodVisual
    {
        public const int LifetimeTicks = 4;

        public BloodVisual(float x, float y)
        {
            X = x;
            Y = y;
            TicksRemaining = LifetimeTicks;
        }

        public float X { get; }

        public float Y { get; }

        public int TicksRemaining { get; set; }
    }

    private sealed class FlameSmokeVisual
    {
        public const int LifetimeTicks = 14;

        public FlameSmokeVisual(float x, float y)
        {
            X = x;
            Y = y;
            TicksRemaining = LifetimeTicks;
        }

        public float X { get; }

        public float Y { get; }

        public int TicksRemaining { get; set; }
    }

    private sealed class LooseSheetVisual
    {
        public const int LifetimeTicks = 260;
        public const int FadeTicks = 60;
        public const int BurnLifetimeTicks = 24;

        public LooseSheetVisual(float x, float y, float velocityX, float velocityY, float rotationSpeedRadians, string spriteName)
        {
            X = x;
            Y = y;
            VelocityX = velocityX;
            VelocityY = velocityY;
            RotationSpeedRadians = rotationSpeedRadians;
            SpriteName = spriteName;
            TicksRemaining = LifetimeTicks;
        }

        public float X { get; set; }

        public float Y { get; set; }

        public float VelocityX { get; set; }

        public float VelocityY { get; set; }

        public float RotationRadians { get; set; }

        public float RotationSpeedRadians { get; }

        public string SpriteName { get; set; }

        public bool IsBurning { get; set; }

        public int BurnTicksRemaining { get; set; }

        public int BurnAnimationTicks { get; set; }

        public int TicksRemaining { get; set; }
    }

    private sealed class StickyGibBloodCoating
    {
        public const int LifetimeTicks = 10 * ClientUpdateTicksPerSecond;
        public const int FadeTicks = 2 * ClientUpdateTicksPerSecond;

        public float Intensity { get; set; }

        public int TicksRemaining { get; set; }
    }

    private sealed class WallspinDustVisual
    {
        public WallspinDustVisual(float x, float y, int totalLifetimeTicks)
        {
            X = x;
            Y = y;
            TotalLifetimeTicks = Math.Max(1, totalLifetimeTicks);
            TicksRemaining = TotalLifetimeTicks;
        }

        public float X { get; }

        public float Y { get; }

        public int TotalLifetimeTicks { get; }

        public int TicksRemaining { get; set; }
    }

    private sealed class BloodSprayVisual
    {
        public BloodSprayVisual(float x, float y, float velocityX, float velocityY, int initialTicks)
        {
            X = x;
            Y = y;
            VelocityX = velocityX;
            VelocityY = velocityY;
            InitialTicks = Math.Max(1, initialTicks);
            TicksRemaining = InitialTicks;
        }

        public float X { get; set; }

        public float Y { get; set; }

        public float VelocityX { get; set; }

        public float VelocityY { get; set; }

        public int InitialTicks { get; }

        public int TicksRemaining { get; set; }
    }

    private sealed class PendingWeaponShellVisual
    {
        public PendingWeaponShellVisual(int playerId, PlayerClass classId, PlayerTeam team, float delaySeconds, int count)
        {
            PlayerId = playerId;
            ClassId = classId;
            Team = team;
            DelaySeconds = delaySeconds;
            Count = count;
        }

        public int PlayerId { get; }

        public PlayerClass ClassId { get; }

        public PlayerTeam Team { get; }

        public float DelaySeconds { get; set; }

        public int Count { get; }
    }

    private sealed class ShellVisual
    {
        public ShellVisual(float x, float y, float velocityX, float velocityY, int frameIndex, float rotationDegrees, float rotationSpeedDegrees, int fadeDelayTicks)
        {
            X = x;
            Y = y;
            VelocityX = velocityX;
            VelocityY = velocityY;
            FrameIndex = frameIndex;
            RotationDegrees = rotationDegrees;
            RotationSpeedDegrees = rotationSpeedDegrees;
            TicksUntilFade = fadeDelayTicks;
        }

        public float X { get; set; }

        public float Y { get; set; }

        public float VelocityX { get; set; }

        public float VelocityY { get; set; }

        public int FrameIndex { get; }

        public float RotationDegrees { get; set; }

        public float RotationSpeedDegrees { get; set; }

        public int TicksUntilFade { get; set; }

        public bool Fade { get; set; }

        public bool Stuck { get; set; }

        public float Alpha { get; set; } = 1f;
    }

    private sealed class BlastJumpFlameVisual
    {
        public BlastJumpFlameVisual(float x, float y, int initialTicks, int frameSeed)
        {
            X = x;
            Y = y;
            InitialTicks = Math.Max(1, initialTicks);
            TicksRemaining = InitialTicks;
            FrameSeed = frameSeed;
        }

        public float X { get; }

        public float Y { get; }

        public int InitialTicks { get; }

        public int TicksRemaining { get; set; }

        public int FrameSeed { get; }
    }

    private sealed class RocketSmokeVisual
    {
        public const int LifetimeTicks = 16;

        public RocketSmokeVisual(float x, float y)
        {
            X = x;
            Y = y;
            TicksRemaining = LifetimeTicks;
        }

        public float X { get; }

        public float Y { get; }

        public int TicksRemaining { get; set; }
    }

    private sealed class MineTrailVisual
    {
        public const int LifetimeTicks = 10;

        public MineTrailVisual(float x, float y)
        {
            X = x;
            Y = y;
            TicksRemaining = LifetimeTicks;
        }

        public float X { get; }

        public float Y { get; }

        public int TicksRemaining { get; set; }
    }
}
