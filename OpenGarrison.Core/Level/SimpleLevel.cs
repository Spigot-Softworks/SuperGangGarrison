using System.Collections.Generic;

namespace OpenGarrison.Core;

public sealed class SimpleLevel
{
    public SimpleLevel(
        string name,
        GameModeKind mode,
        WorldBounds bounds,
        string? backgroundAssetName,
        int mapAreaIndex,
        int mapAreaCount,
        SpawnPoint localSpawn,
        IReadOnlyList<SpawnPoint> redSpawns,
        IReadOnlyList<SpawnPoint> blueSpawns,
        IReadOnlyList<IntelBaseMarker> intelBases,
        IReadOnlyList<RoomObjectMarker> roomObjects,
        float floorY,
        IReadOnlyList<LevelSolid> solids,
        bool importedFromSource,
        IReadOnlyList<AreaTransitionMarker>? areaTransitionMarkers = null,
        IReadOnlyList<string>? unsupportedSourceEntities = null)
    {
        Name = name;
        Mode = mode;
        Bounds = bounds;
        BackgroundAssetName = backgroundAssetName;
        MapAreaIndex = mapAreaIndex;
        MapAreaCount = mapAreaCount;
        LocalSpawn = localSpawn;
        RedSpawns = redSpawns;
        BlueSpawns = blueSpawns;
        IntelBases = intelBases;
        RoomObjects = roomObjects;
        FloorY = floorY;
        Solids = solids;
        ImportedFromSource = importedFromSource;
        AreaTransitionMarkers = areaTransitionMarkers ?? Array.Empty<AreaTransitionMarker>();
        UnsupportedSourceEntities = unsupportedSourceEntities ?? Array.Empty<string>();
    }

    public string Name { get; }

    public GameModeKind Mode { get; }

    public WorldBounds Bounds { get; }

    public string? BackgroundAssetName { get; }

    public int MapAreaIndex { get; }

    public int MapAreaCount { get; }

    public SpawnPoint LocalSpawn { get; }

    public IReadOnlyList<SpawnPoint> RedSpawns { get; }

    public IReadOnlyList<SpawnPoint> BlueSpawns { get; }

    public IReadOnlyList<IntelBaseMarker> IntelBases { get; }

    public IReadOnlyList<RoomObjectMarker> RoomObjects { get; }

    public float FloorY { get; }

    public IReadOnlyList<LevelSolid> Solids { get; }

    public bool ImportedFromSource { get; }

    public IReadOnlyList<AreaTransitionMarker> AreaTransitionMarkers { get; }

    public IReadOnlyList<string> UnsupportedSourceEntities { get; }

    public bool ControlPointSetupGatesActive { get; set; }

    public SpawnPoint GetSpawn(PlayerTeam team, int spawnIndex)
    {
        var teamSpawns = team == PlayerTeam.Blue ? BlueSpawns : RedSpawns;
        if (teamSpawns.Count == 0)
        {
            return LocalSpawn;
        }

        return teamSpawns[spawnIndex % teamSpawns.Count];
    }

    public IntelBaseMarker? GetIntelBase(PlayerTeam team)
    {
        return IntelBases
            .Where(intelBase => intelBase.Team == team)
            .Cast<IntelBaseMarker?>()
            .FirstOrDefault();
    }

    public RoomObjectMarker? GetFirstRoomObject(RoomObjectType type)
    {
        return RoomObjects
            .Where(roomObject => roomObject.Type == type)
            .Cast<RoomObjectMarker?>()
            .FirstOrDefault();
    }

    public IReadOnlyList<RoomObjectMarker> GetRoomObjects(RoomObjectType type)
    {
        return RoomObjects
            .Where(roomObject => roomObject.Type == type)
            .ToArray();
    }

    public IReadOnlyList<RoomObjectMarker> GetBlockingTeamGates(PlayerTeam team, bool carryingIntel)
    {
        var blockingGates = new List<RoomObjectMarker>();
        foreach (var roomObject in RoomObjects)
        {
            switch (roomObject.Type)
            {
                case RoomObjectType.ControlPointSetupGate:
                    if (ControlPointSetupGatesActive)
                    {
                        blockingGates.Add(roomObject);
                    }
                    break;
                case RoomObjectType.TeamGate:
                    if (carryingIntel || (roomObject.Team.HasValue && roomObject.Team.Value != team))
                    {
                        blockingGates.Add(roomObject);
                    }
                    break;
                case RoomObjectType.IntelGate:
                    if (IsIntelGateBlocking(roomObject, team, carryingIntel))
                    {
                        blockingGates.Add(roomObject);
                    }
                    break;
            }
        }

        return blockingGates.Count == 0 ? Array.Empty<RoomObjectMarker>() : blockingGates.ToArray();
    }

    private static bool IsIntelGateBlocking(RoomObjectMarker roomObject, PlayerTeam team, bool carryingIntel)
    {
        if (carryingIntel)
        {
            return false;
        }

        if (roomObject.Team.HasValue)
        {
            return roomObject.Team.Value != team;
        }

        return true;
    }
}
