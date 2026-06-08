#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private bool _builderLogicMapPickActive;
    private string _builderLogicMapPickTargetPropertyKey = string.Empty;
    private bool _builderEntityMapPickActive;
    private string _builderEntityMapPickTargetPropertyKey = string.Empty;
    private bool _builderLogicRecolorDialogOpen;
    private int _builderLogicRecolorEntityIndex = -1;

    private const float GarrisonBuilderLogicNodeWorldWidth = 26f;
    private const float GarrisonBuilderLogicNodeWorldHeight = 17f;
    private const float GarrisonBuilderLogicActivatorWorldWidth = 32f;
    private const float GarrisonBuilderLogicActivatorWorldHeight = 17f;
    private const float GarrisonBuilderLogicLabelRelativeScale = 0.68f;
    private static readonly Color GarrisonBuilderLogicFillColor = new(48, 168, 156);
    private static readonly Color GarrisonBuilderLogicOutlineColor = Color.Black;
    private static readonly Color GarrisonBuilderLogicLinkColor = new(56, 188, 172, 215);
    private static readonly Color GarrisonBuilderLogicLinkHighlightColor = new(96, 232, 214, 255);

    private readonly Dictionary<long, List<Vector2>> _builderLogicConnectionAnchorScratch = new();

    private Color GetGarrisonBuilderLogicFillColor(CustomMapBuilderEntity entity)
    {
        if (MapLogicNodeColorMetadata.TryResolveFillColor(entity.Properties, out var red, out var green, out var blue))
        {
            return new Color(red, green, blue);
        }

        return GarrisonBuilderLogicFillColor;
    }

    private bool TryGetGarrisonBuilderLogicNodeWorldBounds(
        CustomMapBuilderEntity entity,
        out float left,
        out float top,
        out float width,
        out float height)
    {
        left = top = width = height = 0f;
        if (!MapLogicMetadata.IsLogicEntityType(entity.Type)
            && !AreaExtensionMetadata.IsAreaEntityType(entity.Type))
        {
            return false;
        }

        if (entity.Type.Equals(AreaExtensionMetadata.AreaEntityType, StringComparison.OrdinalIgnoreCase))
        {
            (width, height) = AreaExtensionMetadata.ResolveZoneDimensions(entity.XScale, entity.YScale);
            left = entity.X - (width * 0.5f);
            top = entity.Y - (height * 0.5f);
            return true;
        }

        if (entity.Type.Equals(MapLogicMetadata.PlayerTriggerEntityType, StringComparison.OrdinalIgnoreCase))
        {
            (width, height) = PlayerTriggerMetadata.ResolveZoneDimensions(entity.XScale, entity.YScale);
            left = entity.X - (width * 0.5f);
            top = entity.Y - (height * 0.5f);
            return true;
        }

        if (entity.Type.Equals(DamageableMetadata.DamageableEntityType, StringComparison.OrdinalIgnoreCase))
        {
            (width, height) = DamageableMetadata.ResolveZoneDimensions(entity.XScale, entity.YScale);
            left = entity.X - (width * 0.5f);
            top = entity.Y - (height * 0.5f);
            return true;
        }

        if (entity.Type.Equals(MapLogicMetadata.ActivatorEntityType, StringComparison.OrdinalIgnoreCase)
            || entity.Type.Equals(MapLogicMetadata.TimerEntityType, StringComparison.OrdinalIgnoreCase)
            || entity.Type.Equals(MapLogicMetadata.OscillatorEntityType, StringComparison.OrdinalIgnoreCase))
        {
            width = GarrisonBuilderLogicActivatorWorldWidth;
            height = GarrisonBuilderLogicActivatorWorldHeight;
        }
        else
        {
            width = GarrisonBuilderLogicNodeWorldWidth;
            height = GarrisonBuilderLogicNodeWorldHeight;
        }

        left = entity.X - (width * 0.5f);
        top = entity.Y - (height * 0.5f);
        return true;
    }

    private IReadOnlyList<CustomMapBuilderEntityDefinition> GetLogicGarrisonBuilderEntityDefinitions()
    {
        IEnumerable<CustomMapBuilderEntityDefinition> definitions = _builderEntityDefinitions
            .Where(definition => CustomMapCustomSpriteMetadata.IsLogicPaletteEntityType(definition.Type));
        if (_builderSelectedGameMode != CustomMapBuilderGameMode.Free)
        {
            definitions = definitions.Where(definition =>
                definition.Modes == CustomMapBuilderGameMode.Free
                || (definition.Modes & _builderSelectedGameMode) != 0);
        }

        return definitions.ToArray();
    }

    private void EnsureGarrisonBuilderMapEntityIds()
    {
        for (var index = 0; index < _builderEntities.Count; index += 1)
        {
            var entity = _builderEntities[index];
            var properties = new Dictionary<string, string>(entity.Properties, StringComparer.OrdinalIgnoreCase);
            var mapEntityId = MapLogicMetadata.EnsureMapEntityId(properties);
            properties.TryGetValue(MapLogicMetadata.MapEntityIdPropertyKey, out var existingId);
            if (!mapEntityId.Equals(existingId, StringComparison.OrdinalIgnoreCase))
            {
                properties[MapLogicMetadata.MapEntityIdPropertyKey] = mapEntityId;
                _builderEntities[index] = entity with { Properties = properties };
            }
        }

        UpgradeGarrisonBuilderEntityRefsToStableIds();
    }

    private void RefreshGarrisonBuilderEntityRefsAfterMove(
        IReadOnlyList<(int Index, float X, float Y)> movedSnapshots)
    {
        if (movedSnapshots.Count == 0)
        {
            return;
        }

        var movedUpdates = new List<(string OldRef, string NewRef, string MapEntityId)>(movedSnapshots.Count);
        for (var snapshotIndex = 0; snapshotIndex < movedSnapshots.Count; snapshotIndex += 1)
        {
            var snapshot = movedSnapshots[snapshotIndex];
            if (snapshot.Index < 0 || snapshot.Index >= _builderEntities.Count)
            {
                continue;
            }

            var entity = _builderEntities[snapshot.Index];
            var properties = new Dictionary<string, string>(entity.Properties, StringComparer.OrdinalIgnoreCase);
            var mapEntityId = MapLogicMetadata.EnsureMapEntityId(properties);
            if (!properties.TryGetValue(MapLogicMetadata.MapEntityIdPropertyKey, out var existingId)
                || !mapEntityId.Equals(existingId, StringComparison.OrdinalIgnoreCase))
            {
                properties[MapLogicMetadata.MapEntityIdPropertyKey] = mapEntityId;
                _builderEntities[snapshot.Index] = entity with { Properties = properties };
                entity = _builderEntities[snapshot.Index];
            }

            movedUpdates.Add((
                MapLogicEntityReference.FormatEntityRef(entity.Type, snapshot.X, snapshot.Y),
                MapLogicEntityReference.FormatEntityRef(entity),
                mapEntityId));
        }

        for (var entityIndex = 0; entityIndex < _builderEntities.Count; entityIndex += 1)
        {
            var entity = _builderEntities[entityIndex];
            var properties = new Dictionary<string, string>(entity.Properties, StringComparer.OrdinalIgnoreCase);
            var changed = false;
            foreach (var propertyKey in GarrisonBuilderEntityRefPropertyKeys)
            {
                if (!properties.TryGetValue(propertyKey, out var value) || string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                for (var updateIndex = 0; updateIndex < movedUpdates.Count; updateIndex += 1)
                {
                    var update = movedUpdates[updateIndex];
                    if (value.Equals(update.OldRef, StringComparison.OrdinalIgnoreCase)
                        || value.Equals(update.NewRef, StringComparison.OrdinalIgnoreCase))
                    {
                        properties[propertyKey] = update.NewRef;
                        changed = true;
                        break;
                    }

                    if (!MapLogicEntityReference.TryParseEntityRef(
                            value,
                            out _,
                            out _,
                            out _,
                            out var referencedId)
                        || string.IsNullOrWhiteSpace(referencedId)
                        || !referencedId.Equals(update.MapEntityId, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    properties[propertyKey] = update.NewRef;
                    changed = true;
                    break;
                }
            }

            if (changed)
            {
                _builderEntities[entityIndex] = entity with { Properties = properties };
            }
        }
    }

    private void UpgradeGarrisonBuilderEntityRefsToStableIds()
    {
        for (var index = 0; index < _builderEntities.Count; index += 1)
        {
            var entity = _builderEntities[index];
            var properties = new Dictionary<string, string>(entity.Properties, StringComparer.OrdinalIgnoreCase);
            var changed = false;
            foreach (var propertyKey in GarrisonBuilderEntityRefPropertyKeys)
            {
                if (!properties.TryGetValue(propertyKey, out var value) || string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                if (!MapLogicEntityReference.TryFindBuilderEntityIndex(_builderEntities, value, out var targetIndex))
                {
                    continue;
                }

                var stableRef = MapLogicEntityReference.FormatEntityRef(_builderEntities[targetIndex]);
                if (!stableRef.Equals(value, StringComparison.OrdinalIgnoreCase))
                {
                    properties[propertyKey] = stableRef;
                    changed = true;
                }
            }

            if (changed)
            {
                _builderEntities[index] = entity with { Properties = properties };
            }
        }
    }

    private void EnsureGarrisonBuilderLogicEntityKeys()
    {
        EnsureGarrisonBuilderMapEntityIds();

        for (var index = 0; index < _builderEntities.Count; index += 1)
        {
            var entity = _builderEntities[index];
            if (!MapLogicMetadata.IsLogicEntityType(entity.Type))
            {
                continue;
            }

            var properties = new Dictionary<string, string>(entity.Properties, StringComparer.OrdinalIgnoreCase);
            var logicKey = MapLogicMetadata.EnsureLogicKey(properties);
            var changed = false;
            properties.TryGetValue(MapLogicMetadata.LogicKeyPropertyKey, out var existingKey);
            if (!logicKey.Equals(existingKey, StringComparison.OrdinalIgnoreCase))
            {
                properties[MapLogicMetadata.LogicKeyPropertyKey] = logicKey;
                changed = true;
            }

            if (!properties.ContainsKey(MapLogicMetadata.NodePriorityPropertyKey))
            {
                MapLogicMetadata.EnsureNodePriorityProperty(properties);
                changed = true;
            }

            if (!changed)
            {
                continue;
            }

            _builderEntities[index] = entity with { Properties = properties };
        }
    }

    private CustomMapBuilderEntity ApplyGarrisonBuilderLogicDefaults(CustomMapBuilderEntity entity)
    {
        if (!MapLogicMetadata.IsLogicEntityType(entity.Type))
        {
            return entity;
        }

        var properties = new Dictionary<string, string>(entity.Properties, StringComparer.OrdinalIgnoreCase);
        properties[MapLogicMetadata.LogicKeyPropertyKey] = MapLogicMetadata.EnsureLogicKey(properties);
        MapLogicMetadata.EnsureNodePriorityProperty(properties);

        return entity with { Properties = properties };
    }

    private bool IsGarrisonBuilderLogicOutputEntity(CustomMapBuilderEntity entity)
    {
        return MapLogicMetadata.IsLogicOutputEntityType(entity.Type)
            && !string.IsNullOrWhiteSpace(GetEntityProperty(entity.Properties, MapLogicMetadata.LogicKeyPropertyKey, string.Empty));
    }

    private bool TryFindGarrisonBuilderLogicSource(
        string logicRef,
        out CustomMapBuilderEntity source,
        out int sourceIndex)
    {
        source = default!;
        sourceIndex = -1;
        if (!MapLogicMetadata.TryParseLogicRef(logicRef, out var logicKey))
        {
            return false;
        }

        for (var index = 0; index < _builderEntities.Count; index += 1)
        {
            if (IsGarrisonBuilderEntityHidden(index))
            {
                continue;
            }

            var entity = _builderEntities[index];
            if (!IsGarrisonBuilderLogicOutputEntity(entity))
            {
                continue;
            }

            var entityKey = GetEntityProperty(entity.Properties, MapLogicMetadata.LogicKeyPropertyKey, string.Empty);
            if (!entityKey.Equals(logicKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            source = entity;
            sourceIndex = index;
            return true;
        }

        return false;
    }

    private void BeginGarrisonBuilderLogicMapPick(string propertyKey)
    {
        _builderLogicMapPickActive = true;
        _builderLogicMapPickTargetPropertyKey = propertyKey;
        _builderStatus = "pick a logic node on the map";
    }

    private Rectangle GetGarrisonBuilderLogicMapPickPromptBounds()
    {
        var strip = GetModernGarrisonBuilderLayerStripBounds();
        var buttonWidth = BuilderUi(240);
        var buttonHeight = (int)GetGarrisonBuilderMinimumButtonHeight() + BuilderUi(10);
        var x = (BuilderViewportWidth - buttonWidth) / 2;
        var y = strip.Y - buttonHeight - BuilderUi(10);
        return new Rectangle(x, y, buttonWidth, buttonHeight);
    }

    private void DrawGarrisonBuilderLogicMapPickPrompt(MouseState mouse)
    {
        if (!_builderLogicMapPickActive || !_builderUseModernUi)
        {
            return;
        }

        var bounds = GetGarrisonBuilderLogicMapPickPromptBounds();
        var highlighted = bounds.Contains(mouse.Position);
        DrawGarrisonBuilderBrownPanel(bounds);
        DrawBuilderMenuButton(bounds, "Pick a logic node", highlighted);
    }

    private void CancelGarrisonBuilderLogicMapPick()
    {
        _builderLogicMapPickActive = false;
        _builderLogicMapPickTargetPropertyKey = string.Empty;
    }

    private void BeginGarrisonBuilderEntityMapPick(string propertyKey)
    {
        _builderEntityMapPickActive = true;
        _builderEntityMapPickTargetPropertyKey = propertyKey;
        _builderStatus = propertyKey.Equals(TeleportMetadata.TeleportExitPropertyKey, StringComparison.OrdinalIgnoreCase)
            ? "pick a teleport exit on the map"
            : propertyKey.Equals(AreaExtensionMetadata.ExtendsPropertyKey, StringComparison.OrdinalIgnoreCase)
                ? "pick a player trigger, teleport, or area to extend"
                : propertyKey.Equals(DamageTriggerMetadata.DamageableEntityPropertyKey, StringComparison.OrdinalIgnoreCase)
                    ? "pick a damageable area on the map"
                    : "pick an entity on the map";
    }

    private void CancelGarrisonBuilderEntityMapPick()
    {
        _builderEntityMapPickActive = false;
        _builderEntityMapPickTargetPropertyKey = string.Empty;
    }

    private Rectangle GetGarrisonBuilderEntityMapPickPromptBounds()
    {
        var strip = GetModernGarrisonBuilderLayerStripBounds();
        var buttonWidth = BuilderUi(220);
        var buttonHeight = (int)GetGarrisonBuilderMinimumButtonHeight() + BuilderUi(10);
        var x = (BuilderViewportWidth - buttonWidth) / 2;
        var y = strip.Y - buttonHeight - BuilderUi(10);
        return new Rectangle(x, y, buttonWidth, buttonHeight);
    }

    private void DrawGarrisonBuilderEntityMapPickPrompt(MouseState mouse)
    {
        if (!_builderEntityMapPickActive || !_builderUseModernUi)
        {
            return;
        }

        var bounds = GetGarrisonBuilderEntityMapPickPromptBounds();
        var highlighted = bounds.Contains(mouse.Position);
        DrawGarrisonBuilderBrownPanel(bounds);
        var label = _builderEntityMapPickTargetPropertyKey.Equals(
                TeleportMetadata.TeleportExitPropertyKey,
                StringComparison.OrdinalIgnoreCase)
            ? "Pick teleport exit"
            : _builderEntityMapPickTargetPropertyKey.Equals(
                AreaExtensionMetadata.ExtendsPropertyKey,
                StringComparison.OrdinalIgnoreCase)
                ? "Pick area to extend"
                : _builderEntityMapPickTargetPropertyKey.Equals(
                    DamageTriggerMetadata.DamageableEntityPropertyKey,
                    StringComparison.OrdinalIgnoreCase)
                    ? "Pick damageable area"
                    : "Pick an entity";
        DrawBuilderMenuButton(bounds, label, highlighted);
    }

    private bool TryPickGarrisonBuilderEntityMapPickAtWorld(Vector2 world, out CustomMapBuilderEntity picked)
    {
        if (_builderEntityMapPickTargetPropertyKey.Equals(
                TeleportMetadata.TeleportExitPropertyKey,
                StringComparison.OrdinalIgnoreCase))
        {
            return TryPickGarrisonBuilderTeleportExitAtWorld(world, out picked);
        }

        if (_builderEntityMapPickTargetPropertyKey.Equals(
                AreaExtensionMetadata.ExtendsPropertyKey,
                StringComparison.OrdinalIgnoreCase))
        {
            return TryPickGarrisonBuilderExtendableAreaAtWorld(world, out picked);
        }

        if (_builderEntityMapPickTargetPropertyKey.Equals(
                DamageTriggerMetadata.DamageableEntityPropertyKey,
                StringComparison.OrdinalIgnoreCase))
        {
            return TryPickGarrisonBuilderDamageableAtWorld(world, out picked);
        }

        return TryPickGarrisonBuilderActivatorEntityAtWorld(world, out picked);
    }

    private bool TryPickGarrisonBuilderDamageableAtWorld(Vector2 world, out CustomMapBuilderEntity picked)
    {
        picked = default!;
        CollectGarrisonBuilderEntitiesAtWorld(world, _builderEntityOverlapPickScratch);
        for (var pickIndex = 0; pickIndex < _builderEntityOverlapPickScratch.Count; pickIndex += 1)
        {
            var entityIndex = _builderEntityOverlapPickScratch[pickIndex];
            if (IsGarrisonBuilderEntityHidden(entityIndex))
            {
                continue;
            }

            var entity = _builderEntities[entityIndex];
            if (!entity.Type.Equals(DamageableMetadata.DamageableEntityType, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            picked = entity;
            return true;
        }

        return false;
    }

    private bool TryPickGarrisonBuilderExtendableAreaAtWorld(Vector2 world, out CustomMapBuilderEntity picked)
    {
        picked = default!;
        CollectGarrisonBuilderEntitiesAtWorld(world, _builderEntityOverlapPickScratch);
        for (var pickIndex = 0; pickIndex < _builderEntityOverlapPickScratch.Count; pickIndex += 1)
        {
            var entityIndex = _builderEntityOverlapPickScratch[pickIndex];
            if (IsGarrisonBuilderEntityHidden(entityIndex))
            {
                continue;
            }

            var entity = _builderEntities[entityIndex];
            if (!AreaExtensionMetadata.IsExtendableAreaEntityType(entity.Type))
            {
                continue;
            }

            picked = entity;
            return true;
        }

        return false;
    }

    private bool TryPickGarrisonBuilderActivatorEntityAtWorld(Vector2 world, out CustomMapBuilderEntity picked)
    {
        picked = default!;
        CollectGarrisonBuilderEntitiesAtWorld(world, _builderEntityOverlapPickScratch);
        if (_builderEntityOverlapPickScratch.Count == 0)
        {
            return false;
        }

        var editingActivatorIndex = _builderPropertyTarget == GarrisonBuilderPropertyTarget.SelectedMapEntity
            ? _builderSelectedEntityIndex
            : -1;

        for (var pickIndex = 0; pickIndex < _builderEntityOverlapPickScratch.Count; pickIndex += 1)
        {
            var entityIndex = _builderEntityOverlapPickScratch[pickIndex];
            if (IsGarrisonBuilderEntityHidden(entityIndex))
            {
                continue;
            }

            var entity = _builderEntities[entityIndex];
            if (entityIndex == editingActivatorIndex
                || MapLogicMetadata.IsLogicEntityType(entity.Type))
            {
                continue;
            }

            picked = entity;
            return true;
        }

        return false;
    }

    private void ApplyGarrisonBuilderEntityMapPick(CustomMapBuilderEntity target)
    {
        if (!_builderEntityMapPickActive)
        {
            return;
        }

        var propertyKey = _builderEntityMapPickTargetPropertyKey;
        if (string.IsNullOrWhiteSpace(propertyKey))
        {
            propertyKey = MapLogicMetadata.ActivatorEntityPropertyKey;
        }

        _builderPropertyEditorValues[propertyKey] = MapLogicEntityReference.FormatEntityRef(target);
        ApplyGarrisonBuilderPropertyEditorLivePreview();
        CancelGarrisonBuilderEntityMapPick();
        _builderStatus = $"target {target.Type}";
    }

    private bool TryPickGarrisonBuilderTeleportExitAtWorld(Vector2 world, out CustomMapBuilderEntity picked)
    {
        picked = default!;
        CollectGarrisonBuilderEntitiesAtWorld(world, _builderEntityOverlapPickScratch);
        for (var pickIndex = 0; pickIndex < _builderEntityOverlapPickScratch.Count; pickIndex += 1)
        {
            var entityIndex = _builderEntityOverlapPickScratch[pickIndex];
            if (IsGarrisonBuilderEntityHidden(entityIndex))
            {
                continue;
            }

            var entity = _builderEntities[entityIndex];
            if (!entity.Type.Equals(TeleportMetadata.TeleportExitEntityType, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            picked = entity;
            return true;
        }

        return false;
    }

    private bool TryPickGarrisonBuilderLogicSourceAtWorld(Vector2 world, out CustomMapBuilderEntity picked)
    {
        picked = default!;
        var bestIndex = -1;
        var bestArea = float.MaxValue;
        for (var index = 0; index < _builderEntities.Count; index += 1)
        {
            if (IsGarrisonBuilderEntityHidden(index))
            {
                continue;
            }

            var entity = _builderEntities[index];
            if (!IsGarrisonBuilderLogicOutputEntity(entity))
            {
                continue;
            }

            if (!TryGetGarrisonBuilderEntityPickBounds(entity, out var bounds)
                || !bounds.Contains(world.X, world.Y))
            {
                continue;
            }

            var area = bounds.Width * bounds.Height;
            if (area < bestArea)
            {
                bestArea = area;
                bestIndex = index;
            }
        }

        if (bestIndex < 0)
        {
            return false;
        }

        picked = _builderEntities[bestIndex];
        return true;
    }

    private void ApplyGarrisonBuilderLogicMapPick(CustomMapBuilderEntity source)
    {
        if (!_builderLogicMapPickActive || string.IsNullOrWhiteSpace(_builderLogicMapPickTargetPropertyKey))
        {
            return;
        }

        var logicKey = GetEntityProperty(source.Properties, MapLogicMetadata.LogicKeyPropertyKey, string.Empty);
        _builderPropertyEditorValues[_builderLogicMapPickTargetPropertyKey] = MapLogicMetadata.FormatLogicRef(logicKey);
        ApplyGarrisonBuilderPropertyEditorLivePreview();
        CancelGarrisonBuilderLogicMapPick();
        _builderStatus = $"linked {source.Type}";
    }

    private float GetGarrisonBuilderLogicNodeHalfHeight(CustomMapBuilderEntity entity)
    {
        if (entity.Type.Equals(MapLogicMetadata.ActivatorEntityType, StringComparison.OrdinalIgnoreCase)
            || entity.Type.Equals(MapLogicMetadata.TimerEntityType, StringComparison.OrdinalIgnoreCase)
            || entity.Type.Equals(MapLogicMetadata.OscillatorEntityType, StringComparison.OrdinalIgnoreCase))
        {
            return GarrisonBuilderLogicActivatorWorldHeight * 0.5f;
        }

        return GarrisonBuilderLogicNodeWorldHeight * 0.5f;
    }

    private Vector2 GetGarrisonBuilderLogicConsumerInputAnchor(CustomMapBuilderEntity entity)
    {
        return GetGarrisonBuilderEntityLinkAnchor(entity);
    }

    private static bool UsesGarrisonBuilderLogicConnectionMarker(CustomMapBuilderEntity entity)
    {
        return MapLogicMetadata.IsLogicEntityType(entity.Type)
            || AreaExtensionMetadata.IsAreaEntityType(entity.Type);
    }

    private float GetGarrisonBuilderLogicConnectionAnchorSpacingWorld()
    {
        return 8f;
    }

    private static Vector2 GetClosestPointOnRectanglePerimeter(
        float left,
        float top,
        float width,
        float height,
        Vector2 point)
    {
        var centerX = left + (width * 0.5f);
        var centerY = top + (height * 0.5f);
        var direction = new Vector2(point.X - centerX, point.Y - centerY);
        if (direction.LengthSquared() <= 0.0001f)
        {
            return new Vector2(centerX, top);
        }

        var halfWidth = width * 0.5f;
        var halfHeight = height * 0.5f;
        var scale = 1f / MathF.Max(
            MathF.Abs(direction.X) / MathF.Max(0.001f, halfWidth),
            MathF.Abs(direction.Y) / MathF.Max(0.001f, halfHeight));
        return new Vector2(centerX + (direction.X * scale), centerY + (direction.Y * scale));
    }

    private Vector2 ReserveGarrisonBuilderLogicConnectionAnchor(
        int entityIndex,
        bool isOutput,
        Vector2 idealWorld,
        Vector2 centerWorld)
    {
        var key = ((long)entityIndex << 1) | (isOutput ? 1L : 0L);
        if (!_builderLogicConnectionAnchorScratch.TryGetValue(key, out var placed))
        {
            placed = [];
            _builderLogicConnectionAnchorScratch[key] = placed;
        }

        var spacing = GetGarrisonBuilderLogicConnectionAnchorSpacingWorld();
        var edgeDirection = idealWorld - centerWorld;
        if (edgeDirection.LengthSquared() <= 0.0001f)
        {
            edgeDirection = new Vector2(0f, isOutput ? -1f : 1f);
        }
        else
        {
            edgeDirection = Vector2.Normalize(edgeDirection);
        }

        var tangent = new Vector2(-edgeDirection.Y, edgeDirection.X);
        for (var attempt = 0; attempt < 8; attempt += 1)
        {
            var candidate = idealWorld + (tangent * (spacing * attempt));
            var collides = false;
            for (var index = 0; index < placed.Count; index += 1)
            {
                var deltaX = candidate.X - placed[index].X;
                var deltaY = candidate.Y - placed[index].Y;
                if ((deltaX * deltaX) + (deltaY * deltaY) < spacing * spacing)
                {
                    collides = true;
                    break;
                }
            }

            if (!collides)
            {
                placed.Add(candidate);
                return candidate;
            }
        }

        placed.Add(idealWorld);
        return idealWorld;
    }

    private Vector2 ResolveGarrisonBuilderLogicConnectionAnchor(
        CustomMapBuilderEntity entity,
        int entityIndex,
        Vector2 towardWorld,
        bool isOutput)
    {
        if (!TryGetGarrisonBuilderLogicNodeWorldBounds(
                entity,
                out var left,
                out var top,
                out var width,
                out var height))
        {
            return GetGarrisonBuilderEntityLinkAnchor(entity);
        }

        var center = new Vector2(entity.X, entity.Y);
        var ideal = GetClosestPointOnRectanglePerimeter(left, top, width, height, towardWorld);
        return ReserveGarrisonBuilderLogicConnectionAnchor(entityIndex, isOutput, ideal, center);
    }

    private void DrawGarrisonBuilderLogicConnectionMarker(
        Vector2 anchorWorld,
        Vector2 centerWorld,
        bool isOutput,
        Color color)
    {
        var anchorScreen = BuilderWorldToScreen(anchorWorld);
        var centerScreen = BuilderWorldToScreen(centerWorld);
        var direction = anchorScreen - centerScreen;
        if (direction.LengthSquared() <= 0.25f)
        {
            direction = isOutput ? new Vector2(0f, -9f) : new Vector2(0f, 9f);
        }
        else
        {
            direction = Vector2.Normalize(direction);
        }

        var size = MathF.Max(4.5f, 7.5f * GetGarrisonBuilderLinkVisualScale());
        var tip = isOutput
            ? anchorScreen + (direction * size)
            : anchorScreen - (direction * size);
        var baseA = anchorScreen + new Vector2(-direction.Y * 0.55f, direction.X * 0.55f) * size;
        var baseB = anchorScreen - new Vector2(-direction.Y * 0.55f, direction.X * 0.55f) * size;
        DrawGarrisonBuilderFilledTriangle(tip, baseA, baseB, color);
        var outlineThickness = MathF.Max(1f, 1.25f * GetGarrisonBuilderLinkVisualScale());
        DrawGarrisonBuilderLine(tip, baseA, Color.Black, outlineThickness);
        DrawGarrisonBuilderLine(baseA, baseB, Color.Black, outlineThickness);
        DrawGarrisonBuilderLine(baseB, tip, Color.Black, outlineThickness);
    }

    private bool IsGarrisonBuilderLogicLinkHighlighted(CustomMapBuilderEntity source, CustomMapBuilderEntity target)
    {
        if (_builderSelectedEntityIndex < 0 || _builderSelectedEntityIndex >= _builderEntities.Count)
        {
            return false;
        }

        var selected = _builderEntities[_builderSelectedEntityIndex];
        return ReferenceEquals(selected, source) || ReferenceEquals(selected, target);
    }

    private void DrawGarrisonBuilderLogicLink(
        CustomMapBuilderEntity source,
        int sourceIndex,
        CustomMapBuilderEntity target,
        int targetIndex,
        bool highlighted)
    {
        var color = highlighted ? GarrisonBuilderLogicLinkHighlightColor : GarrisonBuilderLogicLinkColor;
        var towardTarget = new Vector2(target.X, target.Y);
        var towardSource = new Vector2(source.X, source.Y);
        var sourceAnchor = ResolveGarrisonBuilderLogicConnectionAnchor(source, sourceIndex, towardTarget, isOutput: true);
        var targetAnchor = ResolveGarrisonBuilderLogicConnectionAnchor(target, targetIndex, towardSource, isOutput: false);
        var start = BuilderWorldToScreen(sourceAnchor);
        var end = BuilderWorldToScreen(targetAnchor);
        var linkScale = GetGarrisonBuilderLinkVisualScale();
        var thickness = MathF.Max(1f, GarrisonBuilderLinkLineThickness * linkScale * 0.65f);
        DrawGarrisonBuilderLine(start, end, color, thickness);

        if (UsesGarrisonBuilderLogicConnectionMarker(source))
        {
            DrawGarrisonBuilderLogicConnectionMarker(
                sourceAnchor,
                new Vector2(source.X, source.Y),
                isOutput: true,
                color);
        }
        else
        {
            DrawGarrisonBuilderLinkEndpoint(start, color, linkScale);
        }

        if (UsesGarrisonBuilderLogicConnectionMarker(target))
        {
            DrawGarrisonBuilderLogicConnectionMarker(
                targetAnchor,
                new Vector2(target.X, target.Y),
                isOutput: false,
                color);
        }
        else
        {
            DrawGarrisonBuilderLinkEndpoint(end, color, linkScale);
        }
    }

    private void DrawGarrisonBuilderLogicLink(
        CustomMapBuilderEntity source,
        CustomMapBuilderEntity target)
    {
        var sourceIndex = _builderEntities.IndexOf(source);
        var targetIndex = _builderEntities.IndexOf(target);
        DrawGarrisonBuilderLogicLink(
            source,
            sourceIndex >= 0 ? sourceIndex : 0,
            target,
            targetIndex >= 0 ? targetIndex : 0,
            IsGarrisonBuilderLogicLinkHighlighted(source, target));
    }

    private static bool IsGarrisonBuilderActivatorEntityMapPickProperty(string key)
    {
        return key.Equals(MapLogicMetadata.ActivatorEntityPropertyKey, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGarrisonBuilderTeleportExitMapPickProperty(string key)
    {
        return key.Equals(TeleportMetadata.TeleportExitPropertyKey, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGarrisonBuilderAreaExtendsMapPickProperty(string key)
    {
        return key.Equals(AreaExtensionMetadata.ExtendsPropertyKey, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGarrisonBuilderDamageableEntityMapPickProperty(string key)
    {
        return key.Equals(DamageTriggerMetadata.DamageableEntityPropertyKey, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGarrisonBuilderEntityMapPickProperty(string key)
    {
        return IsGarrisonBuilderActivatorEntityMapPickProperty(key)
            || IsGarrisonBuilderTeleportExitMapPickProperty(key)
            || IsGarrisonBuilderAreaExtendsMapPickProperty(key)
            || IsGarrisonBuilderDamageableEntityMapPickProperty(key);
    }

    private static List<string> OrderGarrisonBuilderTeleportPropertyRows(List<string> rows)
    {
        string[] order =
        [
            TeleportMetadata.TeamPropertyKey,
            TeleportMetadata.TeleportExitPropertyKey,
        ];

        var ordered = new List<string>(rows.Count);
        foreach (var key in order)
        {
            var match = rows.FirstOrDefault(existing => existing.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                ordered.Add(match);
            }
        }

        foreach (var key in rows)
        {
            if (!ordered.Any(existing => existing.Equals(key, StringComparison.OrdinalIgnoreCase)))
            {
                ordered.Add(key);
            }
        }

        return ordered;
    }

    private static bool IsGarrisonBuilderLogicMapPickProperty(string key)
    {
        return key.Equals(MapLogicMetadata.LogicInputPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(MapLogicMetadata.LogicInput1PropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(MapLogicMetadata.LogicInput2PropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(MapLogicMetadata.LogicResetPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(MapLogicMetadata.StartWhenPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(MapLogicMetadata.EndWhenPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(MapLogicMetadata.LogicSignalPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(MapLogicMetadata.LockedWhenLogicPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(MapLogicMetadata.UnlockedWhenLogicPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(DamageableMetadata.HealWhenPropertyKey, StringComparison.OrdinalIgnoreCase);
    }

    private string GetGarrisonBuilderPropertyEditorValue(string key)
    {
        return _builderPropertyEditorValues.TryGetValue(key, out var value) ? value : string.Empty;
    }

    private bool IsGarrisonBuilderSpawnUsingLogicSignal()
    {
        return IsEditingGarrisonBuilderSpawnEntity()
            && IsGarrisonBuilderEditedSpawnForwardEnabled()
            && !string.IsNullOrWhiteSpace(GetGarrisonBuilderPropertyEditorValue(MapLogicMetadata.LogicSignalPropertyKey));
    }

    private static List<string> OrderGarrisonBuilderLogicPropertyRows(string entityType, List<string> rows)
    {
        string[] order = entityType.Equals(AreaExtensionMetadata.AreaEntityType, StringComparison.OrdinalIgnoreCase)
            ?
            [
                AreaExtensionMetadata.ExtendsPropertyKey,
            ]
            : entityType.Equals(MapLogicMetadata.PlayerTriggerEntityType, StringComparison.OrdinalIgnoreCase)
            ?
            [
                MapLogicSignalMetadata.SignalPropertyKey,
                MapLogicSignalMetadata.DetectPropertyKey,
                PlayerTriggerMetadata.TeamPropertyKey,
                PlayerTriggerMetadata.IntelCarriersOnlyPropertyKey,
                MapLogicMetadata.NodePriorityPropertyKey,
            ]
            : entityType.Equals(MapLogicMetadata.ScoreTriggerEntityType, StringComparison.OrdinalIgnoreCase)
            ?
            [
                MapLogicMetadata.LogicInputPropertyKey,
                MapLogicScoreTriggerMetadata.ScoreTeamPropertyKey,
                MapLogicScoreTriggerMetadata.ChangePropertyKey,
                MapLogicScoreTriggerMetadata.ValuePropertyKey,
                MapLogicMetadata.NodePriorityPropertyKey,
            ]
            : entityType.Equals(MapLogicMetadata.IntelTriggerEntityType, StringComparison.OrdinalIgnoreCase)
            ?
            [
                MapLogicSignalMetadata.SignalPropertyKey,
                IntelTriggerMetadata.IntelPropertyKey,
                IntelTriggerMetadata.TriggerWhenPropertyKey,
                IntelTriggerMetadata.OnPickupPropertyKey,
                IntelTriggerMetadata.OnDropPropertyKey,
                IntelTriggerMetadata.OnCapturePropertyKey,
                IntelTriggerMetadata.OnResetPropertyKey,
                MapLogicMetadata.NodePriorityPropertyKey,
            ]
            : entityType.Equals(DamageableMetadata.DamageableEntityType, StringComparison.OrdinalIgnoreCase)
            ?
            [
                DamageableMetadata.HealthPropertyKey,
                DamageableMetadata.HealWhenPropertyKey,
                DamageableMetadata.ShowHealthBarPropertyKey,
                DamageableMetadata.BlockPlayersPropertyKey,
                DamageableMetadata.DisableWhenDestroyedPropertyKey,
                DamageableMetadata.SentryTargetPropertyKey,
                DamageableMetadata.StabbablePropertyKey,
            ]
            : entityType.Equals(DamageTriggerMetadata.DamageTriggerEntityType, StringComparison.OrdinalIgnoreCase)
            ?
            [
                MapLogicSignalMetadata.SignalPropertyKey,
                DamageTriggerMetadata.DamageableEntityPropertyKey,
                DamageTriggerMetadata.TriggerBelowThresholdPropertyKey,
                DamageTriggerMetadata.TriggerBelowPercentPropertyKey,
                DamageTriggerMetadata.TriggerOnAnyDamagePropertyKey,
                DamageTriggerMetadata.AnyDamageCooldownPropertyKey,
                MapLogicMetadata.TrueTimePropertyKey,
                DamageTriggerMetadata.TriggerOnHealPropertyKey,
                DamageTriggerMetadata.TriggerWhenDestroyedPropertyKey,
                DamageTriggerMetadata.AffectedByTeamPropertyKey,
                MapLogicMetadata.NodePriorityPropertyKey,
            ]
            : entityType.Equals(MapLogicMetadata.RisingEdgeEntityType, StringComparison.OrdinalIgnoreCase)
            ?
            [
                MapLogicMetadata.LogicInputPropertyKey,
                MapLogicMetadata.NodePriorityPropertyKey,
            ]
            : entityType.Equals(MapLogicMetadata.LatchEntityType, StringComparison.OrdinalIgnoreCase)
            ?
            [
                MapLogicMetadata.LogicInputPropertyKey,
                MapLogicMetadata.LogicResetPropertyKey,
                MapLogicMetadata.NodePriorityPropertyKey,
            ]
            : entityType.Equals(MapLogicMetadata.CpTriggerEntityType, StringComparison.OrdinalIgnoreCase)
                ?
                [
                    MapLogicSignalMetadata.SignalPropertyKey,
                    MapLogicMetadata.LinkedControlPointPropertyKey,
                    MapLogicSignalMetadata.DetectPropertyKey,
                    MapLogicMetadata.RequiredOwnerPropertyKey,
                    MapLogicMetadata.NodePriorityPropertyKey,
                ]
                : entityType.Equals(MapLogicMetadata.GateEntityType, StringComparison.OrdinalIgnoreCase)
                ?
                [
                    MapLogicMetadata.GateTypePropertyKey,
                    MapLogicMetadata.LogicInput1PropertyKey,
                    MapLogicMetadata.LogicInput2PropertyKey,
                    MapLogicMetadata.NodePriorityPropertyKey,
                ]
                : entityType.Equals(MapLogicMetadata.ActivatorEntityType, StringComparison.OrdinalIgnoreCase)
                    ?
                    [
                        MapLogicMetadata.ActivatorBehaviorPropertyKey,
                        MapLogicMetadata.ActivatorEntityPropertyKey,
                        MapLogicMetadata.LogicInputPropertyKey,
                        MapLogicMetadata.ActivateOnStartPropertyKey,
                        MapLogicMetadata.NodePriorityPropertyKey,
                    ]
                    : entityType.Equals(MapLogicMetadata.TimerEntityType, StringComparison.OrdinalIgnoreCase)
                        ?
                        [
                            MapLogicSignalMetadata.SignalPropertyKey,
                            MapLogicMetadata.CountdownSecondsPropertyKey,
                            MapLogicMetadata.LogicInputPropertyKey,
                            MapLogicMetadata.TriggerOnStartPropertyKey,
                            MapLogicMetadata.DelayedTruePropertyKey,
                            MapLogicMetadata.DelayedFalsePropertyKey,
                            MapLogicMetadata.NodePriorityPropertyKey,
                        ]
                        : entityType.Equals(MapLogicMetadata.OscillatorEntityType, StringComparison.OrdinalIgnoreCase)
                            ?
                            [
                                MapLogicSignalMetadata.SignalPropertyKey,
                                MapLogicSignalMetadata.PeriodPropertyKey,
                                MapLogicMetadata.TrueTimePropertyKey,
                                MapLogicMetadata.FalseTimePropertyKey,
                                MapLogicMetadata.InitialValuePropertyKey,
                                MapLogicMetadata.AutostartPropertyKey,
                                MapLogicMetadata.StartWhenPropertyKey,
                                MapLogicMetadata.EndWhenPropertyKey,
                                MapLogicMetadata.NodePriorityPropertyKey,
                            ]
                            :
                        [
                            MapLogicMetadata.LogicInputPropertyKey,
                            MapLogicMetadata.NodePriorityPropertyKey,
                        ];

        var ordered = new List<string>(rows.Count);
        foreach (var key in order)
        {
            var match = rows.FirstOrDefault(existing => existing.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                ordered.Add(match);
            }
        }

        foreach (var key in rows)
        {
            if (!ordered.Any(existing => existing.Equals(key, StringComparison.OrdinalIgnoreCase)))
            {
                ordered.Add(key);
            }
        }

        return ordered;
    }

    private static List<string> OrderGarrisonBuilderSpawnPropertyRowsWithLogic(List<string> rows, bool useLogicSignal)
    {
        string[] order = useLogicSignal
            ?
            [
                "team",
                "forward",
                ForwardSpawnPriorityMetadata.PropertyKey,
                MapLogicMetadata.LogicSignalPropertyKey,
            ]
            : GarrisonBuilderSpawnPropertyRowOrder;

        var ordered = new List<string>(rows.Count);
        foreach (var key in order)
        {
            var match = rows.FirstOrDefault(existing => existing.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                ordered.Add(match);
            }
        }

        foreach (var key in rows)
        {
            if (!ordered.Any(existing => existing.Equals(key, StringComparison.OrdinalIgnoreCase)))
            {
                ordered.Add(key);
            }
        }

        return ordered;
    }

    private List<string> BuildGarrisonBuilderControlPointPropertyRowsWithLogic(List<string> rows)
    {
        var ordered = new List<string>
        {
            ControlPointIndexMetadata.PropertyKey,
            ControlPointCapTimeMultiplierMetadata.PropertyKey,
        };
        if (!IsGarrisonBuilderControlPointOverrideEnabled())
        {
            return ordered;
        }

        if (rows.Any(static key => key.Equals(ControlPointInitialOwnershipMetadata.PropertyKey, StringComparison.OrdinalIgnoreCase)))
        {
            ordered.Add(ControlPointInitialOwnershipMetadata.PropertyKey);
        }

        ordered.Add(ControlPointLockDependencyMetadata.LockedWhenSectionKey);
        ordered.Add(MapLogicMetadata.LockedWhenLogicPropertyKey);
        ordered.Add(ControlPointLockDependencyMetadata.UnlockedWhenSectionKey);
        ordered.Add(MapLogicMetadata.UnlockedWhenLogicPropertyKey);
        ordered.Add(ControlPointInitialLockStateMetadata.PropertyKey);
        return ordered;
    }

    private string GetGarrisonBuilderLogicPropertyDisplayValue(string key, string value)
    {
        if (key.Equals(MapLogicMetadata.GateTypePropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return MapLogicMetadata.GetGateTypeDisplayLabel(value);
        }

        if (key.Equals(MapLogicMetadata.RequiredOwnerPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return MapLogicMetadata.GetRequiredOwnerDisplayLabel(value);
        }

        if (key.Equals(PlayerTriggerMetadata.TeamPropertyKey, StringComparison.OrdinalIgnoreCase)
            && TryGetGarrisonBuilderEditedEntityType(out var playerTriggerEntity)
            && playerTriggerEntity.Equals(MapLogicMetadata.PlayerTriggerEntityType, StringComparison.OrdinalIgnoreCase))
        {
            return $"Team: {PlayerTriggerMetadata.GetTeamDisplayLabel(value)}";
        }

        if (key.Equals(PlayerTriggerMetadata.IntelCarriersOnlyPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return DamageableMetadata.GetBooleanDisplayLabel(value);
        }

        if (key.Equals(IntelTriggerMetadata.IntelPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return $"Intel: {IntelTriggerMetadata.GetIntelDisplayLabel(value)}";
        }

        if (key.Equals(IntelTriggerMetadata.TriggerWhenPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return $"Trigger when: {IntelTriggerMetadata.GetLatchStateDisplayLabel(value)}";
        }

        if (key.Equals(IntelTriggerMetadata.OnPickupPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(IntelTriggerMetadata.OnDropPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(IntelTriggerMetadata.OnCapturePropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(IntelTriggerMetadata.OnResetPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return DamageableMetadata.GetBooleanDisplayLabel(value);
        }

        if (key.Equals(MapLogicMetadata.ActivatorEntityPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return GetGarrisonBuilderActivatorEntityDisplayValue(value);
        }

        if (key.Equals(AreaExtensionMetadata.ExtendsPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return GetGarrisonBuilderAreaExtendsDisplayValue(value);
        }

        if (key.Equals(DamageTriggerMetadata.DamageableEntityPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return GetGarrisonBuilderEntityReferenceDisplayValue(value);
        }

        if (key.Equals(DamageableMetadata.ShowHealthBarPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(DamageableMetadata.BlockPlayersPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(DamageableMetadata.DisableWhenDestroyedPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(DamageableMetadata.SentryTargetPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(DamageableMetadata.StabbablePropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return DamageableMetadata.GetBooleanDisplayLabel(value);
        }

        if (DamageTriggerMetadata.IsExclusiveModePropertyKey(key))
        {
            return DamageTriggerMetadata.GetBooleanDisplayLabel(value);
        }

        if (key.Equals(DamageTriggerMetadata.TriggerBelowPercentPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return DamageTriggerMetadata.GetTriggerBelowDisplayLabel(value);
        }

        if (key.Equals(DamageTriggerMetadata.AnyDamageCooldownPropertyKey, StringComparison.OrdinalIgnoreCase)
            && TryGetGarrisonBuilderEditedEntityType(out var damageTriggerCooldownEntity)
            && damageTriggerCooldownEntity.Equals(DamageTriggerMetadata.DamageTriggerEntityType, StringComparison.OrdinalIgnoreCase))
        {
            return DamageTriggerMetadata.ToAnyDamageCooldownPropertyValue(
                DamageTriggerMetadata.ParseAnyDamageCooldownSeconds(_builderPropertyEditorValues));
        }

        if (key.Equals(DamageTriggerMetadata.AffectedByTeamPropertyKey, StringComparison.OrdinalIgnoreCase)
            && TryGetGarrisonBuilderEditedEntityType(out var damageTriggerTeamEntity)
            && damageTriggerTeamEntity.Equals(DamageTriggerMetadata.DamageTriggerEntityType, StringComparison.OrdinalIgnoreCase))
        {
            return $"Affected by team: {DamageTriggerMetadata.GetAffectedByTeamDisplayLabel(value)}";
        }

        if (key.Equals(MapLogicMetadata.TrueTimePropertyKey, StringComparison.OrdinalIgnoreCase)
            && TryGetGarrisonBuilderEditedEntityType(out var damageTriggerTrueTimeEntity)
            && damageTriggerTrueTimeEntity.Equals(DamageTriggerMetadata.DamageTriggerEntityType, StringComparison.OrdinalIgnoreCase))
        {
            return $"TRUE time (sec): {DamageTriggerMetadata.ToAnyDamageTrueTimePropertyValue(DamageTriggerMetadata.ParseAnyDamageTrueTimeSeconds(_builderPropertyEditorValues))}";
        }

        if (IsGarrisonBuilderLogicMapPickProperty(key))
        {
            if (!MapLogicMetadata.TryParseLogicRef(value, out var logicKey)
                || !TryFindGarrisonBuilderLogicSource(MapLogicMetadata.FormatLogicRef(logicKey), out var source, out _))
            {
                return "none";
            }

            return $"{source.Type} ({logicKey})";
        }

        return value;
    }

    private string GetGarrisonBuilderActivatorEntityDisplayValue(string value)
    {
        return GetGarrisonBuilderEntityReferenceDisplayValue(value);
    }

    private string GetGarrisonBuilderAreaExtendsDisplayValue(string value)
    {
        var summary = GetGarrisonBuilderEntityReferenceDisplayValue(value);
        return summary.StartsWith("none", StringComparison.OrdinalIgnoreCase)
            ? "Extends: none"
            : $"Extends: {summary}";
    }

    private string GetGarrisonBuilderEntityReferenceDisplayValue(string value)
    {
        if (!MapLogicEntityReference.TryFindBuilderEntityIndex(_builderEntities, value, out var entityIndex))
        {
            return "none";
        }

        var entity = _builderEntities[entityIndex];
        if (CustomMapBuilderEntityCatalog.TryGetDefinition(entity.Type, out var definition))
        {
            return definition.Label;
        }

        return entity.Type;
    }

    private bool TryDrawGarrisonBuilderLogicEntity(CustomMapBuilderEntityDefinition definition, CustomMapBuilderEntity entity)
    {
        if (!MapLogicMetadata.IsLogicEntityType(definition.Type))
        {
            return false;
        }

        if (definition.Type.Equals(MapLogicMetadata.PlayerTriggerEntityType, StringComparison.OrdinalIgnoreCase))
        {
            DrawGarrisonBuilderPlayerTriggerZone(entity);
            return true;
        }

        if (definition.Type.Equals(AreaExtensionMetadata.AreaEntityType, StringComparison.OrdinalIgnoreCase))
        {
            DrawGarrisonBuilderAreaZone(entity);
            return true;
        }

        if (definition.Type.Equals(DamageableMetadata.DamageableEntityType, StringComparison.OrdinalIgnoreCase))
        {
            DrawGarrisonBuilderDamageableZone(entity);
            return true;
        }

        if (definition.Type.Equals(DamageTriggerMetadata.DamageTriggerEntityType, StringComparison.OrdinalIgnoreCase))
        {
            DrawGarrisonBuilderLogicNode(entity, "DMG", GetGarrisonBuilderLogicScreenOutlineThicknessPx());
            return true;
        }

        if (definition.Type.Equals(MapLogicMetadata.CpTriggerEntityType, StringComparison.OrdinalIgnoreCase))
        {
            DrawGarrisonBuilderLogicNode(entity, "CP", GetGarrisonBuilderLogicScreenOutlineThicknessPx());
            return true;
        }

        if (definition.Type.Equals(MapLogicMetadata.IntelTriggerEntityType, StringComparison.OrdinalIgnoreCase))
        {
            DrawGarrisonBuilderLogicNode(entity, "INTL", GetGarrisonBuilderLogicScreenOutlineThicknessPx());
            return true;
        }

        if (definition.Type.Equals(MapLogicMetadata.ScoreTriggerEntityType, StringComparison.OrdinalIgnoreCase))
        {
            DrawGarrisonBuilderLogicCircleLabel(entity, "SCR");
            return true;
        }

        if (definition.Type.Equals(MapLogicMetadata.NotEntityType, StringComparison.OrdinalIgnoreCase))
        {
            DrawGarrisonBuilderLogicNode(entity, "NOT", GetGarrisonBuilderLogicScreenOutlineThicknessPx());
            return true;
        }

        if (definition.Type.Equals(MapLogicMetadata.RisingEdgeEntityType, StringComparison.OrdinalIgnoreCase))
        {
            DrawGarrisonBuilderLogicNode(entity, "EDG", GetGarrisonBuilderLogicScreenOutlineThicknessPx());
            return true;
        }

        if (definition.Type.Equals(MapLogicMetadata.LatchEntityType, StringComparison.OrdinalIgnoreCase))
        {
            DrawGarrisonBuilderLogicNode(entity, "LCH", GetGarrisonBuilderLogicScreenOutlineThicknessPx());
            return true;
        }

        if (definition.Type.Equals(MapLogicMetadata.ActivatorEntityType, StringComparison.OrdinalIgnoreCase))
        {
            DrawGarrisonBuilderLogicActivator(entity);
            return true;
        }

        if (definition.Type.Equals(MapLogicMetadata.TimerEntityType, StringComparison.OrdinalIgnoreCase))
        {
            DrawGarrisonBuilderLogicTimer(entity);
            return true;
        }

        if (definition.Type.Equals(MapLogicMetadata.OscillatorEntityType, StringComparison.OrdinalIgnoreCase))
        {
            DrawGarrisonBuilderLogicOscillator(entity);
            return true;
        }

        MapLogicMetadata.TryParseGateType(
            GetEntityProperty(entity.Properties, MapLogicMetadata.GateTypePropertyKey, "and"),
            out var gateType);
        DrawGarrisonBuilderLogicNode(entity, gateType.ToString().ToUpperInvariant(), GetGarrisonBuilderLogicScreenOutlineThicknessPx());
        return true;
    }

    private void DrawGarrisonBuilderAreaZone(CustomMapBuilderEntity entity)
    {
        if (!TryGetGarrisonBuilderLogicNodeWorldBounds(entity, out var left, out var top, out var width, out var height))
        {
            return;
        }

        var topLeft = BuilderWorldToScreen(new Vector2(left, top));
        var bottomRight = BuilderWorldToScreen(new Vector2(left + width, top + height));
        var screenBounds = new Rectangle(
            (int)MathF.Floor(MathF.Min(topLeft.X, bottomRight.X)),
            (int)MathF.Floor(MathF.Min(topLeft.Y, bottomRight.Y)),
            Math.Max(1, (int)MathF.Ceiling(MathF.Abs(bottomRight.X - topLeft.X))),
            Math.Max(1, (int)MathF.Ceiling(MathF.Abs(bottomRight.Y - topLeft.Y))));
        var fillColor = new Color(96, 168, 220, 150);
        var borderColor = new Color(40, 96, 140, 255);
        _spriteBatch.Draw(_pixel, screenBounds, fillColor);
        DrawGarrisonBuilderRectangleOutline(screenBounds, borderColor);
    }

    private void DrawGarrisonBuilderDamageableZone(CustomMapBuilderEntity entity)
    {
        if (!TryGetGarrisonBuilderLogicNodeWorldBounds(entity, out var left, out var top, out var width, out var height))
        {
            return;
        }

        var topLeft = BuilderWorldToScreen(new Vector2(left, top));
        var bottomRight = BuilderWorldToScreen(new Vector2(left + width, top + height));
        var screenBounds = new Rectangle(
            (int)MathF.Floor(MathF.Min(topLeft.X, bottomRight.X)),
            (int)MathF.Floor(MathF.Min(topLeft.Y, bottomRight.Y)),
            Math.Max(1, (int)MathF.Ceiling(MathF.Abs(bottomRight.X - topLeft.X))),
            Math.Max(1, (int)MathF.Ceiling(MathF.Abs(bottomRight.Y - topLeft.Y))));
        var fillColor = new Color(220, 48, 48, 150);
        var borderColor = new Color(140, 24, 24, 255);
        _spriteBatch.Draw(_pixel, screenBounds, fillColor);
        DrawGarrisonBuilderRectangleOutline(screenBounds, borderColor);

        var labelScale = GetGarrisonBuilderLogicLabelScale();
        var label = "DMG";
        var labelWidth = _builderUseModernUi
            ? MeasureBitmapFontWidth(label, labelScale)
            : MeasureGarrisonBuilderText(label, labelScale).X;
        var labelHeight = _builderUseModernUi ? MeasureBitmapFontHeight(labelScale) : 10f * labelScale;
        var labelX = screenBounds.X + ((screenBounds.Width - labelWidth) * 0.5f);
        var labelY = screenBounds.Y + ((screenBounds.Height - labelHeight) * 0.5f);
        if (_builderUseModernUi)
        {
            DrawBitmapFontText(label, new Vector2(labelX, labelY), Color.Black, labelScale);
        }
        else
        {
            DrawGarrisonBuilderText(label, new Vector2(labelX, labelY), Color.Black, labelScale);
        }
    }

    private void DrawGarrisonBuilderPlayerTriggerZone(CustomMapBuilderEntity entity)
    {
        if (!TryGetGarrisonBuilderLogicNodeWorldBounds(entity, out var left, out var top, out var width, out var height))
        {
            return;
        }

        var topLeft = BuilderWorldToScreen(new Vector2(left, top));
        var bottomRight = BuilderWorldToScreen(new Vector2(left + width, top + height));
        var screenBounds = new Rectangle(
            (int)MathF.Floor(MathF.Min(topLeft.X, bottomRight.X)),
            (int)MathF.Floor(MathF.Min(topLeft.Y, bottomRight.Y)),
            Math.Max(1, (int)MathF.Ceiling(MathF.Abs(bottomRight.X - topLeft.X))),
            Math.Max(1, (int)MathF.Ceiling(MathF.Abs(bottomRight.Y - topLeft.Y))));
        var baseFillColor = GetGarrisonBuilderLogicFillColor(entity);
        var fillColor = Color.Lerp(
            new Color(baseFillColor.R, baseFillColor.G, baseFillColor.B, (byte)170),
            Color.White,
            0.2f);
        var borderColor = GarrisonBuilderLogicOutlineColor;
        _spriteBatch.Draw(_pixel, screenBounds, fillColor);
        DrawGarrisonBuilderRectangleOutline(screenBounds, borderColor);

        var labelScale = GetGarrisonBuilderLogicLabelScale();
        var label = "PLY";
        var labelWidth = _builderUseModernUi
            ? MeasureBitmapFontWidth(label, labelScale)
            : MeasureGarrisonBuilderText(label, labelScale).X;
        var labelHeight = _builderUseModernUi ? MeasureBitmapFontHeight(labelScale) : 10f * labelScale;
        var labelX = screenBounds.X + ((screenBounds.Width - labelWidth) * 0.5f);
        var labelY = screenBounds.Y + ((screenBounds.Height - labelHeight) * 0.5f);
        if (_builderUseModernUi)
        {
            DrawBitmapFontText(label, new Vector2(labelX, labelY), Color.Black, labelScale);
        }
        else
        {
            DrawGarrisonBuilderText(label, new Vector2(labelX, labelY), Color.Black, labelScale);
        }
    }

    private void DrawGarrisonBuilderLogicNode(CustomMapBuilderEntity entity, string label, int outlineThickness)
    {
        if (!TryGetGarrisonBuilderLogicNodeWorldBounds(entity, out var left, out var top, out var width, out var height))
        {
            return;
        }

        var screenBounds = ToScreenRectangle(new RectangleF(left, top, width, height));
        _spriteBatch.Draw(_pixel, screenBounds, GetGarrisonBuilderLogicFillColor(entity));
        DrawGarrisonBuilderThickRectangleOutline(screenBounds, GarrisonBuilderLogicOutlineColor, outlineThickness);

        DrawGarrisonBuilderLogicNodeLabel(screenBounds, label);
    }

    private void DrawGarrisonBuilderLogicTimer(CustomMapBuilderEntity entity)
    {
        DrawGarrisonBuilderLogicCircleLabel(entity, "TMR");
    }

    private void DrawGarrisonBuilderLogicOscillator(CustomMapBuilderEntity entity)
    {
        if (!TryGetGarrisonBuilderLogicNodeWorldBounds(entity, out var left, out var top, out var width, out var height))
        {
            return;
        }

        var screenBounds = ToScreenRectangle(new RectangleF(left, top, width, height));
        var cornerRadius = MathF.Min(screenBounds.Width, screenBounds.Height) * 0.28f;
        DrawGarrisonBuilderLogicRoundedRectangle(
            screenBounds,
            GetGarrisonBuilderLogicFillColor(entity),
            GarrisonBuilderLogicOutlineColor,
            GetGarrisonBuilderLogicScreenOutlineThicknessPx(),
            cornerRadius);

        DrawGarrisonBuilderLogicNodeLabel(screenBounds, "OSC");
    }

    private float GetGarrisonBuilderLogicLabelScale()
    {
        var baseScale = GetGarrisonBuilderRelativeBitmapFontScale(GarrisonBuilderLogicLabelRelativeScale);
        return _builderUseModernUi
            ? baseScale * _builderZoom
            : baseScale;
    }

    private float GetGarrisonBuilderLogicScreenOutlineThickness()
    {
        return _builderUseModernUi
            ? MathF.Max(1f, 2f * _builderZoom)
            : 2f;
    }

    private int GetGarrisonBuilderLogicScreenOutlineThicknessPx()
    {
        return Math.Max(1, (int)MathF.Round(GetGarrisonBuilderLogicScreenOutlineThickness()));
    }

    private void DrawGarrisonBuilderLogicNodeLabel(Rectangle screenBounds, string label)
    {
        var labelScale = GetGarrisonBuilderLogicLabelScale();
        var labelWidth = MeasureBitmapFontWidth(label, labelScale);
        var labelHeight = MeasureBitmapFontHeight(labelScale);
        DrawBitmapFontText(
            label,
            new Vector2(
                screenBounds.X + ((screenBounds.Width - labelWidth) * 0.5f),
                screenBounds.Y + ((screenBounds.Height - labelHeight) * 0.5f)),
            Color.Black,
            labelScale);
    }

    private void DrawGarrisonBuilderLogicRoundedRectangle(
        Rectangle bounds,
        Color fillColor,
        Color outlineColor,
        int outlineThickness,
        float cornerRadius)
    {
        cornerRadius = MathF.Min(cornerRadius, MathF.Min(bounds.Width, bounds.Height) * 0.5f);
        var core = new Rectangle(
            bounds.X + (int)cornerRadius,
            bounds.Y,
            Math.Max(1, bounds.Width - ((int)cornerRadius * 2)),
            bounds.Height);
        var vertical = new Rectangle(
            bounds.X,
            bounds.Y + (int)cornerRadius,
            bounds.Width,
            Math.Max(1, bounds.Height - ((int)cornerRadius * 2)));
        _spriteBatch.Draw(_pixel, core, fillColor);
        _spriteBatch.Draw(_pixel, vertical, fillColor);

        const int cornerSegments = 8;
        var corners = new[]
        {
            new Vector2(bounds.Left + cornerRadius, bounds.Top + cornerRadius),
            new Vector2(bounds.Right - cornerRadius, bounds.Top + cornerRadius),
            new Vector2(bounds.Right - cornerRadius, bounds.Bottom - cornerRadius),
            new Vector2(bounds.Left + cornerRadius, bounds.Bottom - cornerRadius),
        };
        var startAngles = new[] { MathF.PI, MathF.PI * 1.5f, 0f, MathF.PI * 0.5f };

        for (var corner = 0; corner < corners.Length; corner += 1)
        {
            var center = corners[corner];
            var startAngle = startAngles[corner];
            for (var segment = 0; segment < cornerSegments; segment += 1)
            {
                var angleA = startAngle + (segment / (float)cornerSegments) * (MathF.PI * 0.5f);
                var angleB = startAngle + ((segment + 1) / (float)cornerSegments) * (MathF.PI * 0.5f);
                var pointA = new Vector2(
                    center.X + (MathF.Cos(angleA) * cornerRadius),
                    center.Y + (MathF.Sin(angleA) * cornerRadius));
                var pointB = new Vector2(
                    center.X + (MathF.Cos(angleB) * cornerRadius),
                    center.Y + (MathF.Sin(angleB) * cornerRadius));
                DrawGarrisonBuilderFilledTriangle(center, pointA, pointB, fillColor);
            }
        }

        DrawGarrisonBuilderRoundedRectangleOutline(bounds, outlineColor, outlineThickness, cornerRadius);
    }

    private void DrawGarrisonBuilderRoundedRectangleOutline(
        Rectangle bounds,
        Color color,
        int thickness,
        float cornerRadius)
    {
        var horizontal = new Rectangle(
            bounds.X + (int)cornerRadius,
            bounds.Y,
            Math.Max(1, bounds.Width - ((int)cornerRadius * 2)),
            thickness);
        var bottom = new Rectangle(
            bounds.X + (int)cornerRadius,
            bounds.Bottom - thickness,
            horizontal.Width,
            thickness);
        var vertical = new Rectangle(
            bounds.X,
            bounds.Y + (int)cornerRadius,
            thickness,
            Math.Max(1, bounds.Height - ((int)cornerRadius * 2)));
        var right = new Rectangle(
            bounds.Right - thickness,
            vertical.Y,
            thickness,
            vertical.Height);
        _spriteBatch.Draw(_pixel, horizontal, color);
        _spriteBatch.Draw(_pixel, bottom, color);
        _spriteBatch.Draw(_pixel, vertical, color);
        _spriteBatch.Draw(_pixel, right, color);

        const int cornerSegments = 10;
        var corners = new[]
        {
            new Vector2(bounds.Left + cornerRadius, bounds.Top + cornerRadius),
            new Vector2(bounds.Right - cornerRadius, bounds.Top + cornerRadius),
            new Vector2(bounds.Right - cornerRadius, bounds.Bottom - cornerRadius),
            new Vector2(bounds.Left + cornerRadius, bounds.Bottom - cornerRadius),
        };
        var startAngles = new[] { MathF.PI, MathF.PI * 1.5f, 0f, MathF.PI * 0.5f };
        for (var corner = 0; corner < corners.Length; corner += 1)
        {
            var center = corners[corner];
            var startAngle = startAngles[corner];
            for (var segment = 0; segment < cornerSegments; segment += 1)
            {
                var angleA = startAngle + (segment / (float)cornerSegments) * (MathF.PI * 0.5f);
                var angleB = startAngle + ((segment + 1) / (float)cornerSegments) * (MathF.PI * 0.5f);
                var pointA = new Vector2(
                    center.X + (MathF.Cos(angleA) * cornerRadius),
                    center.Y + (MathF.Sin(angleA) * cornerRadius));
                var pointB = new Vector2(
                    center.X + (MathF.Cos(angleB) * cornerRadius),
                    center.Y + (MathF.Sin(angleB) * cornerRadius));
                DrawGarrisonBuilderLine(pointA, pointB, color, thickness);
            }
        }
    }

    private void DrawGarrisonBuilderLogicActivator(CustomMapBuilderEntity entity)
    {
        DrawGarrisonBuilderLogicCircleLabel(entity, "ACT");
    }

    private void DrawGarrisonBuilderLogicCircleLabel(CustomMapBuilderEntity entity, string label)
    {
        if (!TryGetGarrisonBuilderLogicNodeWorldBounds(entity, out var left, out var top, out var width, out var height))
        {
            return;
        }

        var center = BuilderWorldToScreen(new Vector2(entity.X, entity.Y));
        var topLeft = BuilderWorldToScreen(new Vector2(left, top));
        var bottomRight = BuilderWorldToScreen(new Vector2(left + width, top + height));
        var radiusX = MathF.Abs(bottomRight.X - topLeft.X) * 0.5f;
        var radiusY = MathF.Abs(bottomRight.Y - topLeft.Y) * 0.5f;
        if (radiusX < 0.5f || radiusY < 0.5f)
        {
            return;
        }

        DrawGarrisonBuilderLogicEyeShape(center, radiusX, radiusY, GetGarrisonBuilderLogicFillColor(entity));
        DrawGarrisonBuilderLogicNodeLabel(
            new Rectangle(
                (int)MathF.Floor(center.X - radiusX),
                (int)MathF.Floor(center.Y - radiusY),
                Math.Max(1, (int)MathF.Ceiling(radiusX * 2f)),
                Math.Max(1, (int)MathF.Ceiling(radiusY * 2f))),
            label);
    }

    private void DrawGarrisonBuilderLogicEyeShape(Vector2 center, float radiusX, float radiusY, Color fillColor)
    {
        const int segments = 20;
        var outlineThickness = GetGarrisonBuilderLogicScreenOutlineThickness();
        var fillPoints = new Vector2[segments];
        for (var segment = 0; segment < segments; segment += 1)
        {
            var angle = (segment / (float)segments) * MathF.PI * 2f;
            fillPoints[segment] = new Vector2(
                center.X + (MathF.Cos(angle) * radiusX),
                center.Y + (MathF.Sin(angle) * radiusY));
        }

        for (var segment = 0; segment < segments; segment += 1)
        {
            var next = (segment + 1) % segments;
            DrawGarrisonBuilderFilledTriangle(center, fillPoints[segment], fillPoints[next], fillColor);
        }

        for (var segment = 0; segment < segments; segment += 1)
        {
            var next = (segment + 1) % segments;
            DrawGarrisonBuilderLine(fillPoints[segment], fillPoints[next], GarrisonBuilderLogicOutlineColor, outlineThickness);
        }
    }

    private void DrawGarrisonBuilderThickRectangleOutline(Rectangle rectangle, Color color, int thickness)
    {
        for (var layer = 0; layer < thickness; layer += 1)
        {
            var inset = new Rectangle(
                rectangle.X + layer,
                rectangle.Y + layer,
                Math.Max(1, rectangle.Width - (layer * 2)),
                Math.Max(1, rectangle.Height - (layer * 2)));
            DrawGarrisonBuilderRectangleOutline(inset, color);
        }
    }

    private void DrawGarrisonBuilderExtendableAreaMapPickHighlights()
    {
        if (!_builderEntityMapPickActive
            || !_builderEntityMapPickTargetPropertyKey.Equals(
                AreaExtensionMetadata.ExtendsPropertyKey,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        for (var index = 0; index < _builderEntities.Count; index += 1)
        {
            if (IsGarrisonBuilderEntityHidden(index))
            {
                continue;
            }

            var entity = _builderEntities[index];
            if (!AreaExtensionMetadata.IsExtendableAreaEntityType(entity.Type))
            {
                continue;
            }

            if (!TryGetGarrisonBuilderEntityWorldBounds(entity, out var left, out var top, out var width, out var height))
            {
                continue;
            }

            var highlight = new RectangleF(left - 2f, top - 2f, width + 4f, height + 4f);
            DrawGarrisonBuilderRectangleOutline(ToScreenRectangle(highlight), new Color(120, 200, 255, 220));
            _spriteBatch.Draw(_pixel, ToScreenRectangle(highlight), new Color(120, 200, 255, 24));
        }
    }

    private void DrawGarrisonBuilderLogicMapPickHighlights()
    {
        if (!_builderLogicMapPickActive)
        {
            return;
        }

        for (var index = 0; index < _builderEntities.Count; index += 1)
        {
            if (IsGarrisonBuilderEntityHidden(index))
            {
                continue;
            }

            var entity = _builderEntities[index];
            if (!IsGarrisonBuilderLogicOutputEntity(entity))
            {
                continue;
            }

            if (!TryGetGarrisonBuilderLogicNodeWorldBounds(entity, out var left, out var top, out var width, out var height))
            {
                continue;
            }

            var highlight = new RectangleF(left - 4f, top - 4f, width + 8f, height + 8f);
            DrawGarrisonBuilderRectangleOutline(ToScreenRectangle(highlight), new Color(255, 230, 120, 220));
            _spriteBatch.Draw(_pixel, ToScreenRectangle(highlight), new Color(255, 230, 120, 28));
        }
    }

    private void DrawGarrisonBuilderLogicSignalLinks()
    {
        _builderLogicConnectionAnchorScratch.Clear();
        for (var entityIndex = 0; entityIndex < _builderEntities.Count; entityIndex += 1)
        {
            if (IsGarrisonBuilderEntityHidden(entityIndex))
            {
                continue;
            }

            var entity = _builderEntities[entityIndex];
            if (entity.Type.Equals(MapLogicMetadata.CpTriggerEntityType, StringComparison.OrdinalIgnoreCase))
            {
                DrawGarrisonBuilderCpTriggerLinks(entity);
                continue;
            }

            if (entity.Type.Equals(MapLogicMetadata.GateEntityType, StringComparison.OrdinalIgnoreCase))
            {
                DrawGarrisonBuilderLogicNodeInputLink(entity, MapLogicMetadata.LogicInput1PropertyKey, 0);
                DrawGarrisonBuilderLogicNodeInputLink(entity, MapLogicMetadata.LogicInput2PropertyKey, 1);
                continue;
            }

            if (entity.Type.Equals(MapLogicMetadata.NotEntityType, StringComparison.OrdinalIgnoreCase))
            {
                DrawGarrisonBuilderLogicNodeInputLink(entity, MapLogicMetadata.LogicInputPropertyKey, 0);
                continue;
            }

            if (entity.Type.Equals(MapLogicMetadata.RisingEdgeEntityType, StringComparison.OrdinalIgnoreCase))
            {
                DrawGarrisonBuilderLogicNodeInputLink(entity, MapLogicMetadata.LogicInputPropertyKey, 0);
                continue;
            }

            if (entity.Type.Equals(MapLogicMetadata.LatchEntityType, StringComparison.OrdinalIgnoreCase))
            {
                DrawGarrisonBuilderLogicNodeInputLink(entity, MapLogicMetadata.LogicInputPropertyKey, 0);
                DrawGarrisonBuilderLogicNodeInputLink(entity, MapLogicMetadata.LogicResetPropertyKey, 1);
                continue;
            }

            if (entity.Type.Equals(MapLogicMetadata.ActivatorEntityType, StringComparison.OrdinalIgnoreCase))
            {
                DrawGarrisonBuilderLogicNodeInputLink(entity, MapLogicMetadata.LogicInputPropertyKey, 0);
                DrawGarrisonBuilderActivatorEntityLink(entity);
                continue;
            }

            if (entity.Type.Equals(MapLogicMetadata.TimerEntityType, StringComparison.OrdinalIgnoreCase))
            {
                DrawGarrisonBuilderLogicNodeInputLink(entity, MapLogicMetadata.LogicInputPropertyKey, 0);
                continue;
            }

            if (entity.Type.Equals(MapLogicMetadata.ScoreTriggerEntityType, StringComparison.OrdinalIgnoreCase))
            {
                DrawGarrisonBuilderLogicNodeInputLink(entity, MapLogicMetadata.LogicInputPropertyKey, 0);
                continue;
            }

            if (entity.Type.Equals(MapLogicMetadata.OscillatorEntityType, StringComparison.OrdinalIgnoreCase))
            {
                DrawGarrisonBuilderLogicNodeInputLink(entity, MapLogicMetadata.StartWhenPropertyKey, 0);
                DrawGarrisonBuilderLogicNodeInputLink(entity, MapLogicMetadata.EndWhenPropertyKey, 1);
                continue;
            }

            if (entity.Type.Equals(DamageableMetadata.DamageableEntityType, StringComparison.OrdinalIgnoreCase))
            {
                DrawGarrisonBuilderLogicConsumerLink(
                    entity,
                    GetEntityProperty(entity.Properties, DamageableMetadata.HealWhenPropertyKey, string.Empty));
                continue;
            }

            if (entity.Type.Equals(DamageTriggerMetadata.DamageTriggerEntityType, StringComparison.OrdinalIgnoreCase))
            {
                DrawGarrisonBuilderDamageTriggerEntityLink(entity);
            }
        }

        for (var entityIndex = 0; entityIndex < _builderEntities.Count; entityIndex += 1)
        {
            if (IsGarrisonBuilderEntityHidden(entityIndex))
            {
                continue;
            }

            var entity = _builderEntities[entityIndex];
            if (entity.Type.Equals(TeleportMetadata.TeleportEntityType, StringComparison.OrdinalIgnoreCase))
            {
                DrawGarrisonBuilderTeleportExitLink(entity);
            }
        }

        for (var entityIndex = 0; entityIndex < _builderEntities.Count; entityIndex += 1)
        {
            if (IsGarrisonBuilderEntityHidden(entityIndex))
            {
                continue;
            }

            var entity = _builderEntities[entityIndex];
            if (IsGarrisonBuilderForwardSpawnEntity(entity))
            {
                DrawGarrisonBuilderLogicConsumerLink(
                    entity,
                    GetEntityProperty(entity.Properties, MapLogicMetadata.LogicSignalPropertyKey, string.Empty));
            }

            if (IsGarrisonBuilderControlPointEntity(entity))
            {
                DrawGarrisonBuilderLogicConsumerLink(
                    entity,
                    GetEntityProperty(entity.Properties, MapLogicMetadata.LockedWhenLogicPropertyKey, string.Empty));
                DrawGarrisonBuilderLogicConsumerLink(
                    entity,
                    GetEntityProperty(entity.Properties, MapLogicMetadata.UnlockedWhenLogicPropertyKey, string.Empty));
            }
        }
    }

    private void DrawGarrisonBuilderCpTriggerLinks(CustomMapBuilderEntity trigger)
    {
        var link = GetEntityProperty(trigger.Properties, MapLogicMetadata.LinkedControlPointPropertyKey, string.Empty);
        var objectiveIndex = GetEntityInt(trigger, "objectiveIndex", 0);
        if (!TryFindGarrisonBuilderObjectiveByLink(link, objectiveIndex, out var controlPoint))
        {
            return;
        }

        DrawGarrisonBuilderLogicLink(controlPoint, trigger);
    }

    private void DrawGarrisonBuilderDamageTriggerEntityLink(CustomMapBuilderEntity trigger)
    {
        var entityRef = GetEntityProperty(trigger.Properties, DamageTriggerMetadata.DamageableEntityPropertyKey, string.Empty);
        if (!MapLogicEntityReference.TryFindBuilderEntityIndex(_builderEntities, entityRef, out var targetIndex))
        {
            return;
        }

        DrawGarrisonBuilderLogicLink(_builderEntities[targetIndex], trigger);
    }

    private void DrawGarrisonBuilderLogicNodeInputLink(CustomMapBuilderEntity target, string propertyKey, int inputSlot)
    {
        var logicRef = GetEntityProperty(target.Properties, propertyKey, string.Empty);
        if (!TryFindGarrisonBuilderLogicSource(logicRef, out var source, out var sourceIndex))
        {
            return;
        }

        var targetIndex = _builderEntities.IndexOf(target);
        DrawGarrisonBuilderLogicLink(
            source,
            sourceIndex,
            target,
            targetIndex >= 0 ? targetIndex : 0,
            IsGarrisonBuilderLogicLinkHighlighted(source, target));
    }

    private void DrawGarrisonBuilderActivatorEntityLink(CustomMapBuilderEntity activator)
    {
        var entityRef = GetEntityProperty(activator.Properties, MapLogicMetadata.ActivatorEntityPropertyKey, string.Empty);
        if (!MapLogicEntityReference.TryFindBuilderEntityIndex(_builderEntities, entityRef, out var targetIndex))
        {
            return;
        }

        var target = _builderEntities[targetIndex];
        var activatorIndex = _builderEntities.IndexOf(activator);
        DrawGarrisonBuilderLogicLink(
            activator,
            activatorIndex >= 0 ? activatorIndex : 0,
            target,
            targetIndex,
            IsGarrisonBuilderLogicLinkHighlighted(activator, target));
    }

    private void DrawGarrisonBuilderTeleportExitLink(CustomMapBuilderEntity teleport)
    {
        var exitRef = GetEntityProperty(teleport.Properties, TeleportMetadata.TeleportExitPropertyKey, string.Empty);
        if (!MapLogicEntityReference.TryFindBuilderEntityIndex(_builderEntities, exitRef, out var targetIndex))
        {
            return;
        }

        var exit = _builderEntities[targetIndex];
        DrawGarrisonBuilderLogicLink(teleport, exit);
    }

    private string GetGarrisonBuilderTeleportExitDisplayValue(string value)
    {
        if (!MapLogicEntityReference.TryFindBuilderEntityIndex(_builderEntities, value, out var entityIndex))
        {
            return "none";
        }

        var entity = _builderEntities[entityIndex];
        if (CustomMapBuilderEntityCatalog.TryGetDefinition(entity.Type, out var definition))
        {
            return definition.Label;
        }

        return entity.Type;
    }

    private void DrawGarrisonBuilderLogicConsumerLink(CustomMapBuilderEntity consumer, string logicRef)
    {
        if (string.IsNullOrWhiteSpace(logicRef)
            || !TryFindGarrisonBuilderLogicSource(logicRef, out var source, out var sourceIndex))
        {
            return;
        }

        var consumerIndex = _builderEntities.IndexOf(consumer);
        DrawGarrisonBuilderLogicLink(
            source,
            sourceIndex,
            consumer,
            consumerIndex >= 0 ? consumerIndex : 0,
            IsGarrisonBuilderLogicLinkHighlighted(source, consumer));
    }

    private string FormatGarrisonBuilderLogicPropertyRowLabel(string key, string value)
    {
        if (key.Equals(MapLogicMetadata.LinkedControlPointPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            var summary = DescribeGarrisonBuilderObjectiveLink(value);
            return $"CP: {summary}";
        }

        if (key.Equals(MapLogicMetadata.GateTypePropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return $"Type: {MapLogicMetadata.GetGateTypeDisplayLabel(value)}";
        }

        if (key.Equals(MapLogicMetadata.RequiredOwnerPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return $"Team ownership: {MapLogicMetadata.GetRequiredOwnerDisplayLabel(value)}";
        }

        if (key.Equals(MapLogicSignalMetadata.SignalPropertyKey, StringComparison.OrdinalIgnoreCase)
            && TryGetGarrisonBuilderEditedEntityType(out var signalEntity)
            && MapLogicSignalMetadata.SupportsSignalProperty(signalEntity))
        {
            return $"Signal: {MapLogicSignalMetadata.GetSignalDisplayLabel(value)}";
        }

        if (key.Equals(MapLogicSignalMetadata.DetectPropertyKey, StringComparison.OrdinalIgnoreCase)
            && TryGetGarrisonBuilderEditedEntityType(out var detectEntity))
        {
            if (detectEntity.Equals(MapLogicMetadata.CpTriggerEntityType, StringComparison.OrdinalIgnoreCase))
            {
                return $"Detect: {MapLogicSignalMetadata.GetCpCaptureDetectDisplayLabel(value)}";
            }

            if (detectEntity.Equals(MapLogicMetadata.PlayerTriggerEntityType, StringComparison.OrdinalIgnoreCase))
            {
                return $"Detect: {MapLogicSignalMetadata.GetPlayerDetectDisplayLabel(value)}";
            }
        }

        if (key.Equals(MapLogicSignalMetadata.PeriodPropertyKey, StringComparison.OrdinalIgnoreCase)
            && TryGetGarrisonBuilderEditedEntityType(out var periodEntity)
            && periodEntity.Equals(MapLogicMetadata.OscillatorEntityType, StringComparison.OrdinalIgnoreCase))
        {
            return $"Period (sec): {MapLogicSignalMetadata.ToPeriodPropertyValue(MapLogicSignalMetadata.ParsePeriodSeconds(value))}";
        }

        if (key.Equals(PlayerTriggerMetadata.TeamPropertyKey, StringComparison.OrdinalIgnoreCase)
            && TryGetGarrisonBuilderEditedEntityType(out var playerTriggerTeamEntity)
            && playerTriggerTeamEntity.Equals(MapLogicMetadata.PlayerTriggerEntityType, StringComparison.OrdinalIgnoreCase))
        {
            return $"Team: {PlayerTriggerMetadata.GetTeamDisplayLabel(value)}";
        }

        if (key.Equals(MapLogicScoreTriggerMetadata.ScoreTeamPropertyKey, StringComparison.OrdinalIgnoreCase)
            && TryGetGarrisonBuilderEditedEntityType(out var scoreTeamEntity)
            && scoreTeamEntity.Equals(MapLogicMetadata.ScoreTriggerEntityType, StringComparison.OrdinalIgnoreCase))
        {
            return $"Score team: {MapLogicScoreTriggerMetadata.GetScoreTeamDisplayLabel(value)}";
        }

        if (key.Equals(MapLogicScoreTriggerMetadata.ChangePropertyKey, StringComparison.OrdinalIgnoreCase)
            && TryGetGarrisonBuilderEditedEntityType(out var scoreChangeEntity)
            && scoreChangeEntity.Equals(MapLogicMetadata.ScoreTriggerEntityType, StringComparison.OrdinalIgnoreCase))
        {
            return $"Change: {MapLogicScoreTriggerMetadata.GetChangeDisplayLabel(value)}";
        }

        if (key.Equals(MapLogicMetadata.ActivatorBehaviorPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return $"Behavior: {MapLogicMetadata.GetActivatorBehaviorDisplayLabel(value)}";
        }

        if (key.Equals(MapLogicMetadata.ActivatorEntityPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return $"Entity: {GetGarrisonBuilderActivatorEntityDisplayValue(value)}";
        }

        if (key.Equals(TeleportMetadata.TeleportExitPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return $"Teleport exit: {GetGarrisonBuilderTeleportExitDisplayValue(value)}";
        }

        if (key.Equals(AreaExtensionMetadata.ExtendsPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return GetGarrisonBuilderAreaExtendsDisplayValue(value);
        }

        if (key.Equals(DamageTriggerMetadata.DamageableEntityPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return $"Damageable: {GetGarrisonBuilderEntityReferenceDisplayValue(value)}";
        }

        if (key.Equals(TeleportMetadata.TeamPropertyKey, StringComparison.OrdinalIgnoreCase)
            && TryGetGarrisonBuilderEditedEntityType(out var teleportTeamEntity)
            && teleportTeamEntity.Equals(TeleportMetadata.TeleportEntityType, StringComparison.OrdinalIgnoreCase))
        {
            return $"Team: {TeleportMetadata.GetTeamDisplayLabel(value)}";
        }

        if (key.Equals(MapLogicMetadata.ActivateOnStartPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            var enabled = value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1";
            return $"Activate on start: {(enabled ? "on" : "off")}";
        }

        if (key.Equals(MapLogicMetadata.CountdownSecondsPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return $"Countdown (sec): {MapLogicMetadata.ToCountdownSecondsPropertyValue(MapLogicMetadata.ParseCountdownSeconds(value))}";
        }

        if (key.Equals(MapLogicMetadata.TriggerOnStartPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            var enabled = value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1";
            return $"Trigger on start: {(enabled ? "on" : "off")}";
        }

        if (key.Equals(MapLogicMetadata.DelayedTruePropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            var enabled = value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1";
            return $"Delayed TRUE: {(enabled ? "on" : "off")}";
        }

        if (key.Equals(MapLogicMetadata.DelayedFalsePropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            var enabled = value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1";
            return $"Delayed FALSE: {(enabled ? "on" : "off")}";
        }

        if (key.Equals(MapLogicMetadata.TrueTimePropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return $"TRUE time (sec): {MapLogicMetadata.ToCountdownSecondsPropertyValue(MapLogicMetadata.ParseOscillatorIntervalSeconds(value))}";
        }

        if (key.Equals(MapLogicMetadata.FalseTimePropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return $"FALSE time (sec): {MapLogicMetadata.ToCountdownSecondsPropertyValue(MapLogicMetadata.ParseOscillatorIntervalSeconds(value))}";
        }

        if (key.Equals(MapLogicMetadata.InitialValuePropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return $"Initial value: {MapLogicMetadata.GetInitialValueDisplayLabel(value)}";
        }

        if (key.Equals(MapLogicMetadata.AutostartPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            var enabled = value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1";
            return $"Autostart: {(enabled ? "on" : "off")}";
        }

        if (IsGarrisonBuilderNodePriorityPropertyKey(key))
        {
            return "Node priority";
        }

        if (IsGarrisonBuilderLogicMapPickProperty(key))
        {
            var prefix = key switch
            {
                _ when key.Equals(MapLogicMetadata.StartWhenPropertyKey, StringComparison.OrdinalIgnoreCase) => "Start when",
                _ when key.Equals(MapLogicMetadata.EndWhenPropertyKey, StringComparison.OrdinalIgnoreCase) => "End when",
                _ when key.Equals(MapLogicMetadata.LogicInputPropertyKey, StringComparison.OrdinalIgnoreCase)
                    && TryGetGarrisonBuilderEditedEntityType(out var editedType)
                    && editedType.Equals(MapLogicMetadata.TimerEntityType, StringComparison.OrdinalIgnoreCase) => "Trigger",
                _ when key.Equals(MapLogicMetadata.LogicInputPropertyKey, StringComparison.OrdinalIgnoreCase) => "Input",
                _ when key.Equals(MapLogicMetadata.LogicInput1PropertyKey, StringComparison.OrdinalIgnoreCase) => "Input1",
                _ when key.Equals(MapLogicMetadata.LogicInput2PropertyKey, StringComparison.OrdinalIgnoreCase) => "Input2",
                _ when key.Equals(MapLogicMetadata.LogicResetPropertyKey, StringComparison.OrdinalIgnoreCase) => "Reset",
                _ when key.Equals(MapLogicMetadata.LogicSignalPropertyKey, StringComparison.OrdinalIgnoreCase) => "Logic signal",
                _ when key.Equals(MapLogicMetadata.LockedWhenLogicPropertyKey, StringComparison.OrdinalIgnoreCase) => "Lock when",
                _ when key.Equals(MapLogicMetadata.UnlockedWhenLogicPropertyKey, StringComparison.OrdinalIgnoreCase) => "Unlock when",
                _ when key.Equals(DamageableMetadata.HealWhenPropertyKey, StringComparison.OrdinalIgnoreCase) => "Heal when",
                _ => key,
            };
            return $"{prefix}: {GetGarrisonBuilderLogicPropertyDisplayValue(key, value)}";
        }

        if (IsGarrisonBuilderEntityMapPickProperty(key))
        {
            return $"{FormatGarrisonBuilderPropertyKeyLabel(key)}: {GetGarrisonBuilderLogicPropertyDisplayValue(key, value)}";
        }

        return FormatGarrisonBuilderPropertyRowLabel(key, value);
    }

    private static bool IsGarrisonBuilderNodePriorityPropertyKey(string key)
    {
        return key.Equals(MapLogicMetadata.NodePriorityPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(MapLogicMetadata.SignalPriorityPropertyKey, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsGarrisonBuilderScoreTriggerValuePropertyRow(string key)
    {
        return key.Equals(MapLogicScoreTriggerMetadata.ValuePropertyKey, StringComparison.OrdinalIgnoreCase)
            && TryGetGarrisonBuilderEditedEntityType(out var entityType)
            && entityType.Equals(MapLogicMetadata.ScoreTriggerEntityType, StringComparison.OrdinalIgnoreCase);
    }

    private void GetGarrisonBuilderScoreTriggerValueSliderLayout(
        Rectangle rowBounds,
        string value,
        float textScale,
        out string sliderDisplay,
        out Rectangle sliderBounds,
        out Rectangle digitBounds)
    {
        sliderDisplay = MapLogicScoreTriggerMetadata.FormatValueSliderDisplay(value);
        var sliderWidth = MeasureBitmapFontWidth(sliderDisplay, textScale);
        const float rightPadding = 8f;
        var sliderX = rowBounds.Right - rightPadding - sliderWidth;
        var textHeight = MeasureBitmapFontHeight(textScale);
        var textY = rowBounds.Y + MathF.Max(2f, ((rowBounds.Height - textHeight) * 0.5f) - 1f);
        sliderBounds = new Rectangle(
            (int)MathF.Floor(sliderX),
            (int)MathF.Floor(textY),
            (int)MathF.Ceiling(sliderWidth),
            (int)MathF.Ceiling(textHeight));

        var digitText = MapLogicScoreTriggerMetadata.ToValuePropertyValue(
            MapLogicScoreTriggerMetadata.StepValueProperty(value, 0));
        var digitWidth = MeasureBitmapFontWidth(digitText, textScale);
        var digitX = sliderBounds.X + ((sliderBounds.Width - digitWidth) * 0.5f);
        digitBounds = new Rectangle(
            (int)MathF.Floor(digitX),
            sliderBounds.Y,
            (int)MathF.Ceiling(digitWidth),
            sliderBounds.Height);
    }

    private void DrawGarrisonBuilderScoreTriggerValuePropertyRow(
        Rectangle rowBounds,
        string value,
        MouseState mouse,
        float textScale,
        bool hovered)
    {
        GetGarrisonBuilderScoreTriggerValueSliderLayout(
            rowBounds,
            value,
            textScale,
            out var sliderDisplay,
            out var sliderBounds,
            out _);

        var textY = rowBounds.Y + MathF.Max(2f, ((rowBounds.Height - MeasureBitmapFontHeight(textScale)) * 0.5f) - 1f);
        var label = "Score";
        var labelMaxWidth = MathF.Max(40f, sliderBounds.X - rowBounds.X - 10f);
        var displayLabel = TruncateGarrisonBuilderPropertyLabel(label, labelMaxWidth, textScale);
        var labelColor = _builderUseModernUi ? Color.White : Color.Black;
        var sliderHovered = hovered || sliderBounds.Contains(mouse.Position);
        var sliderColor = sliderHovered
            ? (_builderUseModernUi ? new Color(220, 220, 220) : Color.Black)
            : (_builderUseModernUi ? new Color(186, 186, 186) : new Color(64, 64, 64));
        if (_builderUseModernUi)
        {
            DrawBitmapFontText(displayLabel, new Vector2(rowBounds.X + 6f, textY), labelColor, textScale);
            DrawBitmapFontText(sliderDisplay, new Vector2(sliderBounds.X, textY), sliderColor, textScale);
        }
        else
        {
            DrawGarrisonBuilderText(displayLabel, rowBounds.Location.ToVector2() + new Vector2(4f, 3f), labelColor, textScale);
            DrawGarrisonBuilderText(sliderDisplay, new Vector2(sliderBounds.X, rowBounds.Y + 3f), sliderColor, textScale);
        }
    }

    private bool TryHandleGarrisonBuilderScoreTriggerValueClick(
        string key,
        string value,
        Rectangle rowBounds,
        Point position)
    {
        if (!IsGarrisonBuilderScoreTriggerValuePropertyRow(key))
        {
            return false;
        }

        GetGarrisonBuilderScoreTriggerValueSliderLayout(
            rowBounds,
            value,
            GetGarrisonBuilderPropertyRowTextScale(),
            out _,
            out var sliderBounds,
            out var digitBounds);
        if (!sliderBounds.Contains(position))
        {
            return false;
        }

        if (digitBounds.Contains(position))
        {
            BeginGarrisonBuilderScoreTriggerValueTextEdit(key, value);
            return true;
        }

        var delta = position.X < sliderBounds.X + (sliderBounds.Width / 2) ? -1 : 1;
        _builderPropertyEditorValues[key] = MapLogicScoreTriggerMetadata.ToValuePropertyValue(
            MapLogicScoreTriggerMetadata.StepValueProperty(value, delta));
        ApplyGarrisonBuilderPropertyEditorLivePreview();
        MarkGarrisonBuilderPropertyEditorChanged();
        return true;
    }

    private void BeginGarrisonBuilderScoreTriggerValueTextEdit(string key, string value)
    {
        BeginGarrisonBuilderPropertyTextEdit(
            key,
            MapLogicScoreTriggerMetadata.ToValuePropertyValue(MapLogicScoreTriggerMetadata.StepValueProperty(value, 0)),
            GarrisonBuilderPropertyEditMode.EditValue);
    }

    private bool IsGarrisonBuilderScoreTriggerValueTextEditActive()
    {
        return _builderPropertyEditMode == GarrisonBuilderPropertyEditMode.EditValue
            && IsGarrisonBuilderScoreTriggerValuePropertyRow(_builderPropertyEditKey);
    }

    private bool IsGarrisonBuilderNodePriorityPropertyRow(string key)
    {
        if (!IsGarrisonBuilderNodePriorityPropertyKey(key))
        {
            return false;
        }

        return TryGetGarrisonBuilderEditedEntityType(out var entityType)
            && MapLogicMetadata.IsLogicEntityType(entityType);
    }

    private void GetGarrisonBuilderNodePrioritySliderLayout(
        Rectangle rowBounds,
        string value,
        float textScale,
        out string sliderDisplay,
        out Rectangle sliderBounds,
        out Rectangle digitBounds)
    {
        sliderDisplay = MapLogicMetadata.FormatNodePrioritySliderDisplay(value);
        var sliderWidth = MeasureBitmapFontWidth(sliderDisplay, textScale);
        const float rightPadding = 8f;
        var sliderX = rowBounds.Right - rightPadding - sliderWidth;
        var textHeight = MeasureBitmapFontHeight(textScale);
        var textY = rowBounds.Y + MathF.Max(2f, ((rowBounds.Height - textHeight) * 0.5f) - 1f);
        sliderBounds = new Rectangle(
            (int)MathF.Floor(sliderX),
            (int)MathF.Floor(textY),
            (int)MathF.Ceiling(sliderWidth),
            (int)MathF.Ceiling(textHeight));

        var digitText = MapLogicMetadata.GetNodePriorityDisplayLabel(value);
        var digitWidth = MeasureBitmapFontWidth(digitText, textScale);
        var digitX = sliderBounds.X + ((sliderBounds.Width - digitWidth) * 0.5f);
        digitBounds = new Rectangle(
            (int)MathF.Floor(digitX),
            sliderBounds.Y,
            (int)MathF.Ceiling(digitWidth),
            sliderBounds.Height);
    }

    private void DrawGarrisonBuilderNodePriorityPropertyRow(
        Rectangle rowBounds,
        string value,
        MouseState mouse,
        float textScale,
        bool hovered)
    {
        var label = "Node priority";
        GetGarrisonBuilderNodePrioritySliderLayout(
            rowBounds,
            value,
            textScale,
            out var sliderDisplay,
            out var sliderBounds,
            out _);

        var textY = rowBounds.Y + MathF.Max(2f, ((rowBounds.Height - MeasureBitmapFontHeight(textScale)) * 0.5f) - 1f);
        var labelMaxWidth = MathF.Max(40f, sliderBounds.X - rowBounds.X - 10f);
        var displayLabel = TruncateGarrisonBuilderPropertyLabel(label, labelMaxWidth, textScale);
        var labelColor = _builderUseModernUi ? Color.White : Color.Black;
        if (_builderUseModernUi)
        {
            DrawBitmapFontText(displayLabel, new Vector2(rowBounds.X + 6f, textY), labelColor, textScale);
        }
        else
        {
            DrawGarrisonBuilderText(displayLabel, rowBounds.Location.ToVector2() + new Vector2(4f, 3f), labelColor, textScale);
        }

        var sliderHovered = hovered || sliderBounds.Contains(mouse.Position);
        var sliderColor = sliderHovered
            ? (_builderUseModernUi ? new Color(220, 220, 220) : Color.Black)
            : (_builderUseModernUi ? new Color(186, 186, 186) : new Color(64, 64, 64));
        if (_builderUseModernUi)
        {
            DrawBitmapFontText(sliderDisplay, new Vector2(sliderBounds.X, textY), sliderColor, textScale);
        }
        else
        {
            DrawGarrisonBuilderText(sliderDisplay, new Vector2(sliderBounds.X, rowBounds.Y + 3f), sliderColor, textScale);
        }
    }

    private bool TryHandleGarrisonBuilderNodePriorityClick(
        string key,
        string value,
        Rectangle rowBounds,
        Point position)
    {
        GetGarrisonBuilderNodePrioritySliderLayout(
            rowBounds,
            value,
            GetGarrisonBuilderPropertyRowTextScale(),
            out _,
            out var sliderBounds,
            out var digitBounds);

        if (!sliderBounds.Contains(position))
        {
            return false;
        }

        if (digitBounds.Contains(position))
        {
            BeginGarrisonBuilderNodePriorityTextEdit(key, value);
            return true;
        }

        var nextValue = position.X < sliderBounds.X + (sliderBounds.Width / 2)
            ? MapLogicMetadata.AdjustNodePriority(value, -MapLogicMetadata.NodePriorityStep)
            : MapLogicMetadata.AdjustNodePriority(value, MapLogicMetadata.NodePriorityStep);
        _builderPropertyEditorValues[MapLogicMetadata.NodePriorityPropertyKey] =
            MapLogicMetadata.ToNodePriorityPropertyValue(nextValue);
        _builderPropertyEditorValues.Remove(MapLogicMetadata.SignalPriorityPropertyKey);
        ApplyGarrisonBuilderPropertyEditorLivePreview();
        MarkGarrisonBuilderPropertyEditorChanged();
        return true;
    }

    private void BeginGarrisonBuilderNodePriorityTextEdit(string key, string value)
    {
        BeginGarrisonBuilderPropertyTextEdit(
            MapLogicMetadata.NodePriorityPropertyKey,
            MapLogicMetadata.ToNodePriorityPropertyValue(MapLogicMetadata.ParseNodePriority(value)),
            GarrisonBuilderPropertyEditMode.EditValue);
    }

    private bool IsGarrisonBuilderNodePriorityTextEditActive()
    {
        return _builderPropertyEditMode == GarrisonBuilderPropertyEditMode.EditValue
            && IsGarrisonBuilderNodePriorityPropertyRow(_builderPropertyEditKey);
    }

    private bool IsGarrisonBuilderTriggerBelowPercentPropertyRow(string key)
    {
        if (!key.Equals(DamageTriggerMetadata.TriggerBelowPercentPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return TryGetGarrisonBuilderEditedEntityType(out var entityType)
            && entityType.Equals(DamageTriggerMetadata.DamageTriggerEntityType, StringComparison.OrdinalIgnoreCase)
            && DamageTriggerMetadata.ParseTriggerBelowThreshold(_builderPropertyEditorValues);
    }

    private bool ShouldDimGarrisonBuilderDamageTriggerModeLabel(string key)
    {
        return TryGetGarrisonBuilderEditedEntityType(out var entityType)
            && entityType.Equals(DamageTriggerMetadata.DamageTriggerEntityType, StringComparison.OrdinalIgnoreCase)
            && DamageTriggerMetadata.IsInactiveExclusiveModeRow(_builderPropertyEditorValues, key);
    }

    private bool ShouldSkipGarrisonBuilderSignalModePropertyRow(string key)
    {
        if (!TryGetGarrisonBuilderEditedEntityType(out var entityType)
            || !MapLogicSignalMetadata.SupportsSignalProperty(entityType))
        {
            return false;
        }

        var impulse = MapLogicSignalMetadata.ParseSignalMode(_builderPropertyEditorValues)
            == MapLogicSignalMode.Impulse;

        if (entityType.Equals(MapLogicMetadata.CpTriggerEntityType, StringComparison.OrdinalIgnoreCase))
        {
            if (impulse
                && key.Equals(MapLogicMetadata.RequiredOwnerPropertyKey, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!impulse
                && key.Equals(MapLogicSignalMetadata.DetectPropertyKey, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        if (entityType.Equals(MapLogicMetadata.PlayerTriggerEntityType, StringComparison.OrdinalIgnoreCase)
            && !impulse
            && key.Equals(MapLogicSignalMetadata.DetectPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (entityType.Equals(MapLogicMetadata.TimerEntityType, StringComparison.OrdinalIgnoreCase)
            && impulse
            && (key.Equals(MapLogicMetadata.DelayedTruePropertyKey, StringComparison.OrdinalIgnoreCase)
                || key.Equals(MapLogicMetadata.DelayedFalsePropertyKey, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (entityType.Equals(MapLogicMetadata.OscillatorEntityType, StringComparison.OrdinalIgnoreCase))
        {
            if (impulse
                && (key.Equals(MapLogicMetadata.TrueTimePropertyKey, StringComparison.OrdinalIgnoreCase)
                    || key.Equals(MapLogicMetadata.FalseTimePropertyKey, StringComparison.OrdinalIgnoreCase)
                    || key.Equals(MapLogicMetadata.InitialValuePropertyKey, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            if (!impulse
                && key.Equals(MapLogicSignalMetadata.PeriodPropertyKey, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        if (entityType.Equals(DamageTriggerMetadata.DamageTriggerEntityType, StringComparison.OrdinalIgnoreCase))
        {
            if (DamageTriggerMetadata.ParseTriggerOnHeal(_builderPropertyEditorValues)
                && key.Equals(DamageTriggerMetadata.AffectedByTeamPropertyKey, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!impulse
                && (key.Equals(DamageTriggerMetadata.TriggerOnHealPropertyKey, StringComparison.OrdinalIgnoreCase)
                    || key.Equals(DamageTriggerMetadata.TriggerOnAnyDamagePropertyKey, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            if (impulse
                && key.Equals(MapLogicMetadata.TrueTimePropertyKey, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        if (entityType.Equals(MapLogicMetadata.IntelTriggerEntityType, StringComparison.OrdinalIgnoreCase))
        {
            if (impulse
                && key.Equals(IntelTriggerMetadata.TriggerWhenPropertyKey, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!impulse
                && (key.Equals(IntelTriggerMetadata.OnPickupPropertyKey, StringComparison.OrdinalIgnoreCase)
                    || key.Equals(IntelTriggerMetadata.OnDropPropertyKey, StringComparison.OrdinalIgnoreCase)
                    || key.Equals(IntelTriggerMetadata.OnCapturePropertyKey, StringComparison.OrdinalIgnoreCase)
                    || key.Equals(IntelTriggerMetadata.OnResetPropertyKey, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private void ToggleGarrisonBuilderDamageTriggerExclusiveMode(string key, string value)
    {
        if (DamageTriggerMetadata.ParseBoolProperty(value))
        {
            _builderPropertyEditorValues[key] = "false";
        }
        else
        {
            DamageTriggerMetadata.ApplyExclusiveModeSelection(_builderPropertyEditorValues, key);
        }

        ApplyGarrisonBuilderPropertyEditorLivePreview();
        MarkGarrisonBuilderPropertyEditorChanged();
    }

    private void GetGarrisonBuilderTriggerBelowPercentSliderLayout(
        Rectangle rowBounds,
        string value,
        float textScale,
        out string sliderDisplay,
        out Rectangle sliderBounds,
        out Rectangle digitBounds)
    {
        sliderDisplay = $"< {DamageTriggerMetadata.GetTriggerBelowDisplayLabel(value)} >";
        var sliderWidth = MeasureBitmapFontWidth(sliderDisplay, textScale);
        const float rightPadding = 8f;
        var sliderX = rowBounds.Right - rightPadding - sliderWidth;
        var textHeight = MeasureBitmapFontHeight(textScale);
        var textY = rowBounds.Y + MathF.Max(2f, ((rowBounds.Height - textHeight) * 0.5f) - 1f);
        sliderBounds = new Rectangle(
            (int)MathF.Floor(sliderX),
            (int)MathF.Floor(textY),
            (int)MathF.Ceiling(sliderWidth),
            (int)MathF.Ceiling(textHeight));

        var digitText = DamageTriggerMetadata.GetTriggerBelowDisplayLabel(value);
        var digitWidth = MeasureBitmapFontWidth(digitText, textScale);
        var digitX = sliderBounds.X + ((sliderBounds.Width - digitWidth) * 0.5f);
        digitBounds = new Rectangle(
            (int)MathF.Floor(digitX),
            sliderBounds.Y,
            (int)MathF.Ceiling(digitWidth),
            sliderBounds.Height);
    }

    private void DrawGarrisonBuilderTriggerBelowPercentPropertyRow(
        Rectangle rowBounds,
        string value,
        MouseState mouse,
        float textScale,
        bool hovered)
    {
        var label = "Trigger below";
        GetGarrisonBuilderTriggerBelowPercentSliderLayout(
            rowBounds,
            value,
            textScale,
            out var sliderDisplay,
            out var sliderBounds,
            out _);

        var textY = rowBounds.Y + MathF.Max(2f, ((rowBounds.Height - MeasureBitmapFontHeight(textScale)) * 0.5f) - 1f);
        var labelMaxWidth = MathF.Max(40f, sliderBounds.X - rowBounds.X - 10f);
        var displayLabel = TruncateGarrisonBuilderPropertyLabel(label, labelMaxWidth, textScale);
        var labelColor = _builderUseModernUi ? Color.White : Color.Black;
        if (_builderUseModernUi)
        {
            DrawBitmapFontText(displayLabel, new Vector2(rowBounds.X + 6f, textY), labelColor, textScale);
        }
        else
        {
            DrawGarrisonBuilderText(displayLabel, rowBounds.Location.ToVector2() + new Vector2(4f, 3f), labelColor, textScale);
        }

        var sliderHovered = hovered || sliderBounds.Contains(mouse.Position);
        var sliderColor = sliderHovered
            ? (_builderUseModernUi ? new Color(220, 220, 220) : Color.Black)
            : (_builderUseModernUi ? new Color(186, 186, 186) : new Color(64, 64, 64));
        if (_builderUseModernUi)
        {
            DrawBitmapFontText(sliderDisplay, new Vector2(sliderBounds.X, textY), sliderColor, textScale);
        }
        else
        {
            DrawGarrisonBuilderText(sliderDisplay, new Vector2(sliderBounds.X, textY), sliderColor, textScale);
        }
    }

    private bool TryHandleGarrisonBuilderTriggerBelowPercentClick(
        string key,
        string value,
        Rectangle rowBounds,
        Point position)
    {
        if (!IsGarrisonBuilderTriggerBelowPercentPropertyRow(key))
        {
            return false;
        }

        GetGarrisonBuilderTriggerBelowPercentSliderLayout(
            rowBounds,
            value,
            GetGarrisonBuilderPropertyRowTextScale(),
            out _,
            out var sliderBounds,
            out var digitBounds);

        if (!sliderBounds.Contains(position))
        {
            return false;
        }

        if (digitBounds.Contains(position))
        {
            BeginGarrisonBuilderPropertyTextEdit(key, value, GarrisonBuilderPropertyEditMode.EditValue);
            return true;
        }

        var delta = position.X < sliderBounds.X + (sliderBounds.Width / 2) ? -5 : 5;
        _builderPropertyEditorValues[key] =
            DamageTriggerMetadata.ToTriggerBelowPercentPropertyValue(
                DamageTriggerMetadata.AdjustTriggerBelowPercent(value, delta));
        ApplyGarrisonBuilderPropertyEditorLivePreview();
        MarkGarrisonBuilderPropertyEditorChanged();
        return true;
    }

    private void OpenGarrisonBuilderLogicRecolorDialog(int entityIndex)
    {
        if (entityIndex < 0 || entityIndex >= _builderEntities.Count)
        {
            return;
        }

        var entity = _builderEntities[entityIndex];
        if (!MapLogicNodeColorMetadata.SupportsRecolor(entity.Type))
        {
            return;
        }

        CloseGarrisonBuilderPropertyEditor(applyChanges: false);
        _builderLogicRecolorDialogOpen = true;
        _builderLogicRecolorEntityIndex = entityIndex;
        _builderStatus = "recolor logic node";
    }

    private void CloseGarrisonBuilderLogicRecolorDialog()
    {
        _builderLogicRecolorDialogOpen = false;
        _builderLogicRecolorEntityIndex = -1;
    }

    private Rectangle GetGarrisonBuilderLogicRecolorDialogBounds()
    {
        const int columns = 4;
        const int rows = 4;
        var swatchSize = BuilderUi(34);
        var swatchGap = BuilderUi(6);
        var paletteWidth = (columns * swatchSize) + ((columns - 1) * swatchGap);
        var paletteHeight = (rows * swatchSize) + ((rows - 1) * swatchGap);
        var cancelButtonWidth = BuilderUi(96);
        var cancelButtonHeight = GetGarrisonBuilderMenuRowHeight();
        var width = Math.Max(paletteWidth + BuilderUi(32), cancelButtonWidth + BuilderUi(32));
        var height = BuilderUi(34) + paletteHeight + BuilderUi(14) + cancelButtonHeight + BuilderUi(16);
        return new Rectangle((BuilderViewportWidth - width) / 2, (BuilderViewportHeight - height) / 2, width, height);
    }

    private Rectangle GetGarrisonBuilderLogicRecolorSwatchBounds(Rectangle dialogBounds, int paletteIndex)
    {
        const int columns = 4;
        var swatchSize = BuilderUi(34);
        var swatchGap = BuilderUi(6);
        var paletteWidth = (columns * swatchSize) + ((columns - 1) * swatchGap);
        var paletteLeft = dialogBounds.X + ((dialogBounds.Width - paletteWidth) / 2);
        var paletteTop = dialogBounds.Y + BuilderUi(34);
        var column = paletteIndex % columns;
        var row = paletteIndex / columns;
        return new Rectangle(
            paletteLeft + (column * (swatchSize + swatchGap)),
            paletteTop + (row * (swatchSize + swatchGap)),
            swatchSize,
            swatchSize);
    }

    private Rectangle GetGarrisonBuilderLogicRecolorCancelButtonBounds(Rectangle dialogBounds)
    {
        const int rows = 4;
        var swatchSize = BuilderUi(34);
        var swatchGap = BuilderUi(6);
        var paletteHeight = (rows * swatchSize) + ((rows - 1) * swatchGap);
        var cancelButtonWidth = BuilderUi(96);
        var cancelButtonHeight = GetGarrisonBuilderMenuRowHeight();
        var paletteTop = dialogBounds.Y + BuilderUi(34);
        return new Rectangle(
            dialogBounds.X + ((dialogBounds.Width - cancelButtonWidth) / 2),
            paletteTop + paletteHeight + BuilderUi(14),
            cancelButtonWidth,
            cancelButtonHeight);
    }

    private void DrawGarrisonBuilderLogicRecolorDialog(MouseState mouse)
    {
        if (!_builderLogicRecolorDialogOpen || !_builderUseModernUi)
        {
            return;
        }

        var bounds = GetGarrisonBuilderLogicRecolorDialogBounds();
        DrawGarrisonBuilderBrownPanel(bounds);
        DrawBitmapFontText(
            "Recolor",
            new Vector2(bounds.X + 12f, bounds.Y + 8f),
            Color.White,
            GetGarrisonBuilderBitmapFontScale());

        for (var index = 0; index < MapLogicNodeColorMetadata.VgaPaletteHex.Length; index += 1)
        {
            var swatchBounds = GetGarrisonBuilderLogicRecolorSwatchBounds(bounds, index);
            if (MapLogicNodeColorMetadata.TryParseColor(
                    MapLogicNodeColorMetadata.VgaPaletteHex[index],
                    out var red,
                    out var green,
                    out var blue))
            {
                _spriteBatch.Draw(_pixel, swatchBounds, new Color(red, green, blue));
            }

            var outlineColor = swatchBounds.Contains(mouse.Position) ? Color.White : Color.Black;
            DrawGarrisonBuilderRectangleOutline(swatchBounds, outlineColor);
        }

        var cancelBounds = GetGarrisonBuilderLogicRecolorCancelButtonBounds(bounds);
        DrawBuilderMenuButton(cancelBounds, "Cancel", cancelBounds.Contains(mouse.Position));
    }

    private bool TryHandleGarrisonBuilderLogicRecolorDialogClick(Point position, bool leftClick)
    {
        if (!_builderLogicRecolorDialogOpen || !leftClick)
        {
            return _builderLogicRecolorDialogOpen;
        }

        var bounds = GetGarrisonBuilderLogicRecolorDialogBounds();
        if (!bounds.Contains(position))
        {
            CloseGarrisonBuilderLogicRecolorDialog();
            return true;
        }

        for (var index = 0; index < MapLogicNodeColorMetadata.VgaPaletteHex.Length; index += 1)
        {
            var swatchBounds = GetGarrisonBuilderLogicRecolorSwatchBounds(bounds, index);
            if (!swatchBounds.Contains(position))
            {
                continue;
            }

            if (MapLogicNodeColorMetadata.TryParseColor(
                    MapLogicNodeColorMetadata.VgaPaletteHex[index],
                    out var red,
                    out var green,
                    out var blue))
            {
                ApplyGarrisonBuilderLogicNodeColor(_builderLogicRecolorEntityIndex, red, green, blue);
            }

            CloseGarrisonBuilderLogicRecolorDialog();
            return true;
        }

        var cancelBounds = GetGarrisonBuilderLogicRecolorCancelButtonBounds(bounds);
        if (cancelBounds.Contains(position))
        {
            CloseGarrisonBuilderLogicRecolorDialog();
            return true;
        }

        return true;
    }

    private void ApplyGarrisonBuilderLogicNodeColor(int entityIndex, byte red, byte green, byte blue)
    {
        if (entityIndex < 0 || entityIndex >= _builderEntities.Count)
        {
            return;
        }

        RecordGarrisonBuilderHistory();
        var entity = _builderEntities[entityIndex];
        var properties = new Dictionary<string, string>(entity.Properties, StringComparer.OrdinalIgnoreCase);
        MapLogicNodeColorMetadata.SetColor(properties, red, green, blue);
        _builderEntities[entityIndex] = entity with { Properties = properties };
        _builderDirty = true;
        _builderStatus = "logic node recolored";
    }

}
