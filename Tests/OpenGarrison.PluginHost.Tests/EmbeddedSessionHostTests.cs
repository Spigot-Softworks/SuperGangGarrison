using OpenGarrison.Client;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using OpenGarrison.SessionRuntime;
using Xunit;
using System.Reflection;
using System.Runtime.CompilerServices;
using OpenGarrison.ClientShared;

namespace OpenGarrison.PluginHost.Tests;

public sealed class EmbeddedSessionHostTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClassicBrowserSessionsAdvertiseOriginalMapIdentitiesToDesktopGuests(bool lastToDie)
    {
        using var host = new EmbeddedSessionHost(new() { LastToDie = lastToDie, PreferClassicMaps = true,
            MaximumPlayers = 4, Map = "Harvest", Seed = 123 });
        using var owner = new NetworkGameClient();
        using var guest = new NetworkGameClient();
        Assert.True(owner.Connect(new EmbeddedSessionClientTransport(host.CreatePeer(local: true)), "Owner", 0, out _, clientInstanceId: Guid.NewGuid()));
        Assert.True(guest.Connect(new EmbeddedSessionClientTransport(host.CreatePeer()), "Guest", 0, out _, clientInstanceId: Guid.NewGuid()));
        Pump(host, [owner, guest], () => owner.LocalPlayerSlot == 1 && guest.LocalPlayerSlot == 2);
        Assert.Equal("gg2_koth_harvest", host.World.Level.Name);
        Assert.NotNull(SimpleLevelFactory.CreateImportedLevel(host.World.Level.Name));
        Assert.True(host.World.TrySetNetworkPlayerAwaitingJoin(1, false));
        owner.SendVoteCommand(VoteCommandKind.OpenMenu);
        VoteMenuMessage? menu = null;
        for (var tick = 0; tick < 30 && menu is null; tick++)
        {
            host.Advance(1d / 30);
            menu = owner.ReceiveMessages().OfType<VoteMenuMessage>().FirstOrDefault();
        }
        Assert.NotNull(menu);
        Assert.DoesNotContain(menu.Maps, entry => ClassicStockMapCatalog.Variants.Any(v => v.ReplacedLevelName == entry.LevelName));
        if (lastToDie) Assert.Empty(menu.Maps);
        else Assert.Contains(menu.Maps, entry => entry.LevelName == "gg2_koth_harvest" && entry.DisplayName == "Harvest");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void LocalAuthorityAdmitsIndependentClientsAndStartsWithUnfilledSeats(int count)
    {
        using var host = new EmbeddedSessionHost(new() { MaximumPlayers = count == 1 ? 1 : 4, Seed = 123 });
        var clients = Enumerable.Range(0, count).Select(_ => new NetworkGameClient()).ToArray();
        try
        {
            foreach (var (client, index) in clients.Select((client, index) => (client, index)))
                Assert.True(client.Connect(new EmbeddedSessionClientTransport(host.CreatePeer(index == 0)),
                    "Player " + index, 0, out var error, clientInstanceId: Guid.NewGuid()), error);
            Pump(host, clients, () => clients.All(c => c.LastToDieState.Snapshot?.Players.Count == count));
            Assert.Equal(count, clients.Select(c => c.LocalPlayerSlot).Distinct().Count());
            foreach (var client in clients)
            {
                client.SendLastToDieCommand(LastToDieCommandKind.Ready);
                Pump(host, clients, () => client.LastToDieState.Snapshot!.Players.Single(p => p.Slot == client.LocalPlayerSlot).IsReady);
            }
            clients[0].SendLastToDieCommand(LastToDieCommandKind.RequestStart);
            Pump(host, clients, () => clients.All(c => c.LastToDieState.Snapshot?.Phase == LastToDieWirePhase.SurvivorChoice));
            foreach (var client in clients)
            {
                client.SendLastToDieCommand(LastToDieCommandKind.ChooseSurvivor, "ltd.survivor.spy");
                Pump(host, clients, () => !string.IsNullOrEmpty(client.LastToDieState.Snapshot!.Players.Single(p => p.Slot == client.LocalPlayerSlot).SurvivorId));
            }
            Pump(host, clients, () => clients.All(c => c.LastToDieState.Snapshot?.Phase == LastToDieWirePhase.RewardChoice));
            foreach (var client in clients)
            {
                var offer = client.LastToDieState.Snapshot!.Players.Single(p => p.Slot == client.LocalPlayerSlot);
                client.SendLastToDieCommand(LastToDieCommandKind.SelectReward, offer.ActiveOfferChoices[0], offer.ActiveOfferId);
                Pump(host, clients, () => client.LastToDieState.Snapshot!.Players.Single(p => p.Slot == client.LocalPlayerSlot).ActiveOfferId != offer.ActiveOfferId);
            }
            Pump(host, clients, () => clients.All(c => c.LastToDieState.Snapshot?.Phase == LastToDieWirePhase.Playing));
            foreach (var client in clients)
            {
                Assert.True(host.World.TryGetNetworkPlayer(client.LocalPlayerSlot, out var player));
                Assert.Equal(PlayerTeam.Red, player.Team);
                Assert.True(player.HasLastToDieSurvivorBuff);
                Assert.Equal(PlayerClass.Spy, player.ClassId);
            }
            Assert.All(host.World.EnumerateActiveNetworkPlayers().Where(p => p.Player.Team == PlayerTeam.Blue),
                p => Assert.True(p.Slot > host.Options.MaximumPlayers, "Enemies must not occupy human room seats."));

            // Exercise the client console route, including authority checks, without a graphics device.
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var game = (Game1)RuntimeHelpers.GetUninitializedObject(typeof(Game1));
            var history = new List<string>();
            typeof(Game1).GetField("_consoleHistory", flags)!.SetValue(game, history);
            var forward = typeof(Game1).GetMethod("TryForwardHostedLastToDieConsoleCommand", flags)!;
            var complete = typeof(Game1).GetMethod("UpdateEmbeddedConsoleCommand", flags)!;
            if (count == 1) typeof(Game1).GetField("_embeddedSessionHost", flags)!.SetValue(game, host);
            else
            {
                var connection = (PeerRoomConnection)RuntimeHelpers.GetUninitializedObject(typeof(PeerRoomConnection));
                var grant = new PeerRoomGrant("TEST", 2, "LastToDie", 95, "", "", default);
                var grantField = typeof(PeerRoomConnection).GetField("<Grant>k__BackingField", flags)!;
                grantField.SetValue(connection, grant);
                var room = new PlayerHostedRoomSession(connection);
                typeof(Game1).GetField("_peerRoomSession", flags)!.SetValue(game, room);
                forward.Invoke(game, ["ltd_win"]);
                Assert.Contains(history, line => line.Contains("only be issued by the room host"));
                Assert.Null(typeof(Game1).GetField("_embeddedConsoleCommand", flags)!.GetValue(game));
                Assert.Equal(LastToDieWirePhase.Playing, clients[0].LastToDieState.Snapshot!.Phase);
                grantField.SetValue(connection, grant with { Slot = 1 });
                typeof(PlayerHostedRoomSession).GetField("<Host>k__BackingField", flags)!.SetValue(room, host);
            }
            forward.Invoke(game, ["ltd_win"]);
            Assert.NotNull(typeof(Game1).GetField("_embeddedConsoleCommand", flags)!.GetValue(game));
            Pump(host, clients, () =>
            {
                complete.Invoke(game, null);
                return history.Any(line => line.Contains("stage victory triggered"))
                    && clients.All(client => client.LastToDieState.Snapshot?.Phase != LastToDieWirePhase.Playing);
            });
            Assert.All(clients, client =>
            {
                var snapshot = Assert.IsType<LastToDieRunSnapshotMessage>(client.LastToDieState.Snapshot);
                Assert.Equal(Guid.Empty, snapshot.AttemptId);
                Assert.All(snapshot.Players, player => Assert.Equal(0, player.ScoreUnits));
            });
        }
        finally { foreach (var client in clients) client.Dispose(); }
    }

    [Fact]
    public void PracticeUsesOwnSettingsAndReservesHumanSeatsBeforeAddingBots()
    {
        using var host = new EmbeddedSessionHost(new() { LastToDie = false, MaximumPlayers = 4,
            TickRate = 60, RedBots = 2, BlueBots = 3, TimeLimitMinutes = 20, RespawnSeconds = 3 });
        Assert.Equal(60, host.World.Config.TicksPerSecond);
        Assert.Equal(3, host.World.ConfiguredRespawnSeconds);
        Assert.Equal(20, host.World.MatchRules.TimeLimitMinutes);
        Assert.Equal(5, host.World.EnumerateActiveNetworkPlayers().Count());
        Assert.All(host.World.EnumerateActiveNetworkPlayers(), p => Assert.False(p.Player.HasLastToDieSurvivorBuff));
    }

    private static void Pump(EmbeddedSessionHost host, NetworkGameClient[] clients, Func<bool> done)
    {
        for (var tick = 0; tick < 300; tick++)
        {
            host.Advance(1d / 30);
            foreach (var client in clients)
                foreach (var message in client.ReceiveMessages())
                    if (message is WelcomeMessage welcome) client.SetLocalPlayerSlot(welcome.PlayerSlot);
                    else if (message is SnapshotMessage snapshot)
                    {
                        client.AcknowledgeSnapshot(snapshot.Frame);
                        client.NotifyWorldSnapshotApplied(snapshot);
                    }
            if (done()) return;
        }
        Assert.Fail("The embedded authority did not reach the expected state: "
            + string.Join(", ", clients.Select(c => $"slot={c.LocalPlayerSlot}, phase={c.LastToDieState.Snapshot?.Phase}")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MapVotingIsAvailableOnlyInPractice(bool lastToDie)
    {
        using var host = new EmbeddedSessionHost(new() { LastToDie = lastToDie, MaximumPlayers = 4 });
        using var client = new NetworkGameClient();
        Assert.True(client.Connect(new EmbeddedSessionClientTransport(host.CreatePeer(local: true)),
            "Host", 0, out _, clientInstanceId: Guid.NewGuid()));
        Pump(host, [client], () => client.LocalPlayerSlot == 1);
        Assert.True(host.World.TrySetNetworkPlayerAwaitingJoin(1, false));
        client.SendVoteCommand(VoteCommandKind.OpenMenu);
        VoteMenuMessage? catalog = null;
        for (var tick = 0; tick < 30 && catalog is null; tick++)
        {
            host.Advance(1d / 30);
            catalog = client.ReceiveMessages().OfType<VoteMenuMessage>().FirstOrDefault();
        }
        Assert.NotNull(catalog);
        Assert.Equal(lastToDie, catalog.Maps.Count == 0);
    }

    [Fact]
    public void RoomAdmissionKeepsFourthSeatAcrossReconnectAndRejectsImpersonation()
    {
        using var host = new EmbeddedSessionHost(new() { MaximumPlayers = 4 }, requireRoomAdmission: true);
        var identity = Guid.NewGuid();
        using var client = new NetworkGameClient();
        var peer = host.CreatePeer(slot: 4, clientId: identity);
        Assert.True(client.Connect(new EmbeddedSessionClientTransport(peer), "Fourth", 0, out _, clientInstanceId: identity));
        Pump(host, [client], () => client.LocalPlayerSlot == 4);
        client.Disconnect();
        var replacement = host.CreatePeer(slot: 4, clientId: identity);
        Assert.True(client.Connect(new EmbeddedSessionClientTransport(replacement), "Fourth", 0, out _, clientInstanceId: identity));
        Pump(host, [client], () => client.LastToDieState.Snapshot?.Players.Any(p => p.Slot == 4 && p.IsConnected) == true);
        using var impostor = new NetworkGameClient();
        Assert.True(impostor.Connect(new EmbeddedSessionClientTransport(host.CreatePeer(slot: 1, clientId: Guid.NewGuid())),
            "Impostor", 0, out _, clientInstanceId: Guid.NewGuid()));
        var denied = false;
        for (var tick = 0; tick < 20; tick++)
        {
            host.Advance(1d / 30);
            denied |= impostor.ReceiveMessages().OfType<ConnectionDeniedMessage>().Any();
        }
        Assert.True(denied);
        Assert.DoesNotContain(client.LastToDieState.Snapshot!.Players, p => p.Slot == 1);
    }
}
