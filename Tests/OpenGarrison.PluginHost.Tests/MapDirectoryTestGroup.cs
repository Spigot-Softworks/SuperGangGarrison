using Xunit;

namespace OpenGarrison.PluginHost.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class MapDirectoryTestGroup
{
    public const string Name = "map-directory";
}
