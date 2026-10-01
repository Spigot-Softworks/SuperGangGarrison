using System.Reflection;
using System.Runtime.CompilerServices;
using OpenGarrison.Client;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class MusicResourcesOwnershipTests
{
    [Fact]
    public void MusicResourcesAreStableSharedPerAudioManagerAndIsolatedBetweenGames()
    {
        var first = CreateGameWithAudioManager();
        var second = CreateGameWithAudioManager();

        var firstAudioView = ((IAudioContext)first.Game).MusicResources;
        var firstGameplayView = ((IGameplayContext)first.Game).MusicResources;
        var secondAudioView = ((IAudioContext)second.Game).MusicResources;
        var secondGameplayView = ((IGameplayContext)second.Game).MusicResources;

        Assert.Same(first.AudioManager.MusicResources, firstAudioView);
        Assert.Same(firstAudioView, firstGameplayView);
        Assert.Same(firstAudioView, ((IAudioContext)first.Game).MusicResources);
        Assert.Same(second.AudioManager.MusicResources, secondAudioView);
        Assert.Same(secondAudioView, secondGameplayView);
        Assert.Same(secondAudioView, ((IAudioContext)second.Game).MusicResources);
        Assert.NotSame(firstAudioView, secondAudioView);
        Assert.NotSame(firstGameplayView, secondGameplayView);
    }

    private static (Game1 Game, AudioManager AudioManager) CreateGameWithAudioManager()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var game = (Game1)RuntimeHelpers.GetUninitializedObject(typeof(Game1));
        var services = new ClientServiceContainer();
        typeof(Game1).GetField("_services", flags)!.SetValue(game, services);
        var audioManager = new AudioManager(game);
        services.Register(audioManager);
        return (game, audioManager);
    }
}
