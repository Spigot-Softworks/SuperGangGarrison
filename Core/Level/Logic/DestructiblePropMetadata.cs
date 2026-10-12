using System.Globalization;

namespace OpenGarrison.Core;

public enum PropDestructionEffect { FadeOut, BreakApart }
public enum PropRespawnEffect { FadeIn, StaticWarp }

public readonly record struct DestructiblePropSettings(
    PropDestructionEffect Destruction = PropDestructionEffect.FadeOut,
    float MinimumOpacity = 0f,
    PropRespawnEffect Respawn = PropRespawnEffect.FadeIn,
    float TransitionSeconds = 0.5f,
    string DestructionSound = "",
    string RespawnSound = "")
{
    public float GetOpacity(bool destroyed, float ageSeconds)
    {
        var progress = TransitionSeconds <= 0f ? 1f : Math.Clamp(ageSeconds / TransitionSeconds, 0f, 1f);
        if (destroyed) return Destruction == PropDestructionEffect.BreakApart ? 0f : 1f - progress * (1f - MinimumOpacity);
        var start = Destruction == PropDestructionEffect.BreakApart ? 0f : MinimumOpacity;
        return start + progress * (1f - start);
    }
}

public static class DestructiblePropMetadata
{
    public const string ZoneKey = DamageTriggerMetadata.DamageableEntityPropertyKey;
    public const string DestructionKey = "onDestruction";
    public const string MinimumOpacityKey = "destroyedOpacity";
    public const string RespawnKey = "onRespawn";
    public const string TransitionKey = "destructionTransitionTime";
    public const string DestructionSoundKey = "destructionSound";
    public const string RespawnSoundKey = "respawnSound";
    public static readonly string[] EffectKeys = [DestructionKey, MinimumOpacityKey, RespawnKey, TransitionKey, DestructionSoundKey, RespawnSoundKey];

    public static float ParseNumber(string? raw, float fallback, float max) =>
        float.TryParse(raw?.Trim().TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && float.IsFinite(value)
            ? Math.Clamp(value, 0f, max) : fallback;

    public static DestructiblePropSettings Parse(IReadOnlyDictionary<string, string>? properties)
    {
        string Read(string key) => properties?.GetValueOrDefault(key) ?? "";
        return new(Read(DestructionKey).Equals("breakApart", StringComparison.OrdinalIgnoreCase) ? PropDestructionEffect.BreakApart : PropDestructionEffect.FadeOut,
            ParseNumber(Read(MinimumOpacityKey), 0f, 100f) / 100f,
            Read(RespawnKey).Equals("staticWarp", StringComparison.OrdinalIgnoreCase) ? PropRespawnEffect.StaticWarp : PropRespawnEffect.FadeIn,
            ParseNumber(Read(TransitionKey), 0.5f, 30f), Read(DestructionSoundKey), Read(RespawnSoundKey));
    }

    public static void ResolveLinks(IList<RoomObjectMarker> objects, IList<MapLightMarker> lights, IReadOnlyList<MapImportedEntity> entities)
    {
        var configuredZones = new HashSet<int>();
        foreach (var entity in entities)
        {
            var properties = entity.Properties;
            if (properties is null) continue;
            var id = properties.GetValueOrDefault(MapLogicMetadata.MapEntityIdPropertyKey);
            if (DamageTriggerMetadata.IsDamageTriggerEntityType(entity.Type)
                && DamageableMetadata.TryResolveRoomObjectIndex(objects.ToArray(), properties.GetValueOrDefault(DamageTriggerMetadata.DamageableEntityPropertyKey), out var zone))
            {
                // The first configured damage trigger owns the zone's presentation settings.
                if (!EffectKeys.Any(properties.ContainsKey) || !configuredZones.Add(zone)) continue;
                var marker = objects[zone];
                objects[zone] = marker with { DestructionSettings = Parse(properties) };
            }
            if (!DamageableMetadata.TryResolveRoomObjectIndex(objects.ToArray(), properties.GetValueOrDefault(ZoneKey), out zone)) continue;
            var entityRef = string.IsNullOrEmpty(id) ? MapLogicEntityReference.FormatEntityRef(entity.Type, entity.X, entity.Y)
                : MapLogicEntityReference.FormatEntityRef(entity.Type, id);
            if (MapLogicEntityReference.TryResolveRoomObjectIndex(objects.ToArray(), entityRef, out var objectIndex, entities))
                objects[objectIndex] = objects[objectIndex] with { DestructibleZoneIndex = zone };
            if (!MapLightMetadata.IsLightEntityType(entity.Type)) continue;
            // Lights have no room marker; their stable builder ID is retained on import.
            for (var i = 0; i < lights.Count; i++)
                if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(lights[i].SourceName)
                    ? lights[i].SourceName.Equals(id, StringComparison.OrdinalIgnoreCase)
                    : Math.Abs(lights[i].X - entity.X) < 0.01f && Math.Abs(lights[i].Y - entity.Y) < 0.01f)
                    lights[i] = lights[i] with { DestructibleZoneIndex = zone };
        }
    }
}

public readonly record struct DamageableZoneRuntimeState(float Health, uint Revision = 0, int StateAgeTicks = 0)
{
    public bool IsDestroyed => Health <= 0f;
}
