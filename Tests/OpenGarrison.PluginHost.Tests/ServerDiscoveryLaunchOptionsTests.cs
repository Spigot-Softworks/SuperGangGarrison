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

    [Fact]
    public void SpecialAbilityLaunchAliasesOverrideSettings()
    {
        var disabled = ServerLaunchOptions.Load(["--no-special-abilities"], _ => new ServerSettings
        {
            SecondaryAbilitiesEnabled = true,
        });
        var enabled = ServerLaunchOptions.Load(["--special-abilities"], _ => new ServerSettings
        {
            SecondaryAbilitiesEnabled = false,
        });
        var legacyDisabled = ServerLaunchOptions.Load(["--no-secondary-abilities"], _ => new ServerSettings
        {
            SecondaryAbilitiesEnabled = true,
        });

        Assert.False(disabled.SecondaryAbilitiesEnabled);
        Assert.True(enabled.SecondaryAbilitiesEnabled);
        Assert.False(legacyDisabled.SecondaryAbilitiesEnabled);
    }

    [Fact]
    public void HostedServerLaunchArgumentsUseSpecialAbilitySwitches()
    {
        var target = new OpenGarrison.Client.HostedServerLaunchTarget("OG2.Server.exe", string.Empty, AppContext.BaseDirectory);
        var enabledArguments = OpenGarrison.Client.HostedServerBootstrapper.BuildLaunchArguments(
            target,
            CreateHostedServerLaunchOptions(secondaryAbilitiesEnabled: true));
        var disabledArguments = OpenGarrison.Client.HostedServerBootstrapper.BuildLaunchArguments(
            target,
            CreateHostedServerLaunchOptions(secondaryAbilitiesEnabled: false));

        Assert.Contains("--special-abilities", enabledArguments);
        Assert.DoesNotContain("--secondary-abilities", enabledArguments);
        Assert.Contains("--no-special-abilities", disabledArguments);
        Assert.DoesNotContain("--no-secondary-abilities", disabledArguments);
    }

    private static OpenGarrison.Client.HostedServerLaunchOptions CreateHostedServerLaunchOptions(bool secondaryAbilitiesEnabled)
    {
        return new OpenGarrison.Client.HostedServerLaunchOptions(
            ConfigPath: "server.ini",
            ServerName: "Test Server",
            Port: 8190,
            MaxPlayers: 10,
            Password: string.Empty,
            RconPassword: string.Empty,
            TimeLimitMinutes: 15,
            CapLimit: 5,
            RespawnSeconds: 5,
            LobbyAnnounce: false,
            AutoBalance: true,
            SecondaryAbilitiesEnabled: secondaryAbilitiesEnabled,
            RequestedMap: "Harvest",
            MapRotationFile: null);
    }
}
