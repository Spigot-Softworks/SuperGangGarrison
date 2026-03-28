using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenGarrison.Core;

public sealed class OpenGarrisonPreferencesDocument
{
    public const string DefaultFileName = "OpenGarrison.ini";
    private const string SettingsSection = "Settings";
    private const string ServerSection = "Server";
    private const string ConnectionSection = "Connection";
    private const string ServerAdvancedSection = "Server.Advanced";

    public string PlayerName { get; set; } = "Player";

    public bool Fullscreen { get; set; }

    public bool VSync { get; set; }

    public IngameResolutionKind IngameResolution { get; set; } = IngameResolutionKind.Aspect4x3;

    public MusicMode MusicMode { get; set; } = MusicMode.MenuAndInGame;

    public bool IngameMusicEnabled
    {
        get => MusicMode is MusicMode.MenuAndInGame or MusicMode.InGameOnly;
        set => MusicMode = value ? MusicMode.MenuAndInGame : MusicMode.MenuOnly;
    }

    public bool KillCamEnabled { get; set; } = true;

    public int ParticleMode { get; set; }

    public int GibLevel { get; set; } = 3;

    public int CorpseDurationMode { get; set; }

    public bool HealerRadarEnabled { get; set; } = true;

    public bool ShowHealerEnabled { get; set; } = true;

    public bool ShowHealingEnabled { get; set; } = true;

    public bool ShowHealthBarEnabled { get; set; }

    public string RecentConnectionHost { get; set; } = "127.0.0.1";

    public int RecentConnectionPort { get; set; } = 8190;

    public OpenGarrisonHostSettings HostSettings { get; set; } = new();

    public string LobbyHost { get; set; } = "OpenGarrison.game-host.org";

    public int LobbyPort { get; set; } = 29942;

    public int MaxPlayableClients { get; set; } = SimulationWorld.MaxPlayableNetworkPlayers;

    public int MaxTotalClients { get; set; } = SimulationWorld.MaxPlayableNetworkPlayers;

    public int MaxSpectatorClients { get; set; } = SimulationWorld.MaxPlayableNetworkPlayers;

    public static OpenGarrisonPreferencesDocument Load(string? path = null)
    {
        var resolvedPath = path ?? RuntimePaths.GetConfigPath(DefaultFileName);
        var ini = IniConfigurationFile.Load(resolvedPath);
        var legacySelectedMap = ini.GetString(ServerSection, "SelectedMap", string.Empty);

        return new OpenGarrisonPreferencesDocument
        {
            PlayerName = ini.GetString(SettingsSection, "PlayerName", "Player"),
            Fullscreen = ini.GetBool(SettingsSection, "Fullscreen", false),
            VSync = ini.GetBool(SettingsSection, "Monitor Sync", false),
            IngameResolution = NormalizeIngameResolution((IngameResolutionKind)ini.GetInt(SettingsSection, "Resolution", (int)IngameResolutionKind.Aspect4x3)),
            MusicMode = LoadMusicMode(ini),
            KillCamEnabled = ini.GetBool(SettingsSection, "Kill Cam", true),
            ParticleMode = ini.GetInt(SettingsSection, "Particles", 0),
            GibLevel = ini.GetInt(SettingsSection, "Gib Level", 3),
            CorpseDurationMode = ini.GetInt(SettingsSection, "Corpse Duration", 0),
            HealerRadarEnabled = ini.GetBool(SettingsSection, "Healer Radar", true),
            ShowHealerEnabled = ini.GetBool(SettingsSection, "Show Healer", true),
            ShowHealingEnabled = ini.GetBool(SettingsSection, "Show Healing", true),
            ShowHealthBarEnabled = ini.GetBool(SettingsSection, "Show Healthbar", false),
            RecentConnectionHost = ini.GetString(ConnectionSection, "Host", "127.0.0.1"),
            RecentConnectionPort = ini.GetInt(ConnectionSection, "Port", 8190),
            HostSettings = OpenGarrisonHostSettings.LoadFrom(ini, legacySelectedMap),
            LobbyHost = ini.GetString(ServerAdvancedSection, "LobbyHost", "OpenGarrison.game-host.org"),
            LobbyPort = ini.GetInt(ServerAdvancedSection, "LobbyPort", 29942),
            MaxPlayableClients = ini.GetInt(ServerAdvancedSection, "MaxPlayableClients", SimulationWorld.MaxPlayableNetworkPlayers),
            MaxTotalClients = ini.GetInt(ServerAdvancedSection, "MaxTotalClients", SimulationWorld.MaxPlayableNetworkPlayers),
            MaxSpectatorClients = ini.GetInt(ServerAdvancedSection, "MaxSpectatorClients", SimulationWorld.MaxPlayableNetworkPlayers),
        };
    }

    public void Save(string? path = null)
    {
        var resolvedPath = path ?? RuntimePaths.GetConfigPath(DefaultFileName);
        var ini = new IniConfigurationFile();

        ini.SetString(SettingsSection, "PlayerName", PlayerName);
        ini.SetBool(SettingsSection, "Fullscreen", Fullscreen);
        ini.SetBool(SettingsSection, "UseLobby", HostSettings.LobbyAnnounceEnabled);
        ini.SetInt(SettingsSection, "HostingPort", HostSettings.Port);
        ini.SetInt(SettingsSection, "Resolution", (int)NormalizeIngameResolution(IngameResolution));
        ini.SetInt(SettingsSection, "Music", (int)NormalizeMusicMode(MusicMode));
        ini.SetInt(SettingsSection, "PlayerLimit", HostSettings.Slots);
        ini.SetInt(SettingsSection, "Particles", ParticleMode);
        ini.SetInt(SettingsSection, "Gib Level", GibLevel);
        ini.SetInt(SettingsSection, "Corpse Duration", CorpseDurationMode);
        ini.SetBool(SettingsSection, "Kill Cam", KillCamEnabled);
        ini.SetBool(SettingsSection, "Monitor Sync", VSync);
        ini.SetBool(SettingsSection, "Healer Radar", HealerRadarEnabled);
        ini.SetBool(SettingsSection, "Show Healer", ShowHealerEnabled);
        ini.SetBool(SettingsSection, "Show Healing", ShowHealingEnabled);
        ini.SetBool(SettingsSection, "Show Healthbar", ShowHealthBarEnabled);

        ini.SetString(ServerSection, "MapRotation", HostSettings.MapRotationFile);
        ini.SetBool(ServerSection, "Dedicated", HostSettings.DedicatedModeEnabled);
        ini.SetString(ServerSection, "ServerName", HostSettings.ServerName);
        ini.SetInt(ServerSection, "CapLimit", HostSettings.CapLimit);
        ini.SetBool(ServerSection, "AutoBalance", HostSettings.AutoBalanceEnabled);
        ini.SetInt(ServerSection, "Respawn Time", HostSettings.RespawnSeconds);
        ini.SetInt(ServerSection, "Time Limit", HostSettings.TimeLimitMinutes);
        ini.SetString(ServerSection, "Password", HostSettings.Password);

        OpenGarrisonStockMapCatalog.SaveTo(ini, HostSettings.StockMapRotation);

        ini.SetString(ConnectionSection, "Host", RecentConnectionHost);
        ini.SetInt(ConnectionSection, "Port", RecentConnectionPort);

        ini.SetString(ServerAdvancedSection, "LobbyHost", LobbyHost);
        ini.SetInt(ServerAdvancedSection, "LobbyPort", LobbyPort);
        ini.SetInt(ServerAdvancedSection, "TickRate", SimulationConfig.NormalizeTicksPerSecond(HostSettings.TickRate));
        ini.SetInt(ServerAdvancedSection, "MaxPlayableClients", MaxPlayableClients);
        ini.SetInt(ServerAdvancedSection, "MaxTotalClients", MaxTotalClients);
        ini.SetInt(ServerAdvancedSection, "MaxSpectatorClients", MaxSpectatorClients);

        ini.Save(resolvedPath);
    }

    private static MusicMode LoadMusicMode(IniConfigurationFile ini)
    {
        if (ini.ContainsKey(SettingsSection, "Music"))
        {
            return NormalizeMusicMode((MusicMode)ini.GetInt(SettingsSection, "Music", (int)MusicMode.MenuAndInGame));
        }

        return ini.GetBool(SettingsSection, "IngameMusic", true)
            ? MusicMode.MenuAndInGame
            : MusicMode.MenuOnly;
    }

    private static MusicMode NormalizeMusicMode(MusicMode musicMode)
    {
        return musicMode switch
        {
            MusicMode.MenuOnly => MusicMode.MenuOnly,
            MusicMode.MenuAndInGame => MusicMode.MenuAndInGame,
            MusicMode.InGameOnly => MusicMode.InGameOnly,
            MusicMode.None => MusicMode.None,
            _ => MusicMode.MenuAndInGame,
        };
    }

    private static IngameResolutionKind NormalizeIngameResolution(IngameResolutionKind ingameResolution)
    {
        return ingameResolution switch
        {
            IngameResolutionKind.Aspect5x4 => IngameResolutionKind.Aspect5x4,
            IngameResolutionKind.Aspect4x3 => IngameResolutionKind.Aspect4x3,
            IngameResolutionKind.Aspect16x9 => IngameResolutionKind.Aspect16x9,
            IngameResolutionKind.Aspect16x10 => IngameResolutionKind.Aspect16x9,
            IngameResolutionKind.Aspect2x1 => IngameResolutionKind.Aspect16x9,
            _ => IngameResolutionKind.Aspect4x3,
        };
    }
}

public sealed class OpenGarrisonHostSettings
{
    public string ServerName { get; set; } = "My Server";

    public int Port { get; set; } = 8190;

    public int Slots { get; set; } = 10;

    public string Password { get; set; } = string.Empty;

    public int TimeLimitMinutes { get; set; } = 15;

    public int CapLimit { get; set; } = 5;

    public int RespawnSeconds { get; set; } = 5;

    public int TickRate { get; set; } = SimulationConfig.DefaultTicksPerSecond;

    public bool LobbyAnnounceEnabled { get; set; } = true;

    public bool AutoBalanceEnabled { get; set; } = true;

    public bool DedicatedModeEnabled { get; set; }

    public string MapRotationFile { get; set; } = string.Empty;

    public List<OpenGarrisonMapRotationEntry> StockMapRotation { get; set; } = OpenGarrisonStockMapCatalog.CreateDefaultEntries();

    public string GetFirstIncludedMapLevelName()
    {
        var includedMaps = OpenGarrisonStockMapCatalog.GetOrderedIncludedMapLevelNames(StockMapRotation);
        return includedMaps.Count > 0
            ? includedMaps[0]
            : OpenGarrisonStockMapCatalog.Definitions[0].LevelName;
    }

    public void SetPreferredMap(string levelName)
    {
        var includedEntries = OpenGarrisonStockMapCatalog.GetOrderedEntries(StockMapRotation)
            .Where(entry => entry.Order > 0)
            .ToList();
        var selectedEntry = includedEntries.FirstOrDefault(entry =>
            string.Equals(entry.LevelName, levelName, StringComparison.OrdinalIgnoreCase));
        if (selectedEntry is null)
        {
            selectedEntry = StockMapRotation.FirstOrDefault(entry =>
                string.Equals(entry.LevelName, levelName, StringComparison.OrdinalIgnoreCase));
        }

        if (selectedEntry is null)
        {
            return;
        }

        includedEntries.RemoveAll(entry => string.Equals(entry.LevelName, selectedEntry.LevelName, StringComparison.OrdinalIgnoreCase));
        includedEntries.Insert(0, selectedEntry);
        for (var index = 0; index < includedEntries.Count; index += 1)
        {
            includedEntries[index].Order = index + 1;
        }

        foreach (var entry in StockMapRotation)
        {
            if (includedEntries.Any(candidate => string.Equals(candidate.LevelName, entry.LevelName, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (entry.Order > 0)
            {
                entry.Order = includedEntries.Count + 1;
                includedEntries.Add(entry);
            }
        }
    }

    public OpenGarrisonHostSettings Clone()
    {
        return new OpenGarrisonHostSettings
        {
            ServerName = ServerName,
            Port = Port,
            Slots = Slots,
            Password = Password,
            TimeLimitMinutes = TimeLimitMinutes,
            CapLimit = CapLimit,
            RespawnSeconds = RespawnSeconds,
            TickRate = TickRate,
            LobbyAnnounceEnabled = LobbyAnnounceEnabled,
            AutoBalanceEnabled = AutoBalanceEnabled,
            DedicatedModeEnabled = DedicatedModeEnabled,
            MapRotationFile = MapRotationFile,
            StockMapRotation = StockMapRotation.Select(entry => entry.Clone()).ToList(),
        };
    }

    internal static OpenGarrisonHostSettings LoadFrom(IniConfigurationFile ini, string legacySelectedMap)
    {
        return new OpenGarrisonHostSettings
        {
            ServerName = ini.GetString("Server", "ServerName", "My Server"),
            Port = ini.GetInt("Settings", "HostingPort", 8190),
            Slots = ini.GetInt("Settings", "PlayerLimit", 10),
            Password = ini.GetString("Server", "Password", string.Empty),
            TimeLimitMinutes = ini.GetInt("Server", "Time Limit", 15),
            CapLimit = ini.GetInt("Server", "CapLimit", 5),
            RespawnSeconds = ini.GetInt("Server", "Respawn Time", 5),
            TickRate = SimulationConfig.NormalizeTicksPerSecond(
                ini.GetInt("Server.Advanced", "TickRate", SimulationConfig.DefaultTicksPerSecond)),
            LobbyAnnounceEnabled = ini.GetBool("Settings", "UseLobby", true),
            AutoBalanceEnabled = ini.GetBool("Server", "AutoBalance", true),
            DedicatedModeEnabled = ini.GetBool("Server", "Dedicated", false),
            MapRotationFile = ini.GetString("Server", "MapRotation", string.Empty),
            StockMapRotation = OpenGarrisonStockMapCatalog.LoadFrom(ini, legacySelectedMap),
        };
    }
}

public sealed class OpenGarrisonMapRotationEntry
{
    public string IniKey { get; init; } = string.Empty;

    public string LevelName { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public GameModeKind Mode { get; init; }

    public int DefaultOrder { get; init; }

    public int Order { get; set; }

    public OpenGarrisonMapRotationEntry Clone()
    {
        return new OpenGarrisonMapRotationEntry
        {
            IniKey = IniKey,
            LevelName = LevelName,
            DisplayName = DisplayName,
            Mode = Mode,
            DefaultOrder = DefaultOrder,
            Order = Order,
        };
    }
}

public readonly record struct OpenGarrisonStockMapDefinition(
    string IniKey,
    string LevelName,
    string DisplayName,
    GameModeKind Mode,
    int DefaultOrder,
    params string[] Aliases);

public static class OpenGarrisonStockMapCatalog
{
    private const string MapsSection = "Maps";

    public static IReadOnlyList<OpenGarrisonStockMapDefinition> Definitions { get; } =
    [
        new("ctf_truefort", "Truefort", "Truefort", GameModeKind.CaptureTheFlag, 1, "truefort"),
        new("ctf_2dfort", "TwodFortTwo", "TwodFortTwo", GameModeKind.CaptureTheFlag, 2, "2dfort", "twodforttwo"),
        new("ctf_conflict", "Conflict", "Conflict", GameModeKind.CaptureTheFlag, 3, "conflict"),
        new("ctf_classicwell", "ClassicWell", "ClassicWell", GameModeKind.CaptureTheFlag, 4, "classicwell"),
        new("ctf_waterway", "Waterway", "Waterway", GameModeKind.CaptureTheFlag, 5, "waterway"),
        new("ctf_orange", "Orange", "Orange", GameModeKind.CaptureTheFlag, 6, "orange"),
        new("cp_dirtbowl", "Dirtbowl", "Dirtbowl", GameModeKind.ControlPoint, 7, "dirtbowl"),
        new("cp_egypt", "Egypt", "Egypt", GameModeKind.ControlPoint, 8, "egypt"),
        new("arena_montane", "Montane", "Montane", GameModeKind.Arena, 9, "montane"),
        new("arena_lumberyard", "Lumberyard", "Lumberyard", GameModeKind.Arena, 10, "lumberyard"),
        new("gen_destroy", "Destroy", "Destroy", GameModeKind.Generator, 11, "destroy"),
    ];

    public static List<OpenGarrisonMapRotationEntry> CreateDefaultEntries()
    {
        return Definitions
            .Select(definition => new OpenGarrisonMapRotationEntry
            {
                IniKey = definition.IniKey,
                LevelName = definition.LevelName,
                DisplayName = definition.DisplayName,
                Mode = definition.Mode,
                DefaultOrder = definition.DefaultOrder,
                Order = definition.DefaultOrder,
            })
            .ToList();
    }

    public static List<OpenGarrisonMapRotationEntry> LoadFrom(IniConfigurationFile ini, string legacySelectedMap)
    {
        var entries = CreateDefaultEntries();
        var hasExplicitMaps = false;
        foreach (var entry in entries)
        {
            if (!ini.ContainsKey(MapsSection, entry.IniKey))
            {
                continue;
            }

            hasExplicitMaps = true;
            entry.Order = Math.Max(0, ini.GetInt(MapsSection, entry.IniKey, entry.DefaultOrder));
        }

        if (!hasExplicitMaps && TryGetDefinition(legacySelectedMap, out var legacyDefinition))
        {
            var selectedEntry = entries.First(entry => string.Equals(entry.LevelName, legacyDefinition.LevelName, StringComparison.OrdinalIgnoreCase));
            entries.Remove(selectedEntry);
            entries.Insert(0, selectedEntry);
            for (var index = 0; index < entries.Count; index += 1)
            {
                entries[index].Order = index + 1;
            }
        }

        return entries;
    }

    public static void SaveTo(IniConfigurationFile ini, IEnumerable<OpenGarrisonMapRotationEntry> entries)
    {
        foreach (var entry in Definitions)
        {
            var configuredEntry = entries.FirstOrDefault(candidate =>
                string.Equals(candidate.IniKey, entry.IniKey, StringComparison.OrdinalIgnoreCase));
            ini.SetInt(MapsSection, entry.IniKey, Math.Max(0, configuredEntry?.Order ?? entry.DefaultOrder));
        }
    }

    public static IReadOnlyList<OpenGarrisonMapRotationEntry> GetOrderedEntries(IEnumerable<OpenGarrisonMapRotationEntry> entries)
    {
        return entries
            .OrderBy(entry => entry.Order <= 0 ? int.MaxValue : entry.Order)
            .ThenBy(entry => entry.DefaultOrder)
            .ToArray();
    }

    public static IReadOnlyList<string> GetOrderedIncludedMapLevelNames(IEnumerable<OpenGarrisonMapRotationEntry> entries)
    {
        return GetOrderedEntries(entries)
            .Where(entry => entry.Order > 0)
            .Select(entry => entry.LevelName)
            .ToArray();
    }

    public static bool TryGetDefinition(string mapName, out OpenGarrisonStockMapDefinition definition)
    {
        var normalized = NormalizeMapName(mapName);
        foreach (var candidate in Definitions)
        {
            if (string.Equals(candidate.IniKey, normalized, StringComparison.OrdinalIgnoreCase)
                || string.Equals(candidate.LevelName, normalized, StringComparison.OrdinalIgnoreCase)
                || candidate.Aliases.Any(alias => string.Equals(alias, normalized, StringComparison.OrdinalIgnoreCase)))
            {
                definition = candidate;
                return true;
            }
        }

        definition = default;
        return false;
    }

    private static string NormalizeMapName(string? mapName)
    {
        if (string.IsNullOrWhiteSpace(mapName))
        {
            return string.Empty;
        }

        var trimmed = mapName.Trim();
        var underscoreIndex = trimmed.IndexOf('_');
        if (underscoreIndex > 0)
        {
            var prefix = trimmed[..underscoreIndex];
            if (prefix.Equals("ctf", StringComparison.OrdinalIgnoreCase)
                || prefix.Equals("cp", StringComparison.OrdinalIgnoreCase)
                || prefix.Equals("arena", StringComparison.OrdinalIgnoreCase)
                || prefix.Equals("gen", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed;
            }
        }

        return trimmed;
    }
}
