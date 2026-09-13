namespace OpenGarrison.Protocol;

/// <summary>Fixed Opus stream format. Each packet repeats the last three 20 ms frames.</summary>
public static class AudioWireFormat
{
    public const int SampleRate = 48000;
    public const int FrameMilliseconds = 20;
    public const int SamplesPerFrame = 960;
    public const int HistoryFrames = 3;
    public const int MaxFrameBytes = 320;
    public const int MaxNameBytes = 80;
    public const int MaxTrackBytes = 160;
    public const byte JukeboxSlot = 0;
    public const string JukeboxName = "Jukebox";

    public static bool IsNewer(uint value, uint previous) => unchecked((int)(value - previous)) > 0;

    public static bool IsValid(AudioPacket packet) => packet.Frames is { Length: >= 1 and <= HistoryFrames }
        && packet.Frames.All(frame => frame is { Length: > 0 and <= MaxFrameBytes });
}

public sealed record AudioPacket(uint Sequence, byte[][] Frames);

// A client can submit microphone audio only. Sender identity and music privileges are server-owned.
public sealed record VoiceSubmitMessage(bool TeamOnly, AudioPacket Packet) : IProtocolMessage
{
    public MessageType Type => MessageType.VoiceSubmit;
}

public sealed record AudioRelayMessage(byte SpeakerSlot, uint StreamId, string SpeakerName, byte Team, AudioPacket Packet) : IProtocolMessage
{
    public MessageType Type => MessageType.AudioRelay;
    public bool IsJukebox => SpeakerSlot == AudioWireFormat.JukeboxSlot;
}

public sealed record ServerAudioStateMessage(uint Revision, bool VoiceEnabled, bool TeamOnly,
    uint JukeboxStreamId, bool JukeboxPlaying, bool JukeboxPaused, string TrackName,
    bool VoiceChannelRequiresJoin = false, bool VoiceChannelJoined = false, uint VoiceChannelRevision = 0) : IProtocolMessage
{
    public MessageType Type => MessageType.ServerAudioState;
}

// Repeated until acknowledged by the personalized server audio state. Revisions prevent
// a delayed join from undoing a newer leave on unordered/lossy transports.
public sealed record VoiceChannelMembershipMessage(uint Revision, bool Joined) : IProtocolMessage
{
    public MessageType Type => MessageType.VoiceChannelMembership;
}
