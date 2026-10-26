#nullable enable

using System;
using System.Collections.Generic;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private static readonly IReadOnlyList<string> GarrisonBuilderLogicRefPropertyKeys = BuilderReferenceProperties.Logic;
    private static readonly IReadOnlyList<string> GarrisonBuilderEntityRefPropertyKeys = BuilderReferenceProperties.Entity;

    private bool TryDuplicateGarrisonBuilderEntities(
        IReadOnlyList<int> sourceIndices,
        bool placeAtSourcePosition,
        out List<int> duplicateIndices)
    {
        duplicateIndices = [];
        if (sourceIndices.Count == 0)
        {
            return false;
        }

        EnsureGarrisonBuilderMapEntityIds();

        var sourceToCloneIndex = new Dictionary<int, int>();
        // Clones sit directly to the right of the whole selection, level with it, all moved
        // by the same amount so a multi-selection keeps its exact layout.
        var offsetX = placeAtSourcePosition ? 0f : GetGarrisonBuilderCloneOffsetX(sourceIndices);
        foreach (var sourceIndex in sourceIndices)
        {
            if (sourceIndex < 0 || sourceIndex >= _builderEntities.Count)
            {
                continue;
            }

            var source = _builderEntities[sourceIndex];
            var clone = placeAtSourcePosition
                ? source
                : source with { X = source.X + offsetX };
            var properties = new Dictionary<string, string>(clone.Properties, StringComparer.OrdinalIgnoreCase);
            properties[MapLogicMetadata.MapEntityIdPropertyKey] = MapLogicMetadata.CreateMapEntityId();
            var sourceName = MapObjectMetadata.Name(properties);
            if (sourceName.Length > 0) properties[MapObjectMetadata.NameKey] = MapObjectMetadata.UniqueName(sourceName, _builderEntities);
            _builderEntities.Add((clone with { Properties = properties }).NormalizeForEditing());
            var cloneIndex = _builderEntities.Count - 1;
            sourceToCloneIndex[sourceIndex] = cloneIndex;
            duplicateIndices.Add(cloneIndex);
        }

        if (duplicateIndices.Count == 0)
        {
            return false;
        }

        RemapGarrisonBuilderClonedLogicConnections(sourceToCloneIndex);
        _builderSelectedEntityType = string.Empty;
        _builderActiveTool = GarrisonBuilderTool.Select;
        return true;
    }

    /// <summary>The selection's width (so clones do not overlap it), snapped up to the grid when aligning.</summary>
    private float GetGarrisonBuilderCloneOffsetX(IReadOnlyList<int> sourceIndices)
    {
        var minLeft = float.MaxValue;
        var maxRight = float.MinValue;
        foreach (var sourceIndex in sourceIndices)
        {
            if (sourceIndex < 0 || sourceIndex >= _builderEntities.Count)
            {
                continue;
            }

            var entity = _builderEntities[sourceIndex];
            if (TryGetGarrisonBuilderEntityWorldBounds(entity, out var left, out _, out var width, out _) && width > 0f)
            {
                minLeft = MathF.Min(minLeft, left);
                maxRight = MathF.Max(maxRight, left + width);
            }
            else
            {
                minLeft = MathF.Min(minLeft, entity.X);
                maxRight = MathF.Max(maxRight, entity.X);
            }
        }

        var offset = maxRight > minLeft ? maxRight - minLeft : 24f;
        if (_builderGridAlign)
        {
            var snap = GetGarrisonBuilderGridSnapWorldSize();
            if (snap > 0.0001f)
            {
                offset = MathF.Ceiling((offset / snap) - 0.001f) * snap;
            }
        }

        return MathF.Max(1f, offset);
    }

    private void RemapGarrisonBuilderClonedLogicConnections(IReadOnlyDictionary<int, int> sourceToCloneIndex)
    {
        var logicKeyMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (sourceIndex, cloneIndex) in sourceToCloneIndex)
        {
            var source = _builderEntities[sourceIndex];
            if (!IsGarrisonBuilderLogicOutputEntity(source))
            {
                continue;
            }

            var oldKey = GetEntityProperty(source.Properties, MapLogicMetadata.LogicKeyPropertyKey, string.Empty);
            if (string.IsNullOrWhiteSpace(oldKey))
            {
                continue;
            }

            var clone = _builderEntities[cloneIndex];
            var properties = new Dictionary<string, string>(clone.Properties, StringComparer.OrdinalIgnoreCase);
            var newKey = MapLogicMetadata.CreateLogicKey();
            properties[MapLogicMetadata.LogicKeyPropertyKey] = newKey;
            logicKeyMap[oldKey] = newKey;
            _builderEntities[cloneIndex] = (clone with { Properties = properties }).NormalizeForEditing();
        }

        foreach (var cloneIndex in sourceToCloneIndex.Values)
        {
            var entity = _builderEntities[cloneIndex];
            var properties = new Dictionary<string, string>(entity.Properties, StringComparer.OrdinalIgnoreCase);
            var changed = false;

            foreach (var propertyKey in GarrisonBuilderLogicRefPropertyKeys)
            {
                if (!properties.TryGetValue(propertyKey, out var value)
                    || string.IsNullOrWhiteSpace(value)
                    || !MapLogicMetadata.TryParseLogicRef(value, out var referencedKey)
                    || !logicKeyMap.TryGetValue(referencedKey, out var mappedKey))
                {
                    continue;
                }

                properties[propertyKey] = MapLogicMetadata.FormatLogicRef(mappedKey);
                changed = true;
            }

            foreach (var propertyKey in GarrisonBuilderEntityRefPropertyKeys)
            {
                if (properties.TryGetValue(propertyKey, out var listValue) && IsGarrisonBuilderMultiEntityRefProperty(propertyKey))
                {
                    var refs = MapLogicEntityReferenceList.Parse(listValue).Select(reference =>
                        TryResolveGarrisonBuilderEntityRefSourceIndex(reference, sourceToCloneIndex, out var sourceIndex)
                            && sourceToCloneIndex.TryGetValue(sourceIndex, out var cloneIndex)
                                ? MapLogicEntityReference.FormatEntityRef(_builderEntities[cloneIndex]) : reference).ToArray();
                    properties[propertyKey] = MapLogicEntityReferenceList.Format(refs);
                    changed = true;
                    continue;
                }
                if (!properties.TryGetValue(propertyKey, out var value)
                    || string.IsNullOrWhiteSpace(value)
                    || !TryResolveGarrisonBuilderEntityRefSourceIndex(value, sourceToCloneIndex, out var referencedSourceIndex)
                    || !sourceToCloneIndex.TryGetValue(referencedSourceIndex, out var mappedIndex))
                {
                    continue;
                }

                properties[propertyKey] = MapLogicEntityReference.FormatEntityRef(_builderEntities[mappedIndex]);
                changed = true;
            }

            if (changed)
            {
                _builderEntities[cloneIndex] = (entity with { Properties = properties }).NormalizeForEditing();
            }
        }
    }

    private bool TryResolveGarrisonBuilderEntityRefSourceIndex(
        string entityRef,
        IReadOnlyDictionary<int, int> sourceToCloneIndex,
        out int sourceIndex)
    {
        sourceIndex = -1;
        if (!MapLogicEntityReference.TryParseEntityRef(
                entityRef,
                out var entityType,
                out var refX,
                out var refY,
                out var mapEntityId))
        {
            return false;
        }

        foreach (var candidateIndex in sourceToCloneIndex.Keys)
        {
            var entity = _builderEntities[candidateIndex];
            if (!entity.Type.Equals(entityType, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(mapEntityId))
            {
                var candidateId = MapLogicMetadata.EnsureMapEntityId(entity.Properties);
                if (!candidateId.Equals(mapEntityId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                sourceIndex = candidateIndex;
                return true;
            }

            var deltaX = entity.X - refX;
            var deltaY = entity.Y - refY;
            var distanceSquared = (deltaX * deltaX) + (deltaY * deltaY);
            if (distanceSquared > MapLogicEntityReference.PositionToleranceSquared)
            {
                continue;
            }

            if (sourceIndex >= 0)
            {
                return false;
            }

            sourceIndex = candidateIndex;
        }

        return sourceIndex >= 0;
    }
}
