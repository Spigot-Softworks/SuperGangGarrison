using Xunit;

namespace OpenGarrison.PluginHost.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ContentRootTestGroup
{
    public const string Name = "content-root";
}
