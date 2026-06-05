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
        Define("spawn", AllModes, "team=red;forward=false;priority=1;linkObjective=controlPoint1;useWhen=owned", 30, "Spawn", "Team spawn (red, blue, or neutral). Neutral spawns both teams. Forward spawn links to a control point and uses priority when multiple forwards are active.", "spawnS", 0),
        Define("barrier", AllModes, "xscale=1;yscale=1;redPlayers=block;bluePlayers=block;redShots=allow;blueShots=allow;redIntel=block;blueIntel=block", 50, "Barrier", "Resizable solid area. Blocks players and intel carriers by default. Set each target to Block or Allow.", "sprite45", 3),
        Define("directionalWall", AllModes, "passDirection=right;players=ignore;projectiles=ignore;xscale=1;yscale=1", 51, "Directional wall", "One-way wall. Set pass direction and whether players or projectiles are affected.", "sprite45", 3),
        Define("controlPoint", CustomMapBuilderGameMode.ControlPoint | CustomMapBuilderGameMode.AttackDefenseControlPoint, "index=1;capTimeMultiplier=1;initialOwner=modeDefault", 12, "Control point", "Neutral control point with configurable index (1-5).", "ControlPointNeutralS", 0),
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
        Define(
            TeleportMetadata.TeleportEntityType,
            AllModes,
            "xscale=1;yscale=1;team=all;teleportExit=",
            54,
            "Teleport",
            "Resizable area that teleports players to a linked teleport exit.",
            "sprite64",
            1),
        Define(
            TeleportMetadata.TeleportExitEntityType,
            AllModes,
            string.Empty,
            56,
            "Teleport exit",
            "Destination for a linked teleport entrance.",
            "spawnS",
            0),
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
        Define("CapturePoint", CustomMapBuilderGameMode.ControlPoint | CustomMapBuilderGameMode.AttackDefenseControlPoint | CustomMapBuilderGameMode.KingOfTheHill | CustomMapBuilderGameMode.DualKingOfTheHill | CustomMapBuilderGameMode.Arena, "xscale=1;yscale=1", 26, "Capture zone", "Resizable capture zone. Players capping here use the nearest control point.", "CaptureZoneS", 0),
        Define("SetupGate", CustomMapBuilderGameMode.CaptureTheFlag | CustomMapBuilderGameMode.AttackDefenseControlPoint, "xscale=1;yscale=1", 28, "Setup gate", "Prevents players from passing during setup time.", "SetupGateS", 0),
        Define("ArenaControlPoint", CustomMapBuilderGameMode.Arena, "initialOwner=modeDefault", 22, "Arena CP", "Arena control point.", "ControlPointNeutralS", 0),
        Define("GeneratorRed", CustomMapBuilderGameMode.Generator, "", 76, "Red gen", "Location of the red generator.", "GeneratorRedS", 0),
        Define("GeneratorBlue", CustomMapBuilderGameMode.Generator, "", 78, "Blue gen", "Location of the blue generator.", "GeneratorBlueS", 0),
        Define("MoveBoxUp", AllModes, "xscale=1;yscale=1;speed=5", 66, "Move up", "Move box up.", "sprite64", 5),
        Define("MoveBoxDown", AllModes, "xscale=1;yscale=1;speed=5", 68, "Move down", "Move box down.", "sprite64", 6),
        Define("MoveBoxLeft", AllModes, "xscale=1;yscale=1;speed=5", 72, "Move left", "Move box left.", "sprite64", 7),
        Define("MoveBoxRight", AllModes, "xscale=1;yscale=1;speed=5", 70, "Move right", "Move box right.", "sprite64", 8),
        Define("KothControlPoint", CustomMapBuilderGameMode.KingOfTheHill, "initialOwner=modeDefault", 24, "KOTH CP", "KOTH control point.", "ControlPointNeutralS", 0),
        Define("KothRedControlPoint", CustomMapBuilderGameMode.DualKingOfTheHill, "initialOwner=red", 98, "Red KOTH", "Red KOTH control point.", "ControlPointRedS", 0),
        Define("KothBlueControlPoint", CustomMapBuilderGameMode.DualKingOfTheHill, "initialOwner=blue", 100, "Blue KOTH", "Blue KOTH control point.", "ControlPointBlueS", 0),
        Define("dropdownPlatform", AllModes, "xscale=1;yscale=1;resetMoveStatus=1", 80, "Drop platform", "Dropdown platform.", "sprite44", 5),
        Define("foreground", AllModes, "xscale=1;yscale=1;depth=-2;fade=true;opacity=1;animationspeed=0;trigger=0;distance=0;resource=", 108, "Foreground", "Resizable foreground.", "sprite64", 0),
        Define("foreground_scale", AllModes, "scale=1;depth=-2;fade=true;opacity=1;animationspeed=0;trigger=0;distance=0;resource=", 110, "Foreground scale", "Scalable foreground.", "sprite64", 0),
        Define("moving_platform", AllModes, "scale=1;animationspeed=0;trigger=0;resource=;top=60;left=0;upspeed=3;downspeed=3;resetMoveStatus=1", 112, "Moving platform", "A moving platform.", "sprite64", 0),
        Define(
            PlayerTriggerMetadata.PlayerTriggerEntityType,
            AllModes,
            "xscale=1;yscale=1;signal=latch;team=any;intelCarriersOnly=false;nodePriority=0;logicKey=",
            50,
            "Player trigger",
            "Outputs true while matching players are inside the zone.",
            string.Empty,
            0),
        Define(
            IntelTriggerMetadata.IntelTriggerEntityType,
            CustomMapBuilderGameMode.CaptureTheFlag,
            "signal=latch;intel=any;triggerWhen=atBase;onPickup=true;onDrop=false;onCapture=false;onReset=false;nodePriority=0;logicKey=",
            12,
            "Intel trigger",
            "Outputs based on CTF intelligence state (at base, carried, dropped, pickup, capture, or reset).",
            string.Empty,
            0),
        Define(
            AreaExtensionMetadata.AreaEntityType,
            AllModes,
            "xscale=1;yscale=1;extends=",
            50,
            "Area",
            "Resizeable zone that extends another area entity (player trigger, teleport, or area).",
            string.Empty,
            0),
        Define(
            DamageableMetadata.DamageableEntityType,
            AllModes,
            "xscale=1;yscale=1;health=100;healWhen=;showHealthBar=false;blockPlayers=false;disableWhenDestroyed=true",
            50,
            "Damageable",
            "Resizable zone that blocks projectiles and takes damage from them.",
            string.Empty,
            0),
        Define(
            DamageTriggerMetadata.DamageTriggerEntityType,
            AllModes,
            "damageableEntity=;signal=latch;triggerBelowThreshold=false;triggerBelowPercent=50%;triggerOnAnyDamage=false;trueTime=0.25;triggerOnHeal=false;triggerWhenDestroyed=false;nodePriority=0;logicKey=",
            12,
            "Damage trigger",
            "Outputs true when a linked damageable crosses a health threshold or is fully healed.",
            string.Empty,
            0),
        Define(
            CustomMapCustomSpriteMetadata.CustomSpriteEntityType,
            AllModes,
            "layer=bg;zOrder=0;scale=1;image=",
            50,
            "Custom sprite",
            "Decorative sprite on BG, parallax layers, or FG. Supports activator show/hide.",
            string.Empty,
            0),
        Define(MapLogicMetadata.CpTriggerEntityType, AllModes, "signal=latch;linkObjective=controlPoint1;requiredOwner=red;nodePriority=0;logicKey=", 12, "CP Trigger", "Outputs true when the linked control point matches the required owner (red, blue, neutral, or not neutral).", string.Empty, 0),
        Define(MapLogicMetadata.GateEntityType, AllModes, "gateType=and;logicInput1=;logicInput2=;nodePriority=0;logicKey=", 12, "Logic gate", "Combines two logic inputs with AND, OR, or XOR.", string.Empty, 0),
        Define(MapLogicMetadata.NotEntityType, AllModes, "logicInput=;nodePriority=0;logicKey=", 12, "NOT gate", "Inverts a logic input.", string.Empty, 0),
        Define(
            MapLogicMetadata.RisingEdgeEntityType,
            AllModes,
            "logicInput=;nodePriority=0;logicKey=",
            12,
            "Rising edge",
            "Outputs a one-tick TRUE pulse when its input changes from false to true.",
            string.Empty,
            0),
        Define(
            MapLogicMetadata.LatchEntityType,
            AllModes,
            "logicInput=;logicReset=;nodePriority=0;logicKey=",
            12,
            "Latch",
            "Stays TRUE after any TRUE input until reset receives a rising edge.",
            string.Empty,
            0),
        Define(
            MapLogicMetadata.TimerEntityType,
            AllModes,
            "signal=latch;countdownSeconds=1;logicInput=;triggerOnStart=false;delayedTrue=true;delayedFalse=true;nodePriority=0;logicKey=",
            12,
            "Timer",
            "Outputs true after a countdown. Can start from a logic trigger or automatically at round start.",
            string.Empty,
            0),
        Define(
            MapLogicMetadata.OscillatorEntityType,
            AllModes,
            "signal=latch;trueTime=1;falseTime=1;initialValue=true;autostart=false;startWhen=;endWhen=;nodePriority=0;logicKey=",
            12,
            "Oscillator",
            "Loops TRUE and FALSE at repeating intervals. Can autostart, start from logic, or stop when logic goes true.",
            string.Empty,
            0),
        Define(
            MapLogicMetadata.ActivatorEntityType,
            AllModes,
            "activatorBehavior=disable;activatorEntity=;logicInput=;activateOnStart=false;nodePriority=0;logicKey=",
            12,
            "Activator",
            "Enables or disables a map entity when its logic input is true.",
            string.Empty,
            0),
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
