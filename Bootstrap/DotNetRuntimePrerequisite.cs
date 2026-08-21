#nullable enable

using System.Diagnostics;
using System.Text.Json;

namespace OpenGarrison.Bootstrap;

internal sealed record DotNetFrameworkRequirement(string FrameworkName, Version MinimumVersion)
{
    public string VersionFamily => $"{MinimumVersion.Major}.{MinimumVersion.Minor}";
}

internal static class DotNetRuntimePrerequisite
{
    private const int RuntimeQueryTimeoutMilliseconds = 5000;
    private static readonly string[] DotNetHostEnvironmentVariableNames =
    [
        "DOTNET_HOST_PATH",
        "DOTNET_ROOT_X64",
        "DOTNET_ROOT",
    ];
    private static readonly string[] ProgramFilesDirectories =
    [
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
    ];

    public static bool TryReadFrameworkRequirement(
        string runtimeConfigPath,
        string frameworkName,
        out DotNetFrameworkRequirement requirement)
    {
        requirement = null!;
        if (string.IsNullOrWhiteSpace(runtimeConfigPath)
            || string.IsNullOrWhiteSpace(frameworkName)
            || !File.Exists(runtimeConfigPath))
        {
            return false;
        }

        try
        {
            return TryParseFrameworkRequirement(
                File.ReadAllText(runtimeConfigPath),
                frameworkName,
                out requirement);
        }
        catch
        {
            return false;
        }
    }

    public static bool TryParseFrameworkRequirement(
        string runtimeConfigJson,
        string frameworkName,
        out DotNetFrameworkRequirement requirement)
    {
        requirement = null!;
        if (string.IsNullOrWhiteSpace(runtimeConfigJson) || string.IsNullOrWhiteSpace(frameworkName))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(runtimeConfigJson);
            if (!document.RootElement.TryGetProperty("runtimeOptions", out var runtimeOptions))
            {
                return false;
            }

            return TryFindFrameworkRequirement(runtimeOptions, frameworkName, out requirement);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static bool IsFrameworkAvailable(
        string installedRuntimes,
        DotNetFrameworkRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(requirement);
        if (string.IsNullOrWhiteSpace(installedRuntimes))
        {
            return false;
        }

        foreach (var line in installedRuntimes.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var separatorIndex = line.IndexOf(' ');
            if (separatorIndex <= 0
                || !string.Equals(
                    line[..separatorIndex],
                    requirement.FrameworkName,
                    StringComparison.Ordinal))
            {
                continue;
            }

            var versionStart = separatorIndex + 1;
            while (versionStart < line.Length && char.IsWhiteSpace(line[versionStart]))
            {
                versionStart += 1;
            }

            var versionEnd = line.IndexOf(' ', versionStart);
            var versionText = versionEnd < 0
                ? line[versionStart..]
                : line[versionStart..versionEnd];
            // A stable requirement must not be silently satisfied by a preview runtime.
            if (versionText.Contains('-', StringComparison.Ordinal)
                || !Version.TryParse(versionText, out var installedVersion))
            {
                continue;
            }

            if (installedVersion.Major == requirement.MinimumVersion.Major
                && installedVersion.Minor == requirement.MinimumVersion.Minor
                && installedVersion >= requirement.MinimumVersion)
            {
                return true;
            }
        }

        return false;
    }

    public static bool TryQueryInstalledRuntimes(out string installedRuntimes, out string error)
    {
        installedRuntimes = string.Empty;
        error = string.Empty;

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ResolveDotNetHostPath(),
                    Arguments = "--list-runtimes",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                },
            };
            if (!process.Start())
            {
                error = "The dotnet host could not be started.";
                return false;
            }

            var standardOutput = process.StandardOutput.ReadToEndAsync();
            var standardError = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(RuntimeQueryTimeoutMilliseconds))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                }

                error = "The dotnet runtime query timed out.";
                return false;
            }

            installedRuntimes = standardOutput.GetAwaiter().GetResult();
            error = standardError.GetAwaiter().GetResult().Trim();
            if (process.ExitCode != 0)
            {
                if (string.IsNullOrWhiteSpace(error))
                {
                    error = $"dotnet --list-runtimes exited with code {process.ExitCode}.";
                }

                installedRuntimes = string.Empty;
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static string GetDownloadUrl(DotNetFrameworkRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(requirement);
        return $"https://dotnet.microsoft.com/en-us/download/dotnet/{requirement.VersionFamily}";
    }

    private static string ResolveDotNetHostPath()
    {
        foreach (var environmentVariableName in DotNetHostEnvironmentVariableNames)
        {
            var configuredPath = Environment.GetEnvironmentVariable(environmentVariableName);
            if (string.IsNullOrWhiteSpace(configuredPath))
            {
                continue;
            }

            var candidate = Directory.Exists(configuredPath)
                ? Path.Combine(configuredPath, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet")
                : configuredPath;
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        if (OperatingSystem.IsWindows())
        {
            foreach (var programFilesDirectory in ProgramFilesDirectories)
            {
                if (string.IsNullOrWhiteSpace(programFilesDirectory))
                {
                    continue;
                }

                var candidate = Path.Combine(programFilesDirectory, "dotnet", "dotnet.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return "dotnet";
    }

    private static bool TryFindFrameworkRequirement(
        JsonElement runtimeOptions,
        string frameworkName,
        out DotNetFrameworkRequirement requirement)
    {
        requirement = null!;
        if (runtimeOptions.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (runtimeOptions.TryGetProperty("frameworks", out var frameworks)
            && frameworks.ValueKind == JsonValueKind.Array)
        {
            foreach (var framework in frameworks.EnumerateArray())
            {
                if (TryCreateRequirement(framework, frameworkName, out requirement))
                {
                    return true;
                }
            }
        }

        if (runtimeOptions.TryGetProperty("framework", out var singleFramework)
            && TryCreateRequirement(singleFramework, frameworkName, out requirement))
        {
            return true;
        }

        // Some older package templates nested runtimeOptions while the SDK also
        // generated the outer object. Accept that shape so an existing package
        // still gets a useful prerequisite warning.
        return runtimeOptions.TryGetProperty("runtimeOptions", out var nestedRuntimeOptions)
            && TryFindFrameworkRequirement(nestedRuntimeOptions, frameworkName, out requirement);
    }

    private static bool TryCreateRequirement(
        JsonElement framework,
        string frameworkName,
        out DotNetFrameworkRequirement requirement)
    {
        requirement = null!;
        if (framework.ValueKind != JsonValueKind.Object
            || !framework.TryGetProperty("name", out var nameProperty)
            || !framework.TryGetProperty("version", out var versionProperty)
            || !string.Equals(nameProperty.GetString(), frameworkName, StringComparison.Ordinal))
        {
            return false;
        }

        var versionText = versionProperty.GetString();
        if (string.IsNullOrWhiteSpace(versionText))
        {
            return false;
        }

        var prereleaseSeparator = versionText.IndexOf('-', StringComparison.Ordinal);
        var stableVersionText = prereleaseSeparator < 0 ? versionText : versionText[..prereleaseSeparator];
        if (!Version.TryParse(stableVersionText, out var minimumVersion))
        {
            return false;
        }

        requirement = new DotNetFrameworkRequirement(frameworkName, minimumVersion);
        return true;
    }
}
