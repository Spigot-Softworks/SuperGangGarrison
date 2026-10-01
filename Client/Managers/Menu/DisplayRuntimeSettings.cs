#nullable enable

using OpenGarrison.Core;

namespace OpenGarrison.Client;

public sealed class DisplayRuntimeSettings
{
    public DisplayModeKind DisplayMode { get; set; } = OpenGarrisonPreferencesDocument.DefaultDisplayMode;

    public IngameResolutionKind IngameResolution { get; set; } = OpenGarrisonPreferencesDocument.DefaultIngameResolution;

    public WindowSizeKind WindowSize { get; set; } = OpenGarrisonPreferencesDocument.DefaultWindowSize;

    public DisplayScaleModeKind DisplayScaleMode { get; set; } = OpenGarrisonPreferencesDocument.DefaultDisplayScaleMode;

    public int FrameRateLimit { get; set; }
}
