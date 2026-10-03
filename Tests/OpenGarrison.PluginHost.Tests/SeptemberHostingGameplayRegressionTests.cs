using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Client;
using OpenGarrison.Core;
using OpenGarrison.Server;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class SeptemberHostingGameplayRegressionTests
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;

    [Theory]
    [InlineData("redintelgate")]
    [InlineData("blueintelgate")]
    [InlineData("redintelgate2")]
    [InlineData("blueintelgate2")]
    [InlineData("intelgatevertical")]
    [InlineData("intelgatehorizontal")]
    public void IntelGateCollisionMatchesBuilderBeforeAndAfterExport(string kind)
    {
        var normalized = CustomMapBuilderEntityNormalization.NormalizeEntityForEditor(CustomMapBuilderEntity.Create(kind, 0, 0));
        var exported = CustomMapBuilderEntityNormalization.ResolveEntityForExport(normalized);
        Assert.True(CustomMapRoomObjectFactory.TryCreate(exported.Type, 0, 0, 1, 1, exported.Properties, out var marker));
        var level = new SimpleLevel("intel-filter-test", GameModeKind.CaptureTheFlag, new WorldBounds(128,128),
            1, null, 0, 1, new SpawnPoint(0,0), [], [], [], [marker], 0, [], importedFromSource:false);
        var barrier = BarrierConfiguration.FromProperties(normalized.Properties);
        foreach (var team in new[] { PlayerTeam.Red, PlayerTeam.Blue })
        foreach (var carrying in new[] { false, true, false })
        {
            var expected = carrying && (!marker.Team.HasValue || marker.Team.Value != team);
            Assert.Equal(expected, level.GetBlockingTeamGates(team, carrying).Contains(marker));
            Assert.Equal(expected, BarrierCollision.BlocksPlayerWithoutDirection(barrier, team, carrying));
        }
    }

    [Fact]
    public void RotationWrapsAndRecordsEachBoundary()
    {
        var lines = new List<string>();
        var world = new SimulationWorld();
        var maps = new[] { "Truefort", "Conflict", "ClassicWell" };
        var manager = new MapRotationManager(world, maps[0], null, maps, lines.Add);
        for (var index = 0; index < 6; index++)
        {
            world.TestSetMatchState(world.MatchState with { Phase = MatchPhase.Ended, WinnerTeam = null });
            world.Lifecycle.MapChangeReady = true;
            Assert.True(manager.TryApplyPendingMapChange(out _));
            Assert.Equal(maps[(index + 1) % maps.Length], world.Level.Name);
        }
        Assert.Equal(6, lines.Count(line => line.Contains("phase=begin")));
        Assert.Equal(6, lines.Count(line => line.Contains("phase=world-loaded applied=True")));
        Assert.Contains(lines, line => line.Contains("rotationIndex=0") && line.Contains("rotationCount=3"));
    }

    private static Process StartWaitChild()
    {
        var info = OperatingSystem.IsWindows()
            ? new ProcessStartInfo("powershell.exe") { ArgumentList = { "-NoProfile", "-NonInteractive", "-Command", "[System.Threading.Thread]::Sleep(30000)" } }
            : new ProcessStartInfo("sleep") { ArgumentList = { "30" } };
        info.UseShellExecute = false;
        info.CreateNoWindow = true;
        info.WindowStyle = ProcessWindowStyle.Hidden;
        return Process.Start(info)!;
    }
}
