namespace OpenGarrison.Core.BotBrain;

public enum Og2NavigationGraphResolutionSource
{
    None,
    InMemory,
    Shipped,
    RuntimeCache,
    Built,
}

public readonly record struct Og2NavigationGraphResolution(
    Og2NavigationGraphResolutionSource Source,
    string Path);
