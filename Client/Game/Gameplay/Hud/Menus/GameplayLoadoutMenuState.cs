#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Client;

internal static class GameplayLoadoutMenuState
{
    public static PlayerClass GetSafeViewedClass(PlayerClass viewedClass, PlayerClass localPlayerClassId)
    {
        if (GameplayLoadoutSelectionResolver.GetOrderedLoadouts(viewedClass).Count > 0)
        {
            return viewedClass;
        }

        return localPlayerClassId;
    }

    public static PlayerClass GetShiftedViewedClass(PlayerClass viewedClass, PlayerClass localPlayerClassId, int delta)
    {
        var currentIndex = Array.IndexOf(
            GameplayLoadoutMenuPresentation.ClassStripOrder,
            GetSafeViewedClass(viewedClass, localPlayerClassId));
        if (currentIndex < 0)
        {
            return localPlayerClassId;
        }

        var nextIndex = (currentIndex + delta + GameplayLoadoutMenuPresentation.ClassStripOrder.Length)
            % GameplayLoadoutMenuPresentation.ClassStripOrder.Length;
        return GameplayLoadoutMenuPresentation.ClassStripOrder[nextIndex];
    }

    public static GameplayLoadoutMenuEntry ResolveViewedLoadoutEntry(
        IReadOnlyList<GameplayLoadoutMenuEntry> entries,
        string viewedLoadoutId)
    {
        if (entries.Count == 0)
        {
            throw new InvalidOperationException("Viewed class must have at least one loadout.");
        }

        for (var index = 0; index < entries.Count; index += 1)
        {
            if (string.Equals(entries[index].Loadout.Id, viewedLoadoutId, StringComparison.Ordinal))
            {
                return entries[index];
            }
        }

        return entries[0];
    }

    public static string ResolveViewedLoadoutId(
        PlayerClass classId,
        PlayerClass localPlayerClassId,
        string localLoadoutId,
        string? viewedLoadoutId,
        IReadOnlyList<GameplayLoadoutMenuEntry> entries)
    {
        if (entries.Count == 0)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(viewedLoadoutId)
            && entries.Any(entry => string.Equals(entry.Loadout.Id, viewedLoadoutId, StringComparison.Ordinal)))
        {
            return viewedLoadoutId;
        }

        if (classId == localPlayerClassId
            && entries.Any(entry => string.Equals(entry.Loadout.Id, localLoadoutId, StringComparison.Ordinal)))
        {
            return localLoadoutId;
        }

        return entries[0].Loadout.Id;
    }

    public static int GetHoveredButtonIndex(MouseState mouse, IReadOnlyList<GameplayLoadoutMenuButton> buttons)
    {
        var mousePoint = new Point(mouse.X, mouse.Y);
        for (var index = 0; index < buttons.Count; index += 1)
        {
            if (buttons[index].Bounds.Contains(mousePoint))
            {
                return index;
            }
        }

        return -1;
    }
}

internal enum GameplayLoadoutMenuButtonKind
{
    Class,
    ItemOption,
    Back,
}

internal readonly record struct GameplayLoadoutMenuButton(
    Rectangle Bounds,
    Action Activate,
    GameplayLoadoutMenuButtonKind Kind,
    PlayerClass? ClassId = null,
    GameplayEquipmentSlot? Slot = null,
    string? ItemId = null);
