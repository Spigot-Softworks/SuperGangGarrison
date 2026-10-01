using System.Reflection;
using System.Runtime.CompilerServices;
using OpenGarrison.Client;
using OpenGarrison.ClientShared;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class RuntimeSettingsOwnershipTests
{
    [Fact]
    public void RuntimeSettingsAreStablePerManagerAndSeparateFromPersistedSettings()
    {
        var first = CreateGameWithSettingsManagers();
        var second = CreateGameWithSettingsManagers();

        var firstAudioView = ((IMenuContext)first.Game).AudioRuntimeSettings;
        var firstGameplayView = ((IGameplayContext)first.Game).GameplayRuntimeSettings;
        var firstHudView = ((IHudContext)first.Game).HudRuntimeSettings;
        var firstDisplayView = ((IMenuContext)first.Game).DisplayRuntimeSettings;
        var secondAudioView = ((IMenuContext)second.Game).AudioRuntimeSettings;
        var secondGameplayView = ((IGameplayContext)second.Game).GameplayRuntimeSettings;
        var secondHudView = ((IHudContext)second.Game).HudRuntimeSettings;
        var secondDisplayView = ((IMenuContext)second.Game).DisplayRuntimeSettings;

        Assert.Same(first.AudioManager.RuntimeSettings, firstAudioView);
        Assert.Same(firstAudioView, ((IAudioContext)first.Game).AudioRuntimeSettings);
        Assert.Same(first.GameplayManager.RuntimeSettings, firstGameplayView);
        Assert.Same(firstGameplayView, ((IMenuContext)first.Game).GameplayRuntimeSettings);
        Assert.Same(firstGameplayView, ((IPluginContext)first.Game).GameplayRuntimeSettings);
        Assert.Same(firstGameplayView, ((IAudioContext)first.Game).GameplayRuntimeSettings);
        Assert.Same(first.HudManager.RuntimeSettings, firstHudView);
        Assert.Same(firstHudView, ((IRenderContext)first.Game).HudRuntimeSettings);
        Assert.Same(firstHudView, ((IMenuContext)first.Game).HudRuntimeSettings);
        Assert.Same(first.MenuManager.DisplaySettings, firstDisplayView);
        Assert.Same(firstDisplayView, ((IMenuContext)first.Game).DisplayRuntimeSettings);
        Assert.Equal(100, firstAudioView.MasterVolumePercent);
        Assert.Equal(70, firstAudioView.MenuMusicVolumePercent);
        Assert.True(firstGameplayView.EnablePrediction);
        Assert.True(firstHudView.ShowShieldBarEnabled);
        Assert.Equal(ClientSettings.DefaultCursorSizePercent, firstHudView.CursorSizePercent);
        Assert.Equal(OpenGarrisonPreferencesDocument.DefaultDisplayMode, firstDisplayView.DisplayMode);

        Assert.Same(second.AudioManager.RuntimeSettings, secondAudioView);
        Assert.Same(second.GameplayManager.RuntimeSettings, secondGameplayView);
        Assert.Same(secondGameplayView, ((IPluginContext)second.Game).GameplayRuntimeSettings);
        Assert.Same(secondGameplayView, ((IAudioContext)second.Game).GameplayRuntimeSettings);
        Assert.Same(second.HudManager.RuntimeSettings, secondHudView);
        Assert.Same(secondHudView, ((IRenderContext)second.Game).HudRuntimeSettings);
        Assert.Same(second.MenuManager.DisplaySettings, secondDisplayView);

        Assert.NotSame(firstAudioView, secondAudioView);
        Assert.NotSame(firstGameplayView, secondGameplayView);
        Assert.NotSame(firstHudView, secondHudView);
        Assert.NotSame(firstDisplayView, secondDisplayView);

        Assert.Same(first.ClientSettings, first.Game._clientSettings);
        firstGameplayView.EnablePrediction = false;
        Assert.True(first.Game._clientSettings.EnablePrediction);
    }

    private static (Game1 Game, ClientSettings ClientSettings, AudioManager AudioManager, GameplayManager GameplayManager, HudManager HudManager, MenuManager MenuManager)
        CreateGameWithSettingsManagers()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var game = (Game1)RuntimeHelpers.GetUninitializedObject(typeof(Game1));
        var services = new ClientServiceContainer();
        typeof(Game1).GetField("_services", flags)!.SetValue(game, services);
        var clientSettings = new ClientSettings { EnablePrediction = true };
        services.Register(clientSettings);

        var gameplayManager = new GameplayManager((IGameplayContext)game);
        services.Register(gameplayManager);
        var audioManager = new AudioManager((IAudioContext)game);
        services.Register(audioManager);
        var hudManager = new HudManager((IHudContext)game);
        services.Register(hudManager);
        var menuManager = new MenuManager((IMenuContext)game);
        services.Register(menuManager);

        return (game, clientSettings, audioManager, gameplayManager, hudManager, menuManager);
    }
}
