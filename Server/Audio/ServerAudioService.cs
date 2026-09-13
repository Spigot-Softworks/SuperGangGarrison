using OpenGarrison.Core;
using OpenGarrison.Protocol;

namespace OpenGarrison.Server;

internal sealed class ServerAudioService : IDisposable
{
    private sealed class SenderState
    {
        public uint Sequence;
        public bool HasSequence;
        public double Tokens = 10;
        public double RefilledAt;
        public uint StreamId;
    }
    private readonly Dictionary<ClientSession, SenderState> _senders = new();
    private readonly Dictionary<ClientSession, VoiceChannelMembershipMessage> _memberships = new();
    private readonly bool _requireVoiceChannelJoin;
    private readonly Dictionary<byte, ClientSession> _clients;
    private readonly SimulationWorld _world;
    private readonly Action<ServerTransportPeer, IProtocolMessage> _send;
    private readonly Action<string> _log;
    private readonly Func<double> _now;
    private readonly string _musicDirectory;
    private readonly AudioPacketHistory _musicHistory = new();
    private JukeboxTrackReader? _track;
    private string[] _playlist = [];
    private int _trackIndex = -1;
    private int _failedTracks;
    private uint _nextStreamId;
    private uint _musicStreamId;
    private uint _revision = 1;
    private double _nextMusicAt;
    private double _nextStateAt;
    private bool _paused;
    private bool _trackHadAudio;
    private double _trackFinishedAt = double.NaN;
    private double _lastSendFailureAt = double.NegativeInfinity;
    public ServerAudioSettings Settings { get; }

    public ServerAudioService(ServerAudioSettings settings, Dictionary<byte, ClientSession> clients,
        SimulationWorld world, Action<ServerTransportPeer, IProtocolMessage> send, Func<double> now, Action<string> log,
        bool requireVoiceChannelJoin = false)
    {
        Settings = settings;
        _clients = clients;
        _world = world;
        _send = send;
        _now = now;
        _log = log;
        _requireVoiceChannelJoin = requireVoiceChannelJoin;
        try
        {
            var directory = string.IsNullOrWhiteSpace(settings.JukeboxDirectory) ? "jukebox" : settings.JukeboxDirectory;
            _musicDirectory = Path.GetFullPath(Path.IsPathRooted(directory)
                ? directory : Path.Combine(RuntimePaths.ConfigDirectory, directory));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException)
        {
            _musicDirectory = Path.Combine(RuntimePaths.ConfigDirectory, "jukebox");
            _log("[jukebox] Invalid music directory; using " + _musicDirectory + ": " + ex.Message);
        }
        if (settings.JukeboxAutoPlay && settings.JukeboxEnabled) Execute("play");
    }

    public ServerAudioStateMessage State => new(_revision, Settings.VoiceEnabled, Settings.VoiceTeamOnly,
        _musicStreamId, _track is not null, _paused, _track is null ? "" : Path.GetFileNameWithoutExtension(_playlist[_trackIndex]),
        VoiceChannelRequiresJoin: _requireVoiceChannelJoin);

    private bool HasJoinedVoice(ClientSession client) => !_requireVoiceChannelJoin
        || _memberships.TryGetValue(client, out var membership) && membership.Joined;

    public void ReceiveMembership(ClientSession client, VoiceChannelMembershipMessage message)
    {
        if (!_requireVoiceChannelJoin || !client.IsAuthorized || message.Revision == 0
            || !_clients.TryGetValue(client.Slot, out var current) || !ReferenceEquals(current, client)) return;
        if (_memberships.TryGetValue(client, out var previous)
            && !AudioWireFormat.IsNewer(message.Revision, previous.Revision)) return;
        _memberships[client] = message;
        if (message.Joined && _senders.TryGetValue(client, out var sender)) sender.StreamId = NextStreamId();
        _revision++;
        SendState(client);
    }

    public void ReceiveVoice(ClientSession client, VoiceSubmitMessage message)
    {
        if (!Settings.VoiceEnabled || !client.IsAuthorized || client.IsGagged || !HasJoinedVoice(client)
            || !_clients.TryGetValue(client.Slot, out var current) || !ReferenceEquals(current, client)
            || !StreamingOpus.IsValid(message.Packet)) return;
        var now = _now();
        if (!_senders.TryGetValue(client, out var state))
            _senders.Add(client, state = new SenderState { RefilledAt = now, StreamId = NextStreamId() });
        state.Tokens = Math.Min(10, state.Tokens + (Math.Max(0, now - state.RefilledAt) * 55));
        state.RefilledAt = now;
        if (state.Tokens < 1 || state.HasSequence && !AudioWireFormat.IsNewer(message.Packet.Sequence, state.Sequence)) return;
        state.Tokens--;
        state.Sequence = message.Packet.Sequence;
        state.HasSequence = true;
        var team = TeamFor(client);
        var teamOnly = Settings.VoiceTeamOnly || message.TeamOnly;
        var relay = new AudioRelayMessage(client.Slot, state.StreamId, client.Name, team, message.Packet);
        foreach (var recipient in _clients.Values)
        {
            if (!recipient.IsAuthorized || !HasJoinedVoice(recipient) || ReferenceEquals(client, recipient)) continue;
            if (teamOnly && TeamFor(recipient) != team) continue;
            Send(recipient.Peer, relay);
        }
    }

    private byte TeamFor(ClientSession client) => ServerHelpers.IsSpectatorSlot(client.Slot)
        ? (byte)0 : _world.TryGetNetworkPlayer(client.Slot, out var player) ? (byte)player.Team : (byte)0;

    public void SendState(ClientSession client)
    {
        if (client.IsAuthorized) Send(client.Peer, State with
        {
            VoiceEnabled = Settings.VoiceEnabled && !client.IsGagged,
            VoiceChannelJoined = HasJoinedVoice(client),
            VoiceChannelRevision = _memberships.TryGetValue(client, out var membership) ? membership.Revision : 0,
        });
    }

    public void SettingsChanged()
    {
        if (!Settings.JukeboxEnabled) Stop();
        _revision++;
        Settings.Save();
        BroadcastState();
    }

    public void Tick()
    {
        var now = _now();
        if (now >= _nextStateAt)
        {
            foreach (var sender in _senders.Keys.Where(sender => !_clients.Values.Contains(sender)).ToArray()) _senders.Remove(sender);
            foreach (var member in _memberships.Keys.Where(member => !_clients.Values.Contains(member)).ToArray()) _memberships.Remove(member);
            BroadcastState();
            _nextStateAt = now + 1;
        }
        if (_track is null || _paused || !Settings.JukeboxEnabled) return;
        if (now - _nextMusicAt > 0.15) _nextMusicAt = now;
        for (var i = 0; i < 3 && now >= _nextMusicAt; i++)
        {
            if (!_track.TryRead(out var frame)) break;
            _trackHadAudio = true;
            _failedTracks = 0;
            var relay = new AudioRelayMessage(0, _musicStreamId, AudioWireFormat.JukeboxName, 0, _musicHistory.Add(frame));
            foreach (var client in _clients.Values) if (client.IsAuthorized) Send(client.Peer, relay);
            _nextMusicAt += 0.02;
        }
        if (_track.Finished)
        {
            // Let the receiver's jitter buffer and audio device drain the final frames before
            // a stop or new stream tells clients to discard the previous track's playback.
            if (double.IsNaN(_trackFinishedAt)) _trackFinishedAt = now;
            if (_trackHadAudio && now - _trackFinishedAt < 0.2) return;
            if (_track.Error is { } error)
            {
                _log($"[jukebox] {Path.GetFileName(_playlist[_trackIndex])}: {error}");
            }
            if (!_trackHadAudio) _failedTracks++;
            if (_failedTracks >= _playlist.Length || _trackIndex + 1 >= _playlist.Length && !Settings.JukeboxLoop) Stop();
            else PlayIndex((_trackIndex + 1) % _playlist.Length);
        }
    }

    public IReadOnlyList<string> Execute(string arguments)
    {
        var parts = arguments.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var command = parts.Length == 0 ? "status" : parts[0].ToLowerInvariant();
        var target = parts.Length > 1 ? parts[1].Trim().Trim('"') : "";
        try
        {
            if (command == "stop") { Stop(); return ["[jukebox] stopped."]; }
            if (command == "status") return [$"[jukebox] {(_track is null ? "stopped" : _paused ? "paused" : "playing")}: {State.TrackName}", $"[jukebox] playlist: {Path.Combine(_musicDirectory, "playlist.txt")}"];
            if (command == "list")
            {
                var files = ListFiles();
                return files.Length == 0 ? [EmptyPlaylistHelp]
                    : files.Select((file, index) => $"{index + 1}: {Path.GetFileName(file)}").ToArray();
            }
            if (!Settings.JukeboxEnabled) return ["[jukebox] Disabled. Set sv_jukebox_enabled true first."];
            if (command is "pause" or "resume")
            {
                if (_track is null) return ["[jukebox] No active track."];
                _paused = command == "pause";
                _nextMusicAt = _now();
                _revision++;
                BroadcastState();
                return [$"[jukebox] {(_paused ? "paused" : "resumed")}."];
            }
            if (command == "next" && _playlist.Length > 0) { PlayIndex((_trackIndex + 1) % _playlist.Length); return ["[jukebox] " + State.TrackName]; }
            if (command is "play" or "next")
            {
                var files = ListFiles();
                if (files.Length == 0) return [EmptyPlaylistHelp];
                var index = string.IsNullOrEmpty(target) ? 0 : int.TryParse(target, out var number) ? number - 1
                    : Array.FindIndex(files, file => string.Equals(Path.GetFileName(file), target, StringComparison.OrdinalIgnoreCase));
                if (index < 0 || index >= files.Length) return ["[jukebox] Track not found. Use jukebox list."];
                _playlist = files;
                _failedTracks = 0;
                PlayIndex(index);
                return ["[jukebox] " + State.TrackName];
            }
            return ["jukebox list | play [number or filename] | next | pause | resume | stop | status"];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return ["[jukebox] " + ex.Message];
        }
    }

    private string[] ListFiles()
    {
        Directory.CreateDirectory(_musicDirectory);
        var playlistPath = Path.Combine(_musicDirectory, "playlist.txt");
        if (!File.Exists(playlistPath))
        {
            // Bootstrap existing installations once, then the written line order is authoritative.
            var existing = Directory.EnumerateFiles(_musicDirectory).Where(IsSupportedTrack).Take(1024).Select(file => Path.GetFileName(file));
            File.WriteAllLines(playlistPath, new[] { "# One audio filename per line, in playback order. Blank lines and # comments are ignored." }.Concat(existing));
        }
        var tracks = new List<string>();
        foreach (var line in File.ReadLines(playlistPath))
        {
            var name = line.Trim();
            if (name.Length == 0 || name.StartsWith('#')) continue;
            // Only filenames in this directory; preserve repeats and the user's exact ordering.
            if (name.IndexOfAny(['/', '\\', ':']) >= 0 || !IsSupportedTrack(name))
            {
                _log("[jukebox] Skipping invalid playlist entry: " + name);
                continue;
            }
            var path = Path.Combine(_musicDirectory, name);
            if (!File.Exists(path)) { _log("[jukebox] Missing playlist file: " + name); continue; }
            tracks.Add(path);
            if (tracks.Count == 1024) break;
        }
        return tracks.ToArray();
    }

    private string EmptyPlaylistHelp => "[jukebox] Add WAV, OGG or MP3 files to " + _musicDirectory
        + " and list their filenames in playlist.txt, one per line in playback order.";
    private static bool IsSupportedTrack(string file) => (Path.GetExtension(file).ToLowerInvariant() is ".wav" or ".ogg" or ".mp3")
        && System.Text.Encoding.UTF8.GetByteCount(Path.GetFileNameWithoutExtension(file)) <= AudioWireFormat.MaxTrackBytes;

    private void PlayIndex(int index)
    {
        _track?.Dispose();
        _trackIndex = index;
        _track = new JukeboxTrackReader(_playlist[index]);
        _trackHadAudio = false;
        _trackFinishedAt = double.NaN;
        _musicStreamId = NextStreamId();
        _musicHistory.Clear();
        _paused = false;
        _nextMusicAt = _now();
        _revision++;
        BroadcastState();
    }
    private uint NextStreamId() { if (++_nextStreamId == 0) ++_nextStreamId; return _nextStreamId; }
    private void Send(ServerTransportPeer peer, IProtocolMessage message)
    {
        try { _send(peer, message); }
        catch (Exception ex) when (ex is System.Net.Sockets.SocketException or IOException or ObjectDisposedException)
        {
            if (_now() - _lastSendFailureAt >= 5)
            {
                _lastSendFailureAt = _now();
                _log("[audio] Could not send to a disconnected peer: " + ex.Message);
            }
        }
    }
    private void BroadcastState() { foreach (var client in _clients.Values) SendState(client); }
    private void Stop()
    {
        _track?.Dispose(); _track = null; _paused = false; _revision++; BroadcastState();
    }
    public void Dispose() { _track?.Dispose(); _track = null; _senders.Clear(); _memberships.Clear(); }
}
