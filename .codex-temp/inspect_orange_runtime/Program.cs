using OpenGarrison.Core;

ContentRoot.Initialize(Path.Combine(Environment.CurrentDirectory, "Core", "Content"));

var level = SimpleLevelFactory.CreateImportedLevel("Orange");
if (level is null)
{
    Console.WriteLine("missing Orange");
    return;
}

const float playerX = 4404f;
const float playerY = 1122f;
const float feetY = 1146f;
const int horizontal = -1;
const float targetX = 4391f;
const float targetY = 1164f;
const float horizontalSpeed = 0f;
const float probeRadiusX = 120f;
const float probeRadiusY = 140f;

var staticSolids = new List<LevelSolid>(level.Solids);
foreach (var roomObject in level.RoomObjects)
{
    if (roomObject.Type == RoomObjectType.PlayerWall
        || (roomObject.Type == RoomObjectType.ControlPointSetupGate && level.ControlPointSetupGatesActive))
    {
        staticSolids.Add(new LevelSolid(roomObject.Left, roomObject.Top, roomObject.Width, roomObject.Height));
    }
}

var runtimeSolids = new List<LevelSolid>(level.Solids);
foreach (var gate in level.GetBlockingTeamGates(PlayerTeam.Blue, carryingIntel: true))
{
    runtimeSolids.Add(new LevelSolid(gate.Left, gate.Top, gate.Width, gate.Height));
}

foreach (var wall in level.GetRoomObjects(RoomObjectType.PlayerWall))
{
    runtimeSolids.Add(new LevelSolid(wall.Left, wall.Top, wall.Width, wall.Height));
}

Console.WriteLine($"map={level.Name}");
Console.WriteLine($"stall pos=({playerX},{playerY}) feetY={feetY} target=({targetX},{targetY}) horizontal={horizontal}");
Console.WriteLine();

Console.WriteLine("nearby room objects:");
foreach (var roomObject in level.RoomObjects
             .Where(ro => Math.Abs(ro.CenterX - playerX) <= probeRadiusX && Math.Abs(ro.CenterY - feetY) <= probeRadiusY)
             .OrderBy(ro => ro.Left)
             .ThenBy(ro => ro.Top))
{
    Console.WriteLine($"  room type={roomObject.Type} name={roomObject.SourceName} team={roomObject.Team} left={roomObject.Left} top={roomObject.Top} right={roomObject.Right} bottom={roomObject.Bottom}");
}

Console.WriteLine();
Console.WriteLine("nearby static solids:");
DumpNearbySolids(staticSolids, playerX, feetY, probeRadiusX, probeRadiusY);

Console.WriteLine();
Console.WriteLine("nearby runtime solids (Blue carrying intel):");
DumpNearbySolids(runtimeSolids, playerX, feetY, probeRadiusX, probeRadiusY);

Console.WriteLine();
Console.WriteLine("probe results:");
DumpProbe("ground_contact", staticSolids, runtimeSolids, playerX - 6f, feetY + 3f, playerX + 6f, feetY + 3f);
DumpProbe("forward_foot_block", staticSolids, runtimeSolids, playerX + (7f * horizontal), feetY - 1f, playerX + (7f * horizontal), feetY + 4f);
DumpProbe("ground_ahead", staticSolids, runtimeSolids, playerX + (15f * horizontal), feetY, playerX + (15f * horizontal), feetY + 12f);
DumpProbe("hole_flat_line", staticSolids, runtimeSolids, targetX - (3f * horizontal), targetY, playerX + horizontalSpeed + (2f * horizontal), feetY);
DumpProbe("cp_line", staticSolids, runtimeSolids, playerX, playerY, 4404f, 1134f);
DumpProbe("np_line", staticSolids, runtimeSolids, playerX, playerY, targetX, targetY - 12f);

static void DumpNearbySolids(IEnumerable<LevelSolid> solids, float centerX, float centerY, float radiusX, float radiusY)
{
    foreach (var solid in solids
                 .Where(s => s.Right >= centerX - radiusX
                             && s.Left <= centerX + radiusX
                             && s.Bottom >= centerY - radiusY
                             && s.Top <= centerY + radiusY)
                 .OrderBy(s => s.Left)
                 .ThenBy(s => s.Top))
    {
        Console.WriteLine($"  solid left={solid.Left} top={solid.Top} right={solid.Right} bottom={solid.Bottom}");
    }
}

static void DumpProbe(
    string name,
    IReadOnlyList<LevelSolid> staticSolids,
    IReadOnlyList<LevelSolid> runtimeSolids,
    float x1,
    float y1,
    float x2,
    float y2)
{
    Console.WriteLine(
        $"  {name}: line=({x1},{y1})->({x2},{y2}) static={LineHits(staticSolids, x1, y1, x2, y2)} runtime={LineHits(runtimeSolids, x1, y1, x2, y2)}");
}

static bool LineHits(IReadOnlyList<LevelSolid> solids, float x1, float y1, float x2, float y2)
{
    var dx = x2 - x1;
    var dy = y2 - y1;
    var steps = (int)Math.Ceiling(Math.Max(Math.Abs(dx), Math.Abs(dy)));
    if (steps <= 0)
    {
        return ContainsPoint(solids, x1, y1);
    }

    for (var step = 0; step <= steps; step += 1)
    {
        var t = step / (float)steps;
        var x = x1 + (dx * t);
        var y = y1 + (dy * t);
        if (ContainsPoint(solids, x, y))
        {
            return true;
        }
    }

    return false;
}

static bool ContainsPoint(IReadOnlyList<LevelSolid> solids, float x, float y)
{
    for (var index = 0; index < solids.Count; index += 1)
    {
        var solid = solids[index];
        if (x >= solid.Left && x <= solid.Right && y >= solid.Top && y <= solid.Bottom)
        {
            return true;
        }
    }

    return false;
}
