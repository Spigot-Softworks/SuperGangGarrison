#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private readonly record struct HostSetupLaunchRequest(
        string ServerName,
        int Port,
        int MaxPlayers,
        string Password,
        string RconPassword,
        int TimeLimitMinutes,
        int CapLimit,
        int RespawnSeconds,
        bool LobbyAnnounce,
        bool AutoBalance,
        bool SecondaryAbilitiesEnabled,
        string? RequestedMap,
        string? MapRotationFile);

    private sealed class HostSetupFormState
    {
        public int HoverIndex { get; set; } = -1;
        public int MapIndex { get; set; }
        public int MapScrollOffset { get; set; }
        public int ContentScrollOffset { get; set; }
        public HostSetupEditField EditField { get; set; }
        public HostSetupTab Tab { get; set; }
        public string ServerNameBuffer { get; set; } = "My Server";
        public int ServerNameCursorIndex { get; set; }
        public int ServerNameSelectionStart { get; set; }
        public string PortBuffer { get; set; } = "8190";
        public int PortCursorIndex { get; set; }
        public int PortSelectionStart { get; set; }
        public string SlotsBuffer { get; set; } = "10";
        public int SlotsCursorIndex { get; set; }
        public int SlotsSelectionStart { get; set; }
        public string PasswordBuffer { get; set; } = string.Empty;
        public int PasswordCursorIndex { get; set; }
        public int PasswordSelectionStart { get; set; }
        public string RconPasswordBuffer { get; set; } = string.Empty;
        public int RconPasswordCursorIndex { get; set; }
        public int RconPasswordSelectionStart { get; set; }
        public string MapRotationFileBuffer { get; set; } = string.Empty;
        public int MapRotationFileCursorIndex { get; set; }
        public int MapRotationFileSelectionStart { get; set; }
        public string TimeLimitBuffer { get; set; } = "15";
        public int TimeLimitCursorIndex { get; set; }
        public int TimeLimitSelectionStart { get; set; }
        public string CapLimitBuffer { get; set; } = "5";
        public int CapLimitCursorIndex { get; set; }
        public int CapLimitSelectionStart { get; set; }
        public string RespawnSecondsBuffer { get; set; } = "5";
        public int RespawnSecondsCursorIndex { get; set; }
        public int RespawnSecondsSelectionStart { get; set; }
        public bool LobbyAnnounceEnabled { get; set; } = true;
        public bool AutoBalanceEnabled { get; set; } = true;
        public bool SecondaryAbilitiesEnabled { get; set; } = true;
        public List<OpenGarrisonMapRotationEntry> MapEntries { get; set; } = new();

        public void InitializeFieldCursorStates()
        {
            ServerNameCursorIndex = ServerNameBuffer.Length;
            ServerNameSelectionStart = ServerNameCursorIndex;
            PortCursorIndex = PortBuffer.Length;
            PortSelectionStart = PortCursorIndex;
            SlotsCursorIndex = SlotsBuffer.Length;
            SlotsSelectionStart = SlotsCursorIndex;
            PasswordCursorIndex = PasswordBuffer.Length;
            PasswordSelectionStart = PasswordCursorIndex;
            RconPasswordCursorIndex = RconPasswordBuffer.Length;
            RconPasswordSelectionStart = RconPasswordCursorIndex;
            MapRotationFileCursorIndex = MapRotationFileBuffer.Length;
            MapRotationFileSelectionStart = MapRotationFileCursorIndex;
            TimeLimitCursorIndex = TimeLimitBuffer.Length;
            TimeLimitSelectionStart = TimeLimitCursorIndex;
            CapLimitCursorIndex = CapLimitBuffer.Length;
            CapLimitSelectionStart = CapLimitCursorIndex;
            RespawnSecondsCursorIndex = RespawnSecondsBuffer.Length;
            RespawnSecondsSelectionStart = RespawnSecondsCursorIndex;
        }

        public void LoadFrom(OpenGarrisonHostSettings hostDefaults)
        {
            ArgumentNullException.ThrowIfNull(hostDefaults);

            MapScrollOffset = 0;
            ContentScrollOffset = 0;
            ServerNameBuffer = SanitizeServerName(hostDefaults.ServerName);
            PortBuffer = SanitizePort(hostDefaults.Port);
            SlotsBuffer = Math.Clamp(hostDefaults.Slots, 1, SimulationWorld.MaxPlayableNetworkPlayers)
                .ToString(CultureInfo.InvariantCulture);
            PasswordBuffer = hostDefaults.Password ?? string.Empty;
            RconPasswordBuffer = hostDefaults.RconPassword ?? string.Empty;
            MapRotationFileBuffer = hostDefaults.MapRotationFile ?? string.Empty;
            TimeLimitBuffer = Math.Clamp(hostDefaults.TimeLimitMinutes, 1, 255).ToString(CultureInfo.InvariantCulture);
            CapLimitBuffer = Math.Clamp(hostDefaults.CapLimit, 1, 255).ToString(CultureInfo.InvariantCulture);
            RespawnSecondsBuffer = Math.Clamp(hostDefaults.RespawnSeconds, 0, 255).ToString(CultureInfo.InvariantCulture);
            LobbyAnnounceEnabled = hostDefaults.LobbyAnnounceEnabled;
            AutoBalanceEnabled = hostDefaults.AutoBalanceEnabled;
            SecondaryAbilitiesEnabled = hostDefaults.SecondaryAbilitiesEnabled;
            MapEntries = BuildMapEntries(hostDefaults);
            if (MapEntries.Count == 0)
            {
                MapIndex = 0;
                InitializeFieldCursorStates();
                return;
            }

            var configuredStartMapName = hostDefaults.GetFirstIncludedMapLevelName();
            if (!SelectMapEntry(configuredStartMapName))
            {
                MapIndex = FindDefaultMapIndex();
            }

            InitializeFieldCursorStates();
        }

        public void PrepareForOpen(OpenGarrisonHostSettings hostDefaults)
        {
            ArgumentNullException.ThrowIfNull(hostDefaults);

            HoverIndex = -1;
            MapScrollOffset = 0;
            ContentScrollOffset = 0;
            Tab = HostSetupTab.Settings;
            EditField = HostSetupEditField.ServerName;

            if (string.IsNullOrWhiteSpace(ServerNameBuffer))
            {
                ServerNameBuffer = "My Server";
            }

            if (string.IsNullOrWhiteSpace(PortBuffer))
            {
                PortBuffer = "8190";
            }

            if (string.IsNullOrWhiteSpace(SlotsBuffer))
            {
                SlotsBuffer = "10";
            }

            if (string.IsNullOrWhiteSpace(TimeLimitBuffer))
            {
                TimeLimitBuffer = "15";
            }

            if (string.IsNullOrWhiteSpace(CapLimitBuffer))
            {
                CapLimitBuffer = "5";
            }

            if (string.IsNullOrWhiteSpace(RespawnSecondsBuffer))
            {
                RespawnSecondsBuffer = "5";
            }

            InitializeFieldCursorStates();

            MapEntries = BuildMapEntries(hostDefaults);
            if (MapEntries.Count == 0)
            {
                MapIndex = 0;
                return;
            }

            var configuredStartMapName = hostDefaults.GetFirstIncludedMapLevelName();
            if (!SelectMapEntry(configuredStartMapName))
            {
                MapIndex = FindDefaultMapIndex();
            }
        }

        public void ApplyTo(ClientSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            settings.HostDefaults.ServerName = SanitizeServerName(ServerNameBuffer);
            settings.HostDefaults.Port = ParsePortOrDefault(PortBuffer, 8190);
            settings.HostDefaults.Slots = ParseClampedInt(SlotsBuffer, 10, 1, SimulationWorld.MaxPlayableNetworkPlayers);
            settings.HostDefaults.Password = PasswordBuffer.Trim();
            settings.HostDefaults.RconPassword = RconPasswordBuffer.Trim();
            settings.HostDefaults.MapRotationFile = MapRotationFileBuffer.Trim();
            settings.HostDefaults.TimeLimitMinutes = ParseClampedInt(TimeLimitBuffer, 15, 1, 255);
            settings.HostDefaults.CapLimit = ParseClampedInt(CapLimitBuffer, 5, 1, 255);
            settings.HostDefaults.RespawnSeconds = ParseClampedInt(RespawnSecondsBuffer, 5, 0, 255);
            settings.HostDefaults.LobbyAnnounceEnabled = LobbyAnnounceEnabled;
            settings.HostDefaults.AutoBalanceEnabled = AutoBalanceEnabled;
            settings.HostDefaults.SecondaryAbilitiesEnabled = SecondaryAbilitiesEnabled;
            if (MapEntries.Count > 0)
            {
                settings.HostDefaults.StockMapRotation = MapEntries
                    .Select(entry => entry.Clone())
                    .ToList();
            }
        }

        public bool TryBuildLaunchRequest(out HostSetupLaunchRequest request, out string error)
        {
            request = default;
            error = string.Empty;

            var trimmedRotationFile = MapRotationFileBuffer.Trim();
            string? requestedMap = null;
            if (MapEntries.Count == 0)
            {
                error = "No stock maps are available.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(trimmedRotationFile)
                && !MapEntries.Any(entry => entry.Order > 0))
            {
                error = "Include at least one stock map or set a custom rotation file.";
                return false;
            }

            var serverName = ServerNameBuffer.Trim();
            if (string.IsNullOrWhiteSpace(serverName))
            {
                error = "Server name is required.";
                return false;
            }

            if (!int.TryParse(PortBuffer.Trim(), out var port) || port is <= 0 or > 65535)
            {
                error = "Port must be 1-65535.";
                return false;
            }

            if (!int.TryParse(SlotsBuffer.Trim(), out var maxPlayers)
                || maxPlayers < 1
                || maxPlayers > SimulationWorld.MaxPlayableNetworkPlayers)
            {
                error = $"Slots must be 1-{SimulationWorld.MaxPlayableNetworkPlayers}.";
                return false;
            }

            if (!int.TryParse(TimeLimitBuffer.Trim(), out var timeLimitMinutes)
                || timeLimitMinutes < 1
                || timeLimitMinutes > 255)
            {
                error = "Time limit must be 1-255 minutes.";
                return false;
            }

            if (!int.TryParse(CapLimitBuffer.Trim(), out var capLimit)
                || capLimit < 1
                || capLimit > 255)
            {
                error = "Cap limit must be 1-255.";
                return false;
            }

            if (!int.TryParse(RespawnSecondsBuffer.Trim(), out var respawnSeconds)
                || respawnSeconds < 0
                || respawnSeconds > 255)
            {
                error = "Respawn time must be 0-255 seconds.";
                return false;
            }

            var selectedMap = GetSelectedMapEntry();
            if (selectedMap is not null && selectedMap.Order > 0)
            {
                requestedMap = selectedMap.LevelName;
            }

            if (string.IsNullOrWhiteSpace(requestedMap))
            {
                var includedMaps = OpenGarrisonStockMapCatalog.GetOrderedIncludedMapLevelNames(MapEntries);
                requestedMap = includedMaps.Count > 0 ? includedMaps[0] : null;
            }

            request = new HostSetupLaunchRequest(
                serverName,
                port,
                maxPlayers,
                PasswordBuffer.Trim(),
                RconPasswordBuffer.Trim(),
                timeLimitMinutes,
                capLimit,
                respawnSeconds,
                LobbyAnnounceEnabled,
                AutoBalanceEnabled,
                SecondaryAbilitiesEnabled,
                requestedMap,
                string.IsNullOrWhiteSpace(trimmedRotationFile) ? null : trimmedRotationFile);
            return true;
        }

        public void BackspaceActiveField()
        {
            switch (EditField)
            {
                case HostSetupEditField.ServerName:
                    if (ServerNameBuffer.Length > 0)
                    {
                        ServerNameBuffer = ServerNameBuffer[..^1];
                    }
                    break;
                case HostSetupEditField.Port:
                    if (PortBuffer.Length > 0)
                    {
                        PortBuffer = PortBuffer[..^1];
                    }
                    break;
                case HostSetupEditField.Slots:
                    if (SlotsBuffer.Length > 0)
                    {
                        SlotsBuffer = SlotsBuffer[..^1];
                    }
                    break;
                case HostSetupEditField.Password:
                    if (PasswordBuffer.Length > 0)
                    {
                        PasswordBuffer = PasswordBuffer[..^1];
                    }
                    break;
                case HostSetupEditField.RconPassword:
                    if (RconPasswordBuffer.Length > 0)
                    {
                        RconPasswordBuffer = RconPasswordBuffer[..^1];
                    }
                    break;
                case HostSetupEditField.MapRotationFile:
                    if (MapRotationFileBuffer.Length > 0)
                    {
                        MapRotationFileBuffer = MapRotationFileBuffer[..^1];
                    }
                    break;
                case HostSetupEditField.TimeLimit:
                    if (TimeLimitBuffer.Length > 0)
                    {
                        TimeLimitBuffer = TimeLimitBuffer[..^1];
                    }
                    break;
                case HostSetupEditField.CapLimit:
                    if (CapLimitBuffer.Length > 0)
                    {
                        CapLimitBuffer = CapLimitBuffer[..^1];
                    }
                    break;
                case HostSetupEditField.RespawnSeconds:
                    if (RespawnSecondsBuffer.Length > 0)
                    {
                        RespawnSecondsBuffer = RespawnSecondsBuffer[..^1];
                    }
                    break;
            }
        }

        public void AppendCharacterToActiveField(char character)
        {
            if (char.IsControl(character))
            {
                return;
            }

            if (EditField == HostSetupEditField.None)
            {
                EditField = HostSetupEditField.ServerName;
            }

            if (EditField == HostSetupEditField.ServerName)
            {
                if (ServerNameBuffer.Length < 32)
                {
                    ServerNameBuffer += character;
                }

                return;
            }

            if (EditField == HostSetupEditField.Password)
            {
                if (PasswordBuffer.Length < 32)
                {
                    PasswordBuffer += character;
                }

                return;
            }

            if (EditField == HostSetupEditField.RconPassword)
            {
                if (RconPasswordBuffer.Length < 64)
                {
                    RconPasswordBuffer += character;
                }

                return;
            }

            if (EditField == HostSetupEditField.MapRotationFile)
            {
                if (MapRotationFileBuffer.Length < 180)
                {
                    MapRotationFileBuffer += character;
                }

                return;
            }

            if (!char.IsDigit(character))
            {
                return;
            }

            switch (EditField)
            {
                case HostSetupEditField.Port when PortBuffer.Length < 5:
                    PortBuffer += character;
                    break;
                case HostSetupEditField.Slots when SlotsBuffer.Length < 2:
                    SlotsBuffer += character;
                    break;
                case HostSetupEditField.TimeLimit when TimeLimitBuffer.Length < 3:
                    TimeLimitBuffer += character;
                    break;
                case HostSetupEditField.CapLimit when CapLimitBuffer.Length < 3:
                    CapLimitBuffer += character;
                    break;
                case HostSetupEditField.RespawnSeconds when RespawnSecondsBuffer.Length < 3:
                    RespawnSecondsBuffer += character;
                    break;
            }
        }

        public void CycleField()
        {
            EditField = EditField switch
            {
                HostSetupEditField.ServerName => HostSetupEditField.Port,
                HostSetupEditField.Port => HostSetupEditField.Slots,
                HostSetupEditField.Slots => HostSetupEditField.Password,
                HostSetupEditField.Password => HostSetupEditField.RconPassword,
                HostSetupEditField.RconPassword => HostSetupEditField.MapRotationFile,
                HostSetupEditField.MapRotationFile => HostSetupEditField.TimeLimit,
                HostSetupEditField.TimeLimit => HostSetupEditField.CapLimit,
                HostSetupEditField.CapLimit => HostSetupEditField.RespawnSeconds,
                HostSetupEditField.RespawnSeconds => HostSetupEditField.ServerName,
                _ => HostSetupEditField.ServerName,
            };
        }

        public void ToggleSelectedMap()
        {
            var selected = GetSelectedMapEntry();
            if (selected is null)
            {
                return;
            }

            if (selected.Order > 0)
            {
                selected.Order = 0;
            }
            else
            {
                selected.Order = MapEntries
                    .Where(entry => entry.Order > 0)
                    .Select(entry => entry.Order)
                    .DefaultIfEmpty()
                    .Max() + 1;
            }

            SortMapEntries(selected.LevelName);
        }

        public void MoveSelectedMap(int direction)
        {
            var selected = GetSelectedMapEntry();
            if (selected is null || selected.Order <= 0)
            {
                return;
            }

            var includedEntries = MapEntries
                .Where(entry => entry.Order > 0)
                .OrderBy(entry => entry.Order)
                .ToList();
            var currentIndex = includedEntries.FindIndex(entry =>
                string.Equals(entry.LevelName, selected.LevelName, StringComparison.OrdinalIgnoreCase));
            var targetIndex = currentIndex + direction;
            if (currentIndex < 0 || targetIndex < 0 || targetIndex >= includedEntries.Count)
            {
                return;
            }

            var swapTarget = includedEntries[targetIndex];
            (selected.Order, swapTarget.Order) = (swapTarget.Order, selected.Order);
            SortMapEntries(selected.LevelName);
        }

        public void SortMapEntries(string? selectedLevelName = null)
        {
            var desiredSelection = selectedLevelName ?? GetSelectedMapEntry()?.LevelName;
            NormalizeIncludedMapOrders(MapEntries);
            MapEntries = OpenGarrisonStockMapCatalog.GetOrderedEntries(MapEntries)
                .Select(entry => entry.Clone())
                .ToList();
            if (!SelectMapEntry(desiredSelection))
            {
                MapIndex = Math.Clamp(MapIndex, 0, Math.Max(0, MapEntries.Count - 1));
            }
        }

        public bool SelectMapEntry(string? levelName)
        {
            if (string.IsNullOrWhiteSpace(levelName))
            {
                return false;
            }

            var index = MapEntries.FindIndex(entry =>
                entry.LevelName.Equals(levelName, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                return false;
            }

            MapIndex = index;
            return true;
        }

        public int FindDefaultMapIndex()
        {
            var truefortIndex = MapEntries.FindIndex(entry =>
                entry.LevelName.Equals("Truefort", StringComparison.OrdinalIgnoreCase));
            return truefortIndex >= 0 ? truefortIndex : 0;
        }

        public OpenGarrisonMapRotationEntry? GetSelectedMapEntry()
        {
            return MapIndex >= 0 && MapIndex < MapEntries.Count
                ? MapEntries[MapIndex]
                : null;
        }

        public string GetStockRotationSummary(int previewCount = 4)
        {
            var orderedNames = OpenGarrisonStockMapCatalog.GetOrderedIncludedMapLevelNames(MapEntries);
            if (orderedNames.Count == 0)
            {
                return "Map rotation: no maps selected.";
            }

            var preview = string.Join(" -> ", orderedNames.Take(Math.Max(1, previewCount)));
            if (orderedNames.Count > Math.Max(1, previewCount))
            {
                preview += " ...";
            }

            return $"Map rotation: {preview}";
        }

        public void ScrollMapList(int deltaRows, int visibleRowCount)
        {
            MapScrollOffset = Math.Clamp(
                MapScrollOffset + deltaRows,
                0,
                GetMaxMapScrollOffset(visibleRowCount));
        }

        public void ClampMapScrollOffset(int visibleRowCount)
        {
            MapScrollOffset = Math.Clamp(MapScrollOffset, 0, GetMaxMapScrollOffset(visibleRowCount));
        }

        public void EnsureSelectedMapVisible(int visibleRowCount)
        {
            EnsureMapIndexVisible(MapIndex, visibleRowCount);
        }

        public void EnsureMapIndexVisible(int mapIndex, int visibleRowCount)
        {
            if (MapEntries.Count == 0)
            {
                MapScrollOffset = 0;
                return;
            }

            var capacity = Math.Max(1, visibleRowCount);
            var clampedIndex = Math.Clamp(mapIndex, 0, MapEntries.Count - 1);
            var maxScrollOffset = GetMaxMapScrollOffset(capacity);
            MapScrollOffset = Math.Clamp(MapScrollOffset, 0, maxScrollOffset);
            if (clampedIndex < MapScrollOffset)
            {
                MapScrollOffset = clampedIndex;
            }
            else if (clampedIndex >= MapScrollOffset + capacity)
            {
                MapScrollOffset = clampedIndex - capacity + 1;
            }

            MapScrollOffset = Math.Clamp(MapScrollOffset, 0, maxScrollOffset);
        }

        private int GetMaxMapScrollOffset(int visibleRowCount)
        {
            return Math.Max(0, MapEntries.Count - Math.Max(1, visibleRowCount));
        }

        private static void NormalizeIncludedMapOrders(IEnumerable<OpenGarrisonMapRotationEntry> entries)
        {
            var normalizedOrder = 1;
            foreach (var entry in OpenGarrisonStockMapCatalog.GetOrderedEntries(entries).Where(entry => entry.Order > 0))
            {
                entry.Order = normalizedOrder;
                normalizedOrder += 1;
            }
        }

        private static List<OpenGarrisonMapRotationEntry> BuildMapEntries(OpenGarrisonHostSettings hostDefaults)
        {
            SimpleLevelFactory.ClearCachedCatalog();
            var configuredEntries = hostDefaults.StockMapRotation
                .GroupBy(entry => entry.LevelName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            var stockDefinitions = OpenGarrisonStockMapCatalog.Definitions
                .ToDictionary(definition => definition.LevelName, definition => definition, StringComparer.OrdinalIgnoreCase);
            var sourceLevels = SimpleLevelFactory.GetAvailableSourceLevels();
            var mergedEntries = new List<OpenGarrisonMapRotationEntry>(sourceLevels.Count);
            var customDefaultOrder = OpenGarrisonStockMapCatalog.Definitions.Count + 1;
            foreach (var sourceLevel in sourceLevels)
            {
                var isCustomMap = sourceLevel.IsCustomMap;
                var hasStockDefinition = stockDefinitions.TryGetValue(sourceLevel.Name, out var definition);
                var displayName = hasStockDefinition ? definition.DisplayName : sourceLevel.Name;
                var iniKey = hasStockDefinition ? definition.IniKey : sourceLevel.Name;
                var entryDefaultOrder = hasStockDefinition ? definition.DefaultOrder : customDefaultOrder;
                var configuredEntry = configuredEntries.TryGetValue(sourceLevel.Name, out var existingByLevelName)
                    ? existingByLevelName
                    : hostDefaults.StockMapRotation.FirstOrDefault(entry =>
                        string.Equals(entry.IniKey, iniKey, StringComparison.OrdinalIgnoreCase));

                if (configuredEntry is not null)
                {
                    mergedEntries.Add(new OpenGarrisonMapRotationEntry
                    {
                        IniKey = iniKey,
                        LevelName = sourceLevel.Name,
                        DisplayName = displayName,
                        Mode = sourceLevel.Mode,
                        IsCustomMap = isCustomMap,
                        DefaultOrder = entryDefaultOrder,
                        Order = configuredEntry.Order,
                    });
                }
                else
                {
                    mergedEntries.Add(new OpenGarrisonMapRotationEntry
                    {
                        IniKey = iniKey,
                        LevelName = sourceLevel.Name,
                        DisplayName = displayName,
                        Mode = sourceLevel.Mode,
                        IsCustomMap = isCustomMap,
                        DefaultOrder = entryDefaultOrder,
                        Order = isCustomMap ? 0 : entryDefaultOrder,
                    });
                }

                if (!hasStockDefinition)
                {
                    customDefaultOrder += 1;
                }
            }

            NormalizeIncludedMapOrders(mergedEntries);

            return OpenGarrisonStockMapCatalog.GetOrderedEntries(mergedEntries)
                .Select(entry => entry.Clone())
                .ToList();
        }
    }
}
