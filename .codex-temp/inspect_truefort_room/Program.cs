using System;
using System.Linq;
using OpenGarrison.Core;
ContentRoot.Initialize(System.IO.Path.Combine(Environment.CurrentDirectory, "Core", "Content"));
var level = SimpleLevelFactory.CreateImportedLevel("Truefort");
foreach (var ro in level!.RoomObjects.Where(ro => Math.Abs(ro.CenterX - 320) < 180 && Math.Abs(ro.CenterY - 840) < 180).OrderBy(ro => ro.Left).ThenBy(ro => ro.Top)) {
  Console.WriteLine($"room type={ro.Type} name={ro.SourceName} team={ro.Team} left={ro.Left} top={ro.Top} right={ro.Right} bottom={ro.Bottom} center=({ro.CenterX},{ro.CenterY})");
}
