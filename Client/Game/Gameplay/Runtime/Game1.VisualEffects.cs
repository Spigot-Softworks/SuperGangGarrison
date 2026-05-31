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
    private readonly Dictionary<int, FrozenSpyFrameState> _lastVisibleEnemySpyFrameStates = new();
    private readonly List<FrozenSpyVisual> _frozenSpyVisuals = new();
    private readonly Dictionary<int, FrozenSpyFrameState> _lastHeavyDashFrameStates = new();
    private readonly Dictionary<int, int> _heavyDashTrailTickCounters = new();
    private readonly Dictionary<int, Vector2> _heavyDashTrailLastSpawnPositions = new();
    private readonly List<SniperTracerParticle> _sniperTracerParticles = new();
    private float _medigunBeamHelixPhase;

    private readonly record struct FrozenSpyFrameState(
        string SpriteName,
        int FrameIndex,
        Vector2 RenderPosition,
        Vector2 Scale,
        Vector2 Origin,
        float BodyYOffset,
        Color Tint,
        bool DrawIntelOverlay);

    private sealed class FrozenSpyVisual
    {
        public FrozenSpyVisual(int playerId, FrozenSpyFrameState frameState, int lifetimeTicks)
        {
            PlayerId = playerId;
            FrameState = frameState;
            TicksRemaining = lifetimeTicks;
            LifetimeTicks = lifetimeTicks;
        }

        public int PlayerId { get; }
        public FrozenSpyFrameState FrameState { get; }
        public int TicksRemaining { get; set; }
        public int LifetimeTicks { get; }
    }

    private void AdvanceMedigunBeamHelixPhase()
    {
        _medigunBeamHelixPhase -= 0.08f;
        if (_medigunBeamHelixPhase < 0f)
            _medigunBeamHelixPhase += MathF.PI * 2f;
    }
    private readonly List<MineTrailVisual> _mineTrailVisuals = new();
    private readonly List<WallspinDustVisual> _wallspinDustVisuals = new();
    private readonly List<BlastJumpFlameVisual> _blastJumpFlameVisuals = new();
    private readonly List<FlameSmokeVisual> _flameSmokeVisuals = new();
    private readonly List<FlameSmokeVisual> _flameSmokeSecondaryVisuals = new();
    private readonly List<LooseSheetVisual> _looseSheetVisuals = new();
    private readonly List<SnapshotVisualEvent> _pendingNetworkVisualEvents = new();
    private readonly HashSet<ulong> _processedNetworkVisualEventIds = new();
    private readonly Queue<ulong> _processedNetworkVisualEventOrder = new();
    private readonly List<PresentedExplosionVisual> _presentedExplosionVisualsThisFrame = new();
    private int _nextClientBackstabVisualId = -1;
    private int _spySuperjumpTrajectoryAnimationTicks;

    private readonly record struct PresentedExplosionVisual(float X, float Y);

    private void ResetTransientPresentationEffects()
    {
        _gameplayImpactEffectsController.ResetTransientEffects();
        ResetRetainedDeadBodies();
        ResetImmediateNetworkDeadBodies();
        _gameplayGoreEffectsController.ResetTransientEffects();
        ResetPendingBrowserSoundEvents();
        ResetRecentGibSoundEvents();
        ResetRecentProjectileSoundEvents();
        _pendingNetworkSoundEvents.Clear();
        ResetExperimentalHealingHudIndicators();
        _portraitRumbleRemainingSeconds = 0f;
        _portraitRumbleIntensity = 0f;
        _damageVignetteIntensity = 0f;
        _damageVignetteFlashIntensity = 0f;
        _gameplayMaterialEffectsController.ResetTransientEffects();
        _rocketSmokeVisuals.Clear();
        _mineTrailVisuals.Clear();
        _wallspinDustVisuals.Clear();
        _blastJumpFlameVisuals.Clear();
        _flameSmokeVisuals.Clear();
        _flameSmokeSecondaryVisuals.Clear();
        _pendingNetworkVisualEvents.Clear();
        _pendingNetworkDamageEvents.Clear();
        _frozenSpyVisuals.Clear();
        _lastVisibleEnemySpyFrameStates.Clear();
        _lastHeavyDashFrameStates.Clear();
        _heavyDashTrailTickCounters.Clear();
        _heavyDashTrailLastSpawnPositions.Clear();
    }

    private bool TryCreateExplosionVisual(WorldSoundEvent soundEvent, out ExplosionVisual? explosion)
    {
        return _gameplayImpactEffectsController.TryCreateExplosionVisual(soundEvent, out explosion);
    }

    private void RecordPresentedExplosionVisual(string effectName, float x, float y)
    {
        if (string.Equals(effectName, "Explosion", StringComparison.OrdinalIgnoreCase))
        {
            _presentedExplosionVisualsThisFrame.Add(new PresentedExplosionVisual(x, y));
        }
    }

    private bool HasPresentedExplosionVisualThisFrame(float x, float y)
    {
        const float epsilon = 0.01f;
        for (var index = 0; index < _presentedExplosionVisualsThisFrame.Count; index += 1)
        {
            var visual = _presentedExplosionVisualsThisFrame[index];
            if (MathF.Abs(visual.X - x) <= epsilon
                && MathF.Abs(visual.Y - y) <= epsilon)
            {
                return true;
            }
        }

        return false;
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
        _gameplayMaterialEffectsController.AdvanceLooseSheetVisuals();
    }

    private void AdvanceFrozenSpyVisuals()
    {
        for (var index = _frozenSpyVisuals.Count - 1; index >= 0; index -= 1)
        {
            _frozenSpyVisuals[index].TicksRemaining -= 1;
            if (_frozenSpyVisuals[index].TicksRemaining <= 0)
            {
                _frozenSpyVisuals.RemoveAt(index);
            }
        }
    }

    private void AdvanceBloodVisuals()
    {
        _gameplayGoreEffectsController.AdvanceBloodVisuals();
    }

    private void AdvanceSniperTracerParticles()
    {
        for (var index = _sniperTracerParticles.Count - 1; index >= 0; index -= 1)
        {
            var particle = _sniperTracerParticles[index];
            particle.TicksRemaining -= 1;
            if (particle.TicksRemaining <= 0)
            {
                _sniperTracerParticles.RemoveAt(index);
                continue;
            }

            particle.X += particle.VelocityX;
            particle.Y += particle.VelocityY;
            particle.VelocityX *= 0.75f;
            particle.VelocityY *= 0.75f;
        }
    }

    private void AdvanceShellVisuals()
    {
        _gameplayMaterialEffectsController.AdvanceShellVisuals();
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

    private void DrawFrozenSpyVisuals(Vector2 cameraPosition)
    {
        for (var index = 0; index < _frozenSpyVisuals.Count; index += 1)
        {
            var frozenVisual = _frozenSpyVisuals[index];
            var alpha = frozenVisual.TicksRemaining / (float)frozenVisual.LifetimeTicks;
            var frameState = frozenVisual.FrameState;
            var sprite = GetResolvedSprite(frameState.SpriteName);
            if (sprite is null || sprite.Frames.Count == 0)
            {
                continue;
            }

            var finalTint = frameState.Tint * alpha;
            var position = new Vector2(
                MathF.Round(frameState.RenderPosition.X) - cameraPosition.X,
                MathF.Round(frameState.RenderPosition.Y + frameState.BodyYOffset) - cameraPosition.Y);
            DrawSpriteFrameWithOptionalShadow(
                sprite.Frames[frameState.FrameIndex],
                position,
                finalTint,
                0f,
                frameState.Origin,
                frameState.Scale);
        }
    }

    private void DrawSpySuperjumpVisuals(Vector2 cameraPosition)
    {
        // Only show charging visuals for the local player (not synchronized over network)
        var localPlayer = _world.LocalPlayer;
        if (localPlayer == null || localPlayer.ClassId != PlayerClass.Spy || !localPlayer.IsAlive)
        {
            return;
        }

        var chargeTicks = localPlayer.SpySuperjumpChargeTicks;
        if (chargeTicks <= 0 || localPlayer.IsSpyBackstabAnimating || localPlayer.IsCarryingIntel)
        {
            return;
        }

        var chargeDirection = localPlayer.SpySuperjumpChargeDirectionDegrees;
        var chargeFraction = float.Min(1f, chargeTicks / (float)PlayerEntity.SpySuperjumpMaxChargeTicks);

        // Draw trajectory preview
        var radians = chargeDirection * (MathF.PI / 180f);
        var velocity = PlayerEntity.SpySuperjumpMinVelocity + (PlayerEntity.SpySuperjumpMaxVelocity - PlayerEntity.SpySuperjumpMinVelocity) * chargeFraction;
        var velocityX = MathF.Cos(radians) * velocity;
        var velocityY = MathF.Sin(radians) * velocity;

        // Apply velocity clamping to match game behavior (MaxStepSpeedPerTick = 15, SourceTicksPerSecond = 30)
        const float maxSpeed = 15f * 30f; // 450 units/second
        velocityX = float.Clamp(velocityX, -maxSpeed, maxSpeed);
        velocityY = float.Clamp(velocityY, -maxSpeed, maxSpeed);

        const int squareSize = 2;
        const int squareOutlineSize = 2;
        const float gravityPerSecond = 540f; // 0.6 * 30 * 30 from LegacyMovementModel
        const float maxFallSpeed = 300f; // 10 * 30 from LegacyMovementModel (terminal velocity)
        const float deltaTime = 1f / 30f; // Game physics tick duration (30 ticks per second)
        const int trajectoryPointInterval = 2; // Record trajectory point every N ticks for smooth interpolation

        // Simulate full trajectory and collect positions at regular time intervals
        var dotPositions = new System.Collections.Generic.List<(float x, float y, int tick)>();
        // Start from player's center
        var simX = localPlayer.X;
        var simY = localPlayer.Y;
        var simVelX = velocityX;
        var simVelY = velocityY;
        var maxTicks = 300;
        var collisionDetected = false;
        var collisionTick = maxTicks;

        // Add starting position
        dotPositions.Add((simX, simY, 0));

        // Get level solids for collision detection
        var level = _world.Level;

        // Use small hitbox matching the dot size (2x2 pixels) for trajectory collision detection
        const float dotCollisionRadius = 1f; // Half of squareSize (2 pixels)

        for (var tick = 0; tick < maxTicks && !collisionDetected; tick++)
        {
            var previousX = simX;
            var previousY = simY;

            // Skip gravity on tick 0 to match game behavior (gravity not applied on jump release frame when grounded)
            if (tick > 0)
            {
                // Apply gravity (half-step integration with terminal velocity cap)
                simVelY += gravityPerSecond * deltaTime * 0.5f;
                simVelY = MathF.Min(simVelY, maxFallSpeed);
            }

            simX += simVelX * deltaTime;
            simY += simVelY * deltaTime;

            if (tick > 0)
            {
                simVelY += gravityPerSecond * deltaTime * 0.5f;
                simVelY = MathF.Min(simVelY, maxFallSpeed);
            }

            // Check for wall collision at the new position using small dot-sized hitbox
            foreach (var solid in level.Solids)
            {
                if (simX + dotCollisionRadius > solid.Left
                    && simX - dotCollisionRadius < solid.Right
                    && simY + dotCollisionRadius > solid.Top
                    && simY - dotCollisionRadius < solid.Bottom)
                {
                    collisionDetected = true;
                    collisionTick = tick;
                    // Add the collision point so dots can travel to it
                    dotPositions.Add((simX, simY, tick));
                    break;
                }
            }

            if (collisionDetected)
            {
                break;
            }

            // Record trajectory point at regular time intervals for smooth interpolation
            if (tick % trajectoryPointInterval == 0)
            {
                dotPositions.Add((simX, simY, tick));
            }
        }

        // Animate dots: they spawn at the start and flow linearly through the trajectory
        // New dots spawn at regular intervals and move along the path until they hit a wall or expire
        const float dotSpawnInterval = 26.66f; // Spawn a new dot every ~26 ticks
        const float dotSpeed = 0.075f; // How fast dots move through the trajectory (75% slower than original)
        const float dotMaxLifetime = 180f; // Maximum dot lifetime in ticks (3 seconds at 60 TPS)
        var animationProgress = _spySuperjumpTrajectoryAnimationTicks;

        // Calculate total trajectory length for normalization
        var totalTicks = collisionDetected ? collisionTick : (dotPositions.Count > 0 ? dotPositions[dotPositions.Count - 1].tick : maxTicks);

        // Calculate maximum number of dots that could exist based on animation time
        var maxPossibleDots = (int)MathF.Ceiling(animationProgress / dotSpawnInterval) + 1;

        // Draw the dots - each dot is at a different position along the trajectory
        for (var dotIndex = 0; dotIndex < maxPossibleDots; dotIndex++)
        {
            // Calculate when this dot spawned
            var dotSpawnTime = dotIndex * dotSpawnInterval;
            var dotAge = animationProgress - dotSpawnTime;

            // Skip if this dot hasn't spawned yet
            if (dotAge < 0)
            {
                continue;
            }

            // Skip if this dot has exceeded its maximum lifetime
            if (dotAge > dotMaxLifetime)
            {
                continue;
            }

            // Calculate how far along the trajectory this dot has traveled
            var traveledTicks = dotAge * dotSpeed;

            // Skip if the dot has traveled past the end or collision point
            if (traveledTicks > totalTicks)
            {
                continue;
            }

            // Find the position along the trajectory for this traveled distance
            // Linear interpolation between trajectory points
            float dotX = localPlayer.X;
            float dotY = localPlayer.Y;
            var foundValidPosition = false;

            if (dotPositions.Count > 0)
            {
                // Find the trajectory segment this dot is on using the full float value
                for (var i = 0; i < dotPositions.Count; i++)
                {
                    if (dotPositions[i].tick >= traveledTicks)
                    {
                        if (i == 0)
                        {
                            // At or before the first point - use starting position
                            dotX = dotPositions[0].x;
                            dotY = dotPositions[0].y;
                        }
                        else
                        {
                            // Interpolate between previous and current point
                            var prev = dotPositions[i - 1];
                            var curr = dotPositions[i];
                            var t = (traveledTicks - prev.tick) / (curr.tick - prev.tick);
                            t = float.Clamp(t, 0f, 1f);
                            dotX = prev.x + (curr.x - prev.x) * t;
                            dotY = prev.y + (curr.y - prev.y) * t;
                        }
                        foundValidPosition = true;
                        break;
                    }
                }
            }

            // Skip this dot if we couldn't find a valid position for it
            // (it's beyond the last recorded trajectory point)
            if (!foundValidPosition && dotPositions.Count > 0)
            {
                continue;
            }

            // Use constant alpha to avoid flickering
            var alpha = 0.8f;

            // Convert to screen coordinates (no quantization for smooth sub-pixel movement)
            var screenX = dotX - cameraPosition.X;
            var screenY = dotY - cameraPosition.Y;

            // Draw black outline with rounded corners (draw in pieces, skip 1x1 corners)
            var outlineSize = squareSize + squareOutlineSize * 2;
            var outlineLeft = (int)(screenX - squareSize / 2 - squareOutlineSize);
            var outlineTop = (int)(screenY - squareSize / 2 - squareOutlineSize);

            // Top edge (excluding corners)
            var topRect = new Rectangle(outlineLeft + 1, outlineTop, outlineSize - 2, 2);
            _spriteBatch.Draw(_pixel, topRect, Color.Black * alpha);

            // Bottom edge (excluding corners)
            var bottomRect = new Rectangle(outlineLeft + 1, outlineTop + outlineSize - 2, outlineSize - 2, 2);
            _spriteBatch.Draw(_pixel, bottomRect, Color.Black * alpha);

            // Left edge (excluding corners)
            var leftRect = new Rectangle(outlineLeft, outlineTop + 1, 2, outlineSize - 2);
            _spriteBatch.Draw(_pixel, leftRect, Color.Black * alpha);

            // Right edge (excluding corners)
            var rightRect = new Rectangle(outlineLeft + outlineSize - 2, outlineTop + 1, 2, outlineSize - 2);
            _spriteBatch.Draw(_pixel, rightRect, Color.Black * alpha);

            // White square (center)
            var squareRect = new Rectangle(
                (int)(screenX - squareSize / 2),
                (int)(screenY - squareSize / 2),
                squareSize,
                squareSize);
            _spriteBatch.Draw(_pixel, squareRect, Color.White * alpha);
        }
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
        _gameplayMaterialEffectsController.DrawLooseSheetVisuals(cameraPosition);
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
        _gameplayMaterialEffectsController.DrawShellVisuals(cameraPosition);
    }

    private void QueueWeaponShellVisual(PlayerEntity player, float delaySeconds, int count)
    {
        _gameplayMaterialEffectsController.QueueWeaponShellVisual(player, delaySeconds, count);
    }

    private void QueueWeaponShellVisual(PlayerEntity player, float delaySeconds, int count, PlayerClass classId)
    {
        _gameplayMaterialEffectsController.QueueWeaponShellVisual(player, delaySeconds, count, classId);
    }

    private void QueueWeaponShellVisual(PlayerEntity player, float delaySeconds, int count, PlayerClass classId, string spriteName)
    {
        _gameplayMaterialEffectsController.QueueWeaponShellVisual(player, delaySeconds, count, classId, spriteName);
    }

    private bool IsShellBlocked(float x, float y)
    {
        return _gameplayMaterialEffectsController.IsShellBlocked(x, y);
    }

    private static float ScaleSourceTickDistance(float sourceDistance)
    {
        return GameplayMaterialEffectsController.ScaleSourceTickDistance(sourceDistance);
    }

    private void PlayPendingVisualEvents()
    {
        _gameplayVisualEventController.PlayPendingVisualEvents();
    }

    private void PlayVisualEvent(string effectName, float x, float y, float directionDegrees, int count)
    {
        _gameplayVisualEventController.PlayVisualEvent(effectName, x, y, directionDegrees, count);
    }

    private void SpawnLooseSheetVisual(float x, float y, float initialHorizontalSpeed)
    {
        _gameplayMaterialEffectsController.SpawnLooseSheetVisual(x, y, initialHorizontalSpeed);
    }

    private void RecordLastVisibleEnemySpyFrame(
        PlayerEntity player,
        string spriteName,
        int frameIndex,
        Vector2 renderPosition,
        Vector2 origin,
        Vector2 scale,
        float bodyYOffset,
        Color tint,
        bool drawIntelOverlay)
    {
        _lastVisibleEnemySpyFrameStates[player.Id] = new FrozenSpyFrameState(
            spriteName,
            frameIndex,
            renderPosition,
            scale,
            origin,
            bodyYOffset,
            tint,
            drawIntelOverlay);
    }

    private void SpawnFrozenSpyVisual(int playerId)
    {
        if (!_lastVisibleEnemySpyFrameStates.TryGetValue(playerId, out var frameState))
        {
            return;
        }

        for (var index = 0; index < _frozenSpyVisuals.Count; index += 1)
        {
            if (_frozenSpyVisuals[index].PlayerId == playerId)
            {
                return;
            }
        }

        _frozenSpyVisuals.Add(new FrozenSpyVisual(playerId, frameState, lifetimeTicks: 30));
    }

    private void RemoveFrozenSpyVisualsForPlayer(int playerId)
    {
        for (var index = _frozenSpyVisuals.Count - 1; index >= 0; index -= 1)
        {
            if (_frozenSpyVisuals[index].PlayerId == playerId)
            {
                _frozenSpyVisuals.RemoveAt(index);
            }
        }
    }

    private void ResetFrozenSpyStateForPlayer(int playerId)
    {
        _lastVisibleEnemySpyFrameStates.Remove(playerId);
        RemoveFrozenSpyVisualsForPlayer(playerId);
    }

    private void RecordHeavyDashFrameState(
        PlayerEntity player,
        string spriteName,
        int frameIndex,
        Vector2 renderPosition,
        Vector2 origin,
        Vector2 scale,
        float bodyYOffset,
        Color tint,
        bool drawIntelOverlay)
    {
        _lastHeavyDashFrameStates[player.Id] = new FrozenSpyFrameState(
            spriteName,
            frameIndex,
            renderPosition,
            scale,
            origin,
            bodyYOffset,
            tint,
            drawIntelOverlay);
    }

    // Minimum position change (in world pixels) between trail freeze frames
    private const float HeavyDashTrailSpawnDistanceThreshold = 8f;

    private void AdvanceHeavyDashTrailVisuals()
    {
        foreach (var player in EnumerateRenderablePlayers())
        {
            if (!player.IsAlive || player.ClassId != PlayerClass.Heavy)
            {
                continue;
            }

            // ExperimentalGhostDashEnablesTrail is a local visual flag set during ability execution.
            // Use the predicted state getter so it works correctly for the local player online.
            var trailEnabled = GetPlayerExperimentalGhostDashEnablesTrail(player);
            if (!GetPlayerIsExperimentalGhostDashing(player) || !trailEnabled)
            {
                _heavyDashTrailTickCounters.Remove(player.Id);
                _lastHeavyDashFrameStates.Remove(player.Id);
                _heavyDashTrailLastSpawnPositions.Remove(player.Id);
                continue;
            }

            if (!_lastHeavyDashFrameStates.TryGetValue(player.Id, out var frameState))
            {
                continue;
            }

            // Only spawn if Heavy has moved far enough since the last freeze frame
            var currentPos = frameState.RenderPosition;
            if (_heavyDashTrailLastSpawnPositions.TryGetValue(player.Id, out var lastPos)
                && (currentPos - lastPos).LengthSquared() < HeavyDashTrailSpawnDistanceThreshold * HeavyDashTrailSpawnDistanceThreshold)
            {
                continue;
            }

            // Spawn trail ghost at 50% opacity by halving the tint; it then fades to 0 over lifetime.
            var trailFrameState = frameState with { Tint = frameState.Tint * 0.5f };
            _frozenSpyVisuals.Add(new FrozenSpyVisual(player.Id, trailFrameState, lifetimeTicks: 30));
            _heavyDashTrailLastSpawnPositions[player.Id] = currentPos;
        }
    }

    private void SpawnBackstabVisual(int ownerId, PlayerTeam team, float x, float y, float directionDegrees)
    {
        _gameplayGoreEffectsController.SpawnBackstabVisual(ownerId, team, x, y, directionDegrees);
    }

    private void ResetBackstabVisuals()
    {
        _gameplayGoreEffectsController.ResetBackstabVisuals();
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
        public FlameSmokeVisual(float x, float y, float offsetX, float offsetY, float driftX, float driftY, float initialRadius, float finalRadius, float initialAlpha, int lifetimeTicks)
        {
            X = x;
            Y = y;
            OffsetX = offsetX;
            OffsetY = offsetY;
            DriftX = driftX;
            DriftY = driftY;
            InitialRadius = initialRadius;
            FinalRadius = finalRadius;
            InitialAlpha = initialAlpha;
            LifetimeTicks = Math.Max(1, lifetimeTicks);
            TicksRemaining = LifetimeTicks;
        }

        public float X { get; }

        public float Y { get; }

        public float OffsetX { get; }

        public float OffsetY { get; }

        public float DriftX { get; }

        public float DriftY { get; }

        public float InitialRadius { get; }

        public float FinalRadius { get; }

        public float InitialAlpha { get; }

        public int LifetimeTicks { get; }

        public int TicksRemaining { get; set; }
    }

    private sealed class LooseSheetVisual
    {
        public const int LifetimeTicks = 260;
        public const int FadeTicks = 60;
        public const int BurnLifetimeTicks = 24;

        public LooseSheetVisual(float x, float y, float velocityX, float velocityY, float rotationSpeedRadians, string spriteName, int lifetimeTicks = LifetimeTicks, int fadeTicks = FadeTicks)
        {
            X = x;
            Y = y;
            VelocityX = velocityX;
            VelocityY = velocityY;
            RotationSpeedRadians = rotationSpeedRadians;
            SpriteName = spriteName;
            TicksRemaining = Math.Max(1, lifetimeTicks);
            FadeTicksRemaining = Math.Max(1, fadeTicks);
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

        public int FadeTicksRemaining { get; }
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
        public PendingWeaponShellVisual(int playerId, PlayerClass classId, PlayerTeam team, float delaySeconds, int count, string? spriteName = null)
        {
            PlayerId = playerId;
            ClassId = classId;
            Team = team;
            DelaySeconds = delaySeconds;
            Count = count;
            SpriteName = spriteName;
        }

        public int PlayerId { get; }

        public PlayerClass ClassId { get; }

        public PlayerTeam Team { get; }

        public float DelaySeconds { get; set; }

        public int Count { get; }

        public string? SpriteName { get; }
    }

    private sealed class ShellVisual
    {
        public ShellVisual(float x, float y, float velocityX, float velocityY, int frameIndex, float rotationDegrees, float rotationSpeedDegrees, int fadeDelayTicks, string? spriteName = null)
        {
            X = x;
            Y = y;
            VelocityX = velocityX;
            VelocityY = velocityY;
            FrameIndex = frameIndex;
            RotationDegrees = rotationDegrees;
            RotationSpeedDegrees = rotationSpeedDegrees;
            TicksUntilFade = fadeDelayTicks;
            SpriteName = spriteName;
        }

        public float X { get; set; }

        public float Y { get; set; }

        public float VelocityX { get; set; }

        public float VelocityY { get; set; }

        public int FrameIndex { get; }

        public string? SpriteName { get; }

        public float RotationDegrees { get; set; }

        public float RotationSpeedDegrees { get; set; }

        public int TicksUntilFade { get; set; }

        public bool Fade { get; set; }

        public bool Stuck { get; set; }

        public float Alpha { get; set; } = 1f;
    }

    private sealed class BlastJumpFlameVisual
    {
        public BlastJumpFlameVisual(float x, float y, float motionX, float motionY, int initialTicks, int frameSeed)
        {
            X = x;
            Y = y;
            MotionX = motionX;
            MotionY = motionY;
            InitialTicks = Math.Max(1, initialTicks);
            TicksRemaining = InitialTicks;
            FrameSeed = frameSeed;
        }

        public float X { get; }

        public float Y { get; }

        public float MotionX { get; }

        public float MotionY { get; }

        public int InitialTicks { get; }

        public int TicksRemaining { get; set; }

        public int FrameSeed { get; }
    }

    private sealed class RocketSmokeVisual
    {
        public RocketSmokeVisual(float x, float y, float offsetX, float offsetY, float driftX, float driftY, float initialRadius, float finalRadius, float initialAlpha, int lifetimeTicks)
        {
            X = x;
            Y = y;
            OffsetX = offsetX;
            OffsetY = offsetY;
            DriftX = driftX;
            DriftY = driftY;
            InitialRadius = initialRadius;
            FinalRadius = finalRadius;
            InitialAlpha = initialAlpha;
            LifetimeTicks = Math.Max(1, lifetimeTicks);
            TicksRemaining = LifetimeTicks;
        }

        public float X { get; }

        public float Y { get; }

        public float OffsetX { get; }

        public float OffsetY { get; }

        public float DriftX { get; }

        public float DriftY { get; }

        public float InitialRadius { get; }

        public float FinalRadius { get; }

        public float InitialAlpha { get; }

        public int LifetimeTicks { get; }

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

    private sealed class SniperTracerParticle
    {
        public const int LifetimeTicks = 30; // ~1.0s at 30 TPS

        public SniperTracerParticle(float x, float y, float velocityX, float velocityY, Color color)
        {
            X = x;
            Y = y;
            VelocityX = velocityX;
            VelocityY = velocityY;
            Color = color;
            TicksRemaining = LifetimeTicks;
        }

        public float X { get; set; }

        public float Y { get; set; }

        public float VelocityX { get; set; }

        public float VelocityY { get; set; }

        public Color Color { get; }

        public int TicksRemaining { get; set; }
    }
}
