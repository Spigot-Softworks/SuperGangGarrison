#nullable enable

using System.IO.Pipes;
using System.Text;

namespace OpenGarrison.Client;

internal static class HostedServerAdminClient
{
    public static bool TrySendCommand(string pipeName, string command, out List<string> responseLines, out string error)
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
            using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.None);
            pipe.Connect(1000);
            using var writer = new StreamWriter(pipe, Encoding.UTF8, bufferSize: 1024, leaveOpen: true)
            {
                AutoFlush = true,
            };
            using var reader = new StreamReader(pipe, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
            writer.WriteLine(command);
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                if (string.Equals(line, "__END__", StringComparison.Ordinal))
                {
                    break;
                }

                responseLines.Add(line);
            }

            return true;
        }
        catch (Exception ex)
        {
            error = $"Dedicated server control channel failed: {ex.Message}";
            return false;
        }
    }
}
