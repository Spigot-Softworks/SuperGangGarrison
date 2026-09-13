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
    public void RestrictedPracticeRosterUsesOriginalsInClassicSectionAndDesktopKeepsItsRoster()
    {
        var type = typeof(Game1).GetNestedType("PracticeSetupState", BindingFlags.NonPublic)!;
        var method = type.GetMethod("BuildMapEntriesForEdition", BindingFlags.Public | BindingFlags.Static)!;
        var entries = (IList)method.Invoke(null, [true])!;
        var desktop = ((IEnumerable)method.Invoke(null, [false])!).Cast<object>().ToArray();
        var state = Activator.CreateInstance(type, nonPublic: true)!;
        type.GetProperty("MapEntries")!.SetValue(state, entries);
        for (var i = 0; i < 5; i++)
        {
            var variant = ClassicStockMapCatalog.Variants[i];
            Assert.Equal(variant.LevelName, entries[i]!.GetType().GetProperty("LevelName")!.GetValue(entries[i]));
            Assert.Equal(variant.ReplacedLevelName, desktop[i].GetType().GetProperty("LevelName")!.GetValue(desktop[i]));
            Assert.False((bool)entries[i]!.GetType().GetProperty("IsCustomMap")!.GetValue(entries[i])!);
            type.GetProperty("MapIndex")!.SetValue(state, i);
            type.GetMethod("OpenMapBrowser")!.Invoke(state, null);
            Assert.Equal("Classic", type.GetProperty("MapBrowserSection")!.GetValue(state)!.ToString());
            var visible = ((IEnumerable)type.GetMethod("GetAvailableMapsForDisplay")!.Invoke(state, null)!).Cast<OpenGarrisonMapRotationEntry>();
            Assert.Contains(visible, entry => entry.LevelName == variant.LevelName);
        }
    }

    [Theory]
    [InlineData(640, 480)]
    [InlineData(960, 540)]
    [InlineData(1920, 1080)]
    public void RestrictedMapTabsFillTheAvailableWidthWithoutAnInvisibleSggButton(int width, int height)
    {
        var normal = PracticeMapsMenuLayoutCalculator.Create(width, height);
        var restricted = PracticeMapsMenuLayoutCalculator.Create(width, height, showSuperGangGarrison: false);
        Assert.True(restricted.SuperGangGarrisonButtonBounds.IsEmpty);
        Assert.Equal(normal.SuperGangGarrisonButtonBounds.Left, restricted.ClassicMapsButtonBounds.Left);
        Assert.Equal(normal.CustomMapsButtonBounds.Right, restricted.CustomMapsButtonBounds.Right);
        Assert.True(restricted.ClassicMapsButtonBounds.Right < restricted.CustomMapsButtonBounds.Left);
    }

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
                Assert.True(world.TryLoadLevel(variant.LevelName, 1, preservePlayerStats: false));
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
