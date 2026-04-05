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
        private static readonly HashSet<string> AllowedMapNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "Valley",
            "Montane",
            "Harvest",
            "Conflict",
            "Eiger",
            "Gallery",
            "Waterway",
        };

        public int MapIndex { get; set; }
        public List<PracticeMapEntry> MapEntries { get; set; } = new();
        public int TickRate { get; set; } = SimulationConfig.DefaultTicksPerSecond;
        public int TimeLimitMinutes { get; set; } = 15;
        public int CapLimit { get; set; } = 5;
        public int RespawnSeconds { get; set; } = 5;
        public int EnemyBotCount { get; set; }
        public int FriendlyBotCount { get; set; }

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
            var valleyIndex = MapEntries.FindIndex(entry => string.Equals(entry.LevelName, "Valley", StringComparison.OrdinalIgnoreCase));
            return valleyIndex >= 0 ? valleyIndex : 0;
        }

        public PracticeMapEntry? GetSelectedMapEntry()
        {
            return MapIndex >= 0 && MapIndex < MapEntries.Count
                ? MapEntries[MapIndex]
                : null;
        }

        public static List<PracticeMapEntry> BuildMapEntries()
        {
            var stockDefinitions = OpenGarrisonStockMapCatalog.Definitions
                .ToDictionary(definition => definition.LevelName, definition => definition, StringComparer.OrdinalIgnoreCase);

            return SimpleLevelFactory.GetAvailableSourceLevels()
                .Where(level => AllowedMapNames.Contains(level.Name))
                .Select(level =>
                {
                    var isCustomMap = Path.GetExtension(level.RoomSourcePath).Equals(".png", StringComparison.OrdinalIgnoreCase);
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
}
