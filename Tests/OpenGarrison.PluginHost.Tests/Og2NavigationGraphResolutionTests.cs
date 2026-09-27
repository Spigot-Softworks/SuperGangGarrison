using OpenGarrison.Core;
using OpenGarrison.Core.BotBrain;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

[Collection(MapDirectoryTestGroup.Name)]
public sealed class Og2NavigationGraphResolutionTests
{
    [Fact]
    public void GetOrBuildReportsInMemoryWhenLevelWasAlreadyWarmed()
    {
        var originalContentRoot = ContentRoot.Path;
        var coreContent = ProjectSourceLocator.FindDirectory(Path.Combine("Core", "Content"));
        Assert.False(string.IsNullOrWhiteSpace(coreContent));
        ContentRoot.Initialize(coreContent!);
        try
        {
            var level = SimpleLevelFactory.CreateImportedLevel("Conflict");
            Assert.NotNull(level);

            var provider = new NavigationGraphProvider();
            _ = provider.PreloadGraph(level);
            var firstSource = provider.LastPreloadSource;
            var firstPath = provider.LastSourcePath;
            var graph = provider.PreloadGraph(level);
            var secondSource = provider.LastPreloadSource;

            Assert.Equal("Shipped", firstSource);
            Assert.False(string.IsNullOrWhiteSpace(firstPath));
            Assert.True(File.Exists(firstPath), firstPath);
            Assert.Equal("InMemory", secondSource);
            Assert.True(graph.NodeCount > 0);
        }
        finally
        {
            ContentRoot.Initialize(originalContentRoot);
        }
    }
}
