namespace OpenGarrison.Core;

public sealed record WorldVisualEvent(string EffectName, float X, float Y, float DirectionDegrees = 0f, int Count = 1, ulong EventId = 0);
