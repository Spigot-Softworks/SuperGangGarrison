using OpenGarrison.Core;
using OpenGarrison.Server;
using OpenGarrison.Server.Plugins;

partial class GameServer
{
    private ServerAudioService? _serverAudio;

    private void InitializeServerAudio()
    {
        _serverAudio = new ServerAudioService(ServerAudioSettings.Load(), _clientsBySlot, _world,
            _outboundMessaging.SendMessage, () => _clock.Elapsed.TotalSeconds, Console.WriteLine,
            requireVoiceChannelJoin: IsLastToDieHosted);
    }

    private void RegisterServerAudioCvars(ServerCvarRegistry registry)
    {
        var audio = _serverAudio!;
        registry.RegisterBoolean("sv_voice_enabled", "Allow player voice chat.", true,
            () => audio.Settings.VoiceEnabled, value => { audio.Settings.VoiceEnabled = value; audio.SettingsChanged(); });
        registry.RegisterBoolean("sv_voice_teamonly", "Restrict voice to each team and isolate spectators.", false,
            () => audio.Settings.VoiceTeamOnly, value => { audio.Settings.VoiceTeamOnly = value; audio.SettingsChanged(); });
        registry.RegisterBoolean("sv_jukebox_enabled", "Allow the server Jukebox.", true,
            () => audio.Settings.JukeboxEnabled, value => { audio.Settings.JukeboxEnabled = value; audio.SettingsChanged(); });
        registry.RegisterBoolean("sv_jukebox_loop", "Repeat the server music playlist.", true,
            () => audio.Settings.JukeboxLoop, value => { audio.Settings.JukeboxLoop = value; audio.SettingsChanged(); });
        registry.RegisterBoolean("sv_jukebox_autoplay", "Start the playlist when the server starts.", false,
            () => audio.Settings.JukeboxAutoPlay, value => { audio.Settings.JukeboxAutoPlay = value; audio.SettingsChanged(); });
    }

    private void RegisterServerAudioCommands()
    {
        _pluginCommandRegistry.RegisterBuiltIn("jukebox", "Control the server music playlist.",
            "jukebox list | play [number or filename] | next | pause | resume | stop | status",
            (_, arguments, _) => Task.FromResult(_serverAudio!.Execute(arguments)),
            OpenGarrisonServerAdminPermissions.ManageServerConfiguration);
    }
}
