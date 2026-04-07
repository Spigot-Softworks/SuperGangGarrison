using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class GameplayModPackLoaderTests
{
    [Fact]
    public void StockGameplayPackLoadsFromJsonDirectory()
    {
        var pack = StockGameplayModCatalog.Definition;

        Assert.Equal("stock.gg2", pack.Id);
        Assert.Equal("Stock OpenGarrison Gameplay", pack.DisplayName);
        Assert.True(pack.Items.ContainsKey("weapon.scattergun"));
        Assert.True(pack.Classes.ContainsKey("soldier"));
        Assert.Equal("soldier.stock", pack.Classes["soldier"].DefaultLoadoutId);
    }

    [Fact]
    public void StockGameplayPackExposesOwnershipReadyExperimentalItems()
    {
        var eyelander = StockGameplayModCatalog.GetExperimentalDemoknightEyelanderItem();
        var paintrain = StockGameplayModCatalog.GetExperimentalDemoknightPaintrainItem();

        Assert.NotNull(eyelander.Ownership);
        Assert.True(eyelander.Ownership!.TrackOwnership);
        Assert.False(eyelander.Ownership.DefaultGranted);
        Assert.True(eyelander.Ownership.GrantOnAcquire);
        Assert.Equal(ExperimentalDemoknightCatalog.EyelanderItemId, eyelander.Id);

        Assert.NotNull(paintrain.Ownership);
        Assert.True(paintrain.Ownership!.TrackOwnership);
        Assert.False(paintrain.Ownership.DefaultGranted);
        Assert.True(paintrain.Ownership.GrantOnAcquire);
        Assert.Equal(ExperimentalDemoknightCatalog.PaintrainItemId, paintrain.Id);
    }
}
