using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class TeleportMetadataTests
{
    [Theory]
    [InlineData("all", TeleportTeamFilter.All)]
    [InlineData("red", TeleportTeamFilter.Red)]
    [InlineData("blue", TeleportTeamFilter.Blue)]
    [InlineData(null, TeleportTeamFilter.All)]
    public void TryParseTeamFilterRecognizesValues(string? value, TeleportTeamFilter expected)
    {
        Assert.True(TeleportMetadata.TryParseTeamFilter(value, out var filter));
        Assert.Equal(expected, filter);
    }

    [Fact]
    public void CycleTeamPropertyValueRotatesAllRedBlue()
    {
        Assert.Equal("red", TeleportMetadata.CycleTeamPropertyValue("all"));
        Assert.Equal("blue", TeleportMetadata.CycleTeamPropertyValue("red"));
        Assert.Equal("all", TeleportMetadata.CycleTeamPropertyValue("blue"));
    }

    [Theory]
    [InlineData(TeleportTeamFilter.All, PlayerTeam.Red, true)]
    [InlineData(TeleportTeamFilter.All, PlayerTeam.Blue, true)]
    [InlineData(TeleportTeamFilter.Red, PlayerTeam.Red, true)]
    [InlineData(TeleportTeamFilter.Red, PlayerTeam.Blue, false)]
    [InlineData(TeleportTeamFilter.Blue, PlayerTeam.Blue, true)]
    [InlineData(TeleportTeamFilter.Blue, PlayerTeam.Red, false)]
    public void AllowsTeamFiltersByPlayerTeam(TeleportTeamFilter filter, PlayerTeam team, bool expected)
    {
        Assert.Equal(expected, TeleportMetadata.AllowsTeam(filter, team));
    }

    [Fact]
    public void ParseZoneConfigurationRequiresLinkedExit()
    {
        var exit = new RoomObjectMarker(
            RoomObjectType.TeleportExit,
            100f,
            120f,
            TeleportMetadata.ExitMarkerSize,
            TeleportMetadata.ExitMarkerSize,
            "spawnS",
            SourceName: TeleportMetadata.TeleportExitEntityType);
        var roomObjects = new List<RoomObjectMarker> { exit };
        var exitRef = MapLogicEntityReference.FormatEntityRef(TeleportMetadata.TeleportExitEntityType, 100f, 120f);

        var missingExit = TeleportMetadata.ParseZoneConfiguration(
            new Dictionary<string, string> { [TeleportMetadata.TeamPropertyKey] = "all" },
            roomObjects);
        Assert.False(missingExit.HasExit);

        var linkedExit = TeleportMetadata.ParseZoneConfiguration(
            new Dictionary<string, string>
            {
                [TeleportMetadata.TeamPropertyKey] = "red",
                [TeleportMetadata.TeleportExitPropertyKey] = exitRef,
            },
            roomObjects);
        Assert.True(linkedExit.HasExit);
        Assert.Equal(TeleportTeamFilter.Red, linkedExit.TeamFilter);
        Assert.Equal(exit.CenterX, linkedExit.ExitX);
        Assert.Equal(exit.CenterY, linkedExit.ExitY);
    }
}
