#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
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
        int TimeLimitMinutes,
        int CapLimit,
        int RespawnSeconds,
        bool LobbyAnnounce,
        bool AutoBalance);

    private sealed class HostSetupFormState
    {
        public int HoverIndex { get; set; } = -1;
        public int MapIndex { get; set; }
        public HostSetupEditField EditField { get; set; }
        public HostSetupTab Tab { get; set; }
        public string ServerNameBuffer { get; set; } = "My Server";
        public string PortBuffer { get; set; } = "8190";
        public string SlotsBuffer { get; set; } = "10";
        public string PasswordBuffer { get; set; } = string.Empty;
        public string MapRotationFileBuffer { get; set; } = string.Empty;
        public string TimeLimitBuffer { get; set; } = "15";
        public string CapLimitBuffer { get; set; } = "5";
        public string RespawnSecondsBuffer { get; set; } = "5";
        public bool LobbyAnnounceEnabled { get; set; } = true;
        public bool AutoBalanceEnabled { get; set; } = true;
        public List<OpenGarrisonMapRotationEntry> MapEntries { get; set; } = new();

        public void LoadFrom(OpenGarrisonHostSettings hostDefaults)
        {
            ArgumentNullException.ThrowIfNull(hostDefaults);

            ServerNameBuffer = SanitizeServerName(hostDefaults.ServerName);
            PortBuffer = SanitizePort(hostDefaults.Port);
            SlotsBuffer = Math.Clamp(hostDefaults.Slots, 1, SimulationWorld.MaxPlayableNetworkPlayers)
                .ToString(CultureInfo.InvariantCulture);
            PasswordBuffer = hostDefaults.Password ?? string.Empty;
            MapRotationFileBuffer = hostDefaults.MapRotationFile ?? string.Empty;
            TimeLimitBuffer = Math.Clamp(hostDefaults.TimeLimitMinutes, 1, 255).ToString(CultureInfo.InvariantCulture);
            CapLimitBuffer = Math.Clamp(hostDefaults.CapLimit, 1, 255).ToString(CultureInfo.InvariantCulture);
            RespawnSecondsBuffer = Math.Clamp(hostDefaults.RespawnSeconds, 0, 255).ToString(CultureInfo.InvariantCulture);
            LobbyAnnounceEnabled = hostDefaults.LobbyAnnounceEnabled;
            AutoBalanceEnabled = hostDefaults.AutoBalanceEnabled;
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

        public void PrepareForOpen(OpenGarrisonHostSettings hostDefaults)
        {
            ArgumentNullException.ThrowIfNull(hostDefaults);

            HoverIndex = -1;
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
            settings.HostDefaults.MapRotationFile = MapRotationFileBuffer.Trim();
            settings.HostDefaults.TimeLimitMinutes = ParseClampedInt(TimeLimitBuffer, 15, 1, 255);
            settings.HostDefaults.CapLimit = ParseClampedInt(CapLimitBuffer, 5, 1, 255);
            settings.HostDefaults.RespawnSeconds = ParseClampedInt(RespawnSecondsBuffer, 5, 0, 255);
            settings.HostDefaults.LobbyAnnounceEnabled = LobbyAnnounceEnabled;
            settings.HostDefaults.AutoBalanceEnabled = AutoBalanceEnabled;
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

            request = new HostSetupLaunchRequest(
                serverName,
                port,
                maxPlayers,
                PasswordBuffer.Trim(),
                timeLimitMinutes,
                capLimit,
                respawnSeconds,
                LobbyAnnounceEnabled,
                AutoBalanceEnabled);
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
                HostSetupEditField.Password => HostSetupEditField.MapRotationFile,
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
                return "Stock rotation: no maps selected.";
            }

            var preview = string.Join(" -> ", orderedNames.Take(Math.Max(1, previewCount)));
            if (orderedNames.Count > Math.Max(1, previewCount))
            {
                preview += " ...";
            }

            return $"Stock rotation: {preview}";
        }

        private static List<OpenGarrisonMapRotationEntry> BuildMapEntries(OpenGarrisonHostSettings hostDefaults)
        {
            var configuredEntries = hostDefaults.StockMapRotation
                .ToDictionary(entry => entry.IniKey, entry => entry, StringComparer.OrdinalIgnoreCase);
            var mergedEntries = new List<OpenGarrisonMapRotationEntry>(OpenGarrisonStockMapCatalog.Definitions.Count);
            foreach (var definition in OpenGarrisonStockMapCatalog.Definitions)
            {
                if (configuredEntries.TryGetValue(definition.IniKey, out var existing))
                {
                    mergedEntries.Add(existing.Clone());
                }
                else
                {
                    mergedEntries.Add(new OpenGarrisonMapRotationEntry
                    {
                        IniKey = definition.IniKey,
                        LevelName = definition.LevelName,
                        DisplayName = definition.DisplayName,
                        Mode = definition.Mode,
                        DefaultOrder = definition.DefaultOrder,
                        Order = definition.DefaultOrder,
                    });
                }
            }

            return OpenGarrisonStockMapCatalog.GetOrderedEntries(mergedEntries)
                .Select(entry => entry.Clone())
                .ToList();
        }
    }

    private int _hostSetupHoverIndex
    {
        get => _hostSetupState.HoverIndex;
        set => _hostSetupState.HoverIndex = value;
    }

    private int _hostMapIndex
    {
        get => _hostSetupState.MapIndex;
        set => _hostSetupState.MapIndex = value;
    }

    private List<OpenGarrisonMapRotationEntry> _hostMapEntries
    {
        get => _hostSetupState.MapEntries;
        set => _hostSetupState.MapEntries = value ?? new List<OpenGarrisonMapRotationEntry>();
    }

    private HostSetupEditField _hostSetupEditField
    {
        get => _hostSetupState.EditField;
        set => _hostSetupState.EditField = value;
    }

    private HostSetupTab _hostSetupTab
    {
        get => _hostSetupState.Tab;
        set => _hostSetupState.Tab = value;
    }

    private string _hostServerNameBuffer
    {
        get => _hostSetupState.ServerNameBuffer;
        set => _hostSetupState.ServerNameBuffer = value;
    }

    private string _hostPortBuffer
    {
        get => _hostSetupState.PortBuffer;
        set => _hostSetupState.PortBuffer = value;
    }

    private string _hostSlotsBuffer
    {
        get => _hostSetupState.SlotsBuffer;
        set => _hostSetupState.SlotsBuffer = value;
    }

    private string _hostPasswordBuffer
    {
        get => _hostSetupState.PasswordBuffer;
        set => _hostSetupState.PasswordBuffer = value;
    }

    private string _hostMapRotationFileBuffer
    {
        get => _hostSetupState.MapRotationFileBuffer;
        set => _hostSetupState.MapRotationFileBuffer = value;
    }

    private string _hostTimeLimitBuffer
    {
        get => _hostSetupState.TimeLimitBuffer;
        set => _hostSetupState.TimeLimitBuffer = value;
    }

    private string _hostCapLimitBuffer
    {
        get => _hostSetupState.CapLimitBuffer;
        set => _hostSetupState.CapLimitBuffer = value;
    }

    private string _hostRespawnSecondsBuffer
    {
        get => _hostSetupState.RespawnSecondsBuffer;
        set => _hostSetupState.RespawnSecondsBuffer = value;
    }

    private bool _hostLobbyAnnounceEnabled
    {
        get => _hostSetupState.LobbyAnnounceEnabled;
        set => _hostSetupState.LobbyAnnounceEnabled = value;
    }

    private bool _hostAutoBalanceEnabled
    {
        get => _hostSetupState.AutoBalanceEnabled;
        set => _hostSetupState.AutoBalanceEnabled = value;
    }
}
