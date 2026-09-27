namespace OpenGarrison.Core;

/// <summary>
/// Requested quality for the CRT presentation path. Values are persisted in user
/// preferences; keep the numeric assignments stable.
/// </summary>
public enum CrtQualityKind
{
    Auto = 0,
    Balanced = 1,
    High = 2,
}
