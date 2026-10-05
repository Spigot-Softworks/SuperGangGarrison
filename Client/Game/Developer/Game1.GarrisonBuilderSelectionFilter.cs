#nullable enable

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

/// <summary>Which kinds of map objects clicks and marquee drags may pick in the builder.</summary>
public enum GarrisonBuilderSelectionFilter
{
    All,
    EffectBoxes,
    PointEntities,
    Sprites,
    WeatherRegions,
}

public partial class Game1
{
    private const int GarrisonBuilderSelectionFilterButtonWidth = 112;
    private const int GarrisonBuilderSelectionFilterMenuWidth = 156;

    private static readonly GarrisonBuilderSelectionFilter[] GarrisonBuilderSelectionFilterOptions =
    [
        GarrisonBuilderSelectionFilter.All,
        GarrisonBuilderSelectionFilter.EffectBoxes,
        GarrisonBuilderSelectionFilter.PointEntities,
        GarrisonBuilderSelectionFilter.Sprites,
        GarrisonBuilderSelectionFilter.WeatherRegions,
    ];

    private GarrisonBuilderSelectionFilter _builderSelectionFilter;
    private bool _builderSelectionFilterMenuOpen;

    /// <summary>
    /// Sorts an entity type into a filter category. Order matters: indoor regions and
    /// art are boxes too, but each has its own category.
    /// </summary>
    internal static GarrisonBuilderSelectionFilter ClassifyGarrisonBuilderEntityType(string type)
    {
        if (IndoorRegionMetadata.IsIndoorRegionEntityType(type))
        {
            return GarrisonBuilderSelectionFilter.WeatherRegions;
        }

        if (CustomMapCustomSpriteMetadata.IsCustomSpriteEntityType(type)
            || ForegroundSpriteMetadata.IsForegroundSpriteEntityType(type)
            || SpritesheetMetadata.IsSpritesheetEntityType(type))
        {
            return GarrisonBuilderSelectionFilter.Sprites;
        }

        if (IsGarrisonBuilderAnchorSizedEntityType(type)
            || AreaExtensionMetadata.IsAreaEntityType(type)
            || (CustomMapBuilderEntityCatalog.TryGetDefinition(type, out var definition)
                && IsGarrisonBuilderDefinitionScalable(definition)))
        {
            return GarrisonBuilderSelectionFilter.EffectBoxes;
        }

        return GarrisonBuilderSelectionFilter.PointEntities;
    }

    /// <summary>
    /// True when the entity may be picked by click, hover, marquee or area erase under
    /// the current filter. Linking/map-pick modes ignore the filter so references to
    /// any object can still be chosen.
    /// </summary>
    private bool PassesGarrisonBuilderSelectionFilter(int entityIndex)
    {
        if (_builderSelectionFilter == GarrisonBuilderSelectionFilter.All
            || _builderObjectiveMapPickActive
            || _builderLogicMapPickActive
            || _builderEntityMapPickActive
            || _builderMultiEntityMapPickActive
            || (uint)entityIndex >= (uint)_builderEntities.Count)
        {
            return true;
        }

        return ClassifyGarrisonBuilderEntityType(_builderEntities[entityIndex].Type) == _builderSelectionFilter;
    }

    private bool IsGarrisonBuilderEntityUnpickable(int entityIndex)
    {
        return IsGarrisonBuilderEntityHidden(entityIndex) || !PassesGarrisonBuilderSelectionFilter(entityIndex);
    }

    private static string GetGarrisonBuilderSelectionFilterLabel(GarrisonBuilderSelectionFilter filter) => filter switch
    {
        GarrisonBuilderSelectionFilter.EffectBoxes => "Effect boxes",
        GarrisonBuilderSelectionFilter.PointEntities => "Point entities",
        GarrisonBuilderSelectionFilter.Sprites => "Sprites & art",
        GarrisonBuilderSelectionFilter.WeatherRegions => "Weather regions",
        _ => "All objects",
    };

    private static string GetGarrisonBuilderSelectionFilterShortLabel(GarrisonBuilderSelectionFilter filter) => filter switch
    {
        GarrisonBuilderSelectionFilter.EffectBoxes => "Boxes",
        GarrisonBuilderSelectionFilter.PointEntities => "Points",
        GarrisonBuilderSelectionFilter.Sprites => "Sprites",
        GarrisonBuilderSelectionFilter.WeatherRegions => "Weather",
        _ => "All",
    };

    private void SetGarrisonBuilderSelectionFilter(GarrisonBuilderSelectionFilter filter)
    {
        _builderSelectionFilter = filter;
        _builderMapHoverEntityIndex = -1;
        _builderStatus = filter == GarrisonBuilderSelectionFilter.All
            ? "selection filter off"
            : $"selecting only: {GetGarrisonBuilderSelectionFilterLabel(filter).ToLowerInvariant()}";
    }

    private void CloseGarrisonBuilderSelectionFilterMenu()
    {
        _builderSelectionFilterMenuOpen = false;
    }

    private Rectangle GetModernGarrisonBuilderSelectionFilterButtonBounds()
    {
        var markBounds = GetModernGarrisonBuilderLayerStripMarkBounds();
        var lastTab = GetModernGarrisonBuilderLayerTabBounds(8);
        var x = Math.Max(lastTab.Right + 8, markBounds.X - 8 - GarrisonBuilderSelectionFilterButtonWidth);
        return new Rectangle(x, markBounds.Y, GarrisonBuilderSelectionFilterButtonWidth, markBounds.Height);
    }

    /// <summary>The menu opens upward from the filter button, right-aligned to it.</summary>
    private Rectangle GetGarrisonBuilderSelectionFilterMenuBounds()
    {
        const int rowGap = 2;
        const int padding = 4;
        var rowHeight = GetGarrisonBuilderMenuRowHeight();
        var count = GarrisonBuilderSelectionFilterOptions.Length;
        var height = padding + (count * rowHeight) + ((count - 1) * rowGap) + padding;
        var button = GetModernGarrisonBuilderSelectionFilterButtonBounds();
        var x = Math.Clamp(
            button.Right - GarrisonBuilderSelectionFilterMenuWidth,
            4,
            Math.Max(4, BuilderViewportWidth - GarrisonBuilderSelectionFilterMenuWidth - 4));
        var y = Math.Max(4, button.Y - height - 2);
        return new Rectangle(x, y, GarrisonBuilderSelectionFilterMenuWidth, height);
    }

    private Rectangle GetGarrisonBuilderSelectionFilterMenuItemBounds(Rectangle menuBounds, int index)
    {
        const int rowGap = 2;
        var rowHeight = GetGarrisonBuilderMenuRowHeight();
        var y = menuBounds.Y + 4 + (index * (rowHeight + rowGap));
        return new Rectangle(menuBounds.X + 4, y, menuBounds.Width - 8, rowHeight);
    }

    /// <summary>Strip button click: toggles the menu. Returns true when consumed.</summary>
    private bool TryHandleGarrisonBuilderSelectionFilterButtonClick(Point position)
    {
        if (!_builderLayerStripExpanded || !GetModernGarrisonBuilderSelectionFilterButtonBounds().Contains(position))
        {
            return false;
        }

        _builderSelectionFilterMenuOpen = !_builderSelectionFilterMenuOpen;
        CloseGarrisonBuilderLayerContextMenu();
        return true;
    }

    /// <summary>Open-menu click handling; outside clicks close it and fall through.</summary>
    private bool TryHandleGarrisonBuilderSelectionFilterMenuClick(Point position, bool leftClick)
    {
        if (!_builderSelectionFilterMenuOpen)
        {
            return false;
        }

        if (!leftClick)
        {
            return true;
        }

        var menuBounds = GetGarrisonBuilderSelectionFilterMenuBounds();
        if (!menuBounds.Contains(position))
        {
            CloseGarrisonBuilderSelectionFilterMenu();
            // A click on the button itself only closes the menu.
            return GetModernGarrisonBuilderSelectionFilterButtonBounds().Contains(position);
        }

        for (var index = 0; index < GarrisonBuilderSelectionFilterOptions.Length; index += 1)
        {
            if (GetGarrisonBuilderSelectionFilterMenuItemBounds(menuBounds, index).Contains(position))
            {
                SetGarrisonBuilderSelectionFilter(GarrisonBuilderSelectionFilterOptions[index]);
                break;
            }
        }

        CloseGarrisonBuilderSelectionFilterMenu();
        return true;
    }

    private void DrawModernGarrisonBuilderSelectionFilterButton(MouseState mouse)
    {
        var bounds = GetModernGarrisonBuilderSelectionFilterButtonBounds();
        var active = _builderSelectionFilter != GarrisonBuilderSelectionFilter.All;
        DrawBuilderMenuButton(
            bounds,
            $"Filter: {GetGarrisonBuilderSelectionFilterShortLabel(_builderSelectionFilter)} {(_builderSelectionFilterMenuOpen ? "v" : "^")}",
            active || _builderSelectionFilterMenuOpen || bounds.Contains(mouse.Position));
    }

    private void DrawGarrisonBuilderSelectionFilterMenu(MouseState mouse)
    {
        if (!_builderSelectionFilterMenuOpen)
        {
            return;
        }

        var menuBounds = GetGarrisonBuilderSelectionFilterMenuBounds();
        DrawMenuPanelBackdrop(menuBounds, 0.98f);
        for (var index = 0; index < GarrisonBuilderSelectionFilterOptions.Length; index += 1)
        {
            var option = GarrisonBuilderSelectionFilterOptions[index];
            var itemBounds = GetGarrisonBuilderSelectionFilterMenuItemBounds(menuBounds, index);
            var label = GetGarrisonBuilderSelectionFilterLabel(option)
                + (option == _builderSelectionFilter ? " *" : string.Empty);
            DrawBuilderMenuButton(itemBounds, label, option == _builderSelectionFilter || itemBounds.Contains(mouse.Position));
        }
    }
}
