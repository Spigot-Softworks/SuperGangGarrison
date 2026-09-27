namespace OpenGarrison.Core;

/// <summary>
/// Selects a broad CRT display family. Values are persisted in user preferences;
/// keep the numeric assignments stable.
/// </summary>
public enum CrtPresetKind
{
    Off = 0,
    PcMonitor = 1,
    StudioRgb = 2,
    ArcadeRgb = 3,
    HomeTvRgb = 4,
}
