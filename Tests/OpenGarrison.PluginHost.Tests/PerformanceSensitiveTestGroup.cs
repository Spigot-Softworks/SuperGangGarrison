using Xunit;

namespace OpenGarrison.PluginHost.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PerformanceSensitiveTestGroup
{
    public const string Name = "performance-sensitive";
}
