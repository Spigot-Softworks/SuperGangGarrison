#nullable enable

using OpenGarrison.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class PracticeSetupState
    {
        private static readonly int[] TickRateOptions = [30, 60, 120];
        private static readonly int[] TimeLimitOptions = [5, 10, 15, 20, 30, 45, 60];
        private static readonly int[] CapLimitOptions = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
        private static readonly int[] RespawnOptions = [0, 3, 5, 10, 15];
        private static readonly int[] BotCountOptions = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9];
        public int MapIndex { get; set; } = -1;
        public List<PracticeMapEntry> MapEntries { get; set; } = new();
        public int TickRate { get; set; } = SimulationConfig.DefaultTicksPerSecond;
        public int TimeLimitMinutes { get; set; } = 15;
        public int CapLimit { get; set; } = 5;
        public int RespawnSeconds { get; set; } = 5;
        public int EnemyBotCount { get; set; }
        public int FriendlyBotCount { get; set; }
        public bool SpecialAbilitiesEnabled { get; set; } = true;
        public bool MapBrowserOpen { get; set; }
        public int AvailableMapIndex { get; set; } = -1;
        public int AvailableMapScrollOffset { get; set; }
        public string AvailableMapNameFilterBuffer { get; set; } = string.Empty;
        public int AvailableMapNameFilterCursorIndex { get; set; }
        public int AvailableMapNameFilterSelectionStart { get; set; }
        public GameModeKind? AvailableMapModeFilter { get; set; }
        public bool ModeFilterDropdownOpen { get; set; }
        public bool FiltersPopupOpen { get; set; }
        public bool IncludeCustomMaps { get; set; } = true;
        public bool IncludeBaseMaps { get; set; } = true;
        public HashSet<string> FavouriteLevelNames { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public void Normalize()
        {
            if (MapEntries.Count == 0)
            {
                MapIndex = 0;
            }
            else if (MapIndex < 0 || MapIndex >= MapEntries.Count)
            {
                MapIndex = FindDefaultMapIndex();
            }

            TickRate = NormalizeOption(TickRate, TickRateOptions, SimulationConfig.DefaultTicksPerSecond);
            TimeLimitMinutes = NormalizeOption(TimeLimitMinutes, TimeLimitOptions, 15);
            CapLimit = NormalizeOption(CapLimit, CapLimitOptions, 5);
            RespawnSeconds = NormalizeOption(RespawnSeconds, RespawnOptions, 5);
            EnemyBotCount = NormalizeOption(EnemyBotCount, BotCountOptions, 0);
            FriendlyBotCount = NormalizeOption(FriendlyBotCount, BotCountOptions, 0);
        }

        public void CycleMap(int direction)
        {
            if (MapEntries.Count == 0)
            {
                return;
            }

            var count = MapEntries.Count;
            MapIndex = ((MapIndex + direction) % count + count) % count;
        }

        public void OpenMapBrowser()
        {
            FavouriteLevelNames = HostSetupMapFavouritesStore.Load();
            MapBrowserOpen = true;
            AvailableMapScrollOffset = 0;
            ModeFilterDropdownOpen = false;
            FiltersPopupOpen = false;
            SyncAvailableMapSelectionToSelectedMap();
        }

        public void CloseMapBrowser()
        {
            MapBrowserOpen = false;
            AvailableMapIndex = -1;
            AvailableMapScrollOffset = 0;
            ModeFilterDropdownOpen = false;
            FiltersPopupOpen = false;
        }

        public void ResetAvailableMapFilters()
        {
            AvailableMapNameFilterBuffer = string.Empty;
            AvailableMapNameFilterCursorIndex = 0;
            AvailableMapNameFilterSelectionStart = 0;
            AvailableMapModeFilter = null;
            IncludeCustomMaps = true;
            IncludeBaseMaps = true;
            NotifyAvailableMapFiltersChanged();
        }

        public void NotifyAvailableMapFiltersChanged()
        {
            AvailableMapScrollOffset = 0;
            AvailableMapIndex = -1;
        }

        public List<OpenGarrisonMapRotationEntry> GetAvailableMapsForDisplay()
        {
            IEnumerable<PracticeMapEntry> query = MapEntries;
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
            var combined = new List<PracticeMapEntry>(favourites.Count + regular.Count);
            combined.AddRange(favourites);
            combined.AddRange(regular);
            return combined.Select(ToRotationEntry).ToList();
        }

        public PracticeMapEntry? GetSelectedAvailableMapEntry()
        {
            var available = GetAvailableMapsForDisplay();
            if (AvailableMapIndex < 0 || AvailableMapIndex >= available.Count)
            {
                return null;
            }

            var levelName = available[AvailableMapIndex].LevelName;
            return MapEntries.FirstOrDefault(entry =>
                string.Equals(entry.LevelName, levelName, StringComparison.OrdinalIgnoreCase));
        }

        public void SelectAvailableMap(int index)
        {
            var mapCount = GetAvailableMapsForDisplay().Count;
            AvailableMapIndex = mapCount == 0 ? -1 : Math.Clamp(index, -1, mapCount - 1);
        }

        public bool ConfirmMapBrowserSelection()
        {
            var entry = GetSelectedAvailableMapEntry();
            if (entry is null)
            {
                return false;
            }

            return SelectMapEntry(entry.LevelName);
        }

        public void ClampAvailableMapScroll(int entryCount, int visibleRowCount)
        {
            AvailableMapScrollOffset = Math.Clamp(
                AvailableMapScrollOffset,
                0,
                Math.Max(0, entryCount - Math.Max(1, visibleRowCount)));
            AvailableMapIndex = Math.Clamp(AvailableMapIndex, -1, Math.Max(0, entryCount - 1));
        }

        public void EnsureAvailableMapSelectionVisible(int visibleRowCount)
        {
            if (AvailableMapIndex < 0)
            {
                AvailableMapScrollOffset = 0;
                return;
            }

            var entries = GetAvailableMapsForDisplay();
            if (entries.Count == 0)
            {
                AvailableMapScrollOffset = 0;
                return;
            }

            var capacity = Math.Max(1, visibleRowCount);
            var maxScrollOffset = Math.Max(0, entries.Count - capacity);
            var clampedIndex = Math.Clamp(AvailableMapIndex, 0, entries.Count - 1);
            if (clampedIndex < AvailableMapScrollOffset)
            {
                AvailableMapScrollOffset = clampedIndex;
            }
            else if (clampedIndex >= AvailableMapScrollOffset + capacity)
            {
                AvailableMapScrollOffset = clampedIndex - capacity + 1;
            }

            AvailableMapScrollOffset = Math.Clamp(AvailableMapScrollOffset, 0, maxScrollOffset);
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
        }

        public void CycleTickRate(int direction)
        {
            TickRate = CycleOption(TickRate, TickRateOptions, direction, SimulationConfig.DefaultTicksPerSecond);
        }

        public void CycleTimeLimit(int direction)
        {
            TimeLimitMinutes = CycleOption(TimeLimitMinutes, TimeLimitOptions, direction, 15);
        }

        public void CycleCapLimit(int direction)
        {
            CapLimit = CycleOption(CapLimit, CapLimitOptions, direction, 5);
        }

        public void CycleRespawn(int direction)
        {
            RespawnSeconds = CycleOption(RespawnSeconds, RespawnOptions, direction, 5);
        }

        public void CycleEnemyBots(int direction)
        {
            EnemyBotCount = CycleOption(EnemyBotCount, BotCountOptions, direction, 0);
        }

        public void CycleFriendlyBots(int direction)
        {
            FriendlyBotCount = CycleOption(FriendlyBotCount, BotCountOptions, direction, 0);
        }

        public bool SelectMapEntry(string? levelName)
        {
            if (string.IsNullOrWhiteSpace(levelName))
            {
                return false;
            }

            var index = MapEntries.FindIndex(entry => string.Equals(entry.LevelName, levelName, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                return false;
            }

            MapIndex = index;
            return true;
        }

        public int FindDefaultMapIndex()
        {
            var harvestIndex = MapEntries.FindIndex(entry => string.Equals(entry.LevelName, "Harvest", StringComparison.OrdinalIgnoreCase));
            return harvestIndex >= 0 ? harvestIndex : 0;
        }

        public PracticeMapEntry? GetSelectedMapEntry()
        {
            return MapIndex >= 0 && MapIndex < MapEntries.Count
                ? MapEntries[MapIndex]
                : null;
        }

        private void SyncAvailableMapSelectionToSelectedMap()
        {
            var selectedEntry = GetSelectedMapEntry();
            if (selectedEntry is null)
            {
                AvailableMapIndex = -1;
                return;
            }

            var entries = GetAvailableMapsForDisplay();
            AvailableMapIndex = entries
                .ToList()
                .FindIndex(entry => string.Equals(entry.LevelName, selectedEntry.LevelName, StringComparison.OrdinalIgnoreCase));
        }

        private static OpenGarrisonMapRotationEntry ToRotationEntry(PracticeMapEntry entry)
        {
            return new OpenGarrisonMapRotationEntry
            {
                IniKey = entry.IniKey,
                LevelName = entry.LevelName,
                DisplayName = entry.DisplayName,
                Mode = entry.Mode,
                IsCustomMap = entry.IsCustomMap,
            };
        }

        public static List<PracticeMapEntry> BuildMapEntries()
        {
            SimpleLevelFactory.ClearCachedCatalog();
            var stockDefinitions = OpenGarrisonStockMapCatalog.Definitions
                .ToDictionary(definition => definition.LevelName, definition => definition, StringComparer.OrdinalIgnoreCase);
            var hiddenStockLevelNames = OpenGarrisonStockMapCatalog.SourceDefinitions
                .Where(definition => !stockDefinitions.ContainsKey(definition.LevelName))
                .Select(definition => definition.LevelName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var entries = SimpleLevelFactory.GetAvailableSourceLevels()
                .Where(level => !hiddenStockLevelNames.Contains(level.Name))
                .Select(level =>
                {
                    var isCustomMap = level.IsCustomMap;
                    var hasStockDefinition = stockDefinitions.TryGetValue(level.Name, out var definition);
                    var displayName = hasStockDefinition ? definition.DisplayName : level.Name;
                    var iniKey = hasStockDefinition ? definition.IniKey : level.Name;
                    return new PracticeMapEntry(level.Name, displayName, level.Mode, isCustomMap, iniKey);
                })
                .OrderBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            AddPracticeVipMapEntries(entries);
            return entries
                .OrderBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void AddPracticeVipMapEntries(List<PracticeMapEntry> mapEntries)
        {
            foreach (var baseEntry in mapEntries
                         .Where(entry => OpenGarrisonStockMapCatalog.IsCpPrefixedIniKey(entry.IniKey) && entry.Mode != GameModeKind.Vip)
                         .ToList())
            {
                var rotationBase = new OpenGarrisonMapRotationEntry
                {
                    IniKey = baseEntry.IniKey,
                    LevelName = baseEntry.LevelName,
                    DisplayName = baseEntry.DisplayName,
                    Mode = baseEntry.Mode,
                    IsCustomMap = baseEntry.IsCustomMap,
                };
                if (!OpenGarrisonStockMapCatalog.TryCreateVipMapRotationEntry(rotationBase, out var vipEntry))
                {
                    continue;
                }

                if (mapEntries.Any(entry =>
                        string.Equals(entry.IniKey, vipEntry.IniKey, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(entry.LevelName, vipEntry.LevelName, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                mapEntries.Add(new PracticeMapEntry(
                    vipEntry.LevelName,
                    vipEntry.DisplayName,
                    GameModeKind.Vip,
                    vipEntry.IsCustomMap,
                    vipEntry.IniKey));
            }
        }

        private static int NormalizeOption(int currentValue, int[] options, int fallback)
        {
            return options.Contains(currentValue) ? currentValue : fallback;
        }

        private static int CycleOption(int currentValue, int[] options, int direction, int fallback)
        {
            var normalized = NormalizeOption(currentValue, options, fallback);
            var currentIndex = 0;
            for (var index = 0; index < options.Length; index += 1)
            {
                if (options[index] == normalized)
                {
                    currentIndex = index;
                    break;
                }
            }

            var nextIndex = (currentIndex + direction) % options.Length;
            if (nextIndex < 0)
            {
                nextIndex += options.Length;
            }

            return options[nextIndex];
        }
    }

    private int _practiceMapIndex
    {
        get => _practiceSetupState.MapIndex;
        set => _practiceSetupState.MapIndex = value;
    }

    private List<PracticeMapEntry> _practiceMapEntries
    {
        get => _practiceSetupState.MapEntries;
        set => _practiceSetupState.MapEntries = value ?? new List<PracticeMapEntry>();
    }

    private int _practiceTickRate
    {
        get => _practiceSetupState.TickRate;
        set => _practiceSetupState.TickRate = value;
    }

    private int _practiceTimeLimitMinutes
    {
        get => _practiceSetupState.TimeLimitMinutes;
        set => _practiceSetupState.TimeLimitMinutes = value;
    }

    private int _practiceCapLimit
    {
        get => _practiceSetupState.CapLimit;
        set => _practiceSetupState.CapLimit = value;
    }

    private int _practiceRespawnSeconds
    {
        get => _practiceSetupState.RespawnSeconds;
        set => _practiceSetupState.RespawnSeconds = value;
    }

    private int _practiceEnemyBotCount
    {
        get => _practiceSetupState.EnemyBotCount;
        set => _practiceSetupState.EnemyBotCount = value;
    }

    private int _practiceFriendlyBotCount
    {
        get => _practiceSetupState.FriendlyBotCount;
        set => _practiceSetupState.FriendlyBotCount = value;
    }

    private void NormalizePracticeSetupState()
    {
        _practiceSetupState.Normalize();
    }

    private static List<PracticeMapEntry> BuildPracticeMapEntries()
    {
        return PracticeSetupState.BuildMapEntries();
    }

    private void CyclePracticeMap(int direction)
    {
        if (_practiceMapEntries.Count == 0)
        {
            _menuStatusMessage = "No local maps are available for Practice.";
            return;
        }

        _practiceSetupState.CycleMap(direction);
        _menuStatusMessage = string.Empty;
    }

    private void CyclePracticeTickRate(int direction)
    {
        _practiceSetupState.CycleTickRate(direction);
        _menuStatusMessage = string.Empty;
    }

    private void CyclePracticeTimeLimit(int direction)
    {
        _practiceSetupState.CycleTimeLimit(direction);
        _menuStatusMessage = string.Empty;
    }

    private void CyclePracticeCapLimit(int direction)
    {
        _practiceSetupState.CycleCapLimit(direction);
        _menuStatusMessage = string.Empty;
    }

    private void CyclePracticeRespawn(int direction)
    {
        _practiceSetupState.CycleRespawn(direction);
        _menuStatusMessage = string.Empty;
    }

    private void CyclePracticeEnemyBots(int direction)
    {
        _practiceSetupState.CycleEnemyBots(direction);
        _menuStatusMessage = string.Empty;
    }

    private void CyclePracticeFriendlyBots(int direction)
    {
        _practiceSetupState.CycleFriendlyBots(direction);
        _menuStatusMessage = string.Empty;
    }

    private bool SelectPracticeMapEntry(string? levelName)
    {
        return _practiceSetupState.SelectMapEntry(levelName);
    }

    private int FindDefaultPracticeMapIndex()
    {
        return _practiceSetupState.FindDefaultMapIndex();
    }

    private PracticeMapEntry? GetSelectedPracticeMapEntry()
    {
        return _practiceSetupState.GetSelectedMapEntry();
    }

    private static string GetPracticeBotCountLabel(int count)
    {
        return count <= 0 ? "Off" : count.ToString(CultureInfo.InvariantCulture);
    }

    private bool _practiceSpecialAbilitiesEnabled
    {
        get => _practiceSetupState.SpecialAbilitiesEnabled;
        set => _practiceSetupState.SpecialAbilitiesEnabled = value;
    }

    private void CyclePracticeSpecialAbilities(int direction)
    {
        if (direction == 0)
        {
            return;
        }

        _practiceSetupState.SpecialAbilitiesEnabled = direction > 0;
        ApplyPracticeExperimentalGameplaySettings();
    }
}
