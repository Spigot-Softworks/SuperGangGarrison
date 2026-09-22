using System.Reflection;
using System.Runtime.CompilerServices;
using OpenGarrison.Client;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class HostedLastToDieModalRegressionTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Theory]
    [InlineData(LastToDieWirePhase.Lobby, false, false)]
    [InlineData(LastToDieWirePhase.SurvivorChoice, false, true)]
    [InlineData(LastToDieWirePhase.SurvivorChoice, true, false)]
    [InlineData(LastToDieWirePhase.RewardChoice, true, true)]
    [InlineData(LastToDieWirePhase.LoadingStage, true, false)]
    [InlineData(LastToDieWirePhase.Playing, true, true)]
    [InlineData(LastToDieWirePhase.Won, true, true)]
    [InlineData(LastToDieWirePhase.Lost, true, true)]
    public void EscapeOwnershipFollowsTheVisibleHostedPhase(LastToDieWirePhase phase, bool selected, bool opensMenu)
    {
        var game = CreateGame(phase, selected);
        Assert.Equal(opensMenu, InvokeBool(game, "CanOpenInGamePauseMenu"));
        if (opensMenu)
        {
            Set(game, "_inGameMenuOpen", true);
            Assert.Equal("InGameMenu", Invoke(game, "GetActiveGameplayOverlay").ToString());
            Assert.False(InvokeBool(game, "CanUpdateHostedLastToDieMenuInput"));
            Set(game, "_optionsMenuOpen", true);
            Assert.Equal("OptionsMenu", Invoke(game, "GetActiveGameplayOverlay").ToString());
            Assert.False(InvokeBool(game, "CanUpdateHostedLastToDieMenuInput"));
        }
    }

    [Fact]
    public void WarmupBlocksPlayingMenusButKeepsHostedChoicesAccessible()
    {
        var playing = CreateGame(LastToDieWirePhase.Playing, true);
        Set(playing, "_networkWorldWarmupActive", true);
        Assert.False(InvokeBool(playing, "CanOpenInGamePauseMenu"));
        Set(playing, "_networkWorldWarmupActive", false);
        Assert.True(InvokeBool(playing, "CanOpenInGamePauseMenu"));
        Set(playing, "_loadingOverlayVisible", true);
        Assert.False(InvokeBool(playing, "CanOpenInGamePauseMenu"));
        var choices = CreateGame(LastToDieWirePhase.RewardChoice, true);
        Set(choices, "_networkWorldWarmupActive", true);
        Assert.True(InvokeBool(choices, "CanOpenInGamePauseMenu"));
    }

    [Fact]
    public void EscapePauseMenuSuspendsSoloLastToDieSimulation()
    {
        var game = CreateGame(LastToDieWirePhase.Playing, selected: true);
        var canOpenPauseMenu = InvokeBool(game, "CanOpenInGamePauseMenu");
        Assert.True(Game1.ShouldOpenInGamePauseMenu(
            escapePressed: true,
            controllerPausePressed: false,
            canOpenInGamePauseMenu: canOpenPauseMenu));
        Assert.False(Game1.ShouldOpenInGamePauseMenu(
            escapePressed: true,
            controllerPausePressed: false,
            canOpenInGamePauseMenu: false));
        Set(game, "_inGameMenuOpen", true);
        Assert.True(InvokeBool(game, "HasOpenGameplayOverlay"));
        Assert.True(Game1.ShouldSuspendOfflineLastToDieSimulation(
            isLastToDieSessionActive: true,
            hasOpenGameplayOverlay: true,
            survivorMenuOpen: false,
            perkMenuOpen: false,
            stageClearOverlayOpen: false,
            failurePresentationActive: false));
        Assert.True(Game1.ShouldPauseHostedLastToDieSoloSimulation(
            isHostedServerRunning: true,
            isConnected: true,
            maximumPlayers: 1,
            phase: LastToDieWirePhase.Playing,
            hasOpenGameplayOverlay: true));
        Assert.False(Game1.ShouldPauseHostedLastToDieSoloSimulation(
            isHostedServerRunning: true,
            isConnected: true,
            maximumPlayers: 2,
            phase: LastToDieWirePhase.Playing,
            hasOpenGameplayOverlay: true));
    }

    [Fact]
    public void ClosingAnOverlayCannotPassTheSameInputToTheChoiceUnderneath()
    {
        var game = CreateGame(LastToDieWirePhase.SurvivorChoice, true);
        Set(game, "_optionsMenuOpen", true);
        Set(game, "_gameplayModalOwnedInputThisFrame", InvokeBool(game, "HasGameplayModalInputOwner"));
        Set(game, "_optionsMenuOpen", false);
        Assert.False(InvokeBool(game, "CanUpdateHostedLastToDieMenuInput"));
        Set(game, "_gameplayModalOwnedInputThisFrame", false);
        Assert.True(InvokeBool(game, "CanUpdateHostedLastToDieMenuInput"));
    }

    private static Game1 CreateGame(LastToDieWirePhase phase, bool selected)
    {
        var game = (Game1)RuntimeHelpers.GetUninitializedObject(typeof(Game1));
        Set(game, "_world", new SimulationWorld());
        foreach (var name in new[] { "_uiShellState", "_gameplaySessionState", "_networkClient" })
        {
            var field = typeof(Game1).GetField(name, Private)!;
            field.SetValue(game, Activator.CreateInstance(field.FieldType, true));
        }
        var controller = typeof(Game1).GetField("_gameplayOverlayController", Private)!;
        controller.SetValue(game, Activator.CreateInstance(controller.FieldType, Private | BindingFlags.Public, null, [game], null));
        var client = (NetworkGameClient)typeof(Game1).GetField("_networkClient", Private)!.GetValue(game)!;
        typeof(NetworkGameClient).GetField("_transport", Private)!.SetValue(client, new EmptyTransport());
        client.SetLocalPlayerSlot(1);
        client.LastToDieState.ApplySnapshot(new(Guid.NewGuid(), 1, 1, 1, LastToDieWireDifficulty.Standard,
            phase, 1, 1, 1, "Harvest", 0, 100, 100,
            [new(1, Guid.NewGuid(), true, selected ? "soldier" : "", [], 0, 0, [], true, true, 0)]));
        Set(game, "_mainMenuOpen", false);
        Set(game, "_teamSelectOpen", false);
        Set(game, "_classSelectOpen", false);
        return game;
    }

    private static object Invoke(Game1 game, string name) => typeof(Game1).GetMethod(name, Private)!.Invoke(game, null)!;
    private static bool InvokeBool(Game1 game, string name) => (bool)Invoke(game, name);
    private static void Set(Game1 game, string name, object value)
    {
        if (typeof(Game1).GetField(name, Private) is { } field) field.SetValue(game, value);
        else typeof(Game1).GetProperty(name, Private)!.SetValue(game, value);
    }

    private sealed class EmptyTransport : INetworkClientMessageTransport
    {
        public bool HasPendingMessages => false;
        public bool IsLoopbackConnection => true;
        public string RemoteDescription => "modal test";
        public bool TryReceive(out byte[] payload) { payload = []; return false; }
        public bool TryConsumeDisconnectReason(out string reason) { reason = ""; return false; }
        public void Send(byte[] payload) { }
        public void Dispose() { }
    }
}
