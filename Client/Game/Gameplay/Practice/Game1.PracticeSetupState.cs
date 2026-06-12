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
        public bool VipRulesEnabled { get; set; }
        public bool MapBrowserOpen { get; set; }
        public int MapBrowserIndex { get; set; } = -1;
        public int MapBrowserScrollOffset { get; set; }
        public string MapBrowserNameFilterBuffer { get; set; } = string.Empty;
        public int MapBrowserNameFilterCursorIndex { get; set; }
        public int MapBrowserNameFilterSelectionStart { get; set; }
        public GameModeKind? MapBrowserModeFilter { get; set; }
        public bool MapBrowserIncludeCustomMaps { get; set; } = true;
        public bool MapBrowserIncludeBaseMaps { get; set; } = true;

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
            MapBrowserOpen = true;
            MapBrowserScrollOffset = 0;
            SyncMapBrowserSelectionToSelectedMap();
        }

        public void CloseMapBrowser()
        {
            MapBrowserOpen = false;
            MapBrowserIndex = -1;
            MapBrowserScrollOffset = 0;
        }

        public void ResetMapBrowserFilters()
        {
            MapBrowserNameFilterBuffer = string.Empty;
            MapBrowserNameFilterCursorIndex = 0;
            MapBrowserNameFilterSelectionStart = 0;
            MapBrowserModeFilter = null;
            MapBrowserIncludeCustomMaps = true;
            MapBrowserIncludeBaseMaps = true;
            NotifyMapBrowserFiltersChanged();
        }

        public void NotifyMapBrowserFiltersChanged()
        {
            MapBrowserScrollOffset = 0;
            MapBrowserIndex = -1;
        }

        public List<PracticeMapEntry> GetMapBrowserEntriesForDisplay()
        {
            IEnumerable<PracticeMapEntry> query = MapEntries;
            if (MapBrowserModeFilter is { } modeFilter)
            {
                query = query.Where(entry => entry.Mode == modeFilter);
            }

            if (!MapBrowserIncludeCustomMaps)
            {
                query = query.Where(entry => !entry.IsCustomMap);
            }

            if (!MapBrowserIncludeBaseMaps)
            {
                query = query.Where(entry => entry.IsCustomMap);
            }

            if (!string.IsNullOrWhiteSpace(MapBrowserNameFilterBuffer))
            {
                query = query.Where(entry =>
                    entry.DisplayName.Contains(MapBrowserNameFilterBuffer, StringComparison.OrdinalIgnoreCase)
                    || entry.LevelName.Contains(MapBrowserNameFilterBuffer, StringComparison.OrdinalIgnoreCase));
            }

            return query
                .OrderBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public PracticeMapEntry? GetSelectedMapBrowserEntry()
        {
            var entries = GetMapBrowserEntriesForDisplay();
            return MapBrowserIndex >= 0 && MapBrowserIndex < entries.Count
                ? entries[MapBrowserIndex]
                : null;
        }

        public void SelectMapBrowserIndex(int index)
        {
            var entries = GetMapBrowserEntriesForDisplay();
            MapBrowserIndex = entries.Count == 0 ? -1 : Math.Clamp(index, -1, entries.Count - 1);
        }

        public bool ConfirmMapBrowserSelection()
        {
            var entry = GetSelectedMapBrowserEntry();
            if (entry is null)
            {
                return false;
            }

            return SelectMapEntry(entry.LevelName);
        }

        public void ClampMapBrowserScroll(int entryCount, int visibleRowCount)
        {
            MapBrowserScrollOffset = Math.Clamp(
                MapBrowserScrollOffset,
                0,
                Math.Max(0, entryCount - Math.Max(1, visibleRowCount)));
            MapBrowserIndex = Math.Clamp(MapBrowserIndex, -1, Math.Max(0, entryCount - 1));
        }

        public void EnsureMapBrowserSelectionVisible(int visibleRowCount)
        {
            if (MapBrowserIndex < 0)
            {
                MapBrowserScrollOffset = 0;
                return;
            }

            var entries = GetMapBrowserEntriesForDisplay();
            if (entries.Count == 0)
            {
                MapBrowserScrollOffset = 0;
                return;
            }

            var capacity = Math.Max(1, visibleRowCount);
            var maxScrollOffset = Math.Max(0, entries.Count - capacity);
            var clampedIndex = Math.Clamp(MapBrowserIndex, 0, entries.Count - 1);
            if (clampedIndex < MapBrowserScrollOffset)
            {
                MapBrowserScrollOffset = clampedIndex;
            }
            else if (clampedIndex >= MapBrowserScrollOffset + capacity)
            {
                MapBrowserScrollOffset = clampedIndex - capacity + 1;
            }

            MapBrowserScrollOffset = Math.Clamp(MapBrowserScrollOffset, 0, maxScrollOffset);
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

        private void SyncMapBrowserSelectionToSelectedMap()
        {
            var selectedEntry = GetSelectedMapEntry();
            if (selectedEntry is null)
            {
                MapBrowserIndex = -1;
                return;
            }

            var entries = GetMapBrowserEntriesForDisplay();
            MapBrowserIndex = entries
                .ToList()
                .FindIndex(entry => string.Equals(entry.LevelName, selectedEntry.LevelName, StringComparison.OrdinalIgnoreCase));
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

            return SimpleLevelFactory.GetAvailableSourceLevels()
                .Where(level => !hiddenStockLevelNames.Contains(level.Name))
                .Select(level =>
                {
                    var isCustomMap = level.IsCustomMap;
                    var displayName = stockDefinitions.TryGetValue(level.Name, out var definition)
                        ? definition.DisplayName
                        : level.Name;
                    return new PracticeMapEntry(level.Name, displayName, level.Mode, isCustomMap);
                })
                .OrderBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
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
        DisablePracticeVipRulesIfUnavailable();
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
        var selected = _practiceSetupState.SelectMapEntry(levelName);
        if (selected)
        {
            DisablePracticeVipRulesIfUnavailable();
        }

        return selected;
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

    private bool _practiceVipRulesEnabled
    {
        get => _practiceSetupState.VipRulesEnabled;
        set => _practiceSetupState.VipRulesEnabled = value;
    }

    private void TogglePracticeSpecialAbilities()
    {
        _practiceSetupState.SpecialAbilitiesEnabled = !_practiceSetupState.SpecialAbilitiesEnabled;
        ApplyPracticeExperimentalGameplaySettings();
    }

    private void TogglePracticeVipRules()
    {
        var selectedMap = GetSelectedPracticeMapEntry();
        if (selectedMap is not null && selectedMap.Mode != GameModeKind.ControlPoint)
        {
            _practiceSetupState.VipRulesEnabled = false;
            _menuStatusMessage = "VIP rules are only available on CP maps.";
            return;
        }

        _practiceSetupState.VipRulesEnabled = !_practiceSetupState.VipRulesEnabled;
        _menuStatusMessage = string.Empty;
    }

    private void DisablePracticeVipRulesIfUnavailable()
    {
        if (GetSelectedPracticeMapEntry()?.Mode != GameModeKind.ControlPoint)
        {
            _practiceSetupState.VipRulesEnabled = false;
        }
    }
}
