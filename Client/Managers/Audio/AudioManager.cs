#nullable enable

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class AudioManager
{
    private readonly IAudioContext _context;

    public AudioManager(IAudioContext context)
    {
        _context = context;
        Events = new GameplayAudioEventController(context);
        Music = new GameplayAudioMusicController(context);
        RapidFire = new GameplayRapidFireAudioController(context);
    }

    public GameplayAudioEventController Events { get; }

    public GameplayAudioMusicController Music { get; }

    public GameplayRapidFireAudioController RapidFire { get; }
}
