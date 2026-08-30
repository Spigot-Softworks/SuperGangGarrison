#nullable enable

using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Client;

/// <summary>
/// The projectile families represented by the small, client-only fire preview.
/// These are deliberately presentation categories, not simulation entity types.
/// </summary>
internal enum PredictedWeaponFireVisualFamily
{
    None,
    Shot,
    Needle,
    Rocket,
    Mine,
    Grenade,
    Flame,
    Bubble,
    Revolver,
    Rifle,
}

public partial class Game1
{
    // This is only long enough to bridge the input/render boundary. The real
    // projectile remains authoritative and is responsible for all collision,
    // damage, sound events, and simulation state.
    private const float PredictedWeaponFireVisualLifetimeSeconds = 0.075f;
    private const float PredictedWeaponFireVisualMuzzleOffset = 8f;
    private const int MaxPredictedWeaponFireVisuals = 16;

    private readonly List<PredictedWeaponFireVisual> _predictedWeaponFireVisuals = new();

    private sealed class PredictedWeaponFireVisual
    {
        public PredictedWeaponFireVisual(
            int ownerId,
            PlayerTeam team,
            PredictedWeaponFireVisualFamily family,
            float startX,
            float startY,
            Vector2 direction,
            float length,
            HashSet<int> baselineProjectileIds)
        {
            OwnerId = ownerId;
            Team = team;
            Family = family;
            StartX = startX;
            StartY = startY;
            Direction = direction;
            Length = length;
            RemainingSeconds = PredictedWeaponFireVisualLifetimeSeconds;
            BaselineProjectileIds = baselineProjectileIds;
        }

        public int OwnerId { get; }

        public PlayerTeam Team { get; }

        public PredictedWeaponFireVisualFamily Family { get; }

        public float StartX { get; }

        public float StartY { get; }

        public Vector2 Direction { get; }

        public float Length { get; }

        public float RemainingSeconds { get; set; }

        public HashSet<int> BaselineProjectileIds { get; }
    }

    private void QueuePredictedWeaponFireVisual(PlayerEntity player, PlayerInputSnapshot input)
    {
        var (weaponKind, behaviorId) = GetImmediateWeaponPresentationSelection(player);
        var family = ResolvePredictedWeaponFireVisualFamily(weaponKind, behaviorId);
        if (family == PredictedWeaponFireVisualFamily.None)
        {
            return;
        }

        // A registered custom executor owns its projectile semantics. Only the
        // stock routed families get this generic line preview.
        if (IsCustomPrimaryWeaponExecutor(behaviorId))
        {
            return;
        }

        var direction = ResolvePredictedWeaponFireDirection(player, input);
        var baselineProjectileIds = CapturePredictedFireProjectileIds(player.Id, family);
        _predictedWeaponFireVisuals.Add(new PredictedWeaponFireVisual(
            player.Id,
            player.Team,
            family,
            player.X,
            player.Y,
            direction,
            GetPredictedWeaponFireVisualLength(family),
            baselineProjectileIds));

        if (_predictedWeaponFireVisuals.Count > MaxPredictedWeaponFireVisuals)
        {
            _predictedWeaponFireVisuals.RemoveAt(0);
        }
    }

    private (PrimaryWeaponKind WeaponKind, string? BehaviorId) GetImmediateWeaponPresentationSelection(PlayerEntity player)
    {
        if (player.IsAcquiredWeaponEquipped && player.AcquiredWeapon is { } acquiredWeapon)
        {
            return (acquiredWeapon.Kind, player.AcquiredBehaviorId);
        }

        if (player.IsExperimentalOffhandSelected && player.ExperimentalOffhandWeapon is { } offhandWeapon)
        {
            var behaviorId = player.EquippedBehaviorId
                ?? player.SecondaryBehaviorId
                ?? player.UtilityBehaviorId;
            return (offhandWeapon.Kind, behaviorId);
        }

        return (player.PrimaryWeapon.Kind, player.PrimaryBehaviorId);
    }

    private bool IsCustomPrimaryWeaponExecutor(string? behaviorId)
    {
        return !string.IsNullOrWhiteSpace(behaviorId)
            && CharacterClassCatalog.RuntimeRegistry.TryGetPrimaryWeaponBinding(behaviorId, out var binding)
            && binding.Executor is not null
            && !string.Equals(behaviorId, BuiltInGameplayBehaviorIds.ScoutNailgun, StringComparison.Ordinal);
    }

    internal static PredictedWeaponFireVisualFamily ResolvePredictedWeaponFireVisualFamily(
        PrimaryWeaponKind weaponKind,
        string? behaviorId)
    {
        if (string.Equals(behaviorId, BuiltInGameplayBehaviorIds.ScoutNailgun, StringComparison.Ordinal))
        {
            return PredictedWeaponFireVisualFamily.Needle;
        }

        // Bow release is charge/release driven and is intentionally left to its
        // dedicated presentation path.
        if (string.Equals(behaviorId, BuiltInGameplayBehaviorIds.SniperBow, StringComparison.Ordinal))
        {
            return PredictedWeaponFireVisualFamily.None;
        }

        return weaponKind switch
        {
            PrimaryWeaponKind.PelletGun => PredictedWeaponFireVisualFamily.Shot,
            PrimaryWeaponKind.FlameThrower => PredictedWeaponFireVisualFamily.Flame,
            PrimaryWeaponKind.RocketLauncher => PredictedWeaponFireVisualFamily.Rocket,
            PrimaryWeaponKind.MineLauncher => PredictedWeaponFireVisualFamily.Mine,
            PrimaryWeaponKind.Minigun => PredictedWeaponFireVisualFamily.Shot,
            PrimaryWeaponKind.Rifle => PredictedWeaponFireVisualFamily.Rifle,
            PrimaryWeaponKind.Revolver => PredictedWeaponFireVisualFamily.Revolver,
            PrimaryWeaponKind.Blade => PredictedWeaponFireVisualFamily.Bubble,
            PrimaryWeaponKind.GrenadeLauncher => PredictedWeaponFireVisualFamily.Grenade,
            // Medigun target selection and custom executors have semantics that
            // cannot be represented by a generic projectile preview.
            PrimaryWeaponKind.Medigun or PrimaryWeaponKind.Custom => PredictedWeaponFireVisualFamily.None,
            _ => PredictedWeaponFireVisualFamily.None,
        };
    }

    internal static float GetPredictedWeaponFireVisualLength(PredictedWeaponFireVisualFamily family)
    {
        return family switch
        {
            PredictedWeaponFireVisualFamily.Flame => 22f,
            PredictedWeaponFireVisualFamily.Rocket
                or PredictedWeaponFireVisualFamily.Mine
                or PredictedWeaponFireVisualFamily.Grenade => 25f,
            PredictedWeaponFireVisualFamily.Needle
                or PredictedWeaponFireVisualFamily.Revolver => 21f,
            PredictedWeaponFireVisualFamily.Rifle => 28f,
            PredictedWeaponFireVisualFamily.Bubble => 20f,
            PredictedWeaponFireVisualFamily.Shot => 18f,
            _ => 0f,
        };
    }

    private static Vector2 ResolvePredictedWeaponFireDirection(PlayerEntity player, PlayerInputSnapshot input)
    {
        var direction = new Vector2(input.AimWorldX - player.X, input.AimWorldY - player.Y);
        if (!IsFiniteVector(direction) || direction.LengthSquared() < 0.01f)
        {
            direction = new Vector2(player.FacingDirectionX, 0f);
        }

        if (!IsFiniteVector(direction) || direction.LengthSquared() < 0.01f)
        {
            direction = Vector2.UnitX;
        }

        direction.Normalize();
        return direction;
    }

    private HashSet<int> CapturePredictedFireProjectileIds(
        int ownerId,
        PredictedWeaponFireVisualFamily family)
    {
        var ids = new HashSet<int>();
        foreach (var entity in _world.Entities.Values)
        {
            if (IsMatchingAuthoritativeProjectile(entity, ownerId, family))
            {
                ids.Add(entity.Id);
            }
        }

        return ids;
    }

    private static bool IsMatchingAuthoritativeProjectile(
        SimulationEntity entity,
        int ownerId,
        PredictedWeaponFireVisualFamily family)
    {
        if (family == PredictedWeaponFireVisualFamily.None)
        {
            return false;
        }

        return family switch
        {
            PredictedWeaponFireVisualFamily.Shot
                => entity is ShotProjectileEntity projectile && projectile.OwnerId == ownerId,
            PredictedWeaponFireVisualFamily.Needle
                => entity is NailProjectileEntity projectile && projectile.OwnerId == ownerId,
            PredictedWeaponFireVisualFamily.Rocket
                => entity is RocketProjectileEntity projectile && projectile.OwnerId == ownerId,
            PredictedWeaponFireVisualFamily.Mine
                => entity is MineProjectileEntity projectile && projectile.OwnerId == ownerId,
            PredictedWeaponFireVisualFamily.Grenade
                => entity is GrenadeProjectileEntity projectile && projectile.OwnerId == ownerId,
            PredictedWeaponFireVisualFamily.Flame
                => entity is FlameProjectileEntity projectile && projectile.OwnerId == ownerId,
            PredictedWeaponFireVisualFamily.Bubble
                => entity is BubbleProjectileEntity projectile && projectile.OwnerId == ownerId,
            PredictedWeaponFireVisualFamily.Revolver
                => entity is RevolverProjectileEntity projectile && projectile.OwnerId == ownerId,
            // Rifle is represented by a combat trace rather than an entity.
            PredictedWeaponFireVisualFamily.Rifle => false,
            _ => false,
        };
    }

    private void AdvancePredictedWeaponFireVisuals(float elapsedSeconds)
    {
        if (_predictedWeaponFireVisuals.Count == 0)
        {
            return;
        }

        elapsedSeconds = Math.Clamp(elapsedSeconds, 0f, 0.1f);
        for (var visualIndex = _predictedWeaponFireVisuals.Count - 1; visualIndex >= 0; visualIndex -= 1)
        {
            var visual = _predictedWeaponFireVisuals[visualIndex];
            visual.RemainingSeconds -= elapsedSeconds;
            if (visual.RemainingSeconds <= 0f
                || HasMatchingAuthoritativeProjectileArrived(visual)
                || !_world.LocalPlayer.IsAlive)
            {
                _predictedWeaponFireVisuals.RemoveAt(visualIndex);
            }
        }
    }

    private bool HasMatchingAuthoritativeProjectileArrived(PredictedWeaponFireVisual visual)
    {
        if (visual.Family == PredictedWeaponFireVisualFamily.Rifle
            || visual.Family == PredictedWeaponFireVisualFamily.None)
        {
            // Rifle fire is a server-side combat trace, not a projectile entity.
            return false;
        }

        foreach (var entity in _world.Entities.Values)
        {
            if (IsMatchingAuthoritativeProjectile(entity, visual.OwnerId, visual.Family)
                && !visual.BaselineProjectileIds.Contains(entity.Id))
            {
                return true;
            }
        }

        return false;
    }

    private void ClearPredictedWeaponFireVisuals()
    {
        _predictedWeaponFireVisuals.Clear();
    }

    private void DrawPredictedWeaponFireVisuals(Vector2 cameraPosition)
    {
        foreach (var visual in _predictedWeaponFireVisuals)
        {
            var alpha = Math.Clamp(visual.RemainingSeconds / PredictedWeaponFireVisualLifetimeSeconds, 0f, 1f);
            var muzzle = new Vector2(visual.StartX, visual.StartY)
                + visual.Direction * PredictedWeaponFireVisualMuzzleOffset;
            var end = muzzle + visual.Direction * visual.Length;
            var color = GetPredictedWeaponFireVisualColor(visual.Team) * alpha;

            DrawWorldLine(muzzle.X, muzzle.Y, end.X, end.Y, cameraPosition, color, 2f);
            DrawWorldLine(
                muzzle.X,
                muzzle.Y,
                end.X,
                end.Y,
                cameraPosition,
                Color.White * (alpha * 0.35f),
                1f);
        }
    }

    private static Color GetPredictedWeaponFireVisualColor(PlayerTeam team)
    {
        return team switch
        {
            PlayerTeam.Blue => new Color(130, 185, 255),
            PlayerTeam.Red => new Color(255, 210, 140),
            _ => new Color(220, 220, 220),
        };
    }
}
