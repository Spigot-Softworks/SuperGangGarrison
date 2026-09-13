namespace OpenGarrison.Protocol;

[LastWins(ChannelType.Audio)]
public sealed class VoiceSubmitMessageSchema() : Protocol64LegacyMessageSchema<VoiceSubmitMessage>(
    Protocol64EventId.VoiceSubmit, Protocol64Direction.ClientToServer, 1100);

[LastWins(ChannelType.Audio)]
public sealed class AudioRelayMessageSchema() : Protocol64LegacyMessageSchema<AudioRelayMessage>(
    Protocol64EventId.AudioRelay, Protocol64Direction.ServerToClient, 1100);

[LastWins(ChannelType.Audio)]
public sealed class ServerAudioStateMessageSchema() : Protocol64LegacyMessageSchema<ServerAudioStateMessage>(
    Protocol64EventId.ServerAudioState, Protocol64Direction.ServerToClient, 256);

[LastWins(ChannelType.Audio)]
public sealed class VoiceChannelMembershipMessageSchema() : Protocol64LegacyMessageSchema<VoiceChannelMembershipMessage>(
    Protocol64EventId.VoiceChannelMembership, Protocol64Direction.ClientToServer, 16);
