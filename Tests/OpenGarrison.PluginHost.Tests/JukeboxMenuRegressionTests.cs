using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using OpenGarrison.Client;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class JukeboxMenuRegressionTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Fact]
    public void EmptyTrackPromptAndMusicLibraryButtonBothOpenThePlatformLibrary()
    {
        var game = CreateMenu([]);
        var opened = 0;
        var actions = Actions(game, () => opened++);
        Assert.Contains("open music library", Label(actions[0]));
        Activate(actions[0]);
        Assert.Equal(1, opened);
        Activate(Assert.Single(actions, action => Label(action) == "Music Library"));
        Assert.Equal(2, opened);
    }

    [Fact]
    public void TrackSelectorStillCyclesAndLoadingPromptDoesNotOpenAnotherLibrary()
    {
        var game = CreateMenu(["1. First", "2. Second"]);
        var opened = 0;
        Activate(Actions(game, () => opened++)[0]);
        Assert.Equal("2. Second", Label(Actions(game, () => opened++)[0]));
        Activate(Actions(game, () => opened++)[0]);
        Assert.Equal("1. First", Label(Actions(game, () => opened++)[0]));
        typeof(Game1).GetField("_jukeboxLibraryLoad", Private)!.SetValue(game, new TaskCompletionSource().Task);
        var loading = Actions(game, () => opened++);
        Assert.Equal("Loading tracks...", Label(loading[0]));
        Activate(loading[0]);
        Assert.Equal(0, opened);
    }

    private static Game1 CreateMenu(string[] tracks)
    {
        var game = (Game1)RuntimeHelpers.GetUninitializedObject(typeof(Game1));
        var host = typeof(Game1).GetField("_embeddedSessionHost", Private)!;
        host.SetValue(game, RuntimeHelpers.GetUninitializedObject(host.FieldType));
        var settings = typeof(Game1).GetField("_voiceSettings", Private)!;
        settings.SetValue(game, Activator.CreateInstance(settings.FieldType));
        typeof(Game1).GetField("_jukeboxTracks", Private)!.SetValue(game, tracks);
        return game;
    }

    private static object[] Actions(Game1 game, Action openLibrary) =>
        ((IEnumerable)typeof(Game1).GetMethod("BuildSessionJukeboxActions", Private)!.Invoke(game, [openLibrary])!).Cast<object>().ToArray();
    private static string Label(object action) => (string)action.GetType().GetProperty("Label")!.GetValue(action)!;
    private static void Activate(object action) => ((Action)action.GetType().GetProperty("Activate")!.GetValue(action)!)();
}
