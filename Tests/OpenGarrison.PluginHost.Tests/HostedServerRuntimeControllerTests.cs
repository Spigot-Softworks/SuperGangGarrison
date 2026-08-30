using OpenGarrison.Client;
using OpenGarrison.Core;
using OpenGarrison.Server;
using System.Diagnostics;
using System.IO.Pipes;
using System.Threading.Tasks;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class HostedServerRuntimeControllerTests
{
    private static readonly object HostedServerEnvironmentGate = new();

    [Fact]
    public void CommandSendAttachesToSessionCreatedAfterBackgroundLaunch()
    {
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"opengarrison-hosted-command-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        var sessionPath = Path.Combine(temporaryDirectory, HostedServerSessionInfo.DefaultFileName);
        var pipeName = $"opengarrison-hosted-command-test-{Guid.NewGuid():N}";
        using var shutdown = new CancellationTokenSource();
        using var pipe = new HostedServerAdminPipeHost(
            pipeName,
            (command, _, _) => Task.FromResult<IReadOnlyList<string>>(
                [$"[server] received {command}"]),
            () => { },
            shutdown.Token);

        try
        {
            Assert.True(
                SpinWait.SpinUntil(
                    () => HostedServerAdminClient.TrySendCommand(
                        pipeName,
                        "__ping",
                        out _,
                        out _),
                    TimeSpan.FromSeconds(3)),
                "The test admin pipe did not become ready.");
            new HostedServerSessionInfo
            {
                ProcessId = Environment.ProcessId,
                PipeName = pipeName,
                ServerName = "Last To Die Solo",
            }.Save(sessionPath);
            using var runtime = new HostedServerRuntimeController(
                new HostedServerConsoleState(),
                sessionPath);

            Assert.True(runtime.TrySendCommand("ltd_win", out var responseLines, out var error), error);
            Assert.Equal(["[server] received ltd_win"], responseLines);
        }
        finally
        {
            shutdown.Cancel();
            if (File.Exists(sessionPath))
            {
                File.Delete(sessionPath);
            }

            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory);
            }
        }
    }

    [Fact]
    public void PersistedSessionCanBeResumedWithoutAnInMemoryProcessHandle()
    {
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"opengarrison-hosted-resume-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        var sessionPath = Path.Combine(temporaryDirectory, HostedServerSessionInfo.DefaultFileName);
        var pipeName = $"opengarrison-hosted-resume-test-{Guid.NewGuid():N}";
        using var shutdown = new CancellationTokenSource();
        using var pipe = new HostedServerAdminPipeHost(
            pipeName,
            (command, _, _) => Task.FromResult<IReadOnlyList<string>>(
                command == "status"
                    ? ["[server] status ok"]
                    : [$"[server] received {command}"]),
            () => { },
            shutdown.Token);

        try
        {
            Assert.True(
                SpinWait.SpinUntil(
                    () => HostedServerAdminClient.TrySendCommand(
                        pipeName,
                        "__ping",
                        out _,
                        out _),
                    TimeSpan.FromSeconds(3)),
                "The test admin pipe did not become ready.");

            new HostedServerSessionInfo
            {
                ProcessId = Environment.ProcessId,
                PipeName = pipeName,
                ServerName = "Last To Die Solo",
                Port = 8190,
            }.Save(sessionPath);

            // Constructing the controller does not attach a Process object. The
            // persisted session and admin pipe are the only recovery handles.
            using var runtime = new HostedServerRuntimeController(
                new HostedServerConsoleState(),
                sessionPath);

            Assert.True(runtime.TryResumeSession(loadExistingLog: false), "The persisted hosted session was not resumed.");
            Assert.Null(runtime.TrackedProcessId);
            Assert.True(runtime.IsRunning);
            Assert.True(runtime.TrySendCommand("status", out var responseLines, out var error), error);
            Assert.Equal(["[server] status ok"], responseLines);
        }
        finally
        {
            shutdown.Cancel();
            HostedServerSessionInfo.Delete(sessionPath);
            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory);
            }
        }
    }

    [Fact]
    public void AdminShutdownCommandSignalsHostedServerCancellation()
    {
        var pipeName = $"opengarrison-hosted-shutdown-test-{Guid.NewGuid():N}";
        using var shutdown = new CancellationTokenSource();
        using var pipe = new HostedServerAdminPipeHost(
            pipeName,
            (_, _, _) => Task.FromResult<IReadOnlyList<string>>([]),
            shutdown.Cancel,
            shutdown.Token);

        Assert.True(
            SpinWait.SpinUntil(
                () => HostedServerAdminClient.TrySendCommand(
                    pipeName,
                    "__ping",
                    out _,
                    out _),
                TimeSpan.FromSeconds(3)),
            "The test admin pipe did not become ready.");

        Assert.True(
            HostedServerAdminClient.TrySendCommand(
                pipeName,
                "shutdown",
                out var responseLines,
                out var error),
            error);
        Assert.Equal(["[server] shutdown requested."], responseLines);
        Assert.True(shutdown.IsCancellationRequested);
    }

    [Fact]
    public void HostedServerProcessIdentityRoundTripsThroughLaunchEnvironment()
    {
        Assert.True(
            HostedServerProcessIdentity.TryCaptureCurrent(out var expected),
            "The current test process did not expose a usable start-time identity.");

        var startInfo = new ProcessStartInfo();
        expected.ApplyParentEnvironment(startInfo);

        lock (HostedServerEnvironmentGate)
        {
            var previousPid = Environment.GetEnvironmentVariable(
                HostedServerProcessIdentity.ParentProcessIdEnvironmentVariable);
            var previousStartTime = Environment.GetEnvironmentVariable(
                HostedServerProcessIdentity.ParentProcessStartTimeEnvironmentVariable);
            try
            {
                Environment.SetEnvironmentVariable(
                    HostedServerProcessIdentity.ParentProcessIdEnvironmentVariable,
                    startInfo.Environment[HostedServerProcessIdentity.ParentProcessIdEnvironmentVariable]);
                Environment.SetEnvironmentVariable(
                    HostedServerProcessIdentity.ParentProcessStartTimeEnvironmentVariable,
                    startInfo.Environment[HostedServerProcessIdentity.ParentProcessStartTimeEnvironmentVariable]);

                Assert.True(
                    HostedServerProcessIdentity.TryReadParentFromEnvironment(out var actual));
                Assert.Equal(expected, actual);

                var mismatched = expected with { StartTimeUtcTicks = expected.StartTimeUtcTicks + 1 };
                Assert.False(mismatched.Matches(Process.GetCurrentProcess()));
            }
            finally
            {
                Environment.SetEnvironmentVariable(
                    HostedServerProcessIdentity.ParentProcessIdEnvironmentVariable,
                    previousPid);
                Environment.SetEnvironmentVariable(
                    HostedServerProcessIdentity.ParentProcessStartTimeEnvironmentVariable,
                    previousStartTime);
            }
        }
    }

    [Fact]
    public void HostedParentLifetimeMonitorCancelsWhenIdentifiedParentExits()
    {
        using var child = StartShortLivedChildProcess();
        Assert.True(
            HostedServerProcessIdentity.TryCapture(child, out var childIdentity),
            "The short-lived child did not expose a usable start-time identity.");

        using var serverShutdown = new CancellationTokenSource();
        lock (HostedServerEnvironmentGate)
        {
            var previousPid = Environment.GetEnvironmentVariable(
                HostedServerProcessIdentity.ParentProcessIdEnvironmentVariable);
            var previousStartTime = Environment.GetEnvironmentVariable(
                HostedServerProcessIdentity.ParentProcessStartTimeEnvironmentVariable);
            var previousLaunchMode = Environment.GetEnvironmentVariable("OPENGARRISON_LAUNCH_MODE");
            try
            {
                var environment = new ProcessStartInfo();
                childIdentity.ApplyParentEnvironment(environment);
                Environment.SetEnvironmentVariable(
                    HostedServerProcessIdentity.ParentProcessIdEnvironmentVariable,
                    environment.Environment[HostedServerProcessIdentity.ParentProcessIdEnvironmentVariable]);
                Environment.SetEnvironmentVariable(
                    HostedServerProcessIdentity.ParentProcessStartTimeEnvironmentVariable,
                    environment.Environment[HostedServerProcessIdentity.ParentProcessStartTimeEnvironmentVariable]);
                Environment.SetEnvironmentVariable("OPENGARRISON_LAUNCH_MODE", "launcher");

                using var monitor = HostedServerParentLifetimeMonitor.TryStart(
                    serverShutdown,
                    _ => { },
                    serverShutdown.Token);
                Assert.NotNull(monitor);

                Assert.True(
                    serverShutdown.Token.WaitHandle.WaitOne(TimeSpan.FromSeconds(10)),
                    "The hosted parent lifetime monitor did not cancel after the parent exited.");
                child.WaitForExit(1000);
            }
            finally
            {
                Environment.SetEnvironmentVariable(
                    HostedServerProcessIdentity.ParentProcessIdEnvironmentVariable,
                    previousPid);
                Environment.SetEnvironmentVariable(
                    HostedServerProcessIdentity.ParentProcessStartTimeEnvironmentVariable,
                    previousStartTime);
                Environment.SetEnvironmentVariable("OPENGARRISON_LAUNCH_MODE", previousLaunchMode);

                if (!child.HasExited)
                {
                    child.Kill(entireProcessTree: true);
                    child.WaitForExit(1000);
                }
            }
        }
    }

    [Fact]
    public async Task AdminClientTimesOutWhenServerNeverCompletesResponse()
    {
        var pipeName = $"opengarrison-hosted-timeout-test-{Guid.NewGuid():N}";
        using var server = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);
        using var serverCancellation = new CancellationTokenSource();
        var serverTask = Task.Run(async () =>
        {
            try
            {
                await server.WaitForConnectionAsync(serverCancellation.Token);
                using var reader = new StreamReader(server);
                _ = await reader.ReadLineAsync(serverCancellation.Token);
                await Task.Delay(TimeSpan.FromSeconds(5), serverCancellation.Token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        });

        try
        {
            var started = Stopwatch.StartNew();
            Assert.False(
                HostedServerAdminClient.TrySendCommand(
                    pipeName,
                    "shutdown",
                    out _,
                    out var error,
                    timeoutMilliseconds: 250));
            started.Stop();

            Assert.Contains("timed out", error, StringComparison.OrdinalIgnoreCase);
            Assert.InRange(started.Elapsed, TimeSpan.Zero, TimeSpan.FromSeconds(2));
        }
        finally
        {
            serverCancellation.Cancel();
            server.Dispose();
            try
            {
                await serverTask.WaitAsync(TimeSpan.FromSeconds(1));
            }
            catch (TimeoutException)
            {
                // The timeout test must never wait indefinitely during cleanup.
            }
        }
    }

    private static Process StartShortLivedChildProcess()
    {
        ProcessStartInfo startInfo;
        if (OperatingSystem.IsWindows())
        {
            var commandShell = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
            startInfo = new ProcessStartInfo(
                commandShell,
                "/c ping 127.0.0.1 -n 3 > nul")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
            };
        }
        else
        {
            startInfo = new ProcessStartInfo(
                "/bin/sh",
                "-c \"sleep 2\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
            };
        }

        return Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start the short-lived child process.");
    }
}
