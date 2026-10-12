using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class CustomMapDirectoryLayoutTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "map-layout-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public void FindsPackagesAndPngsInCategoryAndModeFolders()
    {
        var sggPackage = Path.Combine(_root, CustomMapDirectoryLayout.SuperGangGarrisonCategory, "cp", "Docking");
        Directory.CreateDirectory(sggPackage);
        File.WriteAllText(Path.Combine(sggPackage, "Docking.json"), "{}");
        var classicMode = Path.Combine(_root, CustomMapDirectoryLayout.ClassicCategory, "koth");
        Directory.CreateDirectory(classicMode);
        File.WriteAllBytes(Path.Combine(classicMode, "koth_valley.png"), [137, 80, 78, 71]);
        Directory.CreateDirectory(Path.Combine(_root, ".koth_x.download"));

        var groups = CustomMapDirectoryLayout.EnumerateGroupDirectories(_root).ToArray();
        Assert.Contains(_root, groups);
        Assert.Contains(classicMode, groups);
        Assert.DoesNotContain(sggPackage, groups);
        Assert.DoesNotContain(groups, group => Path.GetFileName(group).StartsWith('.'));

        Assert.Contains(Path.Combine(classicMode, "koth_valley.png"), CustomMapDirectoryLayout.EnumerateLegacyPngMaps(_root));
        Assert.True(CustomMapDirectoryLayout.TryFindLegacyPngMap(_root, "KOTH_VALLEY", out var pngPath));
        Assert.Equal(Path.Combine(classicMode, "koth_valley.png"), pngPath, ignoreCase: true);
        Assert.False(CustomMapDirectoryLayout.TryFindLegacyPngMap(_root, "koth_missing", out _));
    }

    [Theory]
    [InlineData("cp_gully", "cp")]
    [InlineData("3cp_kistra", "cp")]
    [InlineData("Dkoth_Standoff!", "dkoth")]
    [InlineData("rj_lavafort", "rj")]
    [InlineData("Kulay", "other")]
    public void ModeFolderComesFromTheNamePrefix(string mapName, string expected)
    {
        Assert.Equal(expected, CustomMapDirectoryLayout.GetModeFolderName(mapName));
    }
}
