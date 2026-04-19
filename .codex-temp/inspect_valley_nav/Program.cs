using System;
using System.Collections;
using System.Reflection;
using OpenGarrison.Core;
using OpenGarrison.BotAI;
ContentRoot.Initialize(System.IO.Path.Combine(Environment.CurrentDirectory, "Core", "Content"));
var level = SimpleLevelFactory.CreateImportedLevel("Valley");
var navType = typeof(ModernPracticeBotController).Assembly.GetType("OpenGarrison.BotAI.ClientBotNavPoints")!;
var build = navType.GetMethod("Build", BindingFlags.Public | BindingFlags.Static, null, new[]{ typeof(SimpleLevel) }, null)!;
var nav = build.Invoke(null, new object?[]{ level! })!;
var tryGetPoint = navType.GetMethod("TryGetPoint", BindingFlags.Public | BindingFlags.Instance)!;
var tryGetOutgoing = navType.GetMethod("TryGetOutgoingConnections", BindingFlags.Public | BindingFlags.Instance)!;
void DumpPoint(int id) {
  var pointArgs = new object?[]{ id, null };
  if (!(bool)tryGetPoint.Invoke(nav, pointArgs)!) { Console.WriteLine($"missing {id}"); return; }
  var p = pointArgs[1]!;
  var t = p.GetType();
  string S(string name) => t.GetProperty(name)!.GetValue(p)?.ToString() ?? "";
  Console.WriteLine($"point {id} x={S("X")} y={S("Y")} kind={S("Kind")} label={S("Label")} rev=[{string.Join(",", (IEnumerable)(t.GetProperty("ReverseOnlyBlockedFromNodeIds")!.GetValue(p) as IEnumerable ?? Array.Empty<object>()))}]");
  var edgeArgs = new object?[]{ id, null };
  if ((bool)tryGetOutgoing.Invoke(nav, edgeArgs)!) {
    foreach (var e in (IEnumerable)edgeArgs[1]!) {
      var et = e.GetType();
      Console.WriteLine($"  edge to={et.GetProperty("ToNodeId")!.GetValue(e)} kind={et.GetProperty("TraversalKind")!.GetValue(e)}");
    }
  }
}
foreach (var id in new[]{97,98,122}) DumpPoint(id);
