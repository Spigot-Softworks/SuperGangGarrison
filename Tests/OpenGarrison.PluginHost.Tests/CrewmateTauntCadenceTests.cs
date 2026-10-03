using OpenGarrison.Core;
using OpenGarrison.GameplayModding;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

[Collection(ContentRootTestGroup.Name)]
public sealed class CrewmateTauntCadenceTests
{
    private const string MoneyBurstEffectName = "CivvieMoneyBurst";

    [Theory]
    [InlineData("quote")]
    [InlineData("plugin.quote-curly.quote")]
    public void CrewmateTauntHealsAndBurstsAtFrameThreeWhileTauntContinues(string gameplayClassId)
    {
        EnsureQuoteCurlyGameplayPackRegistered();
        var world = CreateQuoteWorld(gameplayClassId);
        var player = world.LocalPlayer;
        var startingHealth = player.MaxHealth - 40;
        player.ForceSetHealth(startingHealth);

        StartQuoteTaunt(world);

        for (var tick = 0; player.CivvieTauntHealPending && player.TauntFrameIndex < 3f; tick += 1)
        {
            Assert.Equal(startingHealth, player.Health);
            Assert.Empty(GetMoneyBurstEvents(world));
            Assert.True(tick < 20);
            world.AdvanceOneTick();
        }

        Assert.False(player.CivvieTauntHealPending);
        Assert.InRange(player.TauntFrameIndex, 3f, 9f);
        Assert.True(player.IsTaunting);
        Assert.Equal(startingHealth + 30, player.Health);
        Assert.Single(GetMoneyBurstEvents(world));

        while (player.IsTaunting)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(startingHealth + 30, player.Health);
        Assert.Single(GetMoneyBurstEvents(world));
    }

    [Theory]
    [InlineData("quote")]
    [InlineData("plugin.quote-curly.quote")]
    public void CrewmateAtFullHealthStillBurstsAtFrameThree(string gameplayClassId)
    {
        EnsureQuoteCurlyGameplayPackRegistered();
        var world = CreateQuoteWorld(gameplayClassId);
        var player = world.LocalPlayer;
        var startingHealth = player.Health;

        StartQuoteTaunt(world);
        AdvanceUntilCrewTauntTriggers(world);

        Assert.True(player.IsTaunting);
        Assert.InRange(player.TauntFrameIndex, 3f, 9f);
        Assert.Equal(startingHealth, player.Health);
        Assert.Single(GetMoneyBurstEvents(world));

        while (player.IsTaunting)
        {
            world.AdvanceOneTick();
        }

        Assert.Single(GetMoneyBurstEvents(world));
    }

    [Fact]
    public void PackagedClientAndServerCrewTauntDefinitionsUseFrameThree()
    {
        foreach (var hostFolder in new[] { "Client", "Server" })
        {
            var packDirectory = ProjectSourceLocator.FindDirectory(Path.Combine(
                "Plugins",
                "Packaged",
                hostFolder,
                "Lua.QuoteCurly",
                "Gameplay",
                "quote-curly.gg2"));

            Assert.False(string.IsNullOrWhiteSpace(packDirectory));
            var pack = GameplayModPackDirectoryLoader.LoadFromDirectory(packDirectory!);
            var ability = pack.Items["plugin.quote-curly.ability.taunt"].Ability;

            Assert.NotNull(ability);
            Assert.Equal(3, ability!.Parameters["healFrameIndex"].GetInt32());
            Assert.Equal(30, ability.Parameters["healAmount"].GetInt32());
            Assert.True(ability.Parameters["healSelfOnly"].GetBoolean());
            Assert.True(ability.Parameters["moneyBurst"].GetBoolean());
        }
    }

    [Fact]
    public void CivilianTauntKeepsItsFrameNineHealAndHasNoMoneyBurst()
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        world.NetworkPlayers.PrepareLocalPlayerJoin();
        world.NetworkPlayers.SetLocalPlayerTeam(PlayerTeam.Red);
        world.NetworkPlayers.CompleteLocalPlayerJoin("civilian");
        var player = world.LocalPlayer;
        var startingHealth = player.MaxHealth - 30;
        player.ForceSetHealth(startingHealth);

        world.NetworkPlayers.SetLocalInput(default(PlayerInputSnapshot) with { Taunt = true });
        world.AdvanceOneTick();
        Assert.True(player.CivvieTauntHealPending);

        world.NetworkPlayers.SetLocalInput(default);
        while (player.CivvieTauntHealPending && player.TauntFrameIndex < 9f)
        {
            Assert.Equal(startingHealth, player.Health);
            Assert.Empty(GetMoneyBurstEvents(world));
            world.AdvanceOneTick();
        }

        Assert.False(player.CivvieTauntHealPending);
        Assert.InRange(player.TauntFrameIndex, 9f, 16f);
        Assert.Equal(startingHealth + 15, player.Health);
        Assert.Empty(GetMoneyBurstEvents(world));
    }

    private static SimulationWorld CreateQuoteWorld(string gameplayClassId)
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        world.NetworkPlayers.PrepareLocalPlayerJoin();
        world.NetworkPlayers.SetLocalPlayerTeam(PlayerTeam.Red);
        world.NetworkPlayers.CompleteLocalPlayerJoin(gameplayClassId);
        return world;
    }

    private static void StartQuoteTaunt(SimulationWorld world)
    {
        world.NetworkPlayers.SetLocalInput(default(PlayerInputSnapshot) with { UseAbility = true });
        world.AdvanceOneTick();
        Assert.True(world.LocalPlayer.IsTaunting);
        Assert.True(world.LocalPlayer.CivvieTauntHealPending);

        world.NetworkPlayers.SetLocalInput(default);
        world.AdvanceOneTick();
    }

    private static void AdvanceUntilCrewTauntTriggers(SimulationWorld world)
    {
        var player = world.LocalPlayer;
        for (var tick = 0; player.CivvieTauntHealPending && tick < 20; tick += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.False(player.CivvieTauntHealPending);
    }

    private static IEnumerable<WorldVisualEvent> GetMoneyBurstEvents(SimulationWorld world)
    {
        return world.PendingVisualEvents.Where(static visualEvent =>
            string.Equals(visualEvent.EffectName, MoneyBurstEffectName, StringComparison.Ordinal));
    }

    private static void EnsureQuoteCurlyGameplayPackRegistered()
    {
        if (CharacterClassCatalog.RuntimeRegistry.TryGetClassBinding("plugin.quote-curly.quote", out _))
        {
            return;
        }

        var packDirectory = ProjectSourceLocator.FindDirectory(Path.Combine(
            "Plugins",
            "Packaged",
            "Server",
            "Lua.QuoteCurly",
            "Gameplay",
            "quote-curly.gg2"));

        Assert.False(string.IsNullOrWhiteSpace(packDirectory));
        var pack = GameplayModPackDirectoryLoader.LoadFromDirectory(packDirectory!);
        Assert.True(
            CharacterClassCatalog.RuntimeRegistry.TryRegisterModPack(pack, allowRuntimeClassBindingOverride: false, out var error),
            error);
    }
}
