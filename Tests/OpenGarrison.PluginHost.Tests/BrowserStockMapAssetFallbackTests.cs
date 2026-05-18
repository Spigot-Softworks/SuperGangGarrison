using System.Collections.Generic;
using System.IO;
using OpenGarrison.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class BrowserStockMapAssetFallbackTests
{
    [Fact]
    public void GameMakerRoomMetadataImporterLoadsRoomXmlFromBrowserContentCatalog()
    {
        var originalContentRoot = ContentRoot.Path;
        var tempRoot = Path.Combine(Path.GetTempPath(), $"og-browser-room-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(tempRoot, "Rooms", "Maps"));
        try
        {
            ContentRoot.Initialize(tempRoot);
            var roomPath = ContentRoot.GetPath("Rooms", "Maps", "Truefort.xml");
            BrowserContentCatalog.SetBinaryAssets(
            [
                new KeyValuePair<string, byte[]>(NormalizeRelativePath(roomPath), System.Text.Encoding.UTF8.GetBytes("""
                    <room>
                      <size width="640" height="480" />
                      <backgrounds>
                        <backgroundDef>
                          <visibleOnRoomStart>true</visibleOnRoomStart>
                          <backgroundImage>TruefortB</backgroundImage>
                        </backgroundDef>
                      </backgrounds>
                      <instances>
                        <instance>
                          <object>SpawnPointRed</object>
                          <position x="32" y="64" />
                        </instance>
                        <instance>
                          <object>SpawnPointBlue</object>
                          <position x="320" y="64" />
                        </instance>
                      </instances>
                    </room>
                    """))
            ]);

            var metadata = GameMakerRoomMetadataImporter.Import(roomPath);

            Assert.NotNull(metadata);
            Assert.Equal("TruefortB", metadata!.PrimaryBackgroundAssetName);
            Assert.Single(metadata.RedSpawns);
            Assert.Single(metadata.BlueSpawns);
        }
        finally
        {
            BrowserContentCatalog.SetBinaryAssets([]);
            ContentRoot.Initialize(originalContentRoot);
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void GameMakerCollisionMaskImporterLoadsMaskFromBrowserContentCatalog()
    {
        var originalContentRoot = ContentRoot.Path;
        var tempRoot = Path.Combine(Path.GetTempPath(), $"og-browser-collision-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(tempRoot, "Sprites", "Collision Maps", "TruefortS.images"));
        try
        {
            ContentRoot.Initialize(tempRoot);
            var collisionPath = ContentRoot.GetPath("Sprites", "Collision Maps", "TruefortS.images", "image 0.png");
            BrowserContentCatalog.SetBinaryAssets(
            [
                new KeyValuePair<string, byte[]>(NormalizeRelativePath(collisionPath), CreateOpaqueCornerPngBytes())
            ]);

            var solids = GameMakerCollisionMaskImporter.Import(collisionPath, new WorldBounds(20f, 20f));

            var solid = Assert.Single(solids);
            Assert.Equal(0f, solid.X);
            Assert.Equal(0f, solid.Y);
            Assert.Equal(10f, solid.Width);
            Assert.Equal(10f, solid.Height);
        }
        finally
        {
            BrowserContentCatalog.SetBinaryAssets([]);
            ContentRoot.Initialize(originalContentRoot);
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static byte[] CreateOpaqueCornerPngBytes()
    {
        using var image = new Image<Rgba32>(2, 2);
        image[0, 0] = new Rgba32(255, 255, 255, 255);
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }

    private static string NormalizeRelativePath(string path)
        => path.Replace('\\', '/').TrimStart('/');
}
