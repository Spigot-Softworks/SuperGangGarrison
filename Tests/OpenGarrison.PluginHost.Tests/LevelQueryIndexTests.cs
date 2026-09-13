using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class LevelQueryIndexTests
{
    [Fact]
    public void CombatCandidatesRetainSourceOrderAndEverySupportedObstacleType()
    {
        var types = new[] { RoomObjectType.CustomMapSprite, RoomObjectType.Barrier,
            RoomObjectType.HealingCabinet, RoomObjectType.IntelGate, RoomObjectType.TeamGate,
            RoomObjectType.CustomMapSprite, RoomObjectType.DamageableZone, RoomObjectType.BulletWall,
            RoomObjectType.DirectionalWall, RoomObjectType.ControlPointSetupGate, RoomObjectType.PlayerWall };
        var level = MakeLevel(types.Select(type => new RoomObjectMarker(type, 0, 0, 32, 32, "")).ToArray(), []);
        Assert.Equal(new[] { 4, 9 }, level.GateIndices.ToArray());
        Assert.Equal(new[] { 3, 4, 7, 9 }, level.HitscanObstacleIndices.ToArray());
        Assert.Equal(new[] { 1, 2, 4, 6, 7, 8, 9 }, level.ProjectileObstacleIndices.ToArray());
        level.RoomObjectLogicActiveMask[4] = false;
        Assert.Contains(4, level.ProjectileObstacleIndices.ToArray());
        Assert.False(level.IsRoomObjectActive(4));
        level.RoomObjectLogicActiveMask[4] = true;
        Assert.True(level.IsRoomObjectActive(4));
    }

    [Fact]
    public void CachedExtensionParentsStillFollowLiveActivationThroughAChain()
    {
        var level = MakeLevel([
            new RoomObjectMarker(RoomObjectType.TeleportZone, 0, 0, 32, 32, "parent"),
            new RoomObjectMarker(RoomObjectType.CustomMapSprite, 0, 0, 32, 32, "decoration"),
            new RoomObjectMarker(RoomObjectType.AreaExtension, 0, 0, 32, 32, "child",
                AreaExtension: new AreaExtensionConfiguration(0, AreaExtensionKind.Teleport)),
            new RoomObjectMarker(RoomObjectType.AreaExtension, 0, 0, 32, 32, "grandchild",
                AreaExtension: new AreaExtensionConfiguration(2, AreaExtensionKind.Teleport)),
        ], []);
        Assert.True(level.IsRoomObjectActive(3));
        level.RoomObjectLogicActiveMask[0] = false;
        Assert.False(level.IsRoomObjectActive(2));
        Assert.False(level.IsRoomObjectActive(3));
        Assert.True(level.IsRoomObjectActive(1));
        level.RoomObjectLogicActiveMask[0] = true;
        level.RoomObjectLogicActiveMask[2] = false;
        Assert.False(level.IsRoomObjectActive(3));
        level.RoomObjectLogicActiveMask[2] = true;
        Assert.True(level.IsRoomObjectActive(3));
        Assert.True(level.IsRoomObjectActive(-1));
        Assert.True(level.IsRoomObjectActive(99));
    }

    [Fact]
    public void RelevantObjectIndicesPreserveOriginalOrderAmongUnrelatedDecorations()
    {
        var types = new[] { RoomObjectType.CustomMapSprite, RoomObjectType.FireBox,
            RoomObjectType.MoveBoxRight, RoomObjectType.AreaExtension, RoomObjectType.Catapult,
            RoomObjectType.CustomMapSprite, RoomObjectType.KillBox, RoomObjectType.TeleportZone,
            RoomObjectType.MoveBoxUp, RoomObjectType.SpawnRoom, RoomObjectType.FragBox };
        var level = MakeLevel(types.Select(type => new RoomObjectMarker(type, 0, 0, 32, 32, "")).ToArray(), []);
        Assert.Equal(new[] { 2, 8 }, level.MoveBoxIndices.ToArray());
        Assert.Equal(new[] { 1, 6, 10 }, level.HazardIndices.ToArray());
        Assert.Equal(new[] { 3, 7 }, level.TeleportCandidateIndices.ToArray());
        Assert.Equal(new[] { 4 }, level.GetRoomObjectIndices(RoomObjectType.Catapult).ToArray());
        Assert.Equal(new[] { 9 }, level.GetRoomObjectIndices(RoomObjectType.SpawnRoom).ToArray());
        Assert.Empty(level.GetRoomObjectIndices(RoomObjectType.Barrier).ToArray());
    }

    [Fact]
    public void IndexedPointQueryMatchesOriginalSolidScanAtEdgesAndAcrossCells()
    {
        var random = new Random(20260907);
        var solids = Enumerable.Range(0, 300).Select(_ => new LevelSolid(
            random.Next(-400, 1000), random.Next(-400, 1000), random.Next(1, 300), random.Next(1, 300))).ToArray();
        var level = MakeLevel([], solids);
        void Check(float x, float y) => Assert.Equal(solids.Any(s => x >= s.Left && x < s.Right && y >= s.Top && y < s.Bottom), level.ContainsSolidPoint(x, y));
        foreach (var solid in solids)
        {
            Check(solid.Left, solid.Top);
            Check(solid.Right, solid.Bottom);
            Check(solid.Right, solid.Top);
            Check(solid.Left, solid.Bottom);
            Check(MathF.BitDecrement(solid.Left), solid.Top);
            Check(MathF.BitDecrement(solid.Right), solid.Top);
        }
        for (var i = 0; i < 2000; i++) Check(random.Next(-500, 1500), random.Next(-500, 1500));
    }

    private static SimpleLevel MakeLevel(RoomObjectMarker[] markers, LevelSolid[] solids) => new(
        "indexed-query-test", GameModeKind.TeamDeathmatch, new WorldBounds(2048, 2048), 1, null,
        1, 1, new SpawnPoint(32, 32), [], [], [], markers, 2000, solids, false);
}
