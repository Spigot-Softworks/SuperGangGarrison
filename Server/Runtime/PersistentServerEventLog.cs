using System;
using System.Globalization;
using System.IO;
using System.Text;
using OpenGarrison.Core;

internal sealed class PersistentServerEventLog : IDisposable
{
    private readonly object _sync = new();
    private readonly Action<string>? _diagnostics;
    private StreamWriter? _writer;
    private bool _writeFailureReported;

    public PersistentServerEventLog(string filePath, Action<string>? diagnostics = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        _diagnostics = diagnostics;
        FilePath = Path.GetFullPath(filePath);

        try
        {
            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var stream = new FileStream(FilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            _writer = new StreamWriter(stream, Encoding.UTF8)
            {
                AutoFlush = true,
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            _writer = null;
            ReportDiagnostic($"[server] event log disabled path=\"{FilePath}\" error=\"{ex.Message}\"");
        }
    }

    public string FilePath { get; }

    public bool IsEnabled => _writer is not null;

    public static string GetDefaultPath(DateTimeOffset now)
    {
        return RuntimePaths.GetLogPath($"server-events-{now:yyyyMMdd}.log");
    }

    public void Write(string eventName, params (string Key, object? Value)[] fields)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        if (_writer is null)
        {
            return;
        }

        var line = BuildLine(DateTimeOffset.Now, eventName, fields);
        lock (_sync)
        {
            if (_writer is null)
            {
                return;
            }

            try
            {
                _writer.WriteLine(line);
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException)
            {
                if (!_writeFailureReported)
                {
                    _writeFailureReported = true;
                    ReportDiagnostic($"[server] event log write failed path=\"{FilePath}\" error=\"{ex.Message}\"");
                }

                _writer.Dispose();
                _writer = null;
            }
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _writer?.Dispose();
            _writer = null;
        }
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
