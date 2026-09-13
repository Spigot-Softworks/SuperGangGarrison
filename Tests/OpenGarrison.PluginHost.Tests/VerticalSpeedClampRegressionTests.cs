using System.Globalization;
using System.Reflection;
using OpenGarrison.Client;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class VerticalSpeedClampRegressionTests
{
    private const BindingFlags InstanceMembers =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [Theory]
    [InlineData(41f)]
    [InlineData(59f)]
    [InlineData(60f)]
    public void AdvancedHostEditPreservesHighVerticalClampThroughWorldTuning(float requestedClamp)
    {
        var formType = typeof(Game1).GetNestedType("HostSetupFormState", BindingFlags.NonPublic)!;
        var form = Activator.CreateInstance(formType, nonPublic: true)!;
        var initialSettings = new OpenGarrisonHostSettings();

        formType.GetMethod("LoadAdvancedCvarsFrom", InstanceMembers)!
            .Invoke(form, [initialSettings]);
        formType.GetProperty("ActiveAdvancedCvarName", InstanceMembers)!
            .SetValue(form, "sv_vertical_speed_clamp");
        formType.GetMethod("SetActiveAdvancedCvarEditBuffer", InstanceMembers)!
            .Invoke(form, [requestedClamp.ToString(CultureInfo.InvariantCulture)]);

        var normalizedBuffer = (string)formType
            .GetMethod("GetActiveAdvancedCvarEditBuffer", InstanceMembers)!
            .Invoke(form, null)!;
        Assert.Equal(requestedClamp.ToString(CultureInfo.InvariantCulture), normalizedBuffer);

        var launchedSettings = new OpenGarrisonHostSettings();
        formType.GetMethod("ApplyAdvancedCvarsTo", InstanceMembers)!
            .Invoke(form, [launchedSettings]);
        Assert.Equal(requestedClamp, launchedSettings.VerticalSpeedClampPerTick);

        var world = new SimulationWorld();
        world.SetVerticalSpeedClampPerTick(launchedSettings.VerticalSpeedClampPerTick);
        Assert.Equal(requestedClamp, world.ConfiguredVerticalSpeedClampPerTick);
    }
}
