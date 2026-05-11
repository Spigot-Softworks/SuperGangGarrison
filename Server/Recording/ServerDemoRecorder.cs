#nullable enable

using System;
using System.Globalization;
using System.IO;
using System.Text;
using OpenGarrison.Core;
using OpenGarrison.Protocol;

namespace OpenGarrison.Server;

internal sealed class ServerDemoRecorder : IDisposable
{
    private readonly Func<WelcomeMessage> _createWelcomeMessage;
    private readonly Func<SnapshotMessage> _captureCanonicalSnapshot;
    private readonly Func<string> _serverNameGetter;
    private readonly Func<string> _levelNameGetter;
    private readonly Action<string> _log;

    private OpenGarrisonDemoRecordingWriter? _writer;
    private string? _sessionStemPath;
    private int _segmentIndex;
    private ulong _segmentFirstFrame;
    private bool _segmentFirstFrameInitialized;
    private int _lastDueMilliseconds;
    private bool _sessionActive;
    private bool _captureErrorsSuppressed;

    public ServerDemoRecorder(
        Func<WelcomeMessage> createWelcomeMessage,
        Func<SnapshotMessage> captureCanonicalSnapshot,
        Func<string> serverNameGetter,
        Func<string> levelNameGetter,
        Action<string> log)
    {
        _createWelcomeMessage = createWelcomeMessage;
        _captureCanonicalSnapshot = captureCanonicalSnapshot;
        _serverNameGetter = serverNameGetter;
        _levelNameGetter = levelNameGetter;
        _log = log;
    }

    public bool IsActive => _sessionActive;

    public string GetStatusLine()
    {
        if (!_sessionActive)
        {
            return "[server] demo | status=idle";
        }

        if (_writer is null)
        {
            return "[server] demo | status=armed";
        }

        return
            $"[server] demo | status=recording | file={_writer.FinalPath} | segment={_segmentIndex} | " +
            $"messages={_writer.MessageCount} | bytes={_writer.PayloadByteCount}";
    }

    public bool TryStart(string? requestedPath, out string status, out string error)
    {
        status = string.Empty;
        error = string.Empty;
        if (_sessionActive)
        {
            error = "demo recording is already active.";
            return false;
        }

        try
        {
            _sessionStemPath = ResolveSessionStemPath(requestedPath);
            _segmentIndex = 0;
            _sessionActive = true;
            _captureErrorsSuppressed = false;
            StartNextSegment();
            status = $"[server] demo recording started: {_writer?.FinalPath ?? _sessionStemPath}";
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        {
            ResetSessionState(disposeWriter: true);
            error = ex.Message;
            return false;
        }
    }

    public bool TryStop(out string status, out string error)
    {
        status = string.Empty;
        error = string.Empty;
        if (!_sessionActive)
        {
            error = "demo recording is not active.";
            return false;
        }

        status = StopInternal(saveRecording: true, reason: "stopped");
        return true;
    }

    public void RecordSnapshot(SnapshotMessage snapshot)
    {
        if (!_sessionActive || _writer is null)
        {
            return;
        }

        try
        {
            AppendMessage(snapshot, ProtocolCodec.Serialize(snapshot));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            HandleRecordingFailure(ex);
        }
    }

    public void RecordBroadcastMessage(IProtocolMessage message)
    {
        if (!_sessionActive || _writer is null)
        {
            return;
        }

        try
        {
            AppendMessage(message, ProtocolCodec.Serialize(message));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            HandleRecordingFailure(ex);
        }
    }

    public void HandleMapTransition(MapChangeTransition transition)
    {
        if (!_sessionActive)
        {
            return;
        }

        var previousPath = _writer?.FinalPath;
        if (_writer is not null)
        {
            try
            {
                _writer.Complete();
                _log($"[server] demo segment saved: {previousPath}");
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException)
            {
                HandleRecordingFailure(ex);
                return;
            }
        }

        _writer = null;
        _segmentFirstFrame = 0;
        _segmentFirstFrameInitialized = false;
        _lastDueMilliseconds = 0;
        if (!_sessionActive)
        {
            return;
        }

        try
        {
            StartNextSegment();
            _log($"[server] demo recording rolled over to {_writer?.FinalPath}");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        {
            HandleRecordingFailure(ex);
        }
    }

    private string StopInternal(bool saveRecording, string reason)
    {
        if (_writer is null)
        {
            var stem = _sessionStemPath;
            ResetSessionState(disposeWriter: false);
            return saveRecording
                ? $"[server] demo recording stopped: no segment was active ({stem})"
                : $"[server] demo recording canceled ({stem})";
        }

        var finalPath = _writer.FinalPath;
        try
        {
            if (saveRecording)
            {
                _writer.Complete();
                var summary =
                    $"[server] demo recording {reason}: {finalPath} messages={_writer.MessageCount} bytes={_writer.PayloadByteCount}";
                ResetSessionState(disposeWriter: false);
                return summary;
            }

            _writer.Discard();
            ResetSessionState(disposeWriter: false);
            return $"[server] demo recording canceled: {finalPath}";
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            var failure = $"[server] demo recording failed while stopping: {ex.Message}";
            ResetSessionState(disposeWriter: true);
            return failure;
        }
    }

    private void StartNextSegment()
    {
        var sessionStemPath = _sessionStemPath ?? throw new InvalidOperationException("Demo session path was not initialized.");
        _segmentIndex += 1;
        var segmentPath = BuildSegmentPath(sessionStemPath, _segmentIndex, _levelNameGetter());
        _writer = new OpenGarrisonDemoRecordingWriter(segmentPath, $"demo:{_serverNameGetter().Trim()}");
        _segmentFirstFrame = 0;
        _segmentFirstFrameInitialized = false;
        _lastDueMilliseconds = 0;
        var welcome = _createWelcomeMessage();
        _writer.AppendMessage(0, ProtocolCodec.Serialize(welcome));
        var initialSnapshot = _captureCanonicalSnapshot();
        AppendMessage(initialSnapshot, ProtocolCodec.Serialize(initialSnapshot));
    }

    private void AppendMessage(IProtocolMessage message, byte[] payload)
    {
        if (_writer is null)
        {
            return;
        }

        var dueMilliseconds = ResolveDueMilliseconds(message);
        _writer.AppendMessage(dueMilliseconds, payload);
    }

    private int ResolveDueMilliseconds(IProtocolMessage message)
    {
        if (message is SnapshotMessage snapshot)
        {
            if (!_segmentFirstFrameInitialized)
            {
                _segmentFirstFrame = snapshot.Frame;
                _segmentFirstFrameInitialized = true;
                _lastDueMilliseconds = 0;
                return 0;
            }

            var effectiveTickRate = snapshot.TickRate > 0 ? snapshot.TickRate : 30;
            var frameDelta = snapshot.Frame > _segmentFirstFrame ? snapshot.Frame - _segmentFirstFrame : 0UL;
            var dueMilliseconds = (int)Math.Clamp(
                Math.Round(frameDelta * 1000d / effectiveTickRate, MidpointRounding.AwayFromZero),
                0d,
                int.MaxValue);
            _lastDueMilliseconds = Math.Max(_lastDueMilliseconds, dueMilliseconds);
            return _lastDueMilliseconds;
        }

        return _lastDueMilliseconds;
    }

    private string ResolveSessionStemPath(string? requestedPath)
    {
        if (!string.IsNullOrWhiteSpace(requestedPath))
        {
            var trimmed = requestedPath.Trim().Trim('"');
            var fullPath = Path.GetFullPath(trimmed);
            return string.Equals(Path.GetExtension(fullPath), ".ogdemo", StringComparison.OrdinalIgnoreCase)
                ? Path.Combine(Path.GetDirectoryName(fullPath) ?? RuntimePaths.ConfigDirectory, Path.GetFileNameWithoutExtension(fullPath))
                : fullPath;
        }

        var directory = RuntimePaths.ReplaysDirectory;
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss", CultureInfo.InvariantCulture);
        var serverName = SanitizePathSegment(_serverNameGetter());
        return Path.Combine(directory, $"{timestamp} {serverName}");
    }

    private static string BuildSegmentPath(string sessionStemPath, int segmentIndex, string levelName)
    {
        var directory = Path.GetDirectoryName(sessionStemPath) ?? RuntimePaths.ConfigDirectory;
        Directory.CreateDirectory(directory);
        var baseName = Path.GetFileName(sessionStemPath);
        var mapSegment = SanitizePathSegment(levelName);
        return Path.Combine(directory, $"{baseName} [{segmentIndex:00}] {mapSegment}.ogdemo");
    }

    private static string SanitizePathSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "session";
        }

        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Trim())
        {
            if (Array.IndexOf(invalid, character) >= 0)
            {
                builder.Append('_');
            }
            else
            {
                builder.Append(char.IsWhiteSpace(character) ? ' ' : character);
            }
        }

        var sanitized = builder.ToString().Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "session" : sanitized;
    }

    private void HandleRecordingFailure(Exception exception)
    {
        if (!_captureErrorsSuppressed)
        {
            _log($"[server] demo recording disabled after failure: {exception.Message}");
            _captureErrorsSuppressed = true;
        }

        ResetSessionState(disposeWriter: true);
    }

    private void ResetSessionState(bool disposeWriter)
    {
        if (disposeWriter)
        {
            try
            {
                _writer?.Dispose();
            }
            catch
            {
            }
        }

        _writer = null;
        _sessionStemPath = null;
        _segmentIndex = 0;
        _segmentFirstFrame = 0;
        _segmentFirstFrameInitialized = false;
        _lastDueMilliseconds = 0;
        _sessionActive = false;
    }

    public void Dispose()
    {
        ResetSessionState(disposeWriter: true);
    }
}
