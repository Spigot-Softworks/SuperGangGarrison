#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    // Stock DeadS: classic horizontal ragdolls (below).
    // Elkondo: temporary corpse uses the 5th run-cycle pose â€” see GameplayDynamicRagdollController.Elkondo.cs
    // TODO(dynamic-ragdoll): replace that temporary run-frame with unique Elkondo Dead sprites when authored.

    public const int MaxDynamicRagdolls = 28;
    public const int DynamicRagdollPivotCount = 3;
    public const int DynamicRagdollCollisionNodeCount = DynamicRagdollPivotCount + 1; // one meat chunk per segment
    private const float DynamicRagdollMaxPivotDegrees = 45f;
    private const float DynamicRagdollGravity = 0.7f;
    private const float DynamicRagdollMaxFallSpeed = 11f;
    private const float DynamicRagdollGroundBounce = 0.18f;
    private const float DynamicRagdollWallBounce = 0.28f;
    private const float DynamicRagdollLaunchSpeedScale = 1f;
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
    // Body probes follow the articulated spine and use the sprite thickness.
    private const float DynamicRagdollSegmentRadius = 2.25f; // Ã˜ 4.5
    private const float DynamicRagdollLedgeDroopAccel = 2.8f;
    private const float DynamicRagdollHangProbeDepth = 56f;
    private const float DynamicRagdollLieTorque = 0.14f;
    private const float DynamicRagdollTipOverTorque = 0.22f;
    private const float DynamicRagdollSettleMaxLieErrorDegrees = 38f;
    private const float DynamicRagdollMaxCollisionHalfWidth = 9f;
    private const float DynamicRagdollMaxCollisionHalfHeight = 12f;
    private const float DynamicRagdollCollisionEpsilon = 0.01f;
    private const int DynamicRagdollOpaqueAlphaThreshold = 24;
    private const int DynamicRagdollSeamWidthPixels = 2;

    // Fractions along the *opaque* corpse span (head â†’ feet), not the full padded frame.
    private static readonly float[] DynamicRagdollPivotFractions = [0.28f, 0.52f, 0.76f];

    private readonly Dictionary<int, DynamicRagdollState> _dynamicRagdolls = new();
    private readonly List<int> _staleDynamicRagdollIds = new();
    private readonly Random _dynamicRagdollRandom = new();

    internal sealed class DynamicRagdollState
    {
        public required int DeadBodyId { get; set; }
        public required int SourcePlayerId { get; init; }
        public required PlayerClass ClassId { get; init; }
        public required PlayerTeam Team { get; init; }
        public required DeadBodyAnimationKind AnimationKind { get; set; }
        public required string GameplayClassId { get; init; }
        public required bool FacingLeft { get; init; }
        public bool DiedToFire;

        public float X;
        public float Y;
        public float VelocityX;
        public float VelocityY;
        public float RotationDegrees;
        public float AngularVelocityDegrees;
        public bool Grounded;
        public bool Settled;
        public bool SimulationFrozen;
        /// <summary>Pose frozen for acid dissolve â€” physics and live draw stop; snapshot owns the visual.</summary>
        public bool AcidFrozen;
        public int AgeTicks;
        public int GroundedTicks;
        public Rectangle OpaqueBounds;
        public float CollisionHalfWidth;
        public float CollisionHalfHeight;
        public bool UseElkondoVerticalVisual;
        public bool IsBisectedHalf;
        public bool IsUpperBisectedHalf;
        public Rectangle BisectedSourceRectangle;
        public Rectangle BisectedOpaqueLocalBounds;
        public DynamicRagdollState? BisectedPartner;
        public Rectangle[]? CollisionSegmentOpaqueBounds;
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

    public void ResetDynamicRagdollEffects()
    {
        _dynamicRagdolls.Clear();
        _staleDynamicRagdollIds.Clear();
    }

    public void AdvanceDynamicRagdolls()
    {
        if (!_gameplayManager.RuntimeSettings.DynamicRagdollEnabled || _dynamicRagdolls.Count == 0)
        {
            if (!_gameplayManager.RuntimeSettings.DynamicRagdollEnabled && _dynamicRagdolls.Count > 0)
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
            AdvanceDynamicRagdoll(pair.Value, level, bounds);
            if (pair.Value.BisectedPartner is { } partner)
            {
                AdvanceDynamicRagdoll(partner, level, bounds);
            }
        }
    }

    private void AdvanceDynamicRagdoll(DynamicRagdollState ragdoll, SimpleLevel level, WorldBounds bounds)
    {
        ragdoll.AgeTicks += 1;
        if (ragdoll.SimulationFrozen || ragdoll.AcidFrozen
            || IsBurnCharredRagdollHeld(ragdoll)
            || (TryGetRagdollCorpseTicksRemaining(ragdoll, out var corpseTicks)
                && IsCorpseAcidFading(corpseTicks)))
        {
            return;
        }

        Span<Vector2> poseNodes = stackalloc Vector2[DynamicRagdollCollisionNodeCount];
        Span<bool> poseGrounded = stackalloc bool[DynamicRagdollCollisionNodeCount];
        var poseNodeCount = BuildRagdollCollisionNodes(ragdoll, poseNodes);
        var hangingCount = CountRagdollHangingNodes(
            level, poseNodes, poseGrounded, poseNodeCount, GetRagdollCollisionRadius(ragdoll), out var groundedCount);
        var isHangingOffLedge = groundedCount > 0 && hangingCount > 0;

        if (ragdoll.Settled)
        {
            if (!IsRagdollPoseSupported(ragdoll, level, bounds))
            {
                ragdoll.Settled = false;
                ragdoll.GroundedTicks = 0;
            }
            else
            {
                return;
            }
        }

        ragdoll.VelocityY = MathF.Min(DynamicRagdollMaxFallSpeed, ragdoll.VelocityY + DynamicRagdollGravity);
        var hitGround = AdvanceRagdollSegmentCollision(ragdoll, level, bounds);
        ragdoll.Grounded = hitGround;
        ragdoll.GroundedTicks = hitGround ? ragdoll.GroundedTicks + 1 : 0;

        poseNodeCount = BuildRagdollCollisionNodes(ragdoll, poseNodes);
        hangingCount = CountRagdollHangingNodes(
            level, poseNodes, poseGrounded, poseNodeCount, GetRagdollCollisionRadius(ragdoll), out groundedCount);
        isHangingOffLedge = groundedCount > 0 && hangingCount > 0;
        var waistWorld = GetRagdollWaistWorldPosition(ragdoll);
        var waistGrounded = TryFindRagdollNodeFloor(
            waistWorld, GetRagdollCollisionRadius(ragdoll), level, wasFalling: true, out _);
        if (!waistGrounded && groundedCount > 0)
        {
            isHangingOffLedge = false;
        }

        if (hitGround || isHangingOffLedge || waistGrounded)
        {
            ragdoll.VelocityX *= DynamicRagdollGroundFriction;
            ragdoll.AngularVelocityDegrees *= DynamicRagdollGroundAngularDamping;
            var lieTarget = GetRagdollLieTargetDegrees(ragdoll);
            var settleError = NormalizeRagdollRotationDegrees(lieTarget - ragdoll.RotationDegrees);
            var lieStrength = waistGrounded ? DynamicRagdollLieTorque : DynamicRagdollTipOverTorque;
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

            if (!ragdoll.IsBisectedHalf)
            {
                AdaptRagdollRestPivotsToContacts(ragdoll, level);
                ApplyRagdollLedgeGravityDroop(ragdoll, poseGrounded, poseNodeCount);
                if (waistGrounded && !isHangingOffLedge)
                {
                    ApplyRagdollLyingPoseBias(ragdoll);
                }
            }
        }
        else
        {
            ragdoll.AngularVelocityDegrees *= DynamicRagdollAirAngularDamping;
        }

        var previousRotation = ragdoll.RotationDegrees;
        var previousX = ragdoll.X;
        var previousY = ragdoll.Y;
        Span<float> previousPivots = stackalloc float[DynamicRagdollPivotCount];
        ragdoll.PivotDegrees.AsSpan().CopyTo(previousPivots);
        ragdoll.RotationDegrees += ragdoll.AngularVelocityDegrees;
        if (!ragdoll.IsBisectedHalf)
        {
            AdvanceRagdollPivots(ragdoll, hitGround || isHangingOffLedge || waistGrounded);
        }

        if (!TryDepenetrateRagdoll(ragdoll, level, bounds, maxDistance: 6))
        {
            ragdoll.X = previousX;
            ragdoll.Y = previousY;
            ragdoll.RotationDegrees = previousRotation;
            previousPivots.CopyTo(ragdoll.PivotDegrees);
            ragdoll.AngularVelocityDegrees *= 0.25f;
            Array.Clear(ragdoll.PivotVelocities);
        }

        if (ragdoll.UseElkondoVerticalVisual)
        {
            AdvanceElkondoRagdollWeapon(ragdoll);
        }

        var lieError = MathF.Abs(NormalizeRagdollRotationDegrees(GetRagdollLieTargetDegrees(ragdoll) - ragdoll.RotationDegrees));
        if (hitGround
            && lieError <= DynamicRagdollSettleMaxLieErrorDegrees
            && ragdoll.GroundedTicks > 6
            && MathF.Abs(ragdoll.VelocityX) < DynamicRagdollSettledSpeed
            && MathF.Abs(ragdoll.VelocityY) < DynamicRagdollSettledSpeed
            && MathF.Abs(ragdoll.AngularVelocityDegrees) < DynamicRagdollSettledAngularSpeed
            && (ragdoll.IsBisectedHalf || ragdoll.PivotVelocities.All(velocity => MathF.Abs(velocity) < DynamicRagdollSettledAngularSpeed)))
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

    public void SyncDynamicRagdollsWithDeadBodies()
    {
        if (!_gameplayManager.RuntimeSettings.DynamicRagdollEnabled)
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
            if (_dynamicRagdolls.TryGetValue(deadBody.Id, out var existingRagdoll))
            {
                existingRagdoll.DiedToFire |= deadBody.DiedToFire;
                ApplyAuthoritativeDeathAnimationKind(existingRagdoll, deadBody.AnimationKind);
                continue;
            }

            // Keep the already-simulated immediate ragdoll instead of respawning (looks like a pop/vanish).
            var syntheticId = -Math.Abs(deadBody.SourcePlayerId);
            if (!_dynamicRagdolls.ContainsKey(deadBody.Id)
                && _dynamicRagdolls.Remove(syntheticId, out var transferred))
            {
                _staleDynamicRagdollIds.Remove(syntheticId);
                // Prefer server corpse speeds when the immediate launch was weak or aimed the wrong way.
                // Fire deaths keep original motion â€” never re-apply the shot launch scale.
                if (!transferred.SimulationFrozen && !deadBody.DiedToFire && !transferred.DiedToFire)
                {
                    var authSpeedSq = (deadBody.HorizontalSpeed * deadBody.HorizontalSpeed)
                        + (deadBody.VerticalSpeed * deadBody.VerticalSpeed);
                    var currentSpeedSq = (transferred.VelocityX * transferred.VelocityX)
                        + (transferred.VelocityY * transferred.VelocityY);
                    var authLaunchX = deadBody.HorizontalSpeed * DynamicRagdollLaunchSpeedScale;
                    var directionDisagrees = transferred.AgeTicks <= 8
                        && MathF.Abs(authLaunchX) > 0.5f
                        && MathF.Abs(transferred.VelocityX) > 0.5f
                        && MathF.Sign(authLaunchX) != MathF.Sign(transferred.VelocityX);
                    if (authSpeedSq > 1f
                        && transferred.AgeTicks <= 8
                        && (directionDisagrees || authSpeedSq > currentSpeedSq * 1.35f))
                    {
                        transferred.VelocityX = Math.Clamp(authLaunchX, -12f, 12f);
                        transferred.VelocityY = Math.Clamp(
                            deadBody.VerticalSpeed * DynamicRagdollLaunchSpeedScale,
                            -12f, DynamicRagdollMaxFallSpeed);
                        transferred.Settled = false;
                        transferred.GroundedTicks = 0;
                    }
                }
                else if (!transferred.SimulationFrozen && transferred.AgeTicks <= 8)
                {
                    transferred.VelocityX = deadBody.HorizontalSpeed;
                    transferred.VelocityY = deadBody.VerticalSpeed;
                }

                transferred.DiedToFire |= deadBody.DiedToFire;
                transferred.DeadBodyId = deadBody.Id;
                if (transferred.BisectedPartner is { } transferredPartner)
                {
                    transferredPartner.DeadBodyId = deadBody.Id;
                }
                ApplyAuthoritativeDeathAnimationKind(transferred, deadBody.AnimationKind);
                _dynamicRagdolls[deadBody.Id] = transferred;
                continue;
            }

            EnsureDynamicRagdoll(deadBody);
        }

        for (var index = 0; index < _retainedDeadBodies.Count; index += 1)
        {
            var retainedId = _retainedDeadBodies[index].Id;
            _staleDynamicRagdollIds.Remove(retainedId);
            if (_dynamicRagdolls.TryGetValue(retainedId, out var retainedRagdoll))
            {
                FreezeDynamicRagdoll(retainedRagdoll);
            }
        }

        foreach (var entry in _immediateNetworkDeadBodies)
        {
            var visual = entry.Value;
            var syntheticId = -Math.Abs(visual.SourcePlayerId);
            // Authoritative corpse already owns this player's ragdoll â€” don't keep a duplicate.
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

            // Sentry kills credit the engineer â€” prefer a nearby owned turret as the shot origin.
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

        // Opposite of facing â‰ˆ away from whoever they were aiming at (origin coincidence only).
        var facingFallbackSign = facingLeft ? 1f : -1f;
        if (hasOrigin)
        {
            knockbackX = 0f;
            knockbackY = 0f;
            CorpseKnockbackRules.ApplyDirectedLaunch(
                ref knockbackX,
                ref knockbackY,
                corpseX,
                corpseY,
                originX,
                originY,
                knockbackSpeed,
                facingFallbackSign);
            return;
        }

        knockbackX = facingFallbackSign * CorpseKnockbackRules.EnforceMinimum(knockbackSpeed) * 0.7f;
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
            deadBody.VerticalSpeed,
            deadBody.DiedToFire);
    }

    private void EnsureDynamicRagdollFromImmediate(ImmediateNetworkDeadBodyVisual deadBody, int syntheticId)
    {
        if (_dynamicRagdolls.ContainsKey(syntheticId))
        {
            return;
        }

        // Knock opposite of facing â€” facing the killer is typical, so this reads as away from the shot.
        // Fire deaths keep original motion only (no death shove).
        float knockbackX;
        float knockbackY;
        if (deadBody.DiedToFire)
        {
            knockbackX = 0f;
            knockbackY = 0f;
            var sourcePlayer = FindPlayerById(deadBody.SourcePlayerId);
            if (sourcePlayer is not null)
            {
                knockbackX = sourcePlayer.HorizontalSpeed * (float)_config.FixedDeltaSeconds;
                knockbackY = sourcePlayer.VerticalSpeed * (float)_config.FixedDeltaSeconds;
            }
        }
        else
        {
            var facingSign = deadBody.FacingLeft ? 1f : -1f;
            var knockbackSpeed = CorpseKnockbackRules.EnforceMinimum(CorpseKnockbackRules.StandardSpeed);
            knockbackX = facingSign * knockbackSpeed * 0.7f;
            knockbackY = -2.4f;
        }

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
            knockbackX,
            knockbackY,
            diedToFire: deadBody.DiedToFire);
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
        float knockbackY,
        bool diedToFire = false)
    {
        if (_dynamicRagdolls.ContainsKey(deadBodyId))
        {
            return;
        }

        if (_dynamicRagdolls.Count >= MaxDynamicRagdolls)
        {
            TrimDynamicRagdollCapacity(reserveSlot: true);
        }

        if (!TryResolveRagdollOpaqueBounds(
                gameplayClassId,
                classId,
                team,
                animationKind,
                out var opaqueBounds,
                out var collisionHalfWidth,
                out var collisionHalfHeight,
                out var collisionSegmentOpaqueBounds))
        {
            opaqueBounds = new Rectangle(0, 0, 24, 12);
            collisionHalfWidth = 12f;
            collisionHalfHeight = 6f;
            collisionSegmentOpaqueBounds = null;
        }

        var useElkondoVisual = TryResolveElkondoCorpseSprite(gameplayClassId, classId, team, out _, out _, out _);

        // Elkondo starts from the living upright pose; stock DeadS art starts head-up then tips over.
        var spawnRotation = useElkondoVisual
            ? 0f
            : facingLeft ? -90f : 90f;

        float launchX;
        float launchY;
        float angularVelocityDegrees;
        var tipSign = 1f;
        if (diedToFire)
        {
            // Flames: keep the character's existing motion â€” no death shove / tip impulse.
            launchX = knockbackX;
            launchY = knockbackY;
            angularVelocityDegrees = 0f;
        }
        else
        {
            launchX = knockbackX * DynamicRagdollLaunchSpeedScale;
            launchY = knockbackY * DynamicRagdollLaunchSpeedScale;
            var launchSpeed = MathF.Sqrt((launchX * launchX) + (launchY * launchY));
            if (launchSpeed < DynamicRagdollMinLaunchSpeed)
            {
                // Opposite of facing so the fallback still reads as shot-away, not into the aim.
                var awaySign = facingLeft ? 1f : -1f;
                launchX = awaySign * (CorpseKnockbackRules.MinimumSpeed * DynamicRagdollLaunchSpeedScale);
                launchY = -2.8f - (_dynamicRagdollRandom.NextSingle() * 2.0f);
                launchSpeed = MathF.Sqrt((launchX * launchX) + (launchY * launchY));
            }
            // Keep the authoritative shot direction; collision resolution,
            // rather than an extra upward impulse, keeps bodies out of solids.
            launchX = Math.Clamp(launchX, -12f, 12f);
            launchY = Math.Clamp(launchY, -12f, DynamicRagdollMaxFallSpeed);

            // Tip/fold WITH the knockback so the corpse leans away from the bullet, not into it.
            tipSign = launchX >= 0f ? 1f : -1f;
            angularVelocityDegrees = tipSign * (2.2f + (launchSpeed * 0.28f));
        }

        var ragdoll = new DynamicRagdollState
        {
            DeadBodyId = deadBodyId,
            SourcePlayerId = sourcePlayerId,
            ClassId = classId,
            Team = team,
            AnimationKind = animationKind,
            GameplayClassId = gameplayClassId ?? string.Empty,
            FacingLeft = facingLeft,
            DiedToFire = diedToFire,
            X = x,
            Y = y,
            VelocityX = launchX,
            VelocityY = launchY,
            RotationDegrees = spawnRotation,
            // Light tip from the kill shot â€” enough to flop, not enough to keep rolling on the ground.
            AngularVelocityDegrees = angularVelocityDegrees,
            OpaqueBounds = opaqueBounds,
            CollisionHalfWidth = collisionHalfWidth,
            CollisionHalfHeight = collisionHalfHeight,
            UseElkondoVerticalVisual = useElkondoVisual,
            CollisionSegmentOpaqueBounds = collisionSegmentOpaqueBounds,
        };

        if (!diedToFire && animationKind != DeadBodyAnimationKind.Bisected)
        {
            ApplyDeathShotImpulseThroughWaist(ragdoll, launchX, launchY, tipSign, useElkondoVisual);
        }

        var sourcePlayer = FindPlayerById(sourcePlayerId);
        if (useElkondoVisual && sourcePlayer is { IsAlive: false }
            && sourcePlayer.ClassId == classId && sourcePlayer.Team == team)
        {
            TryCaptureElkondoRagdollWeapon(ragdoll, sourcePlayer);
        }

        if (animationKind == DeadBodyAnimationKind.Bisected && useElkondoVisual)
        {
            TrySplitDynamicRagdollAtWaist(ragdoll);
        }

        _dynamicRagdolls[deadBodyId] = ragdoll;
        TrimDynamicRagdollCapacity();
    }

    private void ApplyAuthoritativeDeathAnimationKind(DynamicRagdollState ragdoll, DeadBodyAnimationKind animationKind)
    {
        if (ragdoll.AnimationKind == animationKind)
        {
            if (animationKind == DeadBodyAnimationKind.Bisected && !ragdoll.IsBisectedHalf)
            {
                _ = TrySplitDynamicRagdollAtWaist(ragdoll);
            }
            return;
        }

        ragdoll.AnimationKind = animationKind;
        if (animationKind == DeadBodyAnimationKind.Bisected)
        {
            _ = TrySplitDynamicRagdollAtWaist(ragdoll);
        }
    }

    private bool TrySplitDynamicRagdollAtWaist(DynamicRagdollState ragdoll)
    {
        if (ragdoll.IsBisectedHalf || !ragdoll.UseElkondoVerticalVisual
            || !TryResolveElkondoCorpseSprite(
                ragdoll.GameplayClassId,
                ragdoll.ClassId,
                ragdoll.Team,
                out var spriteName,
                out var frameIndex,
                out _))
        {
            return false;
        }

        var sprite = GetResolvedSprite(spriteName);
        if (sprite is null || sprite.Frames.Count == 0)
        {
            return false;
        }

        var frame = sprite.Frames[Math.Clamp(frameIndex, 0, sprite.Frames.Count - 1)];
        var opaque = ragdoll.OpaqueBounds;
        if (opaque.Width <= 1 || opaque.Height <= 2)
        {
            return false;
        }

        var waistFraction = GetElkondoWaistFraction(ragdoll.ClassId);
        var chestFraction = Math.Clamp(waistFraction * 0.48f, 0.16f, waistFraction - 0.10f);
        var kneeFraction = Math.Clamp(waistFraction + ((1f - waistFraction) * 0.50f), waistFraction + 0.10f, 0.90f);
        Span<float> cutFractions = stackalloc float[] { 0f, chestFraction, waistFraction, kneeFraction, 1f };
        Span<int> cutYs = stackalloc int[cutFractions.Length];
        for (var index = 0; index < cutFractions.Length; index += 1)
        {
            cutYs[index] = opaque.Top + Math.Clamp((int)MathF.Round(opaque.Height * cutFractions[index]), 0, opaque.Height);
        }
        for (var index = 1; index < cutYs.Length; index += 1)
        {
            if (cutYs[index] <= cutYs[index - 1])
            {
                cutYs[index] = Math.Min(opaque.Bottom, cutYs[index - 1] + 1);
            }
        }

        var waistY = cutYs[2];
        var upperStrip = new Rectangle(opaque.Left, opaque.Top, opaque.Width, waistY - opaque.Top);
        var lowerStrip = new Rectangle(opaque.Left, waistY, opaque.Width, opaque.Bottom - waistY);
        if (upperStrip.Height <= 1 || lowerStrip.Height <= 1)
        {
            return false;
        }

        var segments = ragdoll.CollisionSegmentOpaqueBounds;
        var upperOpaque = segments is { Length: DynamicRagdollPivotCount + 1 }
            ? UnionRagdollSegmentOpaqueBounds(segments[0], segments[1], cutYs[1] - opaque.Top, upperStrip.Height)
            : new Rectangle(0, 0, upperStrip.Width, upperStrip.Height);
        var lowerOpaque = segments is { Length: DynamicRagdollPivotCount + 1 }
            ? UnionRagdollSegmentOpaqueBounds(segments[2], segments[3], cutYs[3] - waistY, lowerStrip.Height)
            : new Rectangle(0, 0, lowerStrip.Width, lowerStrip.Height);
        if (upperOpaque.Width <= 0 || upperOpaque.Height <= 0 || lowerOpaque.Width <= 0 || lowerOpaque.Height <= 0)
        {
            return false;
        }

        var baseSource = frame.SourceRectangle ?? new Rectangle(0, 0, frame.Width, frame.Height);
        var upperSource = new Rectangle(baseSource.X + upperStrip.X, baseSource.Y + upperStrip.Y, upperStrip.Width, upperStrip.Height);
        var lowerSource = new Rectangle(baseSource.X + lowerStrip.X, baseSource.Y + lowerStrip.Y, lowerStrip.Width, lowerStrip.Height);
        var upperCenterOffsetY = (upperStrip.Height - opaque.Height) * 0.5f;
        var lowerCenterOffsetY = (lowerStrip.Top - opaque.Top) + (lowerStrip.Height * 0.5f) - opaque.Height * 0.5f;
        var waistWorld = new Vector2(ragdoll.X, ragdoll.Y)
            + TransformRagdollLocal(
                new Vector2(0f, (waistY - opaque.Top) - (opaque.Height * 0.5f)),
                ragdoll.FacingLeft ? -1f : 1f,
                ragdoll.RotationDegrees * (MathF.PI / 180f));
        var lowerOffsetWorld = TransformRagdollLocal(
            new Vector2(0f, lowerCenterOffsetY),
            ragdoll.FacingLeft ? -1f : 1f,
            ragdoll.RotationDegrees * (MathF.PI / 180f));

        var lower = new DynamicRagdollState
        {
            DeadBodyId = ragdoll.DeadBodyId,
            SourcePlayerId = ragdoll.SourcePlayerId,
            ClassId = ragdoll.ClassId,
            Team = ragdoll.Team,
            AnimationKind = DeadBodyAnimationKind.Bisected,
            GameplayClassId = ragdoll.GameplayClassId,
            FacingLeft = ragdoll.FacingLeft,
            DiedToFire = ragdoll.DiedToFire,
            X = ragdoll.X + lowerOffsetWorld.X,
            Y = ragdoll.Y + lowerOffsetWorld.Y,
            VelocityX = ragdoll.VelocityX,
            VelocityY = ragdoll.VelocityY + 1.45f,
            RotationDegrees = ragdoll.RotationDegrees,
            AngularVelocityDegrees = ragdoll.AngularVelocityDegrees - 1.2f,
            Grounded = false,
            Settled = false,
            SimulationFrozen = ragdoll.SimulationFrozen,
            AcidFrozen = ragdoll.AcidFrozen,
            AgeTicks = ragdoll.AgeTicks,
            OpaqueBounds = new Rectangle(0, 0, lowerStrip.Width, lowerStrip.Height),
            CollisionHalfWidth = Math.Clamp(lowerOpaque.Width * 0.5f, 3f, DynamicRagdollMaxCollisionHalfWidth),
            CollisionHalfHeight = Math.Clamp(lowerOpaque.Height * 0.5f, 2f, DynamicRagdollMaxCollisionHalfHeight),
            UseElkondoVerticalVisual = true,
            IsBisectedHalf = true,
            IsUpperBisectedHalf = false,
            BisectedSourceRectangle = lowerSource,
            BisectedOpaqueLocalBounds = lowerOpaque,
        };

        var upperOffsetWorld = TransformRagdollLocal(
            new Vector2(0f, upperCenterOffsetY),
            ragdoll.FacingLeft ? -1f : 1f,
            ragdoll.RotationDegrees * (MathF.PI / 180f));
        ragdoll.X += upperOffsetWorld.X;
        ragdoll.Y += upperOffsetWorld.Y;
        ragdoll.VelocityY -= 1.45f;
        ragdoll.AngularVelocityDegrees += 1.2f;
        ragdoll.OpaqueBounds = new Rectangle(0, 0, upperStrip.Width, upperStrip.Height);
        ragdoll.CollisionHalfWidth = Math.Clamp(upperOpaque.Width * 0.5f, 3f, DynamicRagdollMaxCollisionHalfWidth);
        ragdoll.CollisionHalfHeight = Math.Clamp(upperOpaque.Height * 0.5f, 2f, DynamicRagdollMaxCollisionHalfHeight);
        ragdoll.CollisionSegmentOpaqueBounds = null;
        ragdoll.IsBisectedHalf = true;
        ragdoll.IsUpperBisectedHalf = true;
        ragdoll.BisectedSourceRectangle = upperSource;
        ragdoll.BisectedOpaqueLocalBounds = upperOpaque;
        ragdoll.WeaponAttachLocalY -= upperCenterOffsetY;
        Array.Clear(ragdoll.PivotDegrees);
        Array.Clear(ragdoll.PivotVelocities);
        Array.Clear(ragdoll.RestPivotDegrees);
        ragdoll.BisectedPartner = lower;
        _gameplayManager.GoreEffects.SpawnBisectCutSquibs(waistWorld.X, waistWorld.Y);
        return true;
    }

    private static Rectangle UnionRagdollSegmentOpaqueBounds(
        Rectangle first,
        Rectangle second,
        int secondOffsetY,
        int fallbackHeight)
    {
        var hasFirst = first.Width > 0 && first.Height > 0;
        var hasSecond = second.Width > 0 && second.Height > 0;
        if (!hasFirst && !hasSecond)
        {
            return new Rectangle(0, 0, 1, Math.Max(1, fallbackHeight));
        }

        if (!hasFirst)
        {
            return new Rectangle(second.X, second.Y + secondOffsetY, second.Width, second.Height);
        }

        if (!hasSecond)
        {
            return first;
        }

        var left = Math.Min(first.Left, second.Left);
        var right = Math.Max(first.Right, second.Right);
        var top = Math.Min(first.Top, second.Top + secondOffsetY);
        var bottom = Math.Max(first.Bottom, second.Bottom + secondOffsetY);
        return new Rectangle(left, top, right - left, bottom - top);
    }

    /// <summary>
    /// Kill-shot knockback is applied at the waist; chest and knee are flung as the chain reacts.
    /// </summary>
    private static void ApplyDeathShotImpulseThroughWaist(
        DynamicRagdollState ragdoll,
        float launchX,
        float launchY,
        float tipSign,
        bool useElkondoVisual)
    {
        var launchSpeed = MathF.Sqrt((launchX * launchX) + (launchY * launchY));
        var shotStrength = MathF.Max(DynamicRagdollMinLaunchSpeed, launchSpeed);

        // Waist absorbs the hit â€” primary joint velocity from the shot.
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
            // Stock DeadS: treat middle pivot as the waist hit point.
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
        out float collisionHalfHeight,
        out Rectangle[]? collisionSegmentOpaqueBounds)
    {
        opaqueBounds = default;
        collisionHalfWidth = 8f;
        collisionHalfHeight = 6f;
        collisionSegmentOpaqueBounds = null;

        string? spriteName;
        var frameIndex = 0;
        var useElkondoVisual = TryResolveElkondoCorpseSprite(
            gameplayClassId,
            classId,
            team,
            out var elkondoSprite,
            out frameIndex,
            out _);
        if (useElkondoVisual)
        {
            spriteName = elkondoSprite;
        }
        else
        {
            spriteName = GetDynamicRagdollCorpseSpriteName(gameplayClassId, classId, team, animationKind);
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
        var hasPixels = TryGetSpriteFramePixels(frame, out var pixels, out var width, out var height);
        if (frame.OpaqueBounds is { Width: > 0, Height: > 0 } frameOpaque)
        {
            opaqueBounds = frameOpaque;
        }
        else if (hasPixels && TryComputeOpaqueBounds(pixels, width, height, out opaqueBounds))
        {
        }
        else
        {
            opaqueBounds = new Rectangle(0, 0, frame.Width, frame.Height);
        }

        if (hasPixels)
        {
            collisionSegmentOpaqueBounds = CreateRagdollCollisionSegmentOpaqueBounds(
                pixels,
                width,
                height,
                opaqueBounds,
                classId,
                useElkondoVisual);
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

    internal static Rectangle[]? CreateRagdollCollisionSegmentOpaqueBounds(
        Color[] pixels,
        int width,
        int height,
        Rectangle opaqueBounds,
        PlayerClass classId,
        bool useElkondoVerticalVisual)
    {
        if (width <= 0 || height <= 0 || pixels.Length < width * height
            || opaqueBounds.Width <= 1 || opaqueBounds.Height <= 1
            || opaqueBounds.Left < 0 || opaqueBounds.Top < 0
            || opaqueBounds.Right > width || opaqueBounds.Bottom > height)
        {
            return null;
        }

        var segmentBounds = new Rectangle[DynamicRagdollPivotCount + 1];
        if (useElkondoVerticalVisual)
        {
            var waistFraction = GetElkondoWaistFraction(classId);
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
            Span<int> cutYs = stackalloc int[cutFractions.Length];
            for (var index = 0; index < cutFractions.Length; index += 1)
            {
                cutYs[index] = opaqueBounds.Top + Math.Clamp(
                    (int)MathF.Round(opaqueBounds.Height * cutFractions[index]),
                    0,
                    opaqueBounds.Height);
            }

            for (var index = 1; index < cutYs.Length; index += 1)
            {
                if (cutYs[index] <= cutYs[index - 1])
                {
                    cutYs[index] = Math.Min(opaqueBounds.Bottom, cutYs[index - 1] + 1);
                }
            }

            for (var segmentIndex = 0; segmentIndex < segmentBounds.Length; segmentIndex += 1)
            {
                var sourceStrip = new Rectangle(
                    opaqueBounds.Left,
                    cutYs[segmentIndex],
                    opaqueBounds.Width,
                    cutYs[segmentIndex + 1] - cutYs[segmentIndex]);
                if (!TryComputeSegmentOpaqueBounds(pixels, width, height, sourceStrip, out segmentBounds[segmentIndex]))
                {
                    return null;
                }
            }
        }
        else
        {
            Span<int> cutXs = stackalloc int[DynamicRagdollPivotCount + 2];
            cutXs[0] = opaqueBounds.Left;
            for (var pivotIndex = 0; pivotIndex < DynamicRagdollPivotCount; pivotIndex += 1)
            {
                cutXs[pivotIndex + 1] = opaqueBounds.Left + Math.Clamp(
                    (int)MathF.Round(opaqueBounds.Width * DynamicRagdollPivotFractions[pivotIndex]),
                    1,
                    Math.Max(1, opaqueBounds.Width - 1));
            }

            cutXs[DynamicRagdollPivotCount + 1] = opaqueBounds.Right;
            for (var index = 1; index < cutXs.Length; index += 1)
            {
                if (cutXs[index] <= cutXs[index - 1])
                {
                    cutXs[index] = Math.Min(opaqueBounds.Right, cutXs[index - 1] + 1);
                }
            }

            for (var segmentIndex = 0; segmentIndex < segmentBounds.Length; segmentIndex += 1)
            {
                var sourceStrip = new Rectangle(
                    cutXs[segmentIndex],
                    opaqueBounds.Top,
                    cutXs[segmentIndex + 1] - cutXs[segmentIndex],
                    opaqueBounds.Height);
                if (!TryComputeSegmentOpaqueBounds(pixels, width, height, sourceStrip, out segmentBounds[segmentIndex]))
                {
                    return null;
                }
            }
        }

        return segmentBounds;
    }

    private static bool TryComputeSegmentOpaqueBounds(
        Color[] pixels,
        int width,
        int height,
        Rectangle sourceStrip,
        out Rectangle localOpaqueBounds)
    {
        localOpaqueBounds = Rectangle.Empty;
        if (sourceStrip.Width <= 0 || sourceStrip.Height <= 0)
        {
            return true;
        }

        if (sourceStrip.Left < 0 || sourceStrip.Top < 0
            || sourceStrip.Right > width || sourceStrip.Bottom > height)
        {
            return false;
        }

        var minX = sourceStrip.Right;
        var minY = sourceStrip.Bottom;
        var maxX = sourceStrip.Left - 1;
        var maxY = sourceStrip.Top - 1;
        for (var y = sourceStrip.Top; y < sourceStrip.Bottom; y += 1)
        {
            var row = y * width;
            for (var x = sourceStrip.Left; x < sourceStrip.Right; x += 1)
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
            return true;
        }

        localOpaqueBounds = new Rectangle(
            minX - sourceStrip.Left,
            minY - sourceStrip.Top,
            maxX - minX + 1,
            maxY - minY + 1);
        return true;
    }

    private void TrimDynamicRagdollCapacity(bool reserveSlot = false)
    {
        var limit = MaxDynamicRagdolls - (reserveSlot ? 1 : 0);
        while (CountActiveDynamicRagdollBodies() > limit)
        {
            var oldestId = 0;
            var oldestAge = int.MinValue;
            var found = false;
            foreach (var pair in _dynamicRagdolls)
            {
                var ragdoll = pair.Value;
                var activeParent = !ragdoll.SimulationFrozen;
                var activePartner = ragdoll.BisectedPartner is { SimulationFrozen: false };
                if (!activeParent && !activePartner)
                {
                    continue;
                }

                var activeAge = activeParent && activePartner
                    ? Math.Max(ragdoll.AgeTicks, ragdoll.BisectedPartner!.AgeTicks)
                    : activeParent ? ragdoll.AgeTicks : ragdoll.BisectedPartner!.AgeTicks;
                if (!found || activeAge > oldestAge)
                {
                    found = true;
                    oldestAge = activeAge;
                    oldestId = pair.Key;
                }
            }

            if (!found)
            {
                break;
            }

            FreezeDynamicRagdoll(_dynamicRagdolls[oldestId]);
        }
    }

    private int CountActiveDynamicRagdollBodies()
    {
        var count = 0;
        foreach (var ragdoll in _dynamicRagdolls.Values)
        {
            if (!ragdoll.SimulationFrozen)
            {
                count += 1;
            }

            if (ragdoll.BisectedPartner is { SimulationFrozen: false })
            {
                count += 1;
            }
        }

        return count;
    }

    private static void FreezeDynamicRagdoll(DynamicRagdollState ragdoll)
    {
        ragdoll.SimulationFrozen = true;
        ragdoll.Settled = true;
        ragdoll.VelocityX = 0f;
        ragdoll.VelocityY = 0f;
        ragdoll.AngularVelocityDegrees = 0f;
        Array.Clear(ragdoll.PivotVelocities);
        if (ragdoll.BisectedPartner is { } partner)
        {
            FreezeDynamicRagdoll(partner);
        }
    }

    private static void FreezeDynamicRagdollForAcid(DynamicRagdollState ragdoll)
    {
        ragdoll.AcidFrozen = true;
        ragdoll.VelocityX = 0f;
        ragdoll.VelocityY = 0f;
        ragdoll.AngularVelocityDegrees = 0f;
        Array.Clear(ragdoll.PivotVelocities);
        if (ragdoll.BisectedPartner is { } partner)
        {
            FreezeDynamicRagdollForAcid(partner);
        }
    }

    private static void AdvanceRagdollPivots(DynamicRagdollState ragdoll, bool hitGround)
    {
        var impactJiggle = hitGround && MathF.Abs(ragdoll.VelocityY) > 0.8f
            ? MathF.CopySign(MathF.Min(12f, MathF.Abs(ragdoll.VelocityY) * 1.8f), -ragdoll.AngularVelocityDegrees)
            : 0f;
        // Keep a little spinâ†’pivot coupling on the ground so chest/knee still settle with the flop.
        var spinDrive = ragdoll.AngularVelocityDegrees
            * (hitGround ? DynamicRagdollAngularPivotCoupling * 0.35f : DynamicRagdollAngularPivotCoupling);

        for (var index = 0; index < DynamicRagdollPivotCount; index += 1)
        {
            // Chest and knee are leafier â€” less falloff so they actually whip.
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
            if (!ragdoll.UseElkondoVerticalVisual
                && ((ragdoll.PivotDegrees[index] <= -DynamicRagdollMaxPivotDegrees && ragdoll.PivotVelocities[index] < 0f)
                    || (ragdoll.PivotDegrees[index] >= DynamicRagdollMaxPivotDegrees && ragdoll.PivotVelocities[index] > 0f)))
            {
                ragdoll.PivotVelocities[index] = 0f;
            }
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

        var radius = GetRagdollCollisionRadius(ragdoll);
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
                    // Keep a readable residual bend â€” especially chest/knee.
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
    /// Circle nodes never collide with each other â€” only against world solids.
    /// Returns how many meat circles hang over empty space while others rest on ground.
    /// </summary>
    private static int CountRagdollHangingNodes(
        SimpleLevel level,
        Span<Vector2> nodes,
        Span<bool> grounded,
        int nodeCount,
        float radius,
        out int groundedCount)
    {
        groundedCount = 0;
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

            // Unsupported and no floor within droop range â†’ hanging off a ledge/cliff.
            if (!HasRagdollFloorWithinDepth(nodes[i], radius, level, DynamicRagdollHangProbeDepth))
            {
                hanging += 1;
                continue;
            }

            // Floor exists far below â€” still treat as hanging so we keep draping until contact.
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
        // Feet hang â†’ droop waist + knee. Head hangs â†’ droop chest (+ waist slightly).
        var feetHang = !grounded[nodeCount - 1];
        var headHang = !grounded[0];
        if (!feetHang && !headHang)
        {
            // Middle unsupported only â€” still nudge waist if lower half hangs.
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
    /// Sweeps the articulated body through solid terrain in small steps.
    /// Weapons and other corpses do not contribute collision shapes.
    /// </summary>
    internal static bool AdvanceRagdollSegmentCollision(
        DynamicRagdollState ragdoll, SimpleLevel level, WorldBounds bounds)
    {
        if (!TryDepenetrateRagdoll(ragdoll, level, bounds, maxDistance: 64))
        {
            FreezeDynamicRagdoll(ragdoll);
            return false;
        }
        // Sweep the whole body in steps no larger than a map pixel.
        var steps = Math.Clamp((int)MathF.Ceiling(MathF.Max(MathF.Abs(ragdoll.VelocityX), MathF.Abs(ragdoll.VelocityY))), 1, 32);
        var stepX = ragdoll.VelocityX / steps;
        var stepY = ragdoll.VelocityY / steps;
        var hitGround = false;
        for (var step = 0; step < steps; step++)
        {
            if (stepX != 0f)
            {
                ragdoll.X += stepX;
                if (IsRagdollPoseBlocked(ragdoll, level, bounds))
                {
                    ragdoll.X -= stepX;
                    ragdoll.VelocityX *= -DynamicRagdollWallBounce;
                    stepX = 0f;
                    ragdoll.AngularVelocityDegrees *= 0.5f;
                }
            }
            if (stepY != 0f)
            {
                ragdoll.Y += stepY;
                if (IsRagdollPoseBlocked(ragdoll, level, bounds))
                {
                    ragdoll.Y -= stepY;
                    hitGround |= stepY > 0f;
                    ragdoll.VelocityY = stepY > 0f && ragdoll.VelocityY > 1f
                        ? -ragdoll.VelocityY * DynamicRagdollGroundBounce : 0f;
                    stepY = 0f;
                    ragdoll.AngularVelocityDegrees *= 0.55f;
                }
            }
        }
        hitGround |= IsRagdollPoseSupported(ragdoll, level, bounds);
        return hitGround;
    }

    private static bool IsRagdollPoseSupported(DynamicRagdollState ragdoll, SimpleLevel level, WorldBounds bounds)
    {
        ragdoll.Y += 1f;
        var supported = IsRagdollPoseBlocked(ragdoll, level, bounds);
        ragdoll.Y -= 1f;
        return supported;
    }

    private static float GetRagdollCollisionRadius(DynamicRagdollState ragdoll)
        => Math.Clamp((ragdoll.IsBisectedHalf
                ? ragdoll.BisectedOpaqueLocalBounds.Width
                : ragdoll.UseElkondoVerticalVisual ? ragdoll.OpaqueBounds.Width : ragdoll.OpaqueBounds.Height) * 0.5f,
            DynamicRagdollSegmentRadius, DynamicRagdollMaxCollisionHalfWidth);

    internal static bool IsRagdollPoseBlocked(DynamicRagdollState ragdoll, SimpleLevel level, WorldBounds bounds)
    {
        var opaque = ragdoll.OpaqueBounds;
        if (opaque.Width <= 1 || opaque.Height <= 1)
        {
            return IsInvalidOpaqueRagdollPoseBlocked(ragdoll, level, bounds);
        }

        return ragdoll.UseElkondoVerticalVisual
            ? IsElkondoRagdollPoseBlocked(ragdoll, level, bounds)
            : IsHorizontalRagdollPoseBlocked(ragdoll, level, bounds);
    }

    private static bool IsInvalidOpaqueRagdollPoseBlocked(
        DynamicRagdollState ragdoll,
        SimpleLevel level,
        WorldBounds bounds)
    {
        var radius = GetRagdollCollisionRadius(ragdoll);
        var left = ragdoll.X - radius;
        var top = ragdoll.Y - radius;
        var right = ragdoll.X + radius;
        var bottom = ragdoll.Y + radius;
        return left < 0f || top < 0f || right > bounds.Width || bottom > bounds.Height
            || level.IntersectsSolid(
                left + DynamicRagdollCollisionEpsilon,
                top + DynamicRagdollCollisionEpsilon,
                right - DynamicRagdollCollisionEpsilon,
                bottom - DynamicRagdollCollisionEpsilon);
    }

    private static bool IsHorizontalRagdollPoseBlocked(
        DynamicRagdollState ragdoll,
        SimpleLevel level,
        WorldBounds bounds)
    {
        var opaque = ragdoll.OpaqueBounds;
        Span<int> cutXs = stackalloc int[DynamicRagdollPivotCount + 2];
        cutXs[0] = opaque.Left;
        for (var pivotIndex = 0; pivotIndex < DynamicRagdollPivotCount; pivotIndex += 1)
        {
            cutXs[pivotIndex + 1] = opaque.Left + Math.Clamp(
                (int)MathF.Round(opaque.Width * DynamicRagdollPivotFractions[pivotIndex]),
                1,
                Math.Max(1, opaque.Width - 1));
        }

        cutXs[DynamicRagdollPivotCount + 1] = opaque.Right;
        for (var index = 1; index < cutXs.Length; index += 1)
        {
            if (cutXs[index] <= cutXs[index - 1])
            {
                cutXs[index] = Math.Min(opaque.Right, cutXs[index - 1] + 1);
            }
        }

        var scaleX = ragdoll.FacingLeft ? -1f : 1f;
        var root = new Vector2(ragdoll.X, ragdoll.Y);
        var cursor = root + TransformRagdollLocal(
            new Vector2(-opaque.Width * 0.5f, 0f),
            scaleX,
            ragdoll.RotationDegrees * (MathF.PI / 180f));
        var cumulativePivotDegrees = 0f;
        var collisionSegmentOpaqueBounds = ragdoll.CollisionSegmentOpaqueBounds;
        for (var segmentIndex = 0; segmentIndex < cutXs.Length - 1; segmentIndex += 1)
        {
            var segmentWidth = cutXs[segmentIndex + 1] - cutXs[segmentIndex];
            if (segmentWidth <= 0)
            {
                continue;
            }

            var rotationRadians = (ragdoll.RotationDegrees + cumulativePivotDegrees) * (MathF.PI / 180f);
            var alongAxis = TransformRagdollLocal(Vector2.UnitX, scaleX, rotationRadians);
            var acrossAxis = TransformRagdollLocal(Vector2.UnitY, scaleX, rotationRadians);
            var next = cursor + (alongAxis * segmentWidth);
            var segmentOpaqueBounds = collisionSegmentOpaqueBounds is { Length: DynamicRagdollPivotCount + 1 }
                ? collisionSegmentOpaqueBounds[segmentIndex]
                : new Rectangle(0, 0, segmentWidth, opaque.Height);
            if (segmentOpaqueBounds.Width > 0 && segmentOpaqueBounds.Height > 0)
            {
                var localCenter = new Vector2(
                    segmentOpaqueBounds.Left + (segmentOpaqueBounds.Width * 0.5f),
                    segmentOpaqueBounds.Top + (segmentOpaqueBounds.Height * 0.5f) - (opaque.Height * 0.5f));
                var center = cursor + TransformRagdollLocal(localCenter, scaleX, rotationRadians);
                if (IsRagdollStripBlocked(
                        center,
                        alongAxis,
                        acrossAxis,
                        segmentOpaqueBounds.Width * 0.5f,
                        segmentOpaqueBounds.Height * 0.5f,
                        level,
                        bounds))
                {
                    return true;
                }
            }

            cursor = next;
            if (segmentIndex < DynamicRagdollPivotCount)
            {
                cumulativePivotDegrees += ragdoll.PivotDegrees[segmentIndex];
            }
        }

        return false;
    }

    private static bool IsElkondoRagdollPoseBlocked(
        DynamicRagdollState ragdoll,
        SimpleLevel level,
        WorldBounds bounds)
    {
        var opaque = ragdoll.OpaqueBounds;
        if (ragdoll.IsBisectedHalf)
        {
            var localOpaque = ragdoll.BisectedOpaqueLocalBounds;
            if (localOpaque.Width <= 0 || localOpaque.Height <= 0)
            {
                return false;
            }

            var halfScaleX = ragdoll.FacingLeft ? -1f : 1f;
            var halfRotationRadians = ragdoll.RotationDegrees * (MathF.PI / 180f);
            var centerOffset = new Vector2(
                localOpaque.Left + (localOpaque.Width * 0.5f) - (opaque.Width * 0.5f),
                localOpaque.Top + (localOpaque.Height * 0.5f) - (opaque.Height * 0.5f));
            var center = new Vector2(ragdoll.X, ragdoll.Y)
                + TransformRagdollLocal(centerOffset, halfScaleX, halfRotationRadians);
            var alongAxis = TransformRagdollLocal(Vector2.UnitY, halfScaleX, halfRotationRadians);
            var acrossAxis = TransformRagdollLocal(Vector2.UnitX, halfScaleX, halfRotationRadians);
            return IsRagdollStripBlocked(
                center,
                alongAxis,
                acrossAxis,
                localOpaque.Height * 0.5f,
                localOpaque.Width * 0.5f,
                level,
                bounds);
        }

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
        Span<int> cutYs = stackalloc int[cutFractions.Length];
        for (var index = 0; index < cutFractions.Length; index += 1)
        {
            cutYs[index] = opaque.Top + Math.Clamp(
                (int)MathF.Round(opaque.Height * cutFractions[index]),
                0,
                opaque.Height);
        }

        for (var index = 1; index < cutYs.Length; index += 1)
        {
            if (cutYs[index] <= cutYs[index - 1])
            {
                cutYs[index] = Math.Min(opaque.Bottom, cutYs[index - 1] + 1);
            }
        }

        var scaleX = ragdoll.FacingLeft ? -1f : 1f;
        var root = new Vector2(ragdoll.X, ragdoll.Y);
        var cursor = root + TransformRagdollLocal(
            new Vector2(0f, -opaque.Height * 0.5f),
            scaleX,
            ragdoll.RotationDegrees * (MathF.PI / 180f));
        var cumulativePivotDegrees = 0f;
        var collisionSegmentOpaqueBounds = ragdoll.CollisionSegmentOpaqueBounds;
        for (var segmentIndex = 0; segmentIndex < cutYs.Length - 1; segmentIndex += 1)
        {
            var segmentHeight = cutYs[segmentIndex + 1] - cutYs[segmentIndex];
            var rotationRadians = (ragdoll.RotationDegrees + cumulativePivotDegrees) * (MathF.PI / 180f);
            var alongAxis = TransformRagdollLocal(Vector2.UnitY, scaleX, rotationRadians);
            var acrossAxis = TransformRagdollLocal(Vector2.UnitX, scaleX, rotationRadians);
            var next = cursor + (alongAxis * segmentHeight);
            var segmentOpaqueBounds = collisionSegmentOpaqueBounds is { Length: DynamicRagdollPivotCount + 1 }
                ? collisionSegmentOpaqueBounds[segmentIndex]
                : new Rectangle(0, 0, opaque.Width, segmentHeight);
            if (segmentHeight > 0 && segmentOpaqueBounds.Width > 0 && segmentOpaqueBounds.Height > 0)
            {
                var localCenter = new Vector2(
                    segmentOpaqueBounds.Left + (segmentOpaqueBounds.Width * 0.5f) - (opaque.Width * 0.5f),
                    segmentOpaqueBounds.Top + (segmentOpaqueBounds.Height * 0.5f));
                var center = cursor + TransformRagdollLocal(localCenter, scaleX, rotationRadians);
                if (IsRagdollStripBlocked(
                        center,
                        alongAxis,
                        acrossAxis,
                        segmentOpaqueBounds.Height * 0.5f,
                        segmentOpaqueBounds.Width * 0.5f,
                        level,
                        bounds))
                {
                    return true;
                }
            }

            cursor = next;
            if (segmentIndex < DynamicRagdollPivotCount)
            {
                cumulativePivotDegrees += ragdoll.PivotDegrees[segmentIndex];
            }
        }

        return false;
    }

    private static bool IsRagdollStripBlocked(
        Vector2 center,
        Vector2 alongAxis,
        Vector2 acrossAxis,
        float halfLength,
        float halfThickness,
        SimpleLevel level,
        WorldBounds bounds)
    {
        var halfExtentX = (MathF.Abs(alongAxis.X) * halfLength) + (MathF.Abs(acrossAxis.X) * halfThickness);
        var halfExtentY = (MathF.Abs(alongAxis.Y) * halfLength) + (MathF.Abs(acrossAxis.Y) * halfThickness);
        var left = center.X - halfExtentX;
        var top = center.Y - halfExtentY;
        var right = center.X + halfExtentX;
        var bottom = center.Y + halfExtentY;
        if (left < -DynamicRagdollCollisionEpsilon
            || top < -DynamicRagdollCollisionEpsilon
            || right > bounds.Width + DynamicRagdollCollisionEpsilon
            || bottom > bounds.Height + DynamicRagdollCollisionEpsilon)
        {
            return true;
        }

        if (!level.IntersectsSolid(left, top, right, bottom))
        {
            return false;
        }

        foreach (var solid in level.Solids)
        {
            if (right <= solid.Left + DynamicRagdollCollisionEpsilon
                || left >= solid.Right - DynamicRagdollCollisionEpsilon
                || bottom <= solid.Top + DynamicRagdollCollisionEpsilon
                || top >= solid.Bottom - DynamicRagdollCollisionEpsilon)
            {
                continue;
            }

            if (RagdollStripIntersectsSolid(
                    center,
                    alongAxis,
                    acrossAxis,
                    halfLength,
                    halfThickness,
                    solid))
            {
                return true;
            }
        }

        return false;
    }

    private static bool RagdollStripIntersectsSolid(
        Vector2 center,
        Vector2 alongAxis,
        Vector2 acrossAxis,
        float halfLength,
        float halfThickness,
        LevelSolid solid)
    {
        var solidCenter = new Vector2(solid.Left + (solid.Width * 0.5f), solid.Top + (solid.Height * 0.5f));
        var delta = center - solidCenter;
        var halfSolidWidth = solid.Width * 0.5f;
        var halfSolidHeight = solid.Height * 0.5f;
        return RagdollProjectionOverlaps(delta, Vector2.UnitX, alongAxis, acrossAxis,
                   halfLength, halfThickness, halfSolidWidth, halfSolidHeight)
            && RagdollProjectionOverlaps(delta, Vector2.UnitY, alongAxis, acrossAxis,
                   halfLength, halfThickness, halfSolidWidth, halfSolidHeight)
            && RagdollProjectionOverlaps(delta, alongAxis, alongAxis, acrossAxis,
                   halfLength, halfThickness, halfSolidWidth, halfSolidHeight)
            && RagdollProjectionOverlaps(delta, acrossAxis, alongAxis, acrossAxis,
                   halfLength, halfThickness, halfSolidWidth, halfSolidHeight);
    }

    private static bool RagdollProjectionOverlaps(
        Vector2 centerDelta,
        Vector2 axis,
        Vector2 alongAxis,
        Vector2 acrossAxis,
        float halfLength,
        float halfThickness,
        float halfSolidWidth,
        float halfSolidHeight)
    {
        var ragdollRadius = (halfLength * MathF.Abs(Vector2.Dot(alongAxis, axis)))
            + (halfThickness * MathF.Abs(Vector2.Dot(acrossAxis, axis)));
        var solidRadius = (halfSolidWidth * MathF.Abs(axis.X)) + (halfSolidHeight * MathF.Abs(axis.Y));
        var centerDistance = MathF.Abs(Vector2.Dot(centerDelta, axis));
        return (ragdollRadius + solidRadius - centerDistance) > DynamicRagdollCollisionEpsilon;
    }

    private static bool TryDepenetrateRagdoll(DynamicRagdollState ragdoll, SimpleLevel level, WorldBounds bounds, int maxDistance)
    {
        if (!IsRagdollPoseBlocked(ragdoll, level, bounds)) return true;
        var originX = ragdoll.X;
        var originY = ragdoll.Y;
        ReadOnlySpan<Vector2> directions = [new(0, -1), new(-1, 0), new(1, 0), new(-1, -1), new(1, -1), new(0, 1)];
        for (var distance = 1; distance <= maxDistance; distance++)
        {
            // Resolve at the closest free position without adding launch energy.
            foreach (var direction in directions)
            {
                ragdoll.X = originX + direction.X * distance;
                ragdoll.Y = originY + direction.Y * distance;
                if (!IsRagdollPoseBlocked(ragdoll, level, bounds)) return true;
            }
        }
        ragdoll.X = originX;
        ragdoll.Y = originY;
        return false;
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

            // Must be near the top face â€” not deep inside a tall wall block.
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

    /// <summary>
    /// World position of the waist joint â€” the primary support / mass center of the corpse.
    /// </summary>
    private static Vector2 GetRagdollWaistWorldPosition(DynamicRagdollState ragdoll)
    {
        if (ragdoll.IsBisectedHalf)
        {
            return new Vector2(ragdoll.X, ragdoll.Y);
        }

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

        // Stock DeadS: middle of opaque span (waist-ish along DeadS).
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

        if (ragdoll.IsBisectedHalf)
        {
            var localOpaque = ragdoll.BisectedOpaqueLocalBounds;
            var centerOffset = new Vector2(
                localOpaque.Left + (localOpaque.Width * 0.5f) - (opaque.Width * 0.5f),
                localOpaque.Top + (localOpaque.Height * 0.5f) - (opaque.Height * 0.5f));
            nodes[0] = root + TransformRagdollLocal(centerOffset, scaleX, bodyRotationRadians);
            return 1;
        }

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

        // Stock DeadS: horizontal opaque spine.
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

        // Stock DeadS is authored horizontally; zero/180 degrees lie flat.
        return MathF.Abs(NormalizeRagdollRotationDegrees(ragdoll.RotationDegrees))
            <= MathF.Abs(NormalizeRagdollRotationDegrees(ragdoll.RotationDegrees - 180f))
            ? 0f : 180f;
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
        if (!_gameplayManager.RuntimeSettings.DynamicRagdollEnabled)
        {
            return false;
        }

        if (!_dynamicRagdolls.ContainsKey(deadBodyId)
            && _world.DeadBodies.Any(body => body.Id == deadBodyId))
        {
            // Draw can precede the next client tick. Use the real death impulse
            // and transfer an immediate pose through the same path as ticking.
            SyncDynamicRagdollsWithDeadBodies();
        }

        if (_dynamicRagdolls.TryGetValue(deadBodyId, out var existingRagdoll))
        {
            ApplyAuthoritativeDeathAnimationKind(existingRagdoll, animationKind);
        }

        if (!_dynamicRagdolls.TryGetValue(deadBodyId, out var ragdoll))
        {
            // Draw can see the authoritative corpse before Sync re-keys the immediate ragdoll.
            var syntheticId = -Math.Abs(sourcePlayerId);
            if (deadBodyId > 0
                && _dynamicRagdolls.Remove(syntheticId, out var transferred))
            {
                transferred.DiedToFire |= ResolveDeadBodyDiedToFire(deadBodyId, sourcePlayerId);
                transferred.DeadBodyId = deadBodyId;
                if (transferred.BisectedPartner is { } transferredPartner)
                {
                    transferredPartner.DeadBodyId = deadBodyId;
                }
                ApplyAuthoritativeDeathAnimationKind(transferred, animationKind);
                _dynamicRagdolls[deadBodyId] = transferred;
                ragdoll = transferred;
            }
            else
            {
                var diedToFire = ResolveDeadBodyDiedToFire(deadBodyId, sourcePlayerId);
                float knockbackX;
                float knockbackY;
                if (diedToFire)
                {
                    knockbackX = 0f;
                    knockbackY = 0f;
                    var sourcePlayer = FindPlayerById(sourcePlayerId);
                    if (sourcePlayer is not null)
                    {
                        knockbackX = sourcePlayer.HorizontalSpeed * (float)_config.FixedDeltaSeconds;
                        knockbackY = sourcePlayer.VerticalSpeed * (float)_config.FixedDeltaSeconds;
                    }
                    else
                    {
                        foreach (var deadBody in _world.DeadBodies)
                        {
                            if (deadBody.Id == deadBodyId || deadBody.SourcePlayerId == sourcePlayerId)
                            {
                                knockbackX = deadBody.HorizontalSpeed;
                                knockbackY = deadBody.VerticalSpeed;
                                break;
                            }
                        }
                    }
                }
                else
                {
                    // Opposite of facing â‰ˆ away from whoever they were aiming at.
                    knockbackX = facingLeft
                        ? CorpseKnockbackRules.MinimumSpeed * 0.75f
                        : -CorpseKnockbackRules.MinimumSpeed * 0.75f;
                    knockbackY = -2.2f;
                }

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
                    knockbackX,
                    knockbackY,
                    diedToFire);
                if (!_dynamicRagdolls.TryGetValue(deadBodyId, out ragdoll))
                {
                    return false;
                }
            }
        }

        if (TryDrawBurnCharredCorpse(
                deadBodyId,
                sourcePlayerId,
                ragdoll.DiedToFire,
                ragdoll.X,
                ragdoll.Y,
                MathF.Max(ragdoll.OpaqueBounds.Height, height),
                ragdoll.FacingLeft,
                ragdoll.GameplayClassId,
                ragdoll.ClassId,
                ragdoll.Team,
                ragdoll.AnimationKind,
                ticksRemaining,
                cameraPosition))
        {
            return true;
        }

        // Acid owns a frozen pose snapshot â€” never swap to the static corpse sprite mid-ragdoll.
        if (TryDrawExistingCorpseAcidDissolve(
                deadBodyId,
                sourcePlayerId,
                ticksRemaining,
                cameraPosition))
        {
            return true;
        }

        return DrawDynamicRagdollVisual(ragdoll, ticksRemaining, cameraPosition);
    }

    private bool ResolveDeadBodyDiedToFire(int deadBodyId, int sourcePlayerId)
    {
        foreach (var deadBody in _world.DeadBodies)
        {
            if (deadBody.Id == deadBodyId || deadBody.SourcePlayerId == sourcePlayerId)
            {
                return deadBody.DiedToFire;
            }
        }

        return _immediateNetworkDeadBodies.TryGetValue(sourcePlayerId, out var immediate) && immediate.DiedToFire;
    }

    private bool TryGetRagdollCorpseTicksRemaining(DynamicRagdollState ragdoll, out int ticksRemaining)
    {
        foreach (var deadBody in _world.DeadBodies)
        {
            if (deadBody.Id == ragdoll.DeadBodyId
                || (ragdoll.DeadBodyId <= 0 && deadBody.SourcePlayerId == ragdoll.SourcePlayerId))
            {
                ticksRemaining = deadBody.TicksRemaining;
                return true;
            }
        }

        if (_immediateNetworkDeadBodies.TryGetValue(ragdoll.SourcePlayerId, out var immediate))
        {
            ticksRemaining = immediate.TicksRemaining;
            return true;
        }

        ticksRemaining = 0;
        return false;
    }

    private bool DrawDynamicRagdollVisual(
        DynamicRagdollState ragdoll,
        int ticksRemaining,
        Vector2 cameraPosition,
        Color? tintOverride = null)
    {
        if (ragdoll.IsBisectedHalf)
        {
            var upperDrawn = DrawElkondoRagdollVisual(ragdoll, ticksRemaining, cameraPosition, tintOverride);
            var lowerDrawn = ragdoll.BisectedPartner is { } lower
                && DrawElkondoRagdollVisual(lower, ticksRemaining, cameraPosition, tintOverride);
            return upperDrawn || lowerDrawn;
        }

        if (ragdoll.UseElkondoVerticalVisual)
        {
            return DrawElkondoRagdollVisual(ragdoll, ticksRemaining, cameraPosition, tintOverride);
        }

        var fadeAlpha = tintOverride.HasValue ? 1f : GetCorpseFadeAlpha(ticksRemaining);
        // Only end-of-life fade (last Regular/Acid ticks) can zero alpha â€” never treat that as "drawn"
        // when we somehow got a non-positive lifetime mid-flight; fall through to the static corpse.
        if (fadeAlpha <= 0.001f)
        {
            return ticksRemaining <= 0;
        }

        var spriteName = GetDynamicRagdollCorpseSpriteName(
            ragdoll.GameplayClassId,
            ragdoll.ClassId,
            ragdoll.Team,
            ragdoll.AnimationKind);
        var sprite = spriteName is null ? null : GetResolvedSprite(spriteName);
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
        var tint = (tintOverride ?? Color.White) * fadeAlpha;
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
        float pivotDegrees,
        float scaleX,
        Color tint)
    {
        var absPivot = MathF.Abs(pivotDegrees);
        if (absPivot < 0.75f)
        {
            return;
        }

        // Stretch a 2px seam column into the open wedge â€” parts stay rigid, only the fill stretches.
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
