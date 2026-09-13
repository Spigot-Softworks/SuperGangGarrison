using System;

namespace OpenGarrison.ClientShared;

/// <summary>Optional compositor-backed indeterminate bar; bounds are fractions of the game canvas.</summary>
public static class BrowserLoadingProgress
{
    public static Action<float, float, float, float>? Show { get; set; }
    public static Action? Hide { get; set; }
}
