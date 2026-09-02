using OpenGarrison.Client;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class BuffBannerReadyCueTrackerTests
{
    [Fact]
    public void PlaysOnceWhenObservedChargeFirstReachesReady()
    {
        var tracker = new BuffBannerReadyCueTracker();

        Assert.False(tracker.Observe(eligible: true, chargeDamage: 0, maxChargeDamage: 400));
        Assert.False(tracker.Observe(eligible: true, chargeDamage: 399, maxChargeDamage: 400));
        Assert.True(tracker.Observe(eligible: true, chargeDamage: 400, maxChargeDamage: 400));
        Assert.False(tracker.Observe(eligible: true, chargeDamage: 400, maxChargeDamage: 400));
    }

    [Fact]
    public void DoesNotReplayForMinorReconciliationButRearmsAfterChargeIsConsumed()
    {
        var tracker = new BuffBannerReadyCueTracker();

        Assert.False(tracker.Observe(eligible: true, chargeDamage: 0, maxChargeDamage: 400));
        Assert.True(tracker.Observe(eligible: true, chargeDamage: 400, maxChargeDamage: 400));
        Assert.False(tracker.Observe(eligible: true, chargeDamage: 399, maxChargeDamage: 400));
        Assert.False(tracker.Observe(eligible: true, chargeDamage: 400, maxChargeDamage: 400));
        Assert.False(tracker.Observe(eligible: true, chargeDamage: 0, maxChargeDamage: 400));
        Assert.True(tracker.Observe(eligible: true, chargeDamage: 400, maxChargeDamage: 400));
    }

    [Fact]
    public void DoesNotPlayWhenFirstObservedAlreadyReadyOrWhileIneligible()
    {
        var tracker = new BuffBannerReadyCueTracker();

        Assert.False(tracker.Observe(eligible: false, chargeDamage: 0, maxChargeDamage: 400));
        Assert.False(tracker.Observe(eligible: true, chargeDamage: 400, maxChargeDamage: 400));
        tracker.Reset();
        Assert.False(tracker.Observe(eligible: true, chargeDamage: 400, maxChargeDamage: 400));
    }
}
