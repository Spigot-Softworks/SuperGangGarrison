using System;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{

    public float GetDamageableZoneHealth(int roomObjectIndex)
    {
        if (roomObjectIndex < 0 || roomObjectIndex >= Level.RoomObjects.Count)
        {
            return 0f;
        }

        ref readonly var marker = ref Level.GetRoomObject(roomObjectIndex);
        if (marker.Type != RoomObjectType.DamageableZone)
        {
            return 0f;
        }

        EnsureDamageableZoneHealthInitialized();
        return MapRuntime.DamageableZoneHealth[roomObjectIndex];
    }

    public float GetDamageableZoneHealthRatio(int roomObjectIndex)
    {
        if (roomObjectIndex < 0 || roomObjectIndex >= Level.RoomObjects.Count)
        {
            return 1f;
        }

        ref readonly var marker = ref Level.GetRoomObject(roomObjectIndex);
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

        ref readonly var marker = ref Level.GetRoomObject(roomObjectIndex);
        if (marker.Type != RoomObjectType.DamageableZone)
        {
            return false;
        }

        return DamageableMetadata.BlocksProjectiles(marker.DamageableZone, GetDamageableZoneHealth(roomObjectIndex));
    }

    public bool TryApplyDamageableZoneDamage(int roomObjectIndex, float damage, PlayerTeam? damagingTeam = null)
    {
        if (damage <= 0f
            || roomObjectIndex < 0
            || roomObjectIndex >= Level.RoomObjects.Count
            || !Level.IsRoomObjectActive(roomObjectIndex))
        {
            return false;
        }

        ref readonly var marker = ref Level.GetRoomObject(roomObjectIndex);
        if (marker.Type != RoomObjectType.DamageableZone)
        {
            return false;
        }

        EnsureDamageableZoneHealthInitialized();
        var previousHealth = MapRuntime.DamageableZoneHealth[roomObjectIndex];
        if (previousHealth <= 0f)
        {
            return false;
        }

        MapRuntime.DamageableZoneHealth[roomObjectIndex] = MathF.Max(0f, previousHealth - damage);
        Level.DamageableZoneCurrentHealth = MapRuntime.DamageableZoneHealth;
        if (damagingTeam.HasValue)
        {
            MapRuntime.DamageableZoneLastDamagingTeam[roomObjectIndex] = damagingTeam;
        }

        EvaluateMapLogicDamageTriggersIfNeeded();
        return true;
    }

    private void EnsureDamageableZoneHealthInitialized()
    {
        if (MapRuntime.DamageableZoneHealth.Length == Level.RoomObjects.Count)
        {
            return;
        }

        ResetDamageableZoneHealth();
    }

    private void ResetDamageableZoneHealth()
    {
        MapRuntime.DamageableZoneHealth = new float[Level.RoomObjects.Count];
        MapRuntime.DamageableZoneLastDamagingTeam = new PlayerTeam?[Level.RoomObjects.Count];
        for (var index = 0; index < Level.RoomObjects.Count; index += 1)
        {
            ref readonly var marker = ref Level.GetRoomObject(index);
            MapRuntime.DamageableZoneHealth[index] = marker.Type == RoomObjectType.DamageableZone
                ? marker.DamageableZone.MaxHealth
                : 0f;
        }

        Level.DamageableZoneCurrentHealth = MapRuntime.DamageableZoneHealth;
    }

    private void ApplyDamageableZoneHealWhenSignals()
    {
        foreach (var index in Level.GetRoomObjectIndices(RoomObjectType.DamageableZone))
        {
            ref readonly var marker = ref Level.GetRoomObject(index);

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

        ref readonly var marker = ref Level.GetRoomObject(roomObjectIndex);
        if (marker.Type != RoomObjectType.DamageableZone)
        {
            return;
        }

        EnsureDamageableZoneHealthInitialized();
        var previousHealth = MapRuntime.DamageableZoneHealth[roomObjectIndex];
        var maxHealth = marker.DamageableZone.MaxHealth;
        if (MathF.Abs(previousHealth - maxHealth) <= 0.001f)
        {
            return;
        }

        MapRuntime.DamageableZoneHealth[roomObjectIndex] = maxHealth;
        Level.DamageableZoneCurrentHealth = MapRuntime.DamageableZoneHealth;
        if (roomObjectIndex < MapRuntime.DamageableZoneLastDamagingTeam.Length)
        {
            MapRuntime.DamageableZoneLastDamagingTeam[roomObjectIndex] = null;
        }

        EvaluateMapLogicDamageTriggersIfNeeded();
    }

    private PlayerTeam? GetDamageableZoneLastDamagingTeam(int roomObjectIndex)
    {
        if (roomObjectIndex < 0 || roomObjectIndex >= MapRuntime.DamageableZoneLastDamagingTeam.Length)
        {
            return null;
        }

        return MapRuntime.DamageableZoneLastDamagingTeam[roomObjectIndex];
    }

    private DamageTriggerEvaluationContext CreateDamageTriggerEvaluationContext()
    {
        return new DamageTriggerEvaluationContext(
            GetDamageableZoneHealthRatio,
            GetDamageableZoneLastDamagingTeam);
    }

    private void EvaluateMapLogicDamageTriggersIfNeeded()
    {
        if (!Level.LogicGraph.HasDamageTriggers)
        {
            return;
        }

        Level.LogicGraph.EvaluateDamageTriggers(CreateDamageTriggerEvaluationContext());
        EvaluateMapLogicScoreTriggersIfNeeded();
        ApplyControlPointLogicLockTriggers();
        ApplyMapLogicActivators();
    }

    private bool TryHandleProjectileDamageableZoneHit(in ShotHitResult hitResult, float damage, PlayerTeam damagingTeam)
    {
        if (hitResult.HitDamageableZoneRoomObjectIndex < 0)
        {
            return false;
        }

        TryApplyDamageableZoneDamage(hitResult.HitDamageableZoneRoomObjectIndex, damage, damagingTeam);
        return true;
    }

    public void ApplyExplosiveDamageToDamageableZones(
        float originX,
        float originY,
        float blastRadius,
        float damage,
        float splashThresholdFactor = 0f,
        int excludeRoomObjectIndex = -1,
        PlayerTeam? damagingTeam = null,
        float minimumSplashDamage = 0f)
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

            ref readonly var marker = ref Level.GetRoomObject(index);
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

            TryApplyDamageableZoneDamage(
                index,
                MathF.Max(MathF.Max(0f, minimumSplashDamage), damage * factor),
                damagingTeam);
        }
    }

    private static float DistanceToRectangle(float x, float y, float left, float top, float right, float bottom)
    {
        var closestX = Math.Clamp(x, left, right);
        var closestY = Math.Clamp(y, top, bottom);
        return DistanceBetween(x, y, closestX, closestY);
    }
}
