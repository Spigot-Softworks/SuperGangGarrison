using Microsoft.Xna.Framework;
using OpenGarrison.Client;
using OpenGarrison.ClientShared;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class PlayerCardAndDeathCamPresentationTests
{

    [Fact]
    public void PlayerCardBioAndMedalRoundTripThroughWireJson()
    {
        var json = PlayerCardProfile.Serialize(new PlayerCardProfile
        {
            Bio = "I SUCK, BUT YOU'RE WORSE",
            Medal = "mercenary",
        });

        var profile = PlayerCardProfile.Deserialize(json);

        Assert.Equal("I SUCK, BUT YOU'RE WORSE", profile.Bio);
        Assert.Equal("mercenary", profile.Medal);
    }

}
