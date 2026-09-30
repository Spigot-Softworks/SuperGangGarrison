using OpenGarrison.Client;
using OpenGarrison.ClientShared;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class SettingsMenuOrganizationTests
{
    [Fact]
    public void WeaponBobSettingDefaultsToEnabledAndRoundTripsThroughClientPreferences()
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
            var defaults = new ClientSettings();
            Assert.Equal(WeaponBobMode.Enabled, defaults.WeaponBobMode);

            var settings = new ClientSettings
            {
                WeaponBobMode = WeaponBobMode.Disabled,
            };

            settings.Save(configPath);

            var loaded = ClientSettings.Load(configPath);

            Assert.Equal(WeaponBobMode.Disabled, loaded.WeaponBobMode);

            // Legacy bool values from the previous Enabled/Disabled toggle still load.
            File.WriteAllText(configPath, "[Settings]\nWeapon Bob=0\n");
            var legacyDisabled = ClientSettings.Load(configPath);
            Assert.Equal(WeaponBobMode.Disabled, legacyDisabled.WeaponBobMode);

            File.WriteAllText(configPath, "[Settings]\nWeapon Bob=1\n");
            var legacyEnabled = ClientSettings.Load(configPath);
            Assert.Equal(WeaponBobMode.Enabled, legacyEnabled.WeaponBobMode);

            // Removed Smooth option maps back to Enabled.
            File.WriteAllText(configPath, "[Settings]\nWeapon Bob=Smooth\n");
            var legacySmooth = ClientSettings.Load(configPath);
            Assert.Equal(WeaponBobMode.Enabled, legacySmooth.WeaponBobMode);
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
