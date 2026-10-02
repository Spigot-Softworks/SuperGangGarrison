using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using OpenGarrison.Client;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ScoreboardVisibleRankingTests
{
    private static readonly MethodInfo CompareScoreboardPlayers = typeof(Game1).GetMethod(
        "CompareScoreboardPlayers",
        BindingFlags.Instance | BindingFlags.NonPublic)!;

    [Fact]
    public void HalfPointChangesKeepRowsStableUntilDisplayedIntegerScoreChanges()
    {
        var game = (Game1)RuntimeHelpers.GetUninitializedObject(typeof(Game1));
        var alice = new PlayerEntity(1, CharacterClassCatalog.Scout, "Alice");
        var bob = new PlayerEntity(2, CharacterClassCatalog.Scout, "Bob");
        alice.AddPoints(5f);
        bob.AddPoints(5f);

        Assert.Equal(new[] { alice, bob }, Sort(game, bob, alice));

        // Assist points are fractional, but the scoreboard still displays both
        // totals as 5. Their order should continue to come from stable identity.
        bob.AddPoints(0.5f);
        Assert.Equal(new[] { alice, bob }, Sort(game, bob, alice));

        // Once the displayed score becomes 6, its legitimate rank change appears.
        bob.AddPoints(0.5f);
        Assert.Equal(new[] { bob, alice }, Sort(game, alice, bob));
    }

    private static PlayerEntity[] Sort(Game1 game, params PlayerEntity[] players)
    {
        Array.Sort(players, (left, right) => (int)CompareScoreboardPlayers.Invoke(game, [left, right])!);
        return players;
    }
}
