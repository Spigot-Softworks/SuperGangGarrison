using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using OpenGarrison.Core;
using OpenGarrison.BotAI;
ContentRoot.Initialize(System.IO.Path.Combine(Environment.CurrentDirectory, "Core", "Content"));
var level = SimpleLevelFactory.CreateImportedLevel("Truefort");
var navType = typeof(ModernPracticeBotController).Assembly.GetType("OpenGarrison.BotAI.ClientBotNavPoints")!;
var build = navType.GetMethod("Build", BindingFlags.Public | BindingFlags.Static, null, new[]{ typeof(SimpleLevel) }, null)!;
var nav = build.Invoke(null, new object?[]{ level! })!;
var tryGetPoint = navType.GetMethod("TryGetPoint", BindingFlags.Public | BindingFlags.Instance)!;
var tryGetOutgoing = navType.GetMethod("TryGetOutgoingConnections", BindingFlags.Public | BindingFlags.Instance)!;
void DumpPoint(int id) {
  var pointArgs = new object?[]{ id, null };
  if (!(bool)tryGetPoint.Invoke(nav, pointArgs)!) { Console.WriteLine($"missing {id}"); return; }
  var p = pointArgs[1]!; var pt=p.GetType();
  var rev=(IEnumerable)(pt.GetProperty("ReverseOnlyBlockedFromNodeIds")!.GetValue(p) as IEnumerable ?? Array.Empty<object>());
  Console.WriteLine($"point {id} x={pt.GetProperty("X")!.GetValue(p)} y={pt.GetProperty("Y")!.GetValue(p)} kind={pt.GetProperty("Kind")!.GetValue(p)} rev=[{string.Join(",", rev.Cast<object>())}]");
  var edgeArgs = new object?[]{ id, null };
  if ((bool)tryGetOutgoing.Invoke(nav, edgeArgs)!) {
    foreach (var e in (IEnumerable)edgeArgs[1]!) {
      var et=e.GetType();
      Console.WriteLine($"  edge to={et.GetProperty("ToNodeId")!.GetValue(e)} kind={et.GetProperty("Kind")!.GetValue(e)} cost={et.GetProperty("Cost")!.GetValue(e)}");
    }
  }
}
foreach (var id in new[]{232,233,167,247}) DumpPoint(id);
