namespace OpenGarrison.Core;

public static class TraversalLabFixtures
{
    public static SimpleLevel Create(TraversalLabFixtureKind fixture)
    {
        return fixture switch
        {
            TraversalLabFixtureKind.FlatGround => CreateFlatGround(),
            TraversalLabFixtureKind.LowBoxClimb => CreateLowBoxClimb(),
            TraversalLabFixtureKind.ShortGap => CreateShortGap(),
            TraversalLabFixtureKind.StairDescent => CreateStairDescent(),
            _ => throw new ArgumentOutOfRangeException(nameof(fixture), fixture, "Unknown TraversalLab fixture."),
        };
    }

    private static SimpleLevel CreateFlatGround()
    {
        return CreateLevel(
            "traversal_lab_flat_ground",
            floorY: 500f,
            solids:
            [
                new LevelSolid(0f, 500f, 2048f, 524f),
            ]);
    }

    private static SimpleLevel CreateLowBoxClimb()
    {
        return CreateLevel(
            "traversal_lab_low_box_climb",
            floorY: 500f,
            solids:
            [
                new LevelSolid(0f, 500f, 2048f, 524f),
                new LevelSolid(320f, 460f, 400f, 500f),
            ]);
    }

    private static SimpleLevel CreateShortGap()
    {
        return CreateLevel(
            "traversal_lab_short_gap",
            floorY: 700f,
            solids:
            [
                new LevelSolid(0f, 500f, 320f, 524f),
                new LevelSolid(448f, 500f, 2048f, 524f),
                new LevelSolid(0f, 700f, 2048f, 724f),
            ]);
    }

    private static SimpleLevel CreateStairDescent()
    {
        return CreateLevel(
            "traversal_lab_stair_descent",
            floorY: 700f,
            solids:
            [
                new LevelSolid(0f, 380f, 240f, 404f),
                new LevelSolid(240f, 410f, 420f, 434f),
                new LevelSolid(420f, 440f, 600f, 464f),
                new LevelSolid(600f, 470f, 780f, 494f),
                new LevelSolid(780f, 500f, 2048f, 524f),
                new LevelSolid(0f, 700f, 2048f, 724f),
            ]);
    }

    private static SimpleLevel CreateLevel(string name, float floorY, LevelSolid[] solids)
    {
        return new SimpleLevel(
            name: name,
            mode: GameModeKind.CaptureTheFlag,
            bounds: new WorldBounds(2048f, 1024f),
            mapScale: 1f,
            backgroundAssetName: null,
            mapAreaIndex: 1,
            mapAreaCount: 1,
            localSpawn: new SpawnPoint(128f, 128f),
            redSpawns: [new SpawnPoint(128f, 128f)],
            blueSpawns: [new SpawnPoint(256f, 128f)],
            intelBases:
            [
                new IntelBaseMarker(PlayerTeam.Red, 128f, 128f),
                new IntelBaseMarker(PlayerTeam.Blue, 256f, 128f),
            ],
            roomObjects: [],
            floorY: floorY,
            solids: solids,
            importedFromSource: false);
    }
}
