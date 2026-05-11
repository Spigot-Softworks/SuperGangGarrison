using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace OpenGarrison.Core;

[Flags]
public enum CustomMapBuilderGameMode
{
    Free = 0,
    CaptureTheFlag = 1 << 0,
    ControlPoint = 1 << 1,
    AttackDefenseControlPoint = 1 << 2,
    KingOfTheHill = 1 << 3,
    DualKingOfTheHill = 1 << 4,
    Arena = 1 << 5,
    Generator = 1 << 6,
}

public sealed record CustomMapBuilderEntityDefinition(
    string Type,
    CustomMapBuilderGameMode Modes,
    IReadOnlyDictionary<string, string> DefaultProperties,
    int IconFrame,
    string Label,
    string Description,
    string EntitySpriteName,
    int EntityImage)
{
    public CustomMapBuilderEntity CreateEntity(float x, float y)
    {
        var properties = new Dictionary<string, string>(DefaultProperties, StringComparer.OrdinalIgnoreCase);
        return CustomMapBuilderEntity.Create(Type, x, y, properties).NormalizeForEditing();
    }
}

public static class CustomMapBuilderEntityCatalog
{
    private static readonly IReadOnlyList<CustomMapBuilderEntityDefinition> DefinitionsValue =
    [
        Define("spawnroom", AllModes, "xscale=1;yscale=1", 74, "Spawn room", "Players can instantly respawn in this area.", "sprite64", 1),
        Define("redspawn", AllModes, "", 30, "Red spawn", "Default spawn locator for the red team.", "spawnS", 0),
        Define("redspawn1", CustomMapBuilderGameMode.ControlPoint | CustomMapBuilderGameMode.AttackDefenseControlPoint | CustomMapBuilderGameMode.KingOfTheHill | CustomMapBuilderGameMode.DualKingOfTheHill, "", 34, "Red spawn 1", "Red forward spawn #1.", "spawnS", 1),
        Define("redspawn2", CustomMapBuilderGameMode.ControlPoint | CustomMapBuilderGameMode.AttackDefenseControlPoint | CustomMapBuilderGameMode.DualKingOfTheHill, "", 38, "Red spawn 2", "Red forward spawn #2.", "spawnS", 2),
        Define("redspawn3", CustomMapBuilderGameMode.AttackDefenseControlPoint, "", 42, "Red spawn 3", "Red forward spawn #3.", "spawnS", 3),
        Define("redspawn4", CustomMapBuilderGameMode.AttackDefenseControlPoint, "", 46, "Red spawn 4", "Red forward spawn #4.", "spawnS", 4),
        Define("bluespawn", AllModes, "", 32, "Blue spawn", "Default spawn locator for the blue team.", "spawnS", 5),
        Define("bluespawn1", CustomMapBuilderGameMode.ControlPoint | CustomMapBuilderGameMode.AttackDefenseControlPoint | CustomMapBuilderGameMode.KingOfTheHill | CustomMapBuilderGameMode.DualKingOfTheHill, "", 36, "Blue spawn 1", "Blue forward spawn #1.", "spawnS", 6),
        Define("bluespawn2", CustomMapBuilderGameMode.ControlPoint | CustomMapBuilderGameMode.AttackDefenseControlPoint | CustomMapBuilderGameMode.DualKingOfTheHill, "", 40, "Blue spawn 2", "Blue forward spawn #2.", "spawnS", 7),
        Define("bluespawn3", CustomMapBuilderGameMode.AttackDefenseControlPoint, "", 44, "Blue spawn 3", "Blue forward spawn #3.", "spawnS", 8),
        Define("bluespawn4", CustomMapBuilderGameMode.AttackDefenseControlPoint, "", 48, "Blue spawn 4", "Blue forward spawn #4.", "spawnS", 9),
        Define("redintel", CustomMapBuilderGameMode.CaptureTheFlag, "", 0, "Red intel", "The red intelligence spawn point.", "IntelligenceRedS", 0),
        Define("blueintel", CustomMapBuilderGameMode.CaptureTheFlag, "", 2, "Blue intel", "The blue intelligence spawn point.", "IntelligenceBlueS", 0),
        Define("redteamgate", TeamObjectiveModes, "xscale=1;yscale=1", 84, "Red gate", "A wall that blocks blue players and bullets.", "sprite45", 1),
        Define("blueteamgate", TeamObjectiveModes, "xscale=1;yscale=1", 86, "Blue gate", "A wall that blocks red players and bullets.", "sprite45", 2),
        Define("redteamgate2", CustomMapBuilderGameMode.Free, "xscale=1;yscale=1", 90, "Red floor gate", "A floor that blocks blue players and bullets.", "sprite44", 1),
        Define("blueteamgate2", CustomMapBuilderGameMode.Free, "xscale=1;yscale=1", 92, "Blue floor gate", "A floor that blocks red players and bullets.", "sprite44", 2),
        Define("redintelgate", CustomMapBuilderGameMode.CaptureTheFlag, "xscale=1;yscale=1", 8, "Red intel gate", "A wall that blocks players carrying the red intel.", "sprite45", 7),
        Define("blueintelgate", CustomMapBuilderGameMode.CaptureTheFlag, "xscale=1;yscale=1", 10, "Blue intel gate", "A wall that blocks players carrying the blue intel.", "sprite45", 8),
        Define("redintelgate2", CustomMapBuilderGameMode.Free, "xscale=1;yscale=1", 4, "Red intel floor gate", "A floor that blocks players carrying the red intel.", "sprite44", 6),
        Define("blueintelgate2", CustomMapBuilderGameMode.Free, "xscale=1;yscale=1", 6, "Blue intel floor gate", "A floor that blocks players carrying the blue intel.", "sprite44", 7),
        Define("intelgatehorizontal", CustomMapBuilderGameMode.Free, "xscale=1;yscale=1", 94, "Intel floor gate", "A floor that blocks players carrying the intel.", "sprite44", 8),
        Define("intelgatevertical", CustomMapBuilderGameMode.CaptureTheFlag, "xscale=1;yscale=1", 96, "Intel gate", "A wall that blocks players carrying the intel.", "sprite45", 9),
        Define("medCabinet", TeamObjectiveModes, "xscale=1;yscale=1;heal=true;refill=true;uber=false", 64, "Cabinet", "Refills health and ammo.", "sprite74", 0),
        Define("killbox", AllModes, "xscale=1;yscale=1", 58, "Kill box", "Kills a player.", "sprite64", 2),
        Define("pitfall", AllModes, "xscale=1;yscale=1", 62, "Pitfall", "Kills a player.", "sprite64", 3),
        Define("fragbox", AllModes, "xscale=1;yscale=1", 60, "Frag box", "Gibs a player.", "sprite64", 4),
        Define("playerwall", AllModes, "xscale=1;yscale=1", 50, "Player wall", "A wall that blocks players but not bullets.", "sprite45", 3),
        Define("playerwall_horizontal", CustomMapBuilderGameMode.Free, "xscale=1;yscale=1", 54, "Player floor", "A floor that blocks players but not bullets.", "sprite44", 3),
        Define("bulletwall", AllModes, "xscale=1;yscale=1;distance=-1", 52, "Bullet wall", "A wall that blocks bullets but not players.", "sprite45", 4),
        Define("bulletwall_horizontal", CustomMapBuilderGameMode.Free, "xscale=1;yscale=1", 56, "Bullet floor", "A floor that blocks bullets but not players.", "sprite44", 4),
        Define("leftdoor", AllModes, "xscale=1;yscale=1", 102, "Left door", "Blocks players trying to go left.", "sprite45", 5),
        Define("rightdoor", AllModes, "xscale=1;yscale=1", 104, "Right door", "Blocks players trying to go right.", "sprite45", 6),
        Define("controlPoint1", CustomMapBuilderGameMode.ControlPoint | CustomMapBuilderGameMode.AttackDefenseControlPoint, "", 12, "CP 1", "Control point #1.", "ControlPointNeutralS", 0),
        Define("controlPoint2", CustomMapBuilderGameMode.ControlPoint | CustomMapBuilderGameMode.AttackDefenseControlPoint, "", 14, "CP 2", "Control point #2.", "ControlPointNeutralS", 2),
        Define("controlPoint3", CustomMapBuilderGameMode.ControlPoint | CustomMapBuilderGameMode.AttackDefenseControlPoint, "", 16, "CP 3", "Control point #3.", "ControlPointNeutralS", 3),
        Define("controlPoint4", CustomMapBuilderGameMode.ControlPoint | CustomMapBuilderGameMode.AttackDefenseControlPoint, "", 18, "CP 4", "Control point #4.", "ControlPointNeutralS", 4),
        Define("controlPoint5", CustomMapBuilderGameMode.ControlPoint | CustomMapBuilderGameMode.AttackDefenseControlPoint, "", 20, "CP 5", "Control point #5.", "ControlPointNeutralS", 5),
        Define("NextAreaO", TeamObjectiveModes, "", 106, "Next area", "Marks the next arena in multi stage maps.", "NextAreaS", 4),
        Define("CapturePoint", CustomMapBuilderGameMode.ControlPoint | CustomMapBuilderGameMode.AttackDefenseControlPoint | CustomMapBuilderGameMode.KingOfTheHill | CustomMapBuilderGameMode.DualKingOfTheHill | CustomMapBuilderGameMode.Arena, "xscale=1;yscale=1", 26, "Capture zone", "Players touching this will start capping the nearest control point.", "CaptureZoneS", 0),
        Define("SetupGate", CustomMapBuilderGameMode.CaptureTheFlag | CustomMapBuilderGameMode.AttackDefenseControlPoint, "xscale=1;yscale=1", 28, "Setup gate", "Prevents players from passing during setup time.", "SetupGateS", 0),
        Define("ArenaControlPoint", CustomMapBuilderGameMode.Arena, "", 22, "Arena CP", "Arena control point.", "ControlPointNeutralS", 0),
        Define("GeneratorRed", CustomMapBuilderGameMode.Generator, "", 76, "Red gen", "Location of the red generator.", "GeneratorRedS", 0),
        Define("GeneratorBlue", CustomMapBuilderGameMode.Generator, "", 78, "Blue gen", "Location of the blue generator.", "GeneratorBlueS", 0),
        Define("MoveBoxUp", AllModes, "xscale=1;yscale=1;speed=5", 66, "Move up", "Move box up.", "sprite64", 5),
        Define("MoveBoxDown", AllModes, "xscale=1;yscale=1;speed=5", 68, "Move down", "Move box down.", "sprite64", 6),
        Define("MoveBoxLeft", AllModes, "xscale=1;yscale=1;speed=5", 72, "Move left", "Move box left.", "sprite64", 7),
        Define("MoveBoxRight", AllModes, "xscale=1;yscale=1;speed=5", 70, "Move right", "Move box right.", "sprite64", 8),
        Define("KothControlPoint", CustomMapBuilderGameMode.KingOfTheHill, "", 24, "KOTH CP", "KOTH control point.", "ControlPointNeutralS", 0),
        Define("KothRedControlPoint", CustomMapBuilderGameMode.DualKingOfTheHill, "", 98, "Red KOTH", "Red KOTH control point.", "ControlPointRedS", 0),
        Define("KothBlueControlPoint", CustomMapBuilderGameMode.DualKingOfTheHill, "", 100, "Blue KOTH", "Blue KOTH control point.", "ControlPointBlueS", 0),
        Define("dropdownPlatform", AllModes, "xscale=1;yscale=1;resetMoveStatus=1", 80, "Drop platform", "Dropdown platform.", "sprite44", 5),
        Define("foreground", AllModes, "xscale=1;yscale=1;depth=-2;fade=true;opacity=1;animationspeed=0;trigger=0;distance=0;resource=", 108, "Foreground", "Resizable foreground.", "sprite64", 0),
        Define("foreground_scale", AllModes, "scale=1;depth=-2;fade=true;opacity=1;animationspeed=0;trigger=0;distance=0;resource=", 110, "Foreground scale", "Scalable foreground.", "sprite64", 0),
        Define("moving_platform", AllModes, "scale=1;animationspeed=0;trigger=0;resource=;top=60;left=0;upspeed=3;downspeed=3;resetMoveStatus=1", 112, "Moving platform", "A moving platform.", "sprite64", 0),
    ];

    public static IReadOnlyList<CustomMapBuilderEntityDefinition> Definitions => DefinitionsValue;

    public static bool TryGetDefinition(string type, out CustomMapBuilderEntityDefinition definition)
    {
        foreach (var candidate in DefinitionsValue)
        {
            if (candidate.Type.Equals(type, StringComparison.OrdinalIgnoreCase))
            {
                definition = candidate;
                return true;
            }
        }

        definition = default!;
        return false;
    }

    private const CustomMapBuilderGameMode AllModes =
        CustomMapBuilderGameMode.CaptureTheFlag
        | CustomMapBuilderGameMode.ControlPoint
        | CustomMapBuilderGameMode.AttackDefenseControlPoint
        | CustomMapBuilderGameMode.KingOfTheHill
        | CustomMapBuilderGameMode.DualKingOfTheHill
        | CustomMapBuilderGameMode.Arena
        | CustomMapBuilderGameMode.Generator;

    private const CustomMapBuilderGameMode TeamObjectiveModes =
        CustomMapBuilderGameMode.CaptureTheFlag
        | CustomMapBuilderGameMode.ControlPoint
        | CustomMapBuilderGameMode.AttackDefenseControlPoint
        | CustomMapBuilderGameMode.KingOfTheHill
        | CustomMapBuilderGameMode.DualKingOfTheHill
        | CustomMapBuilderGameMode.Generator;

    private static CustomMapBuilderEntityDefinition Define(
        string type,
        CustomMapBuilderGameMode modes,
        string defaultProperties,
        int iconFrame,
        string label,
        string description,
        string entitySpriteName = "",
        int entityImage = 0)
    {
        return new CustomMapBuilderEntityDefinition(
            type,
            modes,
            ParseProperties(defaultProperties),
            iconFrame,
            label,
            description,
            entitySpriteName,
            entityImage);
    }

    private static ReadOnlyDictionary<string, string> ParseProperties(string text)
    {
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(text))
        {
            return new ReadOnlyDictionary<string, string>(properties);
        }

        foreach (var part in text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separatorIndex = part.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            properties[part[..separatorIndex].Trim()] = part[(separatorIndex + 1)..].Trim();
        }

        return new ReadOnlyDictionary<string, string>(properties);
    }
}
