using OpenGarrison.Client;
using OpenGarrison.ClientShared;
using OpenGarrison.Core;
using OpenGarrison.Core.LastToDie;
using OpenGarrison.GameplayModding;
using OpenGarrison.Protocol;
using OpenGarrison.SessionRuntime;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class CoopHeldFireRegressionTests
{
    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    public void GuestPyroHeldPrimaryKeepsBurningAndCanBeRepressed(int tickRate)
    {
        using var session = CreateSession(tickRate, PlayerClass.Pyro);
        var guest = GetGuestPlayer(session);
        var initialFuel = guest.PyroPrimaryFuelScaled;
        var maximumFlames = 0;

        for (var tick = 0; tick < tickRate * 2; tick += 1)
        {
            AdvanceInput(session, HeldInput(firePrimary: true));
            maximumFlames = Math.Max(maximumFlames, session.Host.World.Flames.Count);
        }

        var fuelAfterHold = guest.PyroPrimaryFuelScaled;
        Assert.True(
            initialFuel - fuelAfterHold >= PlayerEntity.PyroPrimaryFlameCostScaled * 3,
            $"guest Pyro fuel stopped too early: initial={initialFuel}, afterHold={fuelAfterHold}, flames={maximumFlames}");
        Assert.True(maximumFlames >= 2, $"expected multiple authoritative flames, observed max={maximumFlames}");
        Assert.True(session.Guest.TryGetProtocol64PlayerState(session.Guest.LocalPlayerSlot, out var state));
        Assert.Equal(fuelAfterHold, state.PyroPrimaryFuelScaled);

        for (var tick = 0; tick < 8; tick += 1)
        {
            AdvanceInput(session, default);
        }

        var fuelAfterRelease = guest.PyroPrimaryFuelScaled;
        for (var tick = 0; tick < 12; tick += 1)
        {
            AdvanceInput(session, HeldInput(firePrimary: true));
        }

        Assert.True(
            guest.PyroPrimaryFuelScaled < fuelAfterRelease,
            $"guest Pyro did not fire after release/repress: release={fuelAfterRelease}, repress={guest.PyroPrimaryFuelScaled}");
    }

    private static TestSession CreateSession(int tickRate, PlayerClass guestClass, int maximumPlayers = 2)
    {
        var host = new EmbeddedSessionHost(new EmbeddedSessionOptions
        {
            LastToDie = false,
            MaximumPlayers = maximumPlayers,
            TickRate = tickRate,
            Map = "Harvest",
            RedBots = 0,
            BlueBots = 0,
            Seed = 123,
        });
        host.World.TestSetLevel(CreateEmptyLevel());

        var owner = new NetworkGameClient();
        var guest = new NetworkGameClient();
        Assert.True(owner.Connect(
            new EmbeddedSessionClientTransport(host.CreatePeer(local: true)),
            "Owner",
            0,
            out var ownerError,
            clientInstanceId: Guid.NewGuid()), ownerError);
        Assert.True(guest.Connect(
            new EmbeddedSessionClientTransport(host.CreatePeer()),
            "Guest",
            0,
            out var guestError,
            clientInstanceId: Guid.NewGuid()), guestError);

        var session = new TestSession(host, owner, guest, tickRate);
        PumpUntil(session, () => owner.LocalPlayerSlot == 1 && guest.LocalPlayerSlot == 2);

        owner.QueueTeamSelection(PlayerTeam.Blue);
        owner.QueueClassSelection(PlayerClass.Scout);
        guest.QueueTeamSelection(PlayerTeam.Red);
        guest.QueueClassSelection(guestClass);
        PumpUntil(session, () =>
            host.World.NetworkPlayers.TryGetNetworkPlayer(1, out var ownerPlayer)
            && ownerPlayer.IsAlive
            && ownerPlayer.ClassId == PlayerClass.Scout
            && host.World.NetworkPlayers.TryGetNetworkPlayer(2, out var guestPlayer)
            && guestPlayer.IsAlive
            && guestPlayer.ClassId == guestClass);
        return session;
    }

    private static TestSession CreateLastToDieSession(int tickRate, PlayerClass guestClass)
    {
        var host = new EmbeddedSessionHost(new EmbeddedSessionOptions
        {
            LastToDie = true,
            MaximumPlayers = 2,
            TickRate = tickRate,
            Map = "Harvest",
            RedBots = 0,
            BlueBots = 0,
            Seed = 123,
        });
        var owner = new NetworkGameClient();
        var guest = new NetworkGameClient();
        Assert.True(owner.Connect(
            new EmbeddedSessionClientTransport(host.CreatePeer(local: true)),
            "Owner",
            0,
            out var ownerError,
            clientInstanceId: Guid.NewGuid()), ownerError);
        Assert.True(guest.Connect(
            new EmbeddedSessionClientTransport(host.CreatePeer()),
            "Guest",
            0,
            out var guestError,
            clientInstanceId: Guid.NewGuid()), guestError);

        var session = new TestSession(host, owner, guest, tickRate);
        PumpLastToDieUntil(session, () =>
            owner.LocalPlayerSlot == 1
            && guest.LocalPlayerSlot == 2
            && owner.LastToDieState.Snapshot?.Players.Count == 2
            && guest.LastToDieState.Snapshot?.Players.Count == 2);

        owner.SendLastToDieCommand(LastToDieCommandKind.Ready);
        guest.SendLastToDieCommand(LastToDieCommandKind.Ready);
        PumpLastToDieUntil(session, () =>
            IsLastToDieReady(owner, owner.LocalPlayerSlot)
            && IsLastToDieReady(guest, guest.LocalPlayerSlot));

        owner.SendLastToDieCommand(LastToDieCommandKind.RequestStart);
        PumpLastToDieUntil(session, () =>
            owner.LastToDieState.Snapshot?.Phase == LastToDieWirePhase.SurvivorChoice
            && guest.LastToDieState.Snapshot?.Phase == LastToDieWirePhase.SurvivorChoice);

        var ownerSurvivor = guestClass == PlayerClass.Engineer
            ? LastToDieSurvivorCatalog.SoldierId.Value
            : LastToDieSurvivorCatalog.EngineerId.Value;
        var guestSurvivor = guestClass switch
        {
            PlayerClass.Engineer => LastToDieSurvivorCatalog.EngineerId.Value,
            PlayerClass.Demoman => LastToDieSurvivorCatalog.DemoknightId.Value,
            _ => LastToDieSurvivorCatalog.SoldierId.Value,
        };
        owner.SendLastToDieCommand(LastToDieCommandKind.ChooseSurvivor, ownerSurvivor);
        guest.SendLastToDieCommand(LastToDieCommandKind.ChooseSurvivor, guestSurvivor);
        PumpLastToDieUntil(session, () =>
            !string.IsNullOrWhiteSpace(GetLastToDiePlayer(owner, owner.LocalPlayerSlot).SurvivorId)
            && !string.IsNullOrWhiteSpace(GetLastToDiePlayer(guest, guest.LocalPlayerSlot).SurvivorId));

        PumpLastToDieUntil(session, () =>
            owner.LastToDieState.Snapshot?.Phase == LastToDieWirePhase.RewardChoice
            && guest.LastToDieState.Snapshot?.Phase == LastToDieWirePhase.RewardChoice);
        var ownerOffer = GetLastToDiePlayer(owner, owner.LocalPlayerSlot);
        var guestOffer = GetLastToDiePlayer(guest, guest.LocalPlayerSlot);
        owner.SendLastToDieCommand(
            LastToDieCommandKind.SelectReward,
            SelectNeutralReward(
                ownerOffer,
                guestClass == PlayerClass.Engineer ? PlayerClass.Soldier : PlayerClass.Engineer),
            ownerOffer.ActiveOfferId);
        guest.SendLastToDieCommand(
            LastToDieCommandKind.SelectReward,
            SelectNeutralReward(guestOffer, guestClass),
            guestOffer.ActiveOfferId);
        PumpLastToDieUntil(session, () =>
            owner.LastToDieState.Snapshot?.Phase == LastToDieWirePhase.Playing
            && guest.LastToDieState.Snapshot?.Phase == LastToDieWirePhase.Playing);

        PumpLastToDieUntil(session, () =>
            host.World.NetworkPlayers.TryGetNetworkPlayer(guest.LocalPlayerSlot, out var player)
            && player.IsAlive
            && player.ClassId == guestClass);

        return session;
    }

    private static PlayerEntity GetGuestPlayer(TestSession session)
    {
        Assert.True(session.Host.World.NetworkPlayers.TryGetNetworkPlayer(session.Guest.LocalPlayerSlot, out var player));
        return player;
    }

    private static void PumpUntil(TestSession session, Func<bool> condition)
    {
        for (var tick = 0; tick < session.TickRate * 10; tick += 1)
        {
            AdvanceInput(session, default);
            if (condition())
            {
                return;
            }
        }

        Assert.True(condition(), "embedded P64 practice session did not reach the expected state");
    }

    private static void PumpLastToDieUntil(TestSession session, Func<bool> condition)
    {
        for (var tick = 0; tick < session.TickRate * 30; tick += 1)
        {
            AdvanceInput(session, default);
            if (condition())
            {
                return;
            }
        }

        Assert.True(condition(), "embedded P64 LTD session did not reach the expected state");
    }

    private static bool IsLastToDieReady(NetworkGameClient client, byte slot)
        => client.LastToDieState.Snapshot?.Players
            .FirstOrDefault(player => player.Slot == slot)?.IsReady == true;

    private static LastToDiePlayerSnapshotMessage GetLastToDiePlayer(
        NetworkGameClient client,
        byte slot)
    {
        var player = client.LastToDieState.Snapshot?.Players
            .FirstOrDefault(candidate => candidate.Slot == slot);
        Assert.NotNull(player);
        return player!;
    }

    private static string SelectNeutralReward(
        LastToDiePlayerSnapshotMessage player,
        PlayerClass playerClass)
    {
        var preferred = playerClass == PlayerClass.Engineer
            ? new[]
            {
                LastToDiePerkIds.Engineer.GuardianMatrix.Value,
                LastToDiePerkIds.Engineer.RegenerativeDiode.Value,
                LastToDiePerkIds.Engineer.HardwareHardener.Value,
                LastToDiePerkIds.Engineer.IntegrityProjector.Value,
                LastToDiePerkIds.Engineer.MisdirectionField.Value,
                LastToDiePerkIds.Engineer.ConfusionField.Value,
                LastToDiePerkIds.Engineer.GravitonAffixer.Value,
                LastToDiePerkIds.Engineer.AuraEnergizer.Value,
                LastToDiePerkIds.Engineer.EntanglementTraverser.Value,
                LastToDiePerkIds.Engineer.AlchemicalAnode.Value,
                LastToDiePerkIds.Engineer.EfficiencyStabilizer.Value,
                LastToDiePerkIds.Engineer.MateriaRecycler.Value,
            }
            : new[]
            {
                LastToDiePerkIds.Soldier.PassiveHealthRegeneration.Value,
                LastToDiePerkIds.Soldier.HealOnDamage.Value,
                LastToDiePerkIds.Soldier.HealOnKill.Value,
                LastToDiePerkIds.Soldier.InvincibilityOnKill.Value,
                LastToDiePerkIds.Soldier.RageCaptureLockout.Value,
                LastToDiePerkIds.Soldier.FogOfWar.Value,
            };

        return player.ActiveOfferChoices.FirstOrDefault(preferred.Contains)
            ?? player.ActiveOfferChoices[0];
    }

    private static (string PrimaryItemId, string EquippedItemId, GameplayEquipmentSlot Slot)
        CaptureWeaponIdentity(PlayerEntity player)
        => (
            player.GameplayLoadoutState.PrimaryItemId,
            player.GameplayLoadoutState.EquippedItemId,
            player.SelectedGameplayEquippedSlot);

    private static void AdvanceInput(TestSession session, PlayerInputSnapshot input)
    {
        session.Owner.SendInput(default, 0f, 0f);
        if (input.AimWorldX == 0f && input.AimWorldY == 0f)
        {
            input = input with { AimWorldX = 500f, AimWorldY = -500f };
        }

        session.Guest.SendInput(input, 0f, 0f);
        session.Host.Advance(1d / session.TickRate);
        Drain(session.Owner, session);
        Drain(session.Guest, session);
    }

    private static void Drain(NetworkGameClient client, TestSession session)
    {
        foreach (var message in client.ReceiveMessages())
        {
            if (message is WelcomeMessage welcome)
            {
                client.SetLocalPlayerSlot(welcome.PlayerSlot);
            }
            else if (message is SnapshotMessage snapshot)
            {
                if (!session.Snapshots.TryGetValue(client, out var history))
                    session.Snapshots[client] = history = new();
                history.TryGetValue(snapshot.BaselineFrame, out var baseline);
                Assert.True(!snapshot.IsDelta || baseline is not null, "test client is missing its acknowledged baseline");
                history[snapshot.Frame] = SnapshotDelta.ToFullSnapshot(snapshot, baseline);
                client.AcknowledgeSnapshot(snapshot.Frame);
                client.NotifyWorldSnapshotApplied(snapshot);
            }
        }
    }

    private static PlayerInputSnapshot HeldInput(
        bool firePrimary = false,
        bool fireSecondary = false,
        bool swapWeapon = false,
        bool useAbility = false)
        => default(PlayerInputSnapshot) with
        {
            FirePrimary = firePrimary,
            FireSecondary = fireSecondary,
            SwapWeapon = swapWeapon,
            UseAbility = useAbility,
        };

    private static int CountOwnedProjectiles(SimulationWorld world, int ownerId)
        => world.Shots.Count(projectile => projectile.OwnerId == ownerId)
            + world.Rockets.Count(projectile => projectile.OwnerId == ownerId)
            + world.Grenades.Count(projectile => projectile.OwnerId == ownerId)
            + world.Mines.Count(projectile => projectile.OwnerId == ownerId)
            + world.Needles.Count(projectile => projectile.OwnerId == ownerId);

    private static SimpleLevel CreateEmptyLevel()
    {
        var redSpawn = new SpawnPoint(300f, 500f);
        var blueSpawn = new SpawnPoint(1700f, 500f);
        return new SimpleLevel(
            "coop-held-fire-regression",
            GameModeKind.TeamDeathmatch,
            new WorldBounds(2048f, 2048f),
            1f,
            null,
            1,
            1,
            redSpawn,
            [redSpawn],
            [blueSpawn],
            [],
            [],
            floorY: 1024f,
            [new LevelSolid(0f, 1024f, 2048f, 1024f)],
            importedFromSource: false);
    }

    private sealed record TestSession(
        EmbeddedSessionHost Host,
        NetworkGameClient Owner,
        NetworkGameClient Guest,
        int TickRate) : IDisposable
    {
        public Dictionary<NetworkGameClient, SortedDictionary<ulong, SnapshotMessage>> Snapshots { get; } = new();

        public void Dispose()
        {
            Owner.Dispose();
            Guest.Dispose();
            Host.Dispose();
        }
    }
}
