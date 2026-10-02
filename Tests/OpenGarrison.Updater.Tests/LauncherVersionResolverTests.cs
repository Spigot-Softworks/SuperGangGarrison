using Xunit;

namespace OpenGarrison.Updater.Tests;

public sealed class LauncherVersionResolverTests
{
    [Fact]
    public void WindowsApphostProductVersionHasPriorityOverAssemblyMetadata()
    {
        var version = LauncherVersionResolver.Resolve(
            processPath: @"C:\Games\OpenGarrison\OG2.Updater.exe",
            processProductVersion: "0.8.8",
            informationalVersion: "0.8.7.4",
            assemblyVersion: new Version(0, 8, 7, 4));

        Assert.Equal("0.8.8", version);
    }

    [Theory]
    [InlineData("/usr/share/dotnet/dotnet")]
    [InlineData(@"C:\Program Files\dotnet\dotnet.exe")]
    public void DotNetHostProductVersionIsIgnoredForTheExecutingUpdaterAssembly(string processPath)
    {
        var version = LauncherVersionResolver.Resolve(
            processPath,
            processProductVersion: "10.0.2",
            informationalVersion: "0.8.8",
            assemblyVersion: new Version(0, 8, 7, 4));

        Assert.Equal("0.8.8", version);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ElfApphostWithoutProductVersionUsesInformationalVersion(string? processProductVersion)
    {
        var version = LauncherVersionResolver.Resolve(
            processPath: "/opt/opengarrison/OG2.Updater",
            processProductVersion: processProductVersion,
            informationalVersion: "0.8.8+buildmetadata",
            assemblyVersion: new Version(0, 8, 7, 4));

        Assert.Equal("0.8.8+buildmetadata", version);
    }

    [Fact]
    public void AssemblyVersionIsUsedWhenInformationalVersionIsUnavailable()
    {
        var version = LauncherVersionResolver.Resolve(
            processPath: "/usr/share/dotnet/dotnet",
            processProductVersion: "10.0.2",
            informationalVersion: " ",
            assemblyVersion: new Version(0, 8, 8, 0));

        Assert.Equal("0.8.8.0", version);
    }

    [Fact]
    public void UnknownVersionIsLastResortOnlyWhenAllMetadataIsUnavailable()
    {
        var version = LauncherVersionResolver.Resolve(
            processPath: null,
            processProductVersion: null,
            informationalVersion: null,
            assemblyVersion: null);

        Assert.Equal("0.0.0", version);
    }
}
