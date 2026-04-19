using OpenGarrison.Core;

ContentRoot.Initialize(Path.Combine(Environment.CurrentDirectory, "Core", "Content"));

if (args.Length < 2)
{
    Console.WriteLine("usage: <map> <team:red|blue> [carryingIntel:true|false]");
    return;
}

var mapName = args[0];
var team = args[1].Equals("red", StringComparison.OrdinalIgnoreCase) ? PlayerTeam.Red : PlayerTeam.Blue;
var carryingIntel = args.Length >= 3 && bool.TryParse(args[2], out var parsedCarry) && parsedCarry;

var level = SimpleLevelFactory.CreateImportedLevel(mapName);
if (level is null)
{
    Console.WriteLine($"missing map {mapName}");
    return;
}

Console.WriteLine($"map={level.Name} team={team} carryingIntel={carryingIntel}");
Console.WriteLine("blocking gates:");
foreach (var gate in level.GetBlockingTeamGates(team, carryingIntel).OrderBy(static ro => ro.Left).ThenBy(static ro => ro.Top))
{
    Console.WriteLine($"  type={gate.Type} name={gate.SourceName} team={gate.Team} left={gate.Left} top={gate.Top} right={gate.Right} bottom={gate.Bottom}");
}

Console.WriteLine("player walls:");
foreach (var wall in level.GetRoomObjects(RoomObjectType.PlayerWall).OrderBy(static ro => ro.Left).ThenBy(static ro => ro.Top))
{
    Console.WriteLine($"  name={wall.SourceName} left={wall.Left} top={wall.Top} right={wall.Right} bottom={wall.Bottom}");
}
