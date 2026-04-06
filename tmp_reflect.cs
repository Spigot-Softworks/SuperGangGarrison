using System;
using System.Linq;
using OpenGarrison.Client.Plugins;
using Microsoft.Xna.Framework;

Console.WriteLine("LocalDamageEvent properties:");
foreach (var p in typeof(LocalDamageEvent).GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
    Console.WriteLine($"P {p.PropertyType.FullName} {p.Name}");
Console.WriteLine("LocalDamageEvent fields:");
foreach (var f in typeof(LocalDamageEvent).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
    Console.WriteLine($"F {f.FieldType.FullName} {f.Name}");
Console.WriteLine("Vector2 properties:");
foreach (var p in typeof(Vector2).GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
    Console.WriteLine($"P {p.PropertyType.FullName} {p.Name}");
Console.WriteLine("Vector2 fields:");
foreach (var f in typeof(Vector2).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
    Console.WriteLine($"F {f.FieldType.FullName} {f.Name}");
