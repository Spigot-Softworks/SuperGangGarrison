using System.Collections.Generic;
using OpenGarrison.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class BarrierExportRegressionTests
{
    private sealed class TempWorkspace : IDisposable
    {
        private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("barrier-roundtrip-");
        public static TempWorkspace Create() => new();
        public string PathFor(string relativePath)
        {
            var path = Path.Combine(_directory.FullName, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            return path;
        }
        public void Dispose() => _directory.Delete(recursive: true);
    }

    [Fact]
    public void EveryTargetCombinationSurvivesExportNormalization()
    {
        for (var mask = 0; mask < 64; mask++)
        {
            var properties = BarrierTargetFilterMetadata.TargetPropertyKeys.Select((key, index) =>
                new KeyValuePair<string, string>(key, (mask & (1 << index)) != 0 ? "block" : "allow")).ToDictionary();
            var entity = CustomMapBuilderEntity.Create("barrier", 37, 59, properties, 3, 2);
            var exported = CustomMapBuilderEntityNormalization.ResolveEntityForExport(entity);
            var reloaded = CustomMapBuilderEntityNormalization.NormalizeEntityForEditor(exported);
            Assert.Equal(BarrierTargetFilters.FromProperties(properties), BarrierConfiguration.FromProperties(reloaded.Properties).Targets);
            Assert.Equal(entity.X, reloaded.X);
            Assert.Equal(entity.Y, reloaded.Y);
            Assert.Equal(entity.XScale, reloaded.XScale);
            Assert.Equal(entity.YScale, reloaded.YScale);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DefaultBarrierKeepsPlayerCollisionAfterPngAndPackageRoundTrip(bool blockShots)
    {
        using var workspace = TempWorkspace.Create();
        var background = workspace.PathFor("bg.png");
        var walkmask = workspace.PathFor("wm.png");
        using (var image = new Image<Rgba32>(128, 128))
        {
            image.SaveAsPng(background);
            image.SaveAsPng(walkmask);
        }
        var properties = BarrierTargetFilters.SolidWall.ToProperties();
        properties["redShots"] = blockShots ? "block" : "allow";
        properties["blueShots"] = blockShots ? "block" : "allow";
        var document = CustomMapBuilderDocument.CreateEmpty("barrier_roundtrip") with
        {
            BackgroundImagePath = background, WalkmaskImagePath = walkmask,
            Entities = [CustomMapBuilderEntity.Create("barrier", 50, 20, properties, 3, 2)],
        };
        var png = workspace.PathFor("barrier_roundtrip.png");
        CustomMapPngExporter.Export(document, png);
        var imported = CustomMapPngImporter.Import(png)!;
        Check(Assert.Single(imported.Room.RoomObjects));

        var editable = CustomMapBuilderPngImporter.Import(png)!;
        var package = workspace.PathFor("package/barrier_roundtrip.json");
        CustomMapPackageExporter.Export(editable, package);
        Check(Assert.Single(CustomMapPackageImporter.Import(package)!.Room.RoomObjects));

        void Check(RoomObjectMarker marker)
        {
            Assert.Equal(RoomObjectType.Barrier, marker.Type);
            Assert.Equal(18, marker.Width);
            Assert.Equal(120, marker.Height);
            foreach (var team in new[] { PlayerTeam.Red, PlayerTeam.Blue })
            {
                Assert.True(BarrierCollision.BlocksPlayerMovement(marker.Barrier, team, false, marker,
                    marker.Left - 10, marker.Left, marker.Top, marker.Bottom,
                    marker.Left - 5, marker.Left + 5, marker.Top, marker.Bottom));
                Assert.Equal(blockShots, BarrierProjectileRaycast.TryRaycastMarker(marker.Barrier, team, marker,
                    marker.Left - 10, marker.Top + 10, 1, 0, 40, out _));
                Assert.Equal(blockShots, BarrierCollision.BlocksHitscan(marker.Barrier, team, false, marker,
                    marker.Left - 10, marker.Top + 10, marker.Right + 10, marker.Top + 10));
            }
        }
    }

    [Fact]
    public void ResolveForExportKeepsModernBarrierWhenShotsAreBlocked()
    {
        var entity = CustomMapBuilderEntity.Create(
            "barrier",
            12f,
            34f,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [BarrierTargetFilterMetadata.RedPlayersPropertyKey] = BarrierTargetFilterMetadata.BlockValue,
                [BarrierTargetFilterMetadata.BluePlayersPropertyKey] = BarrierTargetFilterMetadata.BlockValue,
                [BarrierTargetFilterMetadata.RedShotsPropertyKey] = BarrierTargetFilterMetadata.BlockValue,
                [BarrierTargetFilterMetadata.BlueShotsPropertyKey] = BarrierTargetFilterMetadata.BlockValue,
                [BarrierTargetFilterMetadata.RedIntelPropertyKey] = BarrierTargetFilterMetadata.AllowValue,
                [BarrierTargetFilterMetadata.BlueIntelPropertyKey] = BarrierTargetFilterMetadata.AllowValue,
            });

        var exported = CustomMapBuilderEntityNormalization.ResolveEntityForExport(entity);

        Assert.Equal("barrier", exported.Type);
        Assert.Equal(BarrierTargetFilterMetadata.BlockValue, exported.Properties[BarrierTargetFilterMetadata.RedShotsPropertyKey]);
        Assert.Equal(BarrierTargetFilterMetadata.BlockValue, exported.Properties[BarrierTargetFilterMetadata.BlueShotsPropertyKey]);
    }
}
