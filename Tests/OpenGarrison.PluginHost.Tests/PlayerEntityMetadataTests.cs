using System.Linq;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class PlayerEntityMetadataTests
{
    [Fact]
    public void SetDisplayNameSanitizesHashesAndTruncatesToMaxLength()
    {
        var player = new PlayerEntity(1, CharacterClassCatalog.Scout, "Initial");

        player.SetDisplayName("##VeryLongPlayerNameThatKeepsGoing");

        Assert.Equal("VeryLongPlayerNameTh", player.DisplayName);
    }

    [Fact]
    public void SetDisplayNameFallsBackToDefaultWhenEmptyAfterSanitization()
    {
        var player = new PlayerEntity(1, CharacterClassCatalog.Scout, "Initial");

        player.SetDisplayName("###");

        Assert.Equal("Player", player.DisplayName);
    }

    [Fact]
    public void ReplicatedStateSupportsTypedRoundTripAndKindChecks()
    {
        var player = new PlayerEntity(1, CharacterClassCatalog.Scout, "Test");

        Assert.True(player.SetReplicatedStateInt("plugin.score", "value", 42));
        Assert.True(player.TryGetReplicatedStateInt("plugin.score", "value", out var intValue));
        Assert.Equal(42, intValue);
        Assert.False(player.TryGetReplicatedStateFloat("plugin.score", "value", out _));
        Assert.False(player.TryGetReplicatedStateBool("plugin.score", "value", out _));
    }

    [Fact]
    public void ReplicatedStateRejectsInvalidIdentifiers()
    {
        var player = new PlayerEntity(1, CharacterClassCatalog.Scout, "Test");

        Assert.False(player.SetReplicatedStateBool("", "value", true));
        Assert.False(player.SetReplicatedStateBool("plugin", "   ", true));
        Assert.Empty(player.GetReplicatedStateEntries());
    }

    [Fact]
    public void ReplicatedStateOrdersEntriesAndClearsByKey()
    {
        var player = new PlayerEntity(1, CharacterClassCatalog.Scout, "Test");

        Assert.True(player.SetReplicatedStateBool("z.plugin", "beta", true));
        Assert.True(player.SetReplicatedStateInt("a.plugin", "zeta", 1));
        Assert.True(player.SetReplicatedStateFloat("a.plugin", "alpha", 1.5f));

        var entries = player.GetReplicatedStateEntries();

        Assert.Equal(
            ["a.plugin::alpha", "a.plugin::zeta", "z.plugin::beta"],
            entries.Select(entry => $"{entry.OwnerId}::{entry.Key}").ToArray());

        Assert.True(player.ClearReplicatedState("a.plugin", "zeta"));
        Assert.False(player.TryGetReplicatedStateInt("a.plugin", "zeta", out _));
    }

    [Fact]
    public void ReplicatedStateEnforcesEntryLimitButAllowsUpdatingExistingEntry()
    {
        var player = new PlayerEntity(1, CharacterClassCatalog.Scout, "Test");

        for (var index = 0; index < 16; index += 1)
        {
            Assert.True(player.SetReplicatedStateInt($"plugin.{index}", "value", index));
        }

        Assert.False(player.SetReplicatedStateInt("plugin.overflow", "value", 99));
        Assert.True(player.SetReplicatedStateInt("plugin.0", "value", 100));
        Assert.True(player.TryGetReplicatedStateInt("plugin.0", "value", out var updatedValue));
        Assert.Equal(100, updatedValue);
        Assert.Equal(16, player.GetReplicatedStateEntries().Count);
    }
}
