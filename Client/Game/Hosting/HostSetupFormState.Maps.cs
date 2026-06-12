#nullable enable

using Microsoft.Xna.Framework;
using OpenGarrison.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed partial class HostSetupFormState
{
    public int AvailableMapIndex { get; set; } = -1;
    public int PlaylistMapIndex { get; set; } = -1;
    public int PlaylistHoverIndex { get; set; } = -1;
    public int AvailableMapScrollOffset { get; set; }
    public int PlaylistMapScrollOffset { get; set; }
    public GameModeKind? AvailableMapModeFilter { get; set; }
    public bool ModeFilterDropdownOpen { get; set; }
    public bool FiltersPopupOpen { get; set; }
    public string AvailableMapNameFilterBuffer { get; set; } = string.Empty;
    public int AvailableMapNameFilterCursorIndex { get; set; }
    public int AvailableMapNameFilterSelectionStart { get; set; }
    public bool IncludeCustomMaps { get; set; } = true;
    public bool IncludeBaseMaps { get; set; } = true;
    public HostSetupMapContextMenuState? MapContextMenu { get; set; }
    public string? PreviewMapLevelName { get; set; }
    public HashSet<string> FavouriteLevelNames { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public void PrepareMapsScreen()
    {
        FavouriteLevelNames = HostSetupMapFavouritesStore.Load();
        AvailableMapIndex = -1;
        PlaylistMapIndex = -1;
        AvailableMapScrollOffset = 0;
        PlaylistMapScrollOffset = 0;
        ModeFilterDropdownOpen = false;
        FiltersPopupOpen = false;
        MapContextMenu = null;
        PreviewMapLevelName = null;
        AvailableMapModeFilter = null;
        ResetAvailableMapFilters();
        SyncPlaylistSelection();
    }

    public void ResetAvailableMapFilters()
    {
        AvailableMapNameFilterBuffer = string.Empty;
        AvailableMapNameFilterCursorIndex = 0;
        AvailableMapNameFilterSelectionStart = 0;
        IncludeCustomMaps = true;
        IncludeBaseMaps = true;
        NotifyAvailableFiltersChanged();
    }

    public void NotifyAvailableFiltersChanged()
    {
        AvailableMapScrollOffset = 0;
        AvailableMapIndex = -1;
    }

    public List<OpenGarrisonMapRotationEntry> GetAvailableMapsForDisplay()
    {
        IEnumerable<OpenGarrisonMapRotationEntry> query = MapEntries.Where(entry => !entry.IsPlaylistClone);
        if (AvailableMapModeFilter is { } modeFilter)
        {
            query = query.Where(entry => entry.Mode == modeFilter);
        }

        if (!IncludeCustomMaps)
        {
            query = query.Where(entry => !entry.IsCustomMap);
        }

        if (!IncludeBaseMaps)
        {
            query = query.Where(entry => entry.IsCustomMap);
        }

        if (!string.IsNullOrWhiteSpace(AvailableMapNameFilterBuffer))
        {
            query = query.Where(entry =>
                entry.DisplayName.Contains(AvailableMapNameFilterBuffer, StringComparison.OrdinalIgnoreCase)
                || entry.LevelName.Contains(AvailableMapNameFilterBuffer, StringComparison.OrdinalIgnoreCase));
        }

        var filtered = query
            .OrderBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var favourites = filtered
            .Where(entry => FavouriteLevelNames.Contains(entry.LevelName))
            .ToList();
        var regular = filtered
            .Where(entry => !FavouriteLevelNames.Contains(entry.LevelName))
            .ToList();
        var combined = new List<OpenGarrisonMapRotationEntry>(favourites.Count + regular.Count);
        combined.AddRange(favourites);
        combined.AddRange(regular);
        return combined;
    }

    public List<OpenGarrisonMapRotationEntry> GetPlaylistMaps()
    {
        return MapEntries
            .Where(entry => entry.Order > 0)
            .OrderBy(entry => entry.Order)
            .ToList();
    }

    public bool IsMapInPlaylist(string levelName)
    {
        return MapEntries.Any(entry =>
            entry.Order > 0
            && IsMapEntryMatch(entry, levelName));
    }

    public OpenGarrisonMapRotationEntry? GetSelectedAvailableMap()
    {
        var available = GetAvailableMapsForDisplay();
        return AvailableMapIndex >= 0 && AvailableMapIndex < available.Count
            ? available[AvailableMapIndex]
            : null;
    }

    public OpenGarrisonMapRotationEntry? GetSelectedPlaylistMap()
    {
        var playlist = GetPlaylistMaps();
        return PlaylistMapIndex >= 0 && PlaylistMapIndex < playlist.Count
            ? playlist[PlaylistMapIndex]
            : null;
    }

    public void SelectAvailableMap(int index)
    {
        var mapCount = GetAvailableMapsForDisplay().Count;
        AvailableMapIndex = mapCount == 0 ? -1 : Math.Clamp(index, -1, mapCount - 1);
        if (AvailableMapIndex >= 0)
        {
            PlaylistMapIndex = -1;
        }
    }

    public void SelectPlaylistMap(int index)
    {
        var mapCount = GetPlaylistMaps().Count;
        PlaylistMapIndex = mapCount == 0 ? -1 : Math.Clamp(index, -1, mapCount - 1);
        if (PlaylistMapIndex >= 0)
        {
            AvailableMapIndex = -1;
        }
    }

    public void AddSelectedAvailableMapToPlaylist()
    {
        var selected = GetSelectedAvailableMap();
        if (selected is null)
        {
            return;
        }

        var addedEntry = AppendMapToPlaylist(selected);
        SelectPlaylistEntry(addedEntry);
    }

    public void AddMapToPlaylist(string levelName)
    {
        var entry = FindCatalogMapEntry(levelName);
        if (entry is null)
        {
            return;
        }

        var addedEntry = AppendMapToPlaylist(entry);
        SelectPlaylistEntry(addedEntry);
    }

    public void ConvertPlaylistMapTo(OpenGarrisonMapRotationEntry playlistEntry, string levelName)
    {
        if (playlistEntry.Order <= 0)
        {
            return;
        }

        var replacementSource = FindCatalogMapEntry(levelName);
        if (replacementSource is null)
        {
            return;
        }

        var desiredOrder = playlistEntry.Order;
        if (playlistEntry.IsPlaylistClone)
        {
            MapEntries.Remove(playlistEntry);
        }
        else
        {
            playlistEntry.Order = 0;
        }

        var replacement = AppendMapToPlaylist(replacementSource, desiredOrder);
        NormalizeIncludedMapOrders(MapEntries);
        SelectPlaylistEntry(replacement);
    }

    public void RemoveSelectedPlaylistMap()
    {
        var selected = GetSelectedPlaylistMap();
        if (selected is null)
        {
            return;
        }

        var removedIndex = PlaylistMapIndex;
        if (selected.IsPlaylistClone)
        {
            MapEntries.Remove(selected);
        }
        else
        {
            selected.Order = 0;
        }

        NormalizeIncludedMapOrders(MapEntries);
        var playlist = GetPlaylistMaps();
        if (playlist.Count == 0)
        {
            PlaylistMapIndex = -1;
            return;
        }

        SelectPlaylistMap(Math.Clamp(removedIndex, 0, playlist.Count - 1));
    }

    public void MoveSelectedPlaylistMap(int direction)
    {
        var playlist = GetPlaylistMaps();
        if (PlaylistMapIndex < 0 || PlaylistMapIndex >= playlist.Count)
        {
            return;
        }

        var targetIndex = PlaylistMapIndex + direction;
        if (targetIndex < 0 || targetIndex >= playlist.Count)
        {
            return;
        }

        var selected = playlist[PlaylistMapIndex];
        var swapTarget = playlist[targetIndex];
        (selected.Order, swapTarget.Order) = (swapTarget.Order, selected.Order);
        SelectPlaylistMap(targetIndex);
    }

    public void ToggleFavourite(string levelName)
    {
        if (string.IsNullOrWhiteSpace(levelName))
        {
            return;
        }

        if (!FavouriteLevelNames.Remove(levelName))
        {
            FavouriteLevelNames.Add(levelName);
        }

        HostSetupMapFavouritesStore.Save(FavouriteLevelNames);
        ClampAvailableMapScroll(GetAvailableMapsForDisplay().Count, int.MaxValue);
    }

    public bool TryImportPlaylist(string path, out string error)
    {
        error = string.Empty;
        try
        {
            var lines = HostSetupPlaylistFileIO.ReadPlaylistLines(path);
            if (lines.Count == 0)
            {
                error = "Playlist file is empty.";
                return false;
            }

            MapEntries.RemoveAll(entry => entry.IsPlaylistClone);
            foreach (var entry in MapEntries)
            {
                entry.Order = 0;
            }

            var order = 1;
            var catalog = MapEntries.Where(entry => !entry.IsPlaylistClone).ToList();
            foreach (var line in lines)
            {
                if (!HostSetupPlaylistFileIO.TryResolvePlaylistLine(line, catalog, out var resolved))
                {
                    error = $"Unknown map in playlist: {line}";
                    return false;
                }

                AppendMapToPlaylist(resolved, order);
                order += 1;
            }

            NormalizeIncludedMapOrders(MapEntries);
            SyncPlaylistSelection();
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool TryExportPlaylist(string path, out string error)
    {
        error = string.Empty;
        try
        {
            var playlist = GetPlaylistMaps();
            if (playlist.Count == 0)
            {
                error = "Playlist is empty.";
                return false;
            }

            HostSetupPlaylistFileIO.WritePlaylist(path, playlist);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public void ClampAvailableMapScroll(int mapCount, int visibleRowCount)
    {
        AvailableMapScrollOffset = Math.Clamp(
            AvailableMapScrollOffset,
            0,
            Math.Max(0, mapCount - Math.Max(1, visibleRowCount)));
        AvailableMapIndex = Math.Clamp(AvailableMapIndex, -1, Math.Max(0, mapCount - 1));
    }

    public void ClampPlaylistMapScroll(int mapCount, int visibleRowCount)
    {
        PlaylistMapScrollOffset = Math.Clamp(
            PlaylistMapScrollOffset,
            0,
            Math.Max(0, mapCount - Math.Max(1, visibleRowCount)));
        PlaylistMapIndex = Math.Clamp(PlaylistMapIndex, -1, Math.Max(0, mapCount - 1));
    }

    public void EnsureAvailableMapVisible(int visibleRowCount)
    {
        EnsureListIndexVisible(
            AvailableMapIndex,
            visibleRowCount,
            GetAvailableMapsForDisplay().Count,
            offset => AvailableMapScrollOffset = offset,
            () => AvailableMapScrollOffset);
    }

    public void EnsurePlaylistMapVisible(int visibleRowCount)
    {
        EnsureListIndexVisible(
            PlaylistMapIndex,
            visibleRowCount,
            GetPlaylistMaps().Count,
            offset => PlaylistMapScrollOffset = offset,
            () => PlaylistMapScrollOffset);
    }

    private void SyncPlaylistSelection(string? levelName = null)
    {
        var playlist = GetPlaylistMaps();
        if (playlist.Count == 0)
        {
            PlaylistMapIndex = -1;
            return;
        }

        if (!string.IsNullOrWhiteSpace(levelName))
        {
            for (var entryIndex = 0; entryIndex < playlist.Count; entryIndex += 1)
            {
                if (string.Equals(playlist[entryIndex].LevelName, levelName, StringComparison.OrdinalIgnoreCase))
                {
                    SelectPlaylistMap(entryIndex);
                    return;
                }
            }
        }

        if (PlaylistMapIndex >= 0)
        {
            PlaylistMapIndex = Math.Clamp(PlaylistMapIndex, 0, playlist.Count - 1);
            AvailableMapIndex = -1;
        }
    }

    private OpenGarrisonMapRotationEntry AppendMapToPlaylist(OpenGarrisonMapRotationEntry entry, int? requestedOrder = null)
    {
        var playlistEntry = entry.Order > 0 ? entry.Clone() : entry;
        if (!ReferenceEquals(playlistEntry, entry))
        {
            playlistEntry.IsPlaylistClone = true;
            MapEntries.Add(playlistEntry);
        }

        playlistEntry.Order = requestedOrder ?? MapEntries
            .Where(map => map.Order > 0)
            .Select(map => map.Order)
            .DefaultIfEmpty(0)
            .Max() + 1;
        return playlistEntry;
    }

    private OpenGarrisonMapRotationEntry? FindCatalogMapEntry(string levelName)
    {
        var catalog = MapEntries
            .Where(entry => !entry.IsPlaylistClone)
            .ToList();
        return HostSetupPlaylistFileIO.TryResolvePlaylistLine(levelName, catalog, out var entry)
            ? entry
            : null;
    }

    private void SelectPlaylistEntry(OpenGarrisonMapRotationEntry entry)
    {
        var playlist = GetPlaylistMaps();
        for (var index = 0; index < playlist.Count; index += 1)
        {
            if (ReferenceEquals(playlist[index], entry))
            {
                SelectPlaylistMap(index);
                return;
            }
        }

        SyncPlaylistSelection(entry.LevelName);
    }

    private static bool IsMapEntryMatch(OpenGarrisonMapRotationEntry entry, string levelName)
    {
        if (string.Equals(entry.LevelName, levelName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(entry.IniKey, levelName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(entry.DisplayName, levelName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return OpenGarrisonStockMapCatalog.TryGetDefinition(levelName, out var definition)
            && (string.Equals(entry.IniKey, definition.IniKey, StringComparison.OrdinalIgnoreCase)
                || string.Equals(entry.LevelName, definition.LevelName, StringComparison.OrdinalIgnoreCase));
    }

    private static void EnsureListIndexVisible(
        int selectedIndex,
        int visibleRowCount,
        int mapCount,
        Action<int> setOffset,
        Func<int> getOffset)
    {
        if (mapCount == 0 || selectedIndex < 0)
        {
            setOffset(0);
            return;
        }

        var capacity = Math.Max(1, visibleRowCount);
        var maxScrollOffset = Math.Max(0, mapCount - capacity);
        var offset = Math.Clamp(getOffset(), 0, maxScrollOffset);
        var clampedIndex = Math.Clamp(selectedIndex, 0, mapCount - 1);
        if (clampedIndex < offset)
        {
            offset = clampedIndex;
        }
        else if (clampedIndex >= offset + capacity)
        {
            offset = clampedIndex - capacity + 1;
        }

        setOffset(Math.Clamp(offset, 0, maxScrollOffset));
    }
    }
}

public sealed class HostSetupMapContextMenuState
{
    public required string LevelName { get; init; }

    public OpenGarrisonMapRotationEntry? Entry { get; init; }

    public required bool IsPlaylistList { get; init; }

    public bool IsFavourite { get; init; }

    public string? VipVariantLevelName { get; init; }

    public string? VipVariantLabel { get; init; }

    public required Rectangle MenuBounds { get; init; }

    public Rectangle FavouriteBounds { get; init; }

    public Rectangle VipVariantBounds { get; init; }

    public Rectangle PreviewBounds { get; init; }
}
