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
        var practiceMapEntryType = typeof(Game1).GetNestedType("PracticeMapEntry", BindingFlags.Public | BindingFlags.NonPublic);
        var method = typeof(Game1).GetMethod(
            "IsEligibleLastToDieRotationMap",
            BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static,
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
        var practiceMapEntryType = typeof(Game1).GetNestedType("PracticeMapEntry", BindingFlags.Public | BindingFlags.NonPublic);
        var eligibilityMethod = typeof(Game1).GetMethod(
            "IsEligibleLastToDieRotationMap",
            BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static,
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
        var setupStateType = typeof(Game1).GetNestedType("PracticeSetupState", BindingFlags.Public | BindingFlags.NonPublic);
        var method = setupStateType?.GetMethod("BuildMapEntries", BindingFlags.Public | BindingFlags.Static);

        Assert.NotNull(method);
        return ((IEnumerable)method.Invoke(null, null)!).Cast<object>();
    }

    private static object CreatePracticeMapEntry(string levelName, GameModeKind mode, bool isCustomMap = false)
    {
        var entryType = typeof(Game1).GetNestedType("PracticeMapEntry", BindingFlags.Public | BindingFlags.NonPublic);
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


    private static void SetPracticeMapBrowserProperty(Type setupStateType, object state, string propertyName, object? value)
    {
        var property = setupStateType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(property);
        property.SetValue(state, value);
    }

    private static object GetLastToDieSurvivorKind(string name)
    {
        var enumType = typeof(Game1).GetNestedType("LastToDieSurvivorKind", BindingFlags.Public | BindingFlags.NonPublic);

        Assert.NotNull(enumType);
        return Enum.Parse(enumType, name);
    }

    private static bool InvokeLastToDieRotationEligibility(MethodInfo method, object survivorKind, object entry)
    {
        return (bool)method.Invoke(null, [survivorKind, entry])!;
    }
}
