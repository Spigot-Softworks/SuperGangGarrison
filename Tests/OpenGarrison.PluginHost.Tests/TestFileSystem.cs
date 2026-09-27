#nullable enable

namespace OpenGarrison.PluginHost.Tests;

internal static class TestFileSystem
{
    internal static string CreateTempRoot(string? prefix = null)
    {
        var name = prefix is null ? $"OpenGarrison.PluginHost.Tests-{Guid.NewGuid():N}" : $"{prefix}-{Guid.NewGuid():N}";
        var path = Path.Combine(Path.GetTempPath(), name);
        Directory.CreateDirectory(path);
        return path;
    }
}
