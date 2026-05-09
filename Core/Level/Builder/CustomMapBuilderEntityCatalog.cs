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
    string Description)
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
        Define("spawnroom", AllModes, "xscale=1;yscale=1", 74, "Spawn room", "Players can instantly respawn in this area."),
        Define("redspawn", AllModes, "", 30, "Red spawn", "Default spawn locator for the red team."),
        Define("redspawn1", CustomMapBuilderGameMode.ControlPoint | CustomMapBuilderGameMode.AttackDefenseControlPoint | CustomMapBuilderGameMode.KingOfTheHill | CustomMapBuilderGameMode.DualKingOfTheHill, "", 34, "Red spawn 1", "Red forward spawn #1."),
        Define("redspawn2", CustomMapBuilderGameMode.ControlPoint | CustomMapBuilderGameMode.AttackDefenseControlPoint | CustomMapBuilderGameMode.DualKingOfTheHill, "", 38, "Red spawn 2", "Red forward spawn #2."),
        Define("redspawn3", CustomMapBuilderGameMode.AttackDefenseControlPoint, "", 42, "Red spawn 3", "Red forward spawn #3."),
        Define("redspawn4", CustomMapBuilderGameMode.AttackDefenseControlPoint, "", 46, "Red spawn 4", "Red forward spawn #4."),
        Define("bluespawn", AllModes, "", 32, "Blue spawn", "Default spawn locator for the blue team."),
        Define("bluespawn1", CustomMapBuilderGameMode.ControlPoint | CustomMapBuilderGameMode.AttackDefenseControlPoint | CustomMapBuilderGameMode.KingOfTheHill | CustomMapBuilderGameMode.DualKingOfTheHill, "", 36, "Blue spawn 1", "Blue forward spawn #1."),
        Define("bluespawn2", CustomMapBuilderGameMode.ControlPoint | CustomMapBuilderGameMode.AttackDefenseControlPoint | CustomMapBuilderGameMode.DualKingOfTheHill, "", 40, "Blue spawn 2", "Blue forward spawn #2."),
        Define("bluespawn3", CustomMapBuilderGameMode.AttackDefenseControlPoint, "", 44, "Blue spawn 3", "Blue forward spawn #3."),
        Define("bluespawn4", CustomMapBuilderGameMode.AttackDefenseControlPoint, "", 48, "Blue spawn 4", "Blue forward spawn #4."),
        Define("redintel", CustomMapBuilderGameMode.CaptureTheFlag, "", 0, "Red intel", "The red intelligence spawn point."),
        Define("blueintel", CustomMapBuilderGameMode.CaptureTheFlag, "", 2, "Blue intel", "The blue intelligence spawn point."),
        Define("redteamgate", TeamObjectiveModes, "xscale=1;yscale=1", 84, "Red gate", "A wall that blocks blue players and bullets."),
        Define("blueteamgate", TeamObjectiveModes, "xscale=1;yscale=1", 86, "Blue gate", "A wall that blocks red players and bullets."),
        Define("redteamgate2", CustomMapBuilderGameMode.Free, "xscale=1;yscale=1", 90, "Red floor gate", "A floor that blocks blue players and bullets."),
        Define("blueteamgate2", CustomMapBuilderGameMode.Free, "xscale=1;yscale=1", 92, "Blue floor gate", "A floor that blocks red players and bullets."),
        Define("redintelgate", CustomMapBuilderGameMode.CaptureTheFlag, "xscale=1;yscale=1", 8, "Red intel gate", "A wall that blocks players carrying the red intel."),
        Define("blueintelgate", CustomMapBuilderGameMode.CaptureTheFlag, "xscale=1;yscale=1", 10, "Blue intel gate", "A wall that blocks players carrying the blue intel."),
        Define("redintelgate2", CustomMapBuilderGameMode.Free, "xscale=1;yscale=1", 4, "Red intel floor gate", "A floor that blocks players carrying the red intel."),
        Define("blueintelgate2", CustomMapBuilderGameMode.Free, "xscale=1;yscale=1", 6, "Blue intel floor gate", "A floor that blocks players carrying the blue intel."),
        Define("intelgatehorizontal", CustomMapBuilderGameMode.Free, "xscale=1;yscale=1", 94, "Intel floor gate", "A floor that blocks players carrying the intel."),
        Define("intelgatevertical", CustomMapBuilderGameMode.CaptureTheFlag, "xscale=1;yscale=1", 96, "Intel gate", "A wall that blocks players carrying the intel."),
        Define("medCabinet", TeamObjectiveModes, "xscale=1;yscale=1;heal=true;refill=true;uber=false", 64, "Cabinet", "Refills health and ammo."),
        Define("killbox", AllModes, "xscale=1;yscale=1", 58, "Kill box", "Kills a player."),
        Define("pitfall", AllModes, "xscale=1;yscale=1", 62, "Pitfall", "Kills a player."),
        Define("fragbox", AllModes, "xscale=1;yscale=1", 60, "Frag box", "Gibs a player."),
        Define("playerwall", AllModes, "xscale=1;yscale=1", 50, "Player wall", "A wall that blocks players but not bullets."),
        Define("playerwall_horizontal", CustomMapBuilderGameMode.Free, "xscale=1;yscale=1", 54, "Player floor", "A floor that blocks players but not bullets."),
        Define("bulletwall", AllModes, "xscale=1;yscale=1;distance=-1", 52, "Bullet wall", "A wall that blocks bullets but not players."),
        Define("bulletwall_horizontal", CustomMapBuilderGameMode.Free, "xscale=1;yscale=1", 56, "Bullet floor", "A floor that blocks bullets but not players."),
        Define("leftdoor", AllModes, "xscale=1;yscale=1", 102, "Left door", "Blocks players trying to go left."),
        Define("rightdoor", AllModes, "xscale=1;yscale=1", 104, "Right door", "Blocks players trying to go right."),
        Define("controlPoint1", CustomMapBuilderGameMode.ControlPoint | CustomMapBuilderGameMode.AttackDefenseControlPoint, "", 12, "CP 1", "Control point #1."),
        Define("controlPoint2", CustomMapBuilderGameMode.ControlPoint | CustomMapBuilderGameMode.AttackDefenseControlPoint, "", 14, "CP 2", "Control point #2."),
        Define("controlPoint3", CustomMapBuilderGameMode.ControlPoint | CustomMapBuilderGameMode.AttackDefenseControlPoint, "", 16, "CP 3", "Control point #3."),
        Define("controlPoint4", CustomMapBuilderGameMode.ControlPoint | CustomMapBuilderGameMode.AttackDefenseControlPoint, "", 18, "CP 4", "Control point #4."),
        Define("controlPoint5", CustomMapBuilderGameMode.ControlPoint | CustomMapBuilderGameMode.AttackDefenseControlPoint, "", 20, "CP 5", "Control point #5."),
        Define("NextAreaO", TeamObjectiveModes, "", 106, "Next area", "Marks the next arena in multi stage maps."),
        Define("CapturePoint", CustomMapBuilderGameMode.ControlPoint | CustomMapBuilderGameMode.AttackDefenseControlPoint | CustomMapBuilderGameMode.KingOfTheHill | CustomMapBuilderGameMode.DualKingOfTheHill | CustomMapBuilderGameMode.Arena, "xscale=1;yscale=1", 26, "Capture zone", "Players touching this will start capping the nearest control point."),
        Define("SetupGate", CustomMapBuilderGameMode.CaptureTheFlag | CustomMapBuilderGameMode.AttackDefenseControlPoint, "xscale=1;yscale=1", 28, "Setup gate", "Prevents players from passing during setup time."),
        Define("ArenaControlPoint", CustomMapBuilderGameMode.Arena, "", 22, "Arena CP", "Arena control point."),
        Define("GeneratorRed", CustomMapBuilderGameMode.Generator, "", 76, "Red gen", "Location of the red generator."),
        Define("GeneratorBlue", CustomMapBuilderGameMode.Generator, "", 78, "Blue gen", "Location of the blue generator."),
        Define("MoveBoxUp", AllModes, "xscale=1;yscale=1;speed=5", 66, "Move up", "Move box up."),
        Define("MoveBoxDown", AllModes, "xscale=1;yscale=1;speed=5", 68, "Move down", "Move box down."),
        Define("MoveBoxLeft", AllModes, "xscale=1;yscale=1;speed=5", 72, "Move left", "Move box left."),
        Define("MoveBoxRight", AllModes, "xscale=1;yscale=1;speed=5", 70, "Move right", "Move box right."),
        Define("KothControlPoint", CustomMapBuilderGameMode.KingOfTheHill, "", 24, "KOTH CP", "KOTH control point."),
        Define("KothRedControlPoint", CustomMapBuilderGameMode.DualKingOfTheHill, "", 98, "Red KOTH", "Red KOTH control point."),
        Define("KothBlueControlPoint", CustomMapBuilderGameMode.DualKingOfTheHill, "", 100, "Blue KOTH", "Blue KOTH control point."),
        Define("dropdownPlatform", AllModes, "xscale=1;yscale=1;resetMoveStatus=1", 80, "Drop platform", "Dropdown platform."),
        Define("foreground", AllModes, "xscale=1;yscale=1;depth=-2;fade=true;opacity=1;animationspeed=0;trigger=0;distance=0;resource=", 108, "Foreground", "Resizable foreground."),
        Define("foreground_scale", AllModes, "scale=1;depth=-2;fade=true;opacity=1;animationspeed=0;trigger=0;distance=0;resource=", 110, "Foreground scale", "Scalable foreground."),
        Define("moving_platform", AllModes, "scale=1;animationspeed=0;trigger=0;resource=;top=60;left=0;upspeed=3;downspeed=3;resetMoveStatus=1", 112, "Moving platform", "A moving platform."),
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
        string description)
    {
        return new CustomMapBuilderEntityDefinition(
            type,
            modes,
            ParseProperties(defaultProperties),
            iconFrame,
            label,
            description);
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
