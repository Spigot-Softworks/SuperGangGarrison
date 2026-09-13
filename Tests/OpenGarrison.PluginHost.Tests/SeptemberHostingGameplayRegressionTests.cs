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
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

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

    [Theory]
    [InlineData(Keys.D3)]
    [InlineData(Keys.D4)]
    [InlineData(Keys.NumPad3)]
    [InlineData(Keys.NumPad4)]
    public void TeamSelectionFrameCannotActivateTheNewClassMenu(Keys key)
    {
        var game = (Game1)RuntimeHelpers.GetUninitializedObject(typeof(Game1));
        var state = typeof(Game1).GetField("_uiShellState", Private)!;
        state.SetValue(game, Activator.CreateInstance(state.FieldType, true));
        typeof(Game1).GetProperty("_classSelectOpen", Private)!.SetValue(game, true);
        var keyboard = new KeyboardState(key);
        typeof(Game1).GetMethod("UpdateClassSelect", Private)!.Invoke(game, [keyboard, default(MouseState), false]);
        Assert.True((bool)typeof(Game1).GetProperty("_classSelectOpen", Private)!.GetValue(game)!);
        typeof(Game1).GetField("_previousKeyboard", Private)!.SetValue(game, keyboard);
        object?[] args = [keyboard, null];
        Assert.False((bool)typeof(Game1).GetMethod("TryGetClassSelectHotkeySelection", Private)!.Invoke(game, args)!);
        typeof(Game1).GetField("_previousKeyboard", Private)!.SetValue(game, new KeyboardState());
        Assert.True((bool)typeof(Game1).GetMethod("TryGetClassSelectHotkeySelection", Private)!.Invoke(game, args)!);
    }

    [Fact]
    public void NewControllerCannotAttachStopOrDeleteAnotherInstancesRecord()
    {
        var directory = Path.Combine(Path.GetTempPath(), "sgg-owner-test-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "session.json");
        using var child = StartWaitChild();
        Assert.True(HostedServerProcessIdentity.TryCapture(child, out var childIdentity));
        Assert.True(HostedServerProcessIdentity.TryCaptureCurrent(out var owner));
        try
        {
            using var first = new HostedServerRuntimeController(new HostedServerConsoleState(), path);
            var session = new HostedServerSessionInfo
            {
                ProcessId = child.Id, ProcessStartTimeUtcTicks = childIdentity.StartTimeUtcTicks,
                InstanceId = first.InstanceId, OwnerProcessId = owner.ProcessId, OwnerStartTimeUtcTicks = owner.StartTimeUtcTicks,
                LaunchMode = "launcher", IsReady = true, Port = 12345,
            };
            session.Save(path);
            using var second = new HostedServerRuntimeController(new HostedServerConsoleState(), path);
            Assert.False(second.TryResumeSession(false));
            Assert.Null(second.ReadyPort);
            second.Stop();
            Assert.False(child.HasExited);
            Assert.Equal(first.InstanceId, HostedServerSessionInfo.Load(path)!.InstanceId);
            Assert.Equal(12345, first.ReadyPort);
            var stale = new HostedServerSessionInfo { ProcessId = child.Id, InstanceId = Guid.NewGuid().ToString("N") };
            HostedServerSessionInfo.DeleteIfMatching(stale, path);
            Assert.True(File.Exists(path));
            session.IsReady = false;
            session.Save(path);
            Assert.Null(first.ReadyPort);
        }
        finally
        {
            if (!child.HasExited) { child.Kill(true); child.WaitForExit(1000); }
            if (File.Exists(path)) File.Delete(path);
            if (Directory.Exists(directory)) Directory.Delete(directory);
        }
    }

    [Fact]
    public void ConcurrentStartsReserveDistinctPortsAndSkipOccupiedTcp()
    {
        using var occupied = new UdpClient(0);
        var requested = ((IPEndPoint)occupied.Client.LocalEndPoint!).Port;
        if (requested >= 65530) return;
        var reservations = Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Task.Run(() => ServerListenerReservation.Acquire(requested, requested)))).GetAwaiter().GetResult();
        try
        {
            Assert.Equal(3, reservations.Select(r => r.Port).Distinct().Count());
            Assert.All(reservations, r => { Assert.True(r.Port > requested); Assert.Equal(r.Port, r.HttpPort); });
        }
        finally { foreach (var reservation in reservations) reservation.Dispose(); }
        var tcp = new TcpListener(IPAddress.Any, 0);
        tcp.Start();
        try
        {
            var port = ((IPEndPoint)tcp.LocalEndpoint).Port;
            if (port >= 65535) return;
            using var selected = ServerListenerReservation.Acquire(port, port);
            Assert.True(selected.Port > port);
        }
        finally { tcp.Stop(); }
    }

    [Fact]
    public void TerminalLaunchPassesEnvironmentAndQuotedArgumentsWithoutShellExecute()
    {
        if (!OperatingSystem.IsWindows()) return;
        var resultPath = Path.Combine(Path.GetTempPath(), "sgg terminal " + Guid.NewGuid().ToString("N") + ".txt");
        using var runtime = new HostedServerRuntimeController(new HostedServerConsoleState());
        var info = new ProcessStartInfo("powershell.exe") { WorkingDirectory = Path.GetTempPath() };
        info.ArgumentList.Add("-NoProfile");
        info.ArgumentList.Add("-NonInteractive");
        info.ArgumentList.Add("-Command");
        info.ArgumentList.Add("[IO.File]::WriteAllText($env:SGG_TEST_RESULT, $env:SGG_TEST_VALUE)");
        info.Environment["SGG_TEST_RESULT"] = resultPath;
        const string value = "spaces & quotes \" and trailing slash\\";
        info.Environment["SGG_TEST_VALUE"] = value;
        runtime.ConfigureProcessEnvironment(info, terminal: true);
        Assert.False(info.UseShellExecute);
        Assert.False(info.Environment.ContainsKey(HostedServerProcessIdentity.ParentProcessIdEnvironmentVariable));
        Assert.Equal("direct", info.Environment["OPENGARRISON_LAUNCH_MODE"]);
        try
        {
            var pid = DedicatedServerTerminalLauncher.Start(info, visible: false);
            using var process = Process.GetProcessById(pid);
            Assert.True(process.WaitForExit(10000));
            Assert.Equal(value, File.ReadAllText(resultPath));
        }
        finally { if (File.Exists(resultPath)) File.Delete(resultPath); }
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
            typeof(SimulationWorld).GetProperty(nameof(SimulationWorld.MatchState))!.GetSetMethod(true)!
                .Invoke(world, [world.MatchState with { Phase = MatchPhase.Ended, WinnerTeam = null }]);
            typeof(SimulationWorld).GetField("_mapChangeReady", Private)!.SetValue(world, true);
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
