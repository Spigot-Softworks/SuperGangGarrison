#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace OpenGarrison.Client;

public partial class Game1
{
    internal static class ScrollbarOwners
    {
        public static readonly object ControlsMenu = new();
        public static readonly object OptionsMenu = new();
        public static readonly object PluginOptionsMenu = new();
        public static readonly object ClientPowersMenu = new();
        public static readonly object HostSetupContent = new();
        public static readonly object HostSetupOptions = new();
        public static readonly object HostSetupAvailableMaps = new();
        public static readonly object HostSetupPlaylistMaps = new();
        public static readonly object PracticeAvailableMaps = new();
        public static readonly object ChatHud = new();
        public static readonly object GarrisonBuilderActions = new();
        public static readonly object GarrisonBuilderEntityPalette = new();
        public static readonly object GarrisonBuilderEntityRefListDropdown = new();
    }

    internal readonly ScrollbarDragController ScrollbarDrag = new();

    internal bool TryHandleScrollbarDrag(
        MouseState mouse,
        MouseState previousMouse,
        object owner,
        Rectangle trackBounds,
        ref int scrollOffset,
        int itemCount,
        int visibleItemCount,
        int minThumbHeight = 24)
    {
        ScrollbarDrag.NotifyMouseReleased(mouse, previousMouse);
        var metrics = ScrollbarLayout.Compute(
            trackBounds,
            scrollOffset,
            itemCount,
            visibleItemCount,
            minThumbHeight);
        return ScrollbarDrag.Handle(mouse, previousMouse, owner, metrics, ref scrollOffset);
    }

    internal bool TryHandleScrollbarRangeDrag(
        MouseState mouse,
        MouseState previousMouse,
        object owner,
        Rectangle trackBounds,
        ref int scrollOffset,
        int maxScrollOffset,
        int viewportSize,
        int contentSize,
        int minThumbHeight = 24)
    {
        ScrollbarDrag.NotifyMouseReleased(mouse, previousMouse);
        var metrics = ScrollbarLayout.ComputeForRange(
            trackBounds,
            scrollOffset,
            maxScrollOffset,
            viewportSize,
            contentSize,
            minThumbHeight);
        return ScrollbarDrag.Handle(mouse, previousMouse, owner, metrics, ref scrollOffset);
    }

    internal bool TryHandleInvertedScrollbarDrag(
        MouseState mouse,
        MouseState previousMouse,
        object owner,
        Rectangle trackBounds,
        ref int scrollOffset,
        int itemCount,
        int visibleItemCount,
        int minThumbHeight = 8)
    {
        ScrollbarDrag.NotifyMouseReleased(mouse, previousMouse);
        var metrics = ScrollbarLayout.Compute(
            trackBounds,
            scrollOffset,
            itemCount,
            visibleItemCount,
            minThumbHeight,
            invertVertical: true);
        return ScrollbarDrag.HandleInverted(mouse, previousMouse, owner, metrics, ref scrollOffset);
    }

    internal static Rectangle GetHostSetupMapListScrollbarTrackBounds(Rectangle listRowsBounds, int scrollbarWidth)
    {
        return new Rectangle(listRowsBounds.Right + 4, listRowsBounds.Y, scrollbarWidth, listRowsBounds.Height);
    }
}
