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

    private const float GarrisonBuilderLogicNodeWorldWidth = 34f;
    private const float GarrisonBuilderLogicNodeWorldHeight = 24f;
    private const float GarrisonBuilderLogicActivatorWorldWidth = 42f;
    private const float GarrisonBuilderLogicActivatorWorldHeight = 22f;
    private const float GarrisonBuilderLogicOutputAnchorOffsetY = 4f;
    private const float GarrisonBuilderLogicInputAnchorOffsetY = 4f;
    private const float GarrisonBuilderLogicBinaryInputAnchorOffsetX = 10f;

    private static readonly Color GarrisonBuilderLogicFillColor = new(48, 168, 156);
    private static readonly Color GarrisonBuilderLogicOutlineColor = Color.Black;
    private static readonly Color GarrisonBuilderLogicLinkColor = new(56, 188, 172, 215);
    private static readonly Color GarrisonBuilderLogicLinkHighlightColor = new(96, 232, 214, 255);

    private bool TryGetGarrisonBuilderLogicNodeWorldBounds(
        CustomMapBuilderEntity entity,
        out float left,
        out float top,
        out float width,
        out float height)
    {
        left = top = width = height = 0f;
        if (!MapLogicMetadata.IsLogicEntityType(entity.Type))
        {
            return false;
        }

        if (entity.Type.Equals(MapLogicMetadata.PlayerTriggerEntityType, StringComparison.OrdinalIgnoreCase))
        {
            (width, height) = PlayerTriggerMetadata.ResolveZoneDimensions(entity.XScale, entity.YScale);
            left = entity.X - (width * 0.5f);
            top = entity.Y - (height * 0.5f);
            return true;
        }

        if (entity.Type.Equals(MapLogicMetadata.ActivatorEntityType, StringComparison.OrdinalIgnoreCase)
            || entity.Type.Equals(MapLogicMetadata.TimerEntityType, StringComparison.OrdinalIgnoreCase))
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

    private void EnsureGarrisonBuilderLogicEntityKeys()
    {
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

        return TryPickGarrisonBuilderActivatorEntityAtWorld(world, out picked);
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
        return entity.Type.Equals(MapLogicMetadata.ActivatorEntityType, StringComparison.OrdinalIgnoreCase)
            ? GarrisonBuilderLogicActivatorWorldHeight * 0.5f
            : GarrisonBuilderLogicNodeWorldHeight * 0.5f;
    }

    private Vector2 GetGarrisonBuilderLogicOutputAnchor(CustomMapBuilderEntity entity)
    {
        var halfHeight = GetGarrisonBuilderLogicNodeHalfHeight(entity);
        return new Vector2(entity.X, entity.Y - halfHeight - GarrisonBuilderLogicOutputAnchorOffsetY);
    }

    private Vector2 GetGarrisonBuilderLogicInputAnchor(CustomMapBuilderEntity entity, int inputSlot)
    {
        var halfHeight = GetGarrisonBuilderLogicNodeHalfHeight(entity);
        if (entity.Type.Equals(MapLogicMetadata.ActivatorEntityType, StringComparison.OrdinalIgnoreCase))
        {
            return new Vector2(entity.X, entity.Y - halfHeight - GarrisonBuilderLogicOutputAnchorOffsetY);
        }

        if (entity.Type.Equals(MapLogicMetadata.NotEntityType, StringComparison.OrdinalIgnoreCase))
        {
            return new Vector2(entity.X, entity.Y + halfHeight + GarrisonBuilderLogicInputAnchorOffsetY);
        }

        var offsetX = inputSlot == 0 ? -GarrisonBuilderLogicBinaryInputAnchorOffsetX : GarrisonBuilderLogicBinaryInputAnchorOffsetX;
        return new Vector2(
            entity.X + offsetX,
            entity.Y + halfHeight + GarrisonBuilderLogicInputAnchorOffsetY);
    }

    private Vector2 GetGarrisonBuilderLogicActivatorEntityAnchor(CustomMapBuilderEntity activator)
    {
        var halfHeight = GetGarrisonBuilderLogicNodeHalfHeight(activator);
        return new Vector2(activator.X, activator.Y + halfHeight + GarrisonBuilderLogicInputAnchorOffsetY);
    }

    private Vector2 GetGarrisonBuilderLogicCpSourceAnchor(CustomMapBuilderEntity trigger)
    {
        var halfHeight = GarrisonBuilderLogicNodeWorldHeight * 0.5f;
        return new Vector2(trigger.X, trigger.Y + halfHeight + GarrisonBuilderLogicInputAnchorOffsetY);
    }

    private Vector2 GetGarrisonBuilderLogicConsumerInputAnchor(CustomMapBuilderEntity entity)
    {
        return GetGarrisonBuilderEntityLinkAnchor(entity);
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

    private void DrawGarrisonBuilderLogicLink(Vector2 startWorld, Vector2 endWorld, bool highlighted)
    {
        var color = highlighted ? GarrisonBuilderLogicLinkHighlightColor : GarrisonBuilderLogicLinkColor;
        var start = BuilderWorldToScreen(startWorld);
        var end = BuilderWorldToScreen(endWorld);
        var linkScale = GetGarrisonBuilderLinkVisualScale();
        var thickness = MathF.Max(2f, GarrisonBuilderLinkLineThickness * linkScale * 1.15f);
        DrawGarrisonBuilderLine(start, end, color, thickness);
        DrawGarrisonBuilderLinkEndpoint(start, color, linkScale);
        DrawGarrisonBuilderLinkEndpoint(end, color, linkScale);
    }

    private void DrawGarrisonBuilderLogicLink(
        CustomMapBuilderEntity source,
        Vector2 sourceAnchorWorld,
        CustomMapBuilderEntity target,
        Vector2 targetAnchorWorld)
    {
        DrawGarrisonBuilderLogicLink(
            sourceAnchorWorld,
            targetAnchorWorld,
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

    private static bool IsGarrisonBuilderEntityMapPickProperty(string key)
    {
        return IsGarrisonBuilderActivatorEntityMapPickProperty(key)
            || IsGarrisonBuilderTeleportExitMapPickProperty(key);
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
            || key.Equals(MapLogicMetadata.LogicSignalPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(MapLogicMetadata.LockedWhenLogicPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(MapLogicMetadata.UnlockedWhenLogicPropertyKey, StringComparison.OrdinalIgnoreCase);
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
        string[] order = entityType.Equals(MapLogicMetadata.PlayerTriggerEntityType, StringComparison.OrdinalIgnoreCase)
            ?
            [
                PlayerTriggerMetadata.TeamPropertyKey,
                MapLogicMetadata.NodePriorityPropertyKey,
            ]
            : entityType.Equals(MapLogicMetadata.CpTriggerEntityType, StringComparison.OrdinalIgnoreCase)
                ?
                [
                    MapLogicMetadata.LinkedControlPointPropertyKey,
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
                            MapLogicMetadata.CountdownSecondsPropertyKey,
                            MapLogicMetadata.LogicInputPropertyKey,
                            MapLogicMetadata.TriggerOnStartPropertyKey,
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
            return $"Team: {PlayerTriggerMetadata.GetTeamDisplayLabel(value)} (click to cycle)";
        }

        if (key.Equals(MapLogicMetadata.ActivatorEntityPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return GetGarrisonBuilderActivatorEntityDisplayValue(value);
        }

        if (IsGarrisonBuilderLogicMapPickProperty(key))
        {
            if (!MapLogicMetadata.TryParseLogicRef(value, out var logicKey)
                || !TryFindGarrisonBuilderLogicSource(MapLogicMetadata.FormatLogicRef(logicKey), out var source, out _))
            {
                return "none (click to pick on map)";
            }

            return $"{source.Type} ({logicKey})";
        }

        return value;
    }

    private string GetGarrisonBuilderActivatorEntityDisplayValue(string value)
    {
        if (!MapLogicEntityReference.TryFindBuilderEntityIndex(_builderEntities, value, out var entityIndex))
        {
            return "none (click to pick on map)";
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

        if (definition.Type.Equals(MapLogicMetadata.CpTriggerEntityType, StringComparison.OrdinalIgnoreCase))
        {
            DrawGarrisonBuilderLogicNode(entity, "CP", outlineThickness: 3);
            return true;
        }

        if (definition.Type.Equals(MapLogicMetadata.NotEntityType, StringComparison.OrdinalIgnoreCase))
        {
            DrawGarrisonBuilderLogicNode(entity, "NOT", outlineThickness: 2);
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

        MapLogicMetadata.TryParseGateType(
            GetEntityProperty(entity.Properties, MapLogicMetadata.GateTypePropertyKey, "and"),
            out var gateType);
        DrawGarrisonBuilderLogicNode(entity, gateType.ToString().ToUpperInvariant(), outlineThickness: 2);
        return true;
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
        var fillColor = Color.Lerp(
            new Color(
                (byte)GarrisonBuilderLogicFillColor.R,
                (byte)GarrisonBuilderLogicFillColor.G,
                (byte)GarrisonBuilderLogicFillColor.B,
                (byte)170),
            Color.White,
            0.2f);
        var borderColor = GarrisonBuilderLogicOutlineColor;
        _spriteBatch.Draw(_pixel, screenBounds, fillColor);
        DrawGarrisonBuilderRectangleOutline(screenBounds, borderColor);

        var labelScale = Math.Clamp(MathF.Min(width, height) / 42f, 0.55f, 1f) * (_builderUseModernUi ? 0.9f : 0.75f);
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
        _spriteBatch.Draw(_pixel, screenBounds, GarrisonBuilderLogicFillColor);
        DrawGarrisonBuilderThickRectangleOutline(screenBounds, GarrisonBuilderLogicOutlineColor, outlineThickness);

        var labelScale = GetGarrisonBuilderBitmapFontScaleToFit(
            label,
            screenBounds.Width * 0.85f,
            screenBounds.Height * 0.7f,
            0.65f);
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

    private void DrawGarrisonBuilderLogicTimer(CustomMapBuilderEntity entity)
    {
        DrawGarrisonBuilderLogicCircleLabel(entity, "TMR");
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
        var radiusX = MathF.Max(8f, MathF.Abs(bottomRight.X - topLeft.X) * 0.5f);
        var radiusY = MathF.Max(6f, MathF.Abs(bottomRight.Y - topLeft.Y) * 0.5f);
        DrawGarrisonBuilderLogicEyeShape(center, radiusX, radiusY);
        var labelScale = GetGarrisonBuilderBitmapFontScaleToFit(label, radiusX * 1.5f, radiusY * 1.1f, 0.6f);
        var labelWidth = MeasureBitmapFontWidth(label, labelScale);
        var labelHeight = MeasureBitmapFontHeight(labelScale);
        DrawBitmapFontText(
            label,
            new Vector2(center.X - (labelWidth * 0.5f), center.Y - (labelHeight * 0.5f)),
            Color.Black,
            labelScale);
    }

    private void DrawGarrisonBuilderLogicEyeShape(Vector2 center, float radiusX, float radiusY)
    {
        const int segments = 20;
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
            DrawGarrisonBuilderFilledTriangle(center, fillPoints[segment], fillPoints[next], GarrisonBuilderLogicFillColor);
        }

        for (var segment = 0; segment < segments; segment += 1)
        {
            var next = (segment + 1) % segments;
            DrawGarrisonBuilderLine(fillPoints[segment], fillPoints[next], GarrisonBuilderLogicOutlineColor, 2f);
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

            if (entity.Type.Equals(MapLogicMetadata.ActivatorEntityType, StringComparison.OrdinalIgnoreCase))
            {
                DrawGarrisonBuilderLogicNodeInputLink(entity, MapLogicMetadata.LogicInputPropertyKey, 0);
                DrawGarrisonBuilderActivatorEntityLink(entity);
                continue;
            }

            if (entity.Type.Equals(MapLogicMetadata.TimerEntityType, StringComparison.OrdinalIgnoreCase))
            {
                DrawGarrisonBuilderLogicNodeInputLink(entity, MapLogicMetadata.LogicInputPropertyKey, 0);
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

        DrawGarrisonBuilderLogicLink(
            controlPoint,
            GetGarrisonBuilderEntityLinkAnchor(controlPoint),
            trigger,
            GetGarrisonBuilderLogicCpSourceAnchor(trigger));
    }

    private void DrawGarrisonBuilderLogicNodeInputLink(CustomMapBuilderEntity target, string propertyKey, int inputSlot)
    {
        var logicRef = GetEntityProperty(target.Properties, propertyKey, string.Empty);
        if (!TryFindGarrisonBuilderLogicSource(logicRef, out var source, out _))
        {
            return;
        }

        DrawGarrisonBuilderLogicLink(
            source,
            GetGarrisonBuilderLogicOutputAnchor(source),
            target,
            GetGarrisonBuilderLogicInputAnchor(target, inputSlot));
    }

    private void DrawGarrisonBuilderActivatorEntityLink(CustomMapBuilderEntity activator)
    {
        var entityRef = GetEntityProperty(activator.Properties, MapLogicMetadata.ActivatorEntityPropertyKey, string.Empty);
        if (!MapLogicEntityReference.TryFindBuilderEntityIndex(_builderEntities, entityRef, out var targetIndex))
        {
            return;
        }

        var target = _builderEntities[targetIndex];
        DrawGarrisonBuilderLogicLink(
            activator,
            GetGarrisonBuilderLogicActivatorEntityAnchor(activator),
            target,
            GetGarrisonBuilderEntityLinkAnchor(target));
    }

    private void DrawGarrisonBuilderTeleportExitLink(CustomMapBuilderEntity teleport)
    {
        var exitRef = GetEntityProperty(teleport.Properties, TeleportMetadata.TeleportExitPropertyKey, string.Empty);
        if (!MapLogicEntityReference.TryFindBuilderEntityIndex(_builderEntities, exitRef, out var targetIndex))
        {
            return;
        }

        var exit = _builderEntities[targetIndex];
        DrawGarrisonBuilderLogicLink(
            teleport,
            GetGarrisonBuilderEntityLinkAnchor(teleport),
            exit,
            GetGarrisonBuilderEntityLinkAnchor(exit));
    }

    private string GetGarrisonBuilderTeleportExitDisplayValue(string value)
    {
        if (!MapLogicEntityReference.TryFindBuilderEntityIndex(_builderEntities, value, out var entityIndex))
        {
            return "none (click to pick on map)";
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
            || !TryFindGarrisonBuilderLogicSource(logicRef, out var source, out _))
        {
            return;
        }

        DrawGarrisonBuilderLogicLink(
            source,
            GetGarrisonBuilderLogicOutputAnchor(source),
            consumer,
            GetGarrisonBuilderLogicConsumerInputAnchor(consumer));
    }

    private string FormatGarrisonBuilderLogicPropertyRowLabel(string key, string value)
    {
        if (key.Equals(MapLogicMetadata.LinkedControlPointPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            var summary = DescribeGarrisonBuilderObjectiveLink(value);
            return $"CP: {summary} (click to pick on map)";
        }

        if (key.Equals(MapLogicMetadata.GateTypePropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return $"Type: {MapLogicMetadata.GetGateTypeDisplayLabel(value)} (click to cycle)";
        }

        if (key.Equals(MapLogicMetadata.RequiredOwnerPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return $"Team ownership: {MapLogicMetadata.GetRequiredOwnerDisplayLabel(value)} (click to cycle)";
        }

        if (key.Equals(MapLogicMetadata.ActivatorBehaviorPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return $"Behavior: {MapLogicMetadata.GetActivatorBehaviorDisplayLabel(value)} (click to cycle)";
        }

        if (key.Equals(MapLogicMetadata.ActivatorEntityPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return $"Entity: {GetGarrisonBuilderActivatorEntityDisplayValue(value)}";
        }

        if (key.Equals(TeleportMetadata.TeleportExitPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return $"Teleport exit: {GetGarrisonBuilderTeleportExitDisplayValue(value)}";
        }

        if (key.Equals(TeleportMetadata.TeamPropertyKey, StringComparison.OrdinalIgnoreCase)
            && TryGetGarrisonBuilderEditedEntityType(out var teleportTeamEntity)
            && teleportTeamEntity.Equals(TeleportMetadata.TeleportEntityType, StringComparison.OrdinalIgnoreCase))
        {
            return $"Team: {TeleportMetadata.GetTeamDisplayLabel(value)} (click to cycle)";
        }

        if (key.Equals(MapLogicMetadata.ActivateOnStartPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            var enabled = value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1";
            return $"Activate on start: {(enabled ? "on" : "off")} (click to toggle)";
        }

        if (key.Equals(MapLogicMetadata.CountdownSecondsPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return $"Countdown (sec): {MapLogicMetadata.ToCountdownSecondsPropertyValue(MapLogicMetadata.ParseCountdownSeconds(value))} (click to edit)";
        }

        if (key.Equals(MapLogicMetadata.TriggerOnStartPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            var enabled = value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1";
            return $"Trigger on start: {(enabled ? "on" : "off")} (click to toggle)";
        }

        if (IsGarrisonBuilderNodePriorityPropertyKey(key))
        {
            return "Node priority";
        }

        if (IsGarrisonBuilderLogicMapPickProperty(key))
        {
            var prefix = key switch
            {
                _ when key.Equals(MapLogicMetadata.LogicInputPropertyKey, StringComparison.OrdinalIgnoreCase)
                    && TryGetGarrisonBuilderEditedEntityType(out var editedType)
                    && editedType.Equals(MapLogicMetadata.TimerEntityType, StringComparison.OrdinalIgnoreCase) => "Trigger",
                _ when key.Equals(MapLogicMetadata.LogicInputPropertyKey, StringComparison.OrdinalIgnoreCase) => "Input",
                _ when key.Equals(MapLogicMetadata.LogicInput1PropertyKey, StringComparison.OrdinalIgnoreCase) => "Input1",
                _ when key.Equals(MapLogicMetadata.LogicInput2PropertyKey, StringComparison.OrdinalIgnoreCase) => "Input2",
                _ when key.Equals(MapLogicMetadata.LogicSignalPropertyKey, StringComparison.OrdinalIgnoreCase) => "Logic signal",
                _ when key.Equals(MapLogicMetadata.LockedWhenLogicPropertyKey, StringComparison.OrdinalIgnoreCase) => "Lock when",
                _ when key.Equals(MapLogicMetadata.UnlockedWhenLogicPropertyKey, StringComparison.OrdinalIgnoreCase) => "Unlock when",
                _ => key,
            };
            return $"{prefix}: {GetGarrisonBuilderLogicPropertyDisplayValue(key, value)}";
        }

        return FormatGarrisonBuilderPropertyRowLabel(key, value);
    }

    private static bool IsGarrisonBuilderNodePriorityPropertyKey(string key)
    {
        return key.Equals(MapLogicMetadata.NodePriorityPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(MapLogicMetadata.SignalPriorityPropertyKey, StringComparison.OrdinalIgnoreCase);
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

}
