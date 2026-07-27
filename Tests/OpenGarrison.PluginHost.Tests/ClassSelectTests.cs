using OpenGarrison.Client;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ClassSelectTests
{
    [Fact]
    public void CivilianShortcutTargetsStockCivilianInsteadOfPreferredPluginClass()
    {
        Assert.Equal("civilian", Game1.ResolveClassSelectCivilianShortcutGameplayClassId());
    }
}
