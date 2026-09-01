using System;
using System.IO;
using OpenGarrison.ClientShared;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ReplaySettingsTests
{
    [Fact]
    public void AlwaysRecordGamesDefaultsOffWhenPreferenceIsMissing()
    {
        var directory = Path.Combine(Path.GetTempPath(), "og2-replay-settings-tests", Guid.NewGuid().ToString("N"));
        var settingsPath = Path.Combine(directory, ClientSettings.DefaultFileName);
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(settingsPath, "[Settings]" + Environment.NewLine + "PlayerName=Viewer" + Environment.NewLine);

            Assert.False(ClientSettings.Load(settingsPath).AlwaysRecordGames);
            Assert.False(OpenGarrisonPreferencesDocument.Load(settingsPath).AlwaysRecordGames);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void AlwaysRecordGamesRoundTripsThroughClientPreferences()
    {
        var directory = Path.Combine(Path.GetTempPath(), "og2-replay-settings-tests", Guid.NewGuid().ToString("N"));
        var settingsPath = Path.Combine(directory, ClientSettings.DefaultFileName);
        Directory.CreateDirectory(directory);
        try
        {
            new ClientSettings { AlwaysRecordGames = true }.Save(settingsPath);

            Assert.True(ClientSettings.Load(settingsPath).AlwaysRecordGames);
            Assert.True(OpenGarrisonPreferencesDocument.Load(settingsPath).AlwaysRecordGames);
            Assert.Contains("Always Record Games=1", File.ReadAllText(settingsPath), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
