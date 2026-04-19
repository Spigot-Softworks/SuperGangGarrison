using System;
using System.Linq;
using OpenGarrison.Core;
ContentRoot.Initialize(System.IO.Path.Combine(Environment.CurrentDirectory, "Core", "Content"));
var level = SimpleLevelFactory.CreateImportedLevel("Valley");
foreach (var ro in level!.RoomObjects.Where(ro => Math.Abs(ro.CenterX - 2244) < 220 && Math.Abs(ro.CenterY - 630) < 220).OrderBy(ro => ro.Left).ThenBy(ro => ro.Top)) {
  Console.WriteLine($"room type={ro.Type} name={ro.SourceName} team={ro.Team} left={ro.Left} top={ro.Top} right={ro.Right} bottom={ro.Bottom} center=({ro.CenterX},{ro.CenterY})");
}
