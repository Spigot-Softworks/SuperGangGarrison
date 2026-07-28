using System;
using System.IO;
using System.Linq;
using OpenGarrison.Client;
using OpenGarrison.ClientShared;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class SettingsMenuOrganizationTests
{
    [Fact]
    public void HostAdvancedDefinitionsArePartitionedIntoMeaningfulTabs()
    {
        var tabs = new[]
        {
            HostSetupOptionsTab.Match,
            HostSetupOptionsTab.Competitive,
            HostSetupOptionsTab.Teams,
            HostSetupOptionsTab.Bots,
            HostSetupOptionsTab.Simulation,
            HostSetupOptionsTab.Classes,
        };

        var groupedDefinitions = tabs
            .SelectMany(tab => HostSetupServerCvarCatalog.GetDefinitionsForTab(tab))
            .ToArray();

        Assert.Equal(HostSetupServerCvarCatalog.AdvancedDefinitions.Count, groupedDefinitions.Length);
        Assert.Equal(
            HostSetupServerCvarCatalog.AdvancedDefinitions.Count,
            groupedDefinitions.Select(definition => definition.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.DoesNotContain(HostSetupServerCvarCatalog.AdvancedDefinitions, definition => definition.Category == HostSetupOptionsTab.Basic);
        Assert.All(tabs, tab => Assert.NotEmpty(HostSetupServerCvarCatalog.GetDefinitionsForTab(tab)));
    }

    [Fact]
    public void PositionSmoothingSettingRoundTripsThroughClientPreferences()
    {
        var configPath = Path.Combine(
            Path.GetTempPath(),
            "OpenGarrison.PluginHost.Tests",
            Guid.NewGuid().ToString("N"),
            OpenGarrisonPreferencesDocument.DefaultFileName);
        var directory = Path.GetDirectoryName(configPath)!;
        Directory.CreateDirectory(directory);

        try
        {
            var settings = new ClientSettings
            {
                PositionSmoothingEnabled = false,
            };

            settings.Save(configPath);

            var loaded = ClientSettings.Load(configPath);

            Assert.False(loaded.PositionSmoothingEnabled);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
