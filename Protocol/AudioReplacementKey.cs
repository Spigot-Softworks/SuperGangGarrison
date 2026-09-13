namespace OpenGarrison.Protocol;

public static class AudioReplacementKey
{
    public static string? For(object? value) => value switch
    {
        AudioRelayMessage audio => $"audio:{audio.SpeakerSlot}",
        VoiceSubmitMessage => "microphone",
        ServerAudioStateMessage => "audio-state",
        VoiceChannelMembershipMessage => "voice-membership",
        _ => null,
    };
}
