#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

internal enum HostedServerRuntimeUpdateState
{
    None,
    SessionEnded,
    ProcessExited,
}

internal sealed class HostedServerRuntimeController : IDisposable
{
    private const int SnapshotPollIntervalTicks = 90;

    private readonly HostedServerConsoleState _console;
    private Process? _trackedProcess;
    private HostedServerSessionInfo? _session;
    private int _statePollTicks;
    private HostedServerProcessLogPaths? _processLogPaths;

    public HostedServerRuntimeController(HostedServerConsoleState console)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
    }

    public bool IsRunning
    {
        get
        {
            if (_session is not null
                && HostedServerBootstrapper.TryGetProcess(_session.ProcessId, out var attachedProcess))
            {
                attachedProcess?.Dispose();
                return true;
            }

            if (_trackedProcess is null)
            {
                return false;
            }

            try
            {
                return !_trackedProcess.HasExited;
            }
            catch
            {
                return false;
            }
        }
    }

    public int? TrackedProcessId => _trackedProcess?.Id;

    public bool HasTrackedProcessExited
    {
        get
        {
            if (_trackedProcess is null)
            {
                return false;
            }

            try
            {
                return _trackedProcess.HasExited;
            }
            catch
            {
                return false;
            }
        }
    }

    public bool TryStartBackground(HostedServerLaunchOptions launchOptions, out string error)
    {
        ArgumentNullException.ThrowIfNull(launchOptions);

        error = string.Empty;

        Stop();
        HostedServerSessionInfo.Delete();

        if (!HostedServerBootstrapper.IsUdpPortAvailable(launchOptions.Port))
        {
            error = $"UDP port {launchOptions.Port} is already in use.";
            _console.AppendLog("launcher", error);
            return false;
        }

        var serverLaunchTarget = HostedServerBootstrapper.FindLaunchTarget();
        if (serverLaunchTarget is null)
        {
            error = "Could not find OG2.Server. Build the server first.";
            return false;
        }

        if (!HostedServerBootstrapper.TryPrepareRuntimePlugins(serverLaunchTarget, out var pluginPreparationError))
        {
            error = pluginPreparationError;
            _console.AppendLog("launcher", error);
            return false;
        }

        try
        {
            var arguments = HostedServerBootstrapper.BuildLaunchArguments(serverLaunchTarget, launchOptions);
            _processLogPaths = HostedServerBootstrapper.PrepareProcessLogFiles();
            var startInfo = new ProcessStartInfo(serverLaunchTarget.FileName, arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = serverLaunchTarget.WorkingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            startInfo.Environment["OPENGARRISON_LAUNCH_MODE"] = "launcher";
            _console.AppendLog("launcher", $"Starting {serverLaunchTarget.FileName} {arguments}");
            var process = Process.Start(startInfo);
            if (process is null)
            {
                error = "Failed to start local server process.";
                return false;
            }

            BeginCapturingProcessOutput(process, _processLogPaths);
            TrackProcess(process);
            return true;
        }
        catch (Exception ex)
        {
            error = $"Failed to start local server: {ex.Message}";
            return false;
        }
    }

    public bool TryStartInTerminal(HostedServerLaunchOptions launchOptions, out string error)
    {
        ArgumentNullException.ThrowIfNull(launchOptions);

        error = string.Empty;

        Stop();
        HostedServerSessionInfo.Delete();

        if (!HostedServerBootstrapper.IsUdpPortAvailable(launchOptions.Port))
        {
            error = $"UDP port {launchOptions.Port} is already in use.";
            return false;
        }

        var serverLaunchTarget = HostedServerBootstrapper.FindLaunchTarget();
        if (serverLaunchTarget is null)
        {
            error = "Could not find OG2.Server. Build the server first.";
            return false;
        }

        if (!HostedServerBootstrapper.TryPrepareRuntimePlugins(serverLaunchTarget, out var pluginPreparationError))
        {
            error = pluginPreparationError;
            return false;
        }

        try
        {
            var arguments = HostedServerBootstrapper.BuildLaunchArguments(serverLaunchTarget, launchOptions);
            var startInfo = new ProcessStartInfo(serverLaunchTarget.FileName, arguments)
            {
                UseShellExecute = true,
                WorkingDirectory = serverLaunchTarget.WorkingDirectory,
            };
            Process.Start(startInfo);
            return true;
        }
        catch (Exception ex)
        {
            error = $"Failed to start dedicated server terminal: {ex.Message}";
            return false;
        }
    }

    public void Stop()
    {
        var session = _session;
        var trackedProcess = _trackedProcess;
        if (session is null && trackedProcess is null)
        {
            return;
        }

        try
        {
            if (session is not null)
            {
                _console.AppendLog("launcher", "Stop requested for hosted server.");
                if (!TrySendCommand("shutdown", out _, out var shutdownError))
                {
                    _console.AppendLog("launcher", shutdownError);
                }

                if (HostedServerBootstrapper.TryGetProcess(session.ProcessId, out var processToStop)
                    && processToStop is not null
                    && !processToStop.WaitForExit(2000)
                    && trackedProcess is not null
                    && trackedProcess.Id == session.ProcessId)
                {
                    _console.AppendLog("launcher", "Hosted server did not exit after shutdown; terminating process tree.");
                    trackedProcess.Kill(entireProcessTree: true);
                    trackedProcess.WaitForExit(1000);
                }

                processToStop?.Dispose();
            }
            else if (trackedProcess is not null && !trackedProcess.HasExited)
            {
                trackedProcess.Kill(entireProcessTree: true);
                trackedProcess.WaitForExit(1000);
            }
        }
        catch
        {
        }
        finally
        {
            ClearTracking();
            HostedServerSessionInfo.Delete();
        }
    }

    public bool TryResumeSession(bool loadExistingLog, int? expectedProcessId = null)
    {
        var session = HostedServerSessionInfo.Load();
        if (session is null)
        {
            return false;
        }

        if (expectedProcessId.HasValue && session.ProcessId != expectedProcessId.Value)
        {
            return false;
        }

        if (!HostedServerBootstrapper.TryGetProcess(session.ProcessId, out var attachedProcess))
        {
            HostedServerSessionInfo.Delete();
            return false;
        }

        attachedProcess?.Dispose();
        _session = session;
        _console.ApplySessionInfo(session);

        if (!TrySendCommand("__ping", out _, out _))
        {
            _session = null;
            return false;
        }

        _ = loadExistingLog;

        if (TrySendCommand("__snapshot", out var snapshotLines, out _))
        {
            _console.ApplyServerMessages(snapshotLines);
        }

        _statePollTicks = SnapshotPollIntervalTicks;
        return true;
    }

    public bool TrySendCommand(string command, out List<string> responseLines, out string error)
    {
        if (_session is null || string.IsNullOrWhiteSpace(_session.PipeName))
        {
            responseLines = new List<string>();
            error = "Dedicated server control channel is unavailable.";
            return false;
        }

        return HostedServerAdminClient.TrySendCommand(_session.PipeName, command, out responseLines, out error);
    }

    public HostedServerRuntimeUpdateState UpdateForLauncher()
    {
        if (_session is null)
        {
            TryResumeSession(loadExistingLog: true, expectedProcessId: TrackedProcessId);
        }

        if (_session is not null)
        {
            if (!HostedServerBootstrapper.TryGetProcess(_session.ProcessId, out var attachedProcess))
            {
                if (_trackedProcess is not null && _trackedProcess.Id == _session.ProcessId)
                {
                    DisposeTrackedProcess();
                }

                _session = null;
                _statePollTicks = 0;
                HostedServerSessionInfo.Delete();
                return HostedServerRuntimeUpdateState.SessionEnded;
            }

            attachedProcess?.Dispose();
            if (_statePollTicks <= 0)
            {
                if (TrySendCommand("__snapshot", out var snapshotLines, out _))
                {
                    _console.ApplyServerMessages(snapshotLines);
                }

                _statePollTicks = SnapshotPollIntervalTicks;
            }
            else
            {
                _statePollTicks -= 1;
            }

            return HostedServerRuntimeUpdateState.None;
        }

        if (_trackedProcess is null)
        {
            return HostedServerRuntimeUpdateState.None;
        }

        try
        {
            if (_trackedProcess.HasExited)
            {
                DisposeTrackedProcess();
                return HostedServerRuntimeUpdateState.ProcessExited;
            }
        }
        catch
        {
        }

        return HostedServerRuntimeUpdateState.None;
    }

    public void Dispose()
    {
        DisposeTrackedProcess();
    }

    private void TrackProcess(Process process)
    {
        DisposeTrackedProcess();
        process.EnableRaisingEvents = true;
        process.Exited += OnTrackedProcessExited;
        _trackedProcess = process;
    }

    private void ClearTracking()
    {
        DisposeTrackedProcess();
        _session = null;
        _statePollTicks = 0;
        _processLogPaths = null;
    }

    private void DisposeTrackedProcess()
    {
        if (_trackedProcess is null)
        {
            return;
        }

        try
        {
            _trackedProcess.Exited -= OnTrackedProcessExited;
        }
        catch
        {
        }

        _trackedProcess.Dispose();
        _trackedProcess = null;
    }

    private void OnTrackedProcessExited(object? sender, EventArgs e)
    {
        if (sender is not Process process)
        {
            return;
        }

        try
        {
            _console.AppendLog("launcher", $"Server process exited with code {process.ExitCode}.");
            AppendHostedServerProcessLogTail(_processLogPaths?.StdErrPath, "server-error");
        }
        catch
        {
        }
    }

    private void BeginCapturingProcessOutput(Process process, HostedServerProcessLogPaths logPaths)
    {
        process.OutputDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is null)
            {
                return;
            }

            AppendProcessLogLine(logPaths.StdOutPath, eventArgs.Data);
            _console.AppendLog("server", eventArgs.Data);
        };
        process.ErrorDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is null)
            {
                return;
            }

            AppendProcessLogLine(logPaths.StdErrPath, eventArgs.Data);
            _console.AppendLog("server-error", eventArgs.Data);
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
    }

    private static void AppendProcessLogLine(string path, string line)
    {
        try
        {
            File.AppendAllText(path, line + Environment.NewLine);
        }
        catch
        {
        }
    }

    private void AppendHostedServerProcessLogTail(string? path, string source)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        try
        {
            var lines = File.ReadLines(path).Reverse().Take(8).Reverse().ToArray();
            for (var index = 0; index < lines.Length; index += 1)
            {
                if (!string.IsNullOrWhiteSpace(lines[index]))
                {
                    _console.AppendLog(source, lines[index]);
                }
            }
        }
        catch
        {
        }
    }
}
