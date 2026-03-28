#nullable enable

using System.Diagnostics;
using System.IO;
using System.Net.Sockets;

namespace OpenGarrison.Client;

internal sealed record HostedServerLaunchTarget(string FileName, string ArgumentsPrefix, string WorkingDirectory);

internal sealed record HostedServerLaunchOptions(
    string ConfigPath,
    string ServerName,
    int Port,
    int MaxPlayers,
    string Password,
    int TimeLimitMinutes,
    int CapLimit,
    int RespawnSeconds,
    bool LobbyAnnounce,
    bool AutoBalance);

internal static class HostedServerBootstrapper
{
    private const string ServerAssemblyName = "OpenGarrison.Server.dll";
    private const string ServerTargetFramework = "net10.0";

    public static bool IsUdpPortAvailable(int port)
    {
        try
        {
            using var probe = new UdpClient(port);
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    public static HostedServerLaunchTarget? FindLaunchTarget()
    {
        foreach (var candidate in EnumerateDirectAppHostCandidates())
        {
            if (File.Exists(candidate))
            {
                return new HostedServerLaunchTarget(
                    candidate,
                    string.Empty,
                    Path.GetDirectoryName(candidate) ?? AppContext.BaseDirectory);
            }
        }

        foreach (var candidate in EnumerateProbedAppHostCandidates())
        {
            if (File.Exists(candidate))
            {
                return new HostedServerLaunchTarget(
                    candidate,
                    string.Empty,
                    Path.GetDirectoryName(candidate) ?? AppContext.BaseDirectory);
            }
        }

        foreach (var candidate in EnumerateDirectAssemblyCandidates())
        {
            if (File.Exists(candidate))
            {
                return new HostedServerLaunchTarget(
                    "dotnet",
                    QuoteArgument(candidate),
                    Path.GetDirectoryName(candidate) ?? AppContext.BaseDirectory);
            }
        }

        foreach (var candidate in EnumerateProbedAssemblyCandidates())
        {
            if (File.Exists(candidate))
            {
                return new HostedServerLaunchTarget(
                    "dotnet",
                    QuoteArgument(candidate),
                    Path.GetDirectoryName(candidate) ?? AppContext.BaseDirectory);
            }
        }

        return null;
    }

    public static bool TryGetProcess(int processId, out Process? process)
    {
        process = null;
        try
        {
            process = Process.GetProcessById(processId);
            if (process.HasExited)
            {
                process.Dispose();
                process = null;
                return false;
            }

            return true;
        }
        catch
        {
            process?.Dispose();
            process = null;
            return false;
        }
    }

    public static string BuildLaunchArguments(HostedServerLaunchTarget launchTarget, HostedServerLaunchOptions options)
    {
        var arguments = new List<string>();
        if (!string.IsNullOrWhiteSpace(launchTarget.ArgumentsPrefix))
        {
            arguments.Add(launchTarget.ArgumentsPrefix);
        }

        arguments.Add($"--config {QuoteArgument(options.ConfigPath)}");

        if (options.Port > 0)
        {
            arguments.Add($"--port {options.Port}");
        }

        if (!string.IsNullOrWhiteSpace(options.ServerName))
        {
            arguments.Add($"--name {QuoteArgument(options.ServerName)}");
        }

        if (options.MaxPlayers > 0)
        {
            arguments.Add($"--max-players {options.MaxPlayers}");
        }

        if (!string.IsNullOrWhiteSpace(options.Password))
        {
            arguments.Add($"--password {QuoteArgument(options.Password)}");
        }

        if (options.TimeLimitMinutes > 0)
        {
            arguments.Add($"--time-limit {options.TimeLimitMinutes}");
        }

        if (options.CapLimit > 0)
        {
            arguments.Add($"--cap-limit {options.CapLimit}");
        }

        if (options.RespawnSeconds >= 0)
        {
            arguments.Add($"--respawn-seconds {options.RespawnSeconds}");
        }

        arguments.Add(options.LobbyAnnounce ? "--lobby" : "--no-lobby");
        arguments.Add(options.AutoBalance ? "--auto-balance" : "--no-auto-balance");
        return string.Join(' ', arguments);
    }

    private static IEnumerable<string> EnumerateDirectAppHostCandidates()
    {
        foreach (var directory in EnumerateDirectLaunchDirectories())
        {
            foreach (var fileName in GetAppHostFileNames())
            {
                yield return Path.Combine(directory, fileName);
            }
        }
    }

    private static IEnumerable<string> EnumerateDirectAssemblyCandidates()
    {
        foreach (var directory in EnumerateDirectLaunchDirectories())
        {
            yield return Path.Combine(directory, ServerAssemblyName);
        }
    }

    private static IEnumerable<string> EnumerateProbedAppHostCandidates()
    {
        foreach (var root in EnumerateProbeRoots())
        {
            foreach (var relativeDirectory in EnumerateRelativeServerOutputDirectories())
            {
                foreach (var fileName in GetAppHostFileNames())
                {
                    yield return Path.Combine(root, relativeDirectory, fileName);
                }
            }
        }
    }

    private static IEnumerable<string> EnumerateProbedAssemblyCandidates()
    {
        foreach (var root in EnumerateProbeRoots())
        {
            foreach (var relativeDirectory in EnumerateRelativeServerOutputDirectories())
            {
                yield return Path.Combine(root, relativeDirectory, ServerAssemblyName);
            }
        }
    }

    private static IEnumerable<string> EnumerateDirectLaunchDirectories()
    {
        yield return AppContext.BaseDirectory;
        yield return Directory.GetCurrentDirectory();
    }

    private static IEnumerable<string> EnumerateProbeRoots()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var probe in EnumerateDirectLaunchDirectories())
        {
            var directory = new DirectoryInfo(probe);
            while (directory is not null)
            {
                if (seen.Add(directory.FullName))
                {
                    yield return directory.FullName;
                }

                directory = directory.Parent;
            }
        }
    }

    private static IEnumerable<string> EnumerateRelativeServerOutputDirectories()
    {
        yield return Path.Combine("OpenGarrison.Server", "bin", "Debug", ServerTargetFramework);
        yield return Path.Combine("OpenGarrison.Server", "bin", "Release", ServerTargetFramework);
        yield return Path.Combine("src", "OpenGarrison.Server", "bin", "Debug", ServerTargetFramework);
        yield return Path.Combine("src", "OpenGarrison.Server", "bin", "Release", ServerTargetFramework);
        yield return Path.Combine("Source", "OpenGarrison.CSharp", "src", "OpenGarrison.Server", "bin", "Debug", ServerTargetFramework);
        yield return Path.Combine("Source", "OpenGarrison.CSharp", "src", "OpenGarrison.Server", "bin", "Release", ServerTargetFramework);
    }

    private static IReadOnlyList<string> GetAppHostFileNames()
    {
        return OperatingSystem.IsWindows()
            ? ["OpenGarrison.Server.exe", "OpenGarrison.Server"]
            : ["OpenGarrison.Server", "OpenGarrison.Server.exe"];
    }

    private static string QuoteArgument(string value)
    {
        return $"\"{value.Replace("\"", "\\\"")}\"";
    }
}
