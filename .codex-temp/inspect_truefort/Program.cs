using System;
using System.Linq;
using OpenGarrison.Core;
using OpenGarrison.BotAI;
ContentRoot.Initialize(System.IO.Path.Combine(Environment.CurrentDirectory, "Core", "Content"));
var level = SimpleLevelFactory.CreateImportedLevel("Truefort");
var nav = ClientBotNavPoints.Build(level!);
foreach (var id in new[]{232,233,167,247}) {
  if (nav.TryGetPoint(id, out var p)) {
    Console.WriteLine($"point {id} kind={p.Kind} label={p.Label} x={p.X} y={p.Y} neighbors=[{string.Join(",", p.NeighborIds.Take(12))}]");
  }
}
foreach (var ro in level!.RoomObjects.Where(ro => Math.Abs(ro.CenterX - 320) < 120 && Math.Abs(ro.CenterY - 840) < 120)) {
  Console.WriteLine($"room type={ro.Type} name={ro.SourceName} team={ro.Team} left={ro.Left} top={ro.Top} w={ro.Width} h={ro.Height} center=({ro.CenterX},{ro.CenterY})");
}
