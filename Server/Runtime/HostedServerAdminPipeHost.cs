using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace OpenGarrison.Server;

internal sealed class HostedServerAdminPipeHost : IDisposable
{
    private const string EndMarker = "__END__";

    private readonly string _pipeName;
    private readonly Func<string, bool, CancellationToken, Task<IReadOnlyList<string>>> _executeCommandAsync;
    private readonly Action _requestShutdown;
    private readonly CancellationToken _shutdownToken;
    private readonly Task _listenTask;

    public HostedServerAdminPipeHost(
        string pipeName,
        Func<string, bool, CancellationToken, Task<IReadOnlyList<string>>> executeCommandAsync,
        Action requestShutdown,
        CancellationToken shutdownToken)
    {
        _pipeName = pipeName;
        _executeCommandAsync = executeCommandAsync;
        _requestShutdown = requestShutdown;
        _shutdownToken = shutdownToken;
        _listenTask = Task.Run(ListenAsync, CancellationToken.None);
    }

    public void Dispose()
    {
        try
        {
            _listenTask.Wait(250);
        }
        catch
        {
        }
    }

    private async Task ListenAsync()
    {
        while (!_shutdownToken.IsCancellationRequested)
        {
            NamedPipeServerStream? pipe = null;
            try
            {
                pipe = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);
                await pipe.WaitForConnectionAsync(_shutdownToken).ConfigureAwait(false);
                await HandleClientAsync(pipe, _shutdownToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
            }
            finally
            {
                pipe?.Dispose();
            }
        }
    }

    private async Task HandleClientAsync(NamedPipeServerStream pipe, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(pipe);
        using var writer = new StreamWriter(pipe)
        {
            AutoFlush = true,
        };

        var command = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(command))
        {
            await writer.WriteLineAsync(EndMarker).ConfigureAwait(false);
            return;
        }

        IReadOnlyList<string> responseLines;
        var trimmedCommand = command.Trim();
        if (string.Equals(trimmedCommand, "__ping", StringComparison.OrdinalIgnoreCase))
        {
            responseLines = new[] { "[server] admin pipe ok" };
        }
        else if (string.Equals(trimmedCommand, "__snapshot", StringComparison.OrdinalIgnoreCase))
        {
            var statusLines = await _executeCommandAsync("status", false, cancellationToken).ConfigureAwait(false);
            var rotationLines = await _executeCommandAsync("rotation", false, cancellationToken).ConfigureAwait(false);
            var merged = new List<string>();
            merged.AddRange(statusLines);
            merged.AddRange(rotationLines);
            responseLines = merged;
        }
        else if (string.Equals(trimmedCommand, "shutdown", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmedCommand, "quit", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmedCommand, "exit", StringComparison.OrdinalIgnoreCase))
        {
            responseLines = new[] { "[server] shutdown requested." };
            _requestShutdown();
        }
        else
        {
            responseLines = await _executeCommandAsync(trimmedCommand, true, cancellationToken).ConfigureAwait(false);
        }

        foreach (var line in responseLines)
        {
            await writer.WriteLineAsync(line).ConfigureAwait(false);
        }

        await writer.WriteLineAsync(EndMarker).ConfigureAwait(false);
    }
}
