using System;
using System.IO;
using System.Text.Json;
using OpenGarrison.ClientShared;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class CameraPanningSettingsTests
{
    [Fact]
    public void CameraPanningDefaultsToEnabledAcrossSettingsModels()
    {
        Assert.True(OpenGarrisonPreferencesDocument.DefaultCameraPanningEnabled);
        Assert.True(new OpenGarrisonPreferencesDocument().CameraPanningEnabled);
        Assert.True(new ClientSettings().CameraPanningEnabled);
    }

    [Fact]
    public void MissingCameraPanningPreferenceDefaultsToEnabled()
    {
        var directory = Path.Combine(Path.GetTempPath(), "og2-camera-panning-settings-tests", Guid.NewGuid().ToString("N"));
        var settingsPath = Path.Combine(directory, ClientSettings.DefaultFileName);
        Directory.CreateDirectory(directory);

        try
        {
            File.WriteAllText(settingsPath, "[Settings]" + Environment.NewLine + "PlayerName=Viewer" + Environment.NewLine);

            Assert.True(ClientSettings.Load(settingsPath).CameraPanningEnabled);
            Assert.True(OpenGarrisonPreferencesDocument.Load(settingsPath).CameraPanningEnabled);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void CameraPanningDisabledRoundTripsThroughDesktopPreferences()
    {
        var directory = Path.Combine(Path.GetTempPath(), "og2-camera-panning-settings-tests", Guid.NewGuid().ToString("N"));
        var settingsPath = Path.Combine(directory, ClientSettings.DefaultFileName);
        Directory.CreateDirectory(directory);

        try
        {
            new ClientSettings { CameraPanningEnabled = false }.Save(settingsPath);

            Assert.False(ClientSettings.Load(settingsPath).CameraPanningEnabled);
            Assert.False(OpenGarrisonPreferencesDocument.Load(settingsPath).CameraPanningEnabled);
            Assert.Contains("Camera Panning=0", File.ReadAllText(settingsPath), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void CameraPanningIsIncludedInBrowserSettingsPayload()
    {
        var json = JsonSerializer.Serialize(new ClientSettings { CameraPanningEnabled = false });
        using var document = JsonDocument.Parse(json);

        Assert.False(document.RootElement.GetProperty(nameof(ClientSettings.CameraPanningEnabled)).GetBoolean());
        Assert.True(JsonSerializer.Deserialize<ClientSettings>("{}")!.CameraPanningEnabled);
    }
}
