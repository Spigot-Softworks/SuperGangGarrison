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
    private readonly List<AirBlastVisual> _airBlasts = new();
    private readonly List<BackstabVisual> _backstabVisuals = new();
    private readonly List<BloodVisual> _bloodVisuals = new();
    private readonly List<RocketSmokeVisual> _rocketSmokeVisuals = new();
    private readonly List<BlastJumpFlameVisual> _blastJumpFlameVisuals = new();
    private readonly List<FlameSmokeVisual> _flameSmokeVisuals = new();
    private readonly List<SnapshotVisualEvent> _pendingNetworkVisualEvents = new();
    private readonly HashSet<ulong> _processedNetworkVisualEventIds = new();
    private readonly Queue<ulong> _processedNetworkVisualEventOrder = new();
    private int _nextClientBackstabVisualId = -1;

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

        for (var index = _explosions.Count - 1; index >= 0; index -= 1)
        {
            _explosions[index].TicksRemaining -= 1;
            if (_explosions[index].TicksRemaining <= 0)
            {
                _explosions.RemoveAt(index);
            }
        }
    }

    private void AdvanceBloodVisuals()
    {
        if (_gibLevel == 0)
        {
            _bloodVisuals.Clear();
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
            while (visual.PendingSourceTicks >= 1f && !visual.Animation.IsExpired)
            {
                visual.PendingSourceTicks -= 1f;
                var ownerX = visual.Animation.X;
                var ownerY = visual.Animation.Y;
                if (TryGetBackstabOwnerPosition(visual.Animation.OwnerId, out var ownerPosition))
                {
                    ownerX = ownerPosition.X;
                    ownerY = ownerPosition.Y;
                }

                visual.Animation.AdvanceOneTick(ownerX, ownerY);
            }

            if (visual.Animation.IsExpired)
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
            _blastJumpFlameVisuals.Clear();
            _flameSmokeVisuals.Clear();
            return;
        }

        foreach (var flame in _world.Flames)
        {
            var smokeChance = _particleMode == 2 ? 9 : 5;
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
            var alpha = 0.5f * (1f - progress);
            var radius = 3f + (progress * 6f);
            var color = new Color(160, 160, 160) * alpha;
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
            var alpha = 0.35f * (1f - progress);
            var radius = 2f + (progress * 4f);
            var color = new Color(160, 160, 160) * alpha;
            var smokeRectangle = new Rectangle(
                (int)(smoke.X - radius - cameraPosition.X),
                (int)(smoke.Y - radius - (progress * 6f) - cameraPosition.Y),
                (int)(radius * 2f),
                (int)(radius * 2f));
            _spriteBatch.Draw(_pixel, smokeRectangle, color);
        }
    }

    private void DrawExplosionVisuals(Vector2 cameraPosition)
    {
        DrawAirBlastVisuals(cameraPosition);
        var sprite = _runtimeAssets.GetSprite("ExplosionS");
        if (sprite is null || sprite.Frames.Count == 0)
        {
            return;
        }

        foreach (var explosion in _explosions)
        {
            var elapsedTicks = ExplosionVisual.LifetimeTicks - explosion.TicksRemaining;
            var frameIndex = Math.Clamp((int)MathF.Floor(elapsedTicks * sprite.Frames.Count / (float)ExplosionVisual.LifetimeTicks), 0, sprite.Frames.Count - 1);
            _spriteBatch.Draw(
                sprite.Frames[frameIndex],
                new Vector2(explosion.X - cameraPosition.X, explosion.Y - cameraPosition.Y),
                null,
                Color.White,
                0f,
                sprite.Origin.ToVector2(),
                new Vector2(2.2f, 2.2f),
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
            _explosions.Add(new ExplosionVisual(x, y));
            return;
        }

        if (string.Equals(effectName, "AirBlast", StringComparison.OrdinalIgnoreCase))
        {
            _airBlasts.Add(new AirBlastVisual(x, y, directionDegrees * (MathF.PI / 180f)));
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

        if (!string.Equals(effectName, "Blood", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (_gibLevel == 0)
        {
            return;
        }

        var burstCount = Math.Max(1, count);
        for (var index = 0; index < burstCount; index += 1)
        {
            var spreadDegrees = directionDegrees + (_visualRandom.NextSingle() * 43f) - 22f;
            var spreadRadians = spreadDegrees * (MathF.PI / 180f);
            var distance = burstCount > 1 ? _visualRandom.NextSingle() * 6f : 0f;
            _bloodVisuals.Add(new BloodVisual(
                x + MathF.Cos(spreadRadians) * distance,
                y + MathF.Sin(spreadRadians) * distance));
        }
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
        if (owner is null || !owner.IsAlive)
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

    private sealed class ExplosionVisual
    {
        public const int LifetimeTicks = 13;

        public ExplosionVisual(float x, float y)
        {
            X = x;
            Y = y;
            TicksRemaining = LifetimeTicks;
        }

        public float X { get; }

        public float Y { get; }

        public int TicksRemaining { get; set; }
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
        public const int LifetimeTicks = 10;

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
        public const int LifetimeTicks = 12;

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
}
