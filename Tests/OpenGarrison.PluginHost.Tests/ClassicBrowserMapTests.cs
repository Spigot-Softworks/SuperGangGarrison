using System.Collections;
using System.Reflection;
using OpenGarrison.Client;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

[Collection(ContentRootTestGroup.Name)]
public sealed class ClassicBrowserMapTests
{


    [Fact]
    public void EveryOriginalLoadsCollisionAndObjectivesFromTheBrowserMemoryBundle()
    {
        var originalRoot = ContentRoot.Path;
        var sources = ClassicStockMapCatalog.Variants.Select(v => ProjectSourceLocator.FindFile(Path.Combine("Core", "Content", v.RelativeSourcePath))!).ToArray();
        var assets = new Dictionary<string, byte[]>();
        foreach (var source in sources)
        {
            var files = source.EndsWith(".json") ? Directory.GetFiles(Path.GetDirectoryName(source)!) : [source];
            foreach (var file in files)
            {
                var key = file.Replace('\\', '/');
                key = "Content/" + key[(key.IndexOf("/StockMaps/", StringComparison.Ordinal) + 1)..];
                assets[key] = File.ReadAllBytes(file);
            }
        }
        try
        {
            ContentRoot.Initialize("Content");
            BrowserContentCatalog.SetBinaryAssets(assets);
            SimpleLevelFactory.ClearCachedCatalog();
            foreach (var variant in ClassicStockMapCatalog.Variants)
            {
                var entry = Assert.Single(SimpleLevelFactory.GetAvailableSourceLevels(), e => e.Name == variant.LevelName);
                Assert.Equal(ContentRoot.GetPath(variant.RelativeSourcePath.Split('/')), entry.RoomSourcePath);
                var level = SimpleLevelFactory.CreateImportedLevel(variant.LevelName);
                Assert.NotNull(level);
                Assert.Equal(variant.LevelName, level.Name);
                Assert.Equal(variant.Mode, level.Mode);
                Assert.NotEmpty(level.Solids);
                Assert.NotEmpty(level.RedSpawns);
                Assert.NotEmpty(level.BlueSpawns);
                Assert.True(level.IntelBases.Count > 0 || level.RoomObjects.Any(o => o.Type == RoomObjectType.ControlPoint));
                var world = new SimulationWorld(new() { EnableLocalDummies = false });
                Assert.True(world.MapLifecycle.TryLoadLevel(variant.LevelName, 1, preservePlayerStats: false));
                for (var tick = 0; tick < 60; tick++) world.AdvanceOneTick();
            }
        }
        finally
        {
            BrowserContentCatalog.SetBinaryAssets([]);
            ContentRoot.Initialize(originalRoot);
            SimpleLevelFactory.ClearCachedCatalog();
        }
    }
}
