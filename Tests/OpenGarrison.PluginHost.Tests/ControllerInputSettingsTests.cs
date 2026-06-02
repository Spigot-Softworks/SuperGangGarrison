using System;
using System.IO;
using OpenGarrison.ClientShared;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ControllerInputSettingsTests
{
    [Fact]
    public void ClientSettingsSaveAndLoadRoundTripsControllerSettings()
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
                ControllerInputMode = ControllerInputMode.On,
                ControllerReticleMode = ControllerReticleMode.AimLine,
                ControllerAimAssistEnabled = false,
                ControllerFlickToChangeDirections = false,
                ControllerAimAssistStrength = 0.8f,
                ControllerAimDeadzone = 0.3f,
                ControllerScopedPrecisionSpeed = 240f,
                ControllerAimDistanceTier1 = 112f,
                ControllerAimDistanceTier2 = 176f,
                ControllerAimDistanceTier3 = 272f,
                ControllerJumpButton = ControllerButtonBinding.B,
                ControllerPrimaryFireButton = ControllerButtonBinding.RightShoulder,
                ControllerSecondaryFireButton = ControllerButtonBinding.LeftShoulder,
                ControllerUseAbilityButton = ControllerButtonBinding.Y,
                ControllerInteractButton = ControllerButtonBinding.A,
                ControllerSwapWeaponButton = ControllerButtonBinding.X,
                ControllerScoreboardButton = ControllerButtonBinding.LeftStick,
                ControllerPauseButton = ControllerButtonBinding.Start,
                ControllerAimDistanceButton = ControllerButtonBinding.RightStick,
                ControllerChangeTeamButton = ControllerButtonBinding.DPadLeft,
                ControllerChangeClassButton = ControllerButtonBinding.DPadRight,
            };

            settings.Save(configPath);

            var loaded = ClientSettings.Load(configPath);

            Assert.Equal(ControllerInputMode.On, loaded.ControllerInputMode);
            Assert.Equal(ControllerReticleMode.AimLine, loaded.ControllerReticleMode);
            Assert.False(loaded.ControllerAimAssistEnabled);
            Assert.False(loaded.ControllerFlickToChangeDirections);
            Assert.Equal(0.8f, loaded.ControllerAimAssistStrength);
            Assert.Equal(0.3f, loaded.ControllerAimDeadzone);
            Assert.Equal(240f, loaded.ControllerScopedPrecisionSpeed);
            Assert.Equal(112f, loaded.ControllerAimDistanceTier1);
            Assert.Equal(176f, loaded.ControllerAimDistanceTier2);
            Assert.Equal(272f, loaded.ControllerAimDistanceTier3);
            Assert.Equal(ControllerButtonBinding.B, loaded.ControllerJumpButton);
            Assert.Equal(ControllerButtonBinding.RightShoulder, loaded.ControllerPrimaryFireButton);
            Assert.Equal(ControllerButtonBinding.LeftShoulder, loaded.ControllerSecondaryFireButton);
            Assert.Equal(ControllerButtonBinding.Y, loaded.ControllerUseAbilityButton);
            Assert.Equal(ControllerButtonBinding.A, loaded.ControllerInteractButton);
            Assert.Equal(ControllerButtonBinding.X, loaded.ControllerSwapWeaponButton);
            Assert.Equal(ControllerButtonBinding.LeftStick, loaded.ControllerScoreboardButton);
            Assert.Equal(ControllerButtonBinding.Start, loaded.ControllerPauseButton);
            Assert.Equal(ControllerButtonBinding.RightStick, loaded.ControllerAimDistanceButton);
            Assert.Equal(ControllerButtonBinding.DPadLeft, loaded.ControllerChangeTeamButton);
            Assert.Equal(ControllerButtonBinding.DPadRight, loaded.ControllerChangeClassButton);
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
    public void PreferencesNormalizeInvalidControllerValues()
    {
        var document = new OpenGarrisonPreferencesDocument
        {
            ControllerInputMode = (ControllerInputMode)99,
            ControllerReticleMode = (ControllerReticleMode)99,
            ControllerAimAssistStrength = 5f,
            ControllerAimDeadzone = -1f,
            ControllerScopedPrecisionSpeed = 5000f,
            ControllerAimDistanceTier1 = float.NaN,
            ControllerAimDistanceTier2 = -10f,
            ControllerAimDistanceTier3 = 5000f,
            ControllerJumpButton = (ControllerButtonBinding)999,
        };

        Assert.Equal(ControllerInputMode.Auto, OpenGarrisonPreferencesDocument.NormalizeControllerInputMode(document.ControllerInputMode));
        Assert.Equal(ControllerReticleMode.Cursor, OpenGarrisonPreferencesDocument.NormalizeControllerReticleMode(document.ControllerReticleMode));
        Assert.Equal(1f, OpenGarrisonPreferencesDocument.NormalizeControllerAimAssistStrength(document.ControllerAimAssistStrength));
        Assert.Equal(0.01f, OpenGarrisonPreferencesDocument.NormalizeControllerAimDeadzone(document.ControllerAimDeadzone));
        Assert.Equal(1200f, OpenGarrisonPreferencesDocument.NormalizeControllerScopedPrecisionSpeed(document.ControllerScopedPrecisionSpeed));
        Assert.Equal(OpenGarrisonPreferencesDocument.DefaultControllerAimDistanceTier1, OpenGarrisonPreferencesDocument.NormalizeControllerAimDistance(document.ControllerAimDistanceTier1, OpenGarrisonPreferencesDocument.DefaultControllerAimDistanceTier1));
        Assert.Equal(16f, OpenGarrisonPreferencesDocument.NormalizeControllerAimDistance(document.ControllerAimDistanceTier2, OpenGarrisonPreferencesDocument.DefaultControllerAimDistanceTier2));
        Assert.Equal(960f, OpenGarrisonPreferencesDocument.NormalizeControllerAimDistance(document.ControllerAimDistanceTier3, OpenGarrisonPreferencesDocument.DefaultControllerAimDistanceTier3));
        Assert.Equal(ControllerButtonBinding.None, OpenGarrisonPreferencesDocument.NormalizeControllerButtonBinding(document.ControllerJumpButton));
    }
}
