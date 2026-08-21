using System.Collections;
using System.Collections.Generic;
using System;
using System.Reflection;
using OpenGarrison.Client;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class OfflinePracticeSelectionTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(3, 1)]
    [InlineData(4, 2)]
    [InlineData(7, 2)]
    [InlineData(8, 3)]
    public void NativePracticeThinksBotsInBoundedRosterBatches(int controlledBotCount, int expectedBatchSize)
    {
        Assert.Equal(expectedBatchSize, Game1.GetPracticeBotThinkBatchSize(controlledBotCount));
    }

    [Fact]
    public void HeavyDashPredictionFallbackUsesStockBurstDash()
    {
        var method = typeof(Game1).GetMethod(
            "GetPredictedHeavyGhostDashUseMomentum",
            BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            [typeof(GameplayAbilityDefinition)],
            modifiers: null);

        Assert.NotNull(method);
        var useMomentum = (bool)method!.Invoke(null, [null])!;

        Assert.False(useMomentum);
    }

    [Fact]
    public void PracticeMapSelectionUsesCuratedBuiltInOrderAndHidesEverythingElse()
    {
        var entries = BuildPracticeMapEntries();
        (string LevelName, string DisplayName)[] expected =
        [
            (LevelName: "cp_coldfront_js", DisplayName: "Coldfront"),
            (LevelName: "Kulay", DisplayName: "Kulay"),
            (LevelName: "Harvest", DisplayName: "Harvest"),
            (LevelName: "Docking", DisplayName: "Docking"),
            (LevelName: "Conflict", DisplayName: "Conflict"),
            (LevelName: "Gallery", DisplayName: "Gallery"),
            (LevelName: "Valley", DisplayName: "Valley"),
            (LevelName: "Corinth", DisplayName: "Corinth"),
            (LevelName: "Dirtbowl", DisplayName: "Dirtbowl"),
            (LevelName: "Egypt", DisplayName: "Egypt"),
            (LevelName: "Eiger", DisplayName: "Eiger"),
            (LevelName: "Truefort", DisplayName: "Truefort"),
            (LevelName: "Waterway", DisplayName: "Waterway"),
            (LevelName: "koth_bayou", DisplayName: "Bayou"),
            (LevelName: "koth_cdragon", DisplayName: "Cdragon"),
            (LevelName: "koth_eureka", DisplayName: "Eureka"),
            (LevelName: "koth_AMERICA", DisplayName: "America"),
            (LevelName: "3cp_kistra", DisplayName: "Kistra"),
            (LevelName: "cp_gully", DisplayName: "Gully"),
            (LevelName: "koth_ravine", DisplayName: "Ravine"),
            (LevelName: "koth_standoff", DisplayName: "Standoff"),
            (LevelName: "koth_crab_v2", DisplayName: "Crab"),
            (LevelName: "koth_thundermountain_v2", DisplayName: "Thundermountain_v2"),
            (LevelName: "koth_high5tower_a1", DisplayName: "Hightower"),
            (LevelName: "koth_heist", DisplayName: "Heist"),
            (LevelName: "koth_drill_v3", DisplayName: "Drill"),
            (LevelName: "koth_nightly", DisplayName: "Nightly"),
        ];

        Assert.Equal(expected.Length, entries.Count());
        Assert.Equal(expected.Select(item => item.LevelName), entries.Select(GetPracticeMapLevelName));
        Assert.Equal(expected.Select(item => item.DisplayName), entries.Select(GetPracticeMapDisplayName));
        Assert.All(entries, entry => Assert.False(GetPracticeMapIsCustom(entry), GetPracticeMapLevelName(entry)));
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

    [Theory]
    [InlineData("vip_dirtbowl", "Dirtbowl (VIP)")]
    [InlineData("vip_dustbowl", "Dirtbowl (VIP)")]
    [InlineData("vip_egypt", "Egypt (VIP)")]
    public void StockMapCatalogResolvesVipRotationTokens(string token, string displayName)
    {
        Assert.True(OpenGarrisonStockMapCatalog.TryGetDefinition(token, out var definition));
        Assert.Equal(GameModeKind.Vip, definition.Mode);
        Assert.StartsWith("vip_", definition.IniKey, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(displayName, definition.DisplayName);
    }

    [Fact]
    public void ServerMapRotationPersistencePreservesVipAndDuplicateEntries()
    {
        var entries = OpenGarrisonStockMapCatalog.CreateDefaultEntries();
        foreach (var entry in entries)
        {
            entry.Order = 0;
        }

        var dirtbowl = Assert.Single(entries.Where(entry => string.Equals(entry.IniKey, "cp_dirtbowl", StringComparison.OrdinalIgnoreCase)));
        var vipDirtbowl = Assert.Single(entries.Where(entry => string.Equals(entry.IniKey, "vip_dirtbowl", StringComparison.OrdinalIgnoreCase)));
        dirtbowl.Order = 1;
        vipDirtbowl.Order = 2;
        var duplicateDirtbowl = dirtbowl.Clone();
        duplicateDirtbowl.IsPlaylistClone = true;
        duplicateDirtbowl.Order = 3;
        entries.Add(duplicateDirtbowl);

        var ini = new IniConfigurationFile();
        OpenGarrisonStockMapCatalog.SaveTo(ini, entries);
        var loaded = OpenGarrisonStockMapCatalog.LoadFrom(ini, legacySelectedMap: string.Empty);

        Assert.Equal(
            ["Dirtbowl", "vip_dirtbowl", "Dirtbowl"],
            OpenGarrisonStockMapCatalog.GetOrderedIncludedMapLevelNames(loaded));
        Assert.Equal(1, loaded.Count(entry => entry.IsPlaylistClone && string.Equals(entry.IniKey, "cp_dirtbowl", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void HostSetupPlaylistExportWritesDuplicateAndVipTokens()
    {
        var entries = OpenGarrisonStockMapCatalog.CreateDefaultEntries();
        foreach (var entry in entries)
        {
            entry.Order = 0;
        }

        var dirtbowl = Assert.Single(entries.Where(entry => string.Equals(entry.IniKey, "cp_dirtbowl", StringComparison.OrdinalIgnoreCase)));
        var vipDirtbowl = Assert.Single(entries.Where(entry => string.Equals(entry.IniKey, "vip_dirtbowl", StringComparison.OrdinalIgnoreCase)));
        dirtbowl.Order = 1;
        vipDirtbowl.Order = 2;
        var duplicateDirtbowl = dirtbowl.Clone();
        duplicateDirtbowl.IsPlaylistClone = true;
        duplicateDirtbowl.Order = 3;

        var path = Path.Combine(Path.GetTempPath(), $"opengarrison-playlist-{Guid.NewGuid():N}.txt");
        try
        {
            HostSetupPlaylistFileIO.WritePlaylist(path, [dirtbowl, vipDirtbowl, duplicateDirtbowl]);

            var playlistLines = File.ReadAllLines(path)
                .Where(line => !line.StartsWith('#') && !string.IsNullOrWhiteSpace(line))
                .ToArray();
            Assert.Equal(["cp_dirtbowl", "vip_dirtbowl", "cp_dirtbowl"], playlistLines);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
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
    public void PracticeMapBrowserFiltersBySearchModeAndMapType()
    {
        var setupStateType = typeof(Game1).GetNestedType("PracticeSetupState", BindingFlags.NonPublic);
        var entryType = typeof(Game1).GetNestedType("PracticeMapEntry", BindingFlags.NonPublic);
        var state = Activator.CreateInstance(setupStateType!, nonPublic: true)!;
        var mapEntriesProperty = setupStateType!.GetProperty("MapEntries", BindingFlags.Instance | BindingFlags.Public);
        var listType = typeof(List<>).MakeGenericType(entryType!);
        var entries = (IList)Activator.CreateInstance(listType)!;
        entries.Add(CreatePracticeMapEntry("Harvest", GameModeKind.KingOfTheHill));
        entries.Add(CreatePracticeMapEntry("Conflict", GameModeKind.CaptureTheFlag));
        entries.Add(CreatePracticeMapEntry("downloaded_koth", GameModeKind.KingOfTheHill, isCustomMap: true));

        Assert.NotNull(mapEntriesProperty);
        mapEntriesProperty.SetValue(state, entries);

        SetPracticeMapBrowserProperty(setupStateType, state, "AvailableMapNameFilterBuffer", "har");
        Assert.Equal(["Harvest"], GetPracticeAvailableMapLevelNames(setupStateType, state));

        SetPracticeMapBrowserProperty(setupStateType, state, "AvailableMapNameFilterBuffer", string.Empty);
        SetPracticeMapBrowserProperty(setupStateType, state, "AvailableMapModeFilter", GameModeKind.KingOfTheHill);
        SetPracticeMapBrowserProperty(setupStateType, state, "IncludeCustomMaps", false);
        SetPracticeMapBrowserProperty(setupStateType, state, "IncludeBaseMaps", true);
        Assert.Equal(["Harvest"], GetPracticeAvailableMapLevelNames(setupStateType, state));

        SetPracticeMapBrowserProperty(setupStateType, state, "IncludeCustomMaps", true);
        SetPracticeMapBrowserProperty(setupStateType, state, "IncludeBaseMaps", false);
        SetPracticeMapBrowserProperty(setupStateType, state, "ShowCustomMaps", true);
        Assert.Equal(["downloaded_koth"], GetPracticeAvailableMapLevelNames(setupStateType, state));
    }

    [Fact]
    public void PracticeMapBrowserKeepsCustomMapsBehindTheCustomMapsSourceButton()
    {
        var setupStateType = typeof(Game1).GetNestedType("PracticeSetupState", BindingFlags.NonPublic);
        var entryType = typeof(Game1).GetNestedType("PracticeMapEntry", BindingFlags.NonPublic);
        var state = Activator.CreateInstance(setupStateType!, nonPublic: true)!;
        var mapEntriesProperty = setupStateType!.GetProperty("MapEntries", BindingFlags.Instance | BindingFlags.Public);
        var listType = typeof(List<>).MakeGenericType(entryType!);
        var entries = (IList)Activator.CreateInstance(listType)!;
        entries.Add(CreatePracticeMapEntry("Harvest", GameModeKind.KingOfTheHill));
        entries.Add(CreatePracticeMapEntry("downloaded_koth", GameModeKind.KingOfTheHill, isCustomMap: true));
        mapEntriesProperty!.SetValue(state, entries);

        Assert.Equal(["Harvest"], GetPracticeAvailableMapLevelNames(setupStateType, state));

        SetPracticeMapBrowserProperty(setupStateType, state, "ShowCustomMaps", true);
        Assert.Equal(["downloaded_koth"], GetPracticeAvailableMapLevelNames(setupStateType, state));
    }

    [Fact]
    public void PracticeMapBrowserSectionsUseRequestedMapGroups()
    {
        var setupStateType = typeof(Game1).GetNestedType("PracticeSetupState", BindingFlags.NonPublic);
        var state = Activator.CreateInstance(setupStateType!, nonPublic: true)!;
        var mapEntriesProperty = setupStateType!.GetProperty("MapEntries", BindingFlags.Instance | BindingFlags.Public);
        var buildMapEntriesMethod = setupStateType.GetMethod("BuildMapEntries", BindingFlags.Public | BindingFlags.Static);
        var sectionType = setupStateType.GetNestedType("PracticeMapBrowserSection", BindingFlags.Public | BindingFlags.NonPublic);
        var setSectionMethod = setupStateType.GetMethod("SetMapBrowserSection", BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(mapEntriesProperty);
        Assert.NotNull(buildMapEntriesMethod);
        Assert.NotNull(sectionType);
        Assert.NotNull(setSectionMethod);
        mapEntriesProperty.SetValue(state, buildMapEntriesMethod.Invoke(null, null));

        var sgg = Enum.Parse(sectionType!, "SuperGangGarrison");
        setSectionMethod.Invoke(state, [sgg]);
        Assert.Equal(["cp_coldfront_js", "Kulay", "Harvest", "Docking", "Conflict"], GetPracticeAvailableMapLevelNames(setupStateType, state));

        var classic = Enum.Parse(sectionType!, "Classic");
        setSectionMethod.Invoke(state, [classic]);
        Assert.Equal(
            ["Gallery", "Valley", "Corinth", "Dirtbowl", "Egypt", "Eiger", "Truefort", "Waterway", "koth_bayou", "koth_cdragon", "koth_eureka", "koth_AMERICA", "3cp_kistra", "cp_gully", "koth_ravine", "koth_standoff", "koth_crab_v2", "koth_thundermountain_v2", "koth_high5tower_a1", "koth_heist", "koth_drill_v3", "koth_nightly"],
            GetPracticeAvailableMapLevelNames(setupStateType, state));
    }

    [Fact]
    public void PracticeMapSelectionDoesNotExposeVipOrUnlistedMaps()
    {
        var entries = BuildPracticeMapEntries();
        var levelNames = entries
            .Select(GetPracticeMapLevelName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("vip_dirtbowl", levelNames);
        Assert.DoesNotContain("TwodFortTwo", levelNames);
        Assert.DoesNotContain("Mantic", levelNames);
        Assert.DoesNotContain("Lumberyard", levelNames);
        Assert.DoesNotContain("Montane", levelNames);
    }

    [Fact]
    public void AppendVipMapDuplicatesAddsVipEntryForEveryCpPrefixedMap()
    {
        var entries = new List<OpenGarrisonMapRotationEntry>
        {
            new()
            {
                IniKey = "cp_dirtbowl",
                LevelName = "Dirtbowl",
                DisplayName = "Dirtbowl",
                Mode = GameModeKind.ControlPoint,
            },
            new()
            {
                IniKey = "cp_egypt",
                LevelName = "Egypt",
                DisplayName = "Egypt",
                Mode = GameModeKind.ControlPoint,
            },
            new()
            {
                IniKey = "cp_customtest",
                LevelName = "cp_customtest",
                DisplayName = "Custom CP",
                Mode = GameModeKind.ControlPoint,
                IsCustomMap = true,
            },
        };

        OpenGarrisonStockMapCatalog.AppendVipMapDuplicates(entries);

        Assert.Contains(entries, entry => string.Equals(entry.IniKey, "vip_dirtbowl", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(entries, entry => string.Equals(entry.IniKey, "vip_egypt", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(entries, entry => string.Equals(entry.IniKey, "vip_customtest", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void StockCpPrefixedDefinitionsAllReceiveVipDuplicatesInDefaultEntries()
    {
        var entries = OpenGarrisonStockMapCatalog.CreateDefaultEntries();
        foreach (var definition in OpenGarrisonStockMapCatalog.Definitions.Where(definition =>
                     OpenGarrisonStockMapCatalog.IsCpPrefixedIniKey(definition.IniKey)))
        {
            var vipIniKey = OpenGarrisonStockMapCatalog.GetVipIniKey(definition);
            Assert.Contains(
                entries,
                entry => string.Equals(entry.IniKey, vipIniKey, StringComparison.OrdinalIgnoreCase)
                    && entry.Mode == GameModeKind.Vip);
        }
    }

    [Fact]
    public void DefaultLastToDieRotationAllowsOnlyStockKingOfTheHillAndCaptureTheFlag()
    {
        var harvestEntry = CreatePracticeMapEntry("Harvest", GameModeKind.KingOfTheHill);
        var conflictEntry = CreatePracticeMapEntry("Conflict", GameModeKind.CaptureTheFlag);
        var customKothEntry = CreatePracticeMapEntry("downloaded_koth", GameModeKind.KingOfTheHill, isCustomMap: true);
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
        Assert.True((bool)method.Invoke(null, [conflictEntry])!);
        Assert.False((bool)method.Invoke(null, [customKothEntry])!);
    }

    [Fact]
    public void EngineerLastToDieRotationUsesTheSameKothAndCtfEligibility()
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
        Assert.True(InvokeLastToDieRotationEligibility(eligibilityMethod, engineerKind, CreatePracticeMapEntry("Valley", GameModeKind.KingOfTheHill)));
        Assert.False(InvokeLastToDieRotationEligibility(eligibilityMethod, engineerKind, CreatePracticeMapEntry("Dirtbowl", GameModeKind.ControlPoint)));
        Assert.False(InvokeLastToDieRotationEligibility(eligibilityMethod, engineerKind, CreatePracticeMapEntry("Conflict", GameModeKind.CaptureTheFlag, isCustomMap: true)));
    }

    private static IEnumerable<object> BuildPracticeMapEntries()
    {
        var setupStateType = typeof(Game1).GetNestedType("PracticeSetupState", BindingFlags.NonPublic);
        var method = setupStateType?.GetMethod("BuildMapEntries", BindingFlags.Public | BindingFlags.Static);

        Assert.NotNull(method);
        return ((IEnumerable)method.Invoke(null, null)!).Cast<object>();
    }

    private static object CreatePracticeMapEntry(string levelName, GameModeKind mode, bool isCustomMap = false)
    {
        var entryType = typeof(Game1).GetNestedType("PracticeMapEntry", BindingFlags.NonPublic);
        var constructor = entryType?.GetConstructor(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(string), typeof(string), typeof(GameModeKind), typeof(bool), typeof(string)],
            modifiers: null);

        Assert.NotNull(constructor);
        return constructor.Invoke([levelName, levelName, mode, isCustomMap, levelName]);
    }

    private static string GetPracticeMapLevelName(object entry)
    {
        var property = entry.GetType().GetProperty("LevelName", BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(property);
        return (string)property.GetValue(entry)!;
    }

    private static string GetPracticeMapDisplayName(object entry)
    {
        var property = entry.GetType().GetProperty("DisplayName", BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(property);
        return (string)property.GetValue(entry)!;
    }

    private static bool GetPracticeMapIsCustom(object entry)
    {
        var property = entry.GetType().GetProperty("IsCustomMap", BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(property);
        return (bool)property.GetValue(entry)!;
    }

    private static string[] GetPracticeAvailableMapLevelNames(Type setupStateType, object state)
    {
        var method = setupStateType.GetMethod("GetAvailableMapsForDisplay", BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(method);
        return ((IEnumerable)method.Invoke(state, null)!)
            .Cast<object>()
            .Select(GetPracticeMapLevelName)
            .ToArray();
    }

    private static GameModeKind GetPracticeMapMode(object entry)
    {
        var property = entry.GetType().GetProperty("Mode", BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(property);
        return (GameModeKind)property.GetValue(entry)!;
    }

    private static void SetPracticeMapBrowserProperty(Type setupStateType, object state, string propertyName, object? value)
    {
        var property = setupStateType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(property);
        property.SetValue(state, value);
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
