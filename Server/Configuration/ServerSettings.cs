using System;
using System.IO;
using OpenGarrison.Core;

sealed class ServerSettings
{
    public const string DefaultFileName = OpenGarrisonPreferencesDocument.DefaultFileName;
    private const string LegacyFileName = "server.settings.json";

    public int Port { get; set; } = 8190;

    public string ServerName { get; set; } = "My Server";

    public string Password { get; set; } = string.Empty;

    public bool UseLobbyServer { get; set; } = true;

    public string LobbyHost { get; set; } = OpenGarrisonPreferencesDocument.DefaultLobbyHost;

    public int LobbyPort { get; set; } = OpenGarrisonPreferencesDocument.DefaultLobbyPort;

    public string RequestedMap { get; set; } = string.Empty;

    public string MapRotationFile { get; set; } = string.Empty;

    public int MaxPlayableClients { get; set; } = 10;

    public int MaxTotalClients { get; set; } = 10;

    public int MaxSpectatorClients { get; set; } = 10;

    public bool AutoBalanceEnabled { get; set; } = true;

    public bool SecondaryAbilitiesEnabled { get; set; } = true;

    public bool RandomSpreadEnabled { get; set; } = true;

    public bool CompetitiveReadyUpEnabled { get; set; }

    public int CompetitiveSetupSeconds { get; set; } = 10;

    public int TimeLimitMinutes { get; set; } = 15;

    public int CapLimit { get; set; } = 5;

    public int RespawnSeconds { get; set; } = 5;

    public int TickRate { get; set; } = SimulationConfig.DefaultTicksPerSecond;

    public string RconPassword { get; set; } = string.Empty;

    public bool BotAutofillEnabled { get; set; }

    public int BotAutofillMinPlayers { get; set; }

    public int BotAutofillPerTeam { get; set; }

    public bool SnapshotCompressionEnabled { get; set; } = true;

    public bool PersistentGameplayOwnershipEnabled { get; set; }

    public PersistentGameplayOwnershipIdentityMode PersistentGameplayOwnershipIdentityMode { get; set; } = PersistentGameplayOwnershipIdentityMode.Disabled;

    public string PersistentGameplayOwnershipFile { get; set; } = "gameplay-ownership.json";

    public OpenGarrisonHostSettings HostDefaults { get; set; } = new();

    public static ServerSettings Load(string? path = null)
    {
        var resolvedPath = path ?? RuntimePaths.GetConfigPath(DefaultFileName);
        if (File.Exists(resolvedPath))
        {
            return LoadFromIni(resolvedPath);
        }

        if (OpenGarrisonLegacyPreferencesMigration.TryMigrate(resolvedPath))
        {
            return LoadFromIni(resolvedPath);
        }

        var legacyPath = RuntimePaths.GetConfigPath(LegacyFileName);
        if (File.Exists(legacyPath))
        {
            var migrated = JsonConfigurationFile.LoadOrCreate<ServerSettings>(legacyPath);
            migrated.Save(resolvedPath);
            return migrated;
        }

        var created = new ServerSettings();
        created.Save(resolvedPath);
        return created;
    }

    public void Save(string? path = null)
    {
        var resolvedPath = path ?? RuntimePaths.GetConfigPath(DefaultFileName);
        var preferences = File.Exists(resolvedPath)
            ? OpenGarrisonPreferencesDocument.Load(resolvedPath)
            : new OpenGarrisonPreferencesDocument();
        ApplyTo(preferences);
        preferences.Save(resolvedPath);
    }

    private static ServerSettings LoadFromIni(string path)
    {
        var ini = IniConfigurationFile.Load(path);
        var preferences = OpenGarrisonPreferencesDocument.Load(path);
        var hostDefaults = preferences.HostSettings.Clone();
        var maxPlayableClients = ini.ContainsKey("Server.Advanced", "MaxPlayableClients")
            ? preferences.MaxPlayableClients
            : hostDefaults.Slots;
        var maxTotalClients = ini.ContainsKey("Server.Advanced", "MaxTotalClients")
            ? preferences.MaxTotalClients
            : hostDefaults.Slots;
        var maxSpectatorClients = ini.ContainsKey("Server.Advanced", "MaxSpectatorClients")
            ? preferences.MaxSpectatorClients
            : hostDefaults.Slots;
        return new ServerSettings
        {
            Port = hostDefaults.Port,
            ServerName = hostDefaults.ServerName,
            Password = hostDefaults.Password,
            UseLobbyServer = hostDefaults.LobbyAnnounceEnabled,
            LobbyHost = preferences.LobbyHost,
            LobbyPort = preferences.LobbyPort,
            RequestedMap = string.Empty,
            MapRotationFile = hostDefaults.MapRotationFile,
            MaxPlayableClients = maxPlayableClients,
            MaxTotalClients = maxTotalClients,
            MaxSpectatorClients = maxSpectatorClients,
            AutoBalanceEnabled = hostDefaults.AutoBalanceEnabled,
            SecondaryAbilitiesEnabled = hostDefaults.SecondaryAbilitiesEnabled,
            RandomSpreadEnabled = hostDefaults.RandomSpreadEnabled,
            CompetitiveReadyUpEnabled = hostDefaults.CompetitiveReadyUpEnabled,
            CompetitiveSetupSeconds = hostDefaults.CompetitiveSetupSeconds,
            TimeLimitMinutes = hostDefaults.TimeLimitMinutes,
            CapLimit = hostDefaults.CapLimit,
            RespawnSeconds = hostDefaults.RespawnSeconds,
            TickRate = SimulationConfig.NormalizeTicksPerSecond(hostDefaults.TickRate),
            RconPassword = hostDefaults.RconPassword,
            BotAutofillEnabled = hostDefaults.BotAutofillEnabled,
            BotAutofillMinPlayers = hostDefaults.BotAutofillMinPlayers,
            BotAutofillPerTeam = hostDefaults.BotAutofillPerTeam,
            HostDefaults = hostDefaults,
            PersistentGameplayOwnershipEnabled = preferences.PersistentGameplayOwnershipEnabled,
            PersistentGameplayOwnershipIdentityMode = preferences.PersistentGameplayOwnershipIdentityMode,
            PersistentGameplayOwnershipFile = preferences.PersistentGameplayOwnershipFile,
            SnapshotCompressionEnabled = preferences.SnapshotCompressionEnabled,
        };
    }

    private void ApplyTo(OpenGarrisonPreferencesDocument preferences)
    {
        var hostDefaults = (HostDefaults ?? new OpenGarrisonHostSettings()).Clone();
        hostDefaults.Port = Port;
        hostDefaults.ServerName = ServerName;
        hostDefaults.Password = Password;
        hostDefaults.LobbyAnnounceEnabled = UseLobbyServer;
        hostDefaults.MapRotationFile = MapRotationFile;
        hostDefaults.AutoBalanceEnabled = AutoBalanceEnabled;
        hostDefaults.SecondaryAbilitiesEnabled = SecondaryAbilitiesEnabled;
        hostDefaults.RandomSpreadEnabled = RandomSpreadEnabled;
        hostDefaults.CompetitiveReadyUpEnabled = CompetitiveReadyUpEnabled;
        hostDefaults.CompetitiveSetupSeconds = Math.Clamp(CompetitiveSetupSeconds, 0, 120);
        hostDefaults.TimeLimitMinutes = TimeLimitMinutes;
        hostDefaults.CapLimit = CapLimit;
        hostDefaults.RespawnSeconds = RespawnSeconds;
        hostDefaults.TickRate = SimulationConfig.NormalizeTicksPerSecond(TickRate);
        hostDefaults.RconPassword = RconPassword;
        hostDefaults.BotAutofillEnabled = BotAutofillEnabled;
        hostDefaults.BotAutofillMinPlayers = Math.Clamp(BotAutofillMinPlayers, 0, SimulationWorld.MaxPlayableNetworkPlayers);
        hostDefaults.BotAutofillPerTeam = Math.Clamp(BotAutofillPerTeam, 0, SimulationWorld.MaxPlayableNetworkPlayers / 2);
        if (hostDefaults.Slots <= 0)
        {
            hostDefaults.Slots = MaxPlayableClients;
        }

        preferences.HostSettings = hostDefaults;
        preferences.LobbyHost = LobbyHost;
        preferences.LobbyPort = LobbyPort;
        preferences.MaxPlayableClients = MaxPlayableClients;
        preferences.MaxTotalClients = MaxTotalClients;
        preferences.MaxSpectatorClients = MaxSpectatorClients;
        preferences.PersistentGameplayOwnershipEnabled = PersistentGameplayOwnershipEnabled;
        preferences.PersistentGameplayOwnershipIdentityMode = PersistentGameplayOwnershipIdentityMode;
        preferences.PersistentGameplayOwnershipFile = PersistentGameplayOwnershipFile;
        preferences.SnapshotCompressionEnabled = SnapshotCompressionEnabled;
    }
}
