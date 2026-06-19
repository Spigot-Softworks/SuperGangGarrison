using OpenGarrison.Client.Plugins;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class LocalDamageFlagsTests
{
    [Fact]
    public void LocalDamageFlagsStayAlignedWithCoreDamageEventFlags()
    {
        Assert.Equal((byte)DamageEventFlags.Airshot, (byte)LocalDamageFlags.Airshot);
        Assert.Equal((byte)DamageEventFlags.Evaded, (byte)LocalDamageFlags.Evaded);
        Assert.Equal((byte)DamageEventFlags.GhostDash, (byte)LocalDamageFlags.GhostDash);
        Assert.Equal((byte)DamageEventFlags.CivvieUmbrellaBlock, (byte)LocalDamageFlags.CivvieUmbrellaBlock);
        Assert.Equal((byte)DamageEventFlags.AfterburnTick, (byte)LocalDamageFlags.AfterburnTick);
    }
}
