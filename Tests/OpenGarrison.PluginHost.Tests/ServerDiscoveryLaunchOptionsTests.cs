using System;
using OpenGarrison.Core;
using OpenGarrison.Core.LastToDie;
using OpenGarrison.Protocol;
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
        Assert.Equal(OpenGarrison.Server.SnapshotBudgetMode.GameplayCriticalUntrimmed, options.SnapshotBudgetMode);
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
    public void LegacyApiHostLobbyHostMigratesToCanonicalBackend()
    {
        var options = ServerLaunchOptions.Load(
            [],
            _ => new ServerSettings
            {
                LobbyHost = "https://api.unkind-dev.com/api/servers",
                LobbyPort = 443,
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
    public void MaxPlayersLaunchOptionAllowsFortyPlayableClients()
    {
        var options = ServerLaunchOptions.Load(["--max-players", "40"], _ => new ServerSettings());
        var clamped = ServerLaunchOptions.Load(["--max-players", "41"], _ => new ServerSettings());

        Assert.Equal(40, options.MaxPlayableClients);
        Assert.Equal(40, options.MaxTotalClients);
        Assert.Equal(40, options.MaxSpectatorClients);
        Assert.Equal(SimulationWorld.MaxPlayableNetworkPlayers, clamped.MaxPlayableClients);
        Assert.Equal(SimulationWorld.MaxPlayableNetworkPlayers, clamped.MaxTotalClients);
        Assert.Equal(SimulationWorld.MaxPlayableNetworkPlayers, clamped.MaxSpectatorClients);
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
    public void MapRotationShuffleLaunchAliasesOverrideSettings()
    {
        var fromSettings = ServerLaunchOptions.Load([], _ => new ServerSettings
        {
            MapRotationShuffleEnabled = true,
        });
        var disabled = ServerLaunchOptions.Load(["--no-shuffle-map-rotation"], _ => new ServerSettings
        {
            MapRotationShuffleEnabled = true,
        });
        var enabled = ServerLaunchOptions.Load(["--map-rotation-shuffle"], _ => new ServerSettings
        {
            MapRotationShuffleEnabled = false,
        });

        Assert.True(fromSettings.MapRotationShuffleEnabled);
        Assert.False(disabled.MapRotationShuffleEnabled);
        Assert.True(enabled.MapRotationShuffleEnabled);
    }

    [Fact]
    public void RoundEndTeamRuleLaunchAliasesOverrideSettings()
    {
        var fromSettings = ServerLaunchOptions.Load([], _ => new ServerSettings
        {
            SwitchTeamsAfterRoundEnd = true,
            TeamShuffleAfterWins = 3,
        });
        var switchDisabled = ServerLaunchOptions.Load(["--no-switch-teams-after-round-end"], _ => new ServerSettings
        {
            SwitchTeamsAfterRoundEnd = true,
        });
        var switchEnabled = ServerLaunchOptions.Load(["--switch-teams-after-round-end"], _ => new ServerSettings
        {
            SwitchTeamsAfterRoundEnd = false,
        });
        var shuffleOverride = ServerLaunchOptions.Load(["--team-scramble-after-wins", "7"], _ => new ServerSettings
        {
            TeamShuffleAfterWins = 2,
        });

        Assert.True(fromSettings.SwitchTeamsAfterRoundEnd);
        Assert.Equal(3, fromSettings.TeamShuffleAfterWins);
        Assert.False(switchDisabled.SwitchTeamsAfterRoundEnd);
        Assert.True(switchEnabled.SwitchTeamsAfterRoundEnd);
        Assert.Equal(7, shuffleOverride.TeamShuffleAfterWins);
    }

    [Fact]
    public void SnapshotBudgetLaunchAliasesAreDiagnosticsOnlyOverrides()
    {
        var defaultOptions = ServerLaunchOptions.Load([], _ => new ServerSettings());
        var balanced = ServerLaunchOptions.Load(["--snapshot-balanced"], _ => new ServerSettings());
        var untrimmed = ServerLaunchOptions.Load(["--snapshot-no-trim"], _ => new ServerSettings
        {
            SnapshotBudgetMode = OpenGarrison.Server.SnapshotBudgetMode.Balanced,
        });

        Assert.Equal(OpenGarrison.Server.SnapshotBudgetMode.GameplayCriticalUntrimmed, defaultOptions.SnapshotBudgetMode);
        Assert.Equal(OpenGarrison.Server.SnapshotBudgetMode.Balanced, balanced.SnapshotBudgetMode);
        Assert.Equal(OpenGarrison.Server.SnapshotBudgetMode.GameplayCriticalUntrimmed, untrimmed.SnapshotBudgetMode);
    }

    [Fact]
    public void RegistryCompatibilityLaunchAliasesOverridePackageDefaults()
    {
        var options = ServerLaunchOptions.Load(
            [
                "--build-version",
                "0.5.8.0-beta.1",
                "--release-channel",
                "beta",
            ],
            _ => new ServerSettings());

        Assert.Equal("0.5.8.0-beta.1", options.BuildVersion);
        Assert.Equal("beta", options.ReleaseChannel);
        Assert.Equal($"beta:0.5.8.0-beta.1:{ProtocolVersion.Current}", options.CompatibilityKey);
    }

    [Fact]
    public void RegistryCompatibilityKeyLaunchAliasCanOverrideDerivedKey()
    {
        var options = ServerLaunchOptions.Load(
            [
                "--build-version",
                "0.5.8.0-beta.1",
                "--release-channel",
                "beta",
                "--compatibility-key",
                "beta-netcode-experiment",
            ],
            _ => new ServerSettings());

        Assert.Equal("0.5.8.0-beta.1", options.BuildVersion);
        Assert.Equal("beta", options.ReleaseChannel);
        Assert.Equal("beta-netcode-experiment", options.CompatibilityKey);
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

    [Fact]
    public void LastToDieLaunchClampsCoopToTwoPlayersAndPreservesSolo()
    {
        var coopOptions = ServerLaunchOptions.Load(
            [
                "--max-players", "40",
                "--gameplay-variant", "last-to-die",
                "--last-to-die-difficulty", "hardcore",
                "--last-to-die-seed", "4242",
            ],
            _ => new ServerSettings
            {
                AutoBalanceEnabled = true,
                SwitchTeamsAfterRoundEnd = true,
                CompetitiveReadyUpEnabled = true,
            });
        var soloOptions = ServerLaunchOptions.Load(
            [
                "--max-players", "1",
                "--gameplay-variant", "last-to-die",
            ],
            _ => new ServerSettings());

        Assert.Equal(GameplayVariantKind.LastToDie, coopOptions.GameplayVariant);
        Assert.Equal(LastToDieDifficulty.Hardcore, coopOptions.LastToDieDifficulty);
        Assert.Equal(4242UL, coopOptions.LastToDieSeed);
        Assert.Equal(2, coopOptions.MaxPlayableClients);
        Assert.Equal(2, coopOptions.MaxTotalClients);
        Assert.Equal(0, coopOptions.MaxSpectatorClients);
        Assert.False(coopOptions.AutoBalanceEnabled);
        Assert.False(coopOptions.SwitchTeamsAfterRoundEnd);
        Assert.False(coopOptions.CompetitiveReadyUpEnabled);
        Assert.False(coopOptions.UseLobbyServer);
        Assert.Equal(GameplayVariantKind.LastToDie, soloOptions.GameplayVariant);
        Assert.Equal(1, soloOptions.MaxPlayableClients);
        Assert.Equal(1, soloOptions.MaxTotalClients);
        Assert.Equal(0, soloOptions.MaxSpectatorClients);
    }

    [Fact]
    public void LastToDieIgnoresCustomMapAndCustomRotationSettings()
    {
        var options = ServerLaunchOptions.Load(
            ["--gameplay-variant", "last-to-die"],
            _ => new ServerSettings
            {
                RequestedMap = "lake",
                MapRotationFile = "custom-rotation.txt",
                HostDefaults = new OpenGarrisonHostSettings
                {
                    StockMapRotation =
                    [
                        new OpenGarrisonMapRotationEntry
                        {
                            LevelName = "lake",
                            IniKey = "lake",
                            IsCustomMap = true,
                            Order = 1,
                        },
                    ],
                },
            });

        Assert.Null(options.RequestedMap);
        Assert.Null(options.MapRotationFile);
        Assert.NotEmpty(options.StockMapRotation);
        Assert.All(
            options.StockMapRotation,
            map => Assert.True(OpenGarrisonStockMapCatalog.TryGetDefinition(map, out _)));
        Assert.DoesNotContain(options.StockMapRotation, map =>
            string.Equals(map, "lake", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LastToDieRegistryPublicationRequiresExplicitLaunchOptIn()
    {
        var options = ServerLaunchOptions.Load(
            ["--gameplay-variant", "last-to-die", "--lobby"],
            _ => new ServerSettings { UseLobbyServer = false });

        Assert.Equal(GameplayVariantKind.LastToDie, options.GameplayVariant);
        Assert.True(options.UseLobbyServer);
    }

    [Fact]
    public void HostedLastToDieLaunchArgumentsCarryVariantDifficultyAndSeed()
    {
        var target = new OpenGarrison.Client.HostedServerLaunchTarget(
            "OG2.Server.exe",
            string.Empty,
            AppContext.BaseDirectory);
        var launchOptions = CreateHostedServerLaunchOptions(secondaryAbilitiesEnabled: true) with
        {
            GameplayVariant = GameplayVariantKind.LastToDie,
            LastToDieDifficulty = LastToDieDifficulty.Hardcore,
            LastToDieSeed = 4242UL,
        };

        var arguments = OpenGarrison.Client.HostedServerBootstrapper.BuildLaunchArguments(
            target,
            launchOptions);

        Assert.Contains("--gameplay-variant last-to-die", arguments);
        Assert.Contains("--last-to-die-difficulty hardcore", arguments);
        Assert.Contains("--last-to-die-seed 4242", arguments);
    }

    [Fact]
    public void DirectLastToDieLaunchProfileIsPrivateAndTwoPlayer()
    {
        var options = OpenGarrison.Client.HostedServerLaunchOptions.CreateLastToDie(
            "server.ini",
            "Last To Die Co-op",
            8190,
            LastToDieDifficulty.Hardcore,
            seed: 4242UL);

        Assert.Equal(GameplayVariantKind.LastToDie, options.GameplayVariant);
        Assert.Equal(LastToDieDifficulty.Hardcore, options.LastToDieDifficulty);
        Assert.Equal(2, options.MaxPlayers);
        Assert.False(options.LobbyAnnounce);
        Assert.False(options.AutoBalance);
        Assert.True(options.SecondaryAbilitiesEnabled);
        Assert.Null(options.RequestedMap);
        Assert.Null(options.MapRotationFile);
    }

    [Fact]
    public void HostedRelaySecretUsesChildEnvironmentAndNeverCommandLine()
    {
        const string relayUrl = "wss://relay.example.com/api/relay/ws/run/host?token=host-secret";
        var target = new OpenGarrison.Client.HostedServerLaunchTarget(
            "OG2.Server.exe",
            string.Empty,
            AppContext.BaseDirectory);
        var options = OpenGarrison.Client.HostedServerLaunchOptions.CreateLastToDie(
            "server.ini",
            "Last To Die Co-op",
            8190,
            LastToDieDifficulty.Standard) with
        {
            RelayHostUrl = relayUrl,
        };
        var startInfo = new System.Diagnostics.ProcessStartInfo();

        OpenGarrison.Client.HostedServerRuntimeController.ApplyRelayEnvironment(startInfo, options.RelayHostUrl);
        var arguments = OpenGarrison.Client.HostedServerBootstrapper.BuildLaunchArguments(target, options);

        Assert.Equal(relayUrl, startInfo.Environment["OPENGARRISON_RELAY_HOST_URL"]);
        Assert.DoesNotContain("host-secret", arguments, StringComparison.Ordinal);
        Assert.DoesNotContain("relay", arguments, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(GameplayVariantKind.LastToDie, false, true)]
    [InlineData(GameplayVariantKind.Standard, false, false)]
    [InlineData(GameplayVariantKind.Standard, true, true)]
    public void LastToDieAlwaysOffersClientPrediction(
        GameplayVariantKind variant,
        bool configuredEnabled,
        bool expected)
    {
        Assert.Equal(expected, GameServer.ResolveLocalPredictionEnabled(variant, configuredEnabled));
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
