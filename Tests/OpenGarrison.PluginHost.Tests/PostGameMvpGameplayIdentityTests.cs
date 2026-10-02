using System.Reflection;
using OpenGarrison.Client;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class PostGameMvpGameplayIdentityTests
{
    [Fact]
    public void QuoteCurlyUsesImpostorTauntArtWhileCivilianKeepsLegacyMvpArt()
    {
        Assert.Equal(
            "ImpostorRedTauntS",
            GetSpriteName(PlayerTeam.Red, PlayerClass.Quote, winner: true, gameplayClassId: "quote"));
        Assert.Equal(
            "ImpostorBlueTauntS",
            GetSpriteName(PlayerTeam.Blue, PlayerClass.Quote, winner: false, gameplayClassId: "plugin.quote-curly.quote"));
        Assert.Equal(
            "MvpRedQuoteWinnerS",
            GetSpriteName(PlayerTeam.Red, PlayerClass.Quote, winner: true, gameplayClassId: "civilian"));
        Assert.Equal(
            "MvpBlueQuoteS",
            GetSpriteName(PlayerTeam.Blue, PlayerClass.Quote, winner: false, gameplayClassId: null));
    }

    private static string GetSpriteName(
        PlayerTeam team,
        PlayerClass playerClass,
        bool winner,
        string? gameplayClassId)
    {
        var method = typeof(Game1).GetMethod(
            "GetPostGameMvpArtSpriteNameForGameplayClassId",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return (string)method!.Invoke(null, [team, playerClass, winner, gameplayClassId])!;
    }
}
