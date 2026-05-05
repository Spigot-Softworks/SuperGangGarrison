using System;
using System.IO;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ServerSettingsTests
{
    [Fact]
    public void SaveAndLoadRoundTripBotAutofillSettings()
    {
        var configPath = Path.Combine(
            Path.GetTempPath(),
            "OpenGarrison.PluginHost.Tests",
            Guid.NewGuid().ToString("N"),
            OpenGarrisonPreferencesDocument.DefaultFileName);
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);

        var settings = new ServerSettings
        {
            ServerName = "Bot Server",
            RconPassword = "rcon-secret",
            BotAutofillEnabled = true,
            BotAutofillMinPlayers = 10,
            BotAutofillPerTeam = 5,
        };

        settings.Save(configPath);

        var preferences = OpenGarrisonPreferencesDocument.Load(configPath);
        Assert.True(preferences.HostSettings.BotAutofillEnabled);
        Assert.Equal(10, preferences.HostSettings.BotAutofillMinPlayers);
        Assert.Equal(5, preferences.HostSettings.BotAutofillPerTeam);

        var reloaded = ServerSettings.Load(configPath);
        Assert.True(reloaded.BotAutofillEnabled);
        Assert.Equal(10, reloaded.BotAutofillMinPlayers);
        Assert.Equal(5, reloaded.BotAutofillPerTeam);
    }
}
