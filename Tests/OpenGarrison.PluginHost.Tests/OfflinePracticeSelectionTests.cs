using System.Collections;
using System.Reflection;
using OpenGarrison.Client;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class OfflinePracticeSelectionTests
{
    [Fact]
    public void PracticeMapSelectionIncludesEveryStockMap()
    {
        var entries = BuildPracticeMapEntries();
        var levelNames = entries
            .Select(GetPracticeMapLevelName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var definition in OpenGarrisonStockMapCatalog.Definitions)
        {
            Assert.Contains(definition.LevelName, levelNames);
        }
    }

    [Fact]
    public void LastToDieRotationIncludesConflictAfterNavigationRepair()
    {
        var conflictEntry = CreatePracticeMapEntry("Conflict", GameModeKind.KingOfTheHill);
        var method = typeof(Game1).GetMethod("IsEligibleLastToDieRotationMap", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        Assert.True((bool)method.Invoke(null, [conflictEntry])!);
    }

    private static IEnumerable<object> BuildPracticeMapEntries()
    {
        var setupStateType = typeof(Game1).GetNestedType("PracticeSetupState", BindingFlags.NonPublic);
        var method = setupStateType?.GetMethod("BuildMapEntries", BindingFlags.Public | BindingFlags.Static);

        Assert.NotNull(method);
        return ((IEnumerable)method.Invoke(null, null)!).Cast<object>();
    }

    private static object CreatePracticeMapEntry(string levelName, GameModeKind mode)
    {
        var entryType = typeof(Game1).GetNestedType("PracticeMapEntry", BindingFlags.NonPublic);
        var constructor = entryType?.GetConstructor(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(string), typeof(string), typeof(GameModeKind), typeof(bool)],
            modifiers: null);

        Assert.NotNull(constructor);
        return constructor.Invoke([levelName, levelName, mode, false]);
    }

    private static string GetPracticeMapLevelName(object entry)
    {
        var property = entry.GetType().GetProperty("LevelName", BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(property);
        return (string)property.GetValue(entry)!;
    }
}
