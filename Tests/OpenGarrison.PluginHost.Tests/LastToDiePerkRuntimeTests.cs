using System.Reflection;
using OpenGarrison.Core;
using OpenGarrison.Core.LastToDie;
using OpenGarrison.Server;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class LastToDiePerkRuntimeTests
{
    [Fact]
    public void DerivedModifiersAggregateOnlyOwnedPerks()
    {
        var modifiers = LastToDieDerivedModifiers.FromPerks(
        [
            LastToDiePerkIds.Spy.Vampire,
            LastToDiePerkIds.Medic.VitalityTrinket,
            LastToDiePerkIds.Sniper.Zen,
        ]);

        Assert.Equal(75, modifiers.MaximumHealthBonus);
        Assert.Equal(LastToDieDerivedModifiers.SpyVampireDamageHealingFraction, modifiers.DamageHealingFraction);
        Assert.Equal(7f, modifiers.ScopedHealingPerSecond);
        Assert.Equal(1f, modifiers.GroundedVsAirborneDamageMultiplier);
        Assert.Equal(1f, modifiers.AirborneVsGroundedDamageMultiplier);
        Assert.Equal(1f, modifiers.CloakedMovementSpeedMultiplier);
        Assert.Equal(0f, modifiers.CloakedHealingPerSecond);
        Assert.Equal(1f, modifiers.CloakedDamageTakenMultiplier);
        Assert.Equal(0f, modifiers.CloakedEvasionChance);

        var stanceModifiers = LastToDieDerivedModifiers.FromPerks(
        [
            LastToDiePerkIds.Spy.Grounded,
            LastToDiePerkIds.Spy.Acrobat,
        ]);
        Assert.Equal(
            LastToDieDerivedModifiers.SpyStanceDamageMultiplier,
            stanceModifiers.GroundedVsAirborneDamageMultiplier);
        Assert.Equal(
            LastToDieDerivedModifiers.SpyStanceDamageMultiplier,
            stanceModifiers.AirborneVsGroundedDamageMultiplier);

        var cloakModifiers = LastToDieDerivedModifiers.FromPerks(
        [
            LastToDiePerkIds.Spy.Rejuvenation,
            LastToDiePerkIds.Spy.ChameleonShell,
            LastToDiePerkIds.Spy.Shroud,
        ]);
        Assert.Equal(
            LastToDieDerivedModifiers.SpyRejuvenationMovementSpeedMultiplier,
            cloakModifiers.CloakedMovementSpeedMultiplier);
        Assert.Equal(
            LastToDieDerivedModifiers.SpyRejuvenationHealingPerSecond,
            cloakModifiers.CloakedHealingPerSecond);
        Assert.Equal(
            LastToDieDerivedModifiers.SpyChameleonShellDamageTakenMultiplier,
            cloakModifiers.CloakedDamageTakenMultiplier);
        Assert.Equal(
            LastToDieDerivedModifiers.SpyShroudEvasionChance,
            cloakModifiers.CloakedEvasionChance);
        var meteredCloakModifiers = LastToDieDerivedModifiers.FromPerks(
        [
            LastToDiePerkIds.Spy.RogueCommander,
            LastToDiePerkIds.Spy.Professional,
        ]);
        Assert.True(meteredCloakModifiers.RogueCommanderEnabled);
        Assert.True(meteredCloakModifiers.ProfessionalEnabled);
        Assert.Equal(new LastToDieDerivedModifiers(), LastToDieDerivedModifiers.FromPerks([]));
    }

    [Fact]
    public void SpyRevolverProfileComposesRanksAndExclusionsDeterministically()
    {
        var rankOne = LastToDieDerivedModifiers.FromPerks(
            [LastToDiePerkIds.Spy.Blunderbuss1]).SpyRevolverProfile!;
        var rankTwo = LastToDieDerivedModifiers.FromPerks(
            [LastToDiePerkIds.Spy.Blunderbuss1, LastToDiePerkIds.Spy.Blunderbuss2]).SpyRevolverProfile!;
        var rankThree = LastToDieDerivedModifiers.FromPerks(
        [
            LastToDiePerkIds.Spy.Blunderbuss1,
            LastToDiePerkIds.Spy.Blunderbuss2,
            LastToDiePerkIds.Spy.Blunderbuss3,
            LastToDiePerkIds.Spy.Agent,
            LastToDiePerkIds.Spy.RubberBullets,
            LastToDiePerkIds.Spy.Ricochet,
            LastToDiePerkIds.Spy.LuckyStrike,
        ]).SpyRevolverProfile!;

        Assert.Equal(13, rankOne.PelletCount);
        Assert.Equal(5f, rankOne.BleedDamagePerSecond);
        Assert.Equal(13, rankTwo.PelletCount);
        Assert.Equal(8f, rankTwo.BleedDamagePerSecond);
        Assert.Equal(1.4f, rankTwo.KnockbackScale);
        Assert.Equal(26, rankThree.PelletCount);

        var stockRevolver = CharacterClassCatalog.Spy.PrimaryWeapon;
        var rankTwoWeapon = rankTwo.ApplyTo(stockRevolver);
        Assert.Equal(2, rankTwoWeapon.MaxAmmo);
        Assert.Equal(11.2f, rankTwoWeapon.DirectHitDamage);
        Assert.Equal(1.4f, rankTwoWeapon.PlayerKnockbackScale);
        Assert.Equal(
            Math.Max(
                1,
                (int)MathF.Ceiling(
                    stockRevolver.AmmoReloadTicks
                        / LastToDieSpyRevolverProfile.BlunderbussBaseReloadSpeedMultiplier)),
            rankTwoWeapon.AmmoReloadTicks);

        var rankThreeWeapon = rankThree.ApplyTo(stockRevolver);
        Assert.Equal(2, rankThreeWeapon.MaxAmmo);
        Assert.Equal(26, rankThreeWeapon.ProjectilesPerShot);
        Assert.Equal(11.2f, rankThreeWeapon.DirectHitDamage);
        Assert.Equal(33.6f, rankThreeWeapon.SpreadDegrees, precision: 4);
        Assert.Equal(
            Math.Max(
                1,
                (int)MathF.Ceiling(
                    stockRevolver.AmmoReloadTicks
                        / LastToDieSpyRevolverProfile.BlunderbussRankThreeReloadSpeedMultiplier)),
            rankThreeWeapon.AmmoReloadTicks);

        Assert.False(rankThree.AgentEnabled);
        Assert.False(rankThree.RubberBulletsEnabled);
        Assert.True(rankThree.RicochetEnabled);
        Assert.True(rankThree.LuckyStrikeEnabled);
        Assert.Equal(rankThree, LastToDieSpyRevolverProfile.Decode(rankThree.Encode()));
        var replicatedState = rankThree.EncodeReplicatedState(luckyStrikeTriggerProgress: 2);
        Assert.Equal(rankThree, LastToDieSpyRevolverProfile.Decode(replicatedState));
        Assert.Equal(2, LastToDieSpyRevolverProfile.DecodeLuckyStrikeTriggerProgress(replicatedState));
    }

    [Fact]
    public void AgentAndBlunderbussProjectIntoPerPlayerWeaponState()
    {
        var world = CreateWorld(PlayerClass.Spy);

        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Agent],
            refillHealth: true));
        Assert.Equal(9, world.LocalPlayer.MaxShells);
        Assert.Equal(9, world.LocalPlayer.CurrentShells);

        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Blunderbuss1],
            refillHealth: true));
        Assert.Equal(1, world.LocalPlayer.MaxShells);
        Assert.Equal(13, world.LocalPlayer.PrimaryWeapon.ProjectilesPerShot);
        Assert.Equal(8f, world.LocalPlayer.PrimaryWeapon.DirectHitDamage);
        Assert.Equal(1, world.LocalPlayer.CurrentShells);
        Assert.True(world.LocalPlayer.TryGetReplicatedStateInt(
            PlayerEntity.LastToDieWeaponReplicatedStateOwnerId,
            PlayerEntity.LastToDieSpyRevolverProfileReplicatedStateKey,
            out _));
    }

    [Fact]
    public void AgentProfileHydratesBeforePredictionAmmoClamp()
    {
        var world = CreateWorld(PlayerClass.Spy);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Agent, LastToDiePerkIds.Spy.LuckyStrike],
            refillHealth: true));
        Assert.False(world.LocalPlayer.AdvanceLastToDieLuckyStrikeTrigger());
        Assert.False(world.LocalPlayer.AdvanceLastToDieLuckyStrikeTrigger());
        var state = world.LocalPlayer.CapturePredictionState();
        var clone = new PlayerEntity(5000, CharacterClassCatalog.Spy, "Clone");

        clone.RestorePredictionState(state);

        Assert.True(clone.LastToDieSpyRevolverProfile.AgentEnabled);
        Assert.Equal(9, clone.MaxShells);
        Assert.Equal(9, clone.CurrentShells);
        Assert.True(clone.LastToDieSpyRevolverProfile.LuckyStrikeEnabled);
        Assert.Equal(2, clone.LastToDieLuckyStrikeTriggerProgress);
    }

    [Fact]
    public void RevolverMotionCorrectionPreservesImmutablePerkPayload()
    {
        var profile = LastToDieSpyRevolverProfile.FromPerks(
            new HashSet<LastToDiePerkId>
            {
                LastToDiePerkIds.Spy.Blunderbuss1,
                LastToDiePerkIds.Spy.Blunderbuss2,
            });
        var shot = new RevolverProjectileEntity(
            42,
            PlayerTeam.Red,
            ownerId: 1,
            x: 0f,
            y: 0f,
            velocityX: 20f,
            velocityY: 0f,
            damagePerHit: 11.2f,
            lastToDieProfile: profile,
            appliesLuckyStrikeStun: true);

        shot.ApplyNetworkState(10f, 2f, 19f, 1f, ticksRemaining: 30);

        Assert.Equal(11.2f, shot.DamageValue);
        Assert.Same(profile, shot.LastToDieProfile);
        Assert.True(shot.AppliesLuckyStrikeStun);

        var snapshot = global::ServerHelpers.ToSnapshotRevolverState(shot);
        Assert.Equal(11.2f, snapshot.DamageValue);
        Assert.Equal(profile.Encode(), snapshot.LastToDieRevolverProfile);
        Assert.True(snapshot.AppliesLuckyStrikeStun);
    }

    [Fact]
    public void LuckyStrikeCountsAcceptedTriggersAndTagsWholeThirdBlunderbussVolley()
    {
        var world = CreateSpyCombatWorld();
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Blunderbuss1, LastToDiePerkIds.Spy.LuckyStrike],
            refillHealth: true));

        var firstVolley = FireAcceptedRevolverTrigger(world, 512f, 0f);
        Assert.Equal(13, firstVolley.Count);
        Assert.All(firstVolley, shot => Assert.False(shot.AppliesLuckyStrikeStun));
        Assert.Equal(1, world.LocalPlayer.LastToDieLuckyStrikeTriggerProgress);
        Assert.False(world.LocalPlayer.TryFirePrimaryWeapon());
        Assert.Equal(1, world.LocalPlayer.LastToDieLuckyStrikeTriggerProgress);

        AdvanceUntilPrimaryReady(world);
        var secondVolley = FireAcceptedRevolverTrigger(world, 512f, 0f);
        Assert.All(secondVolley, shot => Assert.False(shot.AppliesLuckyStrikeStun));
        AdvanceUntilPrimaryReady(world);
        var thirdVolley = FireAcceptedRevolverTrigger(world, 512f, 0f);

        Assert.Equal(13, thirdVolley.Count);
        Assert.All(thirdVolley, shot => Assert.True(shot.AppliesLuckyStrikeStun));
        Assert.Equal(0, world.LocalPlayer.LastToDieLuckyStrikeTriggerProgress);
    }

    [Fact]
    public void LuckyStrikeProgressPersistsAcrossBuildRefreshAndResetsAtRunAndDeathBoundaries()
    {
        var world = CreateSpyCombatWorld();
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.LuckyStrike],
            refillHealth: true));
        Assert.False(world.LocalPlayer.AdvanceLastToDieLuckyStrikeTrigger());

        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.LuckyStrike, LastToDiePerkIds.Spy.Ricochet],
            refillHealth: true));
        Assert.Equal(1, world.LocalPlayer.LastToDieLuckyStrikeTriggerProgress);

        world.ConfigureLastToDieCombatSeed(99);
        Assert.Equal(0, world.LocalPlayer.LastToDieLuckyStrikeTriggerProgress);
        Assert.False(world.LocalPlayer.AdvanceLastToDieLuckyStrikeTrigger());

        world.LocalPlayer.Kill();
        Assert.Equal(0, world.LocalPlayer.LastToDieLuckyStrikeTriggerProgress);
    }

    [Fact]
    public void LuckyStrikeCountsAcceptedSpyRevolverOffhandTriggersWithoutReusingAStaleProc()
    {
        var world = CreateSpyCombatWorld();
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.LuckyStrike],
            refillHealth: true));
        world.LocalPlayer.SetExperimentalOffhandWeapon(CharacterClassCatalog.Spy.PrimaryWeapon);
        Assert.False(world.LocalPlayer.AdvanceLastToDieLuckyStrikeTrigger());
        Assert.False(world.LocalPlayer.AdvanceLastToDieLuckyStrikeTrigger());

        Assert.True(world.LocalPlayer.TryFireExperimentalOffhandWeapon());
        Assert.Equal(0, world.LocalPlayer.LastToDieLuckyStrikeTriggerProgress);
        Assert.True(world.LocalPlayer.LastPrimaryShotAppliesLastToDieLuckyStrikeStun);

        Assert.False(world.LocalPlayer.TryFireExperimentalOffhandWeapon());
        Assert.False(world.LocalPlayer.LastPrimaryShotAppliesLastToDieLuckyStrikeStun);
    }

    [Fact]
    public void LuckyStrikeStunsForOneSecondOnlyAfterAppliedDamage()
    {
        var world = CreateSpyCombatWorld();
        var enemy = AddNetworkPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Blue);
        world.LocalPlayer.TeleportTo(0f, 0f);
        enemy.TeleportTo(80f, 0f);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.LuckyStrike],
            refillHealth: true));
        Assert.False(world.LocalPlayer.AdvanceLastToDieLuckyStrikeTrigger());
        Assert.False(world.LocalPlayer.AdvanceLastToDieLuckyStrikeTrigger());

        _ = FireAcceptedRevolverTrigger(world, enemy.X, enemy.Y);
        AdvanceRevolverShots(world, 6);

        var stun = Assert.Single(world.GetLastToDieStatusEffects(enemy.Id));
        Assert.Equal(LastToDieStatusEffectIds.SpyLuckyStrikeStun, stun.Id);
        Assert.Equal(world.LocalPlayer.Id, stun.SourcePlayerId);
        Assert.Equal(world.Config.TicksPerSecond, stun.RemainingTicks);
        Assert.True(enemy.IsServerStunned);
    }

    [Fact]
    public void InvulnerableLuckyStrikeHitDoesNotApplyStun()
    {
        var world = CreateSpyCombatWorld();
        var enemy = AddNetworkPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Blue);
        world.LocalPlayer.TeleportTo(0f, 0f);
        enemy.TeleportTo(80f, 0f);
        enemy.RefreshUber();
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.LuckyStrike],
            refillHealth: true));
        Assert.False(world.LocalPlayer.AdvanceLastToDieLuckyStrikeTrigger());
        Assert.False(world.LocalPlayer.AdvanceLastToDieLuckyStrikeTrigger());

        _ = FireAcceptedRevolverTrigger(world, enemy.X, enemy.Y);
        AdvanceRevolverShots(world, 6);

        Assert.Empty(world.GetLastToDieStatusEffects(enemy.Id));
        Assert.False(enemy.IsServerStunned);
    }

    [Fact]
    public void RicochetHitsInitialTargetAndAtMostThreeDistinctEnemies()
    {
        var world = CreateSpyCombatWorld();
        var enemies = Enumerable.Range(2, 5)
            .Select(slot => AddNetworkPlayer(
                world,
                checked((byte)slot),
                PlayerClass.Heavy,
                PlayerTeam.Blue))
            .ToArray();
        world.LocalPlayer.TeleportTo(0f, 0f);
        for (var index = 0; index < enemies.Length; index += 1)
        {
            enemies[index].TeleportTo(80f + (index * 60f), 0f);
        }
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Ricochet],
            refillHealth: true));

        _ = FireAcceptedRevolverTrigger(world, enemies[0].X, enemies[0].Y);
        AdvanceRevolverShots(world, 6);

        Assert.All(enemies[..4], enemy => Assert.Equal(enemy.MaxHealth - 28, enemy.Health));
        Assert.Equal(enemies[4].MaxHealth, enemies[4].Health);
        var hitEvents = world.PendingDamageEvents
            .Where(damageEvent => enemies.Any(enemy => enemy.Id == damageEvent.TargetEntityId))
            .ToArray();
        Assert.Equal(enemies[..4].Select(enemy => enemy.Id), hitEvents.Select(damageEvent => damageEvent.TargetEntityId));
    }

    [Fact]
    public void RicochetSkipsGeometryBlockedNearestTarget()
    {
        var world = CreateSpyCombatWorld(
            solids: [new LevelSolid(105f, -50f, 12f, 100f)]);
        var initial = AddNetworkPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Blue);
        var blocked = AddNetworkPlayer(world, 3, PlayerClass.Heavy, PlayerTeam.Blue);
        var visible = AddNetworkPlayer(world, 4, PlayerClass.Heavy, PlayerTeam.Blue);
        world.LocalPlayer.TeleportTo(0f, 0f);
        initial.TeleportTo(80f, 0f);
        blocked.TeleportTo(140f, 0f);
        visible.TeleportTo(20f, 100f);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Ricochet],
            refillHealth: true));

        _ = FireAcceptedRevolverTrigger(world, initial.X, initial.Y);
        AdvanceRevolverShots(world, 6);

        Assert.Equal(initial.MaxHealth - 28, initial.Health);
        Assert.Equal(blocked.MaxHealth, blocked.Health);
        Assert.Equal(visible.MaxHealth - 28, visible.Health);
    }

    [Fact]
    public void RicochetBreaksEqualDistanceTiesByEntityId()
    {
        var world = CreateSpyCombatWorld();
        var initial = AddNetworkPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Blue);
        var lowerIdCandidate = AddNetworkPlayer(world, 3, PlayerClass.Heavy, PlayerTeam.Blue);
        var higherIdCandidate = AddNetworkPlayer(world, 4, PlayerClass.Heavy, PlayerTeam.Blue);
        world.LocalPlayer.TeleportTo(0f, 0f);
        initial.TeleportTo(80f, 0f);
        lowerIdCandidate.TeleportTo(80f, 60f);
        higherIdCandidate.TeleportTo(80f, -60f);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Ricochet],
            refillHealth: true));

        _ = FireAcceptedRevolverTrigger(world, initial.X, initial.Y);
        AdvanceRevolverShots(world, 6);

        var hits = world.PendingDamageEvents
            .Where(damageEvent => damageEvent.TargetEntityId == initial.Id
                || damageEvent.TargetEntityId == lowerIdCandidate.Id
                || damageEvent.TargetEntityId == higherIdCandidate.Id)
            .ToArray();
        Assert.True(hits.Length >= 2);
        Assert.Equal(initial.Id, hits[0].TargetEntityId);
        Assert.Equal(lowerIdCandidate.Id, hits[1].TargetEntityId);
    }

    [Fact]
    public void RicochetSkipsUnrevealedCloakedSpies()
    {
        var world = CreateSpyCombatWorld();
        var initial = AddNetworkPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Blue);
        var cloaked = AddNetworkPlayer(world, 3, PlayerClass.Spy, PlayerTeam.Blue);
        var visible = AddNetworkPlayer(world, 4, PlayerClass.Heavy, PlayerTeam.Blue);
        world.LocalPlayer.TeleportTo(0f, 0f);
        initial.TeleportTo(80f, 0f);
        cloaked.TeleportTo(140f, 0f);
        visible.TeleportTo(80f, 100f);
        Assert.True(cloaked.TryToggleSpyCloak());
        for (var tick = 0; tick < 120 && cloaked.IsSpyVisibleToEnemies; tick += 1)
        {
            world.AdvanceOneTick();
        }
        Assert.False(cloaked.IsSpyVisibleToEnemies);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Ricochet],
            refillHealth: true));

        _ = FireAcceptedRevolverTrigger(world, initial.X, initial.Y);
        AdvanceRevolverShots(world, 6);

        Assert.Equal(cloaked.MaxHealth, cloaked.Health);
        Assert.Equal(visible.MaxHealth - 28, visible.Health);
    }

    [Fact]
    public void RicochetStopsWhenTheInitialHitAppliesNoHealthDamage()
    {
        var world = CreateSpyCombatWorld();
        var invulnerable = AddNetworkPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Blue);
        var next = AddNetworkPlayer(world, 3, PlayerClass.Heavy, PlayerTeam.Blue);
        world.LocalPlayer.TeleportTo(0f, 0f);
        invulnerable.TeleportTo(80f, 0f);
        next.TeleportTo(140f, 0f);
        invulnerable.RefreshUber();
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Ricochet],
            refillHealth: true));

        _ = FireAcceptedRevolverTrigger(world, invulnerable.X, invulnerable.Y);
        AdvanceRevolverShots(world, 6);

        Assert.Equal(invulnerable.MaxHealth, invulnerable.Health);
        Assert.Equal(next.MaxHealth, next.Health);
    }

    [Fact]
    public void RicochetExecutionerCriticalIsEvaluatedPerTarget()
    {
        var world = CreateSpyCombatWorld();
        var lowHealth = AddNetworkPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Blue);
        var fullHealth = AddNetworkPlayer(world, 3, PlayerClass.Heavy, PlayerTeam.Blue);
        world.LocalPlayer.TeleportTo(0f, 0f);
        lowHealth.TeleportTo(80f, 0f);
        fullHealth.TeleportTo(140f, 0f);
        lowHealth.ForceSetHealth(79);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Ricochet, LastToDiePerkIds.Spy.Executioner],
            refillHealth: true));

        _ = FireAcceptedRevolverTrigger(world, lowHealth.X, lowHealth.Y);
        AdvanceRevolverShots(world, 6);

        Assert.Equal(0, lowHealth.Health);
        Assert.Equal(fullHealth.MaxHealth - 28, fullHealth.Health);
        var lowHealthEvent = Assert.Single(
            world.PendingDamageEvents,
            damageEvent => damageEvent.TargetEntityId == lowHealth.Id);
        var fullHealthEvent = Assert.Single(
            world.PendingDamageEvents,
            damageEvent => damageEvent.TargetEntityId == fullHealth.Id);
        Assert.True(lowHealthEvent.Flags.HasFlag(DamageEventFlags.Critical));
        Assert.False(fullHealthEvent.Flags.HasFlag(DamageEventFlags.Critical));
    }

    [Fact]
    public void LuckyStrikeMarkerAndStunCarryAcrossRicochetChain()
    {
        var world = CreateSpyCombatWorld();
        var first = AddNetworkPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Blue);
        var second = AddNetworkPlayer(world, 3, PlayerClass.Heavy, PlayerTeam.Blue);
        world.LocalPlayer.TeleportTo(0f, 0f);
        first.TeleportTo(80f, 0f);
        second.TeleportTo(140f, 0f);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.LuckyStrike, LastToDiePerkIds.Spy.Ricochet],
            refillHealth: true));
        Assert.False(world.LocalPlayer.AdvanceLastToDieLuckyStrikeTrigger());
        Assert.False(world.LocalPlayer.AdvanceLastToDieLuckyStrikeTrigger());

        var volley = FireAcceptedRevolverTrigger(world, first.X, first.Y);
        Assert.True(Assert.Single(volley).AppliesLuckyStrikeStun);
        AdvanceRevolverShots(world, 6);

        Assert.Equal(
            LastToDieStatusEffectIds.SpyLuckyStrikeStun,
            Assert.Single(world.GetLastToDieStatusEffects(first.Id)).Id);
        Assert.Equal(
            LastToDieStatusEffectIds.SpyLuckyStrikeStun,
            Assert.Single(world.GetLastToDieStatusEffects(second.Id)).Id);
    }

    [Fact]
    public void BlunderbussTriggerCapturesOneAtomicProfileAcrossAllPellets()
    {
        var world = CreateSpyCombatWorld();
        world.ConfigureLastToDieCombatSeed(0xB10DUL);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [
                LastToDiePerkIds.Spy.Blunderbuss1,
                LastToDiePerkIds.Spy.Deadly,
            ],
            refillHealth: true));

        Assert.True(world.LocalPlayer.TryFirePrimaryWeapon());
        InvokeFirePrimaryWeapon(
            world,
            world.LocalPlayer,
            world.LocalPlayer.X + 256f,
            world.LocalPlayer.Y);

        Assert.Equal(13, world.RevolverShots.Count);
        var first = world.RevolverShots[0];
        Assert.All(world.RevolverShots, shot =>
        {
            Assert.Same(first.LastToDieProfile, shot.LastToDieProfile);
            Assert.Equal(first.IsCritical, shot.IsCritical);
            Assert.Equal(8f, shot.DamageValue);
        });
    }

    [Theory]
    [InlineData(false, 13, 24f)]
    [InlineData(true, 26, 33.6f)]
    public void BlunderbussAlwaysFiresOneEvenlySpacedArcWhenRandomSpreadIsEnabled(
        bool rankThree,
        int expectedPelletCount,
        float expectedHalfArcDegrees)
    {
        var world = CreateSpyCombatWorld();
        world.RandomSpreadEnabled = true;
        var perks = rankThree
            ? new[]
            {
                LastToDiePerkIds.Spy.Blunderbuss1,
                LastToDiePerkIds.Spy.Blunderbuss2,
                LastToDiePerkIds.Spy.Blunderbuss3,
            }
            : new[] { LastToDiePerkIds.Spy.Blunderbuss1 };
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            perks,
            refillHealth: true));

        var volley = FireAcceptedRevolverTrigger(world, 512f, 0f);

        Assert.Equal(expectedPelletCount, volley.Count);
        var expectedStep = (expectedHalfArcDegrees * 2f) / (expectedPelletCount - 1);
        var actualDegrees = volley
            .Select(static pellet => MathF.Atan2(
                pellet.VelocityY,
                pellet.VelocityX) * (180f / MathF.PI))
            .ToArray();
        Assert.Equal(expectedHalfArcDegrees * 2f, actualDegrees[^1] - actualDegrees[0], precision: 4);
        for (var pelletIndex = 1; pelletIndex < actualDegrees.Length; pelletIndex += 1)
        {
            Assert.Equal(expectedStep, actualDegrees[pelletIndex] - actualDegrees[pelletIndex - 1], precision: 4);
        }
    }

    [Fact]
    public void DeadlyUsesTheRunSeededThirtyFivePercentRollAndStockCriticalDamage()
    {
        const int deadlyProfileBit = 1 << 4;
        Assert.Equal(0.35f, LastToDieSpyRevolverProfile.DeadlyCriticalChance);

        var procWorld = CreateSpyCombatWorld();
        var procTarget = AddNetworkPlayer(procWorld, 2, PlayerClass.Heavy, PlayerTeam.Blue);
        procWorld.LocalPlayer.TeleportTo(0f, 0f);
        procTarget.TeleportTo(80f, 0f);
        procWorld.ConfigureLastToDieCombatSeed(0UL);
        Assert.True(procWorld.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Deadly],
            refillHealth: true));

        var procShot = Assert.Single(FireAcceptedRevolverTrigger(
            procWorld,
            procTarget.X,
            procTarget.Y));
        Assert.True(procShot.IsCritical);
        Assert.Equal(3f, procShot.CriticalDamageMultiplier);
        Assert.Equal(28f, procShot.DamageValue);
        Assert.True(procShot.LastToDieProfile.DeadlyEnabled);
        Assert.NotEqual(0, procShot.LastToDieProfile.Encode() & deadlyProfileBit);
        Assert.True(LastToDieSpyRevolverProfile.Decode(
            procShot.LastToDieProfile.Encode()).DeadlyEnabled);
        Assert.True(procWorld.LocalPlayer.TryGetReplicatedStateInt(
            PlayerEntity.LastToDieWeaponReplicatedStateOwnerId,
            PlayerEntity.LastToDieSpyRevolverProfileReplicatedStateKey,
            out var legacyProfileState));
        Assert.NotEqual(0, legacyProfileState & deadlyProfileBit);
        var protocol64Player = Assert.Single(
            new Protocol64StatePublisher(procWorld).BuildPlayerStateBatch(1).Players,
            player => player.Slot == SimulationWorld.LocalPlayerSlot);
        Assert.NotEqual(
            0,
            protocol64Player.LastToDieSpyRevolverState & deadlyProfileBit);

        var procHealthBefore = procTarget.Health;
        AdvanceRevolverShots(procWorld, 6);
        Assert.Equal(procHealthBefore - 84, procTarget.Health);

        var missWorld = CreateSpyCombatWorld();
        var missTarget = AddNetworkPlayer(missWorld, 2, PlayerClass.Heavy, PlayerTeam.Blue);
        missWorld.LocalPlayer.TeleportTo(0f, 0f);
        missTarget.TeleportTo(80f, 0f);
        missWorld.ConfigureLastToDieCombatSeed(1UL);
        Assert.True(missWorld.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Deadly],
            refillHealth: true));

        var missShot = Assert.Single(FireAcceptedRevolverTrigger(
            missWorld,
            missTarget.X,
            missTarget.Y));
        Assert.False(missShot.IsCritical);
        Assert.Equal(1f, missShot.CriticalDamageMultiplier);
        Assert.Equal(28f, missShot.DamageValue);

        var missHealthBefore = missTarget.Health;
        AdvanceRevolverShots(missWorld, 6);
        Assert.Equal(missHealthBefore - 28, missTarget.Health);
    }

    [Fact]
    public void ExecutionerCritsStrictlyBelowFortyPercentAtImpactTime()
    {
        var world = CreateSpyCombatWorld();
        var enemy = AddNetworkPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Blue);
        world.LocalPlayer.TeleportTo(0f, 0f);
        enemy.TeleportTo(80f, 0f);
        enemy.ForceSetHealth(79);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Executioner],
            refillHealth: true));

        Assert.True(world.LocalPlayer.TryFirePrimaryWeapon());
        InvokeFirePrimaryWeapon(world, world.LocalPlayer, enemy.X, enemy.Y);
        AdvanceRevolverShots(world, 6);

        Assert.Equal(0, enemy.Health);
        Assert.Contains(
            world.PendingDamageEvents,
            damageEvent => damageEvent.TargetEntityId == enemy.Id
                && damageEvent.Flags.HasFlag(DamageEventFlags.Critical));
    }

    [Fact]
    public void ExecutionerDoesNotCritAtExactlyFortyPercent()
    {
        var world = CreateSpyCombatWorld();
        var enemy = AddNetworkPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Blue);
        world.LocalPlayer.TeleportTo(0f, 0f);
        enemy.TeleportTo(80f, 0f);
        enemy.ForceSetHealth((int)(enemy.MaxHealth * LastToDieSpyRevolverProfile.ExecutionerHealthThreshold));
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Executioner],
            refillHealth: true));

        Assert.True(world.LocalPlayer.TryFirePrimaryWeapon());
        InvokeFirePrimaryWeapon(world, world.LocalPlayer, enemy.X, enemy.Y);
        AdvanceRevolverShots(world, 6);

        Assert.Equal(52, enemy.Health);
        Assert.DoesNotContain(
            world.PendingDamageEvents,
            damageEvent => damageEvent.TargetEntityId == enemy.Id
                && damageEvent.Flags.HasFlag(DamageEventFlags.Critical));
    }

    [Fact]
    public void RubberBulletsApplyLaunchAndAttributedSlowOnlyAfterDamage()
    {
        var world = CreateSpyCombatWorld();
        var enemy = AddNetworkPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Blue);
        world.LocalPlayer.TeleportTo(0f, 0f);
        enemy.TeleportTo(80f, 0f);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.RubberBullets],
            refillHealth: true));

        Assert.True(world.LocalPlayer.TryFirePrimaryWeapon());
        InvokeFirePrimaryWeapon(world, world.LocalPlayer, enemy.X, enemy.Y);
        AdvanceRevolverShots(world, 6);

        Assert.True(enemy.VerticalSpeed < 0f);
        var slow = Assert.Single(world.GetLastToDieStatusEffects(enemy.Id));
        Assert.Equal(LastToDieStatusEffectIds.SpyRubberBulletsSlow, slow.Id);
        Assert.Equal(world.LocalPlayer.Id, slow.SourcePlayerId);
        Assert.Equal(0.6f, slow.MovementSpeedMultiplier);
    }

    [Fact]
    public void BlunderbussHitAppliesAttributedBleedAtRankPotency()
    {
        var world = CreateSpyCombatWorld();
        var enemy = AddNetworkPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Blue);
        world.LocalPlayer.TeleportTo(0f, 0f);
        enemy.TeleportTo(80f, 0f);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [
                LastToDiePerkIds.Spy.Blunderbuss1,
                LastToDiePerkIds.Spy.Blunderbuss2,
            ],
            refillHealth: true));

        Assert.True(world.LocalPlayer.TryFirePrimaryWeapon());
        InvokeFirePrimaryWeapon(world, world.LocalPlayer, enemy.X, enemy.Y);
        AdvanceRevolverShots(world, 6);

        var bleed = Assert.Single(world.GetLastToDieStatusEffects(enemy.Id));
        Assert.Equal(LastToDieStatusEffectIds.SpyBlunderbussBleed, bleed.Id);
        Assert.Equal(world.LocalPlayer.Id, bleed.SourcePlayerId);
        Assert.Equal(8f, bleed.DamagePerSecond);
        Assert.Equal(world.Config.TicksPerSecond * 4, bleed.RemainingTicks);
    }

    [Fact]
    public void InvulnerableRevolverHitDoesNotApplyRubberBulletsPayload()
    {
        var world = CreateSpyCombatWorld();
        var enemy = AddNetworkPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Blue);
        world.LocalPlayer.TeleportTo(0f, 0f);
        enemy.TeleportTo(80f, 0f);
        enemy.RefreshUber();
        var verticalSpeedBefore = enemy.VerticalSpeed;
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.RubberBullets],
            refillHealth: true));

        Assert.True(world.LocalPlayer.TryFirePrimaryWeapon());
        InvokeFirePrimaryWeapon(world, world.LocalPlayer, enemy.X, enemy.Y);
        AdvanceRevolverShots(world, 6);

        Assert.Equal(verticalSpeedBefore, enemy.VerticalSpeed);
        Assert.Empty(world.GetLastToDieStatusEffects(enemy.Id));
    }

    [Fact]
    public void VitalityTrinketAppliesToOneNetworkPlayerWithoutCrossTalk()
    {
        var world = CreateWorld(PlayerClass.Medic);
        var teammate = AddNetworkPlayer(world, 2, PlayerClass.Medic, PlayerTeam.Red);

        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Medic.VitalityTrinket],
            refillHealth: true));
        Assert.True(world.TryConfigureLastToDiePlayerBuild(2, []));

        Assert.Equal(CharacterClassCatalog.Medic.MaxHealth + 75, world.LocalPlayer.MaxHealth);
        Assert.Equal(world.LocalPlayer.MaxHealth, world.LocalPlayer.Health);
        Assert.Equal(CharacterClassCatalog.Medic.MaxHealth, teammate.MaxHealth);
    }

    [Fact]
    public void VampireHealsOnlyItsOwnerFromActualEnemyDamage()
    {
        var world = CreateWorld(PlayerClass.Spy);
        var enemy = AddNetworkPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Blue);
        world.LocalPlayer.ForceSetHealth(world.LocalPlayer.MaxHealth - 50);
        enemy.ForceSetHealth(enemy.MaxHealth);

        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Vampire]));
        Assert.True(world.TryConfigureLastToDiePlayerBuild(2, []));

        var healthBefore = world.LocalPlayer.Health;
        Assert.True(world.TryApplyGameplayDamage(enemy.Id, 100f, world.LocalPlayer.Id, null));

        Assert.Equal(healthBefore + 11, world.LocalPlayer.Health);
        Assert.Equal(enemy.MaxHealth - 100, enemy.Health);

        Assert.True(world.TryConfigureLastToDiePlayerBuild(SimulationWorld.LocalPlayerSlot, []));
        Assert.True(world.TryConfigureLastToDiePlayerBuild(2, [LastToDiePerkIds.Spy.Vampire]));
        world.LocalPlayer.ForceSetHealth(world.LocalPlayer.MaxHealth);
        enemy.ForceSetHealth(enemy.MaxHealth - 50);

        var remoteHealthBefore = enemy.Health;
        Assert.True(world.TryApplyGameplayDamage(world.LocalPlayer.Id, 90f, enemy.Id, null));

        Assert.Equal(remoteHealthBefore + 9, enemy.Health);
    }

    [Fact]
    public void GroundedAndAcrobatApplyOneExclusiveAuthoritativeStanceBonus()
    {
        var world = CreateWorld(PlayerClass.Spy);
        var enemy = AddNetworkPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Blue);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Grounded, LastToDiePerkIds.Spy.Acrobat]));

        world.LocalPlayer.RestoreMovementProbeState(isGrounded: true, null, null);
        enemy.RestoreMovementProbeState(isGrounded: false, null, null);
        Assert.True(world.TryApplyGameplayDamage(enemy.Id, 25f, world.LocalPlayer.Id, null));
        Assert.Equal(enemy.MaxHealth - 40, enemy.Health);

        enemy.ForceSetHealth(enemy.MaxHealth);
        world.LocalPlayer.RestoreMovementProbeState(isGrounded: false, null, null);
        enemy.RestoreMovementProbeState(isGrounded: true, null, null);
        Assert.True(world.TryApplyGameplayDamage(enemy.Id, 25f, world.LocalPlayer.Id, null));
        Assert.Equal(enemy.MaxHealth - 40, enemy.Health);

        enemy.ForceSetHealth(enemy.MaxHealth);
        world.LocalPlayer.RestoreMovementProbeState(isGrounded: true, null, null);
        enemy.RestoreMovementProbeState(isGrounded: true, null, null);
        Assert.True(world.TryApplyGameplayDamage(enemy.Id, 25f, world.LocalPlayer.Id, null));
        Assert.Equal(enemy.MaxHealth - 25, enemy.Health);

        enemy.ForceSetHealth(enemy.MaxHealth);
        world.LocalPlayer.RestoreMovementProbeState(isGrounded: false, null, null);
        enemy.RestoreMovementProbeState(isGrounded: false, null, null);
        Assert.True(world.TryApplyGameplayDamage(enemy.Id, 25f, world.LocalPlayer.Id, null));
        Assert.Equal(enemy.MaxHealth - 25, enemy.Health);
    }

    [Fact]
    public void AcrobatUsesImpactStanceRatherThanRevolverTriggerStance()
    {
        var world = CreateSpyCombatWorld();
        var enemy = AddNetworkPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Blue);
        world.LocalPlayer.TeleportTo(0f, 0f);
        enemy.TeleportTo(80f, 0f);
        world.LocalPlayer.RestoreMovementProbeState(isGrounded: true, null, null);
        enemy.RestoreMovementProbeState(isGrounded: false, null, null);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Acrobat],
            refillHealth: true));

        _ = FireAcceptedRevolverTrigger(world, enemy.X, enemy.Y);
        world.LocalPlayer.RestoreMovementProbeState(isGrounded: false, null, null);
        enemy.RestoreMovementProbeState(isGrounded: true, null, null);
        AdvanceRevolverShots(world, 6);

        Assert.Equal(enemy.MaxHealth - 45, enemy.Health);
    }

    [Fact]
    public void GroundedDoesNotAmplifyAttributedPeriodicDamageAndVampireStillHealsFromIt()
    {
        var world = CreateWorld(PlayerClass.Spy);
        var enemy = AddNetworkPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Blue);
        world.LocalPlayer.ForceSetHealth(world.LocalPlayer.MaxHealth - 10);
        world.LocalPlayer.RestoreMovementProbeState(isGrounded: true, null, null);
        enemy.RestoreMovementProbeState(isGrounded: false, null, null);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Grounded, LastToDiePerkIds.Spy.Vampire]));
        Assert.True(world.TryApplyLastToDieStatusEffect(
            enemy.Id,
            world.LocalPlayer.Id,
            LastToDieStatusEffectSpec.Bleed(
                new LastToDieStatusEffectId("ltd.status.test.stance-periodic"),
                world.Config.TicksPerSecond,
                damagePerSecond: 10f)));

        var attackerHealthBefore = world.LocalPlayer.Health;
        for (var tick = 0; tick < world.Config.TicksPerSecond; tick += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(enemy.MaxHealth - 10, enemy.Health);
        Assert.Equal(attackerHealthBefore + 1, world.LocalPlayer.Health);
    }

    [Fact]
    public void VampireUsesActualOverkillDamageWithAStableFractionalAccumulator()
    {
        var world = CreateWorld(PlayerClass.Spy);
        var firstEnemy = AddNetworkPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Blue);
        var secondEnemy = AddNetworkPlayer(world, 3, PlayerClass.Heavy, PlayerTeam.Blue);
        world.LocalPlayer.ForceSetHealth(world.LocalPlayer.MaxHealth - 10);
        firstEnemy.ForceSetHealth(5);
        secondEnemy.ForceSetHealth(5);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Vampire]));

        var healthBefore = world.LocalPlayer.Health;
        Assert.True(world.TryApplyGameplayDamage(firstEnemy.Id, 100f, world.LocalPlayer.Id, null));
        Assert.Equal(healthBefore, world.LocalPlayer.Health);

        Assert.True(world.TryApplyGameplayDamage(secondEnemy.Id, 100f, world.LocalPlayer.Id, null));
        Assert.Equal(healthBefore + 1, world.LocalPlayer.Health);
    }

    [Fact]
    public void VampireIntegerLedgerIsExactAtTheFloatPrecisionBoundary()
    {
        const int appliedDamage = 128_009;
        const int expectedHealing = 14_208;
        var world = CreateWorld(PlayerClass.Spy);
        var enemy = AddNetworkPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Blue);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Vampire],
            baseMaximumHealthOverride: 20_000,
            refillHealth: true));
        Assert.True(world.TrySetNetworkPlayerMaxHealthOverride(2, 200_000, refillHealth: true));
        world.LocalPlayer.ForceSetHealth(1);

        Assert.True(world.TryApplyGameplayDamage(enemy.Id, appliedDamage, world.LocalPlayer.Id, null));

        Assert.Equal(200_000 - appliedDamage, enemy.Health);
        Assert.Equal(1 + expectedHealing, world.LocalPlayer.Health);
    }

    [Fact]
    public void VampireDoesNotBankFullHealthDamageOrCarryFractionsAcrossBuildReset()
    {
        var world = CreateWorld(PlayerClass.Spy);
        var enemy = AddNetworkPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Blue);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Vampire]));

        Assert.True(world.TryApplyGameplayDamage(enemy.Id, 100f, world.LocalPlayer.Id, null));
        world.LocalPlayer.ForceSetHealth(world.LocalPlayer.MaxHealth - 10);
        Assert.True(world.TryApplyGameplayDamage(enemy.Id, 9f, world.LocalPlayer.Id, null));
        Assert.Equal(world.LocalPlayer.MaxHealth - 10, world.LocalPlayer.Health);

        Assert.True(world.TryConfigureLastToDiePlayerBuild(SimulationWorld.LocalPlayerSlot, []));
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Vampire]));
        Assert.True(world.TryApplyGameplayDamage(enemy.Id, 1f, world.LocalPlayer.Id, null));
        Assert.Equal(world.LocalPlayer.MaxHealth - 10, world.LocalPlayer.Health);

        Assert.True(world.TryApplyGameplayDamage(enemy.Id, 8f, world.LocalPlayer.Id, null));
        world.ConfigureLastToDieCombatSeed(0x51CEUL);
        Assert.True(world.TryApplyGameplayDamage(enemy.Id, 1f, world.LocalPlayer.Id, null));
        Assert.Equal(world.LocalPlayer.MaxHealth - 10, world.LocalPlayer.Health);
    }

    [Fact]
    public void VampireFractionalLedgerResetsAtDeath()
    {
        var world = CreateWorld(PlayerClass.Spy);
        var enemy = AddNetworkPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Blue);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Vampire]));
        world.LocalPlayer.ForceSetHealth(world.LocalPlayer.MaxHealth - 10);

        Assert.True(world.TryApplyGameplayDamage(enemy.Id, 9f, world.LocalPlayer.Id, null));
        Assert.True(world.TryApplyGameplayDamage(
            world.LocalPlayer.Id,
            world.LocalPlayer.MaxHealth,
            enemy.Id,
            null));
        Assert.False(world.LocalPlayer.IsAlive);

        world.ForceRespawnLocalPlayer();
        world.LocalPlayer.ForceSetHealth(world.LocalPlayer.MaxHealth - 10);
        Assert.True(world.TryApplyGameplayDamage(enemy.Id, 1f, world.LocalPlayer.Id, null));

        Assert.Equal(world.LocalPlayer.MaxHealth - 10, world.LocalPlayer.Health);
    }

    [Fact]
    public void VampireCreditsAttributedLegacyAfterburnDamage()
    {
        var world = CreateWorld(PlayerClass.Spy);
        var enemy = AddNetworkPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Blue);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Vampire]));
        world.LocalPlayer.ForceSetHealth(world.LocalPlayer.MaxHealth - 10);
        enemy.IgniteAfterburn(
            world.LocalPlayer.Id,
            PlayerEntity.BurnDefaultMaxDurationSourceTicks,
            PlayerEntity.BurnMaxIntensity,
            afterburnFalloff: false,
            burnFalloffAmount: 0f);

        var enemyHealthBefore = enemy.Health;
        for (var tick = 0; tick < world.Config.TicksPerSecond; tick += 1)
        {
            world.AdvanceOneTick();
        }

        var afterburnDamage = enemyHealthBefore - enemy.Health;
        Assert.True(afterburnDamage >= 10);
        Assert.Equal(
            world.LocalPlayer.MaxHealth - 10 + (afterburnDamage * 111 / 1000),
            world.LocalPlayer.Health);
    }

    [Fact]
    public void ReflectedDamageDoesNotTriggerStanceDamageOrVampireHealing()
    {
        var world = CreateWorld(PlayerClass.Spy);
        var enemy = AddNetworkPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Blue);
        world.LocalPlayer.ForceSetHealth(world.LocalPlayer.MaxHealth - 20);
        world.LocalPlayer.RestoreMovementProbeState(isGrounded: true, null, null);
        enemy.RestoreMovementProbeState(isGrounded: false, null, null);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Grounded, LastToDiePerkIds.Spy.Vampire]));

        var attackerHealthBefore = world.LocalPlayer.Health;
        var resolution = world.ResolvePlayerDamage(
            enemy,
            new PlayerDamageRequest(
                PlayerDamageApplicationKind.Instant,
                25f,
                world.LocalPlayer,
                PlayerEntity.SpyDamageRevealAlpha,
                DamageEventFlags.None,
                PlayerDamageTraits.Reflected,
                AllowOsmosisHealOwnedSentries: false,
                new PlayerDamageUmbrellaOptions(AllowBlock: false)));

        Assert.Equal(25, resolution.AppliedHealthDamage);
        Assert.Equal(attackerHealthBefore, world.LocalPlayer.Health);
    }

    [Fact]
    public void RuntimeThornsReflectionDoesNotTriggerVampireOrReflectAgain()
    {
        var world = CreateWorld(PlayerClass.Spy);
        var enemy = AddNetworkPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Blue);
        world.ConfigureExperimentalGameplaySettings(
            new ExperimentalGameplaySettings(PassiveThornsFraction: 1f));
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Vampire]));
        enemy.ForceSetHealth(enemy.MaxHealth);

        var localHealthBefore = world.LocalPlayer.Health;
        Assert.True(world.TryApplyGameplayDamage(world.LocalPlayer.Id, 100f, enemy.Id, null));

        Assert.Equal(localHealthBefore - 100, world.LocalPlayer.Health);
        Assert.Equal(enemy.MaxHealth - 100, enemy.Health);
    }

    [Fact]
    public void VampireCreditsActualDamageFromFatalPrevention()
    {
        var world = CreateWorld(PlayerClass.Soldier);
        var vampire = AddNetworkPlayer(world, 2, PlayerClass.Spy, PlayerTeam.Blue);
        world.ConfigureExperimentalGameplaySettings(
            new ExperimentalGameplaySettings(EnableSoldierLuckyBastard: true));
        world.LocalPlayer.RegisterKillStreakKill(multiKillWindowTicks: 0);
        world.LocalPlayer.RegisterKillStreakKill(multiKillWindowTicks: 0);
        world.LocalPlayer.RegisterKillStreakKill(multiKillWindowTicks: 0);
        world.LocalPlayer.ForceSetHealth(50);
        vampire.ForceSetHealth(vampire.MaxHealth - 20);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            2,
            [LastToDiePerkIds.Spy.Vampire]));

        var vampireHealthBefore = vampire.Health;
        Assert.True(world.TryApplyGameplayDamage(
            world.LocalPlayer.Id,
            100f,
            vampire.Id,
            null));

        Assert.Equal(1, world.LocalPlayer.Health);
        Assert.Equal(vampireHealthBefore + 5, vampire.Health);
    }

    [Fact]
    public void DeadAcrobatOwnerDoesNotEmpowerALateProjectile()
    {
        var world = CreateSpyCombatWorld();
        var enemy = AddNetworkPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Blue);
        world.LocalPlayer.TeleportTo(0f, 0f);
        enemy.TeleportTo(80f, 0f);
        world.LocalPlayer.RestoreMovementProbeState(isGrounded: false, null, null);
        enemy.RestoreMovementProbeState(isGrounded: true, null, null);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Acrobat],
            refillHealth: true));

        _ = FireAcceptedRevolverTrigger(world, enemy.X, enemy.Y);
        world.LocalPlayer.Kill();
        AdvanceRevolverShots(world, 6);

        Assert.Equal(enemy.MaxHealth - RevolverProjectileEntity.DamagePerHit, enemy.Health);
    }

    [Fact]
    public void RejuvenationAppliesMovementAndHealingOnlyWhileLogicallyCloaked()
    {
        var world = CreateWorld(PlayerClass.Spy);
        var stockMaxRunSpeed = world.LocalPlayer.MaxRunSpeed;
        world.LocalPlayer.ForceSetHealth(world.LocalPlayer.MaxHealth - 20);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Rejuvenation]));

        Assert.Equal(stockMaxRunSpeed, world.LocalPlayer.MaxRunSpeed, precision: 3);
        Assert.True(world.LocalPlayer.TryToggleSpyCloak());
        Assert.Equal(
            stockMaxRunSpeed * LastToDieDerivedModifiers.SpyRejuvenationMovementSpeedMultiplier,
            world.LocalPlayer.MaxRunSpeed,
            precision: 3);

        var cloakedHealthBefore = world.LocalPlayer.Health;
        for (var tick = 0; tick < world.Config.TicksPerSecond; tick += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(
            cloakedHealthBefore + (int)LastToDieDerivedModifiers.SpyRejuvenationHealingPerSecond,
            world.LocalPlayer.Health);

        world.LocalPlayer.ForceDecloak();
        var uncloakedHealthBefore = world.LocalPlayer.Health;
        Assert.Equal(stockMaxRunSpeed, world.LocalPlayer.MaxRunSpeed, precision: 3);
        for (var tick = 0; tick < world.Config.TicksPerSecond; tick += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(uncloakedHealthBefore, world.LocalPlayer.Health);

        Assert.True(world.LocalPlayer.TryToggleSpyCloak());
        Assert.True(world.TryConfigureLastToDiePlayerBuild(SimulationWorld.LocalPlayerSlot, []));
        Assert.Equal(stockMaxRunSpeed, world.LocalPlayer.MaxRunSpeed, precision: 3);
    }

    [Fact]
    public void RejuvenationPredictionProfileSurvivesPredictionCaptureAndRestore()
    {
        var world = CreateWorld(PlayerClass.Spy);
        Assert.True(world.TryApplyLastToDiePlayerPredictionProfile(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Rejuvenation.Value]));
        Assert.True(world.LocalPlayer.TryToggleSpyCloak());
        var state = world.LocalPlayer.CapturePredictionState();
        var shadow = new PlayerEntity(9001, CharacterClassCatalog.Spy, "Prediction Shadow");

        shadow.RestorePredictionState(state);

        Assert.True(shadow.IsSpyCloaked);
        Assert.Equal(
            world.LocalPlayer.MaxRunSpeed,
            shadow.MaxRunSpeed,
            precision: 3);
        Assert.False(world.TryGetLastToDiePlayerModifiers(
            SimulationWorld.LocalPlayerSlot,
            out _));
    }

    [Fact]
    public void ChameleonShellResistsDirectAndLegacyAfterburnDamageOnlyWhileCloaked()
    {
        var world = CreateWorld(PlayerClass.Spy);
        var enemy = AddNetworkPlayer(world, 2, PlayerClass.Pyro, PlayerTeam.Blue);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.ChameleonShell]));
        Assert.True(world.LocalPlayer.TryToggleSpyCloak());

        Assert.True(world.TryApplyGameplayDamage(
            world.LocalPlayer.Id,
            100f,
            enemy.Id,
            null));
        Assert.Equal(world.LocalPlayer.MaxHealth - 40, world.LocalPlayer.Health);

        world.LocalPlayer.ForceSetHealth(world.LocalPlayer.MaxHealth);
        world.LocalPlayer.IgniteAfterburn(
            enemy.Id,
            PlayerEntity.BurnDefaultMaxDurationSourceTicks,
            PlayerEntity.BurnMaxIntensity,
            afterburnFalloff: false,
            burnFalloffAmount: 0f);
        for (var tick = 0; tick < world.Config.TicksPerSecond; tick += 1)
        {
            world.AdvanceOneTick();
        }
        Assert.Equal(world.LocalPlayer.MaxHealth - 5, world.LocalPlayer.Health);

        world.LocalPlayer.ExtinguishAfterburn();
        world.LocalPlayer.ForceSetHealth(world.LocalPlayer.MaxHealth);
        Assert.True(world.TryApplyLastToDieStatusEffect(
            world.LocalPlayer.Id,
            enemy.Id,
            LastToDieStatusEffectSpec.Bleed(
                new LastToDieStatusEffectId("test.chameleon.bleed"),
                world.Config.TicksPerSecond,
                damagePerSecond: 10f)));
        for (var tick = 0; tick < world.Config.TicksPerSecond; tick += 1)
        {
            world.AdvanceOneTick();
        }
        Assert.Equal(world.LocalPlayer.MaxHealth - 4, world.LocalPlayer.Health);

        world.LocalPlayer.ForceSetHealth(world.LocalPlayer.MaxHealth);
        world.LocalPlayer.ForceDecloak();
        Assert.True(world.TryApplyGameplayDamage(
            world.LocalPlayer.Id,
            100f,
            enemy.Id,
            null));
        Assert.Equal(world.LocalPlayer.MaxHealth - 100, world.LocalPlayer.Health);
    }

    [Fact]
    public void ShroudEvadesWithoutRevealingAndExpiresExactlyOneSecondAfterDecloak()
    {
        var world = CreateWorld(PlayerClass.Spy);
        var enemy = AddNetworkPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Blue);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Shroud]));
        Assert.True(world.LocalPlayer.TryToggleSpyCloak());
        for (var tick = 0; tick < 20; tick += 1)
        {
            world.LocalPlayer.AdvanceTickState(default, world.Config.FixedDeltaSeconds);
        }
        Assert.Equal(0f, world.LocalPlayer.SpyCloakAlpha);

        var resolution = default(PlayerDamageResolution);
        for (var attempt = 0; attempt < 32; attempt += 1)
        {
            world.LocalPlayer.ForceSetHealth(world.LocalPlayer.MaxHealth);
            for (var fadeTick = 0; fadeTick < 20; fadeTick += 1)
            {
                world.LocalPlayer.AdvanceTickState(default, world.Config.FixedDeltaSeconds);
            }

            resolution = world.ResolvePlayerDamage(
                world.LocalPlayer,
                new PlayerDamageRequest(
                    PlayerDamageApplicationKind.Instant,
                    50f,
                    enemy,
                    PlayerEntity.SpyDamageRevealAlpha,
                    DamageEventFlags.None,
                    PlayerDamageTraits.CanEvade,
                    AllowOsmosisHealOwnedSentries: false,
                    new PlayerDamageUmbrellaOptions(AllowBlock: false)));
            if (resolution.Disposition == PlayerDamageDisposition.Evaded)
            {
                break;
            }
        }

        Assert.Equal(PlayerDamageDisposition.Evaded, resolution.Disposition);
        Assert.Equal(world.LocalPlayer.MaxHealth, world.LocalPlayer.Health);
        Assert.Equal(0f, world.LocalPlayer.SpyCloakAlpha);

        world.LocalPlayer.ForceDecloak();
        Assert.Equal(
            LastToDieDerivedModifiers.SpyShroudEvasionChance,
            InvokeGetLastToDieEvasionChance(world, world.LocalPlayer));
        for (var tick = 0; tick < world.Config.TicksPerSecond - 1; tick += 1)
        {
            world.AdvanceOneTick();
        }
        Assert.Equal(
            LastToDieDerivedModifiers.SpyShroudEvasionChance,
            InvokeGetLastToDieEvasionChance(world, world.LocalPlayer));

        world.AdvanceOneTick();
        Assert.Equal(0f, InvokeGetLastToDieEvasionChance(world, world.LocalPlayer));
    }

    [Fact]
    public void ShroudGraceClearsOnPerkRemovalAndDeath()
    {
        var world = CreateWorld(PlayerClass.Spy);
        var enemy = AddNetworkPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Blue);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Shroud]));
        Assert.True(world.LocalPlayer.TryToggleSpyCloak());
        _ = InvokeGetLastToDieEvasionChance(world, world.LocalPlayer);
        world.LocalPlayer.ForceDecloak();
        Assert.Equal(
            LastToDieDerivedModifiers.SpyShroudEvasionChance,
            InvokeGetLastToDieEvasionChance(world, world.LocalPlayer));

        Assert.True(world.TryConfigureLastToDiePlayerBuild(SimulationWorld.LocalPlayerSlot, []));
        Assert.Equal(0f, InvokeGetLastToDieEvasionChance(world, world.LocalPlayer));

        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Shroud]));
        Assert.True(world.LocalPlayer.TryToggleSpyCloak());
        _ = InvokeGetLastToDieEvasionChance(world, world.LocalPlayer);
        for (var attempt = 0; attempt < 32 && world.LocalPlayer.IsAlive; attempt += 1)
        {
            _ = world.TryApplyGameplayDamage(
                world.LocalPlayer.Id,
                world.LocalPlayer.MaxHealth,
                enemy.Id,
                null);
        }
        Assert.False(world.LocalPlayer.IsAlive);
        world.ForceRespawnLocalPlayer();

        Assert.Equal(0f, InvokeGetLastToDieEvasionChance(world, world.LocalPlayer));
    }

    [Fact]
    public void RogueCommanderMeterDrainsAndRechargesAtExactEightSecondBoundaries()
    {
        var world = CreateWorld(PlayerClass.Spy);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.RogueCommander],
            resetDynamicState: true));
        var player = world.LocalPlayer;
        var fullMeter = 40 * world.Config.TicksPerSecond;
        Assert.Equal(fullMeter, player.LastToDieSpyCloakMeterMaximumUnits);
        Assert.Equal(fullMeter, player.LastToDieSpyCloakMeterUnits);
        Assert.True(player.TryToggleSpyCloak());

        for (var tick = 0; tick < 8 * world.Config.TicksPerSecond - 1; tick += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.True(player.IsSpyCloaked);
        Assert.Equal(5, player.LastToDieSpyCloakMeterUnits);
        world.AdvanceOneTick();
        Assert.False(player.IsSpyCloaked);
        Assert.Equal(0, player.LastToDieSpyCloakMeterUnits);

        for (var tick = 0; tick < 8 * world.Config.TicksPerSecond; tick += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(fullMeter, player.LastToDieSpyCloakMeterUnits);
        Assert.Equal(8, player.LastToDieSpyRogueRampStacks);
    }

    [Fact]
    public void ProfessionalSpendsExactlyTwentyPercentOnlyOnAcceptedCloakedTriggers()
    {
        var world = CreateWorld(PlayerClass.Spy);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Professional],
            refillHealth: true,
            resetDynamicState: true));
        var player = world.LocalPlayer;
        Assert.True(player.TryToggleSpyCloak());
        var fullMeter = player.LastToDieSpyCloakMeterMaximumUnits;
        var shotCost = fullMeter / 5;

        Assert.True(player.TryFirePrimaryWeapon());
        Assert.True(player.IsSpyCloaked);
        Assert.Equal(fullMeter - shotCost, player.LastToDieSpyCloakMeterUnits);

        var exactWorld = CreateWorld(PlayerClass.Spy);
        Assert.True(exactWorld.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Professional],
            refillHealth: true,
            resetDynamicState: true));
        exactWorld.LocalPlayer.HydrateLastToDieSpyCloakMeter(shotCost, fullMeter, 0);
        Assert.True(exactWorld.LocalPlayer.TryToggleSpyCloak());
        Assert.True(exactWorld.LocalPlayer.TryFirePrimaryWeapon());
        Assert.Equal(0, exactWorld.LocalPlayer.LastToDieSpyCloakMeterUnits);

        var rejectedWorld = CreateWorld(PlayerClass.Spy);
        Assert.True(rejectedWorld.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Professional],
            refillHealth: true,
            resetDynamicState: true));
        var rejectedPlayer = rejectedWorld.LocalPlayer;
        rejectedPlayer.HydrateLastToDieSpyCloakMeter(shotCost - 1, fullMeter, 0);
        Assert.True(rejectedPlayer.TryToggleSpyCloak());
        var ammoBefore = rejectedPlayer.CurrentShells;
        var cooldownBefore = rejectedPlayer.PrimaryCooldownTicks;
        Assert.False(rejectedPlayer.TryFirePrimaryWeapon());
        Assert.Equal(shotCost - 1, rejectedPlayer.LastToDieSpyCloakMeterUnits);
        Assert.Equal(ammoBefore, rejectedPlayer.CurrentShells);
        Assert.Equal(cooldownBefore, rejectedPlayer.PrimaryCooldownTicks);
    }

    [Fact]
    public void RogueCommanderRampCapsAndAppliesAdditiveDamageAndMultiplicativeResistance()
    {
        var world = CreateWorld(PlayerClass.Spy);
        var enemy = AddNetworkPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Blue);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.RogueCommander],
            refillHealth: true,
            resetDynamicState: true));

        for (var tick = 0; tick < 12 * world.Config.TicksPerSecond; tick += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(10, world.LocalPlayer.LastToDieSpyRogueRampStacks);
        Assert.True(world.TryApplyGameplayDamage(enemy.Id, 100f, world.LocalPlayer.Id, null));
        Assert.Equal(enemy.MaxHealth - 150, enemy.Health);
        Assert.True(world.TryApplyGameplayDamage(world.LocalPlayer.Id, 100f, enemy.Id, null));
        Assert.Equal(world.LocalPlayer.MaxHealth - 50, world.LocalPlayer.Health);

        Assert.True(world.LocalPlayer.TryToggleSpyCloak());
        Assert.Equal(0, world.LocalPlayer.LastToDieSpyRogueRampStacks);
    }

    [Fact]
    public void SpyCloakMeterAndRogueRampResetAcrossDirectDeathAndSpawnPaths()
    {
        var world = CreateWorld(PlayerClass.Spy);
        var player = world.LocalPlayer;
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.RogueCommander],
            resetDynamicState: true));

        for (var tick = 0; tick < world.Config.TicksPerSecond + 7; tick += 1)
        {
            player.AdvanceLastToDieSpyCloakMeter(world.Config.TicksPerSecond);
        }

        Assert.Equal(1, player.LastToDieSpyRogueRampStacks);
        Assert.Equal(7, player.LastToDieSpyRogueRampTicks);
        Assert.True(player.TryToggleSpyCloak());
        player.AdvanceLastToDieSpyCloakMeter(world.Config.TicksPerSecond);
        Assert.True(player.LastToDieSpyCloakMeterUnits < player.LastToDieSpyCloakMeterMaximumUnits);

        player.Kill();

        Assert.Equal(player.LastToDieSpyCloakMeterMaximumUnits, player.LastToDieSpyCloakMeterUnits);
        Assert.Equal(0, player.LastToDieSpyRogueRampStacks);
        Assert.Equal(0, player.LastToDieSpyRogueRampTicks);

        player.Spawn(PlayerTeam.Red, 24f, 48f);

        Assert.Equal(player.LastToDieSpyCloakMeterMaximumUnits, player.LastToDieSpyCloakMeterUnits);
        Assert.Equal(0, player.LastToDieSpyRogueRampStacks);
        Assert.Equal(0, player.LastToDieSpyRogueRampTicks);
    }

    [Fact]
    public void RogueCommanderOwnsTheOnlyCloakedControlPointException()
    {
        var world = CreateWorld(PlayerClass.Spy);
        Assert.True(world.LocalPlayer.TryToggleSpyCloak());
        Assert.False(world.CanPlayerContributeToControlPoint(world.LocalPlayer));

        world.LocalPlayer.ForceDecloak();
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.RogueCommander],
            resetDynamicState: true));
        Assert.True(world.LocalPlayer.TryToggleSpyCloak());
        Assert.True(world.CanPlayerContributeToControlPoint(world.LocalPlayer));
    }

    [Fact]
    public void MultistabRemovesTheDamageCapAndBackstabsNearbyVisibleEnemiesOnce()
    {
        var world = CreateSpyCombatWorld();
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Multistab],
            resetDynamicState: true));
        var spy = world.LocalPlayer;
        spy.TeleportTo(0f, 0f);
        var primary = AddNetworkPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Blue);
        var nearby = AddNetworkPlayer(world, 3, PlayerClass.Heavy, PlayerTeam.Blue);
        var distant = AddNetworkPlayer(world, 4, PlayerClass.Heavy, PlayerTeam.Blue);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            2,
            [LastToDiePerkIds.Medic.VitalityTrinket],
            resetDynamicState: true));
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            3,
            [LastToDiePerkIds.Medic.VitalityTrinket],
            resetDynamicState: true));
        primary.TeleportTo(24f, 0f);
        nearby.TeleportTo(48f, 24f);
        distant.TeleportTo(180f, 0f);
        primary.ForceSetHealth(primary.MaxHealth);
        nearby.ForceSetHealth(nearby.MaxHealth);
        distant.ForceSetHealth(distant.MaxHealth);

        Assert.True(primary.MaxHealth > StabMaskEntity.DamagePerHit);
        Assert.True(nearby.MaxHealth > StabMaskEntity.DamagePerHit);

        InvokeSpawnStabMask(world, spy, directionDegrees: 0f);
        InvokeAdvanceStabMasks(world);

        Assert.False(primary.IsAlive);
        Assert.False(nearby.IsAlive);
        Assert.True(distant.IsAlive);
        Assert.Equal(distant.MaxHealth, distant.Health);
    }

    [Fact]
    public void SpringLoadedBackstabRestoresEveryJumpBootChargeAndSharedCooldown()
    {
        var world = CreateSpyCombatWorld();
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.SpringLoaded, LastToDiePerkIds.Spy.DoubleJump],
            resetDynamicState: true));
        var spy = world.LocalPlayer;
        spy.TeleportTo(0f, 0f);
        spy.HydrateSpyJumpBootState(
            isActive: false,
            horizontalVelocity: 0f,
            cooldownTicksRemaining: 120,
            availableCharges: 0,
            maximumCharges: 2);
        var target = AddNetworkPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Blue);
        target.TeleportTo(24f, 0f);
        target.ForceSetHealth(350);

        InvokeSpawnStabMask(world, spy, directionDegrees: 0f);
        InvokeAdvanceStabMasks(world);

        Assert.Equal(0, spy.SpySuperjumpCooldownTicksRemaining);
        Assert.Equal(2, spy.SpySuperjumpAvailableCharges);
        Assert.Equal(2, spy.SpySuperjumpMaximumCharges);
    }

    [Fact]
    public void HealstabHealsAnAllyOnlyWhenNoHostileStabTargetIsAvailable()
    {
        var allyOnlyWorld = CreateSpyCombatWorld();
        Assert.True(allyOnlyWorld.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Healstab],
            resetDynamicState: true));
        var allyOnlySpy = allyOnlyWorld.LocalPlayer;
        allyOnlySpy.TeleportTo(0f, 0f);
        var ally = AddNetworkPlayer(allyOnlyWorld, 2, PlayerClass.Heavy, PlayerTeam.Red);
        ally.TeleportTo(24f, 0f);
        ally.ForceSetHealth(ally.MaxHealth - 80);

        InvokeSpawnStabMask(allyOnlyWorld, allyOnlySpy, directionDegrees: 0f);
        InvokeAdvanceStabMasks(allyOnlyWorld);

        Assert.Equal(ally.MaxHealth - 20, ally.Health);

        var contestedWorld = CreateSpyCombatWorld();
        Assert.True(contestedWorld.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Healstab],
            resetDynamicState: true));
        var contestedSpy = contestedWorld.LocalPlayer;
        contestedSpy.TeleportTo(0f, 0f);
        var contestedAlly = AddNetworkPlayer(contestedWorld, 2, PlayerClass.Heavy, PlayerTeam.Red);
        var hostile = AddNetworkPlayer(contestedWorld, 3, PlayerClass.Heavy, PlayerTeam.Blue);
        contestedAlly.TeleportTo(24f, 0f);
        contestedAlly.ForceSetHealth(contestedAlly.MaxHealth - 80);
        hostile.TeleportTo(40f, 0f);
        hostile.ForceSetHealth(350);

        InvokeSpawnStabMask(contestedWorld, contestedSpy, directionDegrees: 0f);
        InvokeAdvanceStabMasks(contestedWorld);

        Assert.Equal(contestedAlly.MaxHealth - 80, contestedAlly.Health);
        Assert.False(hostile.IsAlive);
    }

    [Fact]
    public void HealingHarnessHealsAndExtinguishesOnlyOnAnActualBootLaunch()
    {
        var world = CreateSpyCombatWorld();
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.HealingHarness],
            resetDynamicState: true));
        var spy = world.LocalPlayer;
        spy.ForceSetHealth(spy.MaxHealth - 80);
        spy.RestoreMovementProbeState(
            isGrounded: true,
            remainingAirJumps: null,
            facingDirectionX: null);
        spy.IgniteAfterburn(
            99,
            PlayerEntity.BurnDefaultMaxDurationSourceTicks,
            PlayerEntity.BurnMaxIntensity,
            afterburnFalloff: false,
            burnFalloffAmount: 0f);
        Assert.True(spy.IsBurning);

        var heldInput = new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: spy.X + 96f,
            AimWorldY: spy.Y,
            DebugKill: false,
            UseAbility: true);
        Assert.True(CharacterClassCatalog.RuntimeRegistry.TryGetGameplayAbilityDefinition(
            spy.GameplayLoadoutState.UtilityItemId,
            out var item,
            out var ability));
        var heldResult = world.ExecuteSpySuperjumpAbility(new GameplayAbilityContext
        {
            World = world,
            Player = spy,
            Item = item,
            Ability = ability,
            Phase = GameplayAbilityInputPhase.Held,
            Input = heldInput,
            PreviousInput = default,
            SourceX = spy.X,
            SourceY = spy.Y,
        });

        Assert.True(heldResult.Handled);
        Assert.Equal(spy.MaxHealth - 80, spy.Health);
        Assert.True(spy.IsBurning);

        var releasedResult = world.ExecuteSpySuperjumpAbility(new GameplayAbilityContext
        {
            World = world,
            Player = spy,
            Item = item,
            Ability = ability,
            Phase = GameplayAbilityInputPhase.Released,
            Input = default,
            PreviousInput = heldInput,
            SourceX = spy.X,
            SourceY = spy.Y,
        });

        Assert.True(releasedResult.Handled);
        Assert.Equal(spy.MaxHealth - 20, spy.Health);
        Assert.False(spy.IsBurning);
        Assert.True(spy.SpySuperjumpCooldownTicksRemaining > 0);
    }

    [Fact]
    public void InstastabAcceleratesBackstabWindupRecoveryAndVisualLifetime()
    {
        var world = CreateWorld(PlayerClass.Spy);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Instastab],
            resetDynamicState: true));
        var player = world.LocalPlayer;
        Assert.True(player.TryToggleSpyCloak());

        Assert.True(player.TryStartSpyBackstab(0f));

        Assert.Equal(6, player.SpyBackstabWindupTicksRemaining);
        Assert.Equal(11, player.SpyBackstabVisualTicksRemaining);

        for (var tick = 0; tick < 6; tick += 1)
        {
            player.AdvanceTickState(default, world.Config.FixedDeltaSeconds);
        }

        Assert.Equal(0, player.SpyBackstabWindupTicksRemaining);
        Assert.Equal(3, player.SpyBackstabRecoveryTicksRemaining);
        Assert.True(player.TryConsumeSpyBackstabHitboxTrigger(out _));

        for (var tick = 0; tick < 3; tick += 1)
        {
            player.AdvanceTickState(default, world.Config.FixedDeltaSeconds);
        }

        Assert.True(player.IsSpyBackstabReady);
        Assert.Equal(2, player.SpyBackstabVisualTicksRemaining);
    }

    [Fact]
    public void InstastabVisualPreservesNormalizedWarmupSwingAndFadePhases()
    {
        var speedMultiplier = LastToDieDerivedModifiers.SpyInstastabSpeedMultiplier;
        var stab = new StabAnimEntity(
            id: 1,
            ownerId: 2,
            team: PlayerTeam.Red,
            x: 10f,
            y: 20f,
            directionDegrees: 0f,
            speedMultiplier);

        Assert.Equal(2, stab.WarmupDurationTicks);
        Assert.Equal(6, stab.SwingDurationTicks);
        Assert.Equal(3, stab.FadeOutDurationTicks);
        Assert.Equal(11, stab.LifetimeTicks);
        Assert.Equal(0.01f, stab.Alpha);

        stab.AdvanceOneTick(11f, 21f);
        stab.AdvanceOneTick(12f, 22f);
        Assert.Equal(0, stab.FrameIndex);
        Assert.Equal(0.99f, stab.Alpha);

        stab.AdvanceOneTick(13f, 23f);
        Assert.InRange(stab.FrameIndex, 1, StabAnimEntity.SwingTicks - 1);
        Assert.Equal(0.99f, stab.Alpha);

        for (var tick = 0; tick < 5; tick += 1)
        {
            stab.AdvanceOneTick(13f, 23f);
        }

        Assert.Equal(StabAnimEntity.SwingTicks, stab.FrameIndex);
        Assert.Equal(0.99f, stab.Alpha);

        for (var tick = 0; tick < 3; tick += 1)
        {
            stab.AdvanceOneTick(13f, 23f);
        }

        Assert.True(stab.IsExpired);
        Assert.Equal(0f, stab.Alpha);
    }

    [Fact]
    public void DoubleJumpAllowsTwoLaunchesAndRefillsBothOnSharedCooldown()
    {
        var world = CreateWorld(PlayerClass.Spy);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.DoubleJump],
            resetDynamicState: true));
        var player = world.LocalPlayer;
        var effectiveMaxChargeTicks = player.ResolveSpySuperjumpMaxChargeTicks(
            PlayerEntity.SpySuperjumpMaxChargeTicks);

        Assert.Equal(15, effectiveMaxChargeTicks);
        Assert.Equal(2, player.SpySuperjumpMaximumCharges);
        Assert.Equal(2, player.SpySuperjumpAvailableCharges);

        player.RestoreMovementProbeState(isGrounded: true, remainingAirJumps: null, facingDirectionX: null);
        Assert.True(player.TryStartSpySuperjumpCharge(
            aimDirectionDegrees: 270f,
            leftHeld: false,
            rightHeld: false,
            upHeld: false,
            downHeld: false));
        Assert.True(player.TryReleaseSpySuperjump(
            out _,
            out _,
            maxChargeTicks: effectiveMaxChargeTicks));
        Assert.Equal(1, player.SpySuperjumpAvailableCharges);
        Assert.Equal(PlayerEntity.SpySuperjumpCooldownTicks, player.SpySuperjumpCooldownTicksRemaining);

        player.RestoreMovementProbeState(isGrounded: false, remainingAirJumps: null, facingDirectionX: null);
        Assert.True(player.TryStartSpySuperjumpCharge(
            aimDirectionDegrees: 270f,
            leftHeld: false,
            rightHeld: false,
            upHeld: false,
            downHeld: false));
        Assert.True(player.TryReleaseSpySuperjump(
            out _,
            out _,
            maxChargeTicks: effectiveMaxChargeTicks));
        Assert.Equal(0, player.SpySuperjumpAvailableCharges);
        Assert.Equal(PlayerEntity.SpySuperjumpCooldownTicks, player.SpySuperjumpCooldownTicksRemaining);

        for (var tick = 0; tick < PlayerEntity.SpySuperjumpCooldownTicks; tick += 1)
        {
            player.AdvanceTickState(default, world.Config.FixedDeltaSeconds);
        }

        Assert.Equal(0, player.SpySuperjumpCooldownTicksRemaining);
        Assert.Equal(2, player.SpySuperjumpAvailableCharges);
    }

    [Fact]
    public void DoubleJumpBuildChangesPreserveSpentChargeCount()
    {
        var world = CreateWorld(PlayerClass.Spy);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.DoubleJump],
            resetDynamicState: true));
        var player = world.LocalPlayer;
        player.HydrateSpyJumpBootState(
            isActive: true,
            horizontalVelocity: 100f,
            cooldownTicksRemaining: 120,
            availableCharges: 1,
            maximumCharges: 2);

        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [],
            resetDynamicState: false));

        Assert.Equal(1, player.SpySuperjumpMaximumCharges);
        Assert.Equal(0, player.SpySuperjumpAvailableCharges);
        Assert.Equal(120, player.SpySuperjumpCooldownTicksRemaining);

        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.DoubleJump],
            resetDynamicState: false));

        Assert.Equal(2, player.SpySuperjumpMaximumCharges);
        Assert.Equal(1, player.SpySuperjumpAvailableCharges);
        Assert.Equal(120, player.SpySuperjumpCooldownTicksRemaining);
    }

    [Fact]
    public void ZenUsesPerPlayerFractionalHealingWhileScoped()
    {
        var world = CreateWorld(PlayerClass.Sniper);
        world.LocalPlayer.ForceSetHealth(world.LocalPlayer.MaxHealth - 20);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Sniper.Zen]));
        Assert.True(world.LocalPlayer.TryToggleSniperScope());

        var healthBefore = world.LocalPlayer.Health;
        for (var tick = 0; tick < world.Config.TicksPerSecond; tick += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(healthBefore + 7, world.LocalPlayer.Health);
    }

    private static SimulationWorld CreateWorld(PlayerClass localClass)
    {
        var world = new SimulationWorld();
        world.PrepareLocalPlayerJoin();
        world.CompleteLocalPlayerJoin(localClass);
        return world;
    }

    private static SimulationWorld CreateSpyCombatWorld(IReadOnlyList<LevelSolid>? solids = null)
    {
        var world = CreateWorld(PlayerClass.Spy);
        world.RandomSpreadEnabled = false;
        SetCombatLevel(
            world,
            new SimpleLevel(
                name: "ltd_spy_revolver_test",
                mode: GameModeKind.CaptureTheFlag,
                bounds: new WorldBounds(2048f, 2048f),
                mapScale: 1f,
                backgroundAssetName: null,
                mapAreaIndex: 1,
                mapAreaCount: 1,
                localSpawn: new SpawnPoint(0f, 0f),
                redSpawns: [new SpawnPoint(0f, 0f)],
                blueSpawns: [new SpawnPoint(256f, 0f)],
                intelBases:
                [
                    new IntelBaseMarker(PlayerTeam.Red, 0f, 0f),
                    new IntelBaseMarker(PlayerTeam.Blue, 256f, 0f),
                ],
                roomObjects: [],
                floorY: 2048f,
                solids: solids ?? [],
                importedFromSource: false));
        world.ForceRespawnLocalPlayer();
        return world;
    }

    private static PlayerEntity AddNetworkPlayer(
        SimulationWorld world,
        byte slot,
        PlayerClass playerClass,
        PlayerTeam team)
    {
        Assert.True(world.TryPrepareNetworkPlayerJoin(slot));
        Assert.True(world.TrySetNetworkPlayerTeam(slot, team));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(slot, playerClass));
        Assert.True(world.TryGetNetworkPlayer(slot, out var player));
        return player;
    }

    private static void SetCombatLevel(SimulationWorld world, SimpleLevel level)
    {
        var method = typeof(SimulationWorld).GetMethod(
            "CombatTestSetLevel",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        _ = method!.Invoke(world, [level]);
    }

    private static float InvokeGetLastToDieEvasionChance(
        SimulationWorld world,
        PlayerEntity player)
    {
        var method = typeof(SimulationWorld).GetMethod(
            "GetLastToDieEvasionChance",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return (float)method!.Invoke(world, [player])!;
    }

    private static void InvokeFirePrimaryWeapon(
        SimulationWorld world,
        PlayerEntity player,
        float aimWorldX,
        float aimWorldY)
    {
        var method = typeof(SimulationWorld).GetMethod(
            "FirePrimaryWeapon",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        _ = method!.Invoke(world, [player, aimWorldX, aimWorldY]);
    }

    private static void InvokeSpawnStabMask(
        SimulationWorld world,
        PlayerEntity player,
        float directionDegrees)
    {
        var method = typeof(SimulationWorld).GetMethod(
            "SpawnStabMask",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        _ = method!.Invoke(world, [player, directionDegrees]);
    }

    private static void InvokeAdvanceStabMasks(SimulationWorld world)
    {
        var method = typeof(SimulationWorld).GetMethod(
            "AdvanceStabMasks",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        _ = method!.Invoke(world, null);
    }

    private static void AdvanceRevolverShots(SimulationWorld world, int ticks)
    {
        var method = typeof(SimulationWorld).GetMethod(
            "AdvanceRevolverShots",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        for (var tick = 0; tick < ticks; tick += 1)
        {
            _ = method!.Invoke(world, null);
        }
    }

    private static IReadOnlyList<RevolverProjectileEntity> FireAcceptedRevolverTrigger(
        SimulationWorld world,
        float aimWorldX,
        float aimWorldY)
    {
        var existingIds = world.RevolverShots.Select(static shot => shot.Id).ToHashSet();
        Assert.True(world.LocalPlayer.TryFirePrimaryWeapon());
        InvokeFirePrimaryWeapon(world, world.LocalPlayer, aimWorldX, aimWorldY);
        return world.RevolverShots
            .Where(shot => !existingIds.Contains(shot.Id))
            .ToArray();
    }

    private static void AdvanceUntilPrimaryReady(SimulationWorld world)
    {
        for (var tick = 0;
             tick < 120
                && (world.LocalPlayer.PrimaryCooldownTicks > 0
                    || world.LocalPlayer.CurrentShells < world.LocalPlayer.PrimaryWeapon.AmmoPerShot);
             tick += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(0, world.LocalPlayer.PrimaryCooldownTicks);
        Assert.True(world.LocalPlayer.CurrentShells >= world.LocalPlayer.PrimaryWeapon.AmmoPerShot);
    }
}
