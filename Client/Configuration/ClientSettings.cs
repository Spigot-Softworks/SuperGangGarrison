#nullable enable

using System.IO;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public sealed class ClientSettings
{
    public const string DefaultFileName = OpenGarrisonPreferencesDocument.DefaultFileName;
    private const string LegacyFileName = "client.settings.json";
    public const int CorpseDurationDefault = 0;
    public const int CorpseDurationInfinite = 1;

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

    public ClientRecentConnectionSettings RecentConnection { get; set; } = new();

    public OpenGarrisonHostSettings HostDefaults { get; set; } = new();

    public static ClientSettings Load(string? path = null)
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
            var migrated = JsonConfigurationFile.LoadOrCreate<ClientSettings>(legacyPath);
            migrated.Save(resolvedPath);
            return migrated;
        }

        var created = new ClientSettings();
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

    private static ClientSettings LoadFromIni(string path)
    {
        var document = OpenGarrisonPreferencesDocument.Load(path);
        return new ClientSettings
        {
            PlayerName = document.PlayerName,
            Fullscreen = document.Fullscreen,
            MusicMode = document.MusicMode,
            IngameResolution = document.IngameResolution,
            ParticleMode = document.ParticleMode,
            GibLevel = document.GibLevel,
            CorpseDurationMode = document.CorpseDurationMode,
            KillCamEnabled = document.KillCamEnabled,
            VSync = document.VSync,
            HealerRadarEnabled = document.HealerRadarEnabled,
            ShowHealerEnabled = document.ShowHealerEnabled,
            ShowHealingEnabled = document.ShowHealingEnabled,
            ShowHealthBarEnabled = document.ShowHealthBarEnabled,
            RecentConnection = new ClientRecentConnectionSettings
            {
                Host = document.RecentConnectionHost,
                Port = document.RecentConnectionPort,
            },
            HostDefaults = document.HostSettings.Clone(),
        };
    }

    private void ApplyTo(OpenGarrisonPreferencesDocument preferences)
    {
        preferences.PlayerName = PlayerName;
        preferences.Fullscreen = Fullscreen;
        preferences.VSync = VSync;
        preferences.IngameResolution = IngameResolution;
        preferences.MusicMode = MusicMode;
        preferences.KillCamEnabled = KillCamEnabled;
        preferences.ParticleMode = ParticleMode;
        preferences.GibLevel = GibLevel;
        preferences.CorpseDurationMode = CorpseDurationMode;
        preferences.HealerRadarEnabled = HealerRadarEnabled;
        preferences.ShowHealerEnabled = ShowHealerEnabled;
        preferences.ShowHealingEnabled = ShowHealingEnabled;
        preferences.ShowHealthBarEnabled = ShowHealthBarEnabled;
        preferences.RecentConnectionHost = RecentConnection.Host;
        preferences.RecentConnectionPort = RecentConnection.Port;
        preferences.HostSettings = HostDefaults.Clone();
    }
}

public sealed class ClientRecentConnectionSettings
{
    public string Host { get; set; } = "127.0.0.1";

    public int Port { get; set; } = 8190;
}
