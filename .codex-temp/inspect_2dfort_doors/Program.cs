using System;
using System.Linq;
using OpenGarrison.Core;
ContentRoot.Initialize(System.IO.Path.Combine(Environment.CurrentDirectory, "Core", "Content"));
var level = SimpleLevelFactory.CreateImportedLevel("TwodFortTwo");
foreach (var ro in level!.RoomObjects.Where(ro => ro.Type == RoomObjectType.PlayerWall || ro.Type == RoomObjectType.TeamGate || ro.Type == RoomObjectType.IntelGate).OrderBy(ro => ro.Left).ThenBy(ro => ro.Top)) {
  Console.WriteLine($"room type={ro.Type} name={ro.SourceName} team={ro.Team} left={ro.Left} top={ro.Top} right={ro.Right} bottom={ro.Bottom} center=({ro.CenterX},{ro.CenterY})");
}
