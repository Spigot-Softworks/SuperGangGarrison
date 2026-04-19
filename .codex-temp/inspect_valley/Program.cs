using System;
using System.Linq;
using OpenGarrison.Core;
using OpenGarrison.BotAI;
ContentRoot.Initialize(System.IO.Path.Combine(Environment.CurrentDirectory, "Core", "Content"));
var level = SimpleLevelFactory.CreateImportedLevel("Valley");
var nav = ClientBotNavPoints.Build(level!);
foreach (var id in new[]{97,98,122}) {
  if (nav.TryGetPoint(id, out var p)) {
    Console.WriteLine($"point {id} kind={p.Kind} label={p.Label} x={p.X} y={p.Y} neighbors=[{string.Join(",", p.NeighborIds.Take(12))}]");
  }
}
foreach (var ro in level!.RoomObjects.Where(ro => Math.Abs(ro.CenterX - 2331) < 140 && Math.Abs(ro.CenterY - 588) < 140)) {
  Console.WriteLine($"room type={ro.Type} name={ro.SourceName} team={ro.Team} left={ro.Left} top={ro.Top} w={ro.Width} h={ro.Height} center=({ro.CenterX},{ro.CenterY})");
}
