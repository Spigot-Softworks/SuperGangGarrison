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
        _explosions.Clear();
        _impactVisuals.Clear();
        _airBlasts.Clear();
        _bubblePops.Clear();
        ResetRetainedDeadBodies();
        ResetBackstabVisuals();
        _bloodVisuals.Clear();
        _bloodSprayVisuals.Clear();
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

    private static ExplosionVisual CreateExplosionVisual(float x, float y, int initialElapsedSourceTicks = 1)
    {
        var explosion = new ExplosionVisual(x, y)
        {
            ElapsedSourceTicks = Math.Clamp(initialElapsedSourceTicks, 0, ExplosionVisual.LifetimeSourceTicks - 1),
        };
        return explosion;
    }

    private bool TryCreateExplosionVisual(WorldSoundEvent soundEvent, out ExplosionVisual? explosion)
    {
        explosion = CreateExplosionVisual(soundEvent.X, soundEvent.Y);
        if (soundEvent.SourceFrame == 0)
        {
            return true;
        }

        var currentFrame = (ulong)Math.Max(0L, _world.Frame);
        if (currentFrame <= soundEvent.SourceFrame)
        {
            return true;
        }

        var elapsedSourceTicks = (currentFrame - soundEvent.SourceFrame)
            * (LegacyMovementModel.SourceTicksPerSecond / (float)_config.TicksPerSecond);
        if (elapsedSourceTicks >= ExplosionVisual.LifetimeSourceTicks)
        {
            explosion = null;
            return false;
        }

        explosion.ElapsedSourceTicks = Math.Clamp((int)MathF.Floor(elapsedSourceTicks), 0, ExplosionVisual.LifetimeSourceTicks - 1);
        explosion.PendingSourceTicks = Math.Clamp(elapsedSourceTicks - explosion.ElapsedSourceTicks, 0f, 1f);
        return true;
    }

    private void AdvanceExplosionVisuals()
    {
        for (var index = _airBlasts.Count - 1; index >= 0; index -= 1)
        {
            _airBlasts[index].TicksRemaining -= 1;
            if (_airBlasts[index].TicksRemaining <= 0)
            {
                _airBlasts.RemoveAt(index);
            }
        }

        var sourceTickAdvance = _clientUpdateElapsedSeconds * LegacyMovementModel.SourceTicksPerSecond;
        if (sourceTickAdvance <= 0f)
        {
            return;
        }

        for (var index = _bubblePops.Count - 1; index >= 0; index -= 1)
        {
            var bubblePop = _bubblePops[index];
            bubblePop.PendingSourceTicks += sourceTickAdvance;
            while (bubblePop.PendingSourceTicks >= 1f && bubblePop.ElapsedSourceTicks < BubblePopVisual.LifetimeSourceTicks)
            {
                bubblePop.PendingSourceTicks -= 1f;
                bubblePop.ElapsedSourceTicks += 1;
            }

            if (bubblePop.ElapsedSourceTicks >= BubblePopVisual.LifetimeSourceTicks)
            {
                _bubblePops.RemoveAt(index);
            }
        }

        for (var index = _explosions.Count - 1; index >= 0; index -= 1)
        {
            var explosion = _explosions[index];
            explosion.PendingSourceTicks += sourceTickAdvance;
            while (explosion.PendingSourceTicks >= 1f && explosion.ElapsedSourceTicks < ExplosionVisual.LifetimeSourceTicks)
            {
                explosion.PendingSourceTicks -= 1f;
                explosion.ElapsedSourceTicks += 1;
            }

            if (explosion.ElapsedSourceTicks >= ExplosionVisual.LifetimeSourceTicks)
            {
                _explosions.RemoveAt(index);
            }
        }
    }

    private void AdvanceImpactVisuals()
    {
        var sourceTickAdvance = _clientUpdateElapsedSeconds * LegacyMovementModel.SourceTicksPerSecond;
        if (sourceTickAdvance <= 0f)
        {
            return;
        }

        for (var index = _impactVisuals.Count - 1; index >= 0; index -= 1)
        {
            var impact = _impactVisuals[index];
            impact.PendingSourceTicks += sourceTickAdvance;
            while (impact.PendingSourceTicks >= 1f && impact.ElapsedSourceTicks < ImpactVisual.LifetimeSourceTicks)
            {
                impact.PendingSourceTicks -= 1f;
                impact.ElapsedSourceTicks += 1;
            }

            if (impact.ElapsedSourceTicks >= ImpactVisual.LifetimeSourceTicks)
            {
                _impactVisuals.RemoveAt(index);
            }
        }
    }

    private void AdvanceLooseSheetVisuals()
    {
        for (var index = _looseSheetVisuals.Count - 1; index >= 0; index -= 1)
        {
            var sheet = _looseSheetVisuals[index];
            sheet.TicksRemaining -= 1;
            if (sheet.TicksRemaining <= 0)
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

            if (!IsLooseSheetBlocked(sheetX, sheetY + 1f))
            {
                velocityY = MathF.Min(1.4f, velocityY + 0.035f);
            }
            else
            {
                velocityX *= 0.95f;
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
        if (_gibLevel == 0)
        {
            _bloodVisuals.Clear();
            _bloodSprayVisuals.Clear();
            return;
        }

        for (var index = _bloodVisuals.Count - 1; index >= 0; index -= 1)
        {
            _bloodVisuals[index].TicksRemaining -= 1;
            if (_bloodVisuals[index].TicksRemaining <= 0)
            {
                _bloodVisuals.RemoveAt(index);
            }
        }

        for (var index = _bloodSprayVisuals.Count - 1; index >= 0; index -= 1)
        {
            var spray = _bloodSprayVisuals[index];
            spray.TicksRemaining -= 1;
            if (spray.TicksRemaining <= 0)
            {
                _bloodSprayVisuals.RemoveAt(index);
                continue;
            }

            spray.VelocityY = MathF.Min(BloodDropEntity.MaxSpeed, spray.VelocityY + 0.35f);
            spray.X += spray.VelocityX;
            spray.Y += spray.VelocityY;
            spray.VelocityX *= 0.97f;
        }
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
        if (_backstabVisuals.Count == 0)
        {
            return;
        }

        var sourceTickAdvance = _clientUpdateElapsedSeconds * LegacyMovementModel.SourceTicksPerSecond;
        if (sourceTickAdvance <= 0f)
        {
            return;
        }

        for (var index = _backstabVisuals.Count - 1; index >= 0; index -= 1)
        {
            var visual = _backstabVisuals[index];
            visual.PendingSourceTicks += sourceTickAdvance;
            var removeVisual = false;
            while (visual.PendingSourceTicks >= 1f && !visual.Animation.IsExpired)
            {
                visual.PendingSourceTicks -= 1f;
                if (!TryGetBackstabOwnerPosition(visual.Animation.OwnerId, out var ownerPosition))
                {
                    removeVisual = true;
                    break;
                }

                visual.Animation.AdvanceOneTick(ownerPosition.X, ownerPosition.Y);
            }

            if (removeVisual || visual.Animation.IsExpired)
            {
                _backstabVisuals.RemoveAt(index);
            }
        }
    }

    private void DrawBackstabVisuals(Vector2 cameraPosition)
    {
        for (var index = 0; index < _backstabVisuals.Count; index += 1)
        {
            DrawStabAnimation(_backstabVisuals[index].Animation, cameraPosition);
        }
    }

    private void AdvanceRocketSmokeVisuals()
    {
        if (_particleMode == 1)
        {
            _rocketSmokeVisuals.Clear();
            return;
        }

        foreach (var rocket in _world.Rockets)
        {
            if (_particleMode == 2 && ((_world.Frame + rocket.Id) & 1) != 0)
            {
                continue;
            }

            var velocityX = rocket.X - rocket.PreviousX;
            var velocityY = rocket.Y - rocket.PreviousY;
            if (MathF.Abs(velocityX) <= 0.001f && MathF.Abs(velocityY) <= 0.001f)
            {
                continue;
            }

            _rocketSmokeVisuals.Add(new RocketSmokeVisual(
                rocket.X - (velocityX * 1.3f),
                rocket.Y - (velocityY * 1.3f)));
            if (_particleMode == 0)
            {
                _rocketSmokeVisuals.Add(new RocketSmokeVisual(
                    rocket.X - (velocityX * 0.75f),
                    rocket.Y - (velocityY * 0.75f)));
            }
        }

        for (var index = _rocketSmokeVisuals.Count - 1; index >= 0; index -= 1)
        {
            _rocketSmokeVisuals[index].TicksRemaining -= 1;
            if (_rocketSmokeVisuals[index].TicksRemaining <= 0)
            {
                _rocketSmokeVisuals.RemoveAt(index);
            }
        }
    }

    private void AdvanceFlameSmokeVisuals()
    {
        if (_particleMode == 1)
        {
            _mineTrailVisuals.Clear();
            _wallspinDustVisuals.Clear();
            _blastJumpFlameVisuals.Clear();
            _flameSmokeVisuals.Clear();
            return;
        }

        foreach (var flame in _world.Flames)
        {
            var smokeChance = _particleMode == 2 ? 4 : 2;
            if (flame.IsAttached || _visualRandom.Next(smokeChance) != 0)
            {
                continue;
            }

            _flameSmokeVisuals.Add(new FlameSmokeVisual(flame.X, flame.Y - 8f));
        }

        foreach (var player in EnumerateRenderablePlayers())
        {
            if (!player.IsAlive || !IsBlastJumpVisualState(player.MovementState))
            {
                continue;
            }

            var renderPosition = GetRenderPosition(player, allowInterpolation: !ReferenceEquals(player, _world.LocalPlayer));
            if (_visualRandom.NextSingle() < GetBlastJumpFlameProbability())
            {
                _blastJumpFlameVisuals.Add(new BlastJumpFlameVisual(
                    renderPosition.X,
                    renderPosition.Y + (player.Height * 0.5f) + 1f,
                    _visualRandom.Next(GetBlastJumpFlameMinimumLifetimeTicks(), GetBlastJumpFlameMaximumLifetimeTicks() + 1),
                    _visualRandom.Next()));
            }

            if (_particleMode != 0)
            {
                continue;
            }

            var smokeProbability = GetBlastJumpSmokeProbability(player);
            if (_visualRandom.NextSingle() >= smokeProbability)
            {
                continue;
            }

            var smokeX = renderPosition.X - (player.HorizontalSpeed * (float)_config.FixedDeltaSeconds * 1.2f);
            var smokeY = renderPosition.Y - (player.VerticalSpeed * (float)_config.FixedDeltaSeconds * 1.2f) + (player.Height * 0.5f) + 2f;
            _flameSmokeVisuals.Add(new FlameSmokeVisual(smokeX, smokeY));
        }

        AdvanceWallspinDustVisuals();

        for (var index = _blastJumpFlameVisuals.Count - 1; index >= 0; index -= 1)
        {
            _blastJumpFlameVisuals[index].TicksRemaining -= 1;
            if (_blastJumpFlameVisuals[index].TicksRemaining <= 0)
            {
                _blastJumpFlameVisuals.RemoveAt(index);
            }
        }

        for (var index = _flameSmokeVisuals.Count - 1; index >= 0; index -= 1)
        {
            _flameSmokeVisuals[index].TicksRemaining -= 1;
            if (_flameSmokeVisuals[index].TicksRemaining <= 0)
            {
                _flameSmokeVisuals.RemoveAt(index);
            }
        }
    }

    private void AdvanceWallspinDustVisuals()
    {
        for (var index = _wallspinDustVisuals.Count - 1; index >= 0; index -= 1)
        {
            _wallspinDustVisuals[index].TicksRemaining -= 1;
            if (_wallspinDustVisuals[index].TicksRemaining <= 0)
            {
                _wallspinDustVisuals.RemoveAt(index);
            }
        }
    }

    private void SpawnWallspinDustVisual(float x, float y, int emissionTicks = 1)
    {
        for (var emissionIndex = 0; emissionIndex < emissionTicks; emissionIndex += 1)
        {
            _wallspinDustVisuals.Add(new WallspinDustVisual(
                x,
                y,
                _visualRandom.Next(GetWallspinDustMinimumLifetimeTicks(), GetWallspinDustMaximumLifetimeTicks() + 1)));
        }
    }

    private void AdvanceMineTrailVisuals()
    {
        if (_particleMode == 1)
        {
            _mineTrailVisuals.Clear();
            return;
        }

        foreach (var mine in _world.Mines)
        {
            if (mine.IsStickied || (_particleMode == 2 && ((_world.Frame + mine.Id) & 1) != 0))
            {
                continue;
            }

            var velocityX = mine.X - mine.PreviousX;
            var velocityY = mine.Y - mine.PreviousY;
            if (MathF.Abs(velocityX) <= 0.001f && MathF.Abs(velocityY) <= 0.001f)
            {
                continue;
            }

            _mineTrailVisuals.Add(new MineTrailVisual(mine.X, mine.Y));
        }

        for (var index = _mineTrailVisuals.Count - 1; index >= 0; index -= 1)
        {
            _mineTrailVisuals[index].TicksRemaining -= 1;
            if (_mineTrailVisuals[index].TicksRemaining <= 0)
            {
                _mineTrailVisuals.RemoveAt(index);
            }
        }
    }

    private void DrawBlastJumpFlameVisuals(Vector2 cameraPosition)
    {
        var sprite = _runtimeAssets.GetSprite("FlameS");
        for (var index = 0; index < _blastJumpFlameVisuals.Count; index += 1)
        {
            var flame = _blastJumpFlameVisuals[index];
            var progress = 1f - (flame.TicksRemaining / (float)flame.InitialTicks);
            var alpha = 1f - (progress * 0.7f);
            var scale = MathF.Max(0.25f, 0.7f - (progress * 0.35f));
            if (sprite is not null && sprite.Frames.Count > 0)
            {
                var frameIndex = Math.Abs(flame.FrameSeed + ((int)(_world.Frame + index))) % sprite.Frames.Count;
                _spriteBatch.Draw(
                    sprite.Frames[frameIndex],
                    new Vector2(flame.X - cameraPosition.X, flame.Y - cameraPosition.Y),
                    null,
                    Color.White * alpha,
                    0f,
                    sprite.Origin.ToVector2(),
                    new Vector2(scale, scale),
                    SpriteEffects.None,
                    0f);
                continue;
            }

            var flameSize = Math.Max(2, (int)MathF.Round(8f * scale));
            var flameRectangle = new Rectangle(
                (int)(flame.X - (flameSize / 2f) - cameraPosition.X),
                (int)(flame.Y - (flameSize / 2f) - cameraPosition.Y),
                flameSize,
                flameSize);
            _spriteBatch.Draw(_pixel, flameRectangle, new Color(255, 170, 90) * alpha);
        }
    }

    private void DrawRocketSmokeVisuals(Vector2 cameraPosition)
    {
        for (var index = 0; index < _rocketSmokeVisuals.Count; index += 1)
        {
            var smoke = _rocketSmokeVisuals[index];
            var progress = 1f - (smoke.TicksRemaining / (float)RocketSmokeVisual.LifetimeTicks);
            var alpha = 0.7f * (1f - progress);
            var radius = 4f + (progress * 8f);
            var color = new Color(176, 176, 176) * alpha;
            var smokeRectangle = new Rectangle(
                (int)(smoke.X - radius - cameraPosition.X),
                (int)(smoke.Y - radius - cameraPosition.Y),
                (int)(radius * 2f),
                (int)(radius * 2f));
            _spriteBatch.Draw(_pixel, smokeRectangle, color);
        }
    }

    private void DrawFlameSmokeVisuals(Vector2 cameraPosition)
    {
        for (var index = 0; index < _flameSmokeVisuals.Count; index += 1)
        {
            var smoke = _flameSmokeVisuals[index];
            var progress = 1f - (smoke.TicksRemaining / (float)FlameSmokeVisual.LifetimeTicks);
            var alpha = 0.55f * (1f - progress);
            var radius = 3f + (progress * 6f);
            var color = new Color(160, 160, 160) * alpha;
            var smokeRectangle = new Rectangle(
                (int)(smoke.X - radius - cameraPosition.X),
                (int)(smoke.Y - radius - (progress * 9f) - cameraPosition.Y),
                (int)(radius * 2f),
                (int)(radius * 2f));
            _spriteBatch.Draw(_pixel, smokeRectangle, color);
        }
    }

    private void DrawExplosionVisuals(Vector2 cameraPosition)
    {
        DrawAirBlastVisuals(cameraPosition);
        DrawBubblePopVisuals(cameraPosition);
        DrawFallbackExplosionVisuals(cameraPosition);
        var largeSprite = _runtimeAssets.GetSprite("ExplosionS");
        var smallSprite = _runtimeAssets.GetSprite("ExplosionSmallS");
        if ((largeSprite is null || largeSprite.Frames.Count == 0)
            && (smallSprite is null || smallSprite.Frames.Count == 0))
        {
            return;
        }

        foreach (var explosion in _explosions)
        {
            DrawExplosionSprite(explosion, cameraPosition, largeSprite, 2.2f, 0.92f, startingFrameBias: 3);
            DrawExplosionSprite(explosion, cameraPosition, smallSprite, 1.45f, 0.78f, startingFrameBias: 2);
        }
    }

    private void DrawExplosionSprite(
        ExplosionVisual explosion,
        Vector2 cameraPosition,
        LoadedGameMakerSprite? sprite,
        float scale,
        float alpha,
        int startingFrameBias)
    {
        if (sprite is null || sprite.Frames.Count == 0)
        {
            return;
        }

        var rawFrameIndex = explosion.ElapsedSourceTicks == 0
            ? Math.Min(startingFrameBias, sprite.Frames.Count - 1)
            : (int)MathF.Floor(explosion.ElapsedSourceTicks * sprite.Frames.Count / (float)ExplosionVisual.LifetimeSourceTicks);
        var frameIndex = Math.Clamp(rawFrameIndex, 0, sprite.Frames.Count - 1);
        _spriteBatch.Draw(
            sprite.Frames[frameIndex],
            new Vector2(explosion.X - cameraPosition.X, explosion.Y - cameraPosition.Y),
            null,
            Color.White * alpha,
            0f,
            sprite.Origin.ToVector2(),
            new Vector2(scale, scale),
            SpriteEffects.None,
            0f);
    }

    private void DrawImpactVisuals(Vector2 cameraPosition)
    {
        var sprite = _runtimeAssets.GetSprite("ImpactS");
        if (sprite is null || sprite.Frames.Count == 0)
        {
            return;
        }

        for (var index = 0; index < _impactVisuals.Count; index += 1)
        {
            var impact = _impactVisuals[index];
            var secondStage = impact.ElapsedSourceTicks >= (ImpactVisual.LifetimeSourceTicks / 2);
            var alpha = secondStage ? 0.5f : 1f;
            var scale = secondStage ? 1f : 0.5f;
            _spriteBatch.Draw(
                sprite.Frames[0],
                new Vector2(impact.X - cameraPosition.X, impact.Y - cameraPosition.Y),
                null,
                Color.White * alpha,
                impact.RotationRadians,
                sprite.Origin.ToVector2(),
                new Vector2(scale, scale),
                SpriteEffects.None,
                0f);
        }
    }

    private void DrawFallbackExplosionVisuals(Vector2 cameraPosition)
    {
        foreach (var explosion in _explosions)
        {
            var progress = explosion.ElapsedSourceTicks / (float)ExplosionVisual.LifetimeSourceTicks;
            var radius = 12f + (progress * 18f);
            var innerRadius = radius * 0.5f;
            var alpha = MathHelper.Clamp(1f - progress, 0f, 1f);
            var outerRectangle = new Rectangle(
                (int)MathF.Round(explosion.X - cameraPosition.X - radius),
                (int)MathF.Round(explosion.Y - cameraPosition.Y - radius),
                (int)MathF.Round(radius * 2f),
                (int)MathF.Round(radius * 2f));
            var innerRectangle = new Rectangle(
                (int)MathF.Round(explosion.X - cameraPosition.X - innerRadius),
                (int)MathF.Round(explosion.Y - cameraPosition.Y - innerRadius),
                (int)MathF.Round(innerRadius * 2f),
                (int)MathF.Round(innerRadius * 2f));
            _spriteBatch.Draw(_pixel, outerRectangle, new Color(255, 182, 68) * alpha);
            _spriteBatch.Draw(_pixel, innerRectangle, new Color(255, 240, 180) * alpha);
        }
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
                _spriteBatch.Draw(
                    sprite.Frames[0],
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

    private void DrawBubblePopVisuals(Vector2 cameraPosition)
    {
        var sprite = _runtimeAssets.GetSprite("PopS");
        if (sprite is null || sprite.Frames.Count == 0)
        {
            return;
        }

        foreach (var bubblePop in _bubblePops)
        {
            var frameIndex = Math.Clamp(
                (int)MathF.Floor(bubblePop.ElapsedSourceTicks * sprite.Frames.Count / (float)BubblePopVisual.LifetimeSourceTicks),
                0,
                sprite.Frames.Count - 1);
            _spriteBatch.Draw(
                sprite.Frames[frameIndex],
                new Vector2(bubblePop.X - cameraPosition.X, bubblePop.Y - cameraPosition.Y),
                null,
                Color.White,
                0f,
                sprite.Origin.ToVector2(),
                Vector2.One,
                SpriteEffects.None,
                0f);
        }
    }

    private void DrawAirBlastVisuals(Vector2 cameraPosition)
    {
        var sprite = _runtimeAssets.GetSprite("AirBlastS");
        if (sprite is null || sprite.Frames.Count == 0)
        {
            return;
        }

        foreach (var airBlast in _airBlasts)
        {
            var elapsedTicks = AirBlastVisual.LifetimeTicks - airBlast.TicksRemaining;
            var frameIndex = Math.Clamp((int)MathF.Floor(elapsedTicks * sprite.Frames.Count / (float)AirBlastVisual.LifetimeTicks), 0, sprite.Frames.Count - 1);
            _spriteBatch.Draw(
                sprite.Frames[frameIndex],
                new Vector2(airBlast.X - cameraPosition.X, airBlast.Y - cameraPosition.Y),
                null,
                Color.White,
                airBlast.RotationRadians,
                sprite.Origin.ToVector2(),
                Vector2.One,
                SpriteEffects.None,
                0f);
        }
    }

    private void DrawBloodVisuals(Vector2 cameraPosition)
    {
        if (_gibLevel == 0)
        {
            return;
        }

        var sprite = _runtimeAssets.GetSprite("BloodS");
        if (sprite is null || sprite.Frames.Count == 0)
        {
            return;
        }

        foreach (var blood in _bloodVisuals)
        {
            var elapsedTicks = BloodVisual.LifetimeTicks - blood.TicksRemaining;
            var frameIndex = Math.Clamp(elapsedTicks, 0, sprite.Frames.Count - 1);
            var scale = elapsedTicks < 2 ? 0.5f : 1f;
            var alpha = elapsedTicks < 2 ? 1f : 0.5f;
            _spriteBatch.Draw(
                sprite.Frames[frameIndex],
                new Vector2(blood.X - cameraPosition.X, blood.Y - cameraPosition.Y),
                null,
                Color.White * alpha,
                0f,
                sprite.Origin.ToVector2(),
                new Vector2(scale, scale),
                SpriteEffects.None,
                0f);
        }

        var bloodDropSprite = _runtimeAssets.GetSprite("BloodDropS");
        for (var index = 0; index < _bloodSprayVisuals.Count; index += 1)
        {
            var spray = _bloodSprayVisuals[index];
            var alpha = Math.Clamp(spray.TicksRemaining / (float)spray.InitialTicks, 0f, 1f);
            if (bloodDropSprite is not null && bloodDropSprite.Frames.Count > 0)
            {
                _spriteBatch.Draw(
                    bloodDropSprite.Frames[0],
                    new Vector2(spray.X - cameraPosition.X, spray.Y - cameraPosition.Y),
                    null,
                    Color.White * alpha,
                    0f,
                    bloodDropSprite.Origin.ToVector2(),
                    Vector2.One,
                    SpriteEffects.None,
                    0f);
                continue;
            }

            var rectangle = new Rectangle(
                (int)(spray.X - cameraPosition.X),
                (int)(spray.Y - cameraPosition.Y),
                2,
                2);
            _spriteBatch.Draw(_pixel, rectangle, Color.White * alpha);
        }
    }

    private void DrawMineTrailVisuals(Vector2 cameraPosition)
    {
        var sprite = _runtimeAssets.GetSprite("MineTrailS");
        if (sprite is null || sprite.Frames.Count == 0)
        {
            return;
        }

        for (var index = 0; index < _mineTrailVisuals.Count; index += 1)
        {
            var trail = _mineTrailVisuals[index];
            var progress = 1f - (trail.TicksRemaining / (float)MineTrailVisual.LifetimeTicks);
            var frameIndex = Math.Clamp((int)MathF.Floor(progress * sprite.Frames.Count), 0, sprite.Frames.Count - 1);
            _spriteBatch.Draw(
                sprite.Frames[frameIndex],
                new Vector2(trail.X - cameraPosition.X, trail.Y - cameraPosition.Y),
                null,
                Color.White,
                0f,
                sprite.Origin.ToVector2(),
                Vector2.One,
                SpriteEffects.None,
                0f);
        }
    }

    private void DrawWallspinDustVisuals(Vector2 cameraPosition)
    {
        var sprite = _runtimeAssets.GetSprite("SpeedBoostS");
        if (sprite is null || sprite.Frames.Count == 0)
        {
            return;
        }

        for (var index = 0; index < _wallspinDustVisuals.Count; index += 1)
        {
            var dust = _wallspinDustVisuals[index];
            var progress = 1f - (dust.TicksRemaining / (float)dust.TotalLifetimeTicks);
            var alpha = progress < 0.5f
                ? MathHelper.Lerp(0.7f, 0.5f, progress * 2f)
                : MathHelper.Lerp(0.5f, 0f, (progress - 0.5f) * 2f);
            _spriteBatch.Draw(
                sprite.Frames[0],
                new Vector2(dust.X - cameraPosition.X, dust.Y - cameraPosition.Y),
                null,
                Color.White * alpha,
                -MathHelper.PiOver2,
                sprite.Origin.ToVector2(),
                Vector2.One,
                SpriteEffects.None,
                0f);
        }
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
        if (_particleMode != 0 || count <= 0)
        {
            return;
        }

        _pendingWeaponShellVisuals.Add(new PendingWeaponShellVisual(
            GetPlayerStateKey(player),
            player.ClassId,
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
        if (string.Equals(effectName, "Explosion", StringComparison.OrdinalIgnoreCase))
        {
            _explosions.Add(CreateExplosionVisual(x, y));
            return;
        }

        if (string.Equals(effectName, "Impact", StringComparison.OrdinalIgnoreCase))
        {
            _impactVisuals.Add(new ImpactVisual(x, y, directionDegrees * (MathF.PI / 180f)));
            return;
        }

        if (string.Equals(effectName, "AirBlast", StringComparison.OrdinalIgnoreCase))
        {
            _airBlasts.Add(new AirBlastVisual(x, y, directionDegrees * (MathF.PI / 180f)));
            return;
        }

        if (string.Equals(effectName, "Pop", StringComparison.OrdinalIgnoreCase))
        {
            _bubblePops.Add(new BubblePopVisual(x, y));
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

        if (string.Equals(effectName, "BackstabBlue", StringComparison.OrdinalIgnoreCase))
        {
            SpawnBackstabVisual(ownerId: count, PlayerTeam.Blue, x, y, directionDegrees);
            return;
        }

        if (string.Equals(effectName, "BackstabRed", StringComparison.OrdinalIgnoreCase))
        {
            SpawnBackstabVisual(ownerId: count, PlayerTeam.Red, x, y, directionDegrees);
            return;
        }

        if (string.Equals(effectName, "GibBlood", StringComparison.OrdinalIgnoreCase))
        {
            if (_gibLevel == 0)
            {
                return;
            }

            SpawnGibBloodImpactVisuals(x, y, Math.Max(1, count));
            return;
        }

        if (!string.Equals(effectName, "Blood", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (_gibLevel == 0)
        {
            return;
        }

        SpawnBloodImpactVisuals(x, y, directionDegrees, Math.Max(1, count));
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
        var normalizedDirection = NormalizeDirectionDegrees(directionDegrees);
        for (var index = 0; index < _backstabVisuals.Count; index += 1)
        {
            var animation = _backstabVisuals[index].Animation;
            if (ownerId != 0 && animation.OwnerId == ownerId)
            {
                return;
            }

            if (animation.Team != team)
            {
                continue;
            }

            if (DistanceSquared(animation.X, animation.Y, x, y) > 16f)
            {
                continue;
            }

            if (GetAngleDifferenceDegrees(animation.DirectionDegrees, normalizedDirection) > 8f)
            {
                continue;
            }

            return;
        }

        _backstabVisuals.Add(new BackstabVisual(
            new StabAnimEntity(_nextClientBackstabVisualId--, ownerId, team, x, y, normalizedDirection)));
    }

    private bool TryGetBackstabOwnerPosition(int ownerId, out Vector2 ownerPosition)
    {
        if (ownerId == 0)
        {
            ownerPosition = default;
            return false;
        }

        var owner = FindPlayerById(ownerId);
        if (owner is null || !owner.IsAlive || owner.ClassId != PlayerClass.Spy)
        {
            ownerPosition = default;
            return false;
        }

        ownerPosition = GetRenderPosition(owner, allowInterpolation: !ReferenceEquals(owner, _world.LocalPlayer));
        return true;
    }

    private void ResetBackstabVisuals()
    {
        _backstabVisuals.Clear();
        _nextClientBackstabVisualId = -1;
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

    private void SpawnBloodImpactVisuals(float x, float y, float directionDegrees, int burstCount)
    {
        for (var index = 0; index < burstCount; index += 1)
        {
            var spreadDegrees = directionDegrees + (_visualRandom.NextSingle() * 57f) - 28f;
            var spreadRadians = spreadDegrees * (MathF.PI / 180f);
            var distance = burstCount > 1 ? _visualRandom.NextSingle() * 8f : 0f;
            _bloodVisuals.Add(new BloodVisual(
                x + MathF.Cos(spreadRadians) * distance,
                y + MathF.Sin(spreadRadians) * distance));
        }

        var sprayCount = Math.Clamp((burstCount * 2) + 4, 6, 14);
        for (var index = 0; index < sprayCount; index += 1)
        {
            var spreadDegrees = directionDegrees + (_visualRandom.NextSingle() * 57f) - 28f;
            var spreadRadians = spreadDegrees * (MathF.PI / 180f);
            var speed = 4f + (_visualRandom.NextSingle() * 14f);
            _bloodSprayVisuals.Add(new BloodSprayVisual(
                x,
                y,
                MathF.Cos(spreadRadians) * speed,
                MathF.Sin(spreadRadians) * speed,
                _visualRandom.Next(24, 47)));
        }
    }

    private void SpawnGibBloodImpactVisuals(float x, float y, int intensity)
    {
        var burstCount = Math.Max(6, intensity * 4);
        for (var index = 0; index < burstCount; index += 1)
        {
            var directionRadians = _visualRandom.NextSingle() * MathF.Tau;
            var distance = _visualRandom.NextSingle() * 11f;
            _bloodVisuals.Add(new BloodVisual(
                x + MathF.Cos(directionRadians) * distance,
                y + MathF.Sin(directionRadians) * distance));
        }

        var sprayCount = Math.Max(14, intensity * 10);
        for (var index = 0; index < sprayCount; index += 1)
        {
            var directionRadians = _visualRandom.NextSingle() * MathF.Tau;
            var speed = 6f + (_visualRandom.NextSingle() * 16f);
            var startRadius = _visualRandom.NextSingle() * 8f;
            _bloodSprayVisuals.Add(new BloodSprayVisual(
                x + MathF.Cos(directionRadians) * startRadius,
                y + MathF.Sin(directionRadians) * startRadius,
                MathF.Cos(directionRadians) * speed,
                MathF.Sin(directionRadians) * speed,
                _visualRandom.Next(28, 57)));
        }
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

        public string SpriteName { get; }

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
