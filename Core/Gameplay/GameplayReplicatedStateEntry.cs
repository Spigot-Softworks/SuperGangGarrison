namespace OpenGarrison.Core;

public enum GameplayReplicatedStateValueKind : byte
{
    Whole = 1,
    Scalar = 2,
    Toggle = 3,
}

public sealed record GameplayReplicatedStateEntry(
    string OwnerId,
    string Key,
    GameplayReplicatedStateValueKind Kind,
    int IntValue = 0,
    float FloatValue = 0f,
    bool BoolValue = false);
