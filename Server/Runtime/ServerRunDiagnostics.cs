using System.Text;

namespace OpenGarrison.Server;

internal sealed class ServerRunDiagnostics : IDisposable
{
    private readonly TextWriter _stdout = Console.Out;
    private readonly TextWriter _stderr = Console.Error;
    private readonly TextWriter _file;
    private string? _shutdownReason;
    public string DirectoryPath { get; }

    public ServerRunDiagnostics(string directory)
    {
        DirectoryPath = directory;
        Directory.CreateDirectory(directory);
        _file = TextWriter.Synchronized(new StreamWriter(Path.Combine(directory, "server-diagnostics.log"), append: true) { AutoFlush = true });
        Console.SetOut(new TeeWriter(_stdout, _file, "stdout"));
        Console.SetError(new TeeWriter(_stderr, _file, "stderr"));
        Console.WriteLine($"[server] process-start pid={Environment.ProcessId} instance={OpenGarrison.Core.HostedServerSessionInfo.GetCurrentInstanceId()} diagnostics=\"{directory}\"");
    }

    public void RequestShutdown(string reason)
    {
        Interlocked.CompareExchange(ref _shutdownReason, reason, null);
        Console.WriteLine($"[server] shutdown-cause={reason}");
    }

    public void Fatal(Exception exception)
    {
        _shutdownReason = "fatal-exception";
        Console.Error.WriteLine($"[server] fatal-exception pid={Environment.ProcessId}: {exception}");
    }

    public void Complete(int exitCode)
        => Console.WriteLine($"[server] process-exit pid={Environment.ProcessId} reason={_shutdownReason ?? "server-run-returned"} exitCode={exitCode}");

    public void Dispose()
    {
        Console.SetOut(_stdout);
        Console.SetError(_stderr);
        _file.Dispose();
    }

    private sealed class TeeWriter(TextWriter console, TextWriter file, string stream) : TextWriter
    {
        public override Encoding Encoding => console.Encoding;
        public override void Write(char value) { console.Write(value); file.Write(value); }
        public override void Write(string? value) { console.Write(value); file.Write(value); }
        public override void WriteLine(string? value)
        {
            console.WriteLine(value);
            file.WriteLine($"{DateTimeOffset.UtcNow:O} [{stream}] {value}");
        }
        public override void Flush() { console.Flush(); file.Flush(); }
    }
}
