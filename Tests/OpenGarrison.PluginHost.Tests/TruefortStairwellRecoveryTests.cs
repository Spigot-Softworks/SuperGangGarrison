using OpenGarrison.Core;
using OpenGarrison.Core.BotBrain;
using Xunit;
using Xunit.Abstractions;

namespace OpenGarrison.PluginHost.Tests;

[Collection(ContentRootTestGroup.Name)]
public sealed class TruefortStairwellRecoveryTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(PlayerClass.Heavy, 4180f)]
    [InlineData(PlayerClass.Heavy, 4250f)]
    [InlineData(PlayerClass.Soldier, 4180f)]
    [InlineData(PlayerClass.Soldier, 4250f)]
    [InlineData(PlayerClass.Scout, 4180f)]
    [InlineData(PlayerClass.Scout, 4250f)]
    [InlineData(PlayerClass.Engineer, 4180f)]
    [InlineData(PlayerClass.Engineer, 4250f)]
    [InlineData(PlayerClass.Pyro, 4180f)]
    [InlineData(PlayerClass.Medic, 4250f)]
    [InlineData(PlayerClass.Sniper, 4250f)]
    [InlineData(PlayerClass.Heavy, 4180f, 3890f)]
    [InlineData(PlayerClass.Soldier, 4180f, 3890f)]
    [InlineData(PlayerClass.Scout, 4180f, 3890f)]
    [InlineData(PlayerClass.Pyro, 4180f, 3890f)]
    public void BotLeavesBlueStairwellToReachEnemyBelow(PlayerClass playerClass, float startX, float enemyX = 3800f)
    {
        var oldRoot = ContentRoot.Path;
        ContentRoot.Initialize(ProjectSourceLocator.FindDirectory(Path.Combine("Core", "Content"))!);
        try
        {
            var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
            Assert.True(world.TryLoadLevel("Truefort"));
            world.PrepareLocalPlayerJoin();
            world.SetLocalPlayerTeam(PlayerTeam.Blue);
            world.CompleteLocalPlayerJoin(playerClass);
            var bot = world.LocalPlayer;
            world.TryPrepareNetworkPlayerJoin(2);
            world.TrySetNetworkPlayerTeam(2, PlayerTeam.Red);
            world.TryApplyNetworkPlayerClassSelection(2, PlayerClass.Engineer);
            Assert.True(world.TryGetNetworkPlayer(2, out var enemy));
            var startY = FindFloor(world, bot, startX, 470, 640);
            var enemyY = FindFloor(world, enemy, enemyX, 600, 800);
            bot.TeleportTo(startX, startY);
            bot.RestoreMovementProbeState(true, null, -1);
            enemy.TeleportTo(enemyX, enemyY);
            var brain = new BotBrainController();
            var reached = false;
            PlayerInputSnapshot input = default;
            for (var tick = 0; tick < 900; tick++)
            {
                bot.ForceSetHealth(bot.MaxHealth);
                enemy.ForceSetHealth(enemy.MaxHealth);
                if (tick % 4 == 0) input = brain.Think(bot, world, bot.Team);
                else if (brain.RequiresPerTickNavigationThink && brain.TryAdvanceCachedNavigation(bot, world, bot.Team, input, out var updated)) input = updated;
                world.SetLocalInput(input);
                world.AdvanceOneTick();
                if (tick % 60 == 0) output.WriteLine($"{tick}: ({bot.X:0},{bot.Y:0}) target=({enemy.X:0},{enemy.Y:0}) graph={brain.LastNavigationGraphSource} {brain.LastDirectDriveTrace} path={brain.CurrentPathIndex}/{brain.CurrentPathCount} {brain.LastSemanticRecoveryTrace}");
                // CTF Constructors legitimately return to defend their intel.
                if ((bot.X < 3990 && MathF.Abs(bot.Y - enemy.Y) < 60)
                    || (playerClass == PlayerClass.Engineer && bot.X > 4700)) { reached = true; break; }
            }
            Assert.True(reached, $"Stuck from {startX},{startY} at {bot.X},{bot.Y}: {brain.LastDirectDriveTrace}");
        }
        finally { ContentRoot.Initialize(oldRoot); }
    }

    [Fact]
    public void MixedLTDWaveDoesNotRemainInBlueStairwellBehindFloatingSentries()
    {
        var oldRoot = ContentRoot.Path;
        ContentRoot.Initialize(ProjectSourceLocator.FindDirectory(Path.Combine("Core", "Content"))!);
        try
        {
            var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
            Assert.True(world.TryLoadLevel("Truefort"));
            world.PrepareLocalPlayerJoin();
            world.SetLocalPlayerTeam(PlayerTeam.Red);
            world.CompleteLocalPlayerJoin(PlayerClass.Engineer);
            world.LocalPlayer.TeleportTo(3800, FindFloor(world, world.LocalPlayer, 3800, 600, 800));
            world.ConfigureExperimentalGameplaySettings(new(EnableEngineerAutonomousPhaseEngine: true));
            foreach (var x in new[] { 3740f, 3880f })
            {
                var sentry = new SentryEntity((int)x + 10000, world.LocalPlayer.Id, PlayerTeam.Red, x, 606, 1, maxHealth: 100000);
                sentry.ForceBuilt();
                world.CombatTestAddSentry(sentry);
            }
            var classes = new[] { PlayerClass.Scout, PlayerClass.Soldier, PlayerClass.Pyro, PlayerClass.Demoman,
                PlayerClass.Heavy, PlayerClass.Engineer, PlayerClass.Sniper, PlayerClass.Spy, PlayerClass.Medic };
            var bots = new List<(byte Slot, PlayerEntity Player, BotBrainController Brain)>();
            for (var i = 0; i < classes.Length; i++)
            {
                var slot = (byte)(i + 2);
                world.TryPrepareNetworkPlayerJoin(slot);
                world.TrySetNetworkPlayerTeam(slot, PlayerTeam.Blue);
                world.TryApplyNetworkPlayerClassSelection(slot, classes[i]);
                Assert.True(world.TryGetNetworkPlayer(slot, out var bot));
                var x = 4180f + i * 10;
                bot.TeleportTo(x, FindFloor(world, bot, x, 470, 640));
                bot.RestoreMovementProbeState(true, null, -1);
                bots.Add((slot, bot, new BotBrainController()));
            }
            var inputs = new Dictionary<byte, PlayerInputSnapshot>();
            var teams = bots.ToDictionary(bot => bot.Slot, _ => PlayerTeam.Blue);
            var reached = new HashSet<byte>();
            for (var tick = 0; tick < 900; tick++)
            {
                world.LocalPlayer.ForceSetHealth(10000);
                foreach (var (slot, bot, brain) in bots)
                {
                    bot.ForceSetHealth(10000);
                    var input = inputs.GetValueOrDefault(slot);
                    if (tick % 4 == 0) input = brain.Think(bot, world, bot.Team, teams);
                    else if (brain.RequiresPerTickNavigationThink && brain.TryAdvanceCachedNavigation(bot, world, bot.Team, input, out var updated)) input = updated;
                    inputs[slot] = input;
                    world.TrySetNetworkPlayerInput(slot, input);
                    if (bot.X < 3990 && MathF.Abs(bot.Y - world.LocalPlayer.Y) < 70) reached.Add(slot);
                    if (tick % 120 == 0) output.WriteLine($"{tick}: {bot.ClassId} ({bot.X:0},{bot.Y:0}) {brain.LastDirectDriveTrace} path={brain.CurrentPathIndex}/{brain.CurrentPathCount} {brain.LastSemanticRecoveryTrace}");
                }
                world.AdvanceOneTick();
            }
            foreach (var (slot, bot, brain) in bots.Where(bot => bot.Player.ClassId is PlayerClass.Heavy or PlayerClass.Soldier or PlayerClass.Pyro or PlayerClass.Scout))
                Assert.True(reached.Contains(slot), $"{bot.ClassId} never escaped; at ({bot.X},{bot.Y}) {brain.LastDirectDriveTrace}");
        }
        finally { ContentRoot.Initialize(oldRoot); }
    }

    private static float FindFloor(SimulationWorld world, PlayerEntity player, float x, int minY, int maxY)
    {
        for (var y = minY; y < maxY; y++)
            if (player.CanOccupy(world.Level, player.Team, x, y) && !player.CanOccupy(world.Level, player.Team, x, y + 1)) return y;
        throw new InvalidOperationException($"No floor at {x}, {minY}..{maxY}");
    }
}
