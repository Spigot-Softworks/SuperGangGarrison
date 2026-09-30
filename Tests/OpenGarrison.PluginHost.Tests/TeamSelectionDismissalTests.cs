using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using OpenGarrison.Client;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class TeamSelectionDismissalTests
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DismissingUnjoinedSelectionWatchesTheMatchAndCancelsQueuedJoinCommands(bool awaitingJoin)
    {
        var game = (Game1)RuntimeHelpers.GetUninitializedObject(typeof(Game1));
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        world.PrepareLocalPlayerJoin();
        if (!awaitingJoin) world.CompleteLocalPlayerJoin(PlayerClass.Scout);
        Set(game, "_world", world);
        foreach (var name in new[] { "_networkClient", "_uiShellState", "_gameplaySessionState", "_pendingPredictedInputs", "_predictedWeaponFireVisuals" })
        {
            var field = typeof(Game1).GetField(name, Instance)!;
            field.SetValue(game, Activator.CreateInstance(field.FieldType, true));
        }
        var client = (NetworkGameClient)typeof(Game1).GetField("_networkClient", Instance)!.GetValue(game)!;
        typeof(NetworkGameClient).GetField("_transport", Instance)!.SetValue(client, new EmptyTransport());
        client.SetLocalPlayerSlot(1);
        if (awaitingJoin)
        {
            client.QueueTeamSelection(PlayerTeam.Red);
            client.QueueClassSelection(PlayerClass.Scout);
        }
        Set(game, "_teamSelectOpen", true);
        Set(game, "_pendingMapTeamSelection", true);
        typeof(Game1).GetMethod("DismissGameplayTeamSelection", Instance)!.Invoke(game, null);
        var commands = (IDictionary)typeof(NetworkGameClient).GetField("_pendingControlCommands", Instance)!.GetValue(client)!;
        Assert.Equal(awaitingJoin, commands.Contains(ControlCommandKind.Spectate));
        Assert.False(commands.Contains(ControlCommandKind.SelectTeam));
        Assert.False(commands.Contains(ControlCommandKind.SelectClass));
        Assert.False((bool)typeof(Game1).GetProperty("_teamSelectOpen", Instance)!.GetValue(game)!);
        Assert.False((bool)typeof(Game1).GetField("_pendingMapTeamSelection", Instance)!.GetValue(game)!);
    }

    private static void Set(Game1 game, string name, object value)
    {
        if (typeof(Game1).GetField(name, Instance) is { } field) field.SetValue(game, value);
        else typeof(Game1).GetProperty(name, Instance)!.SetValue(game, value);
    }

    private sealed class EmptyTransport : INetworkClientMessageTransport
    {
        public bool HasPendingMessages => false;
        public bool IsLoopbackConnection => true;
        public string RemoteDescription => "team dismissal test";
        public bool TryReceive(out byte[] payload) { payload = []; return false; }
        public bool TryConsumeDisconnectReason(out string reason) { reason = ""; return false; }
        public void Send(byte[] payload) { }
        public void Dispose() { }
    }
}
