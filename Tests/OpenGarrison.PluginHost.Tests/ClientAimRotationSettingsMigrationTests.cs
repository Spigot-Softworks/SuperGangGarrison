using System;
using System.IO;
using System.Text.Json;
using OpenGarrison.Client;
using OpenGarrison.ClientShared;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ClientAimRotationSettingsMigrationTests
{
    [Fact]
    public void LegacyIniPreferenceMigratesToLocalAimOnceAndLaterRemoteChoicePersists()
    {
        var (path, directory) = CreateTemporarySettingsPath();
        Directory.CreateDirectory(directory);

        try
        {
            File.WriteAllText(path, "[Settings]\nUse Local Weapon Rotation=0\n");

            var migrated = ClientSettings.Load(path);

            Assert.True(migrated.UseLocalWeaponRotation);
            Assert.Equal(ClientSettings.CurrentClientSettingsMigrationVersion, migrated.ClientSettingsMigrationVersion);
            var savedIni = IniConfigurationFile.Load(path);
            Assert.True(savedIni.GetBool("Settings", "Use Local Weapon Rotation"));
            Assert.Equal(ClientSettings.CurrentClientSettingsMigrationVersion,
                savedIni.GetInt("Settings", "Client Settings Migration Version"));

            migrated.UseLocalWeaponRotation = false;
            migrated.Save(path);

            var reloaded = ClientSettings.Load(path);
            Assert.False(reloaded.UseLocalWeaponRotation);
            Assert.Equal(ClientSettings.CurrentClientSettingsMigrationVersion, reloaded.ClientSettingsMigrationVersion);
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
    public void CurrentIniVersionPreservesExplicitRemoteAim()
    {
        var (path, directory) = CreateTemporarySettingsPath();
        Directory.CreateDirectory(directory);

        try
        {
            File.WriteAllText(path,
                "[Settings]\nUse Local Weapon Rotation=0\nClient Settings Migration Version=1\n");

            var loaded = ClientSettings.Load(path);

            Assert.False(loaded.UseLocalWeaponRotation);
            Assert.Equal(ClientSettings.CurrentClientSettingsMigrationVersion, loaded.ClientSettingsMigrationVersion);
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
    public void BrowserJsonMigrationAndRoundTripPreserveLaterRemoteChoice()
    {
        var jsonContext = BrowserClientSettingsJsonContext.Default.ClientSettings;
        var legacy = JsonSerializer.Deserialize(
            "{\"UseLocalWeaponRotation\":false}",
            jsonContext);
        Assert.NotNull(legacy);
        Assert.Equal(0, legacy!.ClientSettingsMigrationVersion);
        Assert.True(legacy.ApplyVersionedMigrations());
        Assert.True(legacy.UseLocalWeaponRotation);
        Assert.Equal(ClientSettings.CurrentClientSettingsMigrationVersion, legacy.ClientSettingsMigrationVersion);

        var savedLocalAim = JsonSerializer.Serialize(legacy, jsonContext);
        var loadedLocalAim = JsonSerializer.Deserialize(savedLocalAim, jsonContext);
        Assert.NotNull(loadedLocalAim);
        Assert.False(loadedLocalAim!.ApplyVersionedMigrations());
        Assert.True(loadedLocalAim.UseLocalWeaponRotation);

        loadedLocalAim.UseLocalWeaponRotation = false;
        var savedRemoteAim = JsonSerializer.Serialize(loadedLocalAim, jsonContext);
        var loadedRemoteAim = JsonSerializer.Deserialize(savedRemoteAim, jsonContext);
        Assert.NotNull(loadedRemoteAim);
        Assert.False(loadedRemoteAim!.ApplyVersionedMigrations());
        Assert.False(loadedRemoteAim.UseLocalWeaponRotation);
    }

    [Fact]
    public void NewClientAndRuntimeSettingsDefaultToResponsiveLocalAim()
    {
        Assert.True(new ClientSettings().UseLocalWeaponRotation);
        Assert.True(new GameplayRuntimeSettings().UseLocalWeaponRotation);
        Assert.True(new OpenGarrisonPreferencesDocument().UseLocalWeaponRotation);
    }

    private static (string Path, string Directory) CreateTemporarySettingsPath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "OpenGarrison.PluginHost.Tests", Guid.NewGuid().ToString("N"));
        return (Path.Combine(directory, OpenGarrisonPreferencesDocument.DefaultFileName), directory);
    }
}
