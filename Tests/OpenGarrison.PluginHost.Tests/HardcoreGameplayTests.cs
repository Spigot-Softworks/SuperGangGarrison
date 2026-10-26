using OpenGarrison.Core;
using OpenGarrison.Client;
using OpenGarrison.ClientShared;
using OpenGarrison.SessionRuntime;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

[Collection(ContentRootTestGroup.Name)]
public sealed class HardcoreGameplayTests
{
    public static IEnumerable<object[]> Classes => Enum.GetValues<PlayerClass>().Select(c => new object[] { c });

    [Theory]
    [MemberData(nameof(Classes))]
    public void HardcoreCapsEveryClassAcrossHealingRespawnAndPrediction(PlayerClass playerClass)
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        world.ConfigureExperimentalGameplaySettings(new ExperimentalGameplaySettings(EnableHardcore: true));
        world.NetworkPlayers.PrepareLocalPlayerJoin();
        world.NetworkPlayers.SetLocalPlayerTeam(PlayerTeam.Red);
        world.NetworkPlayers.CompleteLocalPlayerJoin(playerClass);
        var player = world.LocalPlayer;
        Assert.Equal(75, player.MaxHealth);
        Assert.Equal(75, player.Health);
        player.ApplyContinuousHealingAndGetAmount(500);
        player.SetExperimentalMaxHealthBonus(500);
        Assert.Equal(75, player.MaxHealth);
        Assert.Equal(75, player.Health);
        var probe = new PlayerEntity(-940_001, player.ClassDefinition, "probe");
        var state = player.CapturePredictionState();
        probe.RestorePredictionState(in state);
        Assert.Equal(75, probe.MaxHealth);
        world.NetworkPlayers.ForceKillLocalPlayer();
        world.NetworkPlayers.ForceRespawnLocalPlayer();
        Assert.Equal(75, player.Health);
        Assert.Equal(75, player.MaxHealth);
    }

    [Fact]
    public void LiveToggleCapsRemotePlayersAndPreservesTheirHealthOverrides()
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        Assert.True(world.NetworkPlayers.TryPrepareNetworkPlayerJoin(2));
        Assert.True(world.NetworkPlayers.TrySetNetworkPlayerTeam(2, PlayerTeam.Blue));
        Assert.True(world.NetworkPlayers.TryCompleteNetworkPlayerJoinState(2, PlayerClass.Heavy));
        Assert.True(world.NetworkPlayers.TryGetNetworkPlayer(2, out var player));
        Assert.True(world.ServerTuning.TrySetNetworkPlayerMaxHealthOverride(2, 300));
        world.ConfigureExperimentalGameplaySettings(new ExperimentalGameplaySettings(EnableHardcore: true));
        Assert.Equal(75, player.MaxHealth);
        Assert.Equal(75, player.Health);
        Assert.True(world.ServerTuning.TrySetNetworkPlayerMaxHealthOverride(2, 50));
        Assert.Equal(50, player.MaxHealth);
        Assert.True(world.ServerTuning.TrySetNetworkPlayerMaxHealthOverride(2, 300, refillHealth: false));
        Assert.Equal(75, player.MaxHealth);
        world.ConfigureExperimentalGameplaySettings(new ExperimentalGameplaySettings());
        Assert.Equal(300, player.MaxHealth);
        Assert.Equal(50, player.Health);
    }

    [Fact]
    public void PracticeKeepsConquestPointLimitSeparateFromClassicCapLimit()
    {
        var state = new Game1.PracticeSetupState
        {
            MapEntries = [new("cq_test", "Conquest", GameModeKind.Conquest, true),
                          new("cp_test", "CP", GameModeKind.ControlPoint, true)],
            MapIndex = 0,
        };
        state.Normalize();
        state.CycleCapLimit(1);
        Assert.Equal(250, state.ConquestScoreLimit);
        Assert.Equal(5, state.CapLimit);
        state.MapIndex = 1;
        state.CycleCapLimit(1);
        Assert.Equal(6, state.CapLimit);
        Assert.Equal(250, state.ConquestScoreLimit);
    }

    [Fact]
    public void HostSettingsPreserveBothOptions()
    {
        var form = new Game1.HostSetupFormState();
        form.LoadFrom(new OpenGarrisonHostSettings { ConquestScoreLimit = 1000, HardcoreEnabled = true });
        var settings = new ClientSettings();
        form.ApplyTo(settings);
        Assert.Equal(1000, settings.HostDefaults.ConquestScoreLimit);
        Assert.True(settings.HostDefaults.HardcoreEnabled);
    }

    [Fact]
    public async Task ServerCvarsApplyBothSettingsAndRejectOutOfRangeLimits()
    {
        using var host = new EmbeddedSessionHost(new()
        {
            LastToDie = false, Map = "Harvest", Hardcore = true, ConquestScoreLimit = 900,
        });
        Assert.True(host.World.ExperimentalGameplaySettings.EnableHardcore);
        Assert.Equal(900, host.World.ConfiguredConquestScoreLimit);
        async Task Set(string command)
        {
            var pending = host.ExecuteHostCommandAsync(command);
            for (var tick = 0; tick < 60 && !pending.IsCompleted; tick++) host.Advance(1d / 30);
            await pending.WaitAsync(TimeSpan.FromSeconds(5));
        }
        await Set("cvar sv_conquest_scorelimit 1000");
        Assert.Equal(1000, host.World.ConfiguredConquestScoreLimit);
        await Set("cvar sv_conquest_scorelimit 0");
        Assert.Equal(1000, host.World.ConfiguredConquestScoreLimit);
        await Set("cvar sv_experimental_hardcore off");
        Assert.False(host.World.ExperimentalGameplaySettings.EnableHardcore);
        await Set("cvar sv_experimental_hardcore on");
        Assert.True(host.World.ExperimentalGameplaySettings.EnableHardcore);
    }

    [Theory]
    [InlineData(false, 800, 600)]
    [InlineData(true, 800, 600)]
    [InlineData(false, 1920, 1080)]
    [InlineData(true, 1920, 1080)]
    public void ConquestCountersFlankTopTimerWithoutMovingCapturePoints(bool classicHud, int width, int height)
    {
        var profile = new HudLayoutProfile { UseGg2Defaults = classicHud };
        Assert.True(profile.TryResolve(HudElementId.MatchObjectiveStatus, width, height, out var pointsBefore));
        profile.SetRuntimeDefault(HudLayoutDefaults.CreateConquestScoreCounter(
            profile.Defaults[HudElementId.MatchKothRedTimer], false), overrideGg2Default: true);
        profile.SetRuntimeDefault(HudLayoutDefaults.CreateConquestScoreCounter(
            profile.Defaults[HudElementId.MatchKothBlueTimer], true), overrideGg2Default: true);
        Assert.True(profile.TryResolve(HudElementId.MatchKothRedTimer, width, height, out var red));
        Assert.True(profile.TryResolve(HudElementId.MatchKothBlueTimer, width, height, out var blue));
        Assert.Equal(width / 2f - 162, red.Origin.X);
        Assert.Equal(width / 2f + 162, blue.Origin.X);
        Assert.Equal(30, red.Origin.Y);
        Assert.Equal(30, blue.Origin.Y);
        Assert.True(profile.TryResolve(HudElementId.MatchObjectiveStatus, width, height, out var pointsAfter));
        Assert.Equal(pointsBefore.Origin, pointsAfter.Origin);
        profile.ClearRuntimeDefaults();
        Assert.True(profile.TryResolve(HudElementId.MatchKothRedTimer, width, height, out var restored));
        Assert.NotEqual(red.Origin, restored.Origin);
    }
}
