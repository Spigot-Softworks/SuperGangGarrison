#nullable enable

using System;
using System.IO.Pipes;
using System.Text;
using System.Threading;

namespace OpenGarrison.Client;

internal static class HostedServerAdminClient
{
    internal const int DefaultTimeoutMilliseconds = 3000;
    internal const int ShutdownTimeoutMilliseconds = 1000;

    public static bool TrySendCommand(
        string pipeName,
        string command,
        out List<string> responseLines,
        out string error,
        int timeoutMilliseconds = DefaultTimeoutMilliseconds)
    {
        responseLines = new List<string>();
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(pipeName))
        {
            error = "Dedicated server control channel is unavailable.";
            return false;
        }

        try
        {
            timeoutMilliseconds = Math.Clamp(timeoutMilliseconds, 100, 30_000);
            using var cancellation = new CancellationTokenSource(timeoutMilliseconds);
            using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.None);
            pipe.ConnectAsync(1000, cancellation.Token).GetAwaiter().GetResult();
            using var writer = new StreamWriter(pipe, Encoding.UTF8, bufferSize: 1024, leaveOpen: true)
            {
                AutoFlush = true,
            };
            using var reader = new StreamReader(pipe, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
            writer.WriteLineAsync(command.AsMemory(), cancellation.Token).GetAwaiter().GetResult();
            while (true)
            {
                var line = reader.ReadLineAsync(cancellation.Token).AsTask().GetAwaiter().GetResult();
                if (line is null)
                {
                    break;
                }

                if (string.Equals(line, "__END__", StringComparison.Ordinal))
                {
                    break;
                }

                responseLines.Add(line);
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            error = $"Dedicated server control channel timed out after {timeoutMilliseconds} ms.";
            return false;
        }
        catch (Exception ex)
        {
            error = $"Dedicated server control channel failed: {ex.Message}";
            return false;
        }
    }
}
