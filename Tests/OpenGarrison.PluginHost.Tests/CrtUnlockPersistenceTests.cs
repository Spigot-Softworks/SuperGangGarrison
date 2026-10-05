using System;
using System.IO;
using OpenGarrison.ClientShared;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class CrtUnlockPersistenceTests
{
    [Fact]
    public void ExplicitUnlockPersistsWhileCrtFilterIsOff()
    {
        var (path, directory) = CreateTemporarySettingsPath();
        Directory.CreateDirectory(directory);

        try
        {
            var settings = new ClientSettings
            {
                CrtSettingsUnlocked = true,
                CrtPreset = CrtPresetKind.Off,
            };
            settings.Save(path);

            var reloaded = ClientSettings.Load(path);

            Assert.True(reloaded.CrtSettingsUnlocked);
            Assert.Equal(CrtPresetKind.Off, reloaded.CrtPreset);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void LegacyCrtSelectionsMigrateToStickyUnlockEvenAfterReturningToDefaults()
    {
        var (path, directory) = CreateTemporarySettingsPath();
        Directory.CreateDirectory(directory);

        try
        {
            File.WriteAllText(path, "[Settings]\nCRT Preset=2\n");

            var migrated = ClientSettings.Load(path);

            Assert.True(migrated.CrtSettingsUnlocked);

            // Match Reset CRT Settings: reset the presentation choices without clearing the unlock.
            migrated.CrtPreset = OpenGarrisonPreferencesDocument.DefaultCrtPreset;
            migrated.CrtQuality = OpenGarrisonPreferencesDocument.DefaultCrtQuality;
            migrated.CrtSignalMode = OpenGarrisonPreferencesDocument.DefaultCrtSignalMode;
            migrated.CrtCurvatureEnabled = OpenGarrisonPreferencesDocument.DefaultCrtCurvatureEnabled;
            migrated.CrtBrightnessPercent = OpenGarrisonPreferencesDocument.DefaultCrtBrightnessPercent;
            migrated.Save(path);

            var reloaded = ClientSettings.Load(path);

            Assert.True(reloaded.CrtSettingsUnlocked);
            Assert.Equal(CrtPresetKind.Off, reloaded.CrtPreset);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static (string Path, string Directory) CreateTemporarySettingsPath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "OpenGarrison.PluginHost.Tests", Guid.NewGuid().ToString("N"));
        return (Path.Combine(directory, OpenGarrisonPreferencesDocument.DefaultFileName), directory);
    }
}
