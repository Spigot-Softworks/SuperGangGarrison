using System;

namespace OpenGarrison.Core;

internal sealed partial class MapLogicSystem
{

    internal float GetDamageableZoneHealth(int roomObjectIndex)
    {
        if (roomObjectIndex < 0 || roomObjectIndex >= _host.Level.RoomObjects.Count)
        {
            return 0f;
        }

        ref readonly var marker = ref _host.Level.GetRoomObject(roomObjectIndex);
        if (marker.Type != RoomObjectType.DamageableZone)
        {
            return 0f;
        }

        EnsureDamageableZoneHealthInitialized();
        return _host.MapRuntime.DamageableZoneHealth[roomObjectIndex];
    }

    internal float GetDamageableZoneHealthRatio(int roomObjectIndex)
    {
        if (roomObjectIndex < 0 || roomObjectIndex >= _host.Level.RoomObjects.Count)
        {
            return 1f;
        }

        ref readonly var marker = ref _host.Level.GetRoomObject(roomObjectIndex);
        if (marker.Type != RoomObjectType.DamageableZone || marker.DamageableZone.MaxHealth <= 0f)
        {
            return 1f;
        }

        return Math.Clamp(GetDamageableZoneHealth(roomObjectIndex) / marker.DamageableZone.MaxHealth, 0f, 1f);
    }

    internal bool BlocksProjectileDamageableZone(int roomObjectIndex)
    {
        if (roomObjectIndex < 0 || roomObjectIndex >= _host.Level.RoomObjects.Count || !_host.Level.IsRoomObjectActive(roomObjectIndex))
        {
            return false;
        }

        ref readonly var marker = ref _host.Level.GetRoomObject(roomObjectIndex);
        if (marker.Type != RoomObjectType.DamageableZone)
        {
            return false;
        }

        return DamageableMetadata.BlocksProjectiles(marker.DamageableZone, GetDamageableZoneHealth(roomObjectIndex));
    }

    internal bool TryApplyDamageableZoneDamage(int roomObjectIndex, float damage, PlayerTeam? damagingTeam = null)
    {
        if (damage <= 0f
            || roomObjectIndex < 0
            || roomObjectIndex >= _host.Level.RoomObjects.Count
            || !_host.Level.IsRoomObjectActive(roomObjectIndex))
        {
            return false;
        }

        ref readonly var marker = ref _host.Level.GetRoomObject(roomObjectIndex);
        if (marker.Type != RoomObjectType.DamageableZone)
        {
            return false;
        }

        EnsureDamageableZoneHealthInitialized();
        var previousHealth = _host.MapRuntime.DamageableZoneHealth[roomObjectIndex];
        if (previousHealth <= 0f)
        {
            return false;
        }

        _host.MapRuntime.DamageableZoneHealth[roomObjectIndex] = MathF.Max(0f, previousHealth - damage);
        _host.Level.DamageableZoneCurrentHealth = _host.MapRuntime.DamageableZoneHealth;
        if (damagingTeam.HasValue)
        {
            _host.MapRuntime.DamageableZoneLastDamagingTeam[roomObjectIndex] = damagingTeam;
        }

        EvaluateMapLogicDamageTriggersIfNeeded();
        return true;
    }

    private void EnsureDamageableZoneHealthInitialized()
    {
        if (_host.MapRuntime.DamageableZoneHealth.Length == _host.Level.RoomObjects.Count)
        {
            return;
        }

        ResetDamageableZoneHealth();
    }

    private void ResetDamageableZoneHealth()
    {
        _host.MapRuntime.DamageableZoneHealth = new float[_host.Level.RoomObjects.Count];
        _host.MapRuntime.DamageableZoneLastDamagingTeam = new PlayerTeam?[_host.Level.RoomObjects.Count];
        for (var index = 0; index < _host.Level.RoomObjects.Count; index += 1)
        {
            ref readonly var marker = ref _host.Level.GetRoomObject(index);
            _host.MapRuntime.DamageableZoneHealth[index] = marker.Type == RoomObjectType.DamageableZone
                ? marker.DamageableZone.MaxHealth
                : 0f;
        }

        _host.Level.DamageableZoneCurrentHealth = _host.MapRuntime.DamageableZoneHealth;
    }

    private void ApplyDamageableZoneHealWhenSignals()
    {
        foreach (var index in _host.Level.GetRoomObjectIndices(RoomObjectType.DamageableZone))
        {
            ref readonly var marker = ref _host.Level.GetRoomObject(index);

            var healWhenNodeIndex = marker.DamageableZone.HealWhenNodeIndex;
            if (healWhenNodeIndex < 0 || !_host.Level.LogicGraph.GetOutput(healWhenNodeIndex))
            {
                continue;
            }

            HealDamageableZone(index);
        }
    }

    private void HealDamageableZone(int roomObjectIndex)
    {
        if (roomObjectIndex < 0 || roomObjectIndex >= _host.Level.RoomObjects.Count)
        {
            return;
        }

        ref readonly var marker = ref _host.Level.GetRoomObject(roomObjectIndex);
        if (marker.Type != RoomObjectType.DamageableZone)
        {
            return;
        }

        EnsureDamageableZoneHealthInitialized();
        var previousHealth = _host.MapRuntime.DamageableZoneHealth[roomObjectIndex];
        var maxHealth = marker.DamageableZone.MaxHealth;
        if (MathF.Abs(previousHealth - maxHealth) <= 0.001f)
        {
            return;
        }

        _host.MapRuntime.DamageableZoneHealth[roomObjectIndex] = maxHealth;
        _host.Level.DamageableZoneCurrentHealth = _host.MapRuntime.DamageableZoneHealth;
        if (roomObjectIndex < _host.MapRuntime.DamageableZoneLastDamagingTeam.Length)
        {
            _host.MapRuntime.DamageableZoneLastDamagingTeam[roomObjectIndex] = null;
        }

        EvaluateMapLogicDamageTriggersIfNeeded();
    }

    private PlayerTeam? GetDamageableZoneLastDamagingTeam(int roomObjectIndex)
    {
        if (roomObjectIndex < 0 || roomObjectIndex >= _host.MapRuntime.DamageableZoneLastDamagingTeam.Length)
        {
            return null;
        }

        return _host.MapRuntime.DamageableZoneLastDamagingTeam[roomObjectIndex];
    }

    private DamageTriggerEvaluationContext CreateDamageTriggerEvaluationContext()
    {
        return new DamageTriggerEvaluationContext(
            GetDamageableZoneHealthRatio,
            GetDamageableZoneLastDamagingTeam);
    }

    private void EvaluateMapLogicDamageTriggersIfNeeded()
    {
        if (!_host.Level.LogicGraph.HasDamageTriggers)
        {
            return;
        }

        _host.Level.LogicGraph.EvaluateDamageTriggers(CreateDamageTriggerEvaluationContext());
        EvaluateMapLogicScoreTriggersIfNeeded();
        ApplyControlPointLogicLockTriggers();
        ApplyMapLogicActivators();
    }

    internal bool TryHandleProjectileDamageableZoneHit(in ShotHitResult hitResult, float damage, PlayerTeam damagingTeam)
    {
        if (hitResult.HitDamageableZoneRoomObjectIndex < 0)
        {
            return false;
        }

        TryApplyDamageableZoneDamage(hitResult.HitDamageableZoneRoomObjectIndex, damage, damagingTeam);
        return true;
    }

    internal void ApplyExplosiveDamageToDamageableZones(
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

        for (var index = 0; index < _host.Level.RoomObjects.Count; index += 1)
        {
            if (index == excludeRoomObjectIndex || !_host.Level.IsRoomObjectActive(index))
            {
                continue;
            }

            ref readonly var marker = ref _host.Level.GetRoomObject(index);
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
        return SimulationMath.DistanceBetween(x, y, closestX, closestY);
    }
}
