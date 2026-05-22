using System.Collections;
using System;
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

    [Theory]
    [InlineData("ctf_avanti", "Avanti")]
    [InlineData("ctf_classicwell", "ClassicWell")]
    [InlineData("ctf_orange", "Orange")]
    [InlineData("dkoth_atalia", "Atalia")]
    [InlineData("dkoth_sixties", "Sixties")]
    [InlineData("gen_destroy", "Destroy")]
    public void PracticeMapSelectionDoesNotExposeHiddenShippedMaps(string iniKey, string levelName)
    {
        var entries = BuildPracticeMapEntries();
        var levelNames = entries
            .Select(GetPracticeMapLevelName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain(levelName, levelNames);
        Assert.DoesNotContain(
            OpenGarrisonStockMapCatalog.GetOrderedIncludedMapLevelNames(OpenGarrisonStockMapCatalog.CreateDefaultEntries()),
            candidate => string.Equals(candidate, levelName, StringComparison.OrdinalIgnoreCase));
        Assert.True(OpenGarrisonStockMapCatalog.TryGetDefinition(iniKey, out var hiddenDefinition));
        Assert.Equal(levelName, hiddenDefinition.LevelName);
    }

    [Fact]
    public void DefaultServerMapRotationUsesConfiguredStockOrder()
    {
        var rotation = OpenGarrisonStockMapCatalog.GetOrderedIncludedMapLevelNames(OpenGarrisonStockMapCatalog.CreateDefaultEntries());

        Assert.Equal(
            [
                "Harvest",
                "Gallery",
                "Dirtbowl",
                "Egypt",
                "Valley",
                "Eiger",
                "Waterway",
                "Conflict",
                "Lumberyard",
                "Montane",
                "Truefort",
                "Corinth",
            ],
            rotation);
    }

    [Fact]
    public void DefaultPracticeMapSelectionUsesHarvest()
    {
        var setupStateType = typeof(Game1).GetNestedType("PracticeSetupState", BindingFlags.NonPublic);
        var state = Activator.CreateInstance(setupStateType!, nonPublic: true)!;
        var mapEntriesProperty = setupStateType!.GetProperty("MapEntries", BindingFlags.Instance | BindingFlags.Public);
        var buildMapEntriesMethod = setupStateType.GetMethod("BuildMapEntries", BindingFlags.Public | BindingFlags.Static);
        var normalizeMethod = setupStateType.GetMethod("Normalize", BindingFlags.Instance | BindingFlags.Public);
        var selectedEntryMethod = setupStateType.GetMethod("GetSelectedMapEntry", BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(mapEntriesProperty);
        Assert.NotNull(buildMapEntriesMethod);
        Assert.NotNull(normalizeMethod);
        Assert.NotNull(selectedEntryMethod);

        mapEntriesProperty.SetValue(state, buildMapEntriesMethod.Invoke(null, null));
        normalizeMethod.Invoke(state, null);

        var selectedEntry = selectedEntryMethod.Invoke(state, null);
        Assert.NotNull(selectedEntry);
        Assert.Equal("Harvest", GetPracticeMapLevelName(selectedEntry));
    }

    [Fact]
    public void DefaultLastToDieRotationRemainsKingOfTheHillOnly()
    {
        var harvestEntry = CreatePracticeMapEntry("Harvest", GameModeKind.KingOfTheHill);
        var conflictEntry = CreatePracticeMapEntry("Conflict", GameModeKind.CaptureTheFlag);
        var practiceMapEntryType = typeof(Game1).GetNestedType("PracticeMapEntry", BindingFlags.NonPublic);
        var method = typeof(Game1).GetMethod(
            "IsEligibleLastToDieRotationMap",
            BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            types: [practiceMapEntryType!],
            modifiers: null);

        Assert.NotNull(practiceMapEntryType);
        Assert.NotNull(method);
        Assert.True((bool)method.Invoke(null, [harvestEntry])!);
        Assert.False((bool)method.Invoke(null, [conflictEntry])!);
    }

    [Fact]
    public void EngineerLastToDieRotationUsesConfiguredMixedMapPool()
    {
        var engineerKind = GetLastToDieSurvivorKind("Engineer");
        var practiceMapEntryType = typeof(Game1).GetNestedType("PracticeMapEntry", BindingFlags.NonPublic);
        var eligibilityMethod = typeof(Game1).GetMethod(
            "IsEligibleLastToDieRotationMap",
            BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            types:
            [
                engineerKind.GetType(),
                practiceMapEntryType!
            ],
            modifiers: null);

        Assert.NotNull(practiceMapEntryType);
        Assert.NotNull(eligibilityMethod);
        Assert.True(InvokeLastToDieRotationEligibility(eligibilityMethod, engineerKind, CreatePracticeMapEntry("Harvest", GameModeKind.KingOfTheHill)));
        Assert.True(InvokeLastToDieRotationEligibility(eligibilityMethod, engineerKind, CreatePracticeMapEntry("Gallery", GameModeKind.KingOfTheHill)));
        Assert.True(InvokeLastToDieRotationEligibility(eligibilityMethod, engineerKind, CreatePracticeMapEntry("TwodFortTwo", GameModeKind.CaptureTheFlag)));
        Assert.True(InvokeLastToDieRotationEligibility(eligibilityMethod, engineerKind, CreatePracticeMapEntry("Conflict", GameModeKind.CaptureTheFlag)));
        Assert.True(InvokeLastToDieRotationEligibility(eligibilityMethod, engineerKind, CreatePracticeMapEntry("Eiger", GameModeKind.CaptureTheFlag)));
        Assert.False(InvokeLastToDieRotationEligibility(eligibilityMethod, engineerKind, CreatePracticeMapEntry("Valley", GameModeKind.KingOfTheHill)));
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

    private static object GetLastToDieSurvivorKind(string name)
    {
        var enumType = typeof(Game1).GetNestedType("LastToDieSurvivorKind", BindingFlags.NonPublic);

        Assert.NotNull(enumType);
        return Enum.Parse(enumType, name);
    }

    private static bool InvokeLastToDieRotationEligibility(MethodInfo method, object survivorKind, object entry)
    {
        return (bool)method.Invoke(null, [survivorKind, entry])!;
    }
}
