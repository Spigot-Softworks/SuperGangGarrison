#nullable enable

using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using OpenGarrison.Protocol;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const double NetworkDiagnosticSummaryIntervalSeconds = 1d;
    private const int NetworkDiagnosticHistoryLimit = 180;
    private bool _networkDiagnosticsEnabled;
    private readonly List<string> _networkDiagnosticOverlayLines = new();
    private readonly List<string> _networkDiagnosticSummaryHistory = new();
    private double _networkDiagnosticSummaryElapsedSeconds;
    private int _networkDiagnosticSummaryFrames;
    private double _networkDiagnosticUpdateElapsedTotalMilliseconds;
    private double _networkDiagnosticUpdateElapsedMaxMilliseconds;
    private int _networkDiagnosticUpdateHitches33;
    private int _networkDiagnosticUpdateHitches50;
    private int _networkDiagnosticReceivePolls;
    private int _networkDiagnosticReceivePackets;
    private long _networkDiagnosticReceiveBytes;
    private int _networkDiagnosticReceiveReleasedMessages;
    private int _networkDiagnosticReceiveSnapshotMessages;
    private int _networkDiagnosticReceiveMaxBatchPackets;
    private int _networkDiagnosticReceiveMaxBatchMessages;
    private int _networkDiagnosticReceiveMaxPayloadBytes;
    private int _networkDiagnosticReceiveMaxPendingInboundMessages;
    private double _networkDiagnosticReceiveDeserializeTotalMilliseconds;
    private double _networkDiagnosticReceiveDeserializeMaxMilliseconds;
    private int _networkDiagnosticProcessedMessages;
    private int _networkDiagnosticProcessedSnapshots;
    private int _networkDiagnosticProcessedDeltaSnapshots;
    private int _networkDiagnosticProcessedFullSnapshots;
    private int _networkDiagnosticAppliedSnapshots;
    private int _networkDiagnosticStaleSnapshots;
    private int _networkDiagnosticMissingBaselineSnapshots;
    private int _networkDiagnosticRejectedSnapshots;
    private int _networkDiagnosticCurrentFrameSnapshotMessages;
    private int _networkDiagnosticMaxSnapshotsPerFrame;
    private double _networkDiagnosticProcessMessagesTotalMilliseconds;
    private double _networkDiagnosticProcessMessagesMaxMilliseconds;
    private double _networkDiagnosticApplySnapshotTotalMilliseconds;
    private double _networkDiagnosticApplySnapshotMaxMilliseconds;
    private double _networkDiagnosticReconcileTotalMilliseconds;
    private double _networkDiagnosticReconcileMaxMilliseconds;
    private double _networkDiagnosticInterpolationTotalMilliseconds;
    private double _networkDiagnosticInterpolationMaxMilliseconds;
    private double _networkDiagnosticPredictionErrorTotalPixels;
    private double _networkDiagnosticPredictionErrorMaxPixels;
    private int _networkDiagnosticPredictionErrorSamples;
    private float _networkDiagnosticLatestPredictionErrorPixels;
    private double _networkDiagnosticRenderCorrectionTotalPixels;
    private double _networkDiagnosticRenderCorrectionMaxPixels;
    private int _networkDiagnosticRenderCorrectionSamples;
    private float _networkDiagnosticLatestRenderCorrectionPixels;
    private int _networkDiagnosticRenderCorrectionHardSnaps;
    private bool _networkDiagnosticGcBaselineInitialized;
    private int _networkDiagnosticLastGen0Collections;
    private int _networkDiagnosticLastGen1Collections;
    private int _networkDiagnosticLastGen2Collections;
    private string _networkDiagnosticLastConsoleSummary = "netdiag disabled";

    private void BeginNetworkDiagnosticsFrame(GameTime gameTime)
    {
        if (!_networkDiagnosticsEnabled)
        {
            return;
        }

        var elapsedMilliseconds = Math.Clamp(gameTime.ElapsedGameTime.TotalMilliseconds, 0d, 250d);
        _networkDiagnosticSummaryElapsedSeconds += Math.Max(0d, gameTime.ElapsedGameTime.TotalSeconds);
        _networkDiagnosticSummaryFrames += 1;
        _networkDiagnosticUpdateElapsedTotalMilliseconds += elapsedMilliseconds;
        _networkDiagnosticUpdateElapsedMaxMilliseconds = Math.Max(_networkDiagnosticUpdateElapsedMaxMilliseconds, elapsedMilliseconds);
        if (elapsedMilliseconds >= 33.4d)
        {
            _networkDiagnosticUpdateHitches33 += 1;
        }

        if (elapsedMilliseconds >= 50d)
        {
            _networkDiagnosticUpdateHitches50 += 1;
        }

        _networkDiagnosticCurrentFrameSnapshotMessages = 0;
    }

    private void FinalizeNetworkDiagnosticsFrame()
    {
        if (!_networkDiagnosticsEnabled)
        {
            return;
        }

        _networkDiagnosticMaxSnapshotsPerFrame = Math.Max(_networkDiagnosticMaxSnapshotsPerFrame, _networkDiagnosticCurrentFrameSnapshotMessages);
        if (_networkDiagnosticSummaryElapsedSeconds < NetworkDiagnosticSummaryIntervalSeconds)
        {
            return;
        }

        PublishNetworkDiagnosticSummary();
    }

    private void RecordNetworkReceiveDiagnostics(NetworkGameClient.ReceiveDiagnostics diagnostics)
    {
        if (!_networkDiagnosticsEnabled)
        {
            return;
        }

        _networkDiagnosticReceivePolls += 1;
        _networkDiagnosticReceivePackets += diagnostics.PacketsRead;
        _networkDiagnosticReceiveBytes += diagnostics.BytesRead;
        _networkDiagnosticReceiveReleasedMessages += diagnostics.ReleasedMessages;
        _networkDiagnosticReceiveSnapshotMessages += diagnostics.SnapshotMessages;
        _networkDiagnosticReceiveMaxBatchPackets = Math.Max(_networkDiagnosticReceiveMaxBatchPackets, diagnostics.PacketsRead);
        _networkDiagnosticReceiveMaxBatchMessages = Math.Max(_networkDiagnosticReceiveMaxBatchMessages, diagnostics.ReleasedMessages);
        _networkDiagnosticReceiveMaxPayloadBytes = Math.Max(_networkDiagnosticReceiveMaxPayloadBytes, diagnostics.MaxPayloadBytes);
        _networkDiagnosticReceiveMaxPendingInboundMessages = Math.Max(_networkDiagnosticReceiveMaxPendingInboundMessages, diagnostics.PendingInboundMessages);
        _networkDiagnosticReceiveDeserializeTotalMilliseconds += diagnostics.DeserializeMilliseconds;
        _networkDiagnosticReceiveDeserializeMaxMilliseconds = Math.Max(
            _networkDiagnosticReceiveDeserializeMaxMilliseconds,
            diagnostics.MaxDeserializeMilliseconds);
    }

    private void RecordNetworkMessageProcessed(IProtocolMessage message)
    {
        if (!_networkDiagnosticsEnabled)
        {
            return;
        }

        _networkDiagnosticProcessedMessages += 1;
        if (message is SnapshotMessage snapshot)
        {
            _networkDiagnosticProcessedSnapshots += 1;
            _networkDiagnosticCurrentFrameSnapshotMessages += 1;
            if (snapshot.IsDelta)
            {
                _networkDiagnosticProcessedDeltaSnapshots += 1;
            }
            else
            {
                _networkDiagnosticProcessedFullSnapshots += 1;
            }
        }
    }

    private void RecordStaleSnapshot()
    {
        if (_networkDiagnosticsEnabled)
        {
            _networkDiagnosticStaleSnapshots += 1;
        }
    }

    private void RecordMissingBaselineSnapshot()
    {
        if (_networkDiagnosticsEnabled)
        {
            _networkDiagnosticMissingBaselineSnapshots += 1;
        }
    }

    private void RecordRejectedSnapshot()
    {
        if (_networkDiagnosticsEnabled)
        {
            _networkDiagnosticRejectedSnapshots += 1;
        }
    }

    private void RecordAppliedSnapshot()
    {
        if (_networkDiagnosticsEnabled)
        {
            _networkDiagnosticAppliedSnapshots += 1;
        }
    }

    private void RecordPredictionError(float errorPixels)
    {
        if (!_networkDiagnosticsEnabled)
        {
            return;
        }

        var clampedErrorPixels = Math.Max(0f, errorPixels);
        _networkDiagnosticPredictionErrorTotalPixels += clampedErrorPixels;
        _networkDiagnosticPredictionErrorMaxPixels = Math.Max(_networkDiagnosticPredictionErrorMaxPixels, clampedErrorPixels);
        _networkDiagnosticPredictionErrorSamples += 1;
        _networkDiagnosticLatestPredictionErrorPixels = clampedErrorPixels;
    }

    private void RecordPredictedRenderCorrection(float correctionPixels, bool hardSnap)
    {
        if (!_networkDiagnosticsEnabled)
        {
            return;
        }

        var clampedCorrectionPixels = Math.Max(0f, correctionPixels);
        _networkDiagnosticRenderCorrectionTotalPixels += clampedCorrectionPixels;
        _networkDiagnosticRenderCorrectionMaxPixels = Math.Max(_networkDiagnosticRenderCorrectionMaxPixels, clampedCorrectionPixels);
        _networkDiagnosticRenderCorrectionSamples += 1;
        _networkDiagnosticLatestRenderCorrectionPixels = clampedCorrectionPixels;
        if (hardSnap)
        {
            _networkDiagnosticRenderCorrectionHardSnaps += 1;
        }
    }

    private void RecordProcessNetworkMessagesDuration(double elapsedMilliseconds)
    {
        if (!_networkDiagnosticsEnabled)
        {
            return;
        }

        _networkDiagnosticProcessMessagesTotalMilliseconds += elapsedMilliseconds;
        _networkDiagnosticProcessMessagesMaxMilliseconds = Math.Max(_networkDiagnosticProcessMessagesMaxMilliseconds, elapsedMilliseconds);
    }

    private void RecordApplySnapshotDuration(double elapsedMilliseconds)
    {
        if (!_networkDiagnosticsEnabled)
        {
            return;
        }

        _networkDiagnosticApplySnapshotTotalMilliseconds += elapsedMilliseconds;
        _networkDiagnosticApplySnapshotMaxMilliseconds = Math.Max(_networkDiagnosticApplySnapshotMaxMilliseconds, elapsedMilliseconds);
    }

    private void RecordReconcileDuration(double elapsedMilliseconds)
    {
        if (!_networkDiagnosticsEnabled)
        {
            return;
        }

        _networkDiagnosticReconcileTotalMilliseconds += elapsedMilliseconds;
        _networkDiagnosticReconcileMaxMilliseconds = Math.Max(_networkDiagnosticReconcileMaxMilliseconds, elapsedMilliseconds);
    }

    private void RecordInterpolationDuration(double elapsedMilliseconds)
    {
        if (!_networkDiagnosticsEnabled)
        {
            return;
        }

        _networkDiagnosticInterpolationTotalMilliseconds += elapsedMilliseconds;
        _networkDiagnosticInterpolationMaxMilliseconds = Math.Max(_networkDiagnosticInterpolationMaxMilliseconds, elapsedMilliseconds);
    }

    private void EnableNetworkDiagnostics()
    {
        _networkDiagnosticsEnabled = true;
        _networkClient.CollectDiagnostics = true;
        ResetNetworkDiagnosticSample();
        _networkDiagnosticSummaryHistory.Clear();
        _networkDiagnosticOverlayLines.Clear();
        InitializeNetworkDiagnosticGcBaseline();
        AddConsoleLine("network diagnostics enabled");
    }

    private void DisableNetworkDiagnostics()
    {
        _networkDiagnosticsEnabled = false;
        _networkClient.CollectDiagnostics = false;
        _networkDiagnosticOverlayLines.Clear();
        AddConsoleLine("network diagnostics disabled");
    }

    private void ClearNetworkDiagnosticsHistory()
    {
        _networkDiagnosticSummaryHistory.Clear();
        _networkDiagnosticOverlayLines.Clear();
        _networkDiagnosticLastConsoleSummary = "netdiag history cleared";
        ResetNetworkDiagnosticSample();
        InitializeNetworkDiagnosticGcBaseline();
        AddConsoleLine("network diagnostics history cleared");
    }

    private void PrintNetworkDiagnosticsStatus()
    {
        AddConsoleLine(_networkDiagnosticsEnabled ? "network diagnostics: enabled" : "network diagnostics: disabled");
        AddConsoleLine(_networkDiagnosticLastConsoleSummary);
    }

    private void ExportNetworkDiagnosticsHistory()
    {
        try
        {
            var directory = Path.Combine(AppContext.BaseDirectory, "network-diags");
            Directory.CreateDirectory(directory);
            var filename = $"netdiag-{DateTime.Now:yyyyMMdd-HHmmss}.log";
            var path = Path.Combine(directory, filename);
            var lines = new List<string>(_networkDiagnosticSummaryHistory.Count + 2)
            {
                $"generated={DateTime.Now:O}",
                _networkDiagnosticsEnabled ? "status=enabled" : "status=disabled",
            };
            if (_networkDiagnosticSummaryHistory.Count == 0)
            {
                lines.Add(_networkDiagnosticLastConsoleSummary);
            }
            else
            {
                lines.AddRange(_networkDiagnosticSummaryHistory);
            }

            File.WriteAllLines(path, lines);
            AddConsoleLine($"network diagnostics exported to {path}");
        }
        catch (Exception ex)
        {
            AddConsoleLine($"network diagnostics export failed: {ex.Message}");
        }
    }

    private void DrawNetworkDiagnosticsOverlay()
    {
        if (!_networkDiagnosticsEnabled)
        {
            return;
        }

        if (_networkDiagnosticOverlayLines.Count == 0)
        {
            _networkDiagnosticOverlayLines.Add("NET DIAG waiting for first 1s sample...");
            _networkDiagnosticOverlayLines.Add(_networkClient.IsConnected
                ? $"conn=yes slot={_networkClient.LocalPlayerSlot} server={_networkClient.ServerDescription}"
                : "conn=no");
        }

        var width = 620;
        var lineHeight = 18;
        var padding = 10;
        var height = (_networkDiagnosticOverlayLines.Count * lineHeight) + (padding * 2);
        var x = ViewportWidth - width - 18;
        var y = 18;
        var rectangle = new Rectangle(x, y, width, height);
        _spriteBatch.Draw(_pixel, rectangle, new Color(12, 16, 20, 210));
        _spriteBatch.Draw(_pixel, new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, 2), new Color(120, 220, 255));

        var position = new Vector2(rectangle.X + padding, rectangle.Y + padding);
        for (var index = 0; index < _networkDiagnosticOverlayLines.Count; index += 1)
        {
            _spriteBatch.DrawString(_consoleFont, _networkDiagnosticOverlayLines[index], position, new Color(232, 238, 244));
            position.Y += lineHeight;
        }
    }

    private void PublishNetworkDiagnosticSummary()
    {
        var intervalSeconds = Math.Max(_networkDiagnosticSummaryElapsedSeconds, 0.0001d);
        var fps = _networkDiagnosticSummaryFrames / intervalSeconds;
        var averageUpdateMilliseconds = _networkDiagnosticSummaryFrames == 0
            ? 0d
            : _networkDiagnosticUpdateElapsedTotalMilliseconds / _networkDiagnosticSummaryFrames;
        var receiveKilobytesPerSecond = _networkDiagnosticReceiveBytes / 1024d / intervalSeconds;
        var averageReceiveDeserializeMilliseconds = _networkDiagnosticReceivePackets == 0
            ? 0d
            : _networkDiagnosticReceiveDeserializeTotalMilliseconds / _networkDiagnosticReceivePackets;
        var averageProcessNetworkMessagesMilliseconds = _networkDiagnosticReceivePolls == 0
            ? 0d
            : _networkDiagnosticProcessMessagesTotalMilliseconds / _networkDiagnosticReceivePolls;
        var averageApplySnapshotMilliseconds = _networkDiagnosticAppliedSnapshots == 0
            ? 0d
            : _networkDiagnosticApplySnapshotTotalMilliseconds / _networkDiagnosticAppliedSnapshots;
        var averageReconcileMilliseconds = _networkDiagnosticAppliedSnapshots == 0
            ? 0d
            : _networkDiagnosticReconcileTotalMilliseconds / _networkDiagnosticAppliedSnapshots;
        var averageInterpolationMilliseconds = _networkDiagnosticSummaryFrames == 0
            ? 0d
            : _networkDiagnosticInterpolationTotalMilliseconds / _networkDiagnosticSummaryFrames;
        var averagePredictionErrorPixels = _networkDiagnosticPredictionErrorSamples == 0
            ? 0d
            : _networkDiagnosticPredictionErrorTotalPixels / _networkDiagnosticPredictionErrorSamples;
        var averageRenderCorrectionPixels = _networkDiagnosticRenderCorrectionSamples == 0
            ? 0d
            : _networkDiagnosticRenderCorrectionTotalPixels / _networkDiagnosticRenderCorrectionSamples;
        var currentMemoryMegabytes = GC.GetTotalMemory(forceFullCollection: false) / (1024d * 1024d);
        var (gen0Collections, gen1Collections, gen2Collections) = ConsumeNetworkDiagnosticGcDeltas();
        var timelineErrorMilliseconds = GetNetworkDiagnosticTimelineErrorMilliseconds();

        _networkDiagnosticOverlayLines.Clear();
        _networkDiagnosticOverlayLines.Add(
            $"NET DIAG conn={(_networkClient.IsConnected ? "yes" : "no")} slot={_networkClient.LocalPlayerSlot} sim={_networkClient.SimulatedLatencyMilliseconds}ms pendingIn={_networkClient.LastReceiveDiagnostics.PendingInboundMessages}");
        _networkDiagnosticOverlayLines.Add(
            string.Create(CultureInfo.InvariantCulture, $"upd {fps:F1}fps dt {averageUpdateMilliseconds:F1}/{_networkDiagnosticUpdateElapsedMaxMilliseconds:F1}ms hitch33 {_networkDiagnosticUpdateHitches33} hitch50 {_networkDiagnosticUpdateHitches50}"));
        _networkDiagnosticOverlayLines.Add(
            string.Create(CultureInfo.InvariantCulture, $"recv pkts {_networkDiagnosticReceivePackets / intervalSeconds:F1}/s msgs {_networkDiagnosticReceiveReleasedMessages / intervalSeconds:F1}/s snaps {_networkDiagnosticReceiveSnapshotMessages / intervalSeconds:F1}/s bytes {receiveKilobytesPerSecond:F1}KB/s burst {_networkDiagnosticReceiveMaxBatchPackets}/{_networkDiagnosticReceiveMaxBatchMessages}"));
        _networkDiagnosticOverlayLines.Add(
            string.Create(CultureInfo.InvariantCulture, $"recv payload {_networkDiagnosticReceiveMaxPayloadBytes}B decode {averageReceiveDeserializeMilliseconds:F3}/{_networkDiagnosticReceiveDeserializeMaxMilliseconds:F3}ms"));
        _networkDiagnosticOverlayLines.Add(
            string.Create(CultureInfo.InvariantCulture, $"snap proc {_networkDiagnosticProcessedSnapshots} appl {_networkDiagnosticAppliedSnapshots} frameBurst {_networkDiagnosticMaxSnapshotsPerFrame} stale {_networkDiagnosticStaleSnapshots} missBase {_networkDiagnosticMissingBaselineSnapshots} rej {_networkDiagnosticRejectedSnapshots} delta/full {_networkDiagnosticProcessedDeltaSnapshots}/{_networkDiagnosticProcessedFullSnapshots}"));
        _networkDiagnosticOverlayLines.Add(
            string.Create(CultureInfo.InvariantCulture, $"cost net {averageProcessNetworkMessagesMilliseconds:F3}/{_networkDiagnosticProcessMessagesMaxMilliseconds:F3}ms apply {averageApplySnapshotMilliseconds:F3}/{_networkDiagnosticApplySnapshotMaxMilliseconds:F3}ms interp {averageInterpolationMilliseconds:F3}/{_networkDiagnosticInterpolationMaxMilliseconds:F3}ms recon {averageReconcileMilliseconds:F3}/{_networkDiagnosticReconcileMaxMilliseconds:F3}ms"));
        _networkDiagnosticOverlayLines.Add(
            string.Create(CultureInfo.InvariantCulture, $"pred err {averagePredictionErrorPixels:F2}px max {_networkDiagnosticPredictionErrorMaxPixels:F2}px last {_networkDiagnosticLatestPredictionErrorPixels:F2}px"));
        _networkDiagnosticOverlayLines.Add(
            string.Create(CultureInfo.InvariantCulture, $"view corr {averageRenderCorrectionPixels:F2}px max {_networkDiagnosticRenderCorrectionMaxPixels:F2}px last {_networkDiagnosticLatestRenderCorrectionPixels:F2}px snaps {_networkDiagnosticRenderCorrectionHardSnaps}"));
        _networkDiagnosticOverlayLines.Add(
            string.Create(CultureInfo.InvariantCulture, $"timeline int {_smoothedSnapshotIntervalSeconds * 1000f:F1}ms jitter {_smoothedSnapshotJitterSeconds * 1000f:F1}ms back {_remotePlayerInterpolationBackTimeSeconds * 1000f:F1}ms err {timelineErrorMilliseconds:F1}ms"));
        _networkDiagnosticOverlayLines.Add(
            string.Create(CultureInfo.InvariantCulture, $"gc {gen0Collections}/{gen1Collections}/{gen2Collections} mem {currentMemoryMegabytes:F1}MB"));

        _networkDiagnosticLastConsoleSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"netdiag fps={fps:F1} hitch33={_networkDiagnosticUpdateHitches33} pkts={_networkDiagnosticReceivePackets / intervalSeconds:F1}/s snaps={_networkDiagnosticAppliedSnapshots / intervalSeconds:F1}/s burst={_networkDiagnosticMaxSnapshotsPerFrame} proc={averageProcessNetworkMessagesMilliseconds:F3}/{_networkDiagnosticProcessMessagesMaxMilliseconds:F3}ms apply={averageApplySnapshotMilliseconds:F3}/{_networkDiagnosticApplySnapshotMaxMilliseconds:F3}ms pred={averagePredictionErrorPixels:F2}/{_networkDiagnosticPredictionErrorMaxPixels:F2}px view={averageRenderCorrectionPixels:F2}/{_networkDiagnosticRenderCorrectionMaxPixels:F2}px snaps={_networkDiagnosticRenderCorrectionHardSnaps} gc={gen0Collections}/{gen1Collections}/{gen2Collections} mem={currentMemoryMegabytes:F1}MB");
        _networkDiagnosticSummaryHistory.Add($"[{DateTime.Now:HH:mm:ss}] {_networkDiagnosticLastConsoleSummary}");
        while (_networkDiagnosticSummaryHistory.Count > NetworkDiagnosticHistoryLimit)
        {
            _networkDiagnosticSummaryHistory.RemoveAt(0);
        }

        AddConsoleLine(_networkDiagnosticLastConsoleSummary);
        ResetNetworkDiagnosticSample();
    }

    private void ResetNetworkDiagnosticSample()
    {
        _networkDiagnosticSummaryElapsedSeconds = 0d;
        _networkDiagnosticSummaryFrames = 0;
        _networkDiagnosticUpdateElapsedTotalMilliseconds = 0d;
        _networkDiagnosticUpdateElapsedMaxMilliseconds = 0d;
        _networkDiagnosticUpdateHitches33 = 0;
        _networkDiagnosticUpdateHitches50 = 0;
        _networkDiagnosticReceivePolls = 0;
        _networkDiagnosticReceivePackets = 0;
        _networkDiagnosticReceiveBytes = 0L;
        _networkDiagnosticReceiveReleasedMessages = 0;
        _networkDiagnosticReceiveSnapshotMessages = 0;
        _networkDiagnosticReceiveMaxBatchPackets = 0;
        _networkDiagnosticReceiveMaxBatchMessages = 0;
        _networkDiagnosticReceiveMaxPayloadBytes = 0;
        _networkDiagnosticReceiveMaxPendingInboundMessages = 0;
        _networkDiagnosticReceiveDeserializeTotalMilliseconds = 0d;
        _networkDiagnosticReceiveDeserializeMaxMilliseconds = 0d;
        _networkDiagnosticProcessedMessages = 0;
        _networkDiagnosticProcessedSnapshots = 0;
        _networkDiagnosticProcessedDeltaSnapshots = 0;
        _networkDiagnosticProcessedFullSnapshots = 0;
        _networkDiagnosticAppliedSnapshots = 0;
        _networkDiagnosticStaleSnapshots = 0;
        _networkDiagnosticMissingBaselineSnapshots = 0;
        _networkDiagnosticRejectedSnapshots = 0;
        _networkDiagnosticCurrentFrameSnapshotMessages = 0;
        _networkDiagnosticMaxSnapshotsPerFrame = 0;
        _networkDiagnosticProcessMessagesTotalMilliseconds = 0d;
        _networkDiagnosticProcessMessagesMaxMilliseconds = 0d;
        _networkDiagnosticApplySnapshotTotalMilliseconds = 0d;
        _networkDiagnosticApplySnapshotMaxMilliseconds = 0d;
        _networkDiagnosticReconcileTotalMilliseconds = 0d;
        _networkDiagnosticReconcileMaxMilliseconds = 0d;
        _networkDiagnosticInterpolationTotalMilliseconds = 0d;
        _networkDiagnosticInterpolationMaxMilliseconds = 0d;
        _networkDiagnosticPredictionErrorTotalPixels = 0d;
        _networkDiagnosticPredictionErrorMaxPixels = 0d;
        _networkDiagnosticPredictionErrorSamples = 0;
        _networkDiagnosticLatestPredictionErrorPixels = 0f;
        _networkDiagnosticRenderCorrectionTotalPixels = 0d;
        _networkDiagnosticRenderCorrectionMaxPixels = 0d;
        _networkDiagnosticRenderCorrectionSamples = 0;
        _networkDiagnosticLatestRenderCorrectionPixels = 0f;
        _networkDiagnosticRenderCorrectionHardSnaps = 0;
    }

    private void InitializeNetworkDiagnosticGcBaseline()
    {
        _networkDiagnosticGcBaselineInitialized = true;
        _networkDiagnosticLastGen0Collections = GC.CollectionCount(0);
        _networkDiagnosticLastGen1Collections = GC.CollectionCount(1);
        _networkDiagnosticLastGen2Collections = GC.CollectionCount(2);
    }

    private (int Gen0Collections, int Gen1Collections, int Gen2Collections) ConsumeNetworkDiagnosticGcDeltas()
    {
        if (!_networkDiagnosticGcBaselineInitialized)
        {
            InitializeNetworkDiagnosticGcBaseline();
            return (0, 0, 0);
        }

        var currentGen0Collections = GC.CollectionCount(0);
        var currentGen1Collections = GC.CollectionCount(1);
        var currentGen2Collections = GC.CollectionCount(2);
        var deltaGen0Collections = currentGen0Collections - _networkDiagnosticLastGen0Collections;
        var deltaGen1Collections = currentGen1Collections - _networkDiagnosticLastGen1Collections;
        var deltaGen2Collections = currentGen2Collections - _networkDiagnosticLastGen2Collections;
        _networkDiagnosticLastGen0Collections = currentGen0Collections;
        _networkDiagnosticLastGen1Collections = currentGen1Collections;
        _networkDiagnosticLastGen2Collections = currentGen2Collections;
        return (deltaGen0Collections, deltaGen1Collections, deltaGen2Collections);
    }

    private double GetNetworkDiagnosticTimelineErrorMilliseconds()
    {
        if (!_hasRemotePlayerRenderTime)
        {
            return 0d;
        }

        var targetRenderTimeSeconds = GetSnapshotRenderTimeSeconds(_remotePlayerInterpolationBackTimeSeconds);
        return (targetRenderTimeSeconds - _remotePlayerRenderTimeSeconds) * 1000d;
    }

    private static double GetDiagnosticsElapsedMilliseconds(long startTimestamp)
    {
        return (Stopwatch.GetTimestamp() - startTimestamp) * 1000d / Stopwatch.Frequency;
    }
}
