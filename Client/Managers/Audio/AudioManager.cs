#nullable enable

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class AudioManager
{
    private readonly IAudioContext _context;

    public AudioManager(IAudioContext context)
    {
        _context = context;
        RuntimeSettings = new AudioRuntimeSettings();
        Events = new GameplayAudioEventController(context);
        Music = new GameplayAudioMusicController(context);
        RapidFire = new GameplayRapidFireAudioController(context);
    }

    internal MusicResources MusicResources { get; } = new();

    public AudioRuntimeSettings RuntimeSettings { get; }

    public GameplayAudioEventController Events { get; }

    public GameplayAudioMusicController Music { get; }

    public GameplayRapidFireAudioController RapidFire { get; }
}
