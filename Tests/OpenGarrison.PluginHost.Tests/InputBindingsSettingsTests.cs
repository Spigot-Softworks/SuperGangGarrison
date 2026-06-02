using System;
using System.Globalization;
using System.IO;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Client;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class InputBindingsSettingsTests
{
    [Fact]
    public void ParseBindingSupportsLegacyIntegerKeyValues()
    {
        Assert.True(InputBindingsSettings.TryParseBinding(((int)Keys.Tab).ToString(CultureInfo.InvariantCulture), out var binding));

        Assert.Equal(InputBinding.FromKey(Keys.Tab), binding);
    }

    [Theory]
    [InlineData("Mouse3", InputMouseButton.Middle)]
    [InlineData("Mouse4", InputMouseButton.XButton1)]
    [InlineData("Mouse5", InputMouseButton.XButton2)]
    [InlineData("Mouse:XButton1", InputMouseButton.XButton1)]
    public void ParseBindingSupportsMouseButtonAliases(string text, InputMouseButton expectedButton)
    {
        Assert.True(InputBindingsSettings.TryParseBinding(text, out var binding));

        Assert.Equal(InputBinding.FromMouse(expectedButton), binding);
    }

    [Fact]
    public void SaveAndLoadRoundTripsMouseBindings()
    {
        var path = Path.Combine(Path.GetTempPath(), "opengarrison-controls-tests", Guid.NewGuid().ToString("N"), InputBindingsSettings.DefaultFileName);
        var settings = new InputBindingsSettings
        {
            ShowScoreboard = InputBinding.FromMouse(InputMouseButton.XButton1),
            ToggleConsole = InputBinding.FromMouse(InputMouseButton.Middle),
        };

        try
        {
            settings.Save(path);

            var loaded = InputBindingsSettings.Load(path);

            Assert.Equal(InputBinding.FromMouse(InputMouseButton.XButton1), loaded.ShowScoreboard);
            Assert.Equal(InputBinding.FromMouse(InputMouseButton.Middle), loaded.ToggleConsole);
        }
        finally
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
