using System;
using System.IO;
using OpenGarrison.Core;
using OpenGarrison.Server;
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
            MapRotationShuffleEnabled = true,
            MapRotationAdvanceMode = MapRotationAdvanceMode.RoundCount,
            MapRotationRounds = 3,
            MapRotationMinutes = 20,
            SwitchTeamsAfterRoundEnd = true,
            TeamShuffleAfterWins = 4,
            BotAutofillEnabled = true,
            BotAutofillMinPlayers = 10,
            BotAutofillPerTeam = 5,
            SnapshotBudgetMode = SnapshotBudgetMode.Balanced,
        };

        settings.Save(configPath);

        var savedText = File.ReadAllText(configPath);
        Assert.DoesNotContain("SnapshotBudgetMode", savedText);

        var preferences = OpenGarrisonPreferencesDocument.Load(configPath);
        Assert.True(preferences.HostSettings.MapRotationShuffleEnabled);
        Assert.Equal(MapRotationAdvanceMode.RoundCount, preferences.HostSettings.MapRotationAdvanceMode);
        Assert.Equal(3, preferences.HostSettings.MapRotationRounds);
        Assert.Equal(20, preferences.HostSettings.MapRotationMinutes);
        Assert.True(preferences.HostSettings.SwitchTeamsAfterRoundEnd);
        Assert.Equal(4, preferences.HostSettings.TeamShuffleAfterWins);
        Assert.True(preferences.HostSettings.BotAutofillEnabled);
        Assert.Equal(10, preferences.HostSettings.BotAutofillMinPlayers);
        Assert.Equal(5, preferences.HostSettings.BotAutofillPerTeam);
        Assert.Equal(SnapshotBudgetModeParser.GameplayCriticalUntrimmedName, preferences.SnapshotBudgetMode);

        var reloaded = ServerSettings.Load(configPath);
        Assert.True(reloaded.MapRotationShuffleEnabled);
        Assert.Equal(MapRotationAdvanceMode.RoundCount, reloaded.MapRotationAdvanceMode);
        Assert.Equal(3, reloaded.MapRotationRounds);
        Assert.Equal(20, reloaded.MapRotationMinutes);
        Assert.True(reloaded.SwitchTeamsAfterRoundEnd);
        Assert.Equal(4, reloaded.TeamShuffleAfterWins);
        Assert.True(reloaded.BotAutofillEnabled);
        Assert.Equal(10, reloaded.BotAutofillMinPlayers);
        Assert.Equal(5, reloaded.BotAutofillPerTeam);
        Assert.Equal(SnapshotBudgetMode.GameplayCriticalUntrimmed, reloaded.SnapshotBudgetMode);
    }

    [Fact]
    public void LegacyBalancedSnapshotBudgetConfigDoesNotOverrideDefault()
    {
        var configPath = Path.Combine(
            Path.GetTempPath(),
            "OpenGarrison.PluginHost.Tests",
            Guid.NewGuid().ToString("N"),
            OpenGarrisonPreferencesDocument.DefaultFileName);
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);

        File.WriteAllText(
            configPath,
            """
            [Server.Advanced]
            SnapshotBudgetMode=Balanced
            """);

        var reloaded = ServerSettings.Load(configPath);

        Assert.Equal(SnapshotBudgetMode.GameplayCriticalUntrimmed, reloaded.SnapshotBudgetMode);
    }

    [Fact]
    public void HostSettingsPreferSpecialAbilitiesKeyOverLegacySecondaryAbilitiesKey()
    {
        var configPath = Path.Combine(
            Path.GetTempPath(),
            "OpenGarrison.PluginHost.Tests",
            Guid.NewGuid().ToString("N"),
            OpenGarrisonPreferencesDocument.DefaultFileName);
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);

        File.WriteAllText(
            configPath,
            """
            [Server]
            SpecialAbilities=0
            SecondaryAbilities=1
            """);

        var preferences = OpenGarrisonPreferencesDocument.Load(configPath);

        Assert.False(preferences.HostSettings.SecondaryAbilitiesEnabled);
    }
}
