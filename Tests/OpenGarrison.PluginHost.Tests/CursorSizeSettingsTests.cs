using System;
using System.IO;
using OpenGarrison.ClientShared;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class CursorSizeSettingsTests
{
    [Theory]
    [InlineData(0, 50)]
    [InlineData(50, 50)]
    [InlineData(59, 50)]
    [InlineData(100, 100)]
    [InlineData(149, 140)]
    [InlineData(250, 250)]
    [InlineData(999, 250)]
    public void CursorSizePercentIsClampedToTenPercentSteps(int input, int expected)
    {
        Assert.Equal(expected, ClientSettings.NormalizeCursorSizePercent(input));
    }

    [Fact]
    public void CursorSizePercentRoundTripsThroughClientPreferences()
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
                CursorSizePercent = ClientSettings.CursorSizeMaxPercent,
            };

            settings.Save(configPath);

            var loaded = ClientSettings.Load(configPath);

            Assert.Equal(ClientSettings.CursorSizeMaxPercent, loaded.CursorSizePercent);
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
