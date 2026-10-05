using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

[Collection(ContentRootTestGroup.Name)]
public sealed class SuperGangGarrisonColdfrontPackageTests
{
    [Fact]
    public void ColdfrontStockIdentityImportsTheFourLayerFivePointPackageAsOneArea()
    {
        var originalContentRoot = ContentRoot.Path;
        try
        {
            ContentRoot.Initialize(ProjectSourceLocator.FindDirectory(Path.Combine("Core", "Content"))!);
            SimpleLevelFactory.ClearCachedCatalog();

            var packagePath = ProjectSourceLocator.FindFile(Path.Combine(
                "Maps", "cp_coldfront_js", "cp_coldfront_js.json"));
            var source = Assert.Single(
                SimpleLevelFactory.GetAvailableSourceLevels(),
                entry => string.Equals(entry.Name, "cp_coldfront_js", StringComparison.OrdinalIgnoreCase));

            Assert.Equal(CustomMapSourceKind.Package, source.SourceKind);
            Assert.Equal(Path.GetFullPath(packagePath!), Path.GetFullPath(source.RoomSourcePath));

            var level = SimpleLevelFactory.CreateImportedLevel("cp_coldfront_js");

            Assert.NotNull(level);
            Assert.Equal("cp_coldfront_js", level.Name);
            Assert.Equal(GameModeKind.ControlPoint, level.Mode);
            Assert.Equal(1, level.MapAreaCount);
            Assert.Equal(5, level.RoomObjects.Count(marker => marker.Type == RoomObjectType.ControlPoint));
            Assert.Equal(4, level.CustomMapVisuals.ParallaxLayers.Count);
        }
        finally
        {
            ContentRoot.Initialize(originalContentRoot);
            SimpleLevelFactory.ClearCachedCatalog();
        }
    }
}
