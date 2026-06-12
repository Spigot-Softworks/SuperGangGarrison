using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using OpenGarrison.Core;

internal sealed class PersistentServerEventLog : IDisposable
{
    public const string DisabledPath = "disabled";
    private const int QueueCapacity = 8192;
    private const int FlushBatchSize = 256;
    private const int FlushIntervalMilliseconds = 500;
    private const int DisposeDrainMilliseconds = 2000;

    private readonly Action<string>? _diagnostics;
    private readonly Channel<string>? _queue;
    private readonly Task? _writerTask;
    private int _droppedWriteCount;
    private volatile bool _enabled;
    private volatile bool _writeFailureReported;

    public PersistentServerEventLog(string filePath, Action<string>? diagnostics = null)
    {
        _diagnostics = diagnostics;

        if (IsDisabledPath(filePath))
        {
            FilePath = DisabledPath;
            ReportDiagnostic("[server] event log disabled.");
            return;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        FilePath = Path.GetFullPath(filePath);

        try
        {
            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var stream = new FileStream(FilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            var writer = new StreamWriter(stream, Encoding.UTF8)
            {
                AutoFlush = false,
            };
            _queue = Channel.CreateBounded<string>(new BoundedChannelOptions(QueueCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false,
            });
            _enabled = true;
            _writerTask = Task.Run(() => ProcessWritesAsync(writer, _queue.Reader));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            _enabled = false;
            ReportDiagnostic($"[server] event log disabled path=\"{FilePath}\" error=\"{ex.Message}\"");
        }
    }

    public string FilePath { get; }

    public bool IsEnabled => _enabled && _queue is not null;

    public static string GetDefaultPath(DateTimeOffset now)
    {
        return RuntimePaths.GetLogPath($"server-events-{now:yyyyMMdd}.log");
    }

    public static bool IsDisabledPath(string? filePath)
    {
        return string.IsNullOrWhiteSpace(filePath)
            || string.Equals(filePath, DisabledPath, StringComparison.OrdinalIgnoreCase)
            || string.Equals(filePath, "none", StringComparison.OrdinalIgnoreCase)
            || string.Equals(filePath, "off", StringComparison.OrdinalIgnoreCase);
    }

    public void Write(string eventName, params (string Key, object? Value)[] fields)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        if (!IsEnabled || _queue is null)
        {
            return;
        }

        var line = BuildLine(DateTimeOffset.Now, eventName, fields);
        if (!_queue.Writer.TryWrite(line))
        {
            Interlocked.Increment(ref _droppedWriteCount);
        }
    }

    public void Dispose()
    {
        if (_queue is null)
        {
            return;
        }

        _enabled = false;
        _queue.Writer.TryComplete();
        try
        {
            _writerTask?.Wait(DisposeDrainMilliseconds);
        }
        catch (AggregateException ex) when (ex.InnerExceptions.All(static inner => inner is IOException or ObjectDisposedException))
        {
        }
    }

    private async Task ProcessWritesAsync(StreamWriter writer, ChannelReader<string> reader)
    {
        var pendingSinceFlush = 0;
        var lastFlushTimestamp = DateTimeOffset.UtcNow;
        try
        {
            while (await WaitForLogWorkAsync(reader).ConfigureAwait(false))
            {
                while (reader.TryRead(out var line))
                {
                    writer.WriteLine(line);
                    pendingSinceFlush += 1;

                    if (pendingSinceFlush >= FlushBatchSize)
                    {
                        WriteDroppedSummaryIfNeeded(writer);
                        writer.Flush();
                        pendingSinceFlush = 0;
                        lastFlushTimestamp = DateTimeOffset.UtcNow;
                    }
                }

                if (pendingSinceFlush > 0
                    && (DateTimeOffset.UtcNow - lastFlushTimestamp).TotalMilliseconds >= FlushIntervalMilliseconds)
                {
                    WriteDroppedSummaryIfNeeded(writer);
                    writer.Flush();
                    pendingSinceFlush = 0;
                    lastFlushTimestamp = DateTimeOffset.UtcNow;
                }
            }

            while (reader.TryRead(out var line))
            {
                writer.WriteLine(line);
                pendingSinceFlush += 1;
            }

            WriteDroppedSummaryIfNeeded(writer);
            if (pendingSinceFlush > 0)
            {
                writer.Flush();
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            ReportWriteFailure(ex);
        }
        finally
        {
            _enabled = false;
            try
            {
                writer.Flush();
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException)
            {
                ReportWriteFailure(ex);
            }

            writer.Dispose();
        }
    }

    private static async Task<bool> WaitForLogWorkAsync(ChannelReader<string> reader)
    {
        var waitForData = reader.WaitToReadAsync().AsTask();
        var flushDelay = Task.Delay(FlushIntervalMilliseconds);
        var completed = await Task.WhenAny(waitForData, flushDelay).ConfigureAwait(false);
        return completed == flushDelay || await waitForData.ConfigureAwait(false);
    }

    private void WriteDroppedSummaryIfNeeded(StreamWriter writer)
    {
        var dropped = Interlocked.Exchange(ref _droppedWriteCount, 0);
        if (dropped <= 0)
        {
            return;
        }

        writer.WriteLine(BuildLine(
            DateTimeOffset.Now,
            "event_log_dropped",
            ("dropped_count", dropped),
            ("reason", "queue_full")));
    }

    private void ReportWriteFailure(Exception ex)
    {
        if (_writeFailureReported)
        {
            return;
        }

        _writeFailureReported = true;
        ReportDiagnostic($"[server] event log write failed path=\"{FilePath}\" error=\"{ex.Message}\"");
    }

    internal static string BuildLine(DateTimeOffset timestamp, string eventName, params (string Key, object? Value)[] fields)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);

        var builder = new StringBuilder();
        builder.Append("timestamp=");
        AppendFormattedValue(builder, timestamp);
        builder.Append(' ');
        builder.Append("event=");
        AppendFormattedValue(builder, eventName);

        for (var index = 0; index < fields.Length; index += 1)
        {
            var (key, value) = fields[index];
            if (string.IsNullOrWhiteSpace(key) || value is null)
            {
                continue;
            }

            builder.Append(' ');
            builder.Append(key);
            builder.Append('=');
            AppendFormattedValue(builder, value);
        }

        return builder.ToString();
    }

    private void ReportDiagnostic(string message)
    {
        try
        {
            _diagnostics?.Invoke(message);
        }
        catch
        {
        }
    }

    private static void AppendFormattedValue(StringBuilder builder, object value)
    {
        switch (value)
        {
            case bool boolValue:
                builder.Append(boolValue ? "true" : "false");
                return;
            case byte or sbyte or short or ushort or int or uint or long or ulong:
                builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            case float floatValue:
                builder.Append(floatValue.ToString("0.###", CultureInfo.InvariantCulture));
                return;
            case double doubleValue:
                builder.Append(doubleValue.ToString("0.###", CultureInfo.InvariantCulture));
                return;
            case decimal decimalValue:
                builder.Append(decimalValue.ToString(CultureInfo.InvariantCulture));
                return;
            case DateTimeOffset timestamp:
                AppendQuoted(builder, timestamp.ToString("O", CultureInfo.InvariantCulture));
                return;
            case DateTime dateTime:
                AppendQuoted(builder, dateTime.ToString("O", CultureInfo.InvariantCulture));
                return;
            default:
                AppendQuoted(builder, Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
                return;
        }
    }

    private static void AppendQuoted(StringBuilder builder, string value)
    {
        builder.Append('"');
        for (var index = 0; index < value.Length; index += 1)
        {
            builder.Append(value[index] switch
            {
                '\\' => "\\\\",
                '"' => "\\\"",
                '\r' => "\\r",
                '\n' => "\\n",
                _ => value[index].ToString(),
            });
        }

        builder.Append('"');
    }
}
