using System.Reflection;
using OpenGarrison.Client;
using OpenGarrison.Core;
using OpenGarrison.Core.LastToDie;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class LastToDieSurvivorRulesTests
{
    public static IEnumerable<object[]> Classes => Enum.GetValues<PlayerClass>().Select(value => new object[] { value });

    [Theory]
    [MemberData(nameof(Classes))]
    public void SurvivorReducesDamageForEveryClassAndStacksWithOtherDefenses(PlayerClass playerClass)
    {
        var world = CreateWorld(playerClass);
        var ally = AddPlayer(world, 2, PlayerTeam.Red, playerClass);
        var enemy = AddPlayer(world, 3, PlayerTeam.Blue, PlayerClass.Scout);
        world.ConfigureLastToDieStage(1);
        Assert.True(world.TrySetLastToDieSurvivorBuff(1, true));
        Assert.True(world.TrySetLastToDieSurvivorBuff(2, true));
        foreach (var player in new[] { world.LocalPlayer, ally })
        {
            Assert.Equal(40, Damage(world, player, enemy, 50).AppliedHealthDamage);
            var buff = Assert.Single(GameplayBuffPresentationCatalog.Collect(player));
            Assert.Equal(GameplayBuffPresentationCatalog.SurvivorId, buff.Id);
            Assert.Equal(new[] { "Survivor", "Damage Reduction: +20%", "Health Regeneration: +3 HP/s" }, buff.StatLines);
            Assert.True(GameplayBuffPresentationCatalog.HasAny(player));
        }
        Assert.False(enemy.HasLastToDieSurvivorBuff);
        Assert.Equal(50, Damage(world, enemy, world.LocalPlayer, 50).AppliedHealthDamage);

        world.ConfigureLastToDieStage(0);
        Assert.False(world.LocalPlayer.HasLastToDieSurvivorBuff);
        Assert.False(ally.HasLastToDieSurvivorBuff);
        Assert.False(GameplayBuffPresentationCatalog.HasAny(world.LocalPlayer));
    }

    [Fact]
    public void SurvivorMultipliesExistingResistanceAndAppliesToPeriodicAndSelfDamage()
    {
        var world = CreateWorld(PlayerClass.Medic);
        var enemy = AddPlayer(world, 2, PlayerTeam.Blue, PlayerClass.Scout);
        world.TryConfigureLastToDiePlayerBuild(1, [LastToDiePerkIds.Medic.SpikedVest]);
        world.TrySetLastToDieSurvivorBuff(1, true);
        Assert.Equal(34, Damage(world, world.LocalPlayer, enemy, 50).AppliedHealthDamage); // 50 * .85 * .8
        world.TryConfigureLastToDiePlayerBuild(1, []);
        Assert.Equal(8f, Damage(world, world.LocalPlayer, enemy, 10,
            PlayerDamageApplicationKind.Continuous).DamageAfterIncomingModifiers);
        Assert.Equal(8, Damage(world, world.LocalPlayer, world.LocalPlayer, 10).AppliedHealthDamage);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(120)]
    public void SurvivorRegeneratesThreeHealthPerSecondOnBothSlotsWithoutHealingEnemies(int tickRate)
    {
        var world = CreateWorld(PlayerClass.Soldier, tickRate);
        var ally = AddPlayer(world, 2, PlayerTeam.Red, PlayerClass.Spy);
        var enemy = AddPlayer(world, 3, PlayerTeam.Blue, PlayerClass.Scout);
        world.ConfigureLastToDieStage(1);
        world.TrySetLastToDieSurvivorBuff(1, true);
        world.TrySetLastToDieSurvivorBuff(2, true);
        foreach (var player in new[] { world.LocalPlayer, ally, enemy }) player.ForceSetHealth(50);
        Advance(world, tickRate);
        Assert.Equal(53, world.LocalPlayer.Health);
        Assert.Equal(53, ally.Health);
        Assert.Equal(50, enemy.Health);

        world.LocalPlayer.ForceSetHealth(world.LocalPlayer.MaxHealth - 1);
        Advance(world, tickRate);
        Assert.Equal(world.LocalPlayer.MaxHealth, world.LocalPlayer.Health);
        world.ConfigureLastToDieStage(0);
        ally.ForceSetHealth(50);
        Advance(world, tickRate);
        Assert.Equal(50, ally.Health);
    }

    [Fact]
    public void RegenerationAddsToToughAsNailsAndDoesNotRunInPredictionOrReviveTheDead()
    {
        var world = CreateWorld(PlayerClass.Soldier);
        world.ConfigureLastToDieStage(1);
        world.TrySetLastToDieSurvivorBuff(1, true);
        world.TryConfigureLastToDiePlayerBuild(1, [LastToDiePerkIds.Soldier.PassiveHealthRegeneration]);
        world.LocalPlayer.ForceSetHealth(50);
        Advance(world, world.Config.TicksPerSecond * 3);
        Assert.InRange(world.LocalPlayer.Health, 82, 83); // Existing fractional perk healing + exact Survivor healing.

        world.TryConfigureLastToDiePlayerBuild(1, []);
        world.ClientPredictionMode = true;
        world.LocalPlayer.ForceSetHealth(50);
        Advance(world, world.Config.TicksPerSecond);
        Assert.Equal(50, world.LocalPlayer.Health);
        world.ClientPredictionMode = false;
        world.TrySetNetworkPlayerAutomaticRespawnSuppressed(1, true);
        world.LocalPlayer.Kill();
        Advance(world, world.Config.TicksPerSecond * 2);
        Assert.False(world.LocalPlayer.IsAlive);
        Assert.Equal(0, world.LocalPlayer.Health);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    public void EarlyRoundEnemyDeathsAlwaysDropSmallOrLargeKitsIncludingEnvironmentalDeaths(int round, bool environmental)
    {
        var world = CreateWorld(PlayerClass.Soldier);
        var victim = AddPlayer(world, 2, PlayerTeam.Blue, PlayerClass.Scout);
        world.ConfigureLastToDieStage(round);
        for (var i = 0; i < 100; i++)
        {
            victim.ForceSetHealth(victim.MaxHealth);
            Kill(world, victim, environmental ? null : world.LocalPlayer);
        }
        Assert.Equal(100, world.HealthPacks.Count);
        Assert.Contains(world.HealthPacks, pack => pack.Size == HealthPackSize.Small);
        Assert.Contains(world.HealthPacks, pack => pack.Size == HealthPackSize.Large);
        Assert.All(world.HealthPacks, pack => Assert.False(pack.IsMapSpawned));
        Kill(world, world.LocalPlayer, victim);
        Assert.Equal(100, world.HealthPacks.Count);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(9)]
    public void LaterRoundDropChanceIsHalfAndOverridesLegacyPerkDefaults(int round)
    {
        var world = CreateWorld(PlayerClass.Soldier);
        var victim = AddPlayer(world, 2, PlayerTeam.Blue, PlayerClass.Scout);
        world.TryConfigureLastToDiePlayerBuild(1, []); // Legacy defaults used to force 100% at every stage.
        world.ConfigureLastToDieStage(round);
        Assert.Equal(0.5f, LastToDieSurvivorRules.GetHealthPackDropChance(round));
        for (var i = 0; i < 1000; i++)
        {
            victim.ForceSetHealth(victim.MaxHealth);
            Kill(world, victim, world.LocalPlayer);
        }
        // SimulationWorld uses a fixed seed. This checks the actual death path,
        // not just the probability helper or the old experimental setting.
        Assert.InRange(world.HealthPacks.Count, 450, 550);
    }

    [Fact]
    public void OrdinaryPracticeStillHasNoSurvivorBuffOrAutomaticHealthDrops()
    {
        var world = CreateWorld(PlayerClass.Soldier);
        var enemy = AddPlayer(world, 2, PlayerTeam.Blue, PlayerClass.Scout);
        Assert.False(world.LocalPlayer.HasLastToDieSurvivorBuff);
        Assert.Equal(50, Damage(world, world.LocalPlayer, enemy, 50).AppliedHealthDamage);
        Kill(world, enemy, world.LocalPlayer);
        Assert.Empty(world.HealthPacks);
    }

    [Fact]
    public void LeavingHostedPredictionRestoresOrdinaryPracticeRespawnAndDrops()
    {
        var world = CreateWorld(PlayerClass.Soldier);
        Assert.True(world.TryApplyLastToDiePlayerPredictionProfile(1, []));
        world.TrySetLastToDieSurvivorBuff(1, true);
        world.TrySetNetworkPlayerAutomaticRespawnSuppressed(1, true);
        Assert.True(world.IsLastToDieGameplaySettingEnabled(settings => settings.EnableEnemyDroppedWeapons));
        world.ResetLastToDieClientSession();
        world.ConfigureExperimentalGameplaySettings(new());
        world.ConfigureMatchDefaults(respawnSeconds: 1);
        Assert.False(world.IsLastToDieGameplaySettingEnabled(settings => settings.EnableEnemyDroppedWeapons));
        Assert.False(world.IsNetworkPlayerAutomaticRespawnSuppressed(world.LocalPlayer));
        Assert.False(world.LocalPlayer.HasLastToDieSurvivorBuff);
        world.ForceKillLocalPlayer();
        Advance(world, 35);
        Assert.True(world.LocalPlayer.IsAlive);
        var enemy = AddPlayer(world, 2, PlayerTeam.Blue, PlayerClass.Scout);
        for (var i = 0; i < 30; i++)
        {
            enemy.ForceSetHealth(enemy.MaxHealth);
            Kill(world, enemy, world.LocalPlayer);
        }
        Assert.Empty(world.DroppedWeapons);
    }

    [Theory]
    [InlineData(PlayerTeam.Red)]
    [InlineData(PlayerTeam.Blue)]
    public void HostedSoldierDropsDoNotDependOnTheUnusedLocalPlayerTeam(PlayerTeam localTeam)
    {
        var world = CreateWorld(PlayerClass.Scout);
        world.SetLocalPlayerTeam(localTeam);
        var soldier = AddPlayer(world, 2, PlayerTeam.Red, PlayerClass.Soldier);
        var enemy = AddPlayer(world, 3, PlayerTeam.Blue, PlayerClass.Scout);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(2, []));
        for (var i = 0; i < 100; i++)
        {
            enemy.ForceSetHealth(enemy.MaxHealth);
            Kill(world, enemy, soldier);
        }
        Assert.InRange(world.DroppedWeapons.Count, 30, 70);
        var drop = world.DroppedWeapons[0];
        soldier.TeleportTo(drop.X, drop.Y);
        typeof(SimulationWorld).GetMethod("TryHandleDroppedWeaponInteraction", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(world, [soldier]);
        Assert.True(soldier.HasAcquiredWeapon);
    }

    private static SimulationWorld CreateWorld(PlayerClass playerClass, int tickRate = 30)
    {
        var world = new SimulationWorld(new SimulationConfig { TicksPerSecond = tickRate, EnableLocalDummies = false });
        var spawn = new SpawnPoint(100f, 100f);
        world.CombatTestSetLevel(new SimpleLevel("survivor-test", GameModeKind.TeamDeathmatch,
            new WorldBounds(1600f, 512f), 1f, null, 1, 1, spawn, [spawn], [new SpawnPoint(1000f, 100f)],
            [], [], 512f, [], importedFromSource: false));
        world.PrepareLocalPlayerJoin();
        world.SetLocalPlayerTeam(PlayerTeam.Red);
        world.CompleteLocalPlayerJoin(playerClass);
        return world;
    }

    private static PlayerEntity AddPlayer(SimulationWorld world, byte slot, PlayerTeam team, PlayerClass playerClass)
    {
        Assert.True(world.TryPrepareNetworkPlayerJoin(slot));
        Assert.True(world.TrySetNetworkPlayerTeam(slot, team));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(slot, playerClass));
        Assert.True(world.TryGetNetworkPlayer(slot, out var player));
        player.TeleportTo(slot * 300f, 100f);
        return player;
    }

    private static void Advance(SimulationWorld world, int ticks)
    {
        for (var i = 0; i < ticks; i++) world.AdvanceOneTick();
    }

    private static PlayerDamageResolution Damage(SimulationWorld world, PlayerEntity target, PlayerEntity attacker,
        float amount, PlayerDamageApplicationKind kind = PlayerDamageApplicationKind.Instant) => world.ResolvePlayerDamage(target,
            new PlayerDamageRequest(kind, amount, attacker, PlayerEntity.SpyDamageRevealAlpha, DamageEventFlags.None,
                PlayerDamageTraits.None, false, new PlayerDamageUmbrellaOptions(AllowBlock: false)));

    private static void Kill(SimulationWorld world, PlayerEntity victim, PlayerEntity? killer)
    {
        var method = typeof(SimulationWorld).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(method => method.Name == "KillPlayer" && method.GetParameters().Length == 14);
        method.Invoke(world, [victim, false, killer, null, DeadBodyAnimationKind.Default, null, null, null,
            true, true, false, true, -1, false]);
    }
}
