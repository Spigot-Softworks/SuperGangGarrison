using System;
using System.Globalization;
using System.Threading;

namespace OpenGarrison.Server;

internal static class ServerConsoleCommandProcessor
{
    public static bool TryProcessLine(
        string? line,
        bool isInputRedirected,
        CancellationTokenSource shutdownCts,
        Action<string> writeLine,
        Action<string>? enqueueCommand = null)
    {
        if (line is null)
        {
            if (isInputRedirected && !shutdownCts.IsCancellationRequested)
            {
                writeLine("[server] stdin closed; command listener stopped.");
            }

            return false;
        }

        var command = line.Replace("\uFEFF", string.Empty, StringComparison.Ordinal).Trim();
        while (command.Length > 0 && (char.GetUnicodeCategory(command[0]) == UnicodeCategory.Format || command.StartsWith("ï»¿", StringComparison.Ordinal)))
        {
            command = command.StartsWith("ï»¿", StringComparison.Ordinal)
                ? command[3..].TrimStart()
                : command[1..].TrimStart();
        }
        if (command.Length == 0)
        {
            return true;
        }

        if (string.Equals(command, "shutdown", StringComparison.OrdinalIgnoreCase)
            || string.Equals(command, "exit", StringComparison.OrdinalIgnoreCase)
            || string.Equals(command, "quit", StringComparison.OrdinalIgnoreCase))
        {
            writeLine("[server] shutdown requested.");
            shutdownCts.Cancel();
            return false;
        }

        if (enqueueCommand is not null)
        {
            enqueueCommand(command);
            return true;
        }

        writeLine($"[server] unknown command \"{command}\". Type shutdown to stop.");
        return true;
    }
}
