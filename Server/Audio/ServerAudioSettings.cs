using OpenGarrison.Core;

namespace OpenGarrison.Server;

internal sealed class ServerAudioSettings
{
    public bool VoiceEnabled { get; set; } = true;
    public bool VoiceTeamOnly { get; set; }
    public bool JukeboxEnabled { get; set; } = true;
    public bool JukeboxAutoPlay { get; set; }
    public bool JukeboxLoop { get; set; } = true;
    public string JukeboxDirectory { get; set; } = "jukebox";
    public static string FilePath => RuntimePaths.GetConfigPath("server-audio.json");
    public static ServerAudioSettings Load() => JsonConfigurationFile.LoadOrCreate<ServerAudioSettings>(FilePath);
    public void Save() => JsonConfigurationFile.Save(FilePath, this);
}
