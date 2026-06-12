#nullable enable

using OpenGarrison.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const string ClientOnlineSmokeEnabledEnvironmentVariable = "OG_CLIENT_ONLINE_SMOKE";
    private const string ClientOnlineSmokeHostEnvironmentVariable = "OG_CLIENT_ONLINE_SMOKE_HOST";
    private const string ClientOnlineSmokePortEnvironmentVariable = "OG_CLIENT_ONLINE_SMOKE_PORT";
    private const string ClientOnlineSmokeDurationSecondsEnvironmentVariable = "OG_CLIENT_ONLINE_SMOKE_DURATION_SECONDS";
    private const string ClientOnlineSmokeAutoExitEnvironmentVariable = "OG_CLIENT_ONLINE_SMOKE_AUTO_EXIT";
    private const string ClientOnlineSmokeLogFilePrefix = "client-online-smoke";
    private const int ClientOnlineSmokeDefaultPort = 8190;
    private const int ClientOnlineSmokeDefaultDurationSeconds = 20;
    private const string ClientOnlineSmokeDefaultHost = "127.0.0.1";

    private static readonly bool ClientOnlineSmokeEnabled = GetClientPerformanceEnvironmentFlag(ClientOnlineSmokeEnabledEnvironmentVariable);

    private enum ClientOnlineSmokeStage
    {
        Idle,
        Connecting,
        Running,
        Completed,
    }

    private ClientOnlineSmokeStage _clientOnlineSmokeStage;
    private bool _clientOnlineSmokeDiagnosticsEnabled;
    private string? _clientOnlineSmokeLogPath;
    private DateTimeOffset _clientOnlineSmokeStartedAtUtc;
    private DateTimeOffset _clientOnlineSmokeLastSummaryAtUtc;
    private ulong _clientOnlineSmokeLastAppliedSnapshotFrame;
    private DateTimeOffset _clientOnlineSmokeLastAppliedSnapshotFrameAtUtc;
    private bool _clientOnlineSmokeDisconnectCaptured;
    private readonly List<string> _clientOnlineSmokeSummaryLines = [];

    private void AdvanceClientOnlineSmokeAutomation()
    {
        if (!ClientOnlineSmokeEnabled || OperatingSystem.IsBrowser() || _clientOnlineSmokeStage == ClientOnlineSmokeStage.Completed)
        {
            return;
        }

        EnsureClientOnlineSmokeInitialized();
        if (_clientOnlineSmokeStage == ClientOnlineSmokeStage.Idle)
        {
            if (_startupSplashOpen || !_mainMenuOpen || !_bootstrapController.CanEnterGameplaySession(out _))
            {
                return;
            }

            var host = Environment.GetEnvironmentVariable(ClientOnlineSmokeHostEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(host))
            {
                host = ClientOnlineSmokeDefaultHost;
            }

            var port = GetClientPerformanceEnvironmentInt(ClientOnlineSmokePortEnvironmentVariable, ClientOnlineSmokeDefaultPort);
            var connected = TryConnectToServer(host.Trim(), port, addConsoleFeedback: true);
            _clientOnlineSmokeStage = ClientOnlineSmokeStage.Connecting;
            _clientOnlineSmokeStartedAtUtc = DateTimeOffset.UtcNow;
            _clientOnlineSmokeLastAppliedSnapshotFrame = _lastAppliedSnapshotFrame;
            _clientOnlineSmokeLastAppliedSnapshotFrameAtUtc = _clientOnlineSmokeStartedAtUtc;
            AppendClientOnlineSmokeLine(
                $"event=connect requested={connected} host={host.Trim()} port={port} status=\"{SanitizeClientOnlineSmokeValue(_menuStatusMessage)}\"");
            return;
        }

        if (_teamSelectOpen)
        {
            ApplyTeamSelection(3);
            AppendClientOnlineSmokeLine("event=team_select team=BLU");
            return;
        }

        if (_classSelectOpen)
        {
            ApplyDirectClassSelection(GetClientPerformanceClass());
            CloseGameplaySelectionMenus();
            AppendClientOnlineSmokeLine($"event=class_select class={GetClientPerformanceClass()}");
            return;
        }

        if (_clientOnlineSmokeStage == ClientOnlineSmokeStage.Connecting
            && _networkClient.IsConnected
            && !_world.LocalPlayerAwaitingJoin
            && !_mainMenuOpen)
        {
            _clientOnlineSmokeStage = ClientOnlineSmokeStage.Running;
            AppendClientOnlineSmokeLine(
                $"event=session_started server=\"{SanitizeClientOnlineSmokeValue(_networkClient.ServerDescription)}\" slot={_networkClient.LocalPlayerSlot}");
        }

        var nowUtc = DateTimeOffset.UtcNow;
        if (_lastAppliedSnapshotFrame != _clientOnlineSmokeLastAppliedSnapshotFrame)
        {
            _clientOnlineSmokeLastAppliedSnapshotFrame = _lastAppliedSnapshotFrame;
            _clientOnlineSmokeLastAppliedSnapshotFrameAtUtc = nowUtc;
        }

        if ((nowUtc - _clientOnlineSmokeLastSummaryAtUtc).TotalSeconds >= 1d)
        {
            _clientOnlineSmokeLastSummaryAtUtc = nowUtc;
            AppendClientOnlineSmokeLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"event=sample stage={_clientOnlineSmokeStage} connected={_networkClient.IsConnected} mainMenu={_mainMenuOpen} awaitingJoin={_world.LocalPlayerAwaitingJoin} frame={_lastAppliedSnapshotFrame} queued={_queuedAuthoritativeSnapshots.Count} ping={_networkClient.EstimatedPingMilliseconds} frameStallMs={(nowUtc - _clientOnlineSmokeLastAppliedSnapshotFrameAtUtc).TotalMilliseconds:0} status=\"{SanitizeClientOnlineSmokeValue(_menuStatusMessage)}\""));
        }

        if (!_clientOnlineSmokeDisconnectCaptured
            && _clientOnlineSmokeStage != ClientOnlineSmokeStage.Idle
            && _mainMenuOpen
            && !_networkClient.IsConnected
            && !string.IsNullOrWhiteSpace(_menuStatusMessage))
        {
            _clientOnlineSmokeDisconnectCaptured = true;
            AppendClientOnlineSmokeLine(
                $"event=returned_to_menu reason=\"{SanitizeClientOnlineSmokeValue(_menuStatusMessage)}\"");
            FinalizeClientOnlineSmokeAutomation();
            return;
        }

        if (_clientOnlineSmokeStage == ClientOnlineSmokeStage.Running)
        {
            var durationSeconds = Math.Max(1, GetClientPerformanceEnvironmentInt(
                ClientOnlineSmokeDurationSecondsEnvironmentVariable,
                ClientOnlineSmokeDefaultDurationSeconds));
            if ((nowUtc - _clientOnlineSmokeStartedAtUtc).TotalSeconds >= durationSeconds)
            {
                AppendClientOnlineSmokeLine("event=duration_complete");
                FinalizeClientOnlineSmokeAutomation();
            }
        }
    }

    private void EnsureClientOnlineSmokeInitialized()
    {
        if (_clientOnlineSmokeDiagnosticsEnabled)
        {
            return;
        }

        _clientOnlineSmokeDiagnosticsEnabled = true;
        if (!_networkDiagnosticsEnabled)
        {
            EnableNetworkDiagnostics();
        }

        var directory = Path.Combine(AppContext.BaseDirectory, "network-diags");
        Directory.CreateDirectory(directory);
        _clientOnlineSmokeLogPath = Path.Combine(
            directory,
            $"{ClientOnlineSmokeLogFilePrefix}-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        _clientOnlineSmokeLastSummaryAtUtc = DateTimeOffset.UtcNow;
        AppendClientOnlineSmokeLine("event=initialized");
    }

    private void FinalizeClientOnlineSmokeAutomation()
    {
        if (_clientOnlineSmokeStage == ClientOnlineSmokeStage.Completed)
        {
            return;
        }

        _clientOnlineSmokeStage = ClientOnlineSmokeStage.Completed;
        ExportClientOnlineSmokeLog();
        if (GetClientPerformanceEnvironmentFlag(ClientOnlineSmokeAutoExitEnvironmentVariable))
        {
            Exit();
        }
    }

    private void ExportClientOnlineSmokeLog()
    {
        if (string.IsNullOrWhiteSpace(_clientOnlineSmokeLogPath))
        {
            return;
        }

        try
        {
            var lines = new List<string>(_clientOnlineSmokeSummaryLines.Count + _networkDiagnosticSummaryHistory.Count + 8)
            {
                $"generated={DateTime.Now:O}",
                $"connected={_networkClient.IsConnected}",
                $"mainMenu={_mainMenuOpen}",
                $"lastStatus=\"{SanitizeClientOnlineSmokeValue(_menuStatusMessage)}\"",
                $"lastAppliedSnapshotFrame={_lastAppliedSnapshotFrame}",
                $"queuedAuthoritativeSnapshots={_queuedAuthoritativeSnapshots.Count}",
                $"estimatedPingMilliseconds={_networkClient.EstimatedPingMilliseconds}",
                string.Empty,
                "[online-smoke]"
            };
            lines.AddRange(_clientOnlineSmokeSummaryLines);
            lines.Add(string.Empty);
            lines.Add("[netdiag]");
            if (_networkDiagnosticSummaryHistory.Count == 0)
            {
                lines.Add(_networkDiagnosticLastConsoleSummary);
            }
            else
            {
                lines.AddRange(_networkDiagnosticSummaryHistory);
            }

            File.WriteAllLines(_clientOnlineSmokeLogPath, lines);
            AddConsoleLine($"client online smoke exported to {_clientOnlineSmokeLogPath}");
        }
        catch (Exception ex)
        {
            AddConsoleLine($"client online smoke export failed: {ex.Message}");
        }
    }

    private void AppendClientOnlineSmokeLine(string line)
    {
        var timestampedLine = $"[{DateTime.Now:HH:mm:ss}] {line}";
        _clientOnlineSmokeSummaryLines.Add(timestampedLine);
        AddConsoleLine(timestampedLine);
    }

    private PlayerInputSnapshot ApplyClientOnlineSmokeInputPattern(PlayerInputSnapshot input)
    {
        if (!ClientOnlineSmokeEnabled
            || OperatingSystem.IsBrowser()
            || _clientOnlineSmokeStage != ClientOnlineSmokeStage.Running
            || !_networkClient.IsConnected
            || _mainMenuOpen
            || _world.LocalPlayerAwaitingJoin
            || !_world.LocalPlayer.IsAlive)
        {
            return input;
        }

        var phaseSeconds = (DateTimeOffset.UtcNow - _clientOnlineSmokeStartedAtUtc).TotalSeconds % 6d;
        var moveRight = phaseSeconds < 2d;
        var moveLeft = phaseSeconds >= 2d && phaseSeconds < 4d;
        var jump = phaseSeconds >= 4d && phaseSeconds < 4.2d;
        var firePrimary = phaseSeconds >= 4d;

        return input with
        {
            Left = moveLeft,
            Right = moveRight,
            Up = jump,
            Down = false,
            BuildSentry = false,
            DestroySentry = false,
            Taunt = false,
            FirePrimary = firePrimary,
            FireSecondary = false,
            DebugKill = false,
            DropIntel = false,
            UseAbility = false,
            InteractWeapon = false,
            SwapWeapon = false,
        };
    }

    private static string SanitizeClientOnlineSmokeValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Replace('\r', ' ').Replace('\n', ' ').Replace('"', '\'').Trim();
    }
}
