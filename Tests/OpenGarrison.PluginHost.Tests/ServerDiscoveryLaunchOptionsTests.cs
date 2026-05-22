using System;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ServerDiscoveryLaunchOptionsTests
{
    [Fact]
    public void DefaultSettingsUseHttpRegistryDiscovery()
    {
        var options = ServerLaunchOptions.Load([], _ => new ServerSettings());

        Assert.True(options.UseLobbyServer);
        Assert.False(options.UseLegacyLobbyServer);
        Assert.Equal(OpenGarrisonPreferencesDocument.DefaultLobbyHost, options.LobbyHost);
        Assert.Equal(new Uri(OpenGarrisonPreferencesDocument.DefaultLobbyHost), options.RegistryUrl);
    }

    [Fact]
    public void PackagedLegacyLobbyHostMigratesToHttpRegistryDiscovery()
    {
        var options = ServerLaunchOptions.Load(
            [],
            _ => new ServerSettings
            {
                LobbyHost = "OpenGarrison.game-host.org",
                LobbyPort = 29942,
            });

        Assert.True(options.UseLobbyServer);
        Assert.False(options.UseLegacyLobbyServer);
        Assert.Equal(OpenGarrisonPreferencesDocument.DefaultLobbyHost, options.LobbyHost);
        Assert.Equal(new Uri(OpenGarrisonPreferencesDocument.DefaultLobbyHost), options.RegistryUrl);
    }

    [Fact]
    public void NoLobbyDisablesHttpRegistryDiscovery()
    {
        var options = ServerLaunchOptions.Load(["--no-lobby"], _ => new ServerSettings());

        Assert.False(options.UseLobbyServer);
        Assert.False(options.UseLegacyLobbyServer);
        Assert.Null(options.RegistryUrl);
    }

    [Fact]
    public void CustomNonHttpLobbyHostStillUsesLegacyLobbyDiscovery()
    {
        var options = ServerLaunchOptions.Load(
            [],
            _ => new ServerSettings
            {
                LobbyHost = "legacy-lobby.example.invalid",
                LobbyPort = 29942,
            });

        Assert.True(options.UseLobbyServer);
        Assert.True(options.UseLegacyLobbyServer);
        Assert.Null(options.RegistryUrl);
    }
}
