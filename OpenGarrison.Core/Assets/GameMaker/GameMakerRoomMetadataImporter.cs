using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace OpenGarrison.Core;

public static class GameMakerRoomMetadataImporter
{
    public static GameMakerRoomMetadata? Import(string roomFilePath)
    {
        if (!File.Exists(roomFilePath))
        {
            return null;
        }

        var document = XDocument.Load(roomFilePath);
        var room = document.Root;
        if (room is null)
        {
            return null;
        }

        var sizeElement = room.Element("size");
        if (sizeElement is null)
        {
            return null;
        }

        var width = (float?)sizeElement.Attribute("width");
        var height = (float?)sizeElement.Attribute("height");
        if (width is null || height is null)
        {
            return null;
        }

        var instances = room.Element("instances")?.Elements("instance").ToArray() ?? [];
        var redSpawns = ReadSpawnPoints(instances, "SpawnPointRed");
        var blueSpawns = ReadSpawnPoints(instances, "SpawnPointBlue");
        var redIntelBases = ReadIntelBases(instances, "IntelligenceBaseRed", PlayerTeam.Red);
        if (redIntelBases.Length == 0)
        {
            redIntelBases = ReadIntelBases(instances, "IntelligenceRed", PlayerTeam.Red);
        }

        var blueIntelBases = ReadIntelBases(instances, "IntelligenceBaseBlue", PlayerTeam.Blue);
        if (blueIntelBases.Length == 0)
        {
            blueIntelBases = ReadIntelBases(instances, "IntelligenceBlue", PlayerTeam.Blue);
        }

        var intelBases = redIntelBases
            .Concat(blueIntelBases)
            .ToArray();
        var roomObjects = ReadRoomObjects(instances);
        var unsupportedEntities = ReadUnsupportedEntities(instances);
        var areaTransitionMarkers = ReadAreaTransitionMarkers(instances);
        var areaBoundaries = AreaTransitionMetadata.BuildAreaBoundaries(areaTransitionMarkers);
        var primaryBackgroundAssetName = ReadPrimaryBackgroundAssetName(room);

        var roomName = Path.GetFileNameWithoutExtension(roomFilePath);
        return new GameMakerRoomMetadata(
            Name: roomName,
            Bounds: new WorldBounds(width.Value, height.Value),
            PrimaryBackgroundAssetName: primaryBackgroundAssetName,
            RedSpawns: redSpawns,
            BlueSpawns: blueSpawns,
            IntelBases: intelBases,
            RoomObjects: roomObjects,
            AreaBoundaries: areaBoundaries)
        {
            AreaTransitionMarkers = areaTransitionMarkers,
            UnsupportedEntities = unsupportedEntities,
        };
    }

    private static SpawnPoint[] ReadSpawnPoints(XElement[] instances, string objectName)
    {
        return instances
            .Where(instance => (string?)instance.Element("object") == objectName)
            .Select(instance => instance.Element("position"))
            .Where(position => position is not null)
            .Select(position => new SpawnPoint(
                (float?)position!.Attribute("x") ?? 0f,
                (float?)position!.Attribute("y") ?? 0f))
            .ToArray();
    }

    private static IntelBaseMarker[] ReadIntelBases(XElement[] instances, string objectName, PlayerTeam team)
    {
        return instances
            .Where(instance => (string?)instance.Element("object") == objectName)
            .Select(instance => instance.Element("position"))
            .Where(position => position is not null)
            .Select(position => new IntelBaseMarker(
                team,
                (float?)position!.Attribute("x") ?? 0f,
                (float?)position!.Attribute("y") ?? 0f))
            .ToArray();
    }

    private static RoomObjectMarker[] ReadRoomObjects(XElement[] instances)
    {
        return instances
            .Select(ToRoomObjectMarker)
            .Where(marker => marker.HasValue)
            .Select(marker => marker!.Value)
            .ToArray();
    }

    private static string[] ReadUnsupportedEntities(XElement[] instances)
    {
        return instances
            .Select(instance => (string?)instance.Element("object"))
            .Where(objectName => !string.IsNullOrWhiteSpace(objectName))
            .Select(objectName => objectName!)
            .Where(IsUnsupportedEntity)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static AreaTransitionMarker[] ReadAreaTransitionMarkers(XElement[] instances)
    {
        var markers = new List<AreaTransitionMarker>();
        foreach (var instance in instances)
        {
            var objectName = (string?)instance.Element("object");
            var position = instance.Element("position");
            if (position is null)
            {
                continue;
            }

            var marker = objectName switch
            {
                "NextAreaO" => new AreaTransitionMarker(
                    (float?)position.Attribute("x") ?? 0f,
                    (float?)position.Attribute("y") ?? 0f,
                    AreaTransitionDirection.Next,
                    objectName),
                "PreviousAreaO" => new AreaTransitionMarker(
                    (float?)position.Attribute("x") ?? 0f,
                    (float?)position.Attribute("y") ?? 0f,
                    AreaTransitionDirection.Previous,
                    objectName),
                _ => (AreaTransitionMarker?)null,
            };
            if (marker.HasValue)
            {
                markers.Add(marker.Value);
            }
        }

        return markers.ToArray();
    }

    private static string ReadPrimaryBackgroundAssetName(XElement room)
    {
        var backgroundDefs = room.Element("backgrounds")?.Elements("backgroundDef").ToArray() ?? [];
        foreach (var backgroundDef in backgroundDefs)
        {
            var backgroundImage = (string?)backgroundDef.Element("backgroundImage");
            if (string.IsNullOrWhiteSpace(backgroundImage))
            {
                continue;
            }

            var visibleOnRoomStart = bool.TryParse((string?)backgroundDef.Element("visibleOnRoomStart"), out var visible)
                && visible;
            if (visibleOnRoomStart)
            {
                return backgroundImage;
            }
        }

        return backgroundDefs
            .Select(backgroundDef => (string?)backgroundDef.Element("backgroundImage"))
            .FirstOrDefault(backgroundImage => !string.IsNullOrWhiteSpace(backgroundImage))
            ?? string.Empty;
    }

    private static RoomObjectMarker? ToRoomObjectMarker(XElement instance)
    {
        var objectName = (string?)instance.Element("object");
        var position = instance.Element("position");
        if (objectName is null || position is null)
        {
            return null;
        }

        var x = (float?)position.Attribute("x") ?? 0f;
        var y = (float?)position.Attribute("y") ?? 0f;

        return objectName switch
        {
            "HealingCabinet" => new RoomObjectMarker(RoomObjectType.HealingCabinet, x, y, 32f, 48f, "sprite74", SourceName: objectName),
            "RedTeamGate" => new RoomObjectMarker(RoomObjectType.TeamGate, x, y, 6f, 60f, "sprite45", PlayerTeam.Red, objectName),
            "BlueTeamGate" => new RoomObjectMarker(RoomObjectType.TeamGate, x, y, 6f, 60f, "sprite45", PlayerTeam.Blue, objectName),
            "RedTeamGate2" => new RoomObjectMarker(RoomObjectType.TeamGate, x, y, 60f, 6f, "sprite44", PlayerTeam.Red, objectName),
            "BlueTeamGate2" => new RoomObjectMarker(RoomObjectType.TeamGate, x, y, 60f, 6f, "sprite44", PlayerTeam.Blue, objectName),
            "RedIntelGate" => new RoomObjectMarker(RoomObjectType.IntelGate, x, y, 6f, 60f, "sprite45", PlayerTeam.Red, objectName),
            "BlueIntelGate" => new RoomObjectMarker(RoomObjectType.IntelGate, x, y, 6f, 60f, "sprite45", PlayerTeam.Blue, objectName),
            "RedIntelGate2" => new RoomObjectMarker(RoomObjectType.IntelGate, x, y, 60f, 6f, "sprite44", PlayerTeam.Red, objectName),
            "BlueIntelGate2" => new RoomObjectMarker(RoomObjectType.IntelGate, x, y, 60f, 6f, "sprite44", PlayerTeam.Blue, objectName),
            "IntelGateVertical" => new RoomObjectMarker(RoomObjectType.IntelGate, x, y, 6f, 60f, "sprite45", SourceName: objectName),
            "IntelGateHorizontal" => new RoomObjectMarker(RoomObjectType.IntelGate, x, y, 60f, 6f, "sprite44", SourceName: objectName),
            "BulletWall" => new RoomObjectMarker(RoomObjectType.BulletWall, x, y, 6f, 60f, "sprite45", SourceName: objectName),
            "BulletWallHorizontal" => new RoomObjectMarker(RoomObjectType.BulletWall, x, y, 60f, 6f, "sprite44", SourceName: objectName),
            "PlayerWall" => new RoomObjectMarker(RoomObjectType.PlayerWall, x, y, 6f, 60f, "sprite45", SourceName: objectName),
            "PlayerWallHorizontal" => new RoomObjectMarker(RoomObjectType.PlayerWall, x, y, 60f, 6f, "sprite44", SourceName: objectName),
            "SpawnRoom" => new RoomObjectMarker(RoomObjectType.SpawnRoom, x, y, 42f, 42f, "sprite64", SourceName: objectName),
            "FragBox" => new RoomObjectMarker(RoomObjectType.FragBox, x, y, 42f, 42f, "sprite64", SourceName: objectName),
            "KillBox" => new RoomObjectMarker(RoomObjectType.KillBox, x, y, 42f, 42f, "sprite64", SourceName: objectName),
            "ArenaControlPoint" => new RoomObjectMarker(RoomObjectType.ArenaControlPoint, x, y, 42f, 42f, "ControlPointNeutralS", SourceName: objectName),
            "CaptureZone" => new RoomObjectMarker(RoomObjectType.CaptureZone, x, y, 42f, 42f, string.Empty, SourceName: objectName),
            "ControlPoint" => new RoomObjectMarker(RoomObjectType.ControlPoint, x, y, 42f, 42f, "ControlPointNeutralS", SourceName: objectName),
            "ControlPoint1" => new RoomObjectMarker(RoomObjectType.ControlPoint, x, y, 42f, 42f, "ControlPointNeutralS", SourceName: objectName),
            "ControlPoint2" => new RoomObjectMarker(RoomObjectType.ControlPoint, x, y, 42f, 42f, "ControlPointNeutralS", SourceName: objectName),
            "ControlPoint3" => new RoomObjectMarker(RoomObjectType.ControlPoint, x, y, 42f, 42f, "ControlPointNeutralS", SourceName: objectName),
            "ControlPoint4" => new RoomObjectMarker(RoomObjectType.ControlPoint, x, y, 42f, 42f, "ControlPointNeutralS", SourceName: objectName),
            "ControlPoint5" => new RoomObjectMarker(RoomObjectType.ControlPoint, x, y, 42f, 42f, "ControlPointNeutralS", SourceName: objectName),
            "ControlPointSetupGate" => new RoomObjectMarker(RoomObjectType.ControlPointSetupGate, x, y, 60f, 6f, "sprite44", SourceName: objectName),
            "GeneratorRed" => new RoomObjectMarker(RoomObjectType.Generator, x, y, 40f, 40f, "GeneratorS", PlayerTeam.Red, objectName),
            "GeneratorBlue" => new RoomObjectMarker(RoomObjectType.Generator, x, y, 40f, 40f, "GeneratorS", PlayerTeam.Blue, objectName),
            _ => null,
        };
    }

    private static bool IsUnsupportedEntity(string objectName)
    {
        if (objectName is "SpawnPointRed" or "SpawnPointBlue" or "IntelligenceBaseRed" or "IntelligenceRed" or "IntelligenceBaseBlue" or "IntelligenceBlue")
        {
            return false;
        }

        if (objectName is "NextAreaO" or "PreviousAreaO")
        {
            return false;
        }

        return ToRoomObjectMarker(new XElement("instance",
            new XElement("object", objectName),
            new XElement("position", new XAttribute("x", 0f), new XAttribute("y", 0f)))) is null;
    }
}
