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
        _gameplayMaterialEffectsController.ResetTransientEffects();
        _rocketSmokeVisuals.Clear();
        _mineTrailVisuals.Clear();
        _wallspinDustVisuals.Clear();
        _blastJumpFlameVisuals.Clear();
        _flameSmokeVisuals.Clear();
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
        _gameplayMaterialEffectsController.AdvanceLooseSheetVisuals();
    }

    private void AdvanceBloodVisuals()
    {
        _gameplayGoreEffectsController.AdvanceBloodVisuals();
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
