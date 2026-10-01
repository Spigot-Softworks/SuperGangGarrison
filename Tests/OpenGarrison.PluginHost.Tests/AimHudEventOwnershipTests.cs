using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using OpenGarrison.Client;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class AimHudEventOwnershipTests
{
    [Fact]
    public void AimHudFeedbackAndPendingNetworkEventsArePerGameControllerState()
    {
        var first = CreateGameWithManagers();
        var second = CreateGameWithManagers();

        AssertOwnedPrivateField(first.Game, first.Gameplay.InputUpdate, "_hasLatestLocalAimWorldPosition");
        AssertOwnedPrivateField(first.Game, first.Gameplay.InputUpdate, "_latestLocalAimWorldX");
        AssertOwnedPrivateField(first.Game, first.Gameplay.InputUpdate, "_latestLocalAimWorldY");
        AssertOwnedPrivateField(first.Game, first.Hud.LocalStatus, "_portraitRumbleSeed");
        AssertOwnedPrivateField(first.Game, first.Hud.LocalStatus, "_portraitRumbleIntensity");
        AssertOwnedPrivateField(first.Game, first.Hud.LocalStatus, "_portraitRumbleRemainingSeconds");
        AssertOwnedPrivateField(first.Game, first.Hud.LocalStatus, "_damageVignetteIntensity");
        AssertOwnedPrivateField(first.Game, first.Hud.LocalStatus, "_damageVignetteFlashIntensity");
        AssertOwnedPrivateField(first.Game, first.Plugin.Events, "_pendingNetworkDamageEvents");
        AssertOwnedPrivateField(first.Game, first.Audio.Events, "_pendingNetworkSoundEvents");
        AssertOwnedPrivateField(first.Game, first.Gameplay.VisualEvents, "_pendingNetworkVisualEvents");

        Assert.False(first.Gameplay.InputUpdate.HasLatestLocalAimWorldPosition);
        Assert.Equal(0f, first.Gameplay.InputUpdate.LatestLocalAimWorldX);
        Assert.Equal(0f, first.Gameplay.InputUpdate.LatestLocalAimWorldY);
        Assert.False(second.Gameplay.InputUpdate.HasLatestLocalAimWorldPosition);

        SetPrivateField(first.Gameplay.InputUpdate, "_hasLatestLocalAimWorldPosition", true);
        SetPrivateField(first.Gameplay.InputUpdate, "_latestLocalAimWorldX", 12f);
        SetPrivateField(first.Gameplay.InputUpdate, "_latestLocalAimWorldY", 34f);
        first.Gameplay.InputUpdate.ResetLatestLocalAimWorldPosition();
        Assert.False(first.Gameplay.InputUpdate.HasLatestLocalAimWorldPosition);
        Assert.Equal(0f, first.Gameplay.InputUpdate.LatestLocalAimWorldX);
        Assert.Equal(0f, first.Gameplay.InputUpdate.LatestLocalAimWorldY);
        Assert.False(second.Gameplay.InputUpdate.HasLatestLocalAimWorldPosition);

        SetPrivateField(first.Hud.LocalStatus, "_portraitRumbleSeed", 7);
        SetPrivateField(first.Hud.LocalStatus, "_portraitRumbleIntensity", 0.8f);
        SetPrivateField(first.Hud.LocalStatus, "_portraitRumbleRemainingSeconds", 0.2f);
        SetPrivateField(first.Hud.LocalStatus, "_damageVignetteIntensity", 0.6f);
        SetPrivateField(first.Hud.LocalStatus, "_damageVignetteFlashIntensity", 0.4f);
        first.Hud.LocalStatus.ResetPortraitRumble();
        first.Hud.LocalStatus.ResetDamageVignette();
        Assert.Equal(7, GetPrivateField<int>(first.Hud.LocalStatus, "_portraitRumbleSeed"));
        Assert.Equal(0f, GetPrivateField<float>(first.Hud.LocalStatus, "_portraitRumbleIntensity"));
        Assert.Equal(0f, GetPrivateField<float>(first.Hud.LocalStatus, "_portraitRumbleRemainingSeconds"));
        Assert.Equal(0f, GetPrivateField<float>(first.Hud.LocalStatus, "_damageVignetteIntensity"));
        Assert.Equal(0f, GetPrivateField<float>(first.Hud.LocalStatus, "_damageVignetteFlashIntensity"));
        Assert.Equal(0, GetPrivateField<int>(second.Hud.LocalStatus, "_portraitRumbleSeed"));
        Assert.Equal(0f, GetPrivateField<float>(second.Hud.LocalStatus, "_damageVignetteIntensity"));

        first.Plugin.Events.QueuePendingNetworkDamageEvent(new SnapshotDamageEvent(5, 1, -1, 0, 2, 4f, 6f, false));
        second.Plugin.Events.QueuePendingNetworkDamageEvent(new SnapshotDamageEvent(6, 1, -1, 0, 3, 7f, 9f, false));
        first.Audio.Events.QueuePendingNetworkSoundEvent(new WorldSoundEvent("tone", 1f, 2f));
        second.Audio.Events.QueuePendingNetworkSoundEvent(new WorldSoundEvent("tone", 3f, 4f));
        first.Gameplay.VisualEvents.QueuePendingNetworkVisualEvent(new SnapshotVisualEvent("spark", 1f, 2f, 0f, 1));
        second.Gameplay.VisualEvents.QueuePendingNetworkVisualEvent(new SnapshotVisualEvent("spark", 3f, 4f, 0f, 1));

        first.Plugin.Events.ClearPendingNetworkDamageEvents();
        first.Audio.Events.ClearPendingNetworkSoundEvents();
        first.Gameplay.VisualEvents.ClearPendingNetworkVisualEvents();

        Assert.Empty(GetPrivateList(first.Plugin.Events, "_pendingNetworkDamageEvents"));
        Assert.Single(GetPrivateList(second.Plugin.Events, "_pendingNetworkDamageEvents"));
        Assert.Empty(GetPrivateList(first.Audio.Events, "_pendingNetworkSoundEvents"));
        Assert.Single(GetPrivateList(second.Audio.Events, "_pendingNetworkSoundEvents"));
        Assert.Empty(GetPrivateList(first.Gameplay.VisualEvents, "_pendingNetworkVisualEvents"));
        Assert.Single(GetPrivateList(second.Gameplay.VisualEvents, "_pendingNetworkVisualEvents"));
    }

    private static void AssertOwnedPrivateField(Game1 game, object owner, string fieldName)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        Assert.Null(typeof(Game1).GetField(fieldName, flags));
        var field = owner.GetType().GetField(fieldName, flags);
        Assert.NotNull(field);
        Assert.True(field!.IsPrivate);
    }

    private static T GetPrivateField<T>(object owner, string fieldName)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        return (T)owner.GetType().GetField(fieldName, flags)!.GetValue(owner)!;
    }

    private static void SetPrivateField<T>(object owner, string fieldName, T value)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        owner.GetType().GetField(fieldName, flags)!.SetValue(owner, value);
    }

    private static ICollection GetPrivateList(object owner, string fieldName)
    {
        return (ICollection)GetPrivateField<object>(owner, fieldName);
    }

    private static (Game1 Game, GameplayManager Gameplay, AudioManager Audio, HudManager Hud, PluginManager Plugin) CreateGameWithManagers()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var game = (Game1)RuntimeHelpers.GetUninitializedObject(typeof(Game1));
        var services = new ClientServiceContainer();
        typeof(Game1).GetField("_services", flags)!.SetValue(game, services);

        var gameplayManager = new GameplayManager(game);
        services.Register(gameplayManager);
        var audioManager = new AudioManager(game);
        services.Register(audioManager);
        var hudManager = new HudManager(game);
        services.Register(hudManager);
        var pluginManager = new PluginManager(game);
        services.Register(pluginManager);

        return (game, gameplayManager, audioManager, hudManager, pluginManager);
    }
}
