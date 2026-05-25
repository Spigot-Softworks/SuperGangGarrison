using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ServerMapRotationTests
{
    [Fact]
    public void MapRotationFileAcceptsCustomMapPathsAndExtensions()
    {
        var root = Path.Combine(Path.GetTempPath(), $"opengarrison-rotation-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(root);
            var rotationPath = Path.Combine(root, "rotation.txt");
            File.WriteAllLines(rotationPath,
            [
                "# comments are ignored",
                "Maps/my_custom.png",
                "my_package/my_package.json",
                "\"quoted_map.png\"",
                "koth_harvest",
            ]);

            var rotation = ServerHelpers.LoadMapRotation(rotationPath, []);

            Assert.Equal(
            [
                "my_custom",
                "my_package",
                "quoted_map",
                "Harvest",
            ], rotation);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
