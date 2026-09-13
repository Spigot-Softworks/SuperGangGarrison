using OpenGarrison.Core;
using OpenGarrison.Protocol;
using OpenGarrison.Server;

namespace OpenGarrison.SessionRuntime;

/// <summary>Uses the same stereo music pipeline in offline practice without starting a network session.</summary>
public sealed class SessionJukeboxPlayer : IDisposable
{
    private readonly ServerAudioService _audio;
    public SessionJukeboxPlayer(SimulationWorld world, Func<double> now, Action<IProtocolMessage> receive)
    {
        var peer = new ServerTransportPeer(ServerTransportKind.WebSocket, 1, "local music", null, null, 0);
        _audio = new(new ServerAudioSettings { VoiceEnabled = false },
            new Dictionary<byte, ClientSession> { [1] = new(1, 1, peer, "Player", TimeSpan.Zero) },
            world, (_, message) => receive(message), now, _ => { });
    }
    public IReadOnlyList<string> Execute(string command) => _audio.Execute(command);
    public void Tick() => _audio.Tick();
    public void Dispose() => _audio.Dispose();
}
