namespace OpenGarrison.Core.BotBrain;

internal enum Og2NavigationGraphResolutionSource
{
    None,
    InMemory,
    Shipped,
    RuntimeCache,
    Built,
}

internal readonly record struct Og2NavigationGraphResolution(
    Og2NavigationGraphResolutionSource Source,
    string Path);
