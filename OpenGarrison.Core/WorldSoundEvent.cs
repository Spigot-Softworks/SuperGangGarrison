namespace OpenGarrison.Core;

public sealed record WorldSoundEvent(string SoundName, float X, float Y, ulong EventId = 0, ulong SourceFrame = 0);
