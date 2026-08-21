using OpenGarrison.Client;
using OpenGarrison.Core;
using OpenGarrison.Server;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class HostedServerRuntimeControllerTests
{
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
}
