using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenGarrison.Core;

public static class OpenGarrisonLegacyPreferencesMigration
{
    private const string ClientLegacyFileName = "client.settings.json";
    private const string ServerLegacyFileName = "server.settings.json";

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    static OpenGarrisonLegacyPreferencesMigration()
    {
        SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public static bool TryMigrate(string? destinationPath = null)
    {
        var resolvedPath = destinationPath ?? RuntimePaths.GetConfigPath(OpenGarrisonPreferencesDocument.DefaultFileName);
        if (File.Exists(resolvedPath))
        {
            return false;
        }

        var preferences = new OpenGarrisonPreferencesDocument();
        var migrated = false;
        migrated |= TryApplyLegacyClientSettings(RuntimePaths.GetConfigPath(ClientLegacyFileName), preferences);
        migrated |= TryApplyLegacyServerSettings(RuntimePaths.GetConfigPath(ServerLegacyFileName), preferences);
        if (!migrated)
        {
            return false;
        }

        preferences.Save(resolvedPath);
        return true;
    }

    private static bool TryApplyLegacyClientSettings(string path, OpenGarrisonPreferencesDocument preferences)
    {
        if (!TryLoadRoot(path, out var root))
        {
            return false;
        }

        if (TryReadString(root, "PlayerName", out var playerName))
        {
            preferences.PlayerName = playerName;
        }

        if (TryReadBool(root, "Fullscreen", out var fullscreen))
        {
            preferences.Fullscreen = fullscreen;
        }

        if (TryReadBool(root, "VSync", out var vSync))
        {
            preferences.VSync = vSync;
        }

        if (TryGetProperty(root, "IngameResolution", out var resolutionElement)
            && TryReadEnum(resolutionElement, out IngameResolutionKind ingameResolution))
        {
            preferences.IngameResolution = ingameResolution;
        }

        if (TryGetProperty(root, "MusicMode", out var musicModeElement)
            && TryReadEnum(musicModeElement, out MusicMode musicMode))
        {
            preferences.MusicMode = musicMode;
        }
        else if (TryReadBool(root, "IngameMusicEnabled", out var ingameMusicEnabled))
        {
            preferences.IngameMusicEnabled = ingameMusicEnabled;
        }

        if (TryReadBool(root, "KillCamEnabled", out var killCamEnabled))
        {
            preferences.KillCamEnabled = killCamEnabled;
        }

        if (TryReadInt(root, "ParticleMode", out var particleMode))
        {
            preferences.ParticleMode = particleMode;
        }

        if (TryReadInt(root, "GibLevel", out var gibLevel))
        {
            preferences.GibLevel = gibLevel;
        }

        if (TryReadInt(root, "CorpseDurationMode", out var corpseDurationMode))
        {
            preferences.CorpseDurationMode = corpseDurationMode;
        }

        if (TryReadBool(root, "HealerRadarEnabled", out var healerRadarEnabled))
        {
            preferences.HealerRadarEnabled = healerRadarEnabled;
        }

        if (TryReadBool(root, "ShowHealerEnabled", out var showHealerEnabled))
        {
            preferences.ShowHealerEnabled = showHealerEnabled;
        }

        if (TryReadBool(root, "ShowHealingEnabled", out var showHealingEnabled))
        {
            preferences.ShowHealingEnabled = showHealingEnabled;
        }

        if (TryReadBool(root, "ShowHealthBarEnabled", out var showHealthBarEnabled))
        {
            preferences.ShowHealthBarEnabled = showHealthBarEnabled;
        }

        if (TryGetProperty(root, "RecentConnection", out var recentConnection)
            && recentConnection.ValueKind is JsonValueKind.Object)
        {
            if (TryReadString(recentConnection, "Host", out var host))
            {
                preferences.RecentConnectionHost = host;
            }

            if (TryReadInt(recentConnection, "Port", out var port))
            {
                preferences.RecentConnectionPort = port;
            }
        }

        if (TryGetProperty(root, "HostDefaults", out var hostDefaults)
            && hostDefaults.ValueKind is JsonValueKind.Object)
        {
            ApplyLegacyHostDefaults(hostDefaults, preferences.HostSettings);
        }

        return true;
    }

    private static bool TryApplyLegacyServerSettings(string path, OpenGarrisonPreferencesDocument preferences)
    {
        if (!TryLoadRoot(path, out var root))
        {
            return false;
        }

        if (TryReadInt(root, "Port", out var port))
        {
            preferences.HostSettings.Port = port;
        }

        if (TryReadString(root, "ServerName", out var serverName))
        {
            preferences.HostSettings.ServerName = serverName;
        }

        if (TryReadString(root, "Password", out var password))
        {
            preferences.HostSettings.Password = password;
        }

        if (TryReadBool(root, "UseLobbyServer", out var useLobbyServer))
        {
            preferences.HostSettings.LobbyAnnounceEnabled = useLobbyServer;
        }

        if (TryReadString(root, "LobbyHost", out var lobbyHost))
        {
            preferences.LobbyHost = lobbyHost;
        }

        if (TryReadInt(root, "LobbyPort", out var lobbyPort))
        {
            preferences.LobbyPort = lobbyPort;
        }

        if (TryReadString(root, "RequestedMap", out var requestedMap))
        {
            ApplyPreferredMap(preferences.HostSettings, requestedMap);
        }

        if (TryReadString(root, "MapRotationFile", out var mapRotationFile))
        {
            preferences.HostSettings.MapRotationFile = mapRotationFile;
        }

        if (TryReadInt(root, "MaxPlayableClients", out var maxPlayableClients))
        {
            preferences.MaxPlayableClients = maxPlayableClients;
        }

        if (TryReadInt(root, "MaxTotalClients", out var maxTotalClients))
        {
            preferences.MaxTotalClients = maxTotalClients;
        }

        if (TryReadInt(root, "MaxSpectatorClients", out var maxSpectatorClients))
        {
            preferences.MaxSpectatorClients = maxSpectatorClients;
        }

        if (TryReadBool(root, "AutoBalanceEnabled", out var autoBalanceEnabled))
        {
            preferences.HostSettings.AutoBalanceEnabled = autoBalanceEnabled;
        }

        if (TryReadInt(root, "TimeLimitMinutes", out var timeLimitMinutes))
        {
            preferences.HostSettings.TimeLimitMinutes = timeLimitMinutes;
        }

        if (TryReadInt(root, "CapLimit", out var capLimit))
        {
            preferences.HostSettings.CapLimit = capLimit;
        }

        if (TryReadInt(root, "RespawnSeconds", out var respawnSeconds))
        {
            preferences.HostSettings.RespawnSeconds = respawnSeconds;
        }

        if (TryReadInt(root, "TickRate", out var tickRate))
        {
            preferences.HostSettings.TickRate = SimulationConfig.NormalizeTicksPerSecond(tickRate);
        }

        if (TryGetProperty(root, "HostDefaults", out var hostDefaults)
            && hostDefaults.ValueKind is JsonValueKind.Object)
        {
            ApplyLegacyHostDefaults(hostDefaults, preferences.HostSettings);
        }

        return true;
    }

    private static void ApplyLegacyHostDefaults(JsonElement hostDefaults, OpenGarrisonHostSettings hostSettings)
    {
        if (TryReadString(hostDefaults, "ServerName", out var serverName))
        {
            hostSettings.ServerName = serverName;
        }

        if (TryReadInt(hostDefaults, "Port", out var port))
        {
            hostSettings.Port = port;
        }

        if (TryReadInt(hostDefaults, "Slots", out var slots))
        {
            hostSettings.Slots = slots;
        }

        if (TryReadString(hostDefaults, "Password", out var password))
        {
            hostSettings.Password = password;
        }

        if (TryReadInt(hostDefaults, "TimeLimitMinutes", out var timeLimitMinutes))
        {
            hostSettings.TimeLimitMinutes = timeLimitMinutes;
        }

        if (TryReadInt(hostDefaults, "CapLimit", out var capLimit))
        {
            hostSettings.CapLimit = capLimit;
        }

        if (TryReadInt(hostDefaults, "RespawnSeconds", out var respawnSeconds))
        {
            hostSettings.RespawnSeconds = respawnSeconds;
        }

        if (TryReadInt(hostDefaults, "TickRate", out var tickRate))
        {
            hostSettings.TickRate = SimulationConfig.NormalizeTicksPerSecond(tickRate);
        }

        if (TryReadBool(hostDefaults, "LobbyAnnounceEnabled", out var lobbyAnnounceEnabled))
        {
            hostSettings.LobbyAnnounceEnabled = lobbyAnnounceEnabled;
        }

        if (TryReadBool(hostDefaults, "AutoBalanceEnabled", out var autoBalanceEnabled))
        {
            hostSettings.AutoBalanceEnabled = autoBalanceEnabled;
        }

        if (TryReadBool(hostDefaults, "DedicatedModeEnabled", out var dedicatedModeEnabled))
        {
            hostSettings.DedicatedModeEnabled = dedicatedModeEnabled;
        }

        if (TryReadString(hostDefaults, "MapRotationFile", out var mapRotationFile))
        {
            hostSettings.MapRotationFile = mapRotationFile;
        }

        if (TryReadString(hostDefaults, "SelectedMapName", out var selectedMapName))
        {
            ApplyPreferredMap(hostSettings, selectedMapName);
        }

        if (TryGetProperty(hostDefaults, "StockMapRotation", out var stockMapRotation)
            && stockMapRotation.ValueKind is JsonValueKind.Array
            && TryDeserialize(stockMapRotation, out List<OpenGarrisonMapRotationEntry>? entries)
            && entries is not null
            && entries.Count > 0)
        {
            hostSettings.StockMapRotation = entries;
        }
    }

    private static void ApplyPreferredMap(OpenGarrisonHostSettings hostSettings, string? mapName)
    {
        if (string.IsNullOrWhiteSpace(mapName))
        {
            return;
        }

        if (OpenGarrisonStockMapCatalog.TryGetDefinition(mapName, out var definition))
        {
            hostSettings.SetPreferredMap(definition.LevelName);
            return;
        }

        hostSettings.SetPreferredMap(mapName.Trim());
    }

    private static bool TryLoadRoot(string path, out JsonElement root)
    {
        root = default;
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path), DocumentOptions);
            root = document.RootElement.Clone();
            return root.ValueKind is JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        value = default;
        if (element.ValueKind is not JsonValueKind.Object)
        {
            return false;
        }

        if (element.TryGetProperty(name, out value))
        {
            return true;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        return false;
    }

    private static bool TryReadString(JsonElement element, string name, out string value)
    {
        value = string.Empty;
        return TryGetProperty(element, name, out var property) && TryReadString(property, out value);
    }

    private static bool TryReadString(JsonElement element, out string value)
    {
        value = string.Empty;
        return element.ValueKind switch
        {
            JsonValueKind.String => AssignString(element.GetString(), out value),
            JsonValueKind.Number => AssignString(element.GetRawText(), out value),
            JsonValueKind.True => AssignString(bool.TrueString, out value),
            JsonValueKind.False => AssignString(bool.FalseString, out value),
            _ => false,
        };
    }

    private static bool TryReadInt(JsonElement element, string name, out int value)
    {
        value = 0;
        return TryGetProperty(element, name, out var property) && TryReadInt(property, out value);
    }

    private static bool TryReadInt(JsonElement element, out int value)
    {
        value = 0;
        if (element.ValueKind is JsonValueKind.Number)
        {
            return element.TryGetInt32(out value);
        }

        return element.ValueKind is JsonValueKind.String
            && int.TryParse(element.GetString(), out value);
    }

    private static bool TryReadBool(JsonElement element, string name, out bool value)
    {
        value = false;
        return TryGetProperty(element, name, out var property) && TryReadBool(property, out value);
    }

    private static bool TryReadBool(JsonElement element, out bool value)
    {
        value = false;
        if (element.ValueKind is JsonValueKind.True)
        {
            value = true;
            return true;
        }

        if (element.ValueKind is JsonValueKind.False)
        {
            value = false;
            return true;
        }

        if (element.ValueKind is JsonValueKind.Number && element.TryGetInt32(out var numericValue))
        {
            value = numericValue != 0;
            return true;
        }

        return element.ValueKind is JsonValueKind.String
            && (bool.TryParse(element.GetString(), out value)
                || TryReadNumericBoolean(element.GetString(), out value));
    }

    private static bool TryReadEnum<TEnum>(JsonElement element, out TEnum value)
        where TEnum : struct, Enum
    {
        value = default;
        if (element.ValueKind is JsonValueKind.String
            && Enum.TryParse(element.GetString(), ignoreCase: true, out value))
        {
            return true;
        }

        return TryReadInt(element, out var numericValue)
            && Enum.IsDefined(typeof(TEnum), numericValue)
            && AssignEnum((TEnum)Enum.ToObject(typeof(TEnum), numericValue), out value);
    }

    private static bool TryDeserialize<T>(JsonElement element, out T? value)
    {
        value = default;
        try
        {
            value = JsonSerializer.Deserialize<T>(element.GetRawText(), SerializerOptions);
            return value is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryReadNumericBoolean(string? text, out bool value)
    {
        value = false;
        if (!int.TryParse(text, out var numericValue))
        {
            return false;
        }

        value = numericValue != 0;
        return true;
    }

    private static bool AssignString(string? candidate, out string value)
    {
        value = candidate ?? string.Empty;
        return true;
    }

    private static bool AssignEnum<TEnum>(TEnum candidate, out TEnum value)
        where TEnum : struct, Enum
    {
        value = candidate;
        return true;
    }
}
