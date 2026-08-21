using OpenGarrison.Bootstrap;
using OpenGarrison.Client;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class DotNetRuntimePrerequisiteTests
{
    [Fact]
    public void ParsesAspNetCoreRequirementFromServerRuntimeConfig()
    {
        const string RuntimeConfig = """
            {
              "runtimeOptions": {
                "tfm": "net10.0",
                "frameworks": [
                  { "name": "Microsoft.NETCore.App", "version": "10.0.0" },
                  { "name": "Microsoft.AspNetCore.App", "version": "10.0.0" }
                ]
              }
            }
            """;

        Assert.True(DotNetRuntimePrerequisite.TryParseFrameworkRequirement(
            RuntimeConfig,
            "Microsoft.AspNetCore.App",
            out var requirement));
        Assert.Equal("Microsoft.AspNetCore.App", requirement.FrameworkName);
        Assert.Equal(new Version(10, 0, 0), requirement.MinimumVersion);
        Assert.Equal("10.0", requirement.VersionFamily);
    }

    [Fact]
    public void NewerPatchInRequiredRuntimeFamilyIsCompatible()
    {
        var requirement = new DotNetFrameworkRequirement(
            "Microsoft.AspNetCore.App",
            new Version(10, 0, 0));
        const string InstalledRuntimes = """
            Microsoft.AspNetCore.App 8.0.24 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]
            Microsoft.AspNetCore.App 10.0.6 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]
            Microsoft.NETCore.App 10.0.6 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
            """;

        Assert.True(DotNetRuntimePrerequisite.IsFrameworkAvailable(InstalledRuntimes, requirement));
    }

    [Theory]
    [InlineData("Microsoft.AspNetCore.App 9.0.12 [runtime]")]
    [InlineData("Microsoft.AspNetCore.App 11.0.0 [runtime]")]
    [InlineData("Microsoft.AspNetCore.App 10.0.0-preview.7 [runtime]")]
    [InlineData("Microsoft.NETCore.App 10.0.6 [runtime]")]
    public void UnrelatedOrPreviewRuntimeDoesNotSatisfyStableRequirement(string installedRuntimes)
    {
        var requirement = new DotNetFrameworkRequirement(
            "Microsoft.AspNetCore.App",
            new Version(10, 0, 0));

        Assert.False(DotNetRuntimePrerequisite.IsFrameworkAvailable(installedRuntimes, requirement));
    }

    [Fact]
    public void HostedServerPreflightReportsMissingRequiredRuntimeFamily()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"opengarrison-runtime-preflight-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        try
        {
            File.WriteAllText(
                Path.Combine(temporaryDirectory, "OG2.Server.runtimeconfig.json"),
                """
                {
                  "runtimeOptions": {
                    "frameworks": [
                      { "name": "Microsoft.AspNetCore.App", "version": "99.0.0" }
                    ]
                  }
                }
                """);
            var launchTarget = new HostedServerLaunchTarget(
                "OG2.Server.exe",
                string.Empty,
                temporaryDirectory);

            Assert.False(HostedServerBootstrapper.TryValidateRuntimePrerequisites(launchTarget, out var error));
            Assert.Contains("ASP.NET Core Runtime 99.0", error, StringComparison.Ordinal);
            Assert.Contains("Microsoft.AspNetCore.App 99.0.x", error, StringComparison.Ordinal);
            Assert.Contains("https://dotnet.microsoft.com", error, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }
}
