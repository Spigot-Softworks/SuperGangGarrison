using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class PowerstripStockMapTests
{
    [Fact]
    public void StockPowerstripImportsAllThreePlayableAreas()
    {
        SimpleLevelFactory.ClearCachedCatalog();
        try
        {
            var source = Assert.Single(
                SimpleLevelFactory.GetAvailableSourceLevels(),
                entry => string.Equals(entry.Name, "cp_powerstrip", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(CustomMapSourceKind.Package, source.SourceKind);
            Assert.Equal(GameModeKind.ControlPoint, source.Mode);

            for (var areaIndex = 1; areaIndex <= 3; areaIndex += 1)
            {
                var level = SimpleLevelFactory.CreateImportedLevel("cp_powerstrip", mapAreaIndex: areaIndex);

                Assert.NotNull(level);
                Assert.Equal("cp_powerstrip", level.Name);
                Assert.Equal(areaIndex, level.MapAreaIndex);
                Assert.Equal(3, level.MapAreaCount);
                Assert.NotEmpty(level.RedSpawns);
                Assert.NotEmpty(level.BlueSpawns);
                Assert.Contains(level.RoomObjects, marker => marker.Type == RoomObjectType.ControlPoint);
            }
        }
        finally
        {
            SimpleLevelFactory.ClearCachedCatalog();
        }
    }
}
