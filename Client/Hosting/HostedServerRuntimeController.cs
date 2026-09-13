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
    private string _sessionPath;
    private readonly string? _explicitSessionPath;
    private string _instanceId = Guid.NewGuid().ToString("N");
    private readonly HostedServerProcessIdentity _ownerIdentity;

    internal string InstanceId => _instanceId;
    internal string SessionPath => _sessionPath;
    public int? ReadyPort => LoadOwnedSession() is { IsReady: true } ready ? ready.Port : null;

    public HostedServerRuntimeController(HostedServerConsoleState console, string? sessionPath = null)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _explicitSessionPath = sessionPath;
        _sessionPath = sessionPath ?? HostedServerSessionInfo.GetInstancePath(_instanceId);
        HostedServerProcessIdentity.TryCaptureCurrent(out _ownerIdentity);
    }

    public bool IsRunning
    {
        get
        {
            if (_session is not null
                && HostedServerBootstrapper.TryGetProcess(_session, out var attachedProcess))
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
        => TryStart(launchOptions, terminal: false, out error);

    public bool TryStartInTerminal(HostedServerLaunchOptions launchOptions, out string error)
        => TryStart(launchOptions, terminal: true, out error);

    private bool TryStart(HostedServerLaunchOptions launchOptions, bool terminal, out string error)
    {
        ArgumentNullException.ThrowIfNull(launchOptions);
        error = string.Empty;
        var target = HostedServerBootstrapper.FindLaunchTarget();
        if (target is null)
        {
            error = "Could not find OG2.Server. Build the server first.";
            return false;
        }
        if (!HostedServerBootstrapper.TryValidateRuntimePrerequisites(target, out error)
            || !HostedServerBootstrapper.TryPrepareRuntimePlugins(target, out error)) return false;

        try
        {
            Stop();
            _instanceId = Guid.NewGuid().ToString("N");
            _sessionPath = _explicitSessionPath ?? HostedServerSessionInfo.GetInstancePath(_instanceId);
            var directory = Path.GetDirectoryName(_sessionPath)!;
            Directory.CreateDirectory(directory);
            var configPath = Path.Combine(directory, "server.ini");
            if (File.Exists(launchOptions.ConfigPath)) File.Copy(launchOptions.ConfigPath, configPath, overwrite: true);
            var sourceConfigDirectory = Path.GetDirectoryName(Path.GetFullPath(launchOptions.ConfigPath));
            var managementConfigPath = string.Empty;
            if (!string.IsNullOrWhiteSpace(sourceConfigDirectory))
            {
                var managementSource = Path.Combine(sourceConfigDirectory, "server-management.json");
                managementConfigPath = managementSource;
                if (File.Exists(managementSource))
                {
                    File.Copy(managementSource, Path.Combine(directory, "server-management.json"), overwrite: true);
                }
            }
            string? playlist = null;
            if (!string.IsNullOrWhiteSpace(launchOptions.MapRotationFile))
            {
                playlist = Path.Combine(directory, "rotation.txt");
                File.Copy(launchOptions.MapRotationFile, playlist, overwrite: true);
            }
            launchOptions = launchOptions with
            {
                ConfigPath = configPath,
                MapRotationFile = playlist,
                ManagementConfigPath = managementConfigPath,
            };
            _processLogPaths = HostedServerBootstrapper.PrepareProcessLogFiles(directory);
            var startInfo = HostedServerBootstrapper.BuildStartInfo(target, launchOptions);
            ConfigureProcessEnvironment(startInfo, terminal);
            ApplyRelayEnvironment(startInfo, launchOptions.RelayHostUrl);
            _console.AppendLog("launcher", $"Starting server instance {_instanceId}; requested port {launchOptions.Port}. Logs: {directory}");
            if (terminal)
            {
                DedicatedServerTerminalLauncher.Start(startInfo);
                // Direct servers are independent. Their session file is never
                // automatically attached by this controller or another client.
                _processLogPaths = null;
                return true;
            }
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            var process = Process.Start(startInfo) ?? throw new InvalidOperationException("No server process was created.");
            BeginCapturingProcessOutput(process, _processLogPaths);
            TrackProcess(process);
            return true;
        }
        catch (Exception ex)
        {
            error = $"Failed to start {(terminal ? "dedicated server terminal" : "local server")}: {ex.Message}";
            _console.AppendLog("launcher", error);
            return false;
        }
    }

    internal void ConfigureProcessEnvironment(ProcessStartInfo startInfo, bool terminal)
    {
        startInfo.UseShellExecute = false;
        startInfo.Environment[HostedServerSessionInfo.SessionPathEnvironmentVariable] = _sessionPath;
        startInfo.Environment[HostedServerSessionInfo.InstanceEnvironmentVariable] = _instanceId;
        startInfo.Environment[RuntimePaths.UserDataRootEnvironmentVariable] = RuntimePaths.UserDataRoot;
        startInfo.Environment["OPENGARRISON_LAUNCH_MODE"] = terminal ? "direct" : "launcher";
        startInfo.Environment.Remove(HostedServerProcessIdentity.ParentProcessIdEnvironmentVariable);
        startInfo.Environment.Remove(HostedServerProcessIdentity.ParentProcessStartTimeEnvironmentVariable);
        if (!terminal) _ownerIdentity.ApplyParentEnvironment(startInfo);
    }

    private HostedServerSessionInfo? LoadOwnedSession()
    {
        var session = HostedServerSessionInfo.Load(_sessionPath);
        return session?.IsOwnedBy(_instanceId, _ownerIdentity) == true ? session : null;
    }

    internal static void ApplyRelayEnvironment(ProcessStartInfo startInfo, string? relayHostUrl)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        if (string.IsNullOrWhiteSpace(relayHostUrl))
        {
            startInfo.Environment.Remove("OPENGARRISON_RELAY_HOST_URL");
            return;
        }

        startInfo.Environment["OPENGARRISON_RELAY_HOST_URL"] = relayHostUrl.Trim();
    }

    public void Stop()
    {
        var session = _session ?? LoadOwnedSession();
        var tracked = _trackedProcess;
        try
        {
            if (session is not null && session.IsOwnedBy(_instanceId, _ownerIdentity))
            {
                _console.AppendLog("launcher", $"Stopping owned server instance {session.InstanceId}.");
                HostedServerAdminClient.TrySendCommand(session.PipeName, "shutdown", out _, out _,
                    HostedServerAdminClient.ShutdownTimeoutMilliseconds);
                if (HostedServerBootstrapper.TryGetProcess(session, out var process) && process is not null)
                {
                    using (process)
                    {
                        if (!process.WaitForExit(2000))
                        {
                            _console.AppendLog("launcher", "Owned server did not finish shutdown; terminating its process tree.");
                            process.Kill(entireProcessTree: true);
                            process.WaitForExit(1000);
                        }
                    }
                }
            }
            // A retained Process handle belongs to this controller even when
            // startup failed before the server published its session record.
            if (tracked is not null && !tracked.HasExited)
            {
                tracked.Kill(entireProcessTree: true);
                tracked.WaitForExit(1000);
            }
        }
        catch (Exception ex)
        {
            _console.AppendLog("launcher", $"Owned server cleanup failed: {ex.Message}");
        }
        finally
        {
            if (session?.IsOwnedBy(_instanceId, _ownerIdentity) == true)
                HostedServerSessionInfo.DeleteIfMatching(session, _sessionPath);
            ClearTracking();
        }
    }

    public bool TryResumeSession(
        bool loadExistingLog,
        int? expectedProcessId = null,
        int commandTimeoutMilliseconds = HostedServerAdminClient.DefaultTimeoutMilliseconds)
    {
        var session = LoadOwnedSession();
        if (session is null)
        {
            return false;
        }

        if (expectedProcessId.HasValue && session.ProcessId != expectedProcessId.Value)
        {
            return false;
        }

        if (!HostedServerBootstrapper.TryGetProcess(session, out var attachedProcess))
        {
            HostedServerSessionInfo.DeleteIfMatching(session, _sessionPath);
            return false;
        }

        attachedProcess?.Dispose();
        _session = session;
        _console.ApplySessionInfo(session);

        if (!TrySendCommand(
                "__ping",
                out _,
                out _,
                commandTimeoutMilliseconds))
        {
            _session = null;
            return false;
        }

        _ = loadExistingLog;

        if (TrySendCommand(
                "__snapshot",
                out var snapshotLines,
                out _,
                commandTimeoutMilliseconds))
        {
            _console.ApplyServerMessages(snapshotLines);
        }

        _statePollTicks = SnapshotPollIntervalTicks;
        return true;
    }

    public bool TrySendCommand(
        string command,
        out List<string> responseLines,
        out string error,
        int commandTimeoutMilliseconds = HostedServerAdminClient.DefaultTimeoutMilliseconds)
    {
        if ((_session is null || string.IsNullOrWhiteSpace(_session.PipeName))
            && !TryResumeSession(
                loadExistingLog: false,
                expectedProcessId: TrackedProcessId,
                commandTimeoutMilliseconds: commandTimeoutMilliseconds))
        {
            responseLines = new List<string>();
            error = "Dedicated server control channel is unavailable.";
            return false;
        }

        var session = _session;
        if (session is null)
        {
            responseLines = new List<string>();
            error = "Dedicated server control channel is unavailable.";
            return false;
        }

        return HostedServerAdminClient.TrySendCommand(
            session.PipeName,
            command,
            out responseLines,
            out error,
            commandTimeoutMilliseconds);
    }

    public HostedServerRuntimeUpdateState UpdateForLauncher()
    {
        if (_session is null)
        {
            TryResumeSession(loadExistingLog: true, expectedProcessId: TrackedProcessId);
        }

        if (_session is not null)
        {
            if (!HostedServerBootstrapper.TryGetProcess(_session, out var attachedProcess))
            {
                if (_trackedProcess is not null && _trackedProcess.Id == _session.ProcessId)
                {
                    DisposeTrackedProcess();
                }

                HostedServerSessionInfo.DeleteIfMatching(_session, _sessionPath);
                _session = null;
                _statePollTicks = 0;
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
