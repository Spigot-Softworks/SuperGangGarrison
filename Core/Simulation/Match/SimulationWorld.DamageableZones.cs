using System;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private float[] _damageableZoneHealth = [];

    public float GetDamageableZoneHealth(int roomObjectIndex)
    {
        if (roomObjectIndex < 0 || roomObjectIndex >= Level.RoomObjects.Count)
        {
            return 0f;
        }

        var marker = Level.RoomObjects[roomObjectIndex];
        if (marker.Type != RoomObjectType.DamageableZone)
        {
            return 0f;
        }

        EnsureDamageableZoneHealthInitialized();
        return _damageableZoneHealth[roomObjectIndex];
    }

    public float GetDamageableZoneHealthRatio(int roomObjectIndex)
    {
        if (roomObjectIndex < 0 || roomObjectIndex >= Level.RoomObjects.Count)
        {
            return 1f;
        }

        var marker = Level.RoomObjects[roomObjectIndex];
        if (marker.Type != RoomObjectType.DamageableZone || marker.DamageableZone.MaxHealth <= 0f)
        {
            return 1f;
        }

        return Math.Clamp(GetDamageableZoneHealth(roomObjectIndex) / marker.DamageableZone.MaxHealth, 0f, 1f);
    }

    public bool BlocksProjectileDamageableZone(int roomObjectIndex)
    {
        if (roomObjectIndex < 0 || roomObjectIndex >= Level.RoomObjects.Count || !Level.IsRoomObjectActive(roomObjectIndex))
        {
            return false;
        }

        var marker = Level.RoomObjects[roomObjectIndex];
        if (marker.Type != RoomObjectType.DamageableZone)
        {
            return false;
        }

        return DamageableMetadata.BlocksProjectiles(marker.DamageableZone, GetDamageableZoneHealth(roomObjectIndex));
    }

    public bool TryApplyDamageableZoneDamage(int roomObjectIndex, float damage)
    {
        if (damage <= 0f
            || roomObjectIndex < 0
            || roomObjectIndex >= Level.RoomObjects.Count
            || !Level.IsRoomObjectActive(roomObjectIndex))
        {
            return false;
        }

        var marker = Level.RoomObjects[roomObjectIndex];
        if (marker.Type != RoomObjectType.DamageableZone)
        {
            return false;
        }

        EnsureDamageableZoneHealthInitialized();
        var previousHealth = _damageableZoneHealth[roomObjectIndex];
        if (previousHealth <= 0f)
        {
            return false;
        }

        _damageableZoneHealth[roomObjectIndex] = MathF.Max(0f, previousHealth - damage);
        Level.DamageableZoneCurrentHealth = _damageableZoneHealth;
        EvaluateMapLogicDamageTriggersIfNeeded();
        return true;
    }

    private void EnsureDamageableZoneHealthInitialized()
    {
        if (_damageableZoneHealth.Length == Level.RoomObjects.Count)
        {
            return;
        }

        ResetDamageableZoneHealth();
    }

    private void ResetDamageableZoneHealth()
    {
        _damageableZoneHealth = new float[Level.RoomObjects.Count];
        for (var index = 0; index < Level.RoomObjects.Count; index += 1)
        {
            var marker = Level.RoomObjects[index];
            _damageableZoneHealth[index] = marker.Type == RoomObjectType.DamageableZone
                ? marker.DamageableZone.MaxHealth
                : 0f;
        }

        Level.DamageableZoneCurrentHealth = _damageableZoneHealth;
    }

    private void ApplyDamageableZoneHealWhenSignals()
    {
        for (var index = 0; index < Level.RoomObjects.Count; index += 1)
        {
            var marker = Level.RoomObjects[index];
            if (marker.Type != RoomObjectType.DamageableZone)
            {
                continue;
            }

            var healWhenNodeIndex = marker.DamageableZone.HealWhenNodeIndex;
            if (healWhenNodeIndex < 0 || !Level.LogicGraph.GetOutput(healWhenNodeIndex))
            {
                continue;
            }

            HealDamageableZone(index);
        }
    }

    private void HealDamageableZone(int roomObjectIndex)
    {
        if (roomObjectIndex < 0 || roomObjectIndex >= Level.RoomObjects.Count)
        {
            return;
        }

        var marker = Level.RoomObjects[roomObjectIndex];
        if (marker.Type != RoomObjectType.DamageableZone)
        {
            return;
        }

        EnsureDamageableZoneHealthInitialized();
        var previousHealth = _damageableZoneHealth[roomObjectIndex];
        var maxHealth = marker.DamageableZone.MaxHealth;
        if (MathF.Abs(previousHealth - maxHealth) <= 0.001f)
        {
            return;
        }

        _damageableZoneHealth[roomObjectIndex] = maxHealth;
        Level.DamageableZoneCurrentHealth = _damageableZoneHealth;
        EvaluateMapLogicDamageTriggersIfNeeded();
    }

    private DamageTriggerEvaluationContext CreateDamageTriggerEvaluationContext()
    {
        return new DamageTriggerEvaluationContext(GetDamageableZoneHealthRatio);
    }

    private void EvaluateMapLogicDamageTriggersIfNeeded()
    {
        if (!Level.LogicGraph.HasDamageTriggers)
        {
            return;
        }

        Level.LogicGraph.EvaluateDamageTriggers(CreateDamageTriggerEvaluationContext());
        ApplyControlPointLogicLockTriggers();
        ApplyMapLogicActivators();
    }

    private bool TryHandleProjectileDamageableZoneHit(in ShotHitResult hitResult, float damage)
    {
        if (hitResult.HitDamageableZoneRoomObjectIndex < 0)
        {
            return false;
        }

        TryApplyDamageableZoneDamage(hitResult.HitDamageableZoneRoomObjectIndex, damage);
        return true;
    }

    public void ApplyExplosiveDamageToDamageableZones(
        float originX,
        float originY,
        float blastRadius,
        float damage,
        float splashThresholdFactor = 0f,
        int excludeRoomObjectIndex = -1)
    {
        if (damage <= 0f || blastRadius <= 0f)
        {
            return;
        }

        for (var index = 0; index < Level.RoomObjects.Count; index += 1)
        {
            if (index == excludeRoomObjectIndex || !Level.IsRoomObjectActive(index))
            {
                continue;
            }

            var marker = Level.RoomObjects[index];
            if (marker.Type != RoomObjectType.DamageableZone)
            {
                continue;
            }

            var distance = DistanceToRectangle(
                originX,
                originY,
                marker.Left,
                marker.Top,
                marker.Right,
                marker.Bottom);
            if (distance >= blastRadius)
            {
                continue;
            }

            var factor = 1f - (distance / blastRadius);
            if (factor <= splashThresholdFactor)
            {
                continue;
            }

            TryApplyDamageableZoneDamage(index, damage * factor);
        }
    }

    private static float DistanceToRectangle(float x, float y, float left, float top, float right, float bottom)
    {
        var closestX = Math.Clamp(x, left, right);
        var closestY = Math.Clamp(y, top, bottom);
        return DistanceBetween(x, y, closestX, closestY);
    }
}
