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

    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    public void GuestHeavyHeldPrimaryKeepsFiringAndCanBeRepressed(int tickRate)
    {
        using var session = CreateSession(tickRate, PlayerClass.Heavy);
        var guest = GetGuestPlayer(session);
        var initialAmmo = guest.CurrentShells;

        for (var tick = 0; tick < tickRate * 2; tick += 1)
        {
            AdvanceInput(session, HeldInput(firePrimary: true));
        }

        var ammoAfterHold = guest.CurrentShells;
        Assert.True(
            initialAmmo - ammoAfterHold >= 3,
            $"guest Heavy primary stopped too early: initial={initialAmmo}, afterHold={ammoAfterHold}");

        for (var tick = 0; tick < 8; tick += 1)
        {
            AdvanceInput(session, default);
        }

        var ammoAfterRelease = guest.CurrentShells;
        for (var tick = 0; tick < 12; tick += 1)
        {
            AdvanceInput(session, HeldInput(firePrimary: true));
        }

        Assert.True(
            guest.CurrentShells < ammoAfterRelease,
            $"guest Heavy did not fire after release/repress: release={ammoAfterRelease}, repress={guest.CurrentShells}");
    }

    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    public void GuestMedicHeldSecondaryMaintainsDefaultUberCadence(int tickRate)
    {
        using var session = CreateSession(tickRate, PlayerClass.Medic);
        var guest = GetGuestPlayer(session);
        var heldBoth = HeldInput(firePrimary: true, fireSecondary: true);
        for (var tick = 0; tick < 4; tick += 1)
        {
            AdvanceInput(session, heldBoth);
        }

        // Medic's default M2 Uber executor requires FirePrimary as well as
        // FireSecondary. Fill the meter after both buttons are already held
        // so this exercises the continuous held state rather than a fresh edge.
        guest.FillMedicUberCharge();
        for (var tick = 0; tick < 8; tick += 1)
        {
            AdvanceInput(session, heldBoth);
        }

        Assert.True(guest.IsMedicUbering, "guest Medic did not activate default Uber while both fire channels stayed held");
    }

    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    public void GuestSniperHeldSecondaryDoesNotRetriggerUntilRepress(int tickRate)
    {
        using var session = CreateSession(tickRate, PlayerClass.Sniper);
        var guest = GetGuestPlayer(session);

        for (var tick = 0; tick < tickRate * 2; tick += 1)
        {
            AdvanceInput(session, HeldInput(fireSecondary: true));
        }

        Assert.True(guest.IsSniperScoped, "guest Sniper did not receive the held M2 press");

        for (var tick = 0; tick < 8; tick += 1)
        {
            AdvanceInput(session, default);
        }

        Assert.True(guest.IsSniperScoped, "releasing M2 should not toggle Sniper scope");
        for (var tick = 0; tick < 12; tick += 1)
        {
            AdvanceInput(session, HeldInput(fireSecondary: true));
        }

        Assert.False(guest.IsSniperScoped, "a release followed by one M2 press should toggle scope exactly once");
    }

    [Theory]
    [InlineData(30, PlayerClass.Scout)]
    [InlineData(60, PlayerClass.Scout)]
    [InlineData(30, PlayerClass.Soldier)]
    [InlineData(60, PlayerClass.Soldier)]
    [InlineData(30, PlayerClass.Demoman)]
    [InlineData(60, PlayerClass.Demoman)]
    [InlineData(30, PlayerClass.Engineer)]
    [InlineData(60, PlayerClass.Engineer)]
    [InlineData(30, PlayerClass.Sniper)]
    [InlineData(60, PlayerClass.Sniper)]
    public void GuestStockPrimaryRemainsHeldAcrossWeaponFamilies(int tickRate, PlayerClass guestClass)
    {
        using var session = CreateSession(tickRate, guestClass);
        var guest = GetGuestPlayer(session);
        var initialAmmo = guest.CurrentShells;
        var minimumAmmo = initialAmmo;
        var maximumOwnedProjectiles = 0;
        var maximumSniperTraces = 0;
        var rifleShots = 0;
        var previousCooldown = guest.PrimaryCooldownTicks;

        for (var tick = 0; tick < tickRate * 2; tick += 1)
        {
            AdvanceInput(session, HeldInput(firePrimary: true));
            if (guest.PrimaryCooldownTicks > previousCooldown) rifleShots++;
            previousCooldown = guest.PrimaryCooldownTicks;
            minimumAmmo = Math.Min(minimumAmmo, guest.CurrentShells);
            maximumOwnedProjectiles = Math.Max(maximumOwnedProjectiles, CountOwnedProjectiles(session.Host.World, guest.Id));
            maximumSniperTraces = Math.Max(
                maximumSniperTraces,
                session.Host.World.CombatTraces.Count(trace => trace.Team == guest.Team && trace.IsSniperTracer));
        }

        if (guestClass == PlayerClass.Sniper)
        {
            Assert.True(
                maximumSniperTraces > 0 && rifleShots >= 2,
                $"guest Sniper held primary did not repeat: shots={rifleShots}, traces={maximumSniperTraces}");
        }
        else
        {
            Assert.True(
                initialAmmo - minimumAmmo >= 2,
                $"guest {guestClass} primary stopped too early: initial={initialAmmo}, minimum={minimumAmmo}, projectiles={maximumOwnedProjectiles}");
            Assert.True(
                maximumOwnedProjectiles >= 2,
                $"guest {guestClass} held primary produced too few authoritative projectiles: max={maximumOwnedProjectiles}");
        }

        for (var tick = 0; tick < tickRate * 2; tick += 1)
        {
            AdvanceInput(session, default);
        }

        var ammoBeforeRepress = guest.CurrentShells;
        for (var tick = 0; tick < 12; tick += 1)
        {
            AdvanceInput(session, HeldInput(firePrimary: true));
            minimumAmmo = Math.Min(minimumAmmo, guest.CurrentShells);
        }

        if (guestClass == PlayerClass.Sniper)
        {
            Assert.True(guest.PrimaryCooldownTicks > 0, "guest Sniper did not fire after release/repress");
        }
        else
        {
            Assert.True(
                guest.CurrentShells < ammoBeforeRepress,
                $"guest {guestClass} did not fire after release/repress: before={ammoBeforeRepress}, after={guest.CurrentShells}");
        }
    }

    [Theory]
    [InlineData(30, PlayerClass.Scout)]
    [InlineData(60, PlayerClass.Scout)]
    [InlineData(30, PlayerClass.Medic)]
    [InlineData(60, PlayerClass.Medic)]
    [InlineData(30, PlayerClass.Sniper)]
    [InlineData(60, PlayerClass.Sniper)]
    public void GuestQSelectedSecondaryFirearmsRemainHeld(int tickRate, PlayerClass guestClass)
    {
        using var session = CreateSession(tickRate, guestClass);
        var guest = GetGuestPlayer(session);

        AdvanceInput(session, HeldInput(swapWeapon: true));
        AdvanceInput(session, default);
        Assert.True(guest.IsExperimentalOffhandSelected, $"{guestClass} default secondary was not Q-selected");

        var initialAmmo = guest.ExperimentalOffhandCurrentShells;
        var minimumAmmo = initialAmmo;
        var maximumOwnedProjectiles = 0;
        for (var tick = 0; tick < tickRate * 2; tick += 1)
        {
            AdvanceInput(session, HeldInput(firePrimary: true));
            minimumAmmo = Math.Min(minimumAmmo, guest.ExperimentalOffhandCurrentShells);
            maximumOwnedProjectiles = Math.Max(maximumOwnedProjectiles, CountOwnedProjectiles(session.Host.World, guest.Id));
        }

        Assert.True(
            initialAmmo - minimumAmmo >= 3,
            $"guest {guestClass} secondary firearm stopped too early: initial={initialAmmo}, minimum={minimumAmmo}, projectiles={maximumOwnedProjectiles}");
        Assert.True(maximumOwnedProjectiles >= 3, $"guest {guestClass} secondary firearm produced too few projectiles: max={maximumOwnedProjectiles}");

        for (var tick = 0; tick < tickRate * 2; tick += 1)
        {
            AdvanceInput(session, default);
        }

        var ammoBeforeRepress = guest.ExperimentalOffhandCurrentShells;
        var minimumRepressAmmo = ammoBeforeRepress;
        for (var tick = 0; tick < 12; tick += 1)
        {
            AdvanceInput(session, HeldInput(firePrimary: true));
            minimumRepressAmmo = Math.Min(minimumRepressAmmo, guest.ExperimentalOffhandCurrentShells);
        }

        Assert.True(
            minimumRepressAmmo < ammoBeforeRepress,
            $"guest {guestClass} secondary firearm did not fire after release/repress: before={ammoBeforeRepress}, minimum={minimumRepressAmmo}");
    }

    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    public void GuestSpyHeldUtilityChargesUntilRelease(int tickRate)
    {
        using var session = CreateSession(tickRate, PlayerClass.Spy);
        var guest = GetGuestPlayer(session);

        for (var tick = 0; tick < tickRate * 10 && !guest.IsGrounded; tick += 1)
        {
            AdvanceInput(session, default);
        }
        Assert.True(guest.IsGrounded, "guest Spy did not settle onto the empty-level floor");

        for (var tick = 0; tick < 12; tick += 1)
        {
            AdvanceInput(
                session,
                HeldInput(useAbility: true) with { AimWorldX = 0f, AimWorldY = -500f });
        }

        Assert.True(
            guest.SpySuperjumpChargeTicks >= 5,
            $"guest Spy utility released too early while held: charge={guest.SpySuperjumpChargeTicks}");

        AdvanceInput(session, default);
        Assert.Equal(0, guest.SpySuperjumpChargeTicks);
        Assert.True(guest.IsSpySuperjumping, "guest Spy did not launch when held utility was released");
        Assert.True(guest.SpySuperjumpCooldownTicksRemaining > 0, "guest Spy release did not start cooldown");
    }

    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    public void GuestCivilianHeldUmbrellaReplicatesOpeningAndCanBeReopened(int tickRate)
    {
        using var session = CreateSession(tickRate, PlayerClass.Quote);
        var guest = GetGuestPlayer(session);
        var initialCharge = guest.CivvieUmbrellaChargeTicks;
        var initialSequence = guest.CivvieUmbrellaOpeningSequence;

        for (var tick = 0; tick < tickRate; tick += 1)
        {
            AdvanceInput(session, HeldInput(fireSecondary: true));
        }

        Assert.True(guest.IsCivvieUmbrellaActive, "guest Civilian umbrella did not activate from held M2");
        Assert.True(
            guest.CivvieUmbrellaOpeningSequence > initialSequence,
            $"guest Civilian umbrella did not start an opening sequence: before={initialSequence}, after={guest.CivvieUmbrellaOpeningSequence}");
        Assert.True(
            guest.CivvieUmbrellaOpeningElapsedTicks > 0,
            "guest Civilian umbrella opening progress did not advance while M2 was held");
        Assert.True(
            guest.CivvieUmbrellaChargeTicks < initialCharge,
            $"guest Civilian umbrella opening did not spend charge: before={initialCharge}, after={guest.CivvieUmbrellaChargeTicks}");

        Assert.True(session.Guest.TryGetProtocol64PlayerState(session.Guest.LocalPlayerSlot, out var protocolState));
        var umbrellaState = protocolState.Umbrella
            ?? throw new InvalidOperationException("P64 guest player record omitted Civilian umbrella state");
        Assert.Equal(guest.CivvieUmbrellaChargeTicks, umbrellaState.ChargeTicks);
        Assert.Equal(guest.IsCivvieUmbrellaActive, umbrellaState.IsActive);
        Assert.Equal(guest.IsCivvieUmbrellaBroken, umbrellaState.IsBroken);
        Assert.Equal(guest.CivvieUmbrellaOpeningElapsedTicks, umbrellaState.OpeningElapsedTicks);
        Assert.Equal(guest.CivvieUmbrellaOpeningSequence, umbrellaState.OpeningSequence);
        Assert.Equal(guest.CivvieUmbrellaOpeningAirblastTriggered, umbrellaState.OpeningAirblastTriggered);
        var clientWorld = new SimulationWorld(new SimulationConfig
        {
            TicksPerSecond = tickRate,
            EnableLocalDummies = false,
        });
        session.Guest.ApplyProtocol64StateToWorld(clientWorld);
        Assert.Equal(PlayerClass.Quote, clientWorld.LocalPlayer.ClassId);
        Assert.Equal(umbrellaState, clientWorld.LocalPlayer.CaptureProtocol64UmbrellaState());

        for (var tick = 0; tick < 8; tick += 1)
        {
            AdvanceInput(session, default);
        }

        Assert.False(guest.IsCivvieUmbrellaActive, "guest Civilian umbrella remained active after M2 release");
        var releasedSequence = guest.CivvieUmbrellaOpeningSequence;
        for (var tick = 0; tick < 4; tick += 1)
        {
            AdvanceInput(session, HeldInput(fireSecondary: true));
        }

        Assert.True(guest.IsCivvieUmbrellaActive, "guest Civilian umbrella did not reopen after release/repress");
        Assert.True(
            guest.CivvieUmbrellaOpeningSequence > releasedSequence,
            $"guest Civilian umbrella did not begin a new opening: before={releasedSequence}, after={guest.CivvieUmbrellaOpeningSequence}");
    }

    [Theory]
    [InlineData(30, PlayerClass.Engineer)]
    [InlineData(60, PlayerClass.Engineer)]
    [InlineData(30, PlayerClass.Soldier)]
    [InlineData(60, PlayerClass.Soldier)]
    public void GuestLastToDieHeldPrimaryKeepsFiringAndWeaponIdentityStable(
        int tickRate,
        PlayerClass guestClass)
    {
        using var session = CreateLastToDieSession(tickRate, guestClass);
        var guest = GetGuestPlayer(session);
        var initialIdentity = CaptureWeaponIdentity(guest);
        var initialAmmo = guest.CurrentShells;
        var minimumAmmo = initialAmmo;
        var maximumProjectiles = 0;
        var emittedProjectileIds = new HashSet<int>();

        for (var tick = 0; tick < tickRate * 2; tick += 1)
        {
            AdvanceInput(session, HeldInput(firePrimary: true));
            Assert.Equal(initialIdentity, CaptureWeaponIdentity(guest));
            Assert.True(
                session.Guest.TryGetProtocol64PlayerState(session.Guest.LocalPlayerSlot, out var heldState));
            Assert.NotNull(heldState.Equipment);
            Assert.Equal(initialIdentity.PrimaryItemId, heldState.Equipment!.PrimaryItemId);
            Assert.Equal(initialIdentity.EquippedItemId, heldState.Equipment.EquippedItemId);
            minimumAmmo = Math.Min(minimumAmmo, guest.CurrentShells);
            maximumProjectiles = Math.Max(
                maximumProjectiles,
                CountOwnedProjectiles(session.Host.World, guest.Id));
            foreach (var shot in session.Host.World.Shots.Where(shot => shot.OwnerId == guest.Id))
                emittedProjectileIds.Add(shot.Id);
            foreach (var rocket in session.Host.World.Rockets.Where(rocket => rocket.OwnerId == guest.Id))
                emittedProjectileIds.Add(rocket.Id);
        }

        Assert.True(
            initialAmmo - minimumAmmo >= 2,
            $"LTD guest {guestClass} held fire stopped too early: initial={initialAmmo}, minimum={minimumAmmo}, projectiles={maximumProjectiles}");
        Assert.True(
            emittedProjectileIds.Count >= 2,
            $"LTD guest {guestClass} produced too few authoritative projectiles: emitted={emittedProjectileIds.Count}, maxConcurrent={maximumProjectiles}");

        Assert.Equal(initialIdentity, CaptureWeaponIdentity(guest));
        Assert.True(session.Guest.TryGetProtocol64PlayerState(session.Guest.LocalPlayerSlot, out var state));
        Assert.NotNull(state.Equipment);
        Assert.Equal(initialIdentity.PrimaryItemId, state.Equipment!.PrimaryItemId);
        Assert.Equal(initialIdentity.EquippedItemId, state.Equipment.EquippedItemId);

        for (var tick = 0; tick < 8; tick += 1)
        {
            AdvanceInput(session, default);
        }

        // The held burst can empty the clip. Reloading and immediately firing
        // a shell can leave the observed ammo at zero, so count new authoritative
        // projectiles instead of requiring ammo to fall below its starting value.
        emittedProjectileIds.UnionWith(session.Host.World.Shots.Where(shot => shot.OwnerId == guest.Id).Select(shot => shot.Id));
        emittedProjectileIds.UnionWith(session.Host.World.Rockets.Where(rocket => rocket.OwnerId == guest.Id).Select(rocket => rocket.Id));
        var repressProjectileIds = new HashSet<int>();
        for (var tick = 0; tick < tickRate; tick += 1)
        {
            AdvanceInput(session, HeldInput(firePrimary: true));
            foreach (var shot in session.Host.World.Shots.Where(shot => shot.OwnerId == guest.Id))
                if (!emittedProjectileIds.Contains(shot.Id)) repressProjectileIds.Add(shot.Id);
            foreach (var rocket in session.Host.World.Rockets.Where(rocket => rocket.OwnerId == guest.Id))
                if (!emittedProjectileIds.Contains(rocket.Id)) repressProjectileIds.Add(rocket.Id);
        }

        Assert.True(
            repressProjectileIds.Count > 0,
            $"LTD guest {guestClass} did not emit a new authoritative projectile after release/repress");

        Assert.Equal(initialIdentity, CaptureWeaponIdentity(guest));
    }

    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    public void GuestLastToDieDemoknightPrimaryUsesSwordWithoutOrdinaryProjectiles(int tickRate)
    {
        using var session = CreateLastToDieSession(tickRate, PlayerClass.Demoman);
        var guest = GetGuestPlayer(session);
        Assert.True(guest.IsExperimentalDemoknightEnabled);
        var initialAmmo = guest.CurrentShells;
        var swingCount = 0;
        var previousCooldown = guest.PrimaryCooldownTicks;
        var maximumOwnedProjectiles = 0;

        for (var tick = 0; tick < tickRate * 2; tick += 1)
        {
            AdvanceInput(session, HeldInput(firePrimary: true));
            if (guest.PrimaryCooldownTicks > previousCooldown)
            {
                swingCount += 1;
            }
            previousCooldown = guest.PrimaryCooldownTicks;
            maximumOwnedProjectiles = Math.Max(
                maximumOwnedProjectiles,
                CountOwnedProjectiles(session.Host.World, guest.Id));
        }

        Assert.True(swingCount >= 2, $"expected repeated sword swings, observed {swingCount}");
        Assert.Equal(initialAmmo, guest.CurrentShells);
        Assert.Equal(0, maximumOwnedProjectiles);
    }

    [Theory]
    [InlineData(30, true)]
    [InlineData(60, true)]
    [InlineData(30, false)]
    [InlineData(60, false)]
    public void HostedCivilDefenseTurretReplicatesThroughLateJoinOrLastToDieReconnect(int tickRate, bool lastToDie)
    {
        using var session = lastToDie ? CreateLastToDieSession(tickRate, PlayerClass.Soldier)
            : CreateSession(tickRate, PlayerClass.Soldier, maximumPlayers: 3);
        session.Host.World.CombatTestSetLevel(CreateEmptyLevel());
        var player = GetGuestPlayer(session);
        player.TeleportTo(500f, 980f);
        for (var tick = 0; tick < tickRate; tick++) AdvanceInput(session, default);
        Assert.True(session.Host.World.TryConfigureLastToDiePlayerBuild(session.Guest.LocalPlayerSlot,
            [LastToDiePerkIds.Soldier.CivilDefenseTurret]));
        for (var tick = 0; tick < tickRate * 3; tick++)
        {
            AdvanceInput(session, HeldInput(fireSecondary: true));
            Assert.True(session.Host.World.CivilDefenseTurrets.Count <= 1);
        }
        var turret = Assert.Single(session.Host.World.CivilDefenseTurrets);
        Assert.True(turret.IsBuilt);
        Assert.Equal(player.Id, turret.OwnerPlayerId);
        Assert.Equal(turret.Id, Assert.Single(session.Snapshots[session.Owner].Last().Value.CivilDefenseTurrets).Id);
        Assert.Equal(turret.Id, Assert.Single(session.Snapshots[session.Guest].Last().Value.CivilDefenseTurrets).Id);

        var observerId = Guid.NewGuid();
        if (lastToDie)
        {
            // LtD deliberately reserves its participant slots during a run.
            // Reconnect the teammate while the turret owner stays in the match.
            observerId = (Guid)typeof(NetworkGameClient).GetField("_pendingHelloClientInstanceId",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(session.Owner)!;
            session.Owner.Disconnect();
        }
        using var observer = new NetworkGameClient();
        Assert.True(observer.Connect(new EmbeddedSessionClientTransport(session.Host.CreatePeer()),
            "Returning teammate", 0, out var error, clientInstanceId: observerId), error);
        for (var tick = 0; tick < tickRate * 2; tick++)
        {
            AdvanceInput(session, default);
            Drain(observer, session);
        }
        Assert.Equal(turret.Id, Assert.Single(session.Snapshots[observer].Last().Value.CivilDefenseTurrets).Id);

        turret.FireAt(750f, 900f);
        var observedFire = false;
        for (var tick = 0; tick < 6; tick++)
        {
            AdvanceInput(session, default);
            Drain(observer, session);
            observedFire |= session.Snapshots[observer].Last().Value.CivilDefenseTurrets.Any(state =>
                state.LastShotTargetX == 750f && state.ShotTraceTicksRemaining > 0);
        }
        Assert.True(observedFire);
        typeof(SimulationWorld).GetMethod("DestroyCivilDefenseTurret", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(session.Host.World, [turret]);
        for (var tick = 0; tick < tickRate; tick++)
        {
            AdvanceInput(session, default);
            Drain(observer, session);
        }
        if (!lastToDie) Assert.Empty(session.Snapshots[session.Owner].Last().Value.CivilDefenseTurrets);
        Assert.Empty(session.Snapshots[session.Guest].Last().Value.CivilDefenseTurrets);
        Assert.Empty(session.Snapshots[observer].Last().Value.CivilDefenseTurrets);
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
        host.World.CombatTestSetLevel(CreateEmptyLevel());

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
            host.World.TryGetNetworkPlayer(1, out var ownerPlayer)
            && ownerPlayer.IsAlive
            && ownerPlayer.ClassId == PlayerClass.Scout
            && host.World.TryGetNetworkPlayer(2, out var guestPlayer)
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
            host.World.TryGetNetworkPlayer(guest.LocalPlayerSlot, out var player)
            && player.IsAlive
            && player.ClassId == guestClass);

        return session;
    }

    private static PlayerEntity GetGuestPlayer(TestSession session)
    {
        Assert.True(session.Host.World.TryGetNetworkPlayer(session.Guest.LocalPlayerSlot, out var player));
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
