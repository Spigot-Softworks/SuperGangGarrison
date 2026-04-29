using OpenGarrison.Core;

var root = ProjectSourceLocator.FindDirectory("Core/Content") ?? ContentRoot.Path;
ContentRoot.Initialize(root);

var world = new SimulationWorld();
if (!world.TryLoadLevel("Atalia"))
{
    Console.Error.WriteLine("failed");
    return 2;
}

var level = world.Level;
Console.WriteLine($"level {level.Name} mode={world.MatchRules.Mode} bounds={level.Bounds.Width}x{level.Bounds.Height} solids={level.Solids.Count}");

Console.WriteLine("objects");
foreach (var marker in level.RoomObjects.Where(static marker => marker.CenterX >= 1100 && marker.CenterX <= 3800 && marker.CenterY >= 1000 && marker.CenterY <= 1350).OrderBy(static marker => marker.CenterY).ThenBy(static marker => marker.CenterX))
{
    Console.WriteLine($"{marker.Type} cx={marker.CenterX:0.0} cy={marker.CenterY:0.0} L={marker.Left:0.0} R={marker.Right:0.0} T={marker.Top:0.0} B={marker.Bottom:0.0} W={marker.Width:0.0} H={marker.Height:0.0}");
}

Console.WriteLine("solids");
foreach (var solid in level.Solids.Where(static solid => solid.Right >= 2200 && solid.Left <= 2850 && solid.Bottom >= 960 && solid.Top <= 1370).OrderBy(static solid => solid.Top).ThenBy(static solid => solid.Left))
{
    Console.WriteLine($"L={solid.Left:0.0} R={solid.Right:0.0} T={solid.Top:0.0} B={solid.Bottom:0.0} W={solid.Width:0.0} H={solid.Height:0.0}");
}

return 0;
