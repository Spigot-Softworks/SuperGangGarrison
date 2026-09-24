#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    // Kelly: classic horizontal DeadS ragdolls (below).
    // Elkondo: temporary corpse uses the 5th run-cycle pose — see GameplayDynamicRagdollController.Elkondo.cs
    // TODO(dynamic-ragdoll): replace that temporary run-frame with unique Elkondo Dead sprites when authored.

    private const int MaxDynamicRagdolls = 28;
    private const int DynamicRagdollPivotCount = 3;
    private const int DynamicRagdollCollisionNodeCount = DynamicRagdollPivotCount + 1; // one meat chunk per segment
    private const float DynamicRagdollMaxPivotDegrees = 45f;
    private const float DynamicRagdollGravity = 0.7f;
    private const float DynamicRagdollMaxFallSpeed = 11f;
    private const float DynamicRagdollGroundBounce = 0.18f;
    private const float DynamicRagdollWallBounce = 0.28f;
    private const float DynamicRagdollLaunchSpeedScale = 1.85f;
    private const float DynamicRagdollMinLaunchSpeed = 3.2f;
    private const float DynamicRagdollGroundFriction = 0.64f;
    private const float DynamicRagdollAirAngularDamping = 0.992f;
    private const float DynamicRagdollGroundAngularDamping = 0.48f;
    private const float DynamicRagdollPivotSpringAir = 0.035f;
    private const float DynamicRagdollPivotSpringGround = 0.018f;
    private const float DynamicRagdollPivotDamping = 0.9f;
    private const float DynamicRagdollSettledSpeed = 0.28f;
    private const float DynamicRagdollSettledAngularSpeed = 0.8f;
    private const float DynamicRagdollAngularPivotCoupling = 0.22f;
    // Circle hitboxes along the spine (world solids only — never each other).
    // Diameter ~4.5px for all meat circles; waist stays the same size but is weighted as primary support.
    private const float DynamicRagdollSegmentRadius = 2.25f; // Ø 4.5
    private const float DynamicRagdollWaistRadius = 2.25f; // same size; primacy is weight/resolve order, not radius
    private const float DynamicRagdollLedgeDroopAccel = 2.8f;
    private const float DynamicRagdollHangProbeDepth = 56f;
    private const float DynamicRagdollLieTorque = 0.14f;
    private const float DynamicRagdollTipOverTorque = 0.22f;
    private const float DynamicRagdollSettleMaxLieErrorDegrees = 38f;
    private const float DynamicRagdollMaxCollisionHalfWidth = 9f;
    private const float DynamicRagdollMaxCollisionHalfHeight = 12f;
    private const int DynamicRagdollOpaqueAlphaThreshold = 24;
    private const int DynamicRagdollSeamWidthPixels = 2;

    // Fractions along the *opaque* corpse span (head → feet), not the full padded frame.
    private static readonly float[] DynamicRagdollPivotFractions = [0.28f, 0.52f, 0.76f];

    private readonly Dictionary<int, DynamicRagdollState> _dynamicRagdolls = new();
    private readonly List<int> _staleDynamicRagdollIds = new();
    private readonly Random _dynamicRagdollRandom = new();

    private sealed class DynamicRagdollState
    {
        public required int DeadBodyId { get; init; }
        public required int SourcePlayerId { get; init; }
        public required PlayerClass ClassId { get; init; }
        public required PlayerTeam Team { get; init; }
        public required DeadBodyAnimationKind AnimationKind { get; init; }
        public required string GameplayClassId { get; init; }
        public required bool FacingLeft { get; init; }

        public float X;
        public float Y;
        public float VelocityX;
        public float VelocityY;
        public float RotationDegrees;
        public float AngularVelocityDegrees;
        public bool Grounded;
        public bool Settled;
        public int AgeTicks;
        public int GroundedTicks;
        public Rectangle OpaqueBounds;
        public float CollisionHalfWidth;
        public float CollisionHalfHeight;
        public bool UseElkondoVerticalVisual;
        public string WeaponSpriteName = string.Empty;
        public int WeaponFrameIndex;
        public Vector2 WeaponOrigin;
        public float WeaponAttachLocalX;
        public float WeaponAttachLocalY;
        public float WeaponFlapDegrees;
        public float WeaponFlapVelocityDegrees;
        public readonly float[] PivotDegrees = new float[DynamicRagdollPivotCount];
        public readonly float[] PivotVelocities = new float[DynamicRagdollPivotCount];
        public readonly float[] RestPivotDegrees = new float[DynamicRagdollPivotCount];
    }

    private void ResetDynamicRagdollEffects()
    {
        _dynamicRagdolls.Clear();
        _staleDynamicRagdollIds.Clear();
    }

    private void AdvanceDynamicRagdolls()
    {
        if (!_dynamicRagdollEnabled || _dynamicRagdolls.Count == 0)
        {
            if (!_dynamicRagdollEnabled && _dynamicRagdolls.Count > 0)
            {
                ResetDynamicRagdollEffects();
            }

            return;
        }

        var level = _world.Level;
        var bounds = _world.Bounds;
        if (level.IsTopDown)
        {
            return;
        }

        foreach (var pair in _dynamicRagdolls)
        {
            var ragdoll = pair.Value;
            ragdoll.AgeTicks += 1;

            Span<Vector2> poseNodes = stackalloc Vector2[DynamicRagdollCollisionNodeCount];
            Span<bool> poseGrounded = stackalloc bool[DynamicRagdollCollisionNodeCount];
            var poseNodeCount = BuildRagdollCollisionNodes(ragdoll, poseNodes);
            var hangingCount = CountRagdollHangingNodes(
                ragdoll,
                level,
                poseNodes,
                poseGrounded,
                poseNodeCount,
                out var groundedCount);
            var isHangingOffLedge = groundedCount > 0 && hangingCount > 0;

            if (ragdoll.Settled)
            {
                var settledWaist = GetRagdollWaistWorldPosition(ragdoll);
                var settledWaistGrounded = TryFindRagdollNodeFloor(
                    settledWaist,
                    DynamicRagdollWaistRadius,
                    level,
                    wasFalling: true,
                    out _);
                // Wake if hanging off a ledge, or settled in an upright feet-plant with hips in the air.
                if (isHangingOffLedge || !settledWaistGrounded)
                {
                    ragdoll.Settled = false;
                    ragdoll.GroundedTicks = 0;
                }
                else
                {
                    continue;
                }
            }

            ragdoll.VelocityY = MathF.Min(DynamicRagdollMaxFallSpeed, ragdoll.VelocityY + DynamicRagdollGravity);
            var hitGround = AdvanceRagdollSegmentCollision(ragdoll, level, bounds);
            ragdoll.Grounded = hitGround;
            ragdoll.GroundedTicks = hitGround ? ragdoll.GroundedTicks + 1 : 0;

            // Refresh pose after collision for draping / waist support checks.
            poseNodeCount = BuildRagdollCollisionNodes(ragdoll, poseNodes);
            hangingCount = CountRagdollHangingNodes(
                ragdoll,
                level,
                poseNodes,
                poseGrounded,
                poseNodeCount,
                out groundedCount);
            isHangingOffLedge = groundedCount > 0 && hangingCount > 0;
            var waistWorld = GetRagdollWaistWorldPosition(ragdoll);
            var waistGrounded = TryFindRagdollNodeFloor(
                waistWorld,
                DynamicRagdollWaistRadius,
                level,
                wasFalling: true,
                out _);
            // Feet-only plant with waist airborne still counts as needing tip-over, not a hang-settle.
            if (!waistGrounded && groundedCount > 0)
            {
                isHangingOffLedge = false;
            }

            if (hitGround || isHangingOffLedge || waistGrounded)
            {
                ragdoll.VelocityX *= DynamicRagdollGroundFriction;
                ragdoll.AngularVelocityDegrees *= DynamicRagdollGroundAngularDamping;

                // Always drive toward a lying slab once the waist has (or should have) ground contact.
                var lieTarget = GetRagdollLieTargetDegrees(ragdoll);
                var settleError = NormalizeRagdollRotationDegrees(lieTarget - ragdoll.RotationDegrees);
                var lieStrength = waistGrounded ? DynamicRagdollLieTorque : DynamicRagdollTipOverTorque;
                // Standing on feet with waist sky-high: tip over hard.
                if (!waistGrounded && groundedCount > 0)
                {
                    lieStrength = DynamicRagdollTipOverTorque * 1.6f;
                }

                if (!isHangingOffLedge || waistGrounded)
                {
                    ragdoll.AngularVelocityDegrees += settleError * lieStrength;
                }

                if (MathF.Abs(ragdoll.VelocityY) < DynamicRagdollSettledSpeed && waistGrounded)
                {
                    ragdoll.VelocityY = 0f;
                }

                AdaptRagdollRestPivotsToContacts(ragdoll, level);
                ApplyRagdollLedgeGravityDroop(ragdoll, poseNodes, poseGrounded, poseNodeCount);
                // Flatten secondary pivots toward a laid-out corpse once waist is planted.
                if (waistGrounded && !isHangingOffLedge)
                {
                    ApplyRagdollLyingPoseBias(ragdoll);
                }
            }
            else
            {
                ragdoll.AngularVelocityDegrees *= DynamicRagdollAirAngularDamping;
            }

            ragdoll.RotationDegrees += ragdoll.AngularVelocityDegrees;
            AdvanceRagdollPivots(ragdoll, hitGround || isHangingOffLedge || waistGrounded);
            if (ragdoll.UseElkondoVerticalVisual)
            {
                AdvanceElkondoRagdollWeapon(ragdoll);
            }

            var lieError = MathF.Abs(NormalizeRagdollRotationDegrees(
                GetRagdollLieTargetDegrees(ragdoll) - ragdoll.RotationDegrees));
            // Settle only with waist planted and body mostly horizontal — never "standing".
            if (waistGrounded
                && !isHangingOffLedge
                && lieError <= DynamicRagdollSettleMaxLieErrorDegrees
                && ragdoll.GroundedTicks > 6
                && MathF.Abs(ragdoll.VelocityX) < DynamicRagdollSettledSpeed
                && MathF.Abs(ragdoll.VelocityY) < DynamicRagdollSettledSpeed
                && MathF.Abs(ragdoll.AngularVelocityDegrees) < DynamicRagdollSettledAngularSpeed)
            {
                ragdoll.VelocityX = 0f;
                ragdoll.VelocityY = 0f;
                ragdoll.AngularVelocityDegrees = 0f;
                ragdoll.WeaponFlapVelocityDegrees = 0f;
                for (var pivotIndex = 0; pivotIndex < DynamicRagdollPivotCount; pivotIndex += 1)
                {
                    ragdoll.RestPivotDegrees[pivotIndex] = ragdoll.PivotDegrees[pivotIndex];
                    ragdoll.PivotVelocities[pivotIndex] = 0f;
                }

                ragdoll.Settled = true;
            }
        }
    }

    private void SyncDynamicRagdollsWithDeadBodies()
    {
        if (!_dynamicRagdollEnabled)
        {
            if (_dynamicRagdolls.Count > 0)
            {
                ResetDynamicRagdollEffects();
            }

            return;
        }

        _staleDynamicRagdollIds.Clear();
        foreach (var id in _dynamicRagdolls.Keys)
        {
            _staleDynamicRagdollIds.Add(id);
        }

        foreach (var deadBody in _world.DeadBodies)
        {
            _staleDynamicRagdollIds.Remove(deadBody.Id);
            // Keep the already-simulated immediate ragdoll instead of respawning (looks like a pop/vanish).
            var syntheticId = -Math.Abs(deadBody.SourcePlayerId);
            if (!_dynamicRagdolls.ContainsKey(deadBody.Id)
                && _dynamicRagdolls.Remove(syntheticId, out var transferred))
            {
                _staleDynamicRagdollIds.Remove(syntheticId);
                // If the immediate launch was a weak facing fallback, prefer server corpse speeds.
                var authSpeedSq = (deadBody.HorizontalSpeed * deadBody.HorizontalSpeed)
                    + (deadBody.VerticalSpeed * deadBody.VerticalSpeed);
                var currentSpeedSq = (transferred.VelocityX * transferred.VelocityX)
                    + (transferred.VelocityY * transferred.VelocityY);
                if (authSpeedSq > 1f
                    && authSpeedSq > currentSpeedSq * 1.35f
                    && transferred.AgeTicks <= 8)
                {
                    transferred.VelocityX = deadBody.HorizontalSpeed * DynamicRagdollLaunchSpeedScale;
                    transferred.VelocityY = MathF.Min(
                        deadBody.VerticalSpeed * DynamicRagdollLaunchSpeedScale,
                        -2.0f);
                    transferred.Settled = false;
                    transferred.GroundedTicks = 0;
                }

                _dynamicRagdolls[deadBody.Id] = transferred;
                continue;
            }

            EnsureDynamicRagdoll(deadBody);
        }

        for (var index = 0; index < _retainedDeadBodies.Count; index += 1)
        {
            _staleDynamicRagdollIds.Remove(_retainedDeadBodies[index].Id);
        }

        foreach (var entry in _immediateNetworkDeadBodies)
        {
            var visual = entry.Value;
            var syntheticId = -Math.Abs(visual.SourcePlayerId);
            // Authoritative corpse already owns this player's ragdoll — don't keep a duplicate.
            var hasAuthoritativeRagdoll = false;
            foreach (var pair in _dynamicRagdolls)
            {
                if (pair.Key > 0 && pair.Value.SourcePlayerId == visual.SourcePlayerId)
                {
                    hasAuthoritativeRagdoll = true;
                    break;
                }
            }

            if (hasAuthoritativeRagdoll)
            {
                continue;
            }

            _staleDynamicRagdollIds.Remove(syntheticId);
            EnsureDynamicRagdollFromImmediate(visual, syntheticId);
        }

        for (var index = 0; index < _staleDynamicRagdollIds.Count; index += 1)
        {
            _dynamicRagdolls.Remove(_staleDynamicRagdollIds[index]);
        }

        TrimDynamicRagdollCapacity();
    }

    /// <summary>
    /// Resolve directed corpse launch away from the kill shot. Magnitudes are weapon-based
    /// (not projectile-speed-based); sniper hits harder than revolver, sentry matches revolver.
    /// </summary>
    internal void ResolveCorpseDeathKnockback(
        float corpseX,
        float corpseY,
        bool facingLeft,
        int attackerPlayerId,
        float damageEventX,
        float damageEventY,
        out float knockbackX,
        out float knockbackY)
    {
        var knockbackSpeed = CorpseKnockbackRules.StandardSpeed;
        var originX = corpseX;
        var originY = corpseY;
        var hasOrigin = false;

        var attacker = FindPlayerById(attackerPlayerId);
        if (attacker is not null)
        {
            knockbackSpeed = CorpseKnockbackRules.ResolveSpeed(weaponSpriteName: null, attacker.ClassId);
            originX = attacker.X;
            originY = attacker.Y;
            hasOrigin = true;

            // Sentry kills credit the engineer — prefer a nearby owned turret as the shot origin.
            if (attacker.ClassId == PlayerClass.Engineer
                && TryFindCorpseKnockbackSentryOrigin(attacker.Id, corpseX, corpseY, out var sentryX, out var sentryY))
            {
                originX = sentryX;
                originY = sentryY;
                knockbackSpeed = CorpseKnockbackRules.StandardSpeed;
            }
        }
        else if (MathF.Abs(damageEventX - corpseX) > 0.5f
                 || MathF.Abs(damageEventY - corpseY) > 0.5f)
        {
            originX = damageEventX;
            originY = damageEventY;
            hasOrigin = true;
        }

        knockbackSpeed = CorpseKnockbackRules.EnforceMinimum(knockbackSpeed);

        if (hasOrigin)
        {
            var deltaX = corpseX - originX;
            var deltaY = corpseY - originY;
            var distance = MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
            if (distance > 0.001f)
            {
                // Match server SpawnDeadBody arc (not projectile-speed based).
                knockbackX = (deltaX / distance) * knockbackSpeed;
                knockbackY = ((deltaY / distance) * knockbackSpeed * 0.45f) - 2.0f;
                return;
            }
        }

        // Opposite of facing ≈ away from whoever they were aiming at.
        knockbackX = (facingLeft ? 1f : -1f) * knockbackSpeed * 0.7f;
        knockbackY = -2.4f;
    }

    private bool TryFindCorpseKnockbackSentryOrigin(
        int ownerPlayerId,
        float corpseX,
        float corpseY,
        out float sentryX,
        out float sentryY)
    {
        sentryX = 0f;
        sentryY = 0f;
        var bestDistance = float.MaxValue;
        var found = false;
        foreach (var sentry in _world.Sentries)
        {
            if (sentry.OwnerPlayerId != ownerPlayerId)
            {
                continue;
            }

            var deltaX = corpseX - sentry.X;
            var deltaY = corpseY - sentry.Y;
            var distance = MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
            if (distance > SentryEntity.TargetRange || distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            sentryX = sentry.X;
            sentryY = sentry.Y;
            found = true;
        }

        return found;
    }

    private void EnsureDynamicRagdoll(DeadBodyEntity deadBody)
    {
        if (_dynamicRagdolls.ContainsKey(deadBody.Id))
        {
            return;
        }

        SpawnDynamicRagdoll(
            deadBody.Id,
            deadBody.SourcePlayerId,
            deadBody.ClassId,
            deadBody.Team,
            deadBody.AnimationKind,
            deadBody.GameplayClassId,
            deadBody.FacingLeft,
            deadBody.X,
            deadBody.Y,
            deadBody.HorizontalSpeed,
            deadBody.VerticalSpeed);
    }

    private void EnsureDynamicRagdollFromImmediate(ImmediateNetworkDeadBodyVisual deadBody, int syntheticId)
    {
        if (_dynamicRagdolls.ContainsKey(syntheticId))
        {
            return;
        }

        // Knock opposite of facing — facing the killer is typical, so this reads as away from the shot.
        var facingSign = deadBody.FacingLeft ? 1f : -1f;
        var knockbackSpeed = CorpseKnockbackRules.EnforceMinimum(CorpseKnockbackRules.StandardSpeed);
        SpawnDynamicRagdoll(
            syntheticId,
            deadBody.SourcePlayerId,
            deadBody.ClassId,
            deadBody.Team,
            deadBody.AnimationKind,
            deadBody.GameplayClassId,
            deadBody.FacingLeft,
            deadBody.X,
            deadBody.Y,
            knockbackX: facingSign * knockbackSpeed * 0.7f,
            knockbackY: -2.4f);
    }

    private void SpawnDynamicRagdoll(
        int deadBodyId,
        int sourcePlayerId,
        PlayerClass classId,
        PlayerTeam team,
        DeadBodyAnimationKind animationKind,
        string gameplayClassId,
        bool facingLeft,
        float x,
        float y,
        float knockbackX,
        float knockbackY)
    {
        if (_dynamicRagdolls.ContainsKey(deadBodyId))
        {
            return;
        }

        if (_dynamicRagdolls.Count >= MaxDynamicRagdolls)
        {
            TrimDynamicRagdollCapacity(forceRemoveOne: true);
        }

        if (!TryResolveRagdollOpaqueBounds(
                gameplayClassId,
                classId,
                team,
                animationKind,
                out var opaqueBounds,
                out var collisionHalfWidth,
                out var collisionHalfHeight))
        {
            opaqueBounds = new Rectangle(0, 0, 24, 12);
            collisionHalfWidth = 12f;
            collisionHalfHeight = 6f;
        }

        var useElkondoVisual = UsesElkondoRagdollVisual()
            && TryResolveElkondoCorpseSprite(gameplayClassId, classId, team, out _, out _, out _);

        // Elkondo starts from the living upright pose; Kelly DeadS stands head-up then tips over.
        var spawnRotation = useElkondoVisual
            ? 0f
            : facingLeft ? -90f : 90f;
        var launchX = knockbackX * DynamicRagdollLaunchSpeedScale;
        var launchY = knockbackY * DynamicRagdollLaunchSpeedScale;
        var launchSpeed = MathF.Sqrt((launchX * launchX) + (launchY * launchY));
        if (launchSpeed < DynamicRagdollMinLaunchSpeed)
        {
            // Opposite of facing so the fallback still reads as shot-away, not into the aim.
            var awaySign = facingLeft ? 1f : -1f;
            launchX = awaySign * (CorpseKnockbackRules.MinimumSpeed * DynamicRagdollLaunchSpeedScale);
            launchY = -2.8f - (_dynamicRagdollRandom.NextSingle() * 2.0f);
            launchSpeed = MathF.Sqrt((launchX * launchX) + (launchY * launchY));
        }
        else
        {
            // Bias a meaty upward burst so it doesn't just crumple in place.
            launchY -= 1.6f + (_dynamicRagdollRandom.NextSingle() * 1.2f);
        }

        // Keep a noticeable horizontal shove even when the supplied knockback was weak/slow.
        var minHorizontal = CorpseKnockbackRules.MinimumSpeed * DynamicRagdollLaunchSpeedScale * 0.85f;
        if (MathF.Abs(launchX) < minHorizontal)
        {
            var awaySign = launchX != 0f
                ? MathF.Sign(launchX)
                : (facingLeft ? 1f : -1f);
            launchX = awaySign * minHorizontal;
            launchSpeed = MathF.Sqrt((launchX * launchX) + (launchY * launchY));
        }

        // Never spawn with net downward launch — overlapping floors + down velocity looks like a slam under the map.
        launchY = MathF.Min(launchY, -2.0f);

        // Tip/fold WITH the knockback so the corpse leans away from the bullet, not into it.
        var tipSign = launchX >= 0f ? 1f : -1f;

        var ragdoll = new DynamicRagdollState
        {
            DeadBodyId = deadBodyId,
            SourcePlayerId = sourcePlayerId,
            ClassId = classId,
            Team = team,
            AnimationKind = animationKind,
            GameplayClassId = gameplayClassId ?? string.Empty,
            FacingLeft = facingLeft,
            X = x,
            Y = y,
            VelocityX = launchX,
            VelocityY = launchY,
            RotationDegrees = spawnRotation,
            // Light tip from the kill shot — enough to flop, not enough to keep rolling on the ground.
            AngularVelocityDegrees = tipSign * (2.2f + (launchSpeed * 0.28f)),
            OpaqueBounds = opaqueBounds,
            CollisionHalfWidth = collisionHalfWidth,
            CollisionHalfHeight = collisionHalfHeight,
            UseElkondoVerticalVisual = useElkondoVisual,
        };

        ApplyDeathShotImpulseThroughWaist(ragdoll, launchX, launchY, tipSign, useElkondoVisual);

        var sourcePlayer = FindPlayerById(sourcePlayerId);
        if (useElkondoVisual)
        {
            TryCaptureElkondoRagdollWeapon(ragdoll, sourcePlayer);
        }

        _dynamicRagdolls[deadBodyId] = ragdoll;
    }

    /// <summary>
    /// Kill-shot knockback is applied at the waist; chest and knee are flung as the chain reacts.
    /// </summary>
    private void ApplyDeathShotImpulseThroughWaist(
        DynamicRagdollState ragdoll,
        float launchX,
        float launchY,
        float tipSign,
        bool useElkondoVisual)
    {
        var launchSpeed = MathF.Sqrt((launchX * launchX) + (launchY * launchY));
        var shotStrength = MathF.Max(DynamicRagdollMinLaunchSpeed, launchSpeed);

        // Waist absorbs the hit — primary joint velocity from the shot.
        var waistVelocity = tipSign * (12f + (shotStrength * 2.8f));
        // Chest counters the waist whip hard so the upper torso visibly folds.
        var chestVelocity = -waistVelocity * 0.9f;
        var kneeBendSign = ragdoll.FacingLeft ? -1f : 1f;
        // Knees buckle backward with the impact.
        var kneeVelocity = kneeBendSign * MathF.Abs(waistVelocity) * 1.15f;

        for (var pivotIndex = 0; pivotIndex < DynamicRagdollPivotCount; pivotIndex += 1)
        {
            ragdoll.RestPivotDegrees[pivotIndex] = 0f;
            ragdoll.PivotDegrees[pivotIndex] = 0f;
        }

        if (useElkondoVisual)
        {
            ragdoll.PivotVelocities[ElkondoPivotWaist] = waistVelocity;
            ragdoll.PivotVelocities[ElkondoPivotChest] = chestVelocity;
            ragdoll.PivotVelocities[ElkondoPivotKnee] = kneeVelocity;

            // Readable immediate bends on all three joints (not just waist).
            ragdoll.PivotDegrees[ElkondoPivotWaist] = tipSign * MathF.Min(28f, 8f + (shotStrength * 1.35f));
            ragdoll.PivotDegrees[ElkondoPivotChest] = -ragdoll.PivotDegrees[ElkondoPivotWaist] * 0.7f;
            ragdoll.PivotDegrees[ElkondoPivotKnee] = kneeBendSign * MathF.Min(70f, 18f + (shotStrength * 2.0f));

            for (var pivotIndex = 0; pivotIndex < DynamicRagdollPivotCount; pivotIndex += 1)
            {
                ragdoll.PivotDegrees[pivotIndex] = ClampElkondoPivotDegrees(
                    ragdoll,
                    pivotIndex,
                    ragdoll.PivotDegrees[pivotIndex]);
            }

            // Keep a soft bent rest so springs don't yank chest/knee flat mid-flight.
            ragdoll.RestPivotDegrees[ElkondoPivotChest] = ragdoll.PivotDegrees[ElkondoPivotChest] * 0.45f;
            ragdoll.RestPivotDegrees[ElkondoPivotWaist] = ragdoll.PivotDegrees[ElkondoPivotWaist] * 0.35f;
            ragdoll.RestPivotDegrees[ElkondoPivotKnee] = ragdoll.PivotDegrees[ElkondoPivotKnee] * 0.55f;
        }
        else
        {
            // Kelly DeadS: treat middle pivot as the waist hit point.
            ragdoll.PivotVelocities[1] = waistVelocity;
            ragdoll.PivotVelocities[0] = chestVelocity;
            ragdoll.PivotVelocities[2] = -waistVelocity * 0.65f;
            ragdoll.PivotDegrees[1] = tipSign * MathF.Min(20f, 5f + (shotStrength * 1.2f));
            ragdoll.PivotDegrees[0] = -ragdoll.PivotDegrees[1] * 0.4f;
            ragdoll.PivotDegrees[2] = -ragdoll.PivotDegrees[1] * 0.55f;
            for (var pivotIndex = 0; pivotIndex < DynamicRagdollPivotCount; pivotIndex += 1)
            {
                ragdoll.PivotDegrees[pivotIndex] = Math.Clamp(
                    ragdoll.PivotDegrees[pivotIndex],
                    -DynamicRagdollMaxPivotDegrees,
                    DynamicRagdollMaxPivotDegrees);
            }
        }
    }

    private bool TryResolveRagdollOpaqueBounds(
        string gameplayClassId,
        PlayerClass classId,
        PlayerTeam team,
        DeadBodyAnimationKind animationKind,
        out Rectangle opaqueBounds,
        out float collisionHalfWidth,
        out float collisionHalfHeight)
    {
        opaqueBounds = default;
        collisionHalfWidth = 8f;
        collisionHalfHeight = 6f;

        string? spriteName;
        var frameIndex = 0;
        if (UsesElkondoRagdollVisual()
            && TryResolveElkondoCorpseSprite(gameplayClassId, classId, team, out var elkondoSprite, out frameIndex, out _))
        {
            spriteName = elkondoSprite;
        }
        else
        {
            spriteName = GetDeadBodySpriteName(gameplayClassId, classId, team, animationKind);
        }

        if (spriteName is null)
        {
            return false;
        }

        var sprite = GetResolvedSprite(spriteName);
        if (sprite is null || sprite.Frames.Count == 0)
        {
            return false;
        }

        var frame = sprite.Frames[Math.Clamp(frameIndex, 0, sprite.Frames.Count - 1)];
        if (frame.OpaqueBounds is { Width: > 0, Height: > 0 } frameOpaque)
        {
            opaqueBounds = frameOpaque;
        }
        else if (TryGetSpriteFramePixels(frame, out var pixels, out var width, out var height)
                 && TryComputeOpaqueBounds(pixels, width, height, out opaqueBounds))
        {
        }
        else
        {
            opaqueBounds = new Rectangle(0, 0, frame.Width, frame.Height);
        }

        // Collision stays player-sized. Full opaque halves (especially Elkondo standing run poses)
        // eject corpses when the rotated AABB clips vertical wall solids.
        collisionHalfWidth = MathF.Min(
            DynamicRagdollMaxCollisionHalfWidth,
            MathF.Max(4f, opaqueBounds.Width * 0.5f));
        collisionHalfHeight = MathF.Min(
            DynamicRagdollMaxCollisionHalfHeight,
            MathF.Max(3f, opaqueBounds.Height * 0.5f));
        return true;
    }

    private static bool TryComputeOpaqueBounds(Color[] pixels, int width, int height, out Rectangle bounds)
    {
        var minX = width;
        var minY = height;
        var maxX = -1;
        var maxY = -1;
        for (var y = 0; y < height; y += 1)
        {
            var row = y * width;
            for (var x = 0; x < width; x += 1)
            {
                if (pixels[row + x].A < DynamicRagdollOpaqueAlphaThreshold)
                {
                    continue;
                }

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        if (maxX < minX || maxY < minY)
        {
            bounds = default;
            return false;
        }

        bounds = new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
        return true;
    }

    private void TrimDynamicRagdollCapacity(bool forceRemoveOne = false)
    {
        while (_dynamicRagdolls.Count > MaxDynamicRagdolls || (forceRemoveOne && _dynamicRagdolls.Count > 0))
        {
            var oldestId = 0;
            var oldestAge = int.MinValue;
            var found = false;
            foreach (var pair in _dynamicRagdolls)
            {
                if (!found || pair.Value.AgeTicks > oldestAge)
                {
                    found = true;
                    oldestAge = pair.Value.AgeTicks;
                    oldestId = pair.Key;
                }
            }

            if (!found)
            {
                break;
            }

            _dynamicRagdolls.Remove(oldestId);
            if (forceRemoveOne)
            {
                break;
            }
        }
    }

    private static void AdvanceRagdollPivots(DynamicRagdollState ragdoll, bool hitGround)
    {
        var impactJiggle = hitGround && MathF.Abs(ragdoll.VelocityY) > 0.8f
            ? MathF.CopySign(MathF.Min(12f, MathF.Abs(ragdoll.VelocityY) * 1.8f), -ragdoll.AngularVelocityDegrees)
            : 0f;
        // Keep a little spin→pivot coupling on the ground so chest/knee still settle with the flop.
        var spinDrive = ragdoll.AngularVelocityDegrees
            * (hitGround ? DynamicRagdollAngularPivotCoupling * 0.35f : DynamicRagdollAngularPivotCoupling);

        for (var index = 0; index < DynamicRagdollPivotCount; index += 1)
        {
            // Chest and knee are leafier — less falloff so they actually whip.
            var segmentFalloff = ragdoll.UseElkondoVerticalVisual
                ? index switch
                {
                    ElkondoPivotChest => 1.05f,
                    ElkondoPivotWaist => 1f,
                    ElkondoPivotKnee => 1.15f,
                    _ => 1f - (index * 0.16f),
                }
                : 1f - (index * 0.16f);

            if (impactJiggle != 0f)
            {
                ragdoll.PivotVelocities[index] += impactJiggle * segmentFalloff;
            }

            ragdoll.PivotVelocities[index] += spinDrive * segmentFalloff;
            if ((index & 1) == 1)
            {
                ragdoll.PivotVelocities[index] -= spinDrive * 0.4f;
            }

            // Softer springs on chest/knee so they don't snap flat before you see them bend.
            var spring = hitGround ? DynamicRagdollPivotSpringGround : DynamicRagdollPivotSpringAir;
            if (ragdoll.UseElkondoVerticalVisual)
            {
                spring *= index switch
                {
                    ElkondoPivotChest => 0.55f,
                    ElkondoPivotKnee => 0.4f,
                    _ => 0.85f,
                };
            }

            var rest = ragdoll.RestPivotDegrees[index];
            ragdoll.PivotVelocities[index] += (rest - ragdoll.PivotDegrees[index]) * spring;
            var damping = DynamicRagdollPivotDamping;
            if (ragdoll.UseElkondoVerticalVisual && (index == ElkondoPivotChest || index == ElkondoPivotKnee))
            {
                damping = 0.94f;
            }

            ragdoll.PivotVelocities[index] *= damping;
            ragdoll.PivotDegrees[index] += ragdoll.PivotVelocities[index];
            ragdoll.PivotDegrees[index] = ClampRagdollPivotDegrees(ragdoll, index, ragdoll.PivotDegrees[index]);
            if (ragdoll.UseElkondoVerticalVisual)
            {
                GetElkondoPivotLimits(ragdoll, index, out var minDegrees, out var maxDegrees);
                if ((ragdoll.PivotDegrees[index] <= minDegrees + 0.01f && ragdoll.PivotVelocities[index] < 0f)
                    || (ragdoll.PivotDegrees[index] >= maxDegrees - 0.01f && ragdoll.PivotVelocities[index] > 0f))
                {
                    ragdoll.PivotVelocities[index] = 0f;
                }
            }
        }
    }

    private static float ClampRagdollPivotDegrees(DynamicRagdollState ragdoll, int pivotIndex, float degrees)
    {
        if (ragdoll.UseElkondoVerticalVisual)
        {
            return ClampElkondoPivotDegrees(ragdoll, pivotIndex, degrees);
        }

        return Math.Clamp(degrees, -DynamicRagdollMaxPivotDegrees, DynamicRagdollMaxPivotDegrees);
    }

    private static void AdaptRagdollRestPivotsToContacts(DynamicRagdollState ragdoll, SimpleLevel level)
    {
        Span<Vector2> nodes = stackalloc Vector2[DynamicRagdollCollisionNodeCount];
        var nodeCount = BuildRagdollCollisionNodes(ragdoll, nodes);
        if (nodeCount <= 0)
        {
            return;
        }

        var radius = DynamicRagdollSegmentRadius;
        Span<bool> grounded = stackalloc bool[DynamicRagdollCollisionNodeCount];
        var anyGround = false;
        var wallLeft = false;
        var wallRight = false;

        for (var nodeIndex = 0; nodeIndex < nodeCount; nodeIndex += 1)
        {
            var node = nodes[nodeIndex];
            foreach (var solid in level.Solids)
            {
                if (!RagdollNodeIntersectsSolid(node, radius, solid))
                {
                    continue;
                }

                var nodeBottom = node.Y + radius;
                if (nodeBottom <= solid.Top + radius * 1.35f
                    && node.X >= solid.Left - radius
                    && node.X <= solid.Right + radius)
                {
                    grounded[nodeIndex] = true;
                    anyGround = true;
                }
                else if (node.Y > solid.Top + radius && node.Y < solid.Bottom - radius)
                {
                    if (node.X < solid.Left + ((solid.Right - solid.Left) * 0.5f))
                    {
                        wallLeft = true;
                    }
                    else
                    {
                        wallRight = true;
                    }
                }
            }
        }

        if (!anyGround && !wallLeft && !wallRight)
        {
            return;
        }

        if (!wallLeft && !wallRight)
        {
            // Uneven ground: fold unsupported segments toward the grounded ones (meat drape).
            for (var index = 0; index < DynamicRagdollPivotCount && index + 1 < nodeCount; index += 1)
            {
                var aGround = grounded[index];
                var bGround = grounded[Math.Min(index + 1, nodeCount - 1)];
                if (aGround == bGround)
                {
                    // Keep a readable residual bend — especially chest/knee.
                    var keep = ragdoll.UseElkondoVerticalVisual && index != ElkondoPivotWaist ? 0.65f : 0.4f;
                    ragdoll.RestPivotDegrees[index] = MathHelper.Lerp(
                        ragdoll.RestPivotDegrees[index],
                        ragdoll.PivotDegrees[index] * keep,
                        0.1f);
                }
                else
                {
                    var foldSign = ragdoll.FacingLeft ? -1f : 1f;
                    if (!aGround && bGround)
                    {
                        foldSign = -foldSign;
                    }

                    float target;
                    if (ragdoll.UseElkondoVerticalVisual && index == ElkondoPivotKnee)
                    {
                        target = ragdoll.FacingLeft ? -55f : 55f;
                    }
                    else if (ragdoll.UseElkondoVerticalVisual && index == ElkondoPivotChest)
                    {
                        target = foldSign * 22f;
                    }
                    else
                    {
                        target = foldSign * (22f + (index * 10f));
                    }

                    ragdoll.RestPivotDegrees[index] = MathHelper.Lerp(ragdoll.RestPivotDegrees[index], target, 0.16f);
                }

                ragdoll.RestPivotDegrees[index] = ClampRagdollPivotDegrees(ragdoll, index, ragdoll.RestPivotDegrees[index]);
            }

            return;
        }

        var drapeSign = wallRight ? -1f : 1f;
        if (ragdoll.FacingLeft)
        {
            drapeSign = -drapeSign;
        }

        for (var index = 0; index < DynamicRagdollPivotCount; index += 1)
        {
            float target;
            if (ragdoll.UseElkondoVerticalVisual && index == ElkondoPivotKnee)
            {
                target = ragdoll.FacingLeft ? -55f : 55f;
            }
            else
            {
                target = drapeSign * (18f + (index * 8f));
                if ((index & 1) == 1)
                {
                    target *= -0.65f;
                }
            }

            ragdoll.RestPivotDegrees[index] = MathHelper.Lerp(ragdoll.RestPivotDegrees[index], target, 0.12f);
            ragdoll.RestPivotDegrees[index] = ClampRagdollPivotDegrees(ragdoll, index, ragdoll.RestPivotDegrees[index]);
        }
    }

    /// <summary>
    /// Circle nodes never collide with each other — only against world solids.
    /// Returns how many meat circles hang over empty space while others rest on ground.
    /// </summary>
    private static int CountRagdollHangingNodes(
        DynamicRagdollState ragdoll,
        SimpleLevel level,
        Span<Vector2> nodes,
        Span<bool> grounded,
        int nodeCount,
        out int groundedCount)
    {
        groundedCount = 0;
        var radius = DynamicRagdollSegmentRadius;
        for (var i = 0; i < nodeCount; i += 1)
        {
            grounded[i] = TryFindRagdollNodeFloor(nodes[i], radius, level, wasFalling: true, out _);
            if (grounded[i])
            {
                groundedCount += 1;
            }
        }

        if (groundedCount == 0 || groundedCount >= nodeCount)
        {
            return 0;
        }

        var hanging = 0;
        for (var i = 0; i < nodeCount; i += 1)
        {
            if (grounded[i])
            {
                continue;
            }

            // Unsupported and no floor within droop range → hanging off a ledge/cliff.
            if (!HasRagdollFloorWithinDepth(nodes[i], radius, level, DynamicRagdollHangProbeDepth))
            {
                hanging += 1;
                continue;
            }

            // Floor exists far below — still treat as hanging so we keep draping until contact.
            hanging += 1;
        }

        return hanging;
    }

    private static bool HasRagdollFloorWithinDepth(
        Vector2 node,
        float radius,
        SimpleLevel level,
        float maxDepth)
    {
        for (var depth = radius; depth <= maxDepth; depth += 4f)
        {
            var probe = new Vector2(node.X, node.Y + depth);
            if (TryFindRagdollNodeFloor(probe, radius, level, wasFalling: true, out var floorTop)
                && floorTop >= node.Y - radius
                && floorTop <= node.Y + maxDepth)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Pull unsupported chain ends downward with gravity torque at the pivots (ledge drape).
    /// </summary>
    private static void ApplyRagdollLedgeGravityDroop(
        DynamicRagdollState ragdoll,
        Span<Vector2> nodes,
        Span<bool> grounded,
        int nodeCount)
    {
        if (nodeCount < 2)
        {
            return;
        }

        var anyGrounded = false;
        for (var i = 0; i < nodeCount; i += 1)
        {
            if (grounded[i])
            {
                anyGrounded = true;
                break;
            }
        }

        if (!anyGrounded)
        {
            return;
        }

        var scaleX = ragdoll.FacingLeft ? -1f : 1f;
        // Feet hang → droop waist + knee. Head hangs → droop chest (+ waist slightly).
        var feetHang = !grounded[nodeCount - 1];
        var headHang = !grounded[0];
        if (!feetHang && !headHang)
        {
            // Middle unsupported only — still nudge waist if lower half hangs.
            for (var i = nodeCount / 2; i < nodeCount; i += 1)
            {
                if (!grounded[i])
                {
                    feetHang = true;
                    break;
                }
            }
        }

        if (feetHang)
        {
            DroopRagdollPivotTowardWorldDown(
                ragdoll,
                ragdoll.UseElkondoVerticalVisual ? ElkondoPivotWaist : 1,
                scaleX,
                DynamicRagdollLedgeDroopAccel);
            DroopRagdollPivotTowardWorldDown(
                ragdoll,
                ragdoll.UseElkondoVerticalVisual ? ElkondoPivotKnee : 2,
                scaleX,
                DynamicRagdollLedgeDroopAccel * 1.15f);
        }

        if (headHang)
        {
            DroopRagdollPivotTowardWorldDown(
                ragdoll,
                ragdoll.UseElkondoVerticalVisual ? ElkondoPivotChest : 0,
                scaleX,
                DynamicRagdollLedgeDroopAccel * 0.9f);
        }
    }

    private static void DroopRagdollPivotTowardWorldDown(
        DynamicRagdollState ragdoll,
        int pivotIndex,
        float scaleX,
        float accel)
    {
        if (pivotIndex < 0 || pivotIndex >= DynamicRagdollPivotCount)
        {
            return;
        }

        // Sample which pivot sign moves the distal chain tip downward (+Y).
        var baseRot = ragdoll.RotationDegrees;
        for (var i = 0; i < pivotIndex; i += 1)
        {
            baseRot += ragdoll.PivotDegrees[i];
        }

        var rot0 = (baseRot + ragdoll.PivotDegrees[pivotIndex]) * (MathF.PI / 180f);
        var tip0 = TransformRagdollLocal(new Vector2(0f, 12f), scaleX, rot0);
        var tipPlus = TransformRagdollLocal(
            new Vector2(0f, 12f),
            scaleX,
            rot0 + (8f * (MathF.PI / 180f)));
        var tipMinus = TransformRagdollLocal(
            new Vector2(0f, 12f),
            scaleX,
            rot0 - (8f * (MathF.PI / 180f)));
        var droopSign = tipPlus.Y >= tipMinus.Y ? 1f : -1f;
        if (ragdoll.UseElkondoVerticalVisual && pivotIndex == ElkondoPivotKnee)
        {
            // Prefer anatomical backward knee bend when it still drops the tip.
            var kneeSign = ragdoll.FacingLeft ? -1f : 1f;
            var tipKnee = TransformRagdollLocal(
                new Vector2(0f, 12f),
                scaleX,
                rot0 + (kneeSign * 8f * (MathF.PI / 180f)));
            if (tipKnee.Y + 1.5f >= MathF.Min(tipPlus.Y, tipMinus.Y))
            {
                droopSign = kneeSign;
            }
        }

        // If already pointing mostly down, ease off.
        if (tip0.Y > 8f)
        {
            accel *= 0.35f;
        }

        ragdoll.PivotVelocities[pivotIndex] += droopSign * accel;
        var restTarget = ClampRagdollPivotDegrees(
            ragdoll,
            pivotIndex,
            ragdoll.RestPivotDegrees[pivotIndex] + (droopSign * 12f));
        ragdoll.RestPivotDegrees[pivotIndex] = MathHelper.Lerp(
            ragdoll.RestPivotDegrees[pivotIndex],
            restTarget,
            0.22f);
        ragdoll.RestPivotDegrees[pivotIndex] = ClampRagdollPivotDegrees(
            ragdoll,
            pivotIndex,
            ragdoll.RestPivotDegrees[pivotIndex]);
    }

    /// <summary>
    /// Body-only chain collision. Waist circle is the primary weighted support; other circles
    /// drape/walls only and never prop the corpse into a standing pose. Circles never hit each other.
    /// </summary>
    private static bool AdvanceRagdollSegmentCollision(
        DynamicRagdollState ragdoll,
        SimpleLevel level,
        WorldBounds bounds)
    {
        ragdoll.X += ragdoll.VelocityX;
        ragdoll.Y += ragdoll.VelocityY;

        Span<Vector2> nodes = stackalloc Vector2[DynamicRagdollCollisionNodeCount];
        var nodeCount = BuildRagdollCollisionNodes(ragdoll, nodes);
        if (nodeCount <= 0)
        {
            ragdoll.X = bounds.ClampX(ragdoll.X, DynamicRagdollWaistRadius * 2f);
            ragdoll.Y = bounds.ClampY(ragdoll.Y, DynamicRagdollWaistRadius * 2f);
            return false;
        }

        var radius = DynamicRagdollSegmentRadius;
        var waistRadius = DynamicRagdollWaistRadius;
        var wasFalling = ragdoll.VelocityY >= -0.05f;
        var maxImpact = 0f;
        var waistWorld = GetRagdollWaistWorldPosition(ragdoll);
        var waistGrounded = false;

        // --- PRIMARY: plant the waist. Root follows so hips stay on the floor. ---
        if (TryFindRagdollNodeFloor(waistWorld, waistRadius, level, wasFalling, out var waistFloorTop))
        {
            var waistPushY = waistFloorTop - waistRadius - waistWorld.Y;
            if (waistPushY <= waistRadius * 0.5f)
            {
                ragdoll.Y += Math.Clamp(waistPushY, -12f, 3f);
                waistGrounded = true;
                maxImpact = MathF.Max(maxImpact, MathF.Max(0f, ragdoll.VelocityY));
                waistWorld = GetRagdollWaistWorldPosition(ragdoll);
            }
        }

        nodeCount = BuildRagdollCollisionNodes(ragdoll, nodes);
        Span<bool> nodeGrounded = stackalloc bool[DynamicRagdollCollisionNodeCount];
        var groundedNodes = waistGrounded ? 1 : 0;

        // Secondary nodes: detect contacts for drape, but do NOT lift the root off a planted waist.
        for (var nodeIndex = 0; nodeIndex < nodeCount; nodeIndex += 1)
        {
            var node = nodes[nodeIndex];
            if (!TryFindRagdollNodeFloor(node, radius, level, wasFalling: true, out var floorTop))
            {
                continue;
            }

            nodeGrounded[nodeIndex] = true;
            if (!waistGrounded)
            {
                groundedNodes += 1;
                continue;
            }

            // Waist already supports the body — secondary floor contacts only nudge if they'd
            // sink deeper into the ground (never push the hips back up into a stand).
            var pushY = floorTop - radius - node.Y;
            if (pushY < -1.5f)
            {
                // Soft sink correction shared lightly so limbs don't clip through floors.
                ragdoll.Y += Math.Clamp(pushY * 0.15f, -2f, 0f);
            }
        }

        // Waist still airborne but feet/head found floor → tip over, pull hips down (more gravity).
        if (!waistGrounded)
        {
            var secondaryFloor = false;
            for (var i = 0; i < nodeCount; i += 1)
            {
                if (nodeGrounded[i])
                {
                    secondaryFloor = true;
                    break;
                }
            }

            if (secondaryFloor)
            {
                ragdoll.VelocityY = MathF.Min(DynamicRagdollMaxFallSpeed, ragdoll.VelocityY + DynamicRagdollGravity * 0.85f);
                groundedNodes = Math.Max(groundedNodes, 1);
            }
            else if (TryFindRagdollNodeFloor(
                         new Vector2(waistWorld.X, waistWorld.Y + 10f),
                         waistRadius,
                         level,
                         wasFalling: true,
                         out var nearFloor))
            {
                // Snap-seek waist toward nearby ground under the hips.
                var seek = nearFloor - waistRadius - waistWorld.Y;
                if (seek < 0f)
                {
                    ragdoll.Y += Math.Clamp(seek * 0.45f, -8f, 0f);
                    waistGrounded = true;
                    groundedNodes = Math.Max(groundedNodes, 1);
                }
            }
        }

        // --- Wall pass: secondary circles + waist; never lift hanging/secondary meat onto tops. ---
        nodeCount = BuildRagdollCollisionNodes(ragdoll, nodes);
        waistWorld = GetRagdollWaistWorldPosition(ragdoll);
        var wallPushX = 0f;
        var wallPushCount = 0;
        var hitWall = false;

        if (TryResolveRagdollNodeWall(waistWorld, waistRadius, level, ragdoll.VelocityX, out var waistWallX, out var waistWallY))
        {
            if (waistWallY < 0f)
            {
                waistWallY = 0f;
            }

            ragdoll.X += Math.Clamp(waistWallX, -8f, 8f);
            ragdoll.Y += Math.Clamp(waistWallY, -4f, 4f);
            if (MathF.Abs(waistWallX) > 0.01f)
            {
                hitWall = true;
            }
        }

        for (var nodeIndex = 0; nodeIndex < nodeCount; nodeIndex += 1)
        {
            if (nodeGrounded[nodeIndex] && waistGrounded)
            {
                continue;
            }

            var node = nodes[nodeIndex];
            if (!TryResolveRagdollNodeWall(node, radius, level, ragdoll.VelocityX, out var pushX, out var pushY))
            {
                continue;
            }

            if (pushY < 0f)
            {
                pushY = 0f;
            }

            wallPushX += Math.Clamp(pushX, -6f, 6f);
            wallPushCount += 1;
            if (MathF.Abs(pushX) > 0.01f)
            {
                hitWall = true;
            }

            _ = pushY;
        }

        if (wallPushCount > 0)
        {
            ragdoll.X += (wallPushX / wallPushCount) * 0.65f;
        }

        // Final: keep waist planted if we have a floor under the hips.
        waistWorld = GetRagdollWaistWorldPosition(ragdoll);
        if (TryFindRagdollNodeFloor(waistWorld, waistRadius, level, wasFalling: true, out var finalWaistTop))
        {
            var finalPush = finalWaistTop - waistRadius - waistWorld.Y;
            if (finalPush <= waistRadius * 0.55f)
            {
                ragdoll.Y += Math.Clamp(finalPush, -10f, 2f);
                waistGrounded = true;
                groundedNodes = Math.Max(groundedNodes, 1);
            }
        }

        var hitGround = waistGrounded || groundedNodes > 0;
        if (waistGrounded)
        {
            if (maxImpact > 1.0f)
            {
                ragdoll.VelocityY = -maxImpact * DynamicRagdollGroundBounce;
                ExciteRagdollPivots(ragdoll, maxImpact * 1.4f);
            }
            else if (ragdoll.VelocityY > 0f)
            {
                ragdoll.VelocityY = 0f;
            }

            ragdoll.AngularVelocityDegrees *= 0.55f;
        }
        else if (hitWall)
        {
            ragdoll.VelocityX *= -DynamicRagdollWallBounce;
            ExciteRagdollPivots(ragdoll, 5f);
        }

        var span = MathF.Max(waistRadius * 2f, EstimateRagdollChainSpan(ragdoll) * 0.35f);
        var clampedX = bounds.ClampX(ragdoll.X, span);
        if (clampedX != ragdoll.X)
        {
            ragdoll.X = clampedX;
            ragdoll.VelocityX *= -DynamicRagdollWallBounce;
        }

        var clampedY = bounds.ClampY(ragdoll.Y, span);
        if (clampedY != ragdoll.Y)
        {
            if (wasFalling && clampedY < ragdoll.Y)
            {
                hitGround = true;
                ragdoll.VelocityY = ragdoll.VelocityY > 1f
                    ? -ragdoll.VelocityY * DynamicRagdollGroundBounce
                    : 0f;
                ragdoll.AngularVelocityDegrees *= 0.55f;
            }
            else
            {
                ragdoll.VelocityY = 0f;
            }

            ragdoll.Y = clampedY;
        }

        return hitGround;
    }

    /// <summary>
    /// Highest solid Top this node can stand on (stairs/ledges pick the upper tread, not a lower slab).
    /// </summary>
    private static bool TryFindRagdollNodeFloor(
        Vector2 node,
        float radius,
        SimpleLevel level,
        bool wasFalling,
        out float floorTop)
    {
        floorTop = float.MinValue;
        var found = false;
        var nodeBottom = node.Y + radius;
        var maxEmbed = wasFalling ? 16f : 10f;

        foreach (var solid in level.Solids)
        {
            if (node.X < solid.Left - radius || node.X > solid.Right + radius)
            {
                continue;
            }

            var solidHeight = solid.Bottom - solid.Top;
            if (solidHeight <= 0.01f)
            {
                continue;
            }

            // Must be near the top face — not deep inside a tall wall block.
            var embed = nodeBottom - solid.Top;
            if (embed < -radius * 0.75f || embed > maxEmbed)
            {
                continue;
            }

            // Reject if clearly a side hit against a tall riser (center beside the face, deep vertically).
            var distToLeft = MathF.Abs((node.X + radius) - solid.Left);
            var distToRight = MathF.Abs((node.X - radius) - solid.Right);
            var distToTop = MathF.Abs(embed);
            if (distToTop > distToLeft + 1.25f && distToTop > distToRight + 1.25f && embed > radius * 1.1f)
            {
                continue;
            }

            if (solid.Top >= floorTop)
            {
                floorTop = solid.Top;
                found = true;
            }
        }

        return found;
    }

    private static bool TryResolveRagdollNodeWall(
        Vector2 node,
        float radius,
        SimpleLevel level,
        float velocityX,
        out float pushX,
        out float pushY)
    {
        pushX = 0f;
        pushY = 0f;
        var bestPen = float.MaxValue;
        var found = false;

        foreach (var solid in level.Solids)
        {
            if (!RagdollNodeIntersectsSolid(node, radius, solid))
            {
                continue;
            }

            // Skip if this is really a floor contact (near Top while over the solid).
            if (node.X >= solid.Left - radius
                && node.X <= solid.Right + radius
                && (node.Y + radius) <= solid.Top + radius * 1.25f
                && (node.Y + radius) >= solid.Top - radius * 0.5f)
            {
                continue;
            }

            var distToLeft = MathF.Abs((node.X + radius) - solid.Left);
            var distToRight = MathF.Abs((node.X - radius) - solid.Right);
            var distToTop = MathF.Abs((node.Y + radius) - solid.Top);
            var distToBottom = MathF.Abs((node.Y - radius) - solid.Bottom);

            // Prefer the shallowest side separation; never use Bottom (that's the old under-map fling).
            if (distToTop <= distToLeft && distToTop <= distToRight && distToTop <= 4f)
            {
                // Shallow top graze — treat as floor, not wall.
                continue;
            }

            float candidateX;
            float candidateY = 0f;
            float pen;
            if (distToLeft <= distToRight && (velocityX >= -0.01f || distToLeft < distToRight - 0.5f))
            {
                candidateX = solid.Left - radius - node.X;
                pen = MathF.Abs(candidateX);
            }
            else
            {
                candidateX = solid.Right + radius - node.X;
                pen = MathF.Abs(candidateX);
            }

            // If deeply overlapping vertically past the top, still don't push to Bottom —
            // lift toward Top when that penetration is smaller.
            if (distToTop < pen && node.X >= solid.Left && node.X <= solid.Right)
            {
                candidateX = 0f;
                candidateY = solid.Top - radius - node.Y;
                pen = MathF.Abs(candidateY);
                if (candidateY > 2f)
                {
                    continue;
                }
            }

            if (pen >= bestPen)
            {
                continue;
            }

            bestPen = pen;
            pushX = candidateX;
            pushY = candidateY;
            found = true;
            _ = distToBottom;
        }

        return found;
    }

    /// <summary>
    /// World position of the waist joint — the primary support / mass center of the corpse.
    /// </summary>
    private static Vector2 GetRagdollWaistWorldPosition(DynamicRagdollState ragdoll)
    {
        var opaque = ragdoll.OpaqueBounds;
        if (opaque.Width <= 1 || opaque.Height <= 1)
        {
            return new Vector2(ragdoll.X, ragdoll.Y);
        }

        var scaleX = ragdoll.FacingLeft ? -1f : 1f;
        var root = new Vector2(ragdoll.X, ragdoll.Y);
        var bodyRotationRadians = ragdoll.RotationDegrees * (MathF.PI / 180f);

        if (ragdoll.UseElkondoVerticalVisual)
        {
            var waistFraction = GetElkondoWaistFraction(ragdoll.ClassId);
            var chestFraction = Math.Clamp(waistFraction * 0.48f, 0.16f, waistFraction - 0.10f);
            Span<float> cutOffsets = stackalloc float[]
            {
                0f,
                opaque.Height * chestFraction,
                opaque.Height * waistFraction,
            };

            var cursor = root + TransformRagdollLocal(
                new Vector2(0f, -opaque.Height * 0.5f),
                scaleX,
                bodyRotationRadians);
            var cumulativePivot = 0f;
            for (var segmentIndex = 0; segmentIndex < cutOffsets.Length - 1; segmentIndex += 1)
            {
                var segmentLength = cutOffsets[segmentIndex + 1] - cutOffsets[segmentIndex];
                var rotationRadians = (ragdoll.RotationDegrees + cumulativePivot) * (MathF.PI / 180f);
                cursor += TransformRagdollLocal(new Vector2(0f, segmentLength), scaleX, rotationRadians);
                if (segmentIndex < DynamicRagdollPivotCount)
                {
                    cumulativePivot += ragdoll.PivotDegrees[segmentIndex];
                }
            }

            return cursor; // waist joint
        }

        // Kelly: middle of opaque span (waist-ish along DeadS).
        var midFraction = DynamicRagdollPivotFractions[1];
        var cursorH = root + TransformRagdollLocal(
            new Vector2(-opaque.Width * 0.5f, 0f),
            scaleX,
            bodyRotationRadians);
        var cumulativePivotH = 0f;
        var traveled = 0f;
        var target = opaque.Width * midFraction;
        for (var pivotIndex = 0; pivotIndex <= DynamicRagdollPivotCount; pivotIndex += 1)
        {
            var nextCut = pivotIndex < DynamicRagdollPivotCount
                ? opaque.Width * DynamicRagdollPivotFractions[pivotIndex]
                : opaque.Width;
            var segmentWidth = nextCut - traveled;
            if (traveled + segmentWidth >= target)
            {
                var remain = target - traveled;
                var rotationRadians = (ragdoll.RotationDegrees + cumulativePivotH) * (MathF.PI / 180f);
                return cursorH + TransformRagdollLocal(new Vector2(remain, 0f), scaleX, rotationRadians);
            }

            var rotationRad = (ragdoll.RotationDegrees + cumulativePivotH) * (MathF.PI / 180f);
            cursorH += TransformRagdollLocal(new Vector2(segmentWidth, 0f), scaleX, rotationRad);
            traveled = nextCut;
            if (pivotIndex < DynamicRagdollPivotCount)
            {
                cumulativePivotH += ragdoll.PivotDegrees[pivotIndex];
            }
        }

        return cursorH;
    }

    /// <summary>
    /// Once the waist is planted, bias secondary pivots toward a laid-out corpse (not standing).
    /// </summary>
    private static void ApplyRagdollLyingPoseBias(DynamicRagdollState ragdoll)
    {
        // Mild flatten: don't yank to zero (keeps meat sag), but kill upright jackknife rests.
        for (var index = 0; index < DynamicRagdollPivotCount; index += 1)
        {
            var flatten = ragdoll.UseElkondoVerticalVisual && index == ElkondoPivotKnee
                ? ragdoll.PivotDegrees[index] * 0.75f // keep some knee bend while lying
                : ragdoll.PivotDegrees[index] * 0.5f;
            ragdoll.RestPivotDegrees[index] = MathHelper.Lerp(
                ragdoll.RestPivotDegrees[index],
                flatten,
                0.12f);
            ragdoll.RestPivotDegrees[index] = ClampRagdollPivotDegrees(
                ragdoll,
                index,
                ragdoll.RestPivotDegrees[index]);
        }
    }

    /// <summary>
    /// World-space centers of each opaque body segment along the pivot chain (no weapon).
    /// </summary>
    private static int BuildRagdollCollisionNodes(DynamicRagdollState ragdoll, Span<Vector2> nodes)
    {
        var opaque = ragdoll.OpaqueBounds;
        if (opaque.Width <= 1 || opaque.Height <= 1)
        {
            nodes[0] = new Vector2(ragdoll.X, ragdoll.Y);
            return 1;
        }

        var scaleX = ragdoll.FacingLeft ? -1f : 1f;
        var root = new Vector2(ragdoll.X, ragdoll.Y);
        var bodyRotationRadians = ragdoll.RotationDegrees * (MathF.PI / 180f);

        if (ragdoll.UseElkondoVerticalVisual)
        {
            var waistFraction = GetElkondoWaistFraction(ragdoll.ClassId);
            var chestFraction = Math.Clamp(waistFraction * 0.48f, 0.16f, waistFraction - 0.10f);
            var kneeFraction = Math.Clamp(waistFraction + ((1f - waistFraction) * 0.50f), waistFraction + 0.10f, 0.90f);
            Span<float> cutFractions = stackalloc float[]
            {
                0f,
                chestFraction,
                waistFraction,
                kneeFraction,
                1f,
            };

            Span<float> cutOffsets = stackalloc float[cutFractions.Length];
            for (var index = 0; index < cutFractions.Length; index += 1)
            {
                cutOffsets[index] = opaque.Height * cutFractions[index];
            }

            var cursor = root + TransformRagdollLocal(
                new Vector2(0f, -opaque.Height * 0.5f),
                scaleX,
                bodyRotationRadians);
            var cumulativePivot = 0f;
            var nodeCount = 0;
            for (var segmentIndex = 0; segmentIndex < cutOffsets.Length - 1; segmentIndex += 1)
            {
                var segmentLength = cutOffsets[segmentIndex + 1] - cutOffsets[segmentIndex];
                var rotationDegrees = ragdoll.RotationDegrees + cumulativePivot;
                var rotationRadians = rotationDegrees * (MathF.PI / 180f);
                var next = cursor + TransformRagdollLocal(new Vector2(0f, segmentLength), scaleX, rotationRadians);
                if (nodeCount < nodes.Length)
                {
                    nodes[nodeCount] = (cursor + next) * 0.5f;
                    nodeCount += 1;
                }

                cursor = next;
                if (segmentIndex < DynamicRagdollPivotCount)
                {
                    cumulativePivot += ragdoll.PivotDegrees[segmentIndex];
                }
            }

            return nodeCount;
        }

        // Kelly DeadS: horizontal opaque spine.
        var cutCount = DynamicRagdollPivotCount + 2;
        Span<float> cutXs = stackalloc float[cutCount];
        cutXs[0] = 0f;
        for (var pivotIndex = 0; pivotIndex < DynamicRagdollPivotCount; pivotIndex += 1)
        {
            cutXs[pivotIndex + 1] = opaque.Width * DynamicRagdollPivotFractions[pivotIndex];
        }

        cutXs[DynamicRagdollPivotCount + 1] = opaque.Width;

        var cursorH = root + TransformRagdollLocal(
            new Vector2(-opaque.Width * 0.5f, 0f),
            scaleX,
            bodyRotationRadians);
        var cumulativePivotH = 0f;
        var nodeCountH = 0;
        for (var segmentIndex = 0; segmentIndex < cutCount - 1; segmentIndex += 1)
        {
            var segmentWidth = cutXs[segmentIndex + 1] - cutXs[segmentIndex];
            var rotationDegrees = ragdoll.RotationDegrees + cumulativePivotH;
            var rotationRadians = rotationDegrees * (MathF.PI / 180f);
            var next = cursorH + TransformRagdollLocal(new Vector2(segmentWidth, 0f), scaleX, rotationRadians);
            if (nodeCountH < nodes.Length)
            {
                nodes[nodeCountH] = (cursorH + next) * 0.5f;
                nodeCountH += 1;
            }

            cursorH = next;
            if (segmentIndex < DynamicRagdollPivotCount)
            {
                cumulativePivotH += ragdoll.PivotDegrees[segmentIndex];
            }
        }

        return nodeCountH;
    }

    private static float EstimateRagdollChainSpan(DynamicRagdollState ragdoll)
    {
        var opaque = ragdoll.OpaqueBounds;
        if (ragdoll.UseElkondoVerticalVisual)
        {
            return MathF.Max(opaque.Height, opaque.Width);
        }

        return MathF.Max(opaque.Width, opaque.Height);
    }

    private static float GetRagdollLieTargetDegrees(DynamicRagdollState ragdoll)
    {
        // Tip onto the side the corpse is already leaning toward.
        if (ragdoll.UseElkondoVerticalVisual)
        {
            return NormalizeRagdollRotationDegrees(ragdoll.RotationDegrees) >= 0f ? 90f : -90f;
        }

        // Kelly already spawns near ±90; settle toward the nearer flat pose.
        return MathF.Abs(NormalizeRagdollRotationDegrees(ragdoll.RotationDegrees - 90f))
            <= MathF.Abs(NormalizeRagdollRotationDegrees(ragdoll.RotationDegrees + 90f))
            ? 90f
            : -90f;
    }

    private static bool RagdollNodeIntersectsSolid(Vector2 node, float radius, LevelSolid solid)
    {
        var nearestX = Math.Clamp(node.X, solid.Left, solid.Right);
        var nearestY = Math.Clamp(node.Y, solid.Top, solid.Bottom);
        var dx = node.X - nearestX;
        var dy = node.Y - nearestY;
        return (dx * dx) + (dy * dy) <= radius * radius;
    }

    private static void ExciteRagdollPivots(DynamicRagdollState ragdoll, float strength)
    {
        var sign = ragdoll.AngularVelocityDegrees >= 0f ? 1f : -1f;
        for (var index = 0; index < DynamicRagdollPivotCount; index += 1)
        {
            var phase = (index & 1) == 0 ? 1f : -1f;
            var weight = ragdoll.UseElkondoVerticalVisual
                ? index switch
                {
                    ElkondoPivotChest => 1.1f,
                    ElkondoPivotKnee => 1.25f,
                    _ => 0.9f,
                }
                : (0.85f - (index * 0.12f));
            ragdoll.PivotVelocities[index] += sign * phase * strength * weight;
        }
    }

    private static float NormalizeRagdollRotationDegrees(float degrees)
    {
        while (degrees > 180f)
        {
            degrees -= 360f;
        }

        while (degrees < -180f)
        {
            degrees += 360f;
        }

        return degrees;
    }

    private bool TryDrawDynamicRagdoll(
        int deadBodyId,
        int sourcePlayerId,
        PlayerClass classId,
        PlayerTeam team,
        DeadBodyAnimationKind animationKind,
        float x,
        float y,
        float width,
        float height,
        bool facingLeft,
        string gameplayClassId,
        int ticksRemaining,
        Vector2 cameraPosition)
    {
        if (!_dynamicRagdollEnabled)
        {
            return false;
        }

        if (!_dynamicRagdolls.TryGetValue(deadBodyId, out var ragdoll))
        {
            // Draw can see the authoritative corpse before Sync re-keys the immediate ragdoll.
            var syntheticId = -Math.Abs(sourcePlayerId);
            if (deadBodyId > 0
                && _dynamicRagdolls.Remove(syntheticId, out var transferred))
            {
                _dynamicRagdolls[deadBodyId] = transferred;
                ragdoll = transferred;
            }
            else
            {
                SpawnDynamicRagdoll(
                    deadBodyId,
                    sourcePlayerId,
                    classId,
                    team,
                    animationKind,
                    gameplayClassId,
                    facingLeft,
                    x,
                    y,
                    // Opposite of facing ≈ away from whoever they were aiming at.
                    knockbackX: facingLeft
                        ? CorpseKnockbackRules.MinimumSpeed * 0.75f
                        : -CorpseKnockbackRules.MinimumSpeed * 0.75f,
                    knockbackY: -2.2f);
                if (!_dynamicRagdolls.TryGetValue(deadBodyId, out ragdoll))
                {
                    return false;
                }
            }
        }

        if (TryDrawCorpseAcidDissolve(
                deadBodyId,
                ragdoll.GameplayClassId,
                ragdoll.ClassId,
                ragdoll.Team,
                ragdoll.AnimationKind,
                ragdoll.X,
                ragdoll.Y,
                MathF.Max(ragdoll.OpaqueBounds.Height, height),
                ragdoll.FacingLeft,
                ragdoll.RotationDegrees,
                ticksRemaining,
                cameraPosition))
        {
            return true;
        }

        return DrawDynamicRagdollVisual(ragdoll, ticksRemaining, cameraPosition);
    }

    private bool DrawDynamicRagdollVisual(DynamicRagdollState ragdoll, int ticksRemaining, Vector2 cameraPosition)
    {
        if (ragdoll.UseElkondoVerticalVisual)
        {
            return DrawElkondoRagdollVisual(ragdoll, ticksRemaining, cameraPosition);
        }

        var fadeAlpha = GetCorpseFadeAlpha(ticksRemaining);
        // Only end-of-life fade (last Regular/Acid ticks) can zero alpha — never treat that as "drawn"
        // when we somehow got a non-positive lifetime mid-flight; fall through to the static corpse.
        if (fadeAlpha <= 0.001f)
        {
            return ticksRemaining <= 0;
        }

        var spriteName = GetDeadBodySpriteName(
            ragdoll.GameplayClassId,
            ragdoll.ClassId,
            ragdoll.Team,
            ragdoll.AnimationKind);
        if (spriteName is null)
        {
            return false;
        }

        var sprite = GetResolvedSprite(spriteName);
        if (sprite is null || sprite.Frames.Count == 0)
        {
            return false;
        }

        var frame = sprite.Frames[0];
        var opaque = ragdoll.OpaqueBounds;
        if (opaque.Width <= 1 || opaque.Height <= 1)
        {
            opaque = frame.OpaqueBounds ?? new Rectangle(0, 0, frame.Width, frame.Height);
        }

        var scaleX = ragdoll.FacingLeft ? -1f : 1f;
        var tint = Color.White * fadeAlpha;
        var roundedOrigin = GetRoundedPlayerSpriteOrigin(new Vector2(ragdoll.X, ragdoll.Y));
        var rootPosition = new Vector2(roundedOrigin.X - cameraPosition.X, roundedOrigin.Y - cameraPosition.Y);
        var baseSource = frame.SourceRectangle ?? new Rectangle(0, 0, frame.Width, frame.Height);
        var spineMidY = opaque.Top + (opaque.Height * 0.5f);
        var bodyRotationRadians = ragdoll.RotationDegrees * (MathF.PI / 180f);

        // Cut only along the opaque corpse meat (ignore transparent padding).
        var cutCount = DynamicRagdollPivotCount + 2;
        Span<int> cutXs = stackalloc int[cutCount];
        cutXs[0] = opaque.Left;
        for (var pivotIndex = 0; pivotIndex < DynamicRagdollPivotCount; pivotIndex += 1)
        {
            cutXs[pivotIndex + 1] = opaque.Left + Math.Clamp(
                (int)MathF.Round(opaque.Width * DynamicRagdollPivotFractions[pivotIndex]),
                1,
                Math.Max(1, opaque.Width - 1));
        }

        cutXs[DynamicRagdollPivotCount + 1] = opaque.Right;
        for (var index = 1; index < cutCount; index += 1)
        {
            if (cutXs[index] <= cutXs[index - 1])
            {
                cutXs[index] = Math.Min(opaque.Right, cutXs[index - 1] + 1);
            }
        }

        var opaqueHalfWidth = opaque.Width * 0.5f;
        var headFromCenter = TransformRagdollLocal(
            new Vector2(-opaqueHalfWidth, 0f),
            scaleX,
            bodyRotationRadians);
        var cursor = rootPosition + headFromCenter;
        var cumulativePivotDegrees = 0f;
        var segmentCount = cutCount - 1;
        var previousRotationDegrees = ragdoll.RotationDegrees;

        for (var segmentIndex = 0; segmentIndex < segmentCount; segmentIndex += 1)
        {
            var segmentLeft = cutXs[segmentIndex];
            var segmentRight = cutXs[segmentIndex + 1];
            var segmentWidth = segmentRight - segmentLeft;
            if (segmentWidth <= 0)
            {
                continue;
            }

            var segmentRotationDegrees = ragdoll.RotationDegrees + cumulativePivotDegrees;
            var segmentRotationRadians = segmentRotationDegrees * (MathF.PI / 180f);

            // Rigid meat chunk: rotate only, never scale the segment itself.
            var segmentOrigin = new Vector2(0f, spineMidY - opaque.Top);
            var segmentSource = new Rectangle(
                baseSource.X + segmentLeft,
                baseSource.Y + opaque.Top,
                segmentWidth,
                opaque.Height);
            var segmentFrame = new LoadedSpriteFrame(
                frame.Texture,
                SourceRectangle: segmentSource,
                OwnsTexture: false,
                OpaqueBounds: null,
                PixelSource: null);

            DrawSpriteFrameWithOptionalShadow(
                segmentFrame,
                cursor,
                tint,
                segmentRotationRadians,
                segmentOrigin,
                new Vector2(scaleX, 1f));

            var nextCursor = cursor + TransformRagdollLocal(
                new Vector2(segmentWidth, 0f),
                scaleX,
                segmentRotationRadians);

            if (segmentIndex < DynamicRagdollPivotCount)
            {
                var pivotDegrees = ragdoll.PivotDegrees[segmentIndex];
                DrawRagdollSeamFill(
                    frame,
                    baseSource,
                    cutXs[segmentIndex + 1],
                    opaque,
                    spineMidY,
                    nextCursor,
                    previousRotationDegrees,
                    segmentRotationDegrees + pivotDegrees,
                    pivotDegrees,
                    scaleX,
                    tint);
                cumulativePivotDegrees += pivotDegrees;
                previousRotationDegrees = ragdoll.RotationDegrees + cumulativePivotDegrees;
            }

            cursor = nextCursor;
        }

        return true;
    }

    private void DrawRagdollSeamFill(
        LoadedSpriteFrame frame,
        Rectangle baseSource,
        int jointTextureX,
        Rectangle opaque,
        float spineMidY,
        Vector2 jointPosition,
        float previousRotationDegrees,
        float nextRotationDegrees,
        float pivotDegrees,
        float scaleX,
        Color tint)
    {
        var absPivot = MathF.Abs(pivotDegrees);
        if (absPivot < 0.75f)
        {
            return;
        }

        // Stretch a 2px seam column into the open wedge — parts stay rigid, only the fill stretches.
        var seamLeft = Math.Clamp(jointTextureX - (DynamicRagdollSeamWidthPixels / 2), opaque.Left, opaque.Right - 1);
        var seamWidth = Math.Min(DynamicRagdollSeamWidthPixels, opaque.Right - seamLeft);
        if (seamWidth <= 0)
        {
            return;
        }

        var openRadians = absPivot * (MathF.PI / 180f);
        var stretchAcross = 1f + (openRadians * 0.85f);
        var bisectorDegrees = previousRotationDegrees + (pivotDegrees * 0.5f);
        var bisectorRadians = bisectorDegrees * (MathF.PI / 180f);
        var seamSource = new Rectangle(
            baseSource.X + seamLeft,
            baseSource.Y + opaque.Top,
            seamWidth,
            opaque.Height);
        var seamFrame = new LoadedSpriteFrame(
            frame.Texture,
            SourceRectangle: seamSource,
            OwnsTexture: false,
            OpaqueBounds: null,
            PixelSource: null);
        var seamOrigin = new Vector2(seamWidth * 0.5f, spineMidY - opaque.Top);

        DrawSpriteFrameWithOptionalShadow(
            seamFrame,
            jointPosition,
            tint,
            bisectorRadians,
            seamOrigin,
            new Vector2(scaleX * stretchAcross, 1f));
    }

    private static Vector2 TransformRagdollLocal(Vector2 local, float scaleX, float rotationRadians)
    {
        var scaledX = local.X * scaleX;
        var scaledY = local.Y;
        var cos = MathF.Cos(rotationRadians);
        var sin = MathF.Sin(rotationRadians);
        return new Vector2(
            (scaledX * cos) - (scaledY * sin),
            (scaledX * sin) + (scaledY * cos));
    }
}
