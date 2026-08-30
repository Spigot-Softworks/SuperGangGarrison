using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using OpenGarrison.Core;

namespace OpenGarrison.Server;

/// <summary>
/// Owns the lifetime of a background hosted server by watching the exact
/// client process that launched it. Dedicated/terminal servers do not create
/// this monitor because they do not receive the parent identity environment.
/// </summary>
internal sealed class HostedServerParentLifetimeMonitor : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan ShutdownGracePeriod = TimeSpan.FromSeconds(5);

    private readonly HostedServerProcessIdentity _parentIdentity;
    private readonly CancellationTokenSource _monitorCts;
    private readonly Task _monitorTask;
    private readonly TaskCompletionSource<object?> _serverRunCompleted = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly bool _forceProcessExitOnTimeout;

    private HostedServerParentLifetimeMonitor(
        HostedServerProcessIdentity parentIdentity,
        CancellationTokenSource shutdownCts,
        Action<string> log,
        bool forceProcessExitOnTimeout,
        CancellationToken serverShutdownToken)
    {
        _parentIdentity = parentIdentity;
        _forceProcessExitOnTimeout = forceProcessExitOnTimeout;
        _monitorCts = CancellationTokenSource.CreateLinkedTokenSource(serverShutdownToken);
        _monitorTask = Task.Factory.StartNew(
            () => MonitorLoop(shutdownCts, log, _monitorCts.Token),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }

    public static HostedServerParentLifetimeMonitor? TryStart(
        CancellationTokenSource shutdownCts,
        Action<string> log,
        CancellationToken serverShutdownToken,
        bool forceProcessExitOnTimeout = false)
    {
        ArgumentNullException.ThrowIfNull(shutdownCts);
        ArgumentNullException.ThrowIfNull(log);

        if (!string.Equals(
                Environment.GetEnvironmentVariable("OPENGARRISON_LAUNCH_MODE"),
                "launcher",
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return HostedServerProcessIdentity.TryReadParentFromEnvironment(out var parentIdentity)
            ? new HostedServerParentLifetimeMonitor(
                parentIdentity,
                shutdownCts,
                log,
                forceProcessExitOnTimeout,
                serverShutdownToken)
            : null;
    }

    public void MarkServerRunCompleted()
    {
        _serverRunCompleted.TrySetResult(null);
    }

    public void Dispose()
    {
        _monitorCts.Cancel();
        try
        {
            _monitorTask.Wait(1000);
        }
        catch
        {
        }
        finally
        {
            _monitorCts.Dispose();
        }
    }

    private void MonitorLoop(
        CancellationTokenSource shutdownCts,
        Action<string> log,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (!IsParentAlive())
            {
                log($"[server] hosted parent process {_parentIdentity.ProcessId} exited; shutting down.");
                shutdownCts.Cancel();
                if (_forceProcessExitOnTimeout
                    && !_serverRunCompleted.Task.Wait(ShutdownGracePeriod, CancellationToken.None))
                {
                    log("[server] hosted shutdown did not complete within 5 seconds; forcing process exit.");
                    Environment.Exit(0);
                }

                return;
            }

            if (cancellationToken.WaitHandle.WaitOne(PollInterval))
            {
                return;
            }
        }
    }

    private bool IsParentAlive()
    {
        try
        {
            using var process = Process.GetProcessById(_parentIdentity.ProcessId);
            return !process.HasExited && _parentIdentity.Matches(process);
        }
        catch
        {
            return false;
        }
    }
}
