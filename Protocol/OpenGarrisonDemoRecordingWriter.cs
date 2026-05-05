#nullable enable

using System;
using System.IO;
using System.Text;

namespace OpenGarrison.Protocol;

public sealed class OpenGarrisonDemoRecordingWriter : IDisposable
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("OGDM");
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private readonly string _finalPath;
    private readonly string _temporaryPath;
    private readonly FileStream _stream;
    private readonly BinaryWriter _writer;
    private readonly long _messageCountOffset;
    private bool _completed;
    private bool _disposed;

    public OpenGarrisonDemoRecordingWriter(
        string path,
        string remoteDescription,
        string completionReason = "Demo ended.")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _finalPath = Path.GetFullPath(path.Trim().Trim('"'));
        _temporaryPath = _finalPath + ".recording";

        var directory = Path.GetDirectoryName(_finalPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (File.Exists(_temporaryPath))
        {
            File.Delete(_temporaryPath);
        }

        _stream = new FileStream(_temporaryPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        _writer = new BinaryWriter(_stream, Utf8, leaveOpen: true);
        WriteHeader(remoteDescription, completionReason);
        _messageCountOffset = _stream.Position;
        _writer.Write(0);
    }

    public string FinalPath => _finalPath;

    public int MessageCount { get; private set; }

    public long PayloadByteCount { get; private set; }

    public void AppendMessage(int dueMilliseconds, byte[] payload)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(payload);
        if (_completed)
        {
            throw new InvalidOperationException("Demo recording has already been completed.");
        }

        if (dueMilliseconds < 0)
        {
            throw new InvalidDataException("Demo messages cannot be scheduled before time zero.");
        }

        _writer.Write(dueMilliseconds);
        _writer.Write(payload.Length);
        _writer.Write(payload);
        MessageCount += 1;
        PayloadByteCount += payload.Length;
    }

    public void Complete()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_completed)
        {
            return;
        }

        _writer.Flush();
        _stream.Position = _messageCountOffset;
        _writer.Write(MessageCount);
        _writer.Flush();
        _stream.Flush(flushToDisk: true);
        _writer.Dispose();
        _stream.Dispose();
        File.Move(_temporaryPath, _finalPath, overwrite: true);
        _completed = true;
        _disposed = true;
    }

    public void Discard()
    {
        if (_disposed)
        {
            return;
        }

        _writer.Dispose();
        _stream.Dispose();
        if (File.Exists(_temporaryPath))
        {
            File.Delete(_temporaryPath);
        }

        _disposed = true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Discard();
    }

    private void WriteHeader(string remoteDescription, string completionReason)
    {
        _writer.Write(Magic);
        _writer.Write(OpenGarrisonDemoFile.CurrentVersion);
        WriteString(remoteDescription ?? string.Empty, maxBytes: 256, "remote description");
        WriteString(completionReason ?? string.Empty, maxBytes: 256, "completion reason");
    }

    private void WriteString(string value, int maxBytes, string fieldName)
    {
        var bytes = Utf8.GetBytes(value);
        if (bytes.Length > ushort.MaxValue || bytes.Length > maxBytes)
        {
            throw new InvalidDataException($"Demo {fieldName} exceeded the configured byte limit.");
        }

        _writer.Write((ushort)bytes.Length);
        _writer.Write(bytes);
    }
}
