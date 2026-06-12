using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace OpenGarrison.Core;

public static class ApplicationBuildInfo
{
    public const string VersionFileName = "version.txt";
    public const string ReleaseChannelFileName = "release-channel.txt";
    public const string DefaultBuildVersion = "dev";
    public const string DefaultReleaseChannel = "stable";

    public static string BuildVersion => ResolveBuildVersion();

    public static string ReleaseChannel => ResolveReleaseChannel();

    public static string ResolveBuildVersion(string? overrideValue = null)
    {
        if (TryNormalizeBuildVersion(overrideValue, out var overrideVersion))
        {
            return overrideVersion;
        }

        if (TryNormalizeBuildVersion(Environment.GetEnvironmentVariable("OPENGARRISON_BUILD_VERSION"), out var envVersion))
        {
            return envVersion;
        }

        foreach (var candidate in EnumerateBuildVersionCandidates())
        {
            if (TryNormalizeBuildVersion(candidate, out var version)
                && !IsDefaultSdkVersionLabel(version))
            {
                return version;
            }
        }

        return DefaultBuildVersion;
    }

    public static string ResolveReleaseChannel(string? overrideValue = null)
    {
        if (TryNormalizeReleaseChannel(overrideValue, out var overrideChannel))
        {
            return overrideChannel;
        }

        if (TryNormalizeReleaseChannel(Environment.GetEnvironmentVariable("OPENGARRISON_RELEASE_CHANNEL"), out var envChannel))
        {
            return envChannel;
        }

        if (!OperatingSystem.IsBrowser())
        {
            foreach (var channelPath in EnumerateProbeFilePaths(ReleaseChannelFileName))
            {
                if (TryReadTextFile(channelPath, out var channel)
                    && TryNormalizeReleaseChannel(channel, out var fileChannel))
                {
                    return fileChannel;
                }
            }
        }

        return DefaultReleaseChannel;
    }

    public static string ResolveCompatibilityKey(int protocolVersion, string? buildVersion = null, string? releaseChannel = null, string? overrideValue = null)
    {
        if (TryNormalizeCompatibilityKey(overrideValue, out var overrideKey))
        {
            return overrideKey;
        }

        if (TryNormalizeCompatibilityKey(Environment.GetEnvironmentVariable("OPENGARRISON_COMPATIBILITY_KEY"), out var envKey))
        {
            return envKey;
        }

        return CreateCompatibilityKey(protocolVersion, buildVersion, releaseChannel);
    }

    public static string CreateCompatibilityKey(int protocolVersion, string? buildVersion = null, string? releaseChannel = null)
    {
        var normalizedChannel = NormalizeReleaseChannel(releaseChannel);
        var normalizedVersion = NormalizeBuildVersion(buildVersion);
        return $"{normalizedChannel}:{normalizedVersion}:{Math.Max(0, protocolVersion)}";
    }

    public static string NormalizeBuildVersion(string? value)
    {
        return TryNormalizeBuildVersion(value, out var version)
            ? version
            : DefaultBuildVersion;
    }

    public static string NormalizeReleaseChannel(string? value)
    {
        return TryNormalizeReleaseChannel(value, out var channel)
            ? channel
            : DefaultReleaseChannel;
    }

    public static string NormalizeCompatibilityKey(string? value)
    {
        return TryNormalizeCompatibilityKey(value, out var key)
            ? key
            : string.Empty;
    }

    private static IEnumerable<string> EnumerateBuildVersionCandidates()
    {
        if (!OperatingSystem.IsBrowser())
        {
            foreach (var versionPath in EnumerateProbeFilePaths(VersionFileName))
            {
                if (TryReadTextFile(versionPath, out var version))
                {
                    yield return version;
                }
            }

            var processPath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(processPath))
            {
                var productVersion = FileVersionInfo.GetVersionInfo(processPath).ProductVersion;
                if (!string.IsNullOrWhiteSpace(productVersion))
                {
                    yield return productVersion;
                }
            }
        }

        var assembly = typeof(ApplicationBuildInfo).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            yield return informationalVersion;
        }

        var assemblyVersion = assembly.GetName().Version?.ToString();
        if (!string.IsNullOrWhiteSpace(assemblyVersion))
        {
            yield return assemblyVersion;
        }
    }

    private static IEnumerable<string> EnumerateProbeFilePaths(string fileName)
    {
        var directories = new List<string>();
        AddProbeDirectory(directories, AppContext.BaseDirectory);
        AddProbeDirectory(directories, RuntimePaths.ApplicationRoot);
        AddProbeDirectory(directories, Path.GetDirectoryName(Environment.ProcessPath));
        AddProbeDirectory(directories, Directory.GetCurrentDirectory());

        foreach (var directory in directories)
        {
            var current = directory;
            for (var depth = 0; depth < 4 && !string.IsNullOrWhiteSpace(current); depth += 1)
            {
                yield return Path.Combine(current, fileName);
                current = Directory.GetParent(current)?.FullName ?? string.Empty;
            }
        }
    }

    private static void AddProbeDirectory(List<string> directories, string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        try
        {
            var fullPath = Path.GetFullPath(directory);
            if (!directories.Contains(fullPath, StringComparer.OrdinalIgnoreCase))
            {
                directories.Add(fullPath);
            }
        }
        catch (ArgumentException)
        {
        }
        catch (NotSupportedException)
        {
        }
    }

    private static bool TryReadTextFile(string path, out string text)
    {
        text = string.Empty;
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            text = File.ReadAllText(path);
            return !string.IsNullOrWhiteSpace(text);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool TryNormalizeBuildVersion(string? value, out string version)
    {
        version = value?.Trim() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(version);
    }

    private static bool TryNormalizeReleaseChannel(string? value, out string channel)
    {
        channel = value?.Trim().ToLowerInvariant() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(channel))
        {
            return false;
        }

        Span<char> sanitized = stackalloc char[Math.Min(channel.Length, 32)];
        var length = 0;
        foreach (var ch in channel)
        {
            if (length >= sanitized.Length)
            {
                break;
            }

            if (char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_' or '.')
            {
                sanitized[length] = ch;
                length += 1;
            }
        }

        channel = length > 0
            ? new string(sanitized[..length])
            : string.Empty;
        return !string.IsNullOrWhiteSpace(channel);
    }

    private static bool TryNormalizeCompatibilityKey(string? value, out string key)
    {
        key = value?.Trim() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(key);
    }

    private static bool IsDefaultSdkVersionLabel(string version)
    {
        var comparable = version.Trim();
        if (comparable.StartsWith('v') || comparable.StartsWith('V'))
        {
            comparable = comparable[1..];
        }

        comparable = comparable.Split(['-', '+'], 2)[0];
        return string.Equals(comparable, "1.0.0", StringComparison.OrdinalIgnoreCase)
            || string.Equals(comparable, "1.0.0.0", StringComparison.OrdinalIgnoreCase);
    }
}
