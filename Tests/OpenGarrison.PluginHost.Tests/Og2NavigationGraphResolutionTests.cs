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

            _ = Og2NavigationGraphStore.GetOrBuild(level, out var firstResolution);
            var graph = Og2NavigationGraphStore.GetOrBuild(level, out var secondResolution);

            Assert.Equal(Og2NavigationGraphResolutionSource.Shipped, firstResolution.Source);
            Assert.False(string.IsNullOrWhiteSpace(firstResolution.Path));
            Assert.True(File.Exists(firstResolution.Path), firstResolution.Path);
            Assert.Equal(Og2NavigationGraphResolutionSource.InMemory, secondResolution.Source);
            Assert.True(graph.NodeCount > 0);
        }
        finally
        {
            ContentRoot.Initialize(originalContentRoot);
        }
    }
}
