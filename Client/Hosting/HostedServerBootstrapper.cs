#nullable enable

using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using OpenGarrison.Bootstrap;
using OpenGarrison.Core;
using OpenGarrison.Core.LastToDie;

namespace OpenGarrison.Client;

internal sealed record HostedServerLaunchTarget(string FileName, string ArgumentsPrefix, string WorkingDirectory);
internal sealed record HostedServerProcessLogPaths(string StdOutPath, string StdErrPath);

internal sealed record HostedServerLaunchOptions(
    string ConfigPath,
    string ServerName,
    int Port,
    int MaxPlayers,
    string Password,
    string RconPassword,
    int TimeLimitMinutes,
    int CapLimit,
    int RespawnSeconds,
    bool LobbyAnnounce,
    bool AutoBalance,
    bool SecondaryAbilitiesEnabled,
    string? RequestedMap,
    string? MapRotationFile,
    GameplayVariantKind GameplayVariant = GameplayVariantKind.Standard,
    LastToDieDifficulty LastToDieDifficulty = LastToDieDifficulty.Standard,
    ulong? LastToDieSeed = null,
    string RelayHostUrl = "",
    string ManagementConfigPath = "")
{
    public static HostedServerLaunchOptions CreateLastToDie(
        string configPath,
        string serverName,
        int port,
        LastToDieDifficulty difficulty,
        ulong? seed = null,
        int maxPlayers = 4)
        => new(
            configPath,
            serverName,
            port,
            MaxPlayers: Math.Clamp(maxPlayers, 1, 4),
            Password: string.Empty,
            RconPassword: string.Empty,
            TimeLimitMinutes: 30,
            CapLimit: 5,
            RespawnSeconds: 5,
            // Private co-op is discovered through authenticated friend
            // presence and its short-lived relay, not the public server list.
            LobbyAnnounce: false,
            AutoBalance: false,
            SecondaryAbilitiesEnabled: true,
            RequestedMap: null,
            MapRotationFile: null,
            GameplayVariantKind.LastToDie,
            difficulty,
            seed);
}

internal static class HostedServerBootstrapper
{
    private const string PreferredServerAssemblyName = "OG2.Server.dll";
    private const string LegacyServerAssemblyName = "OpenGarrison.Server.dll";
    private const string ServerTargetFramework = "net10.0";
    private const string AspNetCoreFrameworkName = "Microsoft.AspNetCore.App";
    private static readonly string PackagedServerPluginsRelativePath = Path.Combine("Plugins", "Packaged", "Server");
    private const string HostedServerStdOutLogFileName = "hosted-server-stdout.log";
    private const string HostedServerStdErrLogFileName = "hosted-server-stderr.log";

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

    public static bool IsTcpPortAvailable(int port)
    {
        TcpListener? probe = null;
        try
        {
            probe = new TcpListener(System.Net.IPAddress.Loopback, port);
            probe.Start();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
        finally
        {
            probe?.Stop();
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

    public static bool TryPrepareRuntimePlugins(HostedServerLaunchTarget launchTarget, out string error)
    {
        ArgumentNullException.ThrowIfNull(launchTarget);

        error = string.Empty;
        var packagedPluginsSource = FindPackagedServerPluginsSource();
        if (string.IsNullOrWhiteSpace(packagedPluginsSource) || !Directory.Exists(packagedPluginsSource))
        {
            return true;
        }

        var runtimePluginsDestination = Path.Combine(launchTarget.WorkingDirectory, "Plugins", "Server");
        try
        {
            Directory.CreateDirectory(runtimePluginsDestination);
            foreach (var pluginDirectory in Directory.GetDirectories(packagedPluginsSource))
            {
                var pluginFolderName = Path.GetFileName(pluginDirectory);
                if (string.IsNullOrWhiteSpace(pluginFolderName))
                {
                    continue;
                }

                var pluginDestination = Path.Combine(runtimePluginsDestination, pluginFolderName);
                if (Directory.Exists(pluginDestination))
                {
                    Directory.Delete(pluginDestination, recursive: true);
                }

                CopyDirectory(pluginDirectory, pluginDestination);
            }

            return true;
        }
        catch (Exception ex)
        {
            error = $"Failed to mirror packaged server plugins: {ex.Message}";
            return false;
        }
    }

    public static ProcessStartInfo BuildStartInfo(HostedServerLaunchTarget target, HostedServerLaunchOptions options)
    {
        var info = new ProcessStartInfo(target.FileName) { UseShellExecute = false, WorkingDirectory = target.WorkingDirectory };
        // FindLaunchTarget's only prefix is the quoted managed entry-point path.
        if (!string.IsNullOrWhiteSpace(target.ArgumentsPrefix))
            info.ArgumentList.Add(target.ArgumentsPrefix.Trim().Trim('"'));
        void Add(string key, object value) { info.ArgumentList.Add(key); info.ArgumentList.Add(value.ToString()!); }
        Add("--config", options.ConfigPath);
        if (!string.IsNullOrWhiteSpace(options.ManagementConfigPath)) Add("--management-config", options.ManagementConfigPath);
        if (options.GameplayVariant == GameplayVariantKind.LastToDie)
        {
            Add("--gameplay-variant", "last-to-die");
            Add("--last-to-die-difficulty", options.LastToDieDifficulty.ToString().ToLowerInvariant());
            if (options.LastToDieSeed is { } seed) Add("--last-to-die-seed", seed);
        }
        if (options.Port > 0) Add("--port", options.Port);
        if (!string.IsNullOrWhiteSpace(options.ServerName)) Add("--name", options.ServerName);
        if (options.MaxPlayers > 0) Add("--max-players", options.MaxPlayers);
        if (!string.IsNullOrWhiteSpace(options.Password)) Add("--password", options.Password);
        if (!string.IsNullOrWhiteSpace(options.RconPassword)) Add("--rcon-password", options.RconPassword);
        if (!string.IsNullOrWhiteSpace(options.RequestedMap)) Add("--map", options.RequestedMap);
        if (!string.IsNullOrWhiteSpace(options.MapRotationFile)) Add("--map-rotation", options.MapRotationFile);
        if (options.TimeLimitMinutes > 0) Add("--time-limit", options.TimeLimitMinutes);
        if (options.CapLimit > 0) Add("--cap-limit", options.CapLimit);
        if (options.RespawnSeconds >= 0) Add("--respawn-seconds", options.RespawnSeconds);
        info.ArgumentList.Add(options.LobbyAnnounce ? "--lobby" : "--no-lobby");
        info.ArgumentList.Add(options.AutoBalance ? "--auto-balance" : "--no-auto-balance");
        info.ArgumentList.Add(options.SecondaryAbilitiesEnabled ? "--special-abilities" : "--no-special-abilities");
        return info;
    }

    public static string BuildLaunchArguments(HostedServerLaunchTarget target, HostedServerLaunchOptions options)
    {
        var arguments = BuildStartInfo(target, options).ArgumentList;
        return string.Join(" ", arguments.Select((argument, index) =>
            index == 0 && !argument.StartsWith("--", StringComparison.Ordinal)
                || index > 0 && arguments[index - 1] is "--config" or "--management-config" or "--name" or "--password" or "--rcon-password" or "--map" or "--map-rotation"
                ? DedicatedServerTerminalLauncher.QuoteWindowsArgument(argument) : argument));
    }

    public static bool TryGetProcess(HostedServerSessionInfo session, out Process? process)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (!TryGetProcess(session.ProcessId, out process) || process is null)
        {
            return false;
        }

        if (session.ProcessStartTimeUtcTicks > 0
            && !new HostedServerProcessIdentity(
                session.ProcessId,
                session.ProcessStartTimeUtcTicks).Matches(process))
        {
            process.Dispose();
            process = null;
            return false;
        }

        return true;
    }

    public static bool TryValidateRuntimePrerequisites(
        HostedServerLaunchTarget launchTarget,
        out string error)
    {
        ArgumentNullException.ThrowIfNull(launchTarget);
        error = string.Empty;

        // Shipped Linux/macOS servers are self-contained. Windows packages are
        // framework-dependent and need the ASP.NET Core shared framework.
        if (!OperatingSystem.IsWindows())
        {
            return true;
        }

        var runtimeConfigPath = FindServerRuntimeConfigPath(launchTarget.WorkingDirectory);
        if (runtimeConfigPath is null
            || !DotNetRuntimePrerequisite.TryReadFrameworkRequirement(
                runtimeConfigPath,
                AspNetCoreFrameworkName,
                out var requirement))
        {
            return true;
        }

        if (!DotNetRuntimePrerequisite.TryQueryInstalledRuntimes(out var installedRuntimes, out var queryError))
        {
            error = "Could not verify the installed ASP.NET Core runtime"
                + (string.IsNullOrWhiteSpace(queryError) ? "." : $": {queryError}");
            return false;
        }

        if (DotNetRuntimePrerequisite.IsFrameworkAvailable(installedRuntimes, requirement))
        {
            return true;
        }

        var downloadUrl = DotNetRuntimePrerequisite.GetDownloadUrl(requirement);
        error = $"ASP.NET Core Runtime {requirement.VersionFamily} (x64) is required to start the local server, "
            + $"but no compatible {requirement.FrameworkName} {requirement.VersionFamily}.x runtime was found. "
            + $"Install it from {downloadUrl}, then restart Super Gang Garrison.";
        return false;
    }

    public static HostedServerProcessLogPaths PrepareProcessLogFiles(string? instanceDirectory = null)
    {
        var logsDirectory = instanceDirectory ?? Path.Combine(OpenGarrison.Core.RuntimePaths.ConfigDirectory, "logs", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(logsDirectory);
        var stdoutPath = Path.Combine(logsDirectory, HostedServerStdOutLogFileName);
        var stderrPath = Path.Combine(logsDirectory, HostedServerStdErrLogFileName);
        File.WriteAllText(stdoutPath, string.Empty);
        File.WriteAllText(stderrPath, string.Empty);
        return new HostedServerProcessLogPaths(stdoutPath, stderrPath);
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
            foreach (var fileName in GetAssemblyFileNames())
            {
                yield return Path.Combine(directory, fileName);
            }
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
                foreach (var fileName in GetAssemblyFileNames())
                {
                    yield return Path.Combine(root, relativeDirectory, fileName);
                }
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
        yield return Path.Combine("Server", "bin", "Debug", ServerTargetFramework);
        yield return Path.Combine("Server", "bin", "Release", ServerTargetFramework);
        yield return Path.Combine("OpenGarrison.Server", "bin", "Debug", ServerTargetFramework);
        yield return Path.Combine("OpenGarrison.Server", "bin", "Release", ServerTargetFramework);
        yield return Path.Combine("src", "Server", "bin", "Debug", ServerTargetFramework);
        yield return Path.Combine("src", "Server", "bin", "Release", ServerTargetFramework);
        yield return Path.Combine("src", "OpenGarrison.Server", "bin", "Debug", ServerTargetFramework);
        yield return Path.Combine("src", "OpenGarrison.Server", "bin", "Release", ServerTargetFramework);
        yield return Path.Combine("Source", "OpenGarrison.CSharp", "src", "Server", "bin", "Debug", ServerTargetFramework);
        yield return Path.Combine("Source", "OpenGarrison.CSharp", "src", "Server", "bin", "Release", ServerTargetFramework);
        yield return Path.Combine("Source", "OpenGarrison.CSharp", "src", "OpenGarrison.Server", "bin", "Debug", ServerTargetFramework);
        yield return Path.Combine("Source", "OpenGarrison.CSharp", "src", "OpenGarrison.Server", "bin", "Release", ServerTargetFramework);
    }

    private static IReadOnlyList<string> GetAppHostFileNames()
    {
        return OperatingSystem.IsWindows()
            ? ["OG2.Server.exe", "OG2.Server", "OpenGarrison.Server.exe", "OpenGarrison.Server"]
            : ["OG2.Server", "OG2.Server.exe", "OpenGarrison.Server", "OpenGarrison.Server.exe"];
    }

    private static IReadOnlyList<string> GetAssemblyFileNames()
    {
        return [PreferredServerAssemblyName, LegacyServerAssemblyName];
    }

    private static string? FindPackagedServerPluginsSource()
    {
        foreach (var root in EnumerateProbeRoots())
        {
            var candidate = Path.Combine(root, PackagedServerPluginsRelativePath);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string? FindServerRuntimeConfigPath(string workingDirectory)
    {
        foreach (var fileName in new[]
                 {
                     "OG2.Server.runtimeconfig.json",
                     "OpenGarrison.Server.runtimeconfig.json",
                 })
        {
            var candidate = Path.Combine(workingDirectory, fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (var filePath in Directory.GetFiles(sourceDirectory))
        {
            var destinationPath = Path.Combine(destinationDirectory, Path.GetFileName(filePath));
            File.Copy(filePath, destinationPath, overwrite: true);
        }

        foreach (var directoryPath in Directory.GetDirectories(sourceDirectory))
        {
            var destinationPath = Path.Combine(destinationDirectory, Path.GetFileName(directoryPath));
            CopyDirectory(directoryPath, destinationPath);
        }
    }

    private static string QuoteArgument(string value)
    {
        return $"\"{value.Replace("\"", "\\\"")}\"";
    }
}
