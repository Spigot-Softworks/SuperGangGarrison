#nullable enable

using System;
using System.Collections.Generic;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

/// <summary>
/// Squib-mode blood from moving gore parts, following GG2's Gib object.
/// </summary>
/// <remarks>
/// GG2 gibs bleed in proportion to how fast they move (Gib Step event) and
/// splat when they hit the ground hard (Gib Collision with Obstacle). Kicking a
/// gib, or blasting it, speeds it up, so it bleeds and splats again. The
/// simulation still spawns legacy <see cref="BloodDropEntity"/> drops for this,
/// but squib mode does not draw those, so here the same rules spawn visible
/// squibs (which also leave floor stains) from the gib's own motion.
/// </remarks>
public sealed partial class GameplayGoreEffectsController
{
    // GG2: if (abs(speed / bloodchance) > random(16 / global.gibLevel)) spawn a drop.
    // Squib mode already doubles blood amounts, so trails roll against twice
    // GG2's range (about half as many drops) and fly at half GG2's speed.
    private const float GibTrailChanceNumerator = 32f; // GG2: 16
    private const float GibTrailVelocityScale = 0.45f; // GG2: 0.9
    private const float GibTrailVelocityJitter = 1f; // GG2: random(3) - 1
    // GG2 splat: (speed > 4) and (vspeed > 2) on contact, then repeat(9) with
    // abs(speed / bloodchance) > random(3).
    private const float GibSplatMinimumSpeed = 4f;
    private const float GibSplatMinimumFallSpeed = 2f;
    private const int GibSplatAttempts = 4; // GG2: 9
    private const float GibSplatChanceDenominator = 3f;
    private const float GibSplatReboundScale = 0.35f; // GG2: 0.8
    // Keep room for wound and death bursts; trails only use the lower part of the pool.
    private const int GibTrailParticleBudget = MaxBloodSquibParticles * 3 / 4;

    private readonly Dictionary<int, (float VelocityX, float VelocityY)> _gibBloodPreviousVelocities = new();
    private readonly HashSet<int> _gibBloodSeenIds = new();
    private readonly List<int> _gibBloodStaleIds = new();

    private void ResetGibBloodTrails()
    {
        _gibBloodPreviousVelocities.Clear();
        _gibBloodSeenIds.Clear();
        _gibBloodStaleIds.Clear();
    }

    private void AdvanceGibBloodTrails()
    {
        var gibLevel = Math.Clamp(_context.GameplayRuntimeSettings.GibLevel, 0, 3);
        if (gibLevel <= 0 || _context.ScaleBloodVisualCount(1) <= 0)
        {
            ResetGibBloodTrails();
            return;
        }

        var trailThreshold = GibTrailChanceNumerator / gibLevel;
        var gibs = _context._world.PlayerGibs;
        _gibBloodSeenIds.Clear();
        for (var index = 0; index < gibs.Count; index += 1)
        {
            var gib = gibs[index];
            if (gib.IsExpired)
            {
                continue;
            }

            _gibBloodSeenIds.Add(gib.Id);
            var velocityX = gib.VelocityX;
            var velocityY = gib.VelocityY;
            var hadPrevious = _gibBloodPreviousVelocities.TryGetValue(gib.Id, out var previous);
            _gibBloodPreviousVelocities[gib.Id] = (velocityX, velocityY);
            if (gib.BloodChance <= 0f || !float.IsFinite(velocityX) || !float.IsFinite(velocityY))
            {
                continue;
            }

            if (hadPrevious)
            {
                TrySpawnGibLandingSplat(gib, previous.VelocityX, previous.VelocityY, velocityY);
            }

            TrySpawnGibTrailSquib(gib, velocityX, velocityY, trailThreshold);
        }

        _gibBloodStaleIds.Clear();
        foreach (var id in _gibBloodPreviousVelocities.Keys)
        {
            if (!_gibBloodSeenIds.Contains(id))
            {
                _gibBloodStaleIds.Add(id);
            }
        }

        for (var index = 0; index < _gibBloodStaleIds.Count; index += 1)
        {
            _gibBloodPreviousVelocities.Remove(_gibBloodStaleIds[index]);
        }
    }

    private void TrySpawnGibTrailSquib(PlayerGibEntity gib, float velocityX, float velocityY, float trailThreshold)
    {
        if (_bloodSquibParticles.Count >= GibTrailParticleBudget)
        {
            return;
        }

        var speed = MathF.Sqrt((velocityX * velocityX) + (velocityY * velocityY));
        if (MathF.Abs(speed / gib.BloodChance) <= _context._visualRandom.NextSingle() * trailThreshold)
        {
            return;
        }

        // GG2: motion_add(direction of travel, speed * 0.9), then random(3)-1 on each axis.
        TryAddBloodSquib(
            gib.X,
            gib.Y - 1f,
            (velocityX * GibTrailVelocityScale) + (((_context._visualRandom.NextSingle() * 2f) - 1f) * GibTrailVelocityJitter),
            (velocityY * GibTrailVelocityScale) + (((_context._visualRandom.NextSingle() * 2f) - 1f) * GibTrailVelocityJitter),
            0.18f + (_context._visualRandom.NextSingle() * 0.17f),
            _context._visualRandom.Next(18, 32),
            gib.ExperimentalCryoTinted,
            heavy: false);
    }

    private void TrySpawnGibLandingSplat(PlayerGibEntity gib, float previousVelocityX, float previousVelocityY, float velocityY)
    {
        // Moving down fast last tick and no longer moving down: it hit the ground.
        if (previousVelocityY <= GibSplatMinimumFallSpeed || velocityY > 0f)
        {
            return;
        }

        var impactSpeed = MathF.Sqrt((previousVelocityX * previousVelocityX) + (previousVelocityY * previousVelocityY));
        if (impactSpeed <= GibSplatMinimumSpeed)
        {
            return;
        }

        // GG2 sends splat drops back against the impact direction at 0.8x speed
        // with hspeed += random(8)-3.5 and vspeed -= random(6)-2; both are
        // scaled down here.
        var attempts = _context.ScaleBloodVisualCount(GibSplatAttempts);
        for (var attempt = 0; attempt < attempts; attempt += 1)
        {
            if (MathF.Abs(impactSpeed / gib.BloodChance) <= _context._visualRandom.NextSingle() * GibSplatChanceDenominator)
            {
                continue;
            }

            TryAddBloodSquib(
                gib.X,
                gib.Y - 1f,
                (-previousVelocityX * GibSplatReboundScale) + (((_context._visualRandom.NextSingle() * 2f) - 1f) * 2f),
                (-previousVelocityY * GibSplatReboundScale) - (_context._visualRandom.NextSingle() * 1.5f),
                0.2f + (_context._visualRandom.NextSingle() * 0.25f),
                _context._visualRandom.Next(20, 36),
                gib.ExperimentalCryoTinted,
                heavy: false);
        }
    }
}
